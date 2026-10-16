#!/usr/bin/env python3
"""Tree textures for the procedural olive / pomegranate trees (WP10_FAL).

  Tools/.venv/bin/python Tools/mesh/props/tree_tex.py VEG_OLIVE_TREE [--leaves leaves_t1] [--bark bark_t1]

Leaves: gpt-image-2.5 transparent 2x2 twig sheet -> alpha-cutout card atlas T_<Name>_Leaves_{BC,N,M} (haze removed,
colour bled under transparent texels, slight card normal bend, smoothness 0.35-0.45 for waxy leaves).
Bark: fal-ai/patina/material take -> tiling T_<Name>_Bark_{BC,N,M} (albedo/roughness calibrated, 512 in game).
Writes RawAssets/wp10/fal/<ID>/tree_tex.json with leaf_tiles (UV rects, v up) and fruit_uv for blender_tree.py.
"""
import argparse, json, pathlib, sys
import numpy as np
from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import patina_pack as PP          # noqa: E402
import decal_atlas as DA          # noqa: E402

ROOT = HERE.parents[2]
RAW = ROOT / "RawAssets/wp10/fal"
READY = ROOT / "RawAssets/wp10/ready"

T = {
    "VEG_OLIVE_TREE": dict(name="OliveTree", folder="olive_tree", leaves="leaves_t1", bark="bark_t1", bark_target=0.36,
                           bark_rough=(0.80, 0.97), leaf_smooth=0.38, leaf_gain=1.0, leaf_sat=0.85, bark_tint=(1.0, 0.94, 0.86),
                           fruit_tile=None),
    "VEG_POMEGRANATE_TREE": dict(name="PomegranateTree", folder="pomegranate_tree", leaves="leaves_t1", bark="bark_t1",
                                 bark_target=0.36, bark_rough=(0.72, 0.92), leaf_smooth=0.5, leaf_gain=0.9, fruit_tile=1),
}


def u8(a):
    return np.clip(a * 255 + 0.5, 0, 255).astype(np.uint8)


def leaves(aid, S, tdir):
    im = np.asarray(Image.open(RAW / aid / f"{S['leaves']}.png").convert("RGBA")).astype(np.float32) / 255.0
    rgb, a = im[..., :3].copy(), im[..., 3].copy()
    a = np.clip((a - 0.13) / 0.87, 0, 1)
    a = DA.smoothstep(0.3, 0.6, a)
    rgb = np.clip(rgb * S["leaf_gain"], 0, 1)
    if S.get("leaf_sat"):
        rgb = PP.calibrate_bc(rgb, None, S["leaf_sat"])
    rgb = DA.bleed(rgb, a)
    H, W = a.shape
    rects = DA.cell_rects(a, (2, 2), pad=4)
    tiles = [[round(x0 / W, 5), round(1 - y1 / H, 5), round(x1 / W, 5), round(1 - y0 / H, 5)] for (x0, y0, x1, y1) in rects if rects]
    # card normal: flat +Z with a soft dome per twig (gives the card some volume under the bent mesh normals)
    n = np.zeros((H, W, 3), np.float32); n[..., 2] = 1
    L = DA.gblur(DA.lum(rgb) * a, 1.5)
    dx, dy = np.gradient(L, axis=1) * 6, np.gradient(L, axis=0) * 6
    n[..., 0], n[..., 1] = -dx, dy
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    n = (n + 1) / 2
    # mask: R metal 0, G AO (darker toward the twig base / overlaps), B 0, A smoothness
    ao = np.clip(0.75 + 0.25 * DA.smoothstep(0.1, 0.5, DA.lum(rgb)), 0, 1)
    sm = np.full_like(a, S["leaf_smooth"])
    m = np.dstack([np.zeros_like(a), ao, np.zeros_like(a), sm])
    nm = S["name"]
    p = {k: tdir / f"T_{nm}_Leaves_{k}.png" for k in ("BC", "N", "M")}
    Image.fromarray(u8(np.dstack([rgb, a])), "RGBA").save(p["BC"])
    Image.fromarray(u8(n)).resize((512, 512), Image.LANCZOS).save(p["N"])
    Image.fromarray(u8(m), "RGBA").resize((512, 512), Image.LANCZOS).save(p["M"])
    fruit_uv = None
    if S["fruit_tile"] is not None:
        # centre of the most saturated red blob inside that tile (the pomegranate on the twig)
        x0, y0, x1, y1 = rects[S["fruit_tile"]]
        sub = rgb[y0:y1, x0:x1]; sa = a[y0:y1, x0:x1]
        red = (sub[..., 0] - np.maximum(sub[..., 1], sub[..., 2])) * (sa > 0.9)
        red = DA.gblur(red, 4)
        cy, cx = np.unravel_index(np.argmax(red), red.shape)
        fruit_uv = [round((x0 + cx) / W, 5), round(1 - (y0 + cy) / H, 5)]
    return p, tiles, fruit_uv, (rgb, a)


def bark(aid, S, tdir):
    td = RAW / aid / S["bark"]
    M, seed, _ = PP.maps_of(td)
    bc = PP.load(M["basecolor"]); n = PP.load(M["normal"]); rough = PP.load(M["roughness"], "L")
    h = PP.load(M["height"], "L") if "height" in M else np.full_like(rough, 0.5)
    if S.get("bark_tint"):
        bc = np.clip(bc * np.array(S["bark_tint"], np.float32), 0, 1)
    bc = PP.calibrate_bc(bc, S["bark_target"])
    rough = PP.remap_rough(rough, *S["bark_rough"])
    conv, cg, cr = PP.normal_convention(n, h)
    if conv < 0:
        n[..., 1] = 1 - n[..., 1]
    blur = PP.wrap_blur(h, 10)
    ao = np.clip(1.0 - 0.7 * np.clip((blur - h) * 4.0, 0, 1), 0, 1)
    m = np.dstack([np.zeros_like(h), ao, np.zeros_like(h), 1 - rough])
    nm = S["name"]
    p = {k: tdir / f"T_{nm}_Bark_{k}.png" for k in ("BC", "N", "M")}
    Image.fromarray(u8(bc)).resize((512, 512), Image.LANCZOS).save(p["BC"])
    Image.fromarray(u8(n)).resize((512, 512), Image.LANCZOS).save(p["N"])
    Image.fromarray(u8(m), "RGBA").resize((512, 512), Image.LANCZOS).save(p["M"])
    return p, dict(seed=seed, conv=conv, cg=round(cg, 3))


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("id"); ap.add_argument("--leaves"); ap.add_argument("--bark")
    a = ap.parse_args()
    S = dict(T[a.id])
    if a.leaves: S["leaves"] = a.leaves
    if a.bark: S["bark"] = a.bark
    tdir = READY / f"Assets/_Game/Art/Textures/Vegetation/{S['folder']}"
    tdir.mkdir(parents=True, exist_ok=True)
    lp, tiles, fruit_uv, _ = leaves(a.id, S, tdir)
    bp, bst = bark(a.id, S, tdir)
    info = {"leaf_tiles": tiles, "fruit_uv": fruit_uv, "leaves": {k: str(v) for k, v in lp.items()},
            "bark": {k: str(v) for k, v in bp.items()}, "bark_stats": bst, "src": {"leaves": S["leaves"], "bark": S["bark"]}}
    (RAW / a.id / "tree_tex.json").write_text(json.dumps(info, indent=1))
    print(json.dumps(info))


if __name__ == "__main__":
    main()
