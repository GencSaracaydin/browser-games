#!/usr/bin/env python3
"""WP10_FAL driver: reference images, image-to-3D, textures and decals through Tools/fal/falcall.py.

Run with the venv that has fal_client:
  PY=/Users/gencsaracaydin/Documents/fun/.venv/bin/python
  $PY Tools/mesh/props/wp10fal.py ref  FAL_SANDBAG [--take 2] [--prompt "..."] [--aspect 1:1]
  $PY Tools/mesh/props/wp10fal.py edit FAL_SANDBAG --src RawAssets/wp10/fal/FAL_SANDBAG/ref_t1.png --tag noant --prompt "..."
  $PY Tools/mesh/props/wp10fal.py 3d   FAL_SANDBAG --image RawAssets/wp10/fal/FAL_SANDBAG/ref_t1.png --tag tripo_t1 [--engine tripo|hy_rapid|hy_pro] [--faces 600]
  $PY Tools/mesh/props/wp10fal.py patina TEX_SALTCRUST [--take 1] [--prompt ...]
  $PY Tools/mesh/props/wp10fal.py gptimg DCL_SCORCH [--take 1] [--prompt ...] [--size 1024x1024]
  $PY Tools/mesh/props/wp10fal.py batch jobs.json        (list of {cmd, id, ...}; runs up to --par in parallel)
Every paid call goes through Tools/fal/falcall.py with --wp WP10FAL (ledger + cap). Outputs land in
RawAssets/wp10/fal/<ID>/<tag>/ and the main file is copied next to it as <tag>.<ext>.
Idempotent: a tag whose output folder already has result.json is skipped unless --force.
"""
import argparse, concurrent.futures as cf, json, os, pathlib, shutil, subprocess, sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
FALCALL = ROOT / "Tools/fal/falcall.py"
RAW = ROOT / "RawAssets/wp10/fal"
CFG = pathlib.Path(__file__).resolve().parent / "wp10fal_assets.json"
PY = "/Users/gencsaracaydin/Documents/fun/.venv/bin/python"
ENV = pathlib.Path("/Users/gencsaracaydin/Documents/fun/.env")
WP = "WP10FAL"

PRICE = {  # honest list-price estimates (2026-10-16 llms.txt)
    "google/nano-banana-2.1": 0.07,           # 2K ~1.7k output tokens @ $35.3/M + thinking
    "google/nano-banana-2.1/edit": 0.075,     # + input image tokens
    "tripo3d/p2/image-to-3d": 1.20,           # detailed textures
    "fal-ai/hunyuan-3d/v3.1/rapid/image-to-3d": 0.375,  # + PBR
    "fal-ai/hunyuan-3d/v3.1/pro/image-to-3d": 0.675,    # + PBR + custom face count (+0.15 multiview)
    "fal-ai/patina/material": 0.085,          # 1K, 5 maps, prompt expansion
    "openai/gpt-image-2.5/flare/text-to-image": 0.13,  # high, 1024^2, transparent
    "fal-ai/bria/background/remove": 0.018,
}

TEMPLATE_TAIL = (" Pure seamless white background, soft even diffuse studio lighting, minimal specular highlights, "
                 "only a faint contact shadow, everything in sharp focus, museum catalog reference photo. No text, no "
                 "lettering, no numbers, no markings, no logos, no serial numbers, no insignia, no flags, no hands, no "
                 "other objects.")


def load_key():
    if os.environ.get("FAL_KEY"):
        return
    for line in ENV.read_text().splitlines():
        if line.strip().startswith("FAL_KEY="):
            os.environ["FAL_KEY"] = line.split("=", 1)[1].strip().strip('"').strip("'")
            return
    sys.exit("FAL_KEY missing")


def cfg():
    return json.loads(CFG.read_text())


def manifest():
    return {e["id"]: e for e in json.loads((ROOT / "Docs/ASSET_MANIFEST.json").read_text())}


def small_jpeg(path, px=1024, q=85):
    """1024 px JPEG copy (Hunyuan Rapid rejects inputs over ~950 KB encoded)."""
    path = pathlib.Path(path)
    out = path.with_name(path.stem + f"_{px}.jpg")
    if not out.exists():
        subprocess.run(["sips", "-Z", str(px), "-s", "format", "jpeg", "-s", "formatOptions", str(q), str(path),
                        "--out", str(out)], check=True, capture_output=True)
    return out


def upload(path):
    """Upload a local file to fal storage (free) and return its URL; cached in <file>.url."""
    path = pathlib.Path(path)
    cache = path.with_suffix(path.suffix + ".url")
    if cache.exists() and cache.stat().st_mtime >= path.stat().st_mtime:
        return cache.read_text().strip()
    load_key()
    import fal_client
    src = path
    if path.stat().st_size > 6_000_000 and path.suffix.lower() == ".png":
        # keep under the 8 MB limit of Hunyuan; high-quality JPEG
        from_png = path.with_name(path.stem + "_up.jpg")
        subprocess.run(["sips", "-s", "format", "jpeg", "-s", "formatOptions", "95", str(path), "--out", str(from_png)],
                       check=True, capture_output=True)
        src = from_png
    url = fal_client.upload_file(str(src))
    cache.write_text(url)
    return url


def call(asset_id, endpoint, args, out, est=None, force=False):
    out = pathlib.Path(out)
    if (out / "result.json").exists() and not force:
        print(f"[skip] {out.relative_to(ROOT)} exists")
        return json.loads((out / "timing.json").read_text()).get("files", [])
    est = PRICE.get(endpoint, 0.1) if est is None else est
    cmd = [PY, str(FALCALL), "--wp", WP, "--id", asset_id, "--endpoint", endpoint, "--args-json", json.dumps(args),
           "--est-usd", f"{est:.3f}", "--out", str(out)]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode == 2:
        print("CAP EXCEEDED", r.stderr.strip())
        sys.exit(2)
    if r.returncode != 0:
        print(f"[fail] {asset_id} {endpoint}: {r.stderr.strip()[-600:]}")
        return []
    files = json.loads((out / "timing.json").read_text()).get("files", [])
    print(f"[ok] {asset_id} {endpoint} -> {out.relative_to(ROOT)} ({len(files)} files)")
    return files


def build_ref_prompt(asset_id, c, m):
    if c.get("ref_prompt"):
        p = c["ref_prompt"]
        if not c.get("ref_prompt_raw"):
            p = p.rstrip() + TEMPLATE_TAIL
        return p
    return m[asset_id]["prompt"]


def cmd_ref(a):
    C, M = cfg(), manifest()
    c = C.get(a.id, {})
    prompt = a.prompt or build_ref_prompt(a.id, c, M)
    aspect = a.aspect or c.get("aspect", "1:1")
    tag = a.tag or f"ref_t{a.take}"
    out = RAW / a.id / tag
    args = {"prompt": prompt, "aspect_ratio": aspect, "resolution": "2K", "output_format": "png", "num_images": 1,
            "limit_generations": True}
    files = call(a.id, "google/nano-banana-2.1", args, out, force=a.force)
    pngs = [f for f in files if f.lower().endswith((".png", ".jpg", ".jpeg", ".webp"))]
    if pngs:
        shutil.copy(pngs[0], RAW / a.id / f"{tag}.png")


def cmd_edit(a):
    srcs = [upload(ROOT / s if not os.path.isabs(s) else s) for s in a.src]
    out = RAW / a.id / a.tag
    args = {"prompt": a.prompt, "image_urls": srcs, "resolution": "2K", "output_format": "png", "num_images": 1,
            "limit_generations": True}
    if a.aspect:
        args["aspect_ratio"] = a.aspect
    files = call(a.id, "google/nano-banana-2.1/edit", args, out, force=a.force)
    pngs = [f for f in files if f.lower().endswith((".png", ".jpg", ".jpeg", ".webp"))]
    if pngs:
        shutil.copy(pngs[0], RAW / a.id / f"{a.tag}.png")


def cmd_3d(a):
    C = cfg()
    c = C.get(a.id, {})
    img = pathlib.Path(a.image)
    if not img.is_absolute():
        img = ROOT / img
    engine = a.engine or c.get("engine", "tripo")
    url = upload(small_jpeg(img) if engine == "hy_rapid" else img)
    out = RAW / a.id / a.tag
    if engine == "tripo":
        faces = int(a.faces or c.get("gen_faces") or c.get("tris"))
        args = {"image_url": url, "face_limit": faces, "texture": True, "pbr": True, "texture_quality": "detailed",
                "delight": True, "export_uv": True, "auto_size": False, "quad": False}
        ep = "tripo3d/p2/image-to-3d"
    elif engine == "hy_rapid":
        # enable_pbr False: with PBR on, the API's single "texture" output is sometimes the metallic map (no base colour);
        # without it the texture is always the base colour (roughness/metal are set flat at processing)
        args = {"input_image_url": url, "enable_pbr": False}
        ep = "fal-ai/hunyuan-3d/v3.1/rapid/image-to-3d"
    elif engine == "hy_pro":
        args = {"input_image_url": url, "generate_type": "Normal", "enable_pbr": True,
                "face_count": int(a.faces or 200000)}
        ep = "fal-ai/hunyuan-3d/v3.1/pro/image-to-3d"
        est = PRICE[ep]
        for view in (a.views or []):
            k, p = view.split("=", 1)
            pp = pathlib.Path(p)
            args[f"{k}_image_url"] = upload(pp if pp.is_absolute() else ROOT / pp)
        if a.views:
            est += 0.15
        files = call(a.id, ep, args, out, est=est, force=a.force)
        return finish_3d(a, out, files)
    else:
        sys.exit("unknown engine " + engine)
    files = call(a.id, ep, args, out, force=a.force)
    finish_3d(a, out, files)


def finish_3d(a, out, files):
    glbs = [f for f in files if f.lower().endswith(".glb")]
    if glbs:
        # prefer the largest GLB (Tripo returns pbr model + base model variants)
        glbs.sort(key=lambda f: os.path.getsize(f), reverse=True)
        shutil.copy(glbs[0], RAW / a.id / f"{a.tag}.glb")


def cmd_patina(a):
    M = manifest()
    C = cfg()
    c = C.get(a.id, {})
    prompt = a.prompt or c.get("patina_prompt") or M[a.id]["prompt"]
    tag = a.tag or f"patina_t{a.take}"
    args = {"prompt": prompt, "image_size": {"width": 1024, "height": 1024}, "num_images": 1,
            "enable_prompt_expansion": True, "tiling_mode": "both", "output_format": "png",
            "maps": ["basecolor", "normal", "roughness", "metalness", "height"]}
    if a.seed is not None:
        args["seed"] = a.seed
    call(a.id, "fal-ai/patina/material", args, RAW / a.id / tag, force=a.force)


def cmd_gptimg(a):
    M = manifest()
    C = cfg()
    c = C.get(a.id, {})
    prompt = a.prompt or c.get("gpt_prompt") or M[a.id]["prompt"]
    tag = a.tag or f"gpt_t{a.take}"
    w, h = (a.size or c.get("gpt_size", "1024x1024")).split("x")
    args = {"prompt": prompt, "image_size": {"width": int(w), "height": int(h)}, "background": a.background,
            "quality": a.quality, "num_images": 1, "output_format": "png"}
    est = PRICE["openai/gpt-image-2.5/flare/text-to-image"] * (int(w) * int(h)) / (1024 * 1024)
    if a.quality == "medium":
        est *= 0.3
    files = call(a.id, "openai/gpt-image-2.5/flare/text-to-image", args, RAW / a.id / tag, est=est, force=a.force)
    pngs = [f for f in files if f.lower().endswith(".png")]
    if pngs:
        shutil.copy(pngs[0], RAW / a.id / f"{tag}.png")


def cmd_nb(a):
    """Plain nano-banana text-to-image (posters, concept frames) with explicit prompt/aspect."""
    tag = a.tag
    args = {"prompt": a.prompt, "aspect_ratio": a.aspect or "1:1", "resolution": a.res, "output_format": "png",
            "num_images": 1, "limit_generations": True}
    est = {"1K": 0.045, "2K": 0.07, "4K": 0.15}[a.res]
    files = call(a.id, "google/nano-banana-2.1", args, RAW / a.id / tag, est=est, force=a.force)
    pngs = [f for f in files if f.lower().endswith((".png", ".jpg", ".jpeg", ".webp"))]
    if pngs:
        shutil.copy(pngs[0], RAW / a.id / f"{tag}.png")


def parser():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("ref"); p.add_argument("id"); p.add_argument("--take", type=int, default=1)
    p.add_argument("--tag"); p.add_argument("--prompt"); p.add_argument("--aspect"); p.add_argument("--force", action="store_true")
    p = sub.add_parser("edit"); p.add_argument("id"); p.add_argument("--src", nargs="+", required=True)
    p.add_argument("--tag", required=True); p.add_argument("--prompt", required=True); p.add_argument("--aspect")
    p.add_argument("--force", action="store_true")
    p = sub.add_parser("3d"); p.add_argument("id"); p.add_argument("--image", required=True); p.add_argument("--tag", required=True)
    p.add_argument("--engine"); p.add_argument("--faces"); p.add_argument("--views", nargs="*")
    p.add_argument("--force", action="store_true")
    p = sub.add_parser("patina"); p.add_argument("id"); p.add_argument("--take", type=int, default=1); p.add_argument("--tag")
    p.add_argument("--prompt"); p.add_argument("--seed", type=int); p.add_argument("--force", action="store_true")
    p = sub.add_parser("gptimg"); p.add_argument("id"); p.add_argument("--take", type=int, default=1); p.add_argument("--tag")
    p.add_argument("--prompt"); p.add_argument("--size"); p.add_argument("--background", default="transparent")
    p.add_argument("--quality", default="high"); p.add_argument("--force", action="store_true")
    p = sub.add_parser("nb"); p.add_argument("id"); p.add_argument("--tag", required=True); p.add_argument("--prompt", required=True)
    p.add_argument("--aspect"); p.add_argument("--res", default="2K"); p.add_argument("--force", action="store_true")
    p = sub.add_parser("batch"); p.add_argument("jobs"); p.add_argument("--par", type=int, default=4)
    return ap


def run_job(argv):
    a = parser().parse_args(argv)
    {"ref": cmd_ref, "edit": cmd_edit, "3d": cmd_3d, "patina": cmd_patina, "gptimg": cmd_gptimg, "nb": cmd_nb}[a.cmd](a)


def main():
    a = parser().parse_args()
    if a.cmd == "batch":
        jobs = json.loads(pathlib.Path(a.jobs).read_text())
        with cf.ThreadPoolExecutor(a.par) as ex:
            futs = [ex.submit(subprocess.run, [PY, __file__] + j) for j in jobs]
            for f in cf.as_completed(futs):
                f.result()
        return
    run_job(sys.argv[1:])


if __name__ == "__main__":
    main()
