#!/usr/bin/env python3
"""Compact review: one row per asset (front-left, back-right, front, close-up, wireframe, reference).
  Tools/.venv/bin/python review_rows.py out.png ID:tag [ID:tag ...] [--cell 300]"""
import sys, pathlib
from PIL import Image, ImageDraw, ImageFont
RAW = pathlib.Path(__file__).resolve().parents[3] / "RawAssets/wp10/fal"
args = [x for x in sys.argv[2:] if not x.startswith("--")]
cell = int(sys.argv[sys.argv.index("--cell") + 1]) if "--cell" in sys.argv else 300
args = [x for x in args if not x.isdigit()]
cols = ["render_v0", "render_v1", "render_v4", "render_v5", "render_v6", "REF"]
W, H = cell * len(cols), (cell + 18) * len(args)
im = Image.new("RGB", (W, H), (25, 25, 25)); d = ImageDraw.Draw(im)
f = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 13)
for r, it in enumerate(args):
    aid, tag = it.split(":")
    y = r * (cell + 18)
    d.text((4, y + 2), it, fill=(255, 255, 120), font=f)
    for c, k in enumerate(cols):
        p = RAW / aid / ("ref_t1.png" if k == "REF" else f"work_{tag}/{k}.png")
        if k == "REF":
            alt = sorted((RAW / aid).glob("ref*.png"))
            refmap = {"tripo_tarpoff_t1": "ref_e1_tarpoff.png", "tripo_closed_t1": "ref_closed_t1.png"}
            if tag in refmap: p = RAW / aid / refmap[tag]
        if not p.exists():
            continue
        t = Image.open(p).convert("RGB"); t.thumbnail((cell, cell))
        im.paste(t, (c * cell + (cell - t.width) // 2, y + 18))
im.save(sys.argv[1]); print(sys.argv[1], im.size)
