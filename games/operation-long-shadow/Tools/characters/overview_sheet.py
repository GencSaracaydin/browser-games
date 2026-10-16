#!/usr/bin/env python3
"""One-page overview of every WP05 body (3/4 views from the preview renders) -> RawAssets/wp05/_review/ALL_bodies_overview.png"""
import pathlib
from PIL import Image, ImageDraw

W5 = pathlib.Path(__file__).resolve().parents[2] / "RawAssets/wp05"
NAMES = ["Calder", "CalderBooth", "CalderReel", "Hollis", "Tinker", "Sparrow", "LegionA", "LegionB", "LegionC", "GreycoatA",
         "GreycoatB", "GreycoatBNVG", "Interrogator", "Magpie", "Harrou", "CalderFP"]
ims = []
for n in NAMES:
    for v in ("q3", "front"):
        p = W5 / f"_work/previews/{n}/prev_{v}.png"
        if p.exists():
            im = Image.open(p).convert("RGB")
            w, h = im.size
            ims.append((n, im.crop((int(w * 0.18), 0, int(w * 0.82), h))))
            break
w, h = ims[0][1].size
S = Image.new("RGB", (w * 8, h * ((len(ims) + 7) // 8)), (20, 20, 20))
for i, (n, im) in enumerate(ims):
    S.paste(im.resize((w, h)), ((i % 8) * w, (i // 8) * h))
    ImageDraw.Draw(S).text(((i % 8) * w + 6, (i // 8) * h + 6), n, fill=(255, 255, 0))
S.save(W5 / "_review/ALL_bodies_overview.png")
print(W5 / "_review/ALL_bodies_overview.png", S.size)
