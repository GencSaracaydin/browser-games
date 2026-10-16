#!/usr/bin/env python3
"""Contact sheet from a list of PNGs: sheet.py out.png cols file1 file2 ... | sheet.py out.png cols --json files.json [--label]"""
import sys, json, math
from PIL import Image, ImageDraw
out, cols = sys.argv[1], int(sys.argv[2])
fs = json.load(open(sys.argv[4])) if sys.argv[3] == "--json" else sys.argv[3:]
fs = [f for f in fs if not f.startswith("--")]
ims = [Image.open(f).convert("RGB") for f in fs]
w = max(i.width for i in ims); h = max(i.height for i in ims)
rows = math.ceil(len(ims) / cols)
S = Image.new("RGB", (w * cols, h * rows), (20, 20, 20))
for k, (f, im) in enumerate(zip(fs, ims)):
    S.paste(im, ((k % cols) * w, (k // cols) * h))
    if "--label" in sys.argv:
        ImageDraw.Draw(S).text(((k % cols) * w + 4, (k // cols) * h + 4), f.split("/")[-1][:40], fill=(255, 255, 0))
S.save(out)
print(out, S.size)
