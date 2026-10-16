#!/usr/bin/env python3
"""CONTENT_AUDIT guardrail fixes on shipped images (GDD §2.3: no religious imagery, no real insignia).

  Tools/.venv/bin/python -I Tools/audit/guardrail_paintout.py

IMG_HARROU_STILL (WP09, T_HarrouStill_BC.png, 2048x1536): the generated plate shows a bell hanging in the clock
tower's arched opening (reads as a church belfry next to the arcades) and cap badges on six berets (one is a
crown-topped badge that resembles a real regimental badge). Both are painted out: OpenCV Telea inpaint, then film
grain matched to the surrounding residual so the patch does not read as a smooth smear.

Idempotent: the untouched original is kept once as RawAssets/wp09/IMG_HARROU_STILL/pre_audit_T_HarrouStill_BC.png and
every run starts from it. Rerun this after any regeneration of the WP09 plate (Tools/images/wp09_*.py).
VID_HARROU_8MM (512x384) is left as is: the tower is ~30 px tall there and neither detail is legible.
"""
import os
import shutil

import cv2
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TARGET = os.path.join(ROOT, "RawAssets/wp09/ready/Assets/_Game/Art/Textures/Images/T_HarrouStill_BC.png")
BACKUP = os.path.join(ROOT, "RawAssets/wp09/IMG_HARROU_STILL/pre_audit_T_HarrouStill_BC.png")
REVIEW = os.path.join(ROOT, "RawAssets/_audit/guardrail")

# (x, y, radius) in the 2048x1536 plate; rectangles as (x0, y0, x1, y1)
BADGES = [(873, 542, 10), (932, 522, 10), (996, 530, 11), (1088, 516, 11), (1175, 501, 10), (1338, 476, 13)]
BELL = (743, 279, 763, 316)


def main():
    if not os.path.exists(BACKUP):
        os.makedirs(os.path.dirname(BACKUP), exist_ok=True)
        shutil.copy2(TARGET, BACKUP)
    img = cv2.imread(BACKUP, cv2.IMREAD_COLOR)
    mask = np.zeros(img.shape[:2], np.uint8)
    for x, y, r in BADGES:
        cv2.circle(mask, (x, y), r, 255, -1)
    out = cv2.inpaint(img, mask, 6, cv2.INPAINT_TELEA)
    # bell: the arch interior is a dark recess, so fill with the recess colour sampled below the bell (Telea would
    # pull in the lit stone jambs) and feather the edge
    x0, y0, x1, y1 = BELL
    recess = np.median(img[y1 + 4:y1 + 40, x0 + 6:x1 - 6].reshape(-1, 3), axis=0)
    bell = np.zeros(img.shape[:2], np.float32)
    cv2.rectangle(bell, (x0, y0), (x1, y1), 1.0, -1)
    bell = cv2.GaussianBlur(bell, (0, 0), 2.0)[..., None]
    out = (out.astype(np.float32) * (1 - bell) + recess[None, None, :] * bell).astype(np.uint8)
    mask = np.maximum(mask, (bell[..., 0] > 0.05).astype(np.uint8) * 255)
    # grain: residual std around each patch, re-applied inside the patch
    rng = np.random.default_rng(7)
    resid = img.astype(np.float32) - cv2.GaussianBlur(img, (0, 0), 1.5).astype(np.float32)
    ring = cv2.dilate(mask, np.ones((25, 25), np.uint8)) & ~mask
    std = resid[ring > 0].std(axis=0)
    noise = rng.normal(0, 1, img.shape).astype(np.float32) * std[None, None, :]
    noise = cv2.GaussianBlur(noise, (0, 0), 0.6) * 1.3
    soft = cv2.GaussianBlur(mask.astype(np.float32) / 255.0, (0, 0), 1.5)[..., None]
    res = np.clip(out.astype(np.float32) + noise * soft, 0, 255).astype(np.uint8)
    cv2.imwrite(TARGET, res)
    os.makedirs(REVIEW, exist_ok=True)
    crops = []
    for (x0, y0, x1, y1) in ((690, 220, 830, 380), (840, 440, 1420, 580)):
        a = img[y0:y1, x0:x1]
        b = res[y0:y1, x0:x1]
        crops.append(np.concatenate([a, b], 0))
    h = max(c.shape[0] for c in crops)
    sheet = np.concatenate([np.pad(c, ((0, h - c.shape[0]), (0, 8), (0, 0))) for c in crops], 1)
    cv2.imwrite(os.path.join(REVIEW, "HARROU_STILL_before_after.png"), cv2.resize(sheet, None, fx=2, fy=2, interpolation=cv2.INTER_LANCZOS4))
    print("painted out", len(BADGES), "badges + bell ->", os.path.relpath(TARGET, ROOT))


if __name__ == "__main__":
    main()
