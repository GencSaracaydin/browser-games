#!/usr/bin/env python3
"""Generated sprite sheet (gpt-image-2.5 transparent PNG) -> clean decal / card atlas for Unity (WP10_FAL).

  Tools/.venv/bin/python Tools/mesh/props/decal_atlas.py DCL_BULLET_HOLES [--src gpt_t1] [--sheet-only]

Per-id presets below: grid (cols, rows) of the generated sheet, labels, alpha mode, normal strength.
Steps: kill the near-transparent background haze gpt-image leaves (alpha < ~0.13), optional "darken" alpha
(stain decals: whitish semi-transparent halo pixels lose their alpha), per-row fixes (bullet holes: the square
wood / painted-metal backing patches are feathered away so only the crater, splinters and bare-metal ring stay),
colour bleed into transparent texels (no dark/white fringes on mips), per-element UV rects, optional normal map
from luminance (dark = deep). Writes:
  RawAssets/wp10/ready/Assets/_Game/Art/Textures/<Decals|Props>/<folder>/T_<Decal_>Name_{BC,N}.png
  .../<folder>/T_<Decal_>Name_layout.json      (element rects: pixels and UV [u0,v0,u1,v1], v up)
  RawAssets/wp10/fal/<ID>/ready_<Name>.json     (import_index rows)
  RawAssets/wp10/fal/_review/<ID>__atlas.png    (checker, composited on CC0 plaster/concrete, normal, rects)
"""
import argparse, json, pathlib
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
RAW = ROOT / "RawAssets/wp10/fal"
READY = ROOT / "RawAssets/wp10/ready"
ENV = READY / "Assets/_Game/Art/Textures/Env"

P = {
    "DCL_BULLET_HOLES": dict(src="gpt_t1", name="Decal_BulletHoles", folder="Decals/bullet_holes", grid=(4, 4), normal=4.0,
                             rows={2: "wood", 3: "metal"}, bg="painted_plaster_wall",
                             labels=[f"{m}_{i}" for m in ("plaster", "concrete", "wood", "metal") for i in range(1, 5)],
                             size_m="0.06-0.12 m per element (plaster/concrete chips ~0.10 m, wood ~0.08 m, metal ~0.07 m)"),
    "DCL_SCORCH": dict(src="gpt_t1", name="Decal_Scorch", folder="Decals/scorch", grid=(2, 2), normal=0.5, darken=(0.70, 0.38),
                       bg="concrete_floor_worn_001", labels=["blast_large", "burn_small", "fire_stain", "ash_ring"],
                       size_m="blast 2.5-3.5 m, burn 0.8-1.2 m, fire stain 1.5-2 m, ash ring 1-1.5 m"),
    "DCL_BLOOD_SMALL": dict(src="gpt_t1", name="Decal_BloodSmall", folder="Decals/blood_small", grid=(3, 2), normal=0.8,
                            bg="concrete_floor_worn_001", labels=["drip", "droplets", "smear", "pool", "spatter", "drops"],
                            size_m="0.15-0.4 m; keep restrained (GDD guardrails), never on faces/UI"),
    "DCL_GRIME": dict(src="gpt_t1", name="Decal_Grime", folder="Decals/grime", grid=(4, 2), normal=0.0, darken=(0.86, 0.5),
                      bg="painted_plaster_wall", labels=["rain_streaks", "base_dust", "damp_rise", "soot", "mud_splash", "rust_drip",
                                                       "water_ring", "brown_smear"],
                      size_m="streaks 1-2.5 m tall, base dust 1.5-3 m wide, others 0.5-1.5 m"),
    "DCL_CHALK_DRAWINGS": dict(src="gpt_t1", name="Decal_ChalkDrawings", folder="Decals/chalk_drawings", grid=(3, 2), normal=0.0,
                               bg="concrete_floor_worn_001", labels=["sun", "house", "hopscotch", "cat", "stick_figures", "car"],
                               size_m="0.6-1.2 m (hopscotch 2.4 m long)", opacity=0.85),
    "VEG_DRY_GRASS_CARDS": dict(src="gpt_t1", name="DryGrass", folder="Props/dry_grass", grid=(4, 2), normal=0.0, cutout=True,
                                bg=None, labels=["bunch_grass", "dense_tuft", "seed_stalks", "grey_shrub", "dead_blades", "plume_tuft",
                                                 "spiky_weed", "mixed_tuft"],
                                size_m="0.3-0.7 m tall tufts"),
}


def lum(c):
    return c[..., 0] * 0.2126 + c[..., 1] * 0.7152 + c[..., 2] * 0.0722


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def gblur(a, r):
    from scipy.ndimage import gaussian_filter
    if a.ndim == 2:
        return gaussian_filter(a.astype(np.float32), r, mode="nearest")
    return np.dstack([gblur(a[..., i], r) for i in range(a.shape[2])])


def sat(c):
    mx, mn = c.max(-1), c.min(-1)
    return (mx - mn) / np.maximum(mx, 1e-4)


def bleed(rgb, a, thr=0.02):
    """Fill colour under transparent texels from nearby opaque ones (push-pull with growing blurs)."""
    out = rgb.copy()
    w = (a > 0.3).astype(np.float32)
    filled = w > 0
    for r in (2, 6, 16, 40, 100):
        num = gblur(rgb * w[..., None], r)
        den = gblur(w, r)
        cand = num / np.maximum(den[..., None], 1e-5)
        m = (~filled) & (den > 1e-3)
        out[m] = cand[m]
        filled |= m
    keep = a > thr
    out[keep] = rgb[keep]
    return out


def cell_rects(a, grid, pad=6):
    H, W = a.shape
    cols, rows = grid
    rects = []
    for r in range(rows):
        for c in range(cols):
            x0, x1 = int(c * W / cols), int((c + 1) * W / cols)
            y0, y1 = int(r * H / rows), int((r + 1) * H / rows)
            sub = a[y0:y1, x0:x1] > 0.04
            ys, xs = np.nonzero(sub)
            if len(xs) == 0:
                rects.append(None); continue
            bx0, bx1 = max(x0, x0 + xs.min() - pad), min(x1, x0 + xs.max() + 1 + pad)
            by0, by1 = max(y0, y0 + ys.min() - pad), min(y1, y0 + ys.max() + 1 + pad)
            rects.append(tuple(int(v) for v in (bx0, by0, bx1, by1)))
    return rects


def normal_from(rgb, a, strength):
    h = lum(rgb) * a + (1 - a) * 0.0
    h = gblur(h, 1.0)
    dx = np.gradient(h, axis=1) * strength * 8
    dy = np.gradient(h, axis=0) * strength * 8
    # dark = deep: height = luminance; GL normal n = (-dh/du, -dh/dv, 1), v up = -row
    n = np.dstack([-dx, dy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    w = smoothstep(0.05, 0.5, a)[..., None]
    n = n * w + np.array([0, 0, 1.0]) * (1 - w)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n + 1) / 2


def checker(h, w, s=16):
    y, x = np.mgrid[0:h, 0:w]
    c = (((x // s) + (y // s)) % 2).astype(np.float32)
    return np.dstack([0.55 + 0.2 * c] * 3)


def process(aid, src=None, sheet_only=False):
    S = P[aid]
    src = src or S["src"]
    im = np.asarray(Image.open(RAW / aid / f"{src}.png").convert("RGBA")).astype(np.float32) / 255.0
    rgb, a = im[..., :3].copy(), im[..., 3].copy()
    H, W = a.shape
    a = np.clip((a - 0.13) / 0.87, 0, 1)                     # background haze -> 0
    if S.get("darken"):
        lw, ls = S["darken"]
        a = a * (1 - smoothstep(ls, lw, lum(rgb)))           # whitish halo loses alpha
    cols, rows = S["grid"]
    for r, kind in S.get("rows", {}).items():
        y0, y1 = int(r * H / rows), int((r + 1) * H / rows)
        for c in range(cols):
            x0, x1 = int(c * W / cols), int((c + 1) * W / cols)
            sa, sr = a[y0:y1, x0:x1], rgb[y0:y1, x0:x1]
            ys, xs = np.nonzero(sa > 0.3)
            if len(xs) == 0:
                continue
            # crater centre = darkest blurred spot
            L = gblur(lum(sr) + (1 - sa) * 2, 6)
            cy, cx = np.unravel_index(np.argmin(L), L.shape)
            yy, xx = np.mgrid[0:sa.shape[0], 0:sa.shape[1]]
            ext = 0.5 * min(xs.max() - xs.min(), ys.max() - ys.min())
            d = np.hypot(yy - cy, xx - cx) / max(ext, 1)
            if kind == "wood":
                fresh = smoothstep(0.30, 0.45, sat(sr)) * smoothstep(0.25, 0.45, lum(sr))   # torn fresh wood (saturated tan)
                dark = 1 - smoothstep(0.10, 0.22, lum(sr))
                keep = np.maximum(1 - smoothstep(0.30, 0.62, d), np.maximum(fresh, dark) * (1 - smoothstep(0.7, 1.0, d)))
            else:   # painted steel: drop the paint patch, keep the bare-metal ring and hole
                bare = (1 - smoothstep(0.10, 0.22, sat(sr))) * smoothstep(0.35, 0.55, lum(sr))
                dark = 1 - smoothstep(0.10, 0.25, lum(sr))
                keep = np.maximum(1 - smoothstep(0.22, 0.42, d), np.maximum(bare, dark) * (1 - smoothstep(0.45, 0.65, d)))
            keep = gblur(keep, 1.2)
            a[y0:y1, x0:x1] = sa * np.clip(keep, 0, 1)
    if S.get("cutout"):
        a = smoothstep(0.35, 0.6, a)
    if S.get("opacity"):
        a = a * S["opacity"]
    rgb = bleed(rgb, a)
    rects = cell_rects(a, S["grid"])
    name, folder = S["name"], S["folder"]
    tdir = READY / f"Assets/_Game/Art/Textures/{folder}"
    bc_p, n_p, lay_p = tdir / f"T_{name}_BC.png", tdir / f"T_{name}_N.png", tdir / f"T_{name}_layout.json"
    n = normal_from(rgb, a, S["normal"]) if S["normal"] > 0 else None
    out_rgba = np.dstack([rgb, a])
    layout = {"texture": f"T_{name}_BC", "size": [W, H], "elements": []}
    for lab, rc in zip(S["labels"], rects):
        if rc is None:
            continue
        x0, y0, x1, y1 = rc
        layout["elements"].append({"name": lab, "px": [x0, y0, x1, y1],
                                   "uv": [round(x0 / W, 5), round(1 - y1 / H, 5), round(x1 / W, 5), round(1 - y0 / H, 5)]})
    layout["note"] = ("URP Decal Projector: UV Offset = (u0, v0), UV Scale = (u1-u0, v1-v0); projector size in metres keeps the element "
                      "aspect (px width/height). Real-world sizes: " + S["size_m"])
    if not sheet_only:
        tdir.mkdir(parents=True, exist_ok=True)
        Image.fromarray(np.clip(out_rgba * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA").save(bc_p)
        if n is not None:
            Image.fromarray(np.clip(n * 255 + 0.5, 0, 255).astype(np.uint8)).save(n_p)
        elif n_p.exists():
            n_p.unlink()
        lay_p.write_text(json.dumps(layout, indent=1))
        rel = lambda p: str(p.relative_to(READY))
        decal = folder.startswith("Decals")
        rows_ = []
        if decal:
            rows_.append({"manifest_id": aid, "file": rel(bc_p), "target_path": rel(bc_p), "type": "texture_decal_basecolor",
                          "import_notes": "Decal base map: Texture Type Default, sRGB ON, Alpha Is Transparency ON, Max Size 1024, DXT5 "
                          "Crunched q60, mips on, aniso 2, wrap Clamp. URP Decal Projector (Shader Graphs/Decal, Screen Space "
                          "technique); alpha = opacity; element rects in T_" + name + "_layout.json. Generated with "
                          f"openai/gpt-image-2.5 ({src}); guardrail-reviewed: no script, symbols or slogans."})
            if n is not None:
                rows_.append({"manifest_id": aid, "file": rel(n_p), "target_path": rel(n_p), "type": "texture_decal_normal",
                              "import_notes": "Normal map, OpenGL (+Y), sRGB OFF, Max Size 1024, DXT5nm Crunched q60, wrap Clamp. "
                              "Derived from luminance (dark = deep). Decal Normal Blend 0.5-1.0 (Affect Normal on)."})
        else:
            rows_.append({"manifest_id": aid, "file": rel(bc_p), "target_path": rel(bc_p), "type": "texture_foliage_cutout",
                          "import_notes": "Foliage card atlas: Texture Type Default, sRGB ON, Alpha Is Transparency ON, Max Size 1024, "
                          "DXT5 Crunched q55, mips on (Mip Maps Preserve Coverage ON, alpha cutoff 0.5), wrap Clamp. Material: "
                          "SG_FoliageWind or URP/Lit Alpha Clipping 0.5, Render Face Both. Element rects in T_" + name + "_layout.json."})
        rows_.append({"manifest_id": aid, "file": rel(lay_p), "target_path": rel(lay_p), "type": "data_json",
                      "import_notes": "Atlas layout (TextAsset): per-element pixel rect and UV rect (v up) + real-world sizes."})
        for r in rows_:
            r["tool"] = "decal_atlas"
        (RAW / aid / f"ready_{name}.json").write_text(json.dumps(rows_, indent=1))
    # review
    cell = 512
    panels = []
    chk = checker(H, W)
    panels.append((rgb * a[..., None] + chk * (1 - a[..., None]), "on checker"))
    bgname = S.get("bg")
    if bgname:
        bgf = next((ENV / bgname).glob("T_*_BC.png"), None)
        bg = np.asarray(Image.open(bgf).convert("RGB").resize((W, H))).astype(np.float32) / 255.0 if bgf else np.full((H, W, 3), 0.6)
        comp = rgb * a[..., None] + bg * (1 - a[..., None])
        if n is not None:
            nn = n * 2 - 1
            Ld = np.array([-0.5, 0.6, 0.62]); Ld /= np.linalg.norm(Ld)
            sh = np.clip((nn * Ld).sum(-1), 0, 1) / Ld[2]
            comp = comp * (0.35 + 0.65 * sh[..., None])
        panels.append((comp, f"on {bgname} (lit with N)"))
    else:
        panels.append((rgb * a[..., None] + np.array([0.75, 0.62, 0.45]) * (1 - a[..., None]), "on sand colour"))
    if n is not None:
        panels.append((n, "N"))
    panels.append((np.dstack([a] * 3), "alpha"))
    sheet = Image.new("RGB", (cell * len(panels), cell + 50), (28, 28, 28))
    d = ImageDraw.Draw(sheet)
    f = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 14)
    for i, (p, lab) in enumerate(panels):
        t = Image.fromarray(np.clip(p * 255, 0, 255).astype(np.uint8)).resize((cell, cell))
        if i == 0:
            dd = ImageDraw.Draw(t)
            for e in layout["elements"]:
                x0, y0, x1, y1 = [v * cell / W for v in e["px"]]
                dd.rectangle([x0, y0, x1, y1], outline=(255, 60, 60))
        sheet.paste(t, (i * cell, 20)); d.text((i * cell + 4, 2), lab, fill=(255, 255, 255), font=f)
    d.text((6, cell + 26), f"{aid} [{src}] T_{name}  {len(layout['elements'])} elements  alpha>0.5: {float((a > 0.5).mean()):.3f}",
           fill=(220, 220, 160), font=f)
    outp = RAW / "_review" / f"{aid}__atlas.png"
    sheet.save(outp)
    print("SHEET", outp, len(layout["elements"]))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("ids", nargs="+"); ap.add_argument("--src"); ap.add_argument("--sheet-only", action="store_true")
    a = ap.parse_args()
    for i in a.ids:
        process(i, a.src, a.sheet_only)
