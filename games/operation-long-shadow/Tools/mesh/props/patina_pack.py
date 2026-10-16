#!/usr/bin/env python3
"""fal-ai/patina/material take -> Unity tiling texture set T_<Name>_{BC,N,M[,H]} + review sheet (WP10_FAL).

  Tools/.venv/bin/python Tools/mesh/props/patina_pack.py TEX_SALTCRUST --take patina_t2 [--sheet-only]
  Tools/.venv/bin/python Tools/mesh/props/patina_pack.py --compare TEX_SALTCRUST patina_t1 patina_t2

Spec per id lives in PATINA below (name, folder, tile metres, ground -> M.B = height + _H, AO strength).
Reads RawAssets/wp10/fal/<ID>/<take>/result.json (map_type per image), detects the normal-map Y convention
from the height map (flips DirectX -> OpenGL), makes the mask (R metal, G AO from height cavity, B height or 0,
A smoothness = 1 - roughness), checks tiling seams and writes:
  RawAssets/wp10/ready/Assets/_Game/Art/Textures/Env/<folder>/T_<Name>_{BC,N,M[,H]}.png
  RawAssets/wp10/fal/<ID>/ready_<Name>.json        (import_index rows)
  RawAssets/wp10/fal/<ID>/material_<Name>.json     (materials_fal.json row)
  RawAssets/wp10/fal/_review/<ID>__<take>.png      (2x2 tiled BC, lit relief preview, N, M, stats)
"""
import argparse, json, pathlib, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
RAW = ROOT / "RawAssets/wp10/fal"
READY = ROOT / "RawAssets/wp10/ready"

PATINA = {
    "TEX_SALTCRUST": dict(name="SaltCrust", folder="saltcrust", tile=2.5, ground=True, ao=0.6, bc_target=0.80, rough=(0.78, 0.96),
                          use="Salt pan (out of bounds) and pitch edges; also a Terrain Layer"),
    "TEX_AWNING_STRIPES": dict(name="AwningStripes", folder="awning_stripes", tile=1.6, ground=False, ao=0.35, bc_target=0.60, sat=0.85, rough=(0.84, 0.96),
                               use="Market awnings AW-01..08 (canvas); two-sided cloth, URP/Lit Render Face Both"),
    "TEX_STONE_PAVING": dict(name="StonePaving", folder="stone_paving", tile=2.0, ground=True, ao=0.7, bc_target=0.64, rough=(0.68, 0.92),
                             use="Clock Square and market paving (world-UV kit floors); also a Terrain Layer"),
    "TEX_MUDBRICK": dict(name="Mudbrick", folder="mudbrick", tile=1.6, ground=False, ao=0.7, bc_target=0.50, rough=(0.85, 0.97),
                         use="Outskirts walls, grove irrigation wall"),
    "TEX_VELVET": dict(name="Velvet", folder="velvet", tile=0.3, ground=False, ao=0.3, bc_target=None, rough=(0.62, 0.88),
                       use="Cinema seats (detail/tiling upholstery)"),
    "TEX_CINEMA_CARPET": dict(name="CinemaCarpet", folder="cinema_carpet", tile=0.8, ground=False, ao=0.4, bc_target=None, rough=(0.86, 0.97),
                              use="Cinema aisles"),
}


def maps_of(take_dir):
    r = json.loads((take_dir / "result.json").read_text())
    files = sorted(p for p in take_dir.glob("*.png"))
    out = {}
    for i, im in enumerate(r["images"]):
        mt = im.get("map_type", "original")
        out[mt] = next(p for p in files if p.name.startswith(f"{i:02d}_"))
    return out, r.get("seed"), r.get("prompt")


def load(p, mode="RGB"):
    return np.asarray(Image.open(p).convert(mode)).astype(np.float32) / 255.0


def normal_convention(n, h):
    """+1 if OpenGL (+Y), -1 if DirectX, from the correlation of the green channel with the height gradient."""
    dh_row = np.gradient(h, axis=0)
    dh_col = np.gradient(h, axis=1)
    g = n[..., 1] - 0.5
    r = n[..., 0] - 0.5
    cg = float(np.corrcoef(g.ravel(), dh_row.ravel())[0, 1])
    cr = float(np.corrcoef(r.ravel(), -dh_col.ravel())[0, 1])
    # patina returns OpenGL maps; only flip on clear, consistent evidence of DirectX
    return (-1 if (cg < -0.3 and cr < -0.3) else 1), cg, cr


def seam_score(a):
    """Mean abs difference across the wrap seam vs inside the image (1.0 = seamless)."""
    edge = np.abs(a[:, 0] - a[:, -1]).mean() + np.abs(a[0, :] - a[-1, :]).mean()
    inner = np.abs(a[:, 1:] - a[:, :-1]).mean() + np.abs(a[1:, :] - a[:-1, :]).mean()
    return float(edge / max(inner, 1e-6))


def cavity_ao(h, strength):
    """AO from height: cavities (below the local blurred mean) get darker."""
    him = Image.fromarray((h * 255).astype(np.uint8))
    blur = np.asarray(him.filter(ImageFilter.GaussianBlur(12))).astype(np.float32) / 255.0
    cav = np.clip((blur - h) * 4.0, 0, 1)
    ao = 1.0 - strength * cav
    # tile-safe: the blur wraps poorly at borders; blend from a wrapped blur
    return np.clip(ao, 0, 1)


def wrap_blur(h, r):
    pad = r * 3
    t = np.pad(h, pad, mode="wrap")
    b = np.asarray(Image.fromarray((t * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))).astype(np.float32) / 255.0
    return b[pad:-pad, pad:-pad]


def srgb2lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin2srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def calibrate_bc(bc, target=None, sat=None):
    """Scale albedo in linear space so the mean sRGB luminance hits a physically plausible target; optional saturation."""
    out = bc
    if sat is not None:
        lum = (out * np.array([0.2126, 0.7152, 0.0722])).sum(-1, keepdims=True)
        out = np.clip(lum + (out - lum) * sat, 0, 1)
    if target is not None and float((out * np.array([0.2126, 0.7152, 0.0722])).sum(-1).mean()) > target:
        # only darkens: generated albedo tends to be over-bright; never push a dark material up (clips saturated colours)
        lin = srgb2lin(out)
        k = 1.0
        for _ in range(30):
            m = float((lin2srgb(lin * k) * np.array([0.2126, 0.7152, 0.0722])).sum(-1).mean())
            k *= (target / max(m, 1e-4)) ** 1.6
        out = lin2srgb(lin * k)
    return out


def remap_rough(r, lo, hi):
    a, b = np.percentile(r, 2), np.percentile(r, 98)
    t = np.clip((r - a) / max(b - a, 1e-4), 0, 1)
    return lo + (hi - lo) * t


def relief_preview(bc, n, light=(-0.6, 0.55, 0.58)):
    L = np.array(light, dtype=np.float32); L /= np.linalg.norm(L)
    nn = n * 2 - 1
    nn /= np.linalg.norm(nn, axis=-1, keepdims=True) + 1e-6
    # image rows go down: GL normal +Y = up on screen
    lam = np.clip(nn[..., 0] * L[0] + nn[..., 1] * L[1] + nn[..., 2] * L[2], 0, 1)
    out = bc * (0.25 + 0.95 * lam[..., None])
    return np.clip(out, 0, 1)


def to_img(a, mode=None):
    a = np.clip(a * 255 + 0.5, 0, 255).astype(np.uint8)
    return Image.fromarray(a, mode) if mode else Image.fromarray(a)


def pack(aid, take, sheet_only=False):
    S = PATINA[aid]
    td = RAW / aid / take
    M, seed, prompt = maps_of(td)
    bc = load(M["basecolor"])
    n = load(M["normal"])
    rough = load(M["roughness"], "L")
    metal = load(M["metalness"], "L") if "metalness" in M else np.zeros_like(rough)
    h = load(M["height"], "L") if "height" in M else np.full_like(rough, 0.5)
    raw_bc_mean = float(bc.mean())
    raw_rough = float(rough.mean())
    bc = calibrate_bc(bc, S.get("bc_target"), S.get("sat"))
    if S.get("rough"):
        rough = remap_rough(rough, *S["rough"])
    conv, cg, cr = normal_convention(n, h)
    if conv < 0:
        n = n.copy(); n[..., 1] = 1.0 - n[..., 1]
    # renormalise
    v = n * 2 - 1
    v /= np.linalg.norm(v, axis=-1, keepdims=True) + 1e-6
    v[..., 2] = np.abs(v[..., 2])
    n = (v + 1) / 2
    blur = wrap_blur(h, 10)
    ao = np.clip(1.0 - S["ao"] * np.clip((blur - h) * 4.0, 0, 1), 0, 1)
    # non-metals: patina sometimes returns noisy metalness; clamp tiny values
    metal = np.where(metal < 0.2, 0.0, metal)
    smooth = 1.0 - rough
    mask = np.dstack([metal, ao, h if S["ground"] else np.zeros_like(h), smooth])
    stats = dict(seed=seed, normal_conv="GL" if conv > 0 else "DX->GL flipped", corr_g=round(cg, 3), corr_r=round(cr, 3),
                 seam_bc=round(seam_score(bc.mean(-1)), 2), seam_h=round(seam_score(h), 2),
                 bc_mean=[round(float(x), 3) for x in bc.reshape(-1, 3).mean(0)], rough_mean=round(float(rough.mean()), 3), raw_bc=round(raw_bc_mean, 3), raw_rough=round(raw_rough, 3),
                 metal_mean=round(float(metal.mean()), 4), ao_mean=round(float(ao.mean()), 3))
    name, folder = S["name"], S["folder"]
    tdir = READY / f"Assets/_Game/Art/Textures/Env/{folder}"
    T = {k: tdir / f"T_{name}_{k}.png" for k in ("BC", "N", "M", "H")}
    if not sheet_only:
        tdir.mkdir(parents=True, exist_ok=True)
        to_img(bc).save(T["BC"])
        to_img(n).save(T["N"])
        to_img(mask, "RGBA").save(T["M"])
        if S["ground"]:
            to_img(h).save(T["H"])
        elif T["H"].exists():
            T["H"].unlink()
        rel = lambda p: str(p.relative_to(READY))
        tile = S["tile"]
        tt = f" Tile {tile} m (world-UV tiling = 1/{tile}). Source: fal-ai/patina/material ({take}, seed {seed})."
        rows = [
            {"manifest_id": aid, "file": rel(T["BC"]), "target_path": rel(T["BC"]), "type": "texture_basecolor",
             "import_notes": "Texture Type Default, sRGB ON, Max Size 1024, DXT1 Crunched q55, mips on, aniso " +
             ("8" if S["ground"] else "4") + ", wrap Repeat." + tt},
            {"manifest_id": aid, "file": rel(T["N"]), "target_path": rel(T["N"]), "type": "texture_normal",
             "import_notes": "Texture Type Normal map, OpenGL (+Y), sRGB OFF, Max Size 1024, DXT5nm Crunched q60, wrap Repeat." + tt},
            {"manifest_id": aid, "file": rel(T["M"]), "target_path": rel(T["M"]), "type": "texture_mask",
             "import_notes": "Texture Type Default, sRGB OFF, Max Size 1024, DXT5 Crunched q60, wrap Repeat. Mask packing D4: R metallic, "
             "G AO (height cavity), A smoothness (1-roughness); assign to BOTH _MetallicGlossMap and _OcclusionMap, Smoothness Source = "
             "Metallic Alpha, _Smoothness 1.0, _OcclusionStrength 1.0. " +
             ("B = height (0..1) so this texture is also the Unity Terrain Layer mask map (R metal, G AO, B height, A smooth)."
              if S["ground"] else "B unused (0).") + tt},
        ]
        if S["ground"]:
            rows.append({"manifest_id": aid, "file": rel(T["H"]), "target_path": rel(T["H"]), "type": "texture_height",
                         "import_notes": "Height for URP/Lit _ParallaxMap (optional, close-up ground only), sRGB OFF, Max Size 1024, "
                         "DXT1 Crunched q55, parallax scale 0.01-0.02." + tt})
        for r in rows:
            r["tool"] = "patina_pack"
        (RAW / aid / f"ready_{name}.json").write_text(json.dumps(rows, indent=1))
        mat = {"manifest_id": aid, "material": f"M_{name}", "target_path": f"Assets/_Game/Art/Materials/Env/M_{name}.mat",
               "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic",
               "textures": {"_BaseMap": rel(T["BC"]), "_BumpMap": rel(T["N"]), "_MetallicGlossMap": rel(T["M"]), "_OcclusionMap": rel(T["M"])},
               "floats": {"_Smoothness": 1.0, "_SmoothnessTextureChannel": 0, "_Metallic": 1.0, "_BumpScale": 1.0, "_OcclusionStrength": 1.0},
               "tile_m": tile, "world_uv_tiling": round(1 / tile, 4), "alpha_clip": None, "use": S["use"],
               "render_face": "Both" if aid == "TEX_AWNING_STRIPES" else "Front",
               "terrain_layer": ({"asset": f"Assets/_Game/Art/Materials/Env/TL_{name}.terrainlayer", "diffuse": rel(T["BC"]),
                                  "normal": rel(T["N"]), "mask": rel(T["M"]), "tile_size_m": tile, "note": "mask B = height"}
                                 if S["ground"] else False)}
        if S["ground"]:
            mat["textures"]["_ParallaxMap"] = rel(T["H"]); mat["floats"]["_Parallax"] = 0.012
        (RAW / aid / f"material_{name}.json").write_text(json.dumps(mat, indent=1))
    # review sheet
    cell = 512
    tiled = np.tile(bc, (2, 2, 1))
    rp = relief_preview(np.tile(bc, (2, 2, 1)), np.tile(n, (2, 2, 1)))
    panels = [(to_img(tiled).resize((cell, cell)), "BC tiled 2x2"), (to_img(rp).resize((cell, cell)), "lit relief 2x2 (N, low sun)"),
              (to_img(n).resize((cell, cell)), "N (GL)"), (to_img(mask[..., :3]).resize((cell, cell)), "M rgb (R metal G AO B h)"),
              (to_img(smooth).resize((cell, cell)), "M alpha smoothness"), (to_img(bc).resize((cell, cell)), "BC 1x")]
    im = Image.new("RGB", (cell * 3, (cell + 20) * 2 + 60), (28, 28, 28))
    d = ImageDraw.Draw(im)
    f = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 14)
    for i, (p, lab) in enumerate(panels):
        x, y = (i % 3) * cell, (i // 3) * (cell + 20)
        im.paste(p.convert("RGB"), (x, y + 20)); d.text((x + 4, y + 2), lab, fill=(255, 255, 255), font=f)
    d.text((6, 2 * (cell + 20) + 4), f"{aid} [{take}] T_{name}  tile {S['tile']} m  {json.dumps(stats)}", fill=(220, 220, 160), font=f)
    d.text((6, 2 * (cell + 20) + 24), (prompt or "")[:220], fill=(160, 160, 160), font=f)
    out = RAW / "_review" / f"{aid}__{take}.png"
    im.save(out)
    print("SHEET", out)
    print(json.dumps(stats))
    return stats


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id", nargs="?"); ap.add_argument("--take"); ap.add_argument("--sheet-only", action="store_true")
    ap.add_argument("--compare", nargs="+")
    a = ap.parse_args()
    if a.compare:
        aid, takes = a.compare[0], a.compare[1:]
        for t in takes:
            pack(aid, t, sheet_only=True)
        return
    pack(a.id, a.take, a.sheet_only)


if __name__ == "__main__":
    main()
