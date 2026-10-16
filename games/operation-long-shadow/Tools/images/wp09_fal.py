#!/usr/bin/env python3
"""WP09_ART / WP08 fal driver: images, edits, image-to-3D, PBR materials, transparent sprites, video.

Run with the venv that has fal_client:
  PY=/Users/gencsaracaydin/Documents/fun/.venv/bin/python
  $PY Tools/images/wp09_fal.py nb     IMG_MAP_SARDAHN --tag take_a [--prompt "..."] [--aspect 4:3] [--res 2K] [--seed N]
  $PY Tools/images/wp09_fal.py edit   FAL_SCRAMBLER_BOX --tag ref_noneedle --src RawAssets/wp09/FAL_SCRAMBLER_BOX/ref_t1.png --prompt "..."
  $PY Tools/images/wp09_fal.py 3d     FAL_SCRAMBLER_BOX --tag tripo_t1 --image RawAssets/wp09/FAL_SCRAMBLER_BOX/ref_t1.png [--engine tripo|hy_rapid|hy_pro] [--faces 3000]
  $PY Tools/images/wp09_fal.py patina TEX_ACOUSTIC_TILE --tag patina_t1 [--prompt ...] [--seed N]
  $PY Tools/images/wp09_fal.py gptimg UI_HUD_ICONS --tag gpt_t1 [--prompt ...] [--size 1024x1024] [--quality high]
  $PY Tools/images/wp09_fal.py raw    VID_HARROU_8MM --tag kling_t1 --endpoint <id> --args-json '{...}' --est 0.5
  $PY Tools/images/wp09_fal.py batch  jobs.json [--par 4]      (list of argv lists)
The fal --wp tag is WP08 for UI_* ids and WP09 for everything else (caps in Tools/fal/caps.json). Every paid
call goes through Tools/fal/falcall.py (ledger + caps; exit 2 = cap reached). Outputs land in
RawAssets/<wp08|wp09>/<ID>/<tag>/ (request.json, result.json, timing.json, downloaded files) and the main
file is copied next to it as <tag>.<ext>. Idempotent: a tag whose folder has result.json is skipped unless --force.
"""
import argparse, concurrent.futures as cf, json, os, pathlib, shutil, subprocess, sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
FALCALL = ROOT / "Tools/fal/falcall.py"
PY = "/Users/gencsaracaydin/Documents/fun/.venv/bin/python"
ENV = pathlib.Path("/Users/gencsaracaydin/Documents/fun/.env")

PRICE = {  # list-price estimates (fal llms.txt, 2026-10-16)
    "google/nano-banana-2.1": {"1K": 0.045, "2K": 0.07, "4K": 0.15},
    "google/nano-banana-2.1/edit": {"1K": 0.05, "2K": 0.075, "4K": 0.155},
    "tripo3d/p2/image-to-3d": 1.20,
    "fal-ai/hunyuan-3d/v3.1/rapid/image-to-3d": 0.375,
    "fal-ai/hunyuan-3d/v3.1/pro/image-to-3d": 0.675,
    "fal-ai/patina/material": 0.085,
    "openai/gpt-image-2.5/flare/text-to-image": 0.13,
}
PROP_TAIL = (" Pure seamless white background, soft even diffuse studio lighting, minimal specular highlights, "
             "only a faint contact shadow, everything in sharp focus, museum catalog reference photo. No text, no "
             "lettering, no numbers, no markings, no logos, no serial numbers, no insignia, no flags, no hands, no "
             "other objects.")


def wp_of(asset_id):
    return "WP08" if asset_id.startswith("UI_") else "WP09"


def raw_dir(asset_id):
    return ROOT / "RawAssets" / wp_of(asset_id).lower() / asset_id


def manifest():
    return {e["id"]: e for e in json.loads((ROOT / "Docs/ASSET_MANIFEST.json").read_text())}


def load_key():
    if os.environ.get("FAL_KEY"):
        return
    for line in ENV.read_text().splitlines():
        if line.strip().startswith("FAL_KEY="):
            os.environ["FAL_KEY"] = line.split("=", 1)[1].strip().strip('"').strip("'")
            return
    sys.exit("FAL_KEY missing")


def upload(path):
    """Upload a local file to fal storage and return its URL (cached in <file>.url)."""
    path = pathlib.Path(path)
    if not path.is_absolute():
        path = ROOT / path
    cache = path.with_name(path.name + ".url")
    if cache.exists() and cache.stat().st_mtime >= path.stat().st_mtime:
        return cache.read_text().strip()
    load_key()
    import fal_client
    src = path
    if path.stat().st_size > 6_000_000 and path.suffix.lower() == ".png":
        src = path.with_name(path.stem + "_up.jpg")
        subprocess.run(["sips", "-s", "format", "jpeg", "-s", "formatOptions", "95", str(path), "--out", str(src)],
                       check=True, capture_output=True)
    url = fal_client.upload_file(str(src))
    cache.write_text(url)
    return url


def small_jpeg(path, px=1024, q=88):
    path = pathlib.Path(path)
    out = path.with_name(path.stem + f"_{px}.jpg")
    if not out.exists():
        subprocess.run(["sips", "-Z", str(px), "-s", "format", "jpeg", "-s", "formatOptions", str(q), str(path),
                        "--out", str(out)], check=True, capture_output=True)
    return out


def call(asset_id, endpoint, args, out, est, force=False):
    out = pathlib.Path(out)
    if (out / "result.json").exists() and not force:
        print(f"[skip] {out.relative_to(ROOT)} exists")
        return json.loads((out / "timing.json").read_text()).get("files", [])
    cmd = [PY, str(FALCALL), "--wp", wp_of(asset_id), "--id", asset_id, "--endpoint", endpoint,
           "--args-json", json.dumps(args), "--est-usd", f"{est:.3f}", "--out", str(out)]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode == 2:
        print("CAP EXCEEDED", r.stderr.strip()[-400:])
        sys.exit(2)
    if r.returncode != 0:
        print(f"[fail] {asset_id} {endpoint}: {(r.stderr or r.stdout).strip()[-800:]}")
        return []
    files = json.loads((out / "timing.json").read_text()).get("files", [])
    print(f"[ok] {asset_id} {endpoint} -> {out.relative_to(ROOT)} ({len(files)} files)")
    return files


def copy_main(files, dest_stem, exts):
    hits = [f for f in files if f.lower().endswith(exts)]
    if not hits:
        return None
    hits.sort(key=lambda f: os.path.getsize(f), reverse=True)
    dst = dest_stem.with_suffix(pathlib.Path(hits[0]).suffix.lower())
    shutil.copy(hits[0], dst)
    return dst


IMG_EXT = (".png", ".jpg", ".jpeg", ".webp")


def cmd_nb(a):
    m = manifest().get(a.id, {})
    prompt = a.prompt or m.get("prompt")
    if a.prop_tail:
        prompt = prompt.rstrip() + PROP_TAIL
    args = {"prompt": prompt, "aspect_ratio": a.aspect or "1:1", "resolution": a.res, "output_format": "png",
            "num_images": 1, "limit_generations": True, "thinking_level": a.thinking}
    if a.seed is not None:
        args["seed"] = a.seed
    files = call(a.id, "google/nano-banana-2.1", args, raw_dir(a.id) / a.tag,
                 PRICE["google/nano-banana-2.1"][a.res], a.force)
    print(copy_main(files, raw_dir(a.id) / a.tag, IMG_EXT))


def cmd_edit(a):
    urls = [upload(s) for s in a.src]
    args = {"prompt": a.prompt, "image_urls": urls, "resolution": a.res, "output_format": "png", "num_images": 1,
            "limit_generations": True, "thinking_level": a.thinking}
    if a.aspect:
        args["aspect_ratio"] = a.aspect
    est = PRICE["google/nano-banana-2.1/edit"][a.res] + 0.003 * len(urls)
    files = call(a.id, "google/nano-banana-2.1/edit", args, raw_dir(a.id) / a.tag, est, a.force)
    print(copy_main(files, raw_dir(a.id) / a.tag, IMG_EXT))


def cmd_3d(a):
    img = pathlib.Path(a.image)
    img = img if img.is_absolute() else ROOT / img
    eng = a.engine
    if eng == "tripo":
        url = upload(img)
        args = {"image_url": url, "face_limit": int(a.faces), "texture": True, "pbr": True,
                "texture_quality": "detailed", "delight": True, "export_uv": True, "auto_size": False, "quad": False}
        ep = "tripo3d/p2/image-to-3d"
    elif eng == "hy_rapid":
        url = upload(small_jpeg(img))
        args = {"input_image_url": url, "enable_pbr": False}
        ep = "fal-ai/hunyuan-3d/v3.1/rapid/image-to-3d"
    elif eng == "hy_pro":
        url = upload(img)
        args = {"input_image_url": url, "generate_type": "Normal", "enable_pbr": True, "face_count": int(a.faces or 200000)}
        ep = "fal-ai/hunyuan-3d/v3.1/pro/image-to-3d"
    else:
        sys.exit("unknown engine")
    files = call(a.id, ep, args, raw_dir(a.id) / a.tag, PRICE[ep], a.force)
    print(copy_main(files, raw_dir(a.id) / a.tag, (".glb",)))


def cmd_patina(a):
    m = manifest().get(a.id, {})
    args = {"prompt": a.prompt or m.get("prompt"), "image_size": {"width": 1024, "height": 1024}, "num_images": 1,
            "enable_prompt_expansion": True, "tiling_mode": "both", "output_format": "png",
            "maps": ["basecolor", "normal", "roughness", "metalness", "height"]}
    if a.seed is not None:
        args["seed"] = a.seed
    call(a.id, "fal-ai/patina/material", args, raw_dir(a.id) / a.tag, PRICE["fal-ai/patina/material"], a.force)


def cmd_gptimg(a):
    m = manifest().get(a.id, {})
    w, h = (int(x) for x in a.size.split("x"))
    args = {"prompt": a.prompt or m.get("prompt"), "image_size": {"width": w, "height": h}, "background": a.background,
            "quality": a.quality, "num_images": 1, "output_format": "png"}
    est = PRICE["openai/gpt-image-2.5/flare/text-to-image"] * (w * h) / (1024 * 1024) * (0.3 if a.quality == "medium" else 1)
    files = call(a.id, "openai/gpt-image-2.5/flare/text-to-image", args, raw_dir(a.id) / a.tag, est, a.force)
    print(copy_main(files, raw_dir(a.id) / a.tag, (".png",)))


def cmd_raw(a):
    args = json.loads(a.args_json)
    for k, v in list(args.items()):   # "@path" values are uploaded local files
        if isinstance(v, str) and v.startswith("@"):
            args[k] = upload(v[1:])
    files = call(a.id, a.endpoint, args, raw_dir(a.id) / a.tag, a.est, a.force)
    print(copy_main(files, raw_dir(a.id) / a.tag, (".mp4", ".mov", ".webm", ".glb", ".png", ".jpg", ".wav")))


def parser():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("nb"); p.add_argument("id"); p.add_argument("--tag", required=True); p.add_argument("--prompt")
    p.add_argument("--aspect"); p.add_argument("--res", default="2K"); p.add_argument("--seed", type=int)
    p.add_argument("--thinking", default="medium"); p.add_argument("--prop-tail", action="store_true")
    p.add_argument("--force", action="store_true")
    p = sub.add_parser("edit"); p.add_argument("id"); p.add_argument("--tag", required=True)
    p.add_argument("--src", nargs="+", required=True); p.add_argument("--prompt", required=True); p.add_argument("--aspect")
    p.add_argument("--res", default="2K"); p.add_argument("--thinking", default="medium"); p.add_argument("--force", action="store_true")
    p = sub.add_parser("3d"); p.add_argument("id"); p.add_argument("--tag", required=True); p.add_argument("--image", required=True)
    p.add_argument("--engine", default="tripo"); p.add_argument("--faces", default="3000"); p.add_argument("--force", action="store_true")
    p = sub.add_parser("patina"); p.add_argument("id"); p.add_argument("--tag", required=True); p.add_argument("--prompt")
    p.add_argument("--seed", type=int); p.add_argument("--force", action="store_true")
    p = sub.add_parser("gptimg"); p.add_argument("id"); p.add_argument("--tag", required=True); p.add_argument("--prompt")
    p.add_argument("--size", default="1024x1024"); p.add_argument("--background", default="transparent")
    p.add_argument("--quality", default="high"); p.add_argument("--force", action="store_true")
    p = sub.add_parser("raw"); p.add_argument("id"); p.add_argument("--tag", required=True); p.add_argument("--endpoint", required=True)
    p.add_argument("--args-json", required=True); p.add_argument("--est", type=float, required=True); p.add_argument("--force", action="store_true")
    p = sub.add_parser("batch"); p.add_argument("jobs"); p.add_argument("--par", type=int, default=4)
    return ap


def main():
    a = parser().parse_args()
    if a.cmd == "batch":
        jobs = json.loads(pathlib.Path(a.jobs).read_text())
        with cf.ThreadPoolExecutor(a.par) as ex:
            for f in cf.as_completed([ex.submit(subprocess.run, [PY, __file__] + j) for j in jobs]):
                f.result()
        return
    {"nb": cmd_nb, "edit": cmd_edit, "3d": cmd_3d, "patina": cmd_patina, "gptimg": cmd_gptimg, "raw": cmd_raw}[a.cmd](a)


if __name__ == "__main__":
    main()
