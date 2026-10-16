#!/usr/bin/env python3
"""Contact sheet: grid of images (alpha shown over a checker) with file labels.

  Tools/.venv/bin/python Tools/images/contact.py out.png img1 img2 ... [--cell 512] [--cols 3] [--title "..."] [--bg dark|checker]
"""
import argparse, os
from PIL import Image, ImageDraw, ImageFont


def font(sz):
    for p in ("/System/Library/Fonts/Supplemental/Arial.ttf", "/System/Library/Fonts/Helvetica.ttc"):
        try:
            return ImageFont.truetype(p, sz)
        except Exception:
            pass
    return ImageFont.load_default()


def checker(w, h, s=16):
    im = Image.new("RGB", (w, h), (90, 90, 90))
    d = ImageDraw.Draw(im)
    for y in range(0, h, s):
        for x in range(0, w, s):
            if (x // s + y // s) % 2:
                d.rectangle([x, y, x + s - 1, y + s - 1], fill=(130, 130, 130))
    return im


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("imgs", nargs="+")
    ap.add_argument("--cell", type=int, default=512); ap.add_argument("--cols", type=int, default=3)
    ap.add_argument("--title", default=""); ap.add_argument("--bg", default="dark")
    a = ap.parse_args()
    f = font(15)
    cols = min(a.cols, len(a.imgs))
    rows = (len(a.imgs) + cols - 1) // cols
    th = 26 if a.title else 0
    sheet = Image.new("RGB", (cols * a.cell, rows * (a.cell + 22) + th), (24, 24, 24))
    d = ImageDraw.Draw(sheet)
    if a.title:
        d.text((6, 4), a.title, fill=(230, 220, 150), font=font(18))
    for i, p in enumerate(a.imgs):
        x, y = (i % cols) * a.cell, th + (i // cols) * (a.cell + 22)
        try:
            im = Image.open(p)
            info = f"{im.size[0]}x{im.size[1]} {im.mode}"
            im.thumbnail((a.cell - 4, a.cell - 4))
            if im.mode in ("RGBA", "LA", "P"):
                im = im.convert("RGBA")
                bg = checker(im.width, im.height) if a.bg == "checker" else Image.new("RGB", im.size, (40, 40, 40))
                bg.paste(im, (0, 0), im)
                im = bg
            sheet.paste(im.convert("RGB"), (x + (a.cell - im.width) // 2, y + 22 + (a.cell - 4 - im.height) // 2))
        except Exception as e:  # noqa
            info = f"ERR {e}"
        d.text((x + 4, y + 3), f"{os.path.basename(os.path.dirname(p))}/{os.path.basename(p)}  {info}"[:90], fill=(255, 255, 255), font=f)
    sheet.save(a.out)
    print(a.out, sheet.size)


if __name__ == "__main__":
    main()
