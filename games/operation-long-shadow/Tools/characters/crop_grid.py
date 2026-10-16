#!/usr/bin/env python3
"""Crop a texture region with a labelled pixel grid (for authoring paint-out rectangles).
  Tools/.venv/bin/python Tools/characters/crop_grid.py <tex> x0 y0 x1 y1 <out.png> [grid=50] [scale=1]
Coordinates are pixels of the full-resolution texture."""
import sys
from PIL import Image, ImageDraw
p, x0, y0, x1, y1, out = sys.argv[1], *map(int, sys.argv[2:6]), sys.argv[6]
g = int(sys.argv[7]) if len(sys.argv) > 7 else 50
sc = float(sys.argv[8]) if len(sys.argv) > 8 else 1.0
im = Image.open(p).convert("RGB").crop((x0, y0, x1, y1))
im = im.resize((int(im.width * sc), int(im.height * sc)))
d = ImageDraw.Draw(im)
for x in range((x0 // g + 1) * g, x1, g):
    X = (x - x0) * sc
    d.line([(X, 0), (X, im.height)], fill=(255, 0, 255) if x % (g * 4) == 0 else (0, 255, 255), width=1)
    d.text((X + 2, 2), str(x), fill=(255, 255, 0))
for y in range((y0 // g + 1) * g, y1, g):
    Y = (y - y0) * sc
    d.line([(0, Y), (im.width, Y)], fill=(255, 0, 255) if y % (g * 4) == 0 else (0, 255, 255), width=1)
    d.text((2, Y + 2), str(y), fill=(255, 255, 0))
im.save(out)
