#!/usr/bin/env python3
"""WP09 guardrail paint-out + look fixes on the Poly Haven booth textures (GDD 2.3-3: no real brands or words).

  Tools/.venv/bin/python -I Tools/images/wp09_paintout.py

Uses the stroke-mask + OpenCV inpaint approach of Tools/tex/wp10_paintout.py (imported). Boxes are 1k texture
pixels (x0, y0, x1, y1). Writes RawAssets/wp09/<ID>/clean/<texture> (preferred by wp09_props_tex.py) and a
before/after sheet RawAssets/wp09/_review/PAINTOUT_<ID>_<tex>.jpg.
Extra ops: 'panel' fills a box with the median colour of a sample box plus fine grain (normal maps: flat +Z);
the lighter gets a bronze -> brushed steel recolour (GDD 4.2: "a steel lighter").
"""
import importlib.util, os, pathlib
import numpy as np
import cv2

ROOT = pathlib.Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("wp10_paintout", ROOT / "Tools/tex/wp10_paintout.py")
P = importlib.util.module_from_spec(spec)
spec.loader.exec_module(P)
RAW = ROOT / "RawAssets/wp09"

JOBS = {
    "PH_TELEVISION_01": [("Television_01_diff_1k.jpg", [((383, 714, 468, 734), "all"),      # brand on the control plate
                                                       ((510, 768, 532, 852), "bright"),   # brand on the side panel
                                                       ((660, 752, 786, 842), "dark"),     # WARNING sticker text -> blank sticker
                                                       ((852, 52, 880, 118), "dark")])],   # rating label text -> blank label
    "PH_TELEVISION_02": [("television_02_diff_1k.jpg", [((273, 455, 289, 540), "all")])],  # brand + model on the bezel strip
    "PH_WALL_CLOCK": [("wall_clock_diff_1k.jpg", [((254, 612, 306, 658), "dark")])],         # dial brand
    "PH_VINTAGE_LIGHTER": [("vintage_lighter_diff_1k.jpg", [((48, 444, 312, 516), "all"),           # embossed brand
                                                           ((28, 705, 295, 988), "panel:150,600,40,30")]),  # emblem panel
                           ("vintage_lighter_nor_gl_1k.jpg", [((48, 444, 312, 516), "flat"),
                                                             ((28, 705, 295, 988), "flat")])],
}


def grain(shape, sigma=3.5, seed=7):
    rng = np.random.default_rng(seed)
    g = rng.normal(0, sigma, shape[:2]).astype(np.float32)
    # brushed: stretch the grain horizontally
    g = cv2.GaussianBlur(g, (0, 0), sigmaX=6.0, sigmaY=0.6)
    return (g / max(g.std(), 1e-3) * sigma)[..., None]


def steel(img):
    """Bronze/copper finish -> worn brushed steel: desaturate, lift and cool slightly (sRGB in/out, BGR order)."""
    f = img.astype(np.float32) / 255.0
    lin = np.where(f <= 0.04045, f / 12.92, ((f + 0.055) / 1.055) ** 2.4)
    lum = (lin * np.array([0.0722, 0.7152, 0.2126], np.float32)).sum(2, keepdims=True)
    out = lum + (lin - lum) * 0.10
    out = out * np.array([1.04, 1.0, 0.97], np.float32)        # BGR: a touch cool
    out = np.clip(out * 1.55, 0, 0.92)                            # metal albedo ~0.5-0.6 linear for the case
    s = np.where(out <= 0.0031308, out * 12.92, 1.055 * np.power(out, 1 / 2.4) - 0.055)
    return np.clip(s * 255 + 0.5, 0, 255).astype(np.uint8)


def main():
    rv = RAW / "_review"; rv.mkdir(exist_ok=True)
    for mid, jobs in JOBS.items():
        for tex, boxes in jobs:
            src = RAW / mid / "gltf_1k" / "textures" / tex
            img = cv2.imread(str(src), cv2.IMREAD_COLOR)
            orig = img.copy()
            for box, mode in boxes:
                x0, y0, x1, y1 = box
                if mode == "flat":
                    g = grain((y1 - y0, x1 - x0), 1.2)
                    img[y0:y1, x0:x1] = np.clip(np.array([255, 128, 128], np.float32)[None, None] + np.concatenate([g * 0, g, g * 0], 2), 0, 255).astype(np.uint8)
                    continue
                if mode.startswith("panel"):
                    sx, sy, sw, sh = map(int, mode.split(":")[1].split(","))
                    col = np.median(img[sy:sy + sh, sx:sx + sw].reshape(-1, 3), axis=0)
                    img[y0:y1, x0:x1] = np.clip(col[None, None, :] + grain((y1 - y0, x1 - x0)), 0, 255).astype(np.uint8)
                    continue
                m = P.stroke_mask(img, box, mode)
                img = cv2.inpaint(img, m, 5, cv2.INPAINT_TELEA)
            if mid == "PH_VINTAGE_LIGHTER" and "diff" in tex:
                img = steel(img)
            out = RAW / mid / "clean" / tex
            out.parent.mkdir(parents=True, exist_ok=True)
            cv2.imwrite(str(out), img, [cv2.IMWRITE_JPEG_QUALITY, 97])
            tiles = []
            for box, _ in boxes:
                x0, y0, x1, y1 = box
                pad = 12
                xa, ya, xb, yb = max(0, x0 - pad), max(0, y0 - pad), min(img.shape[1], x1 + pad), min(img.shape[0], y1 + pad)
                a, b = orig[ya:yb, xa:xb], img[ya:yb, xa:xb]
                s = max(1, int(260 / max(a.shape[:2])))
                a = cv2.resize(a, None, fx=s, fy=s, interpolation=cv2.INTER_NEAREST)
                b = cv2.resize(b, None, fx=s, fy=s, interpolation=cv2.INTER_NEAREST)
                tiles.append(np.concatenate([a, np.zeros((a.shape[0], 6, 3), np.uint8), b], 1))
            W = max(t.shape[1] for t in tiles)
            sheet = np.concatenate([np.pad(t, ((0, 8), (0, W - t.shape[1]), (0, 0))) for t in tiles], 0)
            cv2.imwrite(str(rv / f"PAINTOUT_{mid}_{tex[:-4]}.jpg"), sheet, [cv2.IMWRITE_JPEG_QUALITY, 90])
            print(mid, tex, "boxes", len(boxes))


if __name__ == "__main__":
    main()
