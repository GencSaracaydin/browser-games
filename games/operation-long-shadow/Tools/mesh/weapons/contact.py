#!/usr/bin/env python3
"""Make a labelled contact sheet from images. Usage: contact.py out.png cols cell_w img1 [img2 ...]"""
import sys
from PIL import Image, ImageDraw
out, cols, cw = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
paths = sys.argv[4:]
ims = []
for p in paths:
    im = Image.open(p).convert("RGB")
    im.thumbnail((cw, cw))
    ims.append((p, im))
ch = max(im.size[1] for _, im in ims) + 22
rows = (len(ims) + cols - 1) // cols
sheet = Image.new("RGB", (cols * cw, rows * ch), (128, 128, 128))
d = ImageDraw.Draw(sheet)
for i, (p, im) in enumerate(ims):
    x, y = (i % cols) * cw, (i // cols) * ch
    sheet.paste(im, (x, y + 20))
    d.text((x + 4, y + 4), "/".join(p.split("/")[-3:-1]), fill=(0, 0, 0))
sheet.save(out)
print(out, sheet.size)
