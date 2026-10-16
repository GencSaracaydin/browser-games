#!/usr/bin/env python3
"""Download Microsoft Rocketbox avatars / animations (MIT) file by file from GitHub raw.

The repo is 24 GB, so it is never cloned. A cached git tree listing
(RawAssets/wp05/_downloads/gh/tree.json, one GitHub API call) maps names to paths.

Usage (project venv or any python3 with certifi):
  Tools/.venv/bin/python Tools/characters/fetch_rocketbox.py avatar Military_Male_02 Female_Adult_07 ...
  Tools/.venv/bin/python Tools/characters/fetch_rocketbox.py anim m_sit_table_idle_01 ... [--variant static|xy|xyz]
  Tools/.venv/bin/python Tools/characters/fetch_rocketbox.py list-anims [regex]
Outputs:
  RawAssets/wp05/rocketbox/<Avatar>/{Export/<Avatar>.fbx, Textures/*.tga, <Avatar>.png, provenance.json}
  RawAssets/wp05/rocketbox/Animations/<variant>/<clip>.max.fbx (+ provenance.json)
Idempotent: files whose size matches the tree entry are skipped (--force to redo).
The *_facial.fbx sample is skipped unless --facial is given.
"""
import argparse, json, pathlib, re, ssl, sys, time, urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[2]
TREE = ROOT / "RawAssets/wp05/_downloads/gh/tree.json"
OUT = ROOT / "RawAssets/wp05/rocketbox"
RAW = "https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/master/"
API_TREE = "https://api.github.com/repos/microsoft/Microsoft-Rocketbox/git/trees/master?recursive=1"
UA = {"User-Agent": "Mozilla/5.0 (LongShadow WP05 asset fetch)"}


def ctx():
    try:
        import certifi
        return ssl.create_default_context(cafile=certifi.where())
    except Exception:
        return ssl.create_default_context()


def get(url, dest=None):
    for attempt in range(4):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), context=ctx(), timeout=600) as r:
                data = r.read()
            if dest:
                dest.parent.mkdir(parents=True, exist_ok=True)
                dest.write_bytes(data)
            return data
        except Exception as e:  # noqa: BLE001
            if attempt == 3:
                raise
            print("retry", url, e, file=sys.stderr)
            time.sleep(3 * (attempt + 1))


def tree():
    if not TREE.exists():
        TREE.parent.mkdir(parents=True, exist_ok=True)
        get(API_TREE, TREE)
    return [t for t in json.loads(TREE.read_text())["tree"] if t["type"] == "blob"]


def fetch(entries, base, force=False):
    got = []
    for e in entries:
        dest = base / e["rel"]
        if dest.exists() and dest.stat().st_size == e["size"] and not force:
            continue
        print("GET", e["path"], f"{e['size']/1e6:.1f} MB")
        get(RAW + urllib.request.quote(e["path"]), dest)
        got.append(e["path"])
    return got


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["avatar", "anim", "list-anims"])
    ap.add_argument("names", nargs="*")
    ap.add_argument("--variant", default="static", choices=["static", "xy", "xyz"])
    ap.add_argument("--facial", action="store_true")
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    t = tree()
    if a.mode == "list-anims":
        rx = re.compile(a.names[0] if a.names else ".")
        for e in t:
            if e["path"].startswith("Assets/Animations/all_animations_max_motextr_static/") and rx.search(e["path"]):
                print(e["path"].split("/")[-1], e["size"])
        return
    if a.mode == "avatar":
        for name in a.names:
            pre = None
            for e in t:
                m = re.match(r"Assets/Avatars/(Adults|Children|Professions)/%s/" % re.escape(name), e["path"])
                if m:
                    pre = m.group(0)
                    break
            if not pre:
                sys.exit(f"avatar {name} not found in tree")
            ents = []
            for e in t:
                if not e["path"].startswith(pre):
                    continue
                rel = e["path"][len(pre):]
                if rel.endswith(".max") or rel.lower().endswith(".max.fbx") and "Export" not in rel:
                    continue
                if rel.startswith("Export/") and rel.endswith("_facial.fbx") and not a.facial:
                    continue
                if rel.startswith("Export/") or rel.startswith("Textures/") or rel == f"{name}.png":
                    ents.append(dict(e, rel=rel))
            base = OUT / name
            got = fetch(ents, base, a.force)
            prov = {"source": "Microsoft Rocketbox (github.com/microsoft/Microsoft-Rocketbox)", "license": "MIT",
                    "repo_path": pre, "files": [e["rel"] for e in ents], "fetched": time.strftime("%Y-%m-%dT%H:%M:%S"),
                    "downloaded_now": got, "tool": "Tools/characters/fetch_rocketbox.py"}
            (base / "provenance.json").write_text(json.dumps(prov, indent=1))
            print(name, "ok", len(ents), "files")
    if a.mode == "anim":
        folder = f"Assets/Animations/all_animations_max_motextr_{a.variant}/"
        ents = []
        for name in a.names:
            hit = [e for e in t if e["path"].startswith(folder) and e["path"].split("/")[-1].split(".")[0] == name]
            if not hit:
                print("anim not found", name, file=sys.stderr)
            for e in hit:
                ents.append(dict(e, rel=e["path"].split("/")[-1]))
        base = OUT / "Animations" / a.variant
        got = fetch(ents, base, a.force)
        provp = base / "provenance.json"
        prov = json.loads(provp.read_text()) if provp.exists() else {"source": "Microsoft Rocketbox animations", "license": "MIT", "files": []}
        prov["files"] = sorted(set(prov["files"]) | {e["rel"] for e in ents})
        prov["fetched"] = time.strftime("%Y-%m-%dT%H:%M:%S")
        provp.write_text(json.dumps(prov, indent=1))
        print("anims ok", len(ents), "downloaded", len(got))


if __name__ == "__main__":
    main()
