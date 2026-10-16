#!/usr/bin/env python3
"""Assemble per-prop contact sheets from wp10_prop_preview.py tiles + the shipped base-colour textures.

  Tools/.venv/bin/python -I Tools/tex/wp10_prop_sheet.py [--ids ...] [--grid PREFIX]

RawAssets/wp10/cc0/_contact/PROP_<ID>.jpg (one row per output: front | back | LOD1 | LOD2 | BC texture).
"""
import argparse, glob, json, os
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')
READY = os.path.join(ROOT, 'RawAssets', 'wp10', 'ready')
T = 300


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ids')
    ap.add_argument('--grid')
    a = ap.parse_args()
    specs = json.load(open(os.path.join(READY, 'materials_cc0.json')))
    sheets = []
    for rp in sorted(glob.glob(os.path.join(RAW, '_reports', 'props', 'PH_*.json'))):
        rep = json.load(open(rp))
        mid = rep['manifest_id']
        if a.ids and mid not in a.ids.split(','):
            continue
        rows = []
        for out in rep['outputs']:
            v = out['variant'] or 'main'
            tiles = [os.path.join(RAW, '_contact', 'props', f'{mid}__{v}__{k}.png') for k in ('front', 'back', 'lod1', 'lod2')]
            bcs = [s['textures'].get('_BaseMap') for s in specs if s['manifest_id'] == mid and s['textures'].get('_BaseMap')]
            img = Image.new('RGB', (5 * T, T + 30), (22, 22, 22))
            d = ImageDraw.Draw(img)
            for i, t in enumerate(tiles):
                if os.path.exists(t):
                    img.paste(Image.open(t).convert('RGB').resize((T, T)), (i * T, 30))
            if bcs:
                img.paste(Image.open(os.path.join(READY, bcs[0])).convert('RGB').resize((T, T)), (4 * T, 30))
            d.text((4, 4), f"{mid}  {out['unity_name']}  tris {out['lod_tris']}  size {out['size_unity_m']} m", fill=(255, 225, 140))
            d.text((4, 17), 'front 3/4 | back 3/4 | LOD1 | LOD2 | BC (shipped)', fill=(160, 160, 160))
            rows.append(img)
        sheet = Image.new('RGB', (5 * T, sum(r.size[1] for r in rows)), (0, 0, 0))
        y = 0
        for r in rows:
            sheet.paste(r, (0, y))
            y += r.size[1]
        p = os.path.join(RAW, '_contact', f'PROP_{mid}.jpg')
        sheet.save(p, quality=86)
        sheets.append(p)
    if a.grid:
        per = 5
        for k in range(0, len(sheets), per):
            ims = [Image.open(p) for p in sheets[k:k + per]]
            g = Image.new('RGB', (5 * T, sum(i.size[1] for i in ims)), (0, 0, 0))
            y = 0
            for i in ims:
                g.paste(i, (0, y))
                y += i.size[1]
            g.save(f'{a.grid}_{k // per:02d}.jpg', quality=82)
            print(f'{a.grid}_{k // per:02d}.jpg')


if __name__ == '__main__':
    main()
