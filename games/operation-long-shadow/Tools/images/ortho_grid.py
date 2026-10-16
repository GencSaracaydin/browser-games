#!/usr/bin/env python3
"""Overlay a metric grid on bl_ortho.py renders: ortho_grid.py <out-prefix> [step_m=0.01] -> <prefix>_<view>_grid.png"""
import json, sys, math
from PIL import Image, ImageDraw, ImageFont
pre = sys.argv[1]; step = float(sys.argv[2]) if len(sys.argv) > 2 else 0.01
J = json.load(open(pre + "_ortho.json"))
f = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 13)
for v in ("front", "top", "side"):
    o = J[v]; im = Image.open(f"{pre}_{v}.png").convert("RGB"); d = ImageDraw.Draw(im); R = o["res"]; S = o["scale_m"]
    u0, u1 = o["cx"] - S / 2, o["cx"] + S / 2; v0, v1 = o["cy"] - S / 2, o["cy"] + S / 2
    k = math.floor(u0 / step)
    while k * step <= u1:
        u = k * step; px = (u - u0) / S * R; major = abs(round(u / (step * 5)) * step * 5 - u) < 1e-9
        d.line([(px, 0), (px, R)], fill=(255, 0, 0) if major else (255, 170, 170), width=1)
        if major: d.text((px + 2, 2), f"{u:+.2f}", fill=(200, 0, 0), font=f)
        k += 1
    k = math.floor(v0 / step)
    while k * step <= v1:
        vv = k * step; py = (o["cy"] - vv) / S * R + R / 2; major = abs(round(vv / (step * 5)) * step * 5 - vv) < 1e-9
        d.line([(0, py), (R, py)], fill=(0, 0, 255) if major else (170, 170, 255), width=1)
        if major: d.text((2, py + 2), f"{vv:+.2f}", fill=(0, 0, 200), font=f)
        k += 1
    im.save(f"{pre}_{v}_grid.png")
print("ok")
