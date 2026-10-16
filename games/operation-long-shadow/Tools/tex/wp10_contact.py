#!/usr/bin/env python3
"""WP10_CC0 contact sheets for shipped texture sets (reads the READY files, not the sources).

  Tools/.venv/bin/python -I Tools/tex/wp10_contact.py [--ids TEX_SAND_01,...] [--grid out.jpg]

Per set: BC tiled 2x2 | relit 2x2 (grazing sun 22 deg from top-left, uses N + M exactly as URP/Lit would:
metal R, AO G, smoothness A) | half-offset full-res crop (a seam shows as a cross in the middle) |
N, M.R, M.G, M.B, M.A thumbnails. Written to RawAssets/wp10/cc0/_contact/<id>.jpg.
"""
import argparse, json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wp10_texlib as T  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')
OUT = os.path.join(RAW, '_contact')


def relight(bc, n, m, el_deg=22.0, az_deg=135.0, sun=3.2, amb=0.35):
    alb = T.srgb_to_lin(bc[..., :3])
    nn = T.decode_normal(n)
    el, az = math.radians(el_deg), math.radians(az_deg)
    L = np.array([math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)], np.float32)
    ndl = np.clip((nn * L).sum(axis=2), 0, 1)
    metal, ao, smooth = m[..., 0], m[..., 1], m[..., 3]
    V = np.array([0, 0, 1], np.float32)
    H = (L + V) / np.linalg.norm(L + V)
    ndh = np.clip((nn * H).sum(axis=2), 0, 1)
    r = np.clip(1 - smooth, 0.03, 1)
    a2 = (r * r) ** 2
    d = ndh * ndh * (a2 - 1) + 1
    D = a2 / (math.pi * d * d)
    f0 = 0.04 * (1 - metal[..., None]) + alb * metal[..., None]
    spec = f0 * (D * 0.25)[..., None]
    diff = alb * (1 - metal[..., None])
    col = diff * (ndl[..., None] * sun + amb * ao[..., None]) + spec * (ndl[..., None] * sun) + f0 * 0.1 * ao[..., None]
    col = col * 0.8
    col = col / (1 + col)  # Reinhard
    return T.lin_to_srgb(col * 1.6)


def tile2(a):
    return np.concatenate([np.concatenate([a, a], 1)] * 2, 0)


def to_img(a, size):
    a = np.clip(a, 0, 1)
    if a.ndim == 2:
        a = np.stack([a] * 3, 2)
    im = Image.fromarray((a[..., :3] * 255 + 0.5).astype(np.uint8))
    return im.resize(size, Image.LANCZOS)


def checker_alpha(bc):
    if bc.shape[2] < 4:
        return bc[..., :3]
    h, w = bc.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    ch = (((yy // 32) + (xx // 32)) % 2).astype(np.float32) * 0.25 + 0.5
    a = bc[..., 3:4]
    return bc[..., :3] * a + ch[..., None] * (1 - a)


def sheet(title, bc, n, m, out, extra_lines=()):
    W = 1560
    img = Image.new('RGB', (W, 820), (24, 24, 24))
    dr = ImageDraw.Draw(img)
    S = 512
    bcv = checker_alpha(bc)
    img.paste(to_img(tile2(bcv), (S, S)), (8, 40))
    lit = relight(bc, n, m)
    if bc.shape[2] == 4:
        lit = lit * bc[..., 3:4] + 0.1 * (1 - bc[..., 3:4])
    img.paste(to_img(tile2(lit), (S, S)), (8 + S + 8, 40))
    h, w = bc.shape[:2]
    rolled = np.roll(np.roll(bcv, h // 2, 0), w // 2, 1)
    c = min(384, h // 2)
    crop = rolled[h // 2 - c // 2:h // 2 + c // 2, w // 2 - c // 2:w // 2 + c // 2]
    img.paste(to_img(crop, (S - 16, S - 16)), (8 + 2 * (S + 8), 40))
    t = 186
    thumbs = [('N', n), ('M.R metal', m[..., 0]), ('M.G AO', m[..., 1]), ('M.B height', m[..., 2]), ('M.A smooth', m[..., 3])]
    for i, (lab, a) in enumerate(thumbs):
        x = 8 + i * (t + 10)
        img.paste(to_img(a, (t, t)), (x, 600))
        dr.text((x, 790), lab, fill=(220, 220, 220))
    dr.text((8, 10), title, fill=(255, 230, 160))
    dr.text((8, 24), 'BC tiled 2x2  |  relit 2x2 (sun 22 deg, URP/Lit packing)  |  half-offset crop (seam check)', fill=(170, 170, 170))
    for i, line in enumerate(extra_lines):
        dr.text((8 + 5 * (t + 10) + 10, 600 + 16 * i), line, fill=(200, 200, 200))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out, quality=88)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ids')
    a = ap.parse_args()
    rep = json.load(open(os.path.join(RAW, '_reports', 'texsets.json')))
    if a.ids:
        want = set(a.ids.split(','))
        rep = [r for r in rep if r['manifest_id'] in want]
    for r in rep:
        f = r['files']
        bc = T.load(f['BC'])
        n = T.load(f['N'], 'RGB')
        m = T.load(f['M'], 'RGBA')
        st = r['stats']
        lines = [f"{r['name']}  {r['size']}px  tile {r['tile_m']} m  {r['priority']}",
                 f"albedo sRGB mean {st['albedo_mean_srgb']}",
                 f"metal {st['metal_mean']}  smooth {st['smooth_mean']}  ao {st['ao_mean']}",
                 f"seam score bc {st['seam_score_bc']}  n {st['seam_score_n']}",
                 f"terrain layer: {r['terrain_layer']}  alpha: {r['has_alpha']}",
                 'files: ' + ' '.join(sorted(f.keys()))]
        out = sheet(f"{r['manifest_id']}  ({r['asset_id']})", bc, n, m, os.path.join(OUT, r['manifest_id'] + '.jpg'), lines)
        print(out)


if __name__ == '__main__':
    main()
