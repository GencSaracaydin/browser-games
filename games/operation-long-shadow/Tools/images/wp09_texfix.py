#!/usr/bin/env python3
"""Post-paint fixes on processed WP09 prop textures (run after wp09_prop.py, which rewrites the maps).
  Tools/.venv/bin/python Tools/images/wp09_texfix.py scrambler
scrambler: VU meter face in T_ScramblerBox_BC gets scale ticks and a red zone (the moving needle is the Needle child);
           texture-space geometry from bl_uvprobe.py: meter 'up' = -x, 'right' = -y, needle pivot (795, 587) px, arc r 35 px.
"""
import sys, math, pathlib
from PIL import Image, ImageDraw
ROOT = pathlib.Path(__file__).resolve().parents[2]
T = ROOT / "RawAssets/wp09/ready/Assets/_Game/Art/Textures/Props"


def scrambler():
    p = T / "scrambler_box/T_ScramblerBox_BC.png"
    im = Image.open(p).convert("RGB")
    k = 6
    box = (735, 545, 815, 630)
    crop = im.crop(box).resize(((box[2] - box[0]) * k, (box[3] - box[1]) * k), Image.LANCZOS)
    d = ImageDraw.Draw(crop)
    cx, cy = (795 - box[0]) * k, (587 - box[1]) * k
    def pt(phi_deg, r):
        ph = math.radians(phi_deg)       # meter frame: up = -x, right = -y
        return (cx - r * math.cos(ph) * k, cy - r * math.sin(ph) * k)
    # red zone along the arc, right end
    for ph in [x / 4 for x in range(4 * 22, 4 * 46)]:
        a, b = pt(ph, 34.0), pt(ph, 37.0)
        d.line([a, b], fill=(168, 32, 26), width=int(1.4 * k))
    for i in range(11):
        ph = -45 + i * 9
        major = i % 2 == 0
        a, b = pt(ph, 34.5), pt(ph, 40.0 if major else 37.5)
        d.line([a, b], fill=(186, 36, 28) if ph > 21 else (58, 52, 46), width=int((0.9 if major else 0.6) * k))
    crop = crop.resize((box[2] - box[0], box[3] - box[1]), Image.LANCZOS)
    im.paste(crop, box[:2])
    im.save(p)
    print("painted", p)


if __name__ == "__main__":
    {"scrambler": scrambler}[sys.argv[1]]()
