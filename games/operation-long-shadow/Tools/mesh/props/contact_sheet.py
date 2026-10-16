#!/usr/bin/env python3
"""Grid contact sheet of images with labels.  Tools/.venv/bin/python contact_sheet.py out.png --cell 512 --cols 4 img1 img2 ...
Labels default to the parent folder name + file stem. Transparent images are shown over a checker."""
import argparse, pathlib
from PIL import Image, ImageDraw, ImageFont
ap = argparse.ArgumentParser()
ap.add_argument("out"); ap.add_argument("imgs", nargs="+")
ap.add_argument("--cell", type=int, default=480); ap.add_argument("--cols", type=int, default=4)
ap.add_argument("--labels", nargs="*")
a = ap.parse_args()
n = len(a.imgs); cols = min(a.cols, n); rows = (n + cols - 1) // cols
lab_h = 22
sheet = Image.new("RGB", (cols * a.cell, rows * (a.cell + lab_h)), (40, 40, 40))
d = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 15)
except Exception:
    font = ImageFont.load_default()
def checker(w, h, s=16):
    im = Image.new("RGB", (w, h), (200, 200, 200)); dd = ImageDraw.Draw(im)
    for y in range(0, h, s):
        for x in range(0, w, s):
            if (x // s + y // s) % 2: dd.rectangle([x, y, x + s - 1, y + s - 1], fill=(150, 150, 150))
    return im
for i, p in enumerate(a.imgs):
    im = Image.open(p)
    im.thumbnail((a.cell, a.cell))
    x, y = (i % cols) * a.cell, (i // cols) * (a.cell + lab_h)
    if im.mode in ("RGBA", "LA"):
        bg = checker(im.width, im.height); bg.paste(im, (0, 0), im.convert("RGBA")); im = bg
    sheet.paste(im.convert("RGB"), (x + (a.cell - im.width) // 2, y + lab_h + (a.cell - im.height) // 2))
    pp = pathlib.Path(p)
    lab = a.labels[i] if a.labels and i < len(a.labels) else f"{pp.parent.name}/{pp.stem}"
    d.text((x + 4, y + 3), lab[:60], fill=(255, 255, 255), font=font)
sheet.save(a.out)
print(a.out, sheet.size)
