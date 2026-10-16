"""Run fal jobs for WP06_SFX through Tools/fal/falcall.py (ledger + caps), in parallel, idempotently.

A job is {id, take, endpoint, args, est_usd, out}. A job whose <out>/timing.json lists downloaded files is
skipped (pass force=True to redo). After each call a provenance.json is written next to the outputs
(source, endpoint, arguments, seeds, request id, wall time, list price, license).

Also: upload(path) -> fal storage URL (free; used for music audio_reference and audio-understanding checks),
cached by content hash in RawAssets/audio/sfx/_uploads.json. The key is never printed.
"""
from __future__ import annotations

import concurrent.futures as cf
import hashlib
import json
import os
import pathlib
import subprocess
import sys
import time

ROOT = pathlib.Path("/Users/gencsaracaydin/Documents/fun/fun-unity")
FALPY = "/Users/gencsaracaydin/Documents/fun/.venv/bin/python"
FALCALL = ROOT / "Tools/fal/falcall.py"
WP = "WP06SFX"
UPLOAD_CACHE = ROOT / "RawAssets/audio/sfx/_uploads.json"

LICENSE = {
    "mirelo-ai/sfx2.0/text-to-audio": "fal / Mirelo generated output, commercial use per fal terms",
    "fal-ai/elevenlabs/sound-effects/v2": "fal / ElevenLabs generated output, commercial use per fal terms",
    "elevenlabs/music/v2.5": "fal / ElevenLabs Music generated output, commercial use per fal terms",
    "fal-ai/audio-understanding": "analysis only (no asset)",
}


def done(out: pathlib.Path) -> bool:
    t = out / "timing.json"
    if not t.exists():
        return False
    try:
        return len(json.loads(t.read_text()).get("files", [])) > 0
    except Exception:
        return False


def run_one(job, force=False, retries=1):
    out = pathlib.Path(job["out"])
    if not out.is_absolute():
        out = ROOT / out
    if done(out) and not force:
        return {"job": job["id"] + "/" + job.get("take", ""), "status": "cached", "out": str(out)}
    out.mkdir(parents=True, exist_ok=True)
    cmd = [FALPY, str(FALCALL), "--wp", WP, "--id", job["id"], "--endpoint", job["endpoint"],
           "--args-json", json.dumps(job["args"]), "--est-usd", f"{job['est_usd']:.4f}", "--out", str(out)]
    for attempt in range(retries + 1):
        t0 = time.time()
        p = subprocess.run(cmd, capture_output=True, text=True, cwd=str(ROOT))
        if p.returncode == 0:
            break
        if p.returncode == 2:
            return {"job": job["id"], "status": "CAP", "err": p.stderr[-300:]}
        time.sleep(4)
    if p.returncode != 0:
        return {"job": job["id"] + "/" + job.get("take", ""), "status": "ERROR", "err": p.stderr[-600:]}
    timing = json.loads((out / "timing.json").read_text())
    res = json.loads((out / "result.json").read_text()) if (out / "result.json").exists() else {}
    prov = {
        "manifest_id": job["id"], "take": job.get("take"), "source": "fal", "endpoint": job["endpoint"],
        "arguments": job["args"], "seeds": res.get("seeds") or res.get("seed"), "request_id": timing.get("request_id"),
        "wall_seconds": timing.get("wall_seconds"), "list_price_usd": job["est_usd"],
        "license": LICENSE.get(job["endpoint"], "fal generated output"), "files": timing.get("files"),
        "generated_at": time.strftime("%Y-%m-%dT%H:%M:%S"), "wp": "WP06_SFX",
    }
    (out / "provenance.json").write_text(json.dumps(prov, indent=1))
    return {"job": job["id"] + "/" + job.get("take", ""), "status": "ok", "out": str(out), "wall": timing.get("wall_seconds")}


def run_jobs(jobs, workers=6, force=False, log=True):
    results = []
    with cf.ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(run_one, j, force): j for j in jobs}
        for f in cf.as_completed(futs):
            r = f.result()
            results.append(r)
            if log:
                print(json.dumps(r), flush=True)
            if r["status"] == "CAP":
                print("CAP HIT - stopping new submissions", file=sys.stderr)
    return results


def files_of(out, exts=(".wav", ".mp3", ".pcm", ".flac")):
    out = pathlib.Path(out)
    if not out.is_absolute():
        out = ROOT / out
    if not done(out):
        return []
    fs = json.loads((out / "timing.json").read_text()).get("files", [])
    return [pathlib.Path(f) for f in fs if pathlib.Path(f).suffix.lower() in exts or "audio" in pathlib.Path(f).name]


def _load_key():
    if os.environ.get("FAL_KEY"):
        return
    env = pathlib.Path("/Users/gencsaracaydin/Documents/fun/.env")
    for line in env.read_text().splitlines():
        line = line.strip()
        if line.startswith("FAL_KEY="):
            os.environ["FAL_KEY"] = line.split("=", 1)[1].strip().strip('"').strip("'")
            return


def upload(path) -> str:
    """Upload a local file to fal storage (free) and return its URL; cached by sha1."""
    path = pathlib.Path(path)
    h = hashlib.sha1(path.read_bytes()).hexdigest()
    cache = json.loads(UPLOAD_CACHE.read_text()) if UPLOAD_CACHE.exists() else {}
    if h in cache:
        return cache[h]["url"]
    code = (
        "import os,sys,json,pathlib\n"
        "env=pathlib.Path('/Users/gencsaracaydin/Documents/fun/.env')\n"
        "if not os.environ.get('FAL_KEY'):\n"
        "  for l in env.read_text().splitlines():\n"
        "    l=l.strip()\n"
        "    if l.startswith('FAL_KEY='): os.environ['FAL_KEY']=l.split('=',1)[1].strip().strip(chr(34)).strip(chr(39))\n"
        "import fal_client\n"
        "print(fal_client.upload_file(sys.argv[1]))\n"
    )
    p = subprocess.run([FALPY, "-c", code, str(path)], capture_output=True, text=True)
    if p.returncode != 0:
        raise RuntimeError("upload failed: " + p.stderr[-300:])
    url = p.stdout.strip().splitlines()[-1]
    cache[h] = {"url": url, "path": str(path), "ts": time.strftime("%Y-%m-%dT%H:%M:%S")}
    UPLOAD_CACHE.parent.mkdir(parents=True, exist_ok=True)
    UPLOAD_CACHE.write_text(json.dumps(cache, indent=1))
    return url


# ---------------------------------------------------------------- pricing helpers (list prices 2026-10-16)
def est_mirelo(duration, n):
    return round(0.010 * duration * n + 1e-9, 4)  # promo $0.010/s/sample until 2026-12-13


def est_el_sfx(duration):
    return round(0.002 * duration + 1e-9, 4)


def est_el_music(ms):
    import math
    return round(0.60 * math.ceil(ms / 60000), 2)


def est_understanding(seconds):
    import math
    return round(0.01 * math.ceil(max(seconds, 0.1) / 5), 2)
