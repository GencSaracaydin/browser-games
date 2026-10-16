#!/usr/bin/env python3
"""Shared fal.ai caller with a spend ledger and per-work-package caps.

Every paid fal call in this project goes through this script so parallel agents
cannot overspend. Run it with the project venv that has fal_client:
  /Users/gencsaracaydin/Documents/fun/.venv/bin/python Tools/fal/falcall.py \
      --wp WP03 --id WPN_AK_ref --endpoint google/nano-banana-2.1 \
      --args args.json --est-usd 0.06 --out RawAssets/wp03/WPN_AK/ref
  ... --args-json '{"prompt": "..."}'      (inline instead of a file)
  falcall.py --report                       (spend per WP)
The key is read from the environment or /Users/gencsaracaydin/Documents/fun/.env
and is never printed. Outputs: <out>/request.json, result.json, timing.json and
every file URL in the result downloaded into <out>/.
Exit codes: 0 ok, 2 cap exceeded (nothing submitted), 3 fal error.
"""
import argparse, fcntl, json, os, pathlib, re, ssl, sys, time, urllib.request, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
HERE = pathlib.Path(__file__).resolve().parent
LEDGER = HERE / "ledger.jsonl"
LOCK = HERE / "ledger.lock"
CAPS = HERE / "caps.json"
ENV = pathlib.Path("/Users/gencsaracaydin/Documents/fun/.env")


def load_key():
    if os.environ.get("FAL_KEY"):
        return
    if ENV.exists():
        for line in ENV.read_text().splitlines():
            line = line.strip()
            if line.startswith("FAL_KEY="):
                os.environ["FAL_KEY"] = line.split("=", 1)[1].strip().strip('"').strip("'")
                return
    sys.exit("FAL_KEY not found in environment or .env")


def read_ledger():
    rows = {}
    if LEDGER.exists():
        for line in LEDGER.read_text().splitlines():
            if not line.strip():
                continue
            r = json.loads(line)
            rows.setdefault(r["uid"], {}).update(r)
    return list(rows.values())


def spent(rows, wp=None):
    total = 0.0
    for r in rows:
        if r.get("status") == "not_submitted":
            continue
        if wp is None or r.get("wp") == wp:
            total += float(r.get("est_usd", 0))
    return total


def append(row):
    with open(LEDGER, "a") as f:
        f.write(json.dumps(row) + "\n")


def report():
    rows = read_ledger()
    caps = json.loads(CAPS.read_text())
    wps = sorted({r.get("wp") for r in rows} | {k for k in caps if not k.startswith("_") and k != "total"})
    print(f"{'WP':10} {'spent':>9} {'cap':>8} {'calls':>6}")
    for wp in wps:
        n = sum(1 for r in rows if r.get("wp") == wp and r.get("status") != "not_submitted")
        print(f"{wp:10} {spent(rows, wp):9.2f} {caps.get(wp, 0):8.0f} {n:6d}")
    print(f"{'TOTAL':10} {spent(rows):9.2f} {caps['total']:8.0f}")


def download_all(res, out):
    ctx = ssl.create_default_context()
    try:
        import certifi
        ctx = ssl.create_default_context(cafile=certifi.where())
    except Exception:
        pass
    urls = []

    def walk(o):
        if isinstance(o, dict):
            for v in o.values():
                walk(v)
        elif isinstance(o, list):
            for v in o:
                walk(v)
        elif isinstance(o, str) and o.startswith("http"):
            urls.append(o)

    walk(res)
    saved = []
    for i, u in enumerate(dict.fromkeys(urls)):
        fn = re.sub(r"[^A-Za-z0-9._-]", "_", u.split("/")[-1].split("?")[0])[-100:] or f"file_{i}"
        dest = out / f"{i:02d}_{fn}"
        for attempt in range(3):
            try:
                req = urllib.request.Request(u, headers={"User-Agent": "curl/8"})
                with urllib.request.urlopen(req, context=ctx, timeout=600) as r, open(dest, "wb") as f:
                    f.write(r.read())
                saved.append(str(dest))
                break
            except Exception as e:  # noqa: BLE001
                if attempt == 2:
                    print("DOWNLOAD FAILED", u, e, file=sys.stderr)
                time.sleep(3)
    return saved


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--wp")
    ap.add_argument("--id", help="ASSET_MANIFEST id or a descriptive tag")
    ap.add_argument("--endpoint")
    ap.add_argument("--args", help="path to JSON arguments")
    ap.add_argument("--args-json", help="inline JSON arguments")
    ap.add_argument("--est-usd", type=float, help="estimated cost of this call from the model's pricing")
    ap.add_argument("--out", help="output folder (relative to project root or absolute)")
    ap.add_argument("--timeout", type=int, default=1800)
    a = ap.parse_args()
    if a.report:
        report()
        return
    for k in ("wp", "id", "endpoint", "est_usd", "out"):
        if getattr(a, k) is None:
            ap.error(f"--{k.replace('_', '-')} is required")
    args = json.loads(a.args_json) if a.args_json else json.loads(pathlib.Path(a.args).read_text())
    out = pathlib.Path(a.out)
    if not out.is_absolute():
        out = ROOT / out
    out.mkdir(parents=True, exist_ok=True)

    uid = uuid.uuid4().hex[:12]
    caps = json.loads(CAPS.read_text())
    LOCK.touch()
    with open(LOCK) as lk:
        fcntl.flock(lk, fcntl.LOCK_EX)
        rows = read_ledger()
        wp_cap = float(caps.get(a.wp, 0))
        if spent(rows, a.wp) + a.est_usd > wp_cap + 1e-9:
            print(f"CAP EXCEEDED for {a.wp}: spent {spent(rows, a.wp):.2f} + {a.est_usd:.2f} > {wp_cap:.2f}", file=sys.stderr)
            sys.exit(2)
        if spent(rows) + a.est_usd > float(caps["total"]) + 1e-9:
            print(f"TOTAL CAP EXCEEDED: {spent(rows):.2f} + {a.est_usd:.2f} > {caps['total']}", file=sys.stderr)
            sys.exit(2)
        append({"uid": uid, "ts": time.strftime("%Y-%m-%dT%H:%M:%S"), "wp": a.wp, "id": a.id,
                "endpoint": a.endpoint, "est_usd": a.est_usd, "status": "reserved", "out": str(out)})
        fcntl.flock(lk, fcntl.LOCK_UN)

    load_key()
    import fal_client
    (out / "request.json").write_text(json.dumps({"endpoint": a.endpoint, "arguments": args}, indent=2))
    t0 = time.time()
    try:
        handle = None
        for attempt in range(3):
            try:
                handle = fal_client.submit(a.endpoint, arguments=args)
                break
            except Exception as e:  # noqa: BLE001
                if attempt == 2:
                    raise
                print("submit retry:", e, file=sys.stderr)
                time.sleep(5)
        rid = handle.request_id
        append({"uid": uid, "status": "submitted", "request_id": rid})
        print("request_id", rid, flush=True)
        while True:
            st = handle.status()
            if isinstance(st, fal_client.Completed):
                break
            if time.time() - t0 > a.timeout:
                raise TimeoutError(f"timed out after {a.timeout}s (request {rid})")
            time.sleep(3)
        res = handle.get()
    except Exception as e:  # noqa: BLE001
        status = "not_submitted" if handle is None else "failed"
        append({"uid": uid, "status": status, "error": str(e)[:500], "wall_s": round(time.time() - t0, 1)})
        print("FAL ERROR:", e, file=sys.stderr)
        sys.exit(3)
    wall = round(time.time() - t0, 1)
    (out / "result.json").write_text(json.dumps(res, indent=2))
    saved = download_all(res, out)
    (out / "timing.json").write_text(json.dumps({"request_id": rid, "wall_seconds": wall, "files": saved}, indent=2))
    append({"uid": uid, "status": "done", "request_id": rid, "wall_s": wall, "files": len(saved)})
    print(json.dumps({"ok": True, "request_id": rid, "wall_seconds": wall, "out": str(out), "files": saved}, indent=1))


if __name__ == "__main__":
    main()
