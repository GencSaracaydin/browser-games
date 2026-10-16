#!/usr/bin/env python3
"""WP03 weapon fal jobs. Every paid call goes through Tools/fal/falcall.py (ledger + caps).

Run with the fal venv:
  PY=/Users/gencsaracaydin/Documents/fun/.venv/bin/python
  $PY Tools/mesh/weapons/wpn_fal.py ref WPN_COMMANDO                # Nano Banana 2.1 2K reference (left side)
  $PY Tools/mesh/weapons/wpn_fal.py edit WPN_COMMANDO nomag          # edit of ref_left: magazine removed
  $PY Tools/mesh/weapons/wpn_fal.py edit WPN_COMMANDO magonly        # edit of ref_left: magazine only
  $PY Tools/mesh/weapons/wpn_fal.py edit WPN_COMMANDO right --src nomag
  $PY Tools/mesh/weapons/wpn_fal.py tripo WPN_COMMANDO --src nomag --faces 20000 --tag tripo_take1
  $PY Tools/mesh/weapons/wpn_fal.py hunyuan WPN_COMMANDO --src nomag --back right --faces 40000 --tag hy_take1
  $PY Tools/mesh/weapons/wpn_fal.py upload path/to/image.png
Sources (--src) are step folders under RawAssets/wp03/<ID>/ (their first image) or a local file path.
Every step is idempotent: an existing result.json skips the call (use --force to redo).
"""
import argparse, json, os, pathlib, subprocess, sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
SPEC = json.loads((pathlib.Path(__file__).resolve().parent / "weapons.json").read_text())
FALCALL = ROOT / "Tools/fal/falcall.py"
PY = "/Users/gencsaracaydin/Documents/fun/.venv/bin/python"
RAW = ROOT / "RawAssets/wp03"
UPLOADS = RAW / "_uploads.json"
ENV = pathlib.Path("/Users/gencsaracaydin/Documents/fun/.env")
EST = {"google/nano-banana-2.1": 0.08, "google/nano-banana-2.1/edit": 0.09,
       "tripo3d/p2/image-to-3d": 1.20, "fal-ai/hunyuan-3d/v3.1/pro/image-to-3d": 0.825}


def load_key():
    if os.environ.get("FAL_KEY"):
        return
    for line in ENV.read_text().splitlines():
        if line.strip().startswith("FAL_KEY="):
            os.environ["FAL_KEY"] = line.split("=", 1)[1].strip().strip('"').strip("'")
            return
    sys.exit("FAL_KEY missing")


def upload(path):
    path = pathlib.Path(path).resolve()
    cache = json.loads(UPLOADS.read_text()) if UPLOADS.exists() else {}
    key = f"{path}:{path.stat().st_mtime_ns}"
    if key in cache:
        return cache[key]
    load_key()
    import fal_client
    url = fal_client.upload_file(str(path))
    cache[key] = url
    UPLOADS.write_text(json.dumps(cache, indent=1))
    return url


def step_image_url(wid, step):
    """URL of the first image produced by a step folder (from result.json), or upload a local file."""
    p = pathlib.Path(step)
    if p.suffix.lower() in (".png", ".jpg", ".jpeg", ".webp") and p.exists():
        return upload(p)
    if step == "ref_left" and SPEC["weapons"][wid].get("ref_reuse_url"):
        return SPEC["weapons"][wid]["ref_reuse_url"]
    res = json.loads((RAW / wid / step / "result.json").read_text())
    return res["images"][0]["url"]


def call(wid, endpoint, args, step, force=False):
    out = RAW / wid / step
    if (out / "result.json").exists() and not force:
        print(f"skip {wid}/{step} (exists)")
        return 0
    cmd = [PY, str(FALCALL), "--wp", "WP03", "--id", wid, "--endpoint", endpoint,
           "--args-json", json.dumps(args), "--est-usd", str(EST[endpoint]), "--out", str(out)]
    r = subprocess.run(cmd)
    if r.returncode == 2:
        print("CAP REACHED", file=sys.stderr)
    return r.returncode


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["ref", "edit", "tripo", "hunyuan", "upload"])
    ap.add_argument("id")
    ap.add_argument("kind", nargs="?")
    ap.add_argument("--src", default="ref_left")
    ap.add_argument("--back")
    ap.add_argument("--faces", type=int, default=20000)
    ap.add_argument("--tag")
    ap.add_argument("--prompt", help="override prompt (edit/ref)")
    ap.add_argument("--seed", type=int)
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    if a.cmd == "upload":
        print(upload(a.id))
        return
    w = SPEC["weapons"][a.id]
    if a.cmd == "ref":
        tail = SPEC["tail_34"] if w.get("view") == "34" else SPEC["tail_side"]
        prompt = a.prompt or (w["prompt"] + tail)
        args = {"prompt": prompt, "aspect_ratio": w.get("aspect", "16:9"), "resolution": "2K",
                "output_format": "png", "num_images": 1, "limit_generations": True}
        if a.seed is not None:
            args["seed"] = a.seed
        sys.exit(call(a.id, "google/nano-banana-2.1", args, a.tag or "ref_left", a.force))
    if a.cmd == "edit":
        prompt = a.prompt or SPEC["edit_" + a.kind]
        src = a.src
        args = {"prompt": prompt, "image_urls": [step_image_url(a.id if "parent" not in w else w["parent"], src)],
                "aspect_ratio": "auto", "resolution": "2K", "output_format": "png", "num_images": 1,
                "limit_generations": True}
        if a.seed is not None:
            args["seed"] = a.seed
        sys.exit(call(a.id, "google/nano-banana-2.1/edit", args, a.tag or a.kind, a.force))
    if a.cmd == "tripo":
        src_id = w.get("parent", a.id) if not pathlib.Path(a.src).exists() else a.id
        args = {"image_url": step_image_url(src_id, a.src), "face_limit": a.faces, "texture": True, "pbr": True,
                "texture_quality": "detailed", "delight": True, "export_uv": True, "auto_size": False, "quad": False}
        if a.seed is not None:
            args["model_seed"] = a.seed
        sys.exit(call(a.id, "tripo3d/p2/image-to-3d", args, a.tag or "tripo_take1", a.force))
    if a.cmd == "hunyuan":
        args = {"input_image_url": step_image_url(a.id, a.src), "generate_type": "Normal", "enable_pbr": True,
                "face_count": max(40000, a.faces)}
        if a.back:
            args["back_image_url"] = step_image_url(a.id, a.back)
        sys.exit(call(a.id, "fal-ai/hunyuan-3d/v3.1/pro/image-to-3d", args, a.tag or "hy_take1", a.force))


if __name__ == "__main__":
    main()
