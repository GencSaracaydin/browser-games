#!/usr/bin/env python3
"""CONTENT_AUDIT realism fix on WP10_FAL's helicopter texture: Tripo baked bright reflection 'teeth' into the canopy
glass of T_Helicopter_BC (visible head-on in the insertion cinematic). Every canopy glass texel is repainted as clean
dark tinted glass (BC), glossy non-metal (M: R 0, A 0.92) and flat (N).

  Tools/.venv/bin/python -I Tools/audit/heli_windshield.py

Needs RawAssets/_audit/heli_cabin/windshield_polys.json from
  Blender -b --factory-startup -P Tools/audit/blender_heli_glass_mask.py -- <SM_Helicopter.fbx> <T_Helicopter_BC.png> <json>
Idempotent: the untouched WP10_FAL maps are kept once in RawAssets/wp10/fal/_audit_backup/ and every run starts from them.
"""
import json
import os
import shutil

import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TEX = os.path.join(ROOT, "RawAssets/wp10/ready/Assets/_Game/Art/Textures/Vehicles/helicopter")
BACK = os.path.join(ROOT, "RawAssets/wp10/fal/_audit_backup")
POLYS = os.path.join(ROOT, "RawAssets/_audit/heli_cabin/windshield_polys.json")


def main():
    os.makedirs(BACK, exist_ok=True)
    for s in ("BC", "M", "N"):
        b = os.path.join(BACK, f"T_Helicopter_{s}.png")
        if not os.path.exists(b):
            shutil.copy2(os.path.join(TEX, f"T_Helicopter_{s}.png"), b)
    J = json.load(open(POLYS))
    W, H = J["size"]
    mk = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(mk)
    for p in J["polys"]:
        d.polygon([(u * W, (1 - v) * H) for u, v in p], fill=255)
    m = cv2.dilate(np.asarray(mk), np.ones((5, 5), np.uint8)).astype(np.float32) / 255.0
    m = cv2.GaussianBlur(m, (0, 0), 0.8)[..., None]
    bc = np.asarray(Image.open(os.path.join(BACK, "T_Helicopter_BC.png")).convert("RGB")).astype(np.float32) / 255.0
    lum = bc.mean(2, keepdims=True)
    glass = np.array([0.055, 0.07, 0.075], np.float32)[None, None, :] * (0.9 + 0.2 * np.clip(lum, 0, 0.5))
    out = bc * (1 - m) + glass * m
    Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(TEX, "T_Helicopter_BC.png"))
    for s in ("M", "N"):
        src = Image.open(os.path.join(BACK, f"T_Helicopter_{s}.png"))
        a = np.asarray(src).astype(np.float32) / 255.0
        Hm, Wm = a.shape[:2]
        mm = cv2.resize(m[..., 0], (Wm, Hm), interpolation=cv2.INTER_AREA)[..., None]
        if s == "M":
            tgt = a.copy()
            tgt[..., 0] = 0.0
            if a.shape[2] == 4:
                tgt[..., 3] = 0.92
            tgt[..., 1] = 1.0
        else:
            tgt = a.copy()
            tgt[..., 0] = 0.5
            tgt[..., 1] = 0.5
            tgt[..., 2] = 1.0
        res = a * (1 - mm) + tgt * mm
        Image.fromarray((np.clip(res, 0, 1) * 255 + 0.5).astype(np.uint8), mode=src.mode).save(os.path.join(TEX, f"T_Helicopter_{s}.png"))
    print("repainted", len(J["polys"]), "canopy glass faces;", f"{float(m.mean()) * 100:.2f}% of the atlas")


if __name__ == "__main__":
    main()
