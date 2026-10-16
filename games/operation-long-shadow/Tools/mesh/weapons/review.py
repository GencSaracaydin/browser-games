#!/usr/bin/env python3
"""Render a review contact sheet for one weapon folder of the ready tree (runs Blender, then PIL).

  Tools/.venv/bin/python Tools/mesh/weapons/review.py AK [--model SM_WPN_AK_FP] [--views left,right,...] [--samples 48]
     [--res 960x540] [--out RawAssets/wp03/_review/AK_FP.png] [--fp-cam loc;target;lens] [--no-tex]
"""
import argparse, json, pathlib, subprocess, sys
from PIL import Image, ImageDraw

ROOT = pathlib.Path(__file__).resolve().parents[3]
BL = "/Applications/Blender.app/Contents/MacOS/Blender"
READY = ROOT / "RawAssets/wp03/ready/Assets/_Game/Art"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("--model", action="append")
    ap.add_argument("--objects", action="append")
    ap.add_argument("--views", default="left,right,top,front34,rear34,fp")
    ap.add_argument("--samples", type=int, default=40)
    ap.add_argument("--res", default="960x540")
    ap.add_argument("--out")
    ap.add_argument("--fp-cam")
    ap.add_argument("--no-tex", action="store_true")
    ap.add_argument("--light", type=float, default=1.0)
    ap.add_argument("--hdri", help="lighting HDRI (default Blender studio.exr); 'desert' = RawAssets/cc0/hdri/goegap_2k.hdr, no area lights")
    ap.add_argument("--hdri-strength", type=float, default=1.0)
    ap.add_argument("--exposure", type=float)
    ap.add_argument("--ready", help="alternative ready root (…/ready) for comparing takes")
    a = ap.parse_args()
    ready = (pathlib.Path(a.ready) / "Assets/_Game/Art") if a.ready else READY
    if not ready.is_absolute():
        ready = ROOT / ready
    mdir = ready / "Models/Weapons" / a.folder
    tdir = ready / "Textures/Weapons" / a.folder
    models = a.model or [p.stem for p in sorted(mdir.glob("*_FP.fbx"))] or [p.stem for p in sorted(mdir.glob("*.fbx"))][:1]
    tex = {}
    for p in tdir.glob("T_*_BC.png"):
        base = p.name[:-7]  # T_WPN_AK
        tex["M_" + base[2:]] = str(tdir / base)
    if a.no_tex:
        tex = {}
    items = [{"fbx": str(mdir / f"{m}.fbx"), "tex": tex, "objects": a.objects} for m in models]
    out = pathlib.Path(a.out) if a.out else ROOT / f"RawAssets/wp03/_review/{a.folder}_{models[0]}.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    w, h = map(int, a.res.split("x"))
    views = json.loads(a.views) if a.views.startswith("[") else a.views.split(",")
    job = {"out": str(out), "items": items, "views": views, "res": [w, h], "samples": a.samples, "light_scale": a.light,
           "hdri_strength": a.hdri_strength}
    if a.hdri == "desert":
        job["hdri"] = str(ROOT / "RawAssets/cc0/hdri/goegap_2k.hdr")
        job["light_scale"] = 0.0
    elif a.hdri:
        job["hdri"] = a.hdri
    if a.exposure is not None:
        job["exposure"] = a.exposure
    if a.fp_cam:
        loc, tgt, lens = a.fp_cam.split(";")
        job["fp_cam"] = {"loc": json.loads(loc), "target": json.loads(tgt), "lens": float(lens)}
    jp = out.with_suffix(".json")
    jp.write_text(json.dumps(job, indent=1))
    r = subprocess.run([BL, "-b", "--factory-startup", "--python-exit-code", "1", "-t", "3", "-P", str(ROOT / "Tools/mesh/weapons/bl_render.py"), "--", str(jp)],
                       capture_output=True, text=True)
    if r.returncode != 0 or "RENDERED" not in r.stdout:
        print(r.stdout[-3000:], r.stderr[-3000:])
        sys.exit(1)
    frames = json.loads((out.parent / "_frames.json").read_text())["frames"]
    # coordinate grid on ortho box views (model metres: Y along the image x axis, Z up), 1 cm lines, labels in cm
    for f, v in zip(frames, views):
        if isinstance(v, dict) and "box" in v:
            im = Image.open(f).convert("RGB")
            d = ImageDraw.Draw(im)
            y0, z0, y1, z1 = v["box"]
            span = max(y1 - y0, (z1 - z0) * w / h)
            sgn = 1 if v.get("side", "left") == "left" else -1
            cy, cz = (y0 + y1) / 2, (z0 + z1) / 2
            px = w / span
            import math as _m
            for k in range(_m.floor((cy - span / 2) * 100), _m.ceil((cy + span / 2) * 100) + 1):
                X = w / 2 + sgn * (k / 100 - cy) * px
                d.line([(X, 0), (X, h)], fill=(255, 60, 60), width=1)
                d.text((X + 2, 2), f"y{k}", fill=(255, 90, 90))
            for k in range(_m.floor((cz - span * h / w / 2) * 100), _m.ceil((cz + span * h / w / 2) * 100) + 1):
                Y = h / 2 - (k / 100 - cz) * px
                d.line([(0, Y), (w, Y)], fill=(60, 160, 255), width=1)
                d.text((2, Y + 2), f"z{k}", fill=(90, 180, 255))
            im.save(f)
    cols = 2 if len(frames) > 1 else 1
    rows = (len(frames) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * w, rows * (h + 18)), (40, 40, 40))
    d = ImageDraw.Draw(sheet)
    for i, f in enumerate(frames):
        x, y = (i % cols) * w, (i // cols) * (h + 18)
        sheet.paste(Image.open(f).convert("RGB"), (x, y + 18))
        d.text((x + 4, y + 3), f"{models[0]}  {pathlib.Path(f).stem[6:]}", fill=(230, 230, 230))
    for f in frames:
        pathlib.Path(f).unlink(missing_ok=True)
    sheet.save(out)
    print(out)


if __name__ == "__main__":
    main()
