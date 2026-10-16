#!/usr/bin/env python3
"""CONTENT_AUDIT fix for the WP05 -> WP10 request 'FAL_DIVE_WATCH as a case without hands': the Tripo texture has the
hands painted on the dial (10:10), and WP05 adds procedural hands for the MID-A watch check (story clock), which
would show two sets of hands in a close-up. The inner dial (r < 13.5 mm) is repainted procedurally: matte black,
aged-lume dots/batons/12 o'clock triangle, minute track, centre pinion, no hands, no text, no logo. The bezel ring
(r 13.5-21 mm) keeps the generated texture.

  Blender -b --factory-startup -P Tools/audit/blender_uv_probe.py -- <SM_DiveWatch.fbx> RawAssets/_audit/watch/faces.json
  Tools/.venv/bin/python -I Tools/audit/dive_watch_dial.py

Idempotent: the untouched WP10_FAL maps are kept once in RawAssets/wp10/fal/_audit_backup/ and each run starts there.
The dial plane is measured on the mesh (normal ~ (0, 0.23, 0.97) Unity, 12 o'clock towards +Y; the pattern is
mirror-symmetric, so the handedness of the export does not matter).
"""
import json
import math
import os
import shutil

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TEX = os.path.join(ROOT, "RawAssets/wp10/ready/Assets/_Game/Art/Textures/Props/dive_watch")
BACK = os.path.join(ROOT, "RawAssets/wp10/fal/_audit_backup")
FACES = os.path.join(ROOT, "RawAssets/_audit/watch/faces.json")
R_DIAL = 0.0135


def dial_color(a, b):
    """a, b in metres on the dial plane (b towards 12). Returns (rgb, lume mask)."""
    mm_a, mm_b = a * 1000, b * 1000
    r = np.hypot(mm_a, mm_b)
    th = (np.degrees(np.arctan2(mm_a, mm_b)) + 360) % 360   # 0 at 12, clockwise (mirror-symmetric pattern)
    black = np.array([0.022, 0.022, 0.024])
    lume_c = np.array([0.80, 0.74, 0.56])
    silver = np.array([0.62, 0.62, 0.60])
    col = np.tile(black, (len(r), 1)) * (0.9 + 0.2 * (r / 13.5))[:, None]
    lume = np.zeros(len(r))
    rim = np.zeros(len(r))
    # minute track
    track = ((r > 12.55) & (r < 12.75)) | ((r > 11.8) & (r < 12.75) & (np.abs(((th / 6.0) + 0.5) % 1.0 - 0.5) * 6.0 * math.pi / 180 * r < 0.09))
    five = (r > 11.4) & (r < 12.75) & (np.abs(((th / 30.0) + 0.5) % 1.0 - 0.5) * 30.0 * math.pi / 180 * r < 0.16)
    col[track | five] = silver * 0.9
    for h in range(12):
        ang = math.radians(h * 30)
        ux, uy = math.sin(ang), math.cos(ang)
        # marker-local coordinates: along = radial, across = tangential
        along = mm_a * ux + mm_b * uy
        across = mm_a * uy - mm_b * ux
        if h == 0:
            # inverted triangle pointing at the centre: apex at r 8.6, base at r 11.4 (3.0 mm wide)
            t = (along - 8.6) / 2.8
            inside = (t >= 0) & (t <= 1) & (np.abs(across) <= 1.5 * t)
            edge = (t >= -0.05) & (t <= 1.05) & (np.abs(across) <= 1.5 * t + 0.18) & ~inside
        elif h in (3, 6, 9):
            inside = (np.abs(along - 10.0) <= 1.6) & (np.abs(across) <= 0.7)
            edge = (np.abs(along - 10.0) <= 1.78) & (np.abs(across) <= 0.88) & ~inside
        else:
            d = np.hypot(along - 10.3, across)
            inside = d <= 1.0
            edge = (d <= 1.18) & ~inside
        lume[inside] = 1
        rim[edge] = 1
    col[rim > 0] = silver
    col[lume > 0] = lume_c
    pin = r < 0.85
    col[pin] = np.array([0.30, 0.30, 0.31])
    return col, lume, rim + pin


def main():
    os.makedirs(BACK, exist_ok=True)
    for s in ("BC", "M", "N"):
        b = os.path.join(BACK, f"T_DiveWatch_{s}.png")
        if not os.path.exists(b):
            shutil.copy2(os.path.join(TEX, f"T_DiveWatch_{s}.png"), b)
    d = np.array(json.load(open(FACES)), dtype=np.float64)
    c, n = d[:, 0:3], d[:, 3:6]
    uvs = d[:, 7:13].reshape(-1, 3, 2)
    verts = d[:, 13:22].reshape(-1, 3, 3)
    nd = np.array([0.0, 0.23, 0.97])
    nd /= np.linalg.norm(nd)
    s = n @ nd > 0.97
    h = c @ nd
    hmax = h[s].max()
    ring = s & (h > hmax - 0.0008)               # bezel ring (sits ~1.6 mm above the dial): its centroid is the axis
    cen = c[ring].mean(axis=0)
    cen = cen - nd * ((cen - c[s & (h < hmax - 0.0010) & (h > hmax - 0.0025)].mean(axis=0)) @ nd)  # drop onto the dial plane
    e1 = np.array([1.0, 0.0, 0.0])
    e2 = np.cross(nd, e1)
    e2 /= np.linalg.norm(e2)
    sel = []
    for i in np.nonzero(n @ nd > 0.9)[0]:
        rel = c[i] - cen
        if abs(rel @ nd) < 0.0012 and math.hypot(rel @ e1, rel @ e2) < R_DIAL - 0.0002:
            sel.append(i)
    bc = np.asarray(Image.open(os.path.join(BACK, "T_DiveWatch_BC.png")).convert("RGB")).astype(np.float32) / 255.0
    mk = np.asarray(Image.open(os.path.join(BACK, "T_DiveWatch_M.png"))).astype(np.float32) / 255.0
    nm = np.asarray(Image.open(os.path.join(BACK, "T_DiveWatch_N.png")).convert("RGB")).astype(np.float32) / 255.0
    H, W = bc.shape[:2]
    Hm, Wm = mk.shape[:2]
    painted = 0
    for i in sel:
        uv = uvs[i] * np.array([W, H])
        uv[:, 1] = H - uv[:, 1]
        x0, x1 = int(max(0, np.floor(uv[:, 0].min()) - 1)), int(min(W - 1, np.ceil(uv[:, 0].max()) + 1))
        y0, y1 = int(max(0, np.floor(uv[:, 1].min()) - 1)), int(min(H - 1, np.ceil(uv[:, 1].max()) + 1))
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        p = np.stack([xs.ravel(), ys.ravel()], 1)
        A, B_, C = uv
        v0, v1, v2 = B_ - A, C - A, p - A
        den = v0[0] * v1[1] - v1[0] * v0[1]
        if abs(den) < 1e-9:
            continue
        l1 = (v2[:, 0] * v1[1] - v1[0] * v2[:, 1]) / den
        l2 = (v0[0] * v2[:, 1] - v2[:, 0] * v0[1]) / den
        l0 = 1 - l1 - l2
        eps = 2.0 / max(1.0, math.sqrt(abs(den)))    # include edge texels (seam padding, ~2 px)
        ins = (l0 >= -eps) & (l1 >= -eps) & (l2 >= -eps)
        if not ins.any():
            continue
        bary = np.stack([l0, l1, l2], 1)[ins]
        pos = bary @ verts[i]
        rel = pos - cen
        col, lume, rim = dial_color(rel @ e1, rel @ e2)
        px = p[ins].astype(int)
        bc[px[:, 1].clip(0, H - 1) - 0, px[:, 0].clip(0, W - 1)] = col
        mx = (px[:, 0] * Wm // W).clip(0, Wm - 1)
        my = (px[:, 1] * Hm // H).clip(0, Hm - 1)
        mk[my, mx, 0] = rim * 0.9            # silver rims metallic
        mk[my, mx, 1] = 1.0
        if mk.shape[2] == 4:
            mk[my, mx, 3] = 0.78             # under the crystal: glossy
        nm[(px[:, 1] * nm.shape[0] // H).clip(0, nm.shape[0] - 1), (px[:, 0] * nm.shape[1] // W).clip(0, nm.shape[1] - 1)] = (0.5, 0.5, 1.0)
        painted += int(ins.sum())
    Image.fromarray((bc * 255 + 0.5).astype(np.uint8)).save(os.path.join(TEX, "T_DiveWatch_BC.png"))
    Image.fromarray((mk * 255 + 0.5).astype(np.uint8)).save(os.path.join(TEX, "T_DiveWatch_M.png"))
    Image.fromarray((nm * 255 + 0.5).astype(np.uint8)).save(os.path.join(TEX, "T_DiveWatch_N.png"))
    print(f"dial faces {len(sel)}, texels repainted {painted}, centre {np.round(cen, 4)}")


if __name__ == "__main__":
    main()
