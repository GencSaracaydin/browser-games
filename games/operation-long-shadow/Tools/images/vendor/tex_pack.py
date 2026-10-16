#!/usr/bin/env python3
"""Pack generator textures into the Unity set T_<Name>_BC / _N / _M (/ _E).

  Tools/.venv/bin/python tex_pack.py --work <dir> --name Sandbag --out <tex dir> --size 1024 [--emissive orange|red]
Sources (first found): bake_basecolor.png | src_basecolor.*, bake_normal.png | src_normal.*,
glTF MR (src_mr.*: G roughness, B metallic) or bake_roughness.png + bake_metallic.png, ao.png.
_M = R metallic, G ambient occlusion, B 0, A smoothness (1 - roughness): the URP/Lit metallic map
(smoothness from its alpha) and occlusion map (URP samples G) in one texture. Normal maps are OpenGL (+Y).
"""
import argparse, glob, json, os, pathlib
import numpy as np
from PIL import Image

ap = argparse.ArgumentParser()
ap.add_argument("--work", required=True); ap.add_argument("--name", required=True)
ap.add_argument("--out", required=True); ap.add_argument("--size", type=int, default=1024)
ap.add_argument("--emissive"); ap.add_argument("--alpha", action="store_true")
ap.add_argument("--ao-strength", type=float, default=1.0)
ap.add_argument("--match-ref", help="reference photo: pull the albedo mean (per channel, linear) toward it")
ap.add_argument("--match-strength", type=float, default=0.7)
ap.add_argument("--gain", nargs=3, type=float, help="explicit linear RGB gain for the base colour")
ap.add_argument("--keep-metal", action="store_true")
ap.add_argument("--sat", type=float, default=1.0, help="saturation multiplier for the base colour")
ap.add_argument("--bc-size", type=int, help="base colour size if larger than --size (hero vehicles: BC 2048, N/M 1024)")
a = ap.parse_args()
W = pathlib.Path(a.work); O = pathlib.Path(a.out); O.mkdir(parents=True, exist_ok=True)


def find(*pats):
    for p in pats:
        g = sorted(glob.glob(str(W / p)))
        if g:
            return g[0]
    return None


def load(p, mode):
    im = Image.open(p)
    return im.convert(mode)


def rs(im, s):
    return im if im.size == (s, s) else im.resize((s, s), Image.LANCZOS)


S = a.size
SB = a.bc_size or S
bc_p = find("bake_basecolor.png", "src_basecolor.*")
n_p = find("bake_normal.png", "src_normal.*")
mr_p = None if find("bake_roughness.png") else find("src_mr.*")
info = {"bc": bc_p, "n": n_p, "mr": mr_p}

bc = rs(load(bc_p, "RGBA" if a.alpha else "RGB"), SB)


def to_lin(x):
    return np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4)


def to_srgb(x):
    x = np.clip(x, 0, 1)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * x ** (1 / 2.4) - 0.055)


if a.match_ref or a.gain or a.sat != 1.0:
    arr = np.asarray(bc).astype(np.float32) / 255
    lin = to_lin(arr[..., :3])
    gain = np.ones(3, np.float32)
    if a.match_ref:
        # luminance CDF matching (used UV area vs object pixels of the studio reference) + chroma mean transfer
        ref = np.asarray(Image.open(a.match_ref).convert("RGB")).astype(np.float32) / 255
        rl = to_lin(ref)
        obj = ~((ref.min(2) > 0.86) & (ref.max(2) - ref.min(2) < 0.06))   # not the white studio backdrop
        ao_p0 = find("ao.png")
        used = np.ones(lin.shape[:2], bool)
        if ao_p0:
            used = np.asarray(rs(load(ao_p0, "L"), SB)) > 8
        W3 = np.array([0.2126, 0.7152, 0.0722], np.float32)
        Lb = (lin * W3).sum(2); Lr = (rl[obj] * W3).sum(1) / 0.9
        qs = np.linspace(0, 1, 257)
        src_q = np.quantile(Lb[used], qs); dst_q = np.quantile(Lr, qs)
        Lm = np.interp(Lb, src_q, dst_q)
        k = a.match_strength
        Lt = Lb * (1 - k) + Lm * k
        lin = lin * (Lt / np.maximum(Lb, 1e-5))[..., None]
        # chroma: per-channel ratio to luminance
        cb = (lin[used] / np.maximum((lin[used] * W3).sum(1, keepdims=True), 1e-5)).mean(0)
        cr = (rl[obj] / np.maximum((rl[obj] * W3).sum(1, keepdims=True), 1e-5)).mean(0)
        gain = np.clip((cr / cb) ** k, 0.7, 1.4).astype(np.float32)
    if a.gain:
        gain = gain * np.array(a.gain, np.float32)
    lin = lin * gain
    if a.sat != 1.0:
        lum = (lin * np.array([0.2126, 0.7152, 0.0722])).sum(2, keepdims=True)
        lin = lum + (lin - lum) * a.sat
    out = to_srgb(lin)
    if arr.shape[2] == 4:
        out = np.concatenate([out, arr[..., 3:]], 2)
    bc = Image.fromarray(np.clip(out * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA" if arr.shape[2] == 4 else "RGB")
    info["albedo_gain"] = [round(float(g), 3) for g in gain]
glass = None
gp = W / "glass_tris.json"
if gp.exists():
    from PIL import ImageDraw
    gm = Image.new("L", (SB, SB), 0); gd = ImageDraw.Draw(gm)
    for t in json.loads(gp.read_text()):
        gd.polygon([(u * SB, (1 - v) * SB) for u, v in t], fill=255)
    from PIL import ImageFilter as _IF
    gm = gm.filter(_IF.MaxFilter(3))
    glass = np.asarray(gm).astype(np.float32) / 255
    arr = np.asarray(bc).astype(np.float32) / 255
    # dark, slightly green-grey tinted dusty glass; keeps a little of the original variation
    lum = arr[..., :3].mean(2, keepdims=True)
    gcol = np.array([0.07, 0.085, 0.085]) + 0.12 * (lum - lum.mean())
    arr[..., :3] = arr[..., :3] * (1 - glass[..., None]) + gcol * glass[..., None]
    bc = Image.fromarray(np.clip(arr * 255 + 0.5, 0, 255).astype(np.uint8), bc.mode)
    info["glass_coverage"] = float(glass.mean())
bc.save(O / f"T_{a.name}_BC.png", optimize=True)

if n_p:
    n = np.asarray(rs(load(n_p, "RGB"), S)).astype(np.float32) / 255 * 2 - 1
    ln = np.linalg.norm(n, axis=2, keepdims=True); n = n / np.maximum(ln, 1e-6)
    Image.fromarray(np.clip((n + 1) / 2 * 255 + 0.5, 0, 255).astype(np.uint8)).save(O / f"T_{a.name}_N.png", optimize=True)

if mr_p:
    mr = np.asarray(rs(load(mr_p, "RGB"), S)).astype(np.float32) / 255
    rough, metal = mr[..., 1], mr[..., 2]
else:
    rp, mp = find("bake_roughness.png"), find("bake_metallic.png")
    rough = np.asarray(rs(load(rp, "L"), S)).astype(np.float32) / 255 if rp else np.full((S, S), 0.7, np.float32)
    metal = np.asarray(rs(load(mp, "L"), S)).astype(np.float32) / 255 if mp else np.zeros((S, S), np.float32)
if not a.keep_metal:
    # generator MR maps carry low-level metallic noise on paint/wood: push toward binary metal (PBR-plausible)
    t = np.clip((metal - 0.3) / 0.35, 0, 1)
    metal = t * t * (3 - 2 * t)
ao_p = find("ao.png")
ao = np.asarray(rs(load(ao_p, "L"), S)).astype(np.float32) / 255 if ao_p else np.ones((S, S), np.float32)
ao = 1 - (1 - ao) * a.ao_strength
smooth = 1 - rough
if glass is not None:
    g_s = glass if SB == S else np.asarray(Image.fromarray((glass * 255).astype(np.uint8)).resize((S, S), Image.LANCZOS)).astype(np.float32) / 255
    smooth = smooth * (1 - g_s) + 0.9 * g_s
    metal = metal * (1 - g_s)
m = np.stack([metal, ao, np.zeros_like(ao), smooth], axis=2)
Image.fromarray(np.clip(m * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA").save(O / f"T_{a.name}_M.png", optimize=True)
info["stats"] = {"metal_mean": float(metal.mean()), "rough_mean": float(rough.mean()), "ao_mean": float(ao.mean())}

if a.emissive:
    rgb = np.asarray(bc.convert("RGB")).astype(np.float32) / 255
    import colorsys
    mx, mn = rgb.max(2), rgb.min(2)
    sat = (mx - mn) / np.maximum(mx, 1e-6)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    if a.emissive == "orange":   # glowing valves: warm, saturated, bright
        mask = (r > 0.33) & (r > g * 1.25) & (r > b * 1.6)
        tint = np.array([1.0, 0.55, 0.18])
    else:                        # red lamp glass
        mask = (r > 0.35) & (sat > 0.5) & (g < 0.45 * r) & (b < 0.5 * r)
        tint = np.array([1.0, 0.12, 0.08])
    mask = mask.astype(np.float32)
    from PIL import ImageFilter
    from scipy import ndimage
    dil = ndimage.binary_dilation(mask > 0, iterations=2)
    lab, nlab = ndimage.label(dil)
    areas = ndimage.sum(dil, lab, range(1, nlab + 1))
    keep = np.isin(lab, 1 + np.nonzero(areas >= 0.00015 * SB * SB)[0])   # drop rust specks, keep tube/lamp clusters
    mask = (mask > 0) & keep
    mimg = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(2.0))
    mask = np.asarray(mimg).astype(np.float32) / 255
    e = (0.35 * rgb + 0.65 * tint) * mask[..., None] * np.clip(0.4 + mx[..., None], 0, 1)
    es = 512 if SB > 512 else SB
    Image.fromarray(np.clip(e * 255, 0, 255).astype(np.uint8)).resize((es, es), Image.LANCZOS).save(O / f"T_{a.name}_E.png", optimize=True)
    info["emissive_coverage"] = float(mask.mean())
(W / f"texpack_{a.name}.json").write_text(json.dumps(info, indent=1))
print(json.dumps(info))
