#!/usr/bin/env python3
"""Crop a region of an image, upscale it and draw a labelled pixel grid (for locating paint-out boxes).
  Tools/.venv/bin/python Tools/images/gridcrop.py in.png out.png x0 y0 x1 y1 [--step 16] [--scale 3]"""
import sys
from PIL import Image, ImageDraw, ImageFont
a = sys.argv
src, out = a[1], a[2]
x0, y0, x1, y1 = map(int, a[3:7])
step = int(a[a.index("--step") + 1]) if "--step" in a else 16
sc = int(a[a.index("--scale") + 1]) if "--scale" in a else 3
im = Image.open(src).convert("RGB").crop((x0, y0, x1, y1))
im = im.resize((im.width * sc, im.height * sc), Image.NEAREST)
d = ImageDraw.Draw(im)
f = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 11)
for x in range((x0 // step + 1) * step, x1, step):
    d.line([((x - x0) * sc, 0), ((x - x0) * sc, im.height)], fill=(255, 0, 255) if x % (step * 4) == 0 else (0, 200, 255), width=1)
    if x % (step * 2) == 0:
        d.text(((x - x0) * sc + 2, 2), str(x), fill=(255, 255, 0), font=f)
for y in range((y0 // step + 1) * step, y1, step):
    d.line([(0, (y - y0) * sc), (im.width, (y - y0) * sc)], fill=(255, 0, 255) if y % (step * 4) == 0 else (0, 200, 255), width=1)
    if y % (step * 2) == 0:
        d.text((2, (y - y0) * sc + 2), str(y), fill=(255, 255, 0), font=f)
im.save(out)
