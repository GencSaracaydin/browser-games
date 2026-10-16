#!/usr/bin/env python3
"""CONTENT_AUDIT gap fill: offline version of the editor SignBaker for the P0/P1 text-bearing textures that were
waiting for WP08's Sardani glyph font (now in RawAssets/wp08/ready/Assets/_Game/Art/Fonts/Sardani-Regular.ttf).
No fal calls: pure image generation (numpy + Pillow + OpenCV), deterministic.

  Tools/.venv/bin/python -I Tools/audit/sign_bake.py [--only signs,marquee,posters,clocks,filmcans,exit]

Ids (owner WP10; written into WP10's ready tree with their own index/spec files so WP10's tools never overwrite them):
  DCL_SARDANI_SIGNS      shop signs, street plates, curfew notices (Sardani glyphs + Latin numerals) -> decal atlas
  DCL_MARQUEE_TITLES     Cinema Zenith marquee title panels (changeable letters) + the blade sign -> decal atlas
  DCL_POSTERS_COMPOSED   DCL_POSTER_ART plates + Sardani titles, fresh and sun-faded/torn -> decal atlas (P1)
  PROC_CLOCK_TOWER_FACES tower dial (Roman numerals, no hands: the hands are kit geometry at 6:40), world-time wall
                         dial and the 12 city name plates (invented Sardani names)
  PROC_FILM_CAN_LABELS   masking-tape can labels "S-3 / SESSION 11 / 03-84" (+3 more sessions), typewriter
  PROC_EXIT_SIGN         cinema EXIT box face (Latin, allowed by the manifest) with a Sardani subline, BC + emissive

Guardrails (GDD §2.3): only Sardani glyphs (invented, O3-reviewed) and Latin letters/numerals; no flags, no symbols,
no religious imagery. Latin key strings below are invented words; the glyphs are what is seen.

Outputs: RawAssets/wp10/ready/Assets/_Game/Art/Textures/{Decals,Props}/<sub>/T_*_{BC,N,M,E}.png + *_layout.json,
rows in RawAssets/wp10/ready/import_index_audit.json, material specs in RawAssets/wp10/ready/materials_audit.json,
review sheets in RawAssets/_audit/signs/.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "Tools", "tex"))
import wp10_texlib as T  # noqa: E402

READY = os.path.join(ROOT, "RawAssets", "wp10", "ready")
REVIEW = os.path.join(ROOT, "RawAssets", "_audit", "signs")
INDEX = os.path.join(READY, "import_index_audit.json")
SPECS = os.path.join(READY, "materials_audit.json")
FONTS = os.path.join(ROOT, "RawAssets", "wp08", "ready", "Assets", "_Game", "Art", "Fonts")
F_SARDANI = os.path.join(FONTS, "Sardani-Regular.ttf")
F_COND = os.path.join(FONTS, "BarlowCondensed-SemiBold.ttf")
F_TYPE = os.path.join(FONTS, "SpecialElite-Regular.ttf")
F_DECO = os.path.join(ROOT, "RawAssets", "wp10", "cc0", "_fonts", "Limelight-Regular.ttf")
F_SERIF = os.path.join(ROOT, "RawAssets", "wp10", "cc0", "_fonts", "Federo-Regular.ttf")
POSTER_ART = os.path.join(READY, "Assets/_Game/Art/Textures/Decals/poster_art/T_Decal_PosterArt_BC.png")
PLASTER = os.path.join(READY, "Assets/_Game/Art/Textures/Env/painted_plaster_wall/T_PaintedPlasterWall_BC.png")

DEC = "Assets/_Game/Art/Textures/Decals"
PROPS = "Assets/_Game/Art/Textures/Props"
TOOL = "audit_sign_bake"

BC_NOTE = ("Decal base map: Texture Type Default, sRGB ON, Alpha Is Transparency ON, Max Size 1024, DXT5 Crunched q60, "
           "mips on, aniso 2, wrap Clamp. URP Decal Projector (Shader Graphs/Decal), alpha = opacity.")
N_NOTE = "Normal map, OpenGL (+Y), sRGB OFF, Max Size 1024, DXT5nm Crunched q60, wrap Clamp. Decal Normal Blend 0.5."
M_NOTE = ("Mask, sRGB OFF, Max Size 1024, DXT5 Crunched q60, wrap Clamp: R metallic, G AO, B 0, A smoothness "
          "(custom MAOS decal graph only; the default URP decal graph ignores it).")


# ----------------------------------------------------------------------------------------------------- helpers


def fbm(shape, seed, base_px, octaves=5):
    h, w = shape
    rng = np.random.default_rng(seed)
    acc = np.zeros(shape, np.float32)
    amp, tot, px = 1.0, 0.0, float(base_px)
    for _ in range(octaves):
        gh, gw = max(2, int(h / px) + 2), max(2, int(w / px) + 2)
        g = rng.random((gh, gw)).astype(np.float32)
        up = cv2.resize(g, (int(gw * px), int(gh * px)), interpolation=cv2.INTER_CUBIC)[:h, :w]
        acc += amp * up
        tot += amp
        amp *= 0.55
        px = max(px / 2.0, 1.0)
    acc /= tot
    return (acc - acc.min()) / max(acc.max() - acc.min(), 1e-6)


def srgb(hexstr):
    h = hexstr.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


def lin(hexstr):
    return T.srgb_to_lin(srgb(hexstr))


def text_mask(text, font, W, H, fill_h=0.62, margin=0.06, ss=4, tracking=0.0, align="center", cap_ref="H",
              y_bias=0.0, max_w=None):
    """Render one line fitted into W x H: cap height = fill_h * H unless the width limits it. Returns float HxW."""
    big = Image.new("L", (W * ss, H * ss), 0)
    d = ImageDraw.Draw(big)
    avail_w = (max_w or W) * (1 - 2 * margin) * ss
    size = int(H * ss * 1.4)
    while size > 6:
        f = ImageFont.truetype(font, size)
        cb = d.textbbox((0, 0), cap_ref, font=f)
        cap = cb[3] - cb[1]
        widths = [d.textbbox((0, 0), ch, font=f)[2] - d.textbbox((0, 0), ch, font=f)[0] if ch != " " else size * 0.3 for ch in text]
        adv = [f.getlength(ch) for ch in text]
        total = sum(adv) + tracking * size * (len(text) - 1)
        if cap <= fill_h * H * ss and total <= avail_w:
            break
        size -= 2
    f = ImageFont.truetype(font, size)
    cb = d.textbbox((0, 0), cap_ref, font=f)
    cap = cb[3] - cb[1]
    adv = [f.getlength(ch) for ch in text]
    total = sum(adv) + tracking * size * (len(text) - 1)
    if align == "center":
        x = (W * ss - total) / 2
    elif align == "left":
        x = margin * W * ss
    else:
        x = W * ss - margin * W * ss - total
    y = (H * ss - cap) / 2 - cb[1] + y_bias * H * ss
    for ch, a in zip(text, adv):
        d.text((x, y), ch, font=f, fill=255)
        x += a + tracking * size
    m = np.asarray(big.resize((W, H), Image.LANCZOS)).astype(np.float32) / 255.0
    return m


def glyph_tiles(text, font, W, H, seed, allow_missing=True, ss=4, tracking=0.32):
    """Changeable marquee letters: each glyph placed separately with its natural advance plus tracking, slightly
    misaligned and tilted (one tile occasionally crooked, one title slot empty). Returns (letters mask, tile mask)."""
    rng = np.random.default_rng(seed)
    probe = ImageFont.truetype(font, 100)
    cb = probe.getbbox("H")
    cap100 = cb[3] - cb[1]
    size = 0.70 * H / cap100 * 100
    adv = [probe.getlength(ch) * size / 100 for ch in text]
    total = sum(adv) + tracking * size * (len(text) - 1)
    if total > W * 0.94:
        size *= W * 0.94 / total
        adv = [probe.getlength(ch) * size / 100 for ch in text]
        total = sum(adv) + tracking * size * (len(text) - 1)
    letters = np.zeros((H, W), np.float32)
    tiles = np.zeros((H, W), np.float32)
    idx = [i for i, ch in enumerate(text) if ch != " "]
    missing = idx[rng.integers(0, len(idx))] if allow_missing and len(idx) > 4 and rng.random() < 0.8 else -1
    x = (W - total) / 2
    for i, ch in enumerate(text):
        a = adv[i]
        if ch != " " and i != missing:
            cw = int(a + tracking * size * 0.9)
            chh = int(H * 0.92)
            m = text_mask(ch, font, cw, chh, fill_h=0.70 * H / chh, margin=0.0, ss=ss)
            ang = rng.normal(0, 1.2) + (rng.choice([-5, 5]) if rng.random() < 0.07 else 0)
            dy = rng.normal(0, 0.012) * H
            M = cv2.getRotationMatrix2D((cw / 2, chh / 2), ang, 1.0)
            m = cv2.warpAffine(m, M, (cw, chh))
            tile = cv2.warpAffine(np.ones((chh, cw), np.float32), M, (cw, chh))
            px = int(x - tracking * size * 0.45)
            py = int((H - chh) / 2 + dy)
            y0, y1 = max(py, 0), min(py + chh, H)
            x0, x1 = max(px, 0), min(px + cw, W)
            letters[y0:y1, x0:x1] = np.maximum(letters[y0:y1, x0:x1], m[y0 - py:y1 - py, x0 - px:x1 - px])
            tiles[y0:y1, x0:x1] = np.maximum(tiles[y0:y1, x0:x1], tile[y0 - py:y1 - py, x0 - px:x1 - px])
        x += a + tracking * size
    return letters, tiles


def flakes(shape, seed, amount=0.25, scale=40):
    big = fbm(shape, seed, scale, 5)
    chips = fbm(shape, seed + 7, max(4, scale // 5), 4)
    thr = 1.0 - amount
    lost = np.clip((big - thr) * 30.0, 0, 1)
    lost = np.maximum(lost, np.clip((chips - (thr + 0.12)) * 30.0, 0, 1))
    return cv2.GaussianBlur(lost, (0, 0), 0.6)


def streaks(shape, seed, strength=0.35):
    """Vertical rain/grime runs (darker towards the bottom)."""
    h, w = shape
    rng = np.random.default_rng(seed)
    col = cv2.resize(rng.random((1, max(2, w // 6))).astype(np.float32), (w, 1), interpolation=cv2.INTER_CUBIC)
    col = np.clip((col - 0.68) * 3.5, 0, 1)
    col = cv2.GaussianBlur(col, (0, 0), 2.0)
    # runs start at the top edge and fade out at a random length per column
    length = 0.3 + 0.7 * cv2.resize(rng.random((1, max(2, w // 10))).astype(np.float32), (w, 1), interpolation=cv2.INTER_CUBIC)
    yy = np.linspace(0, 1, h, dtype=np.float32)[:, None]
    run = np.clip(1.0 - yy / np.maximum(length, 0.05), 0, 1) ** 0.7
    s = col * run * (0.6 + 0.4 * fbm(shape, seed + 3, 30, 3))
    base = 0.25 * fbm(shape, seed + 4, 90, 3) * (0.4 + 0.6 * yy)  # general dirt, heavier low down
    return np.clip((s + base) * strength, 0, 1)


def edge_dist(alpha):
    """Distance (px) to the shape outline; the image border counts as outside."""
    a = np.pad((alpha > 0.5).astype(np.uint8), 1)
    return cv2.distanceTransform(a, cv2.DIST_L2, 3)[1:-1, 1:-1]


def chips_near_edges(alpha, seed, width_px=10, amount=0.5):
    d = edge_dist(alpha)
    near = np.clip(1.0 - d / width_px, 0, 1)
    n = fbm(alpha.shape, seed, 6, 4)
    return np.clip((n * near - (1 - amount) * 0.6) * 6.0, 0, 1) * (alpha > 0.5)


def height_to_normal(hgt, strength):
    gy, gx = np.gradient(hgt * strength)
    n = np.stack([-gx, gy, np.ones_like(hgt)], 2)
    return T.encode_normal(n)


class Atlas:
    """Linear-colour atlas with alpha, height, smoothness, metal, AO and emission planes."""

    def __init__(self, W, H):
        self.W, self.H = W, H
        self.col = np.zeros((H, W, 3), np.float32)
        self.a = np.zeros((H, W), np.float32)
        self.h = np.zeros((H, W), np.float32)
        self.s = np.full((H, W), 0.15, np.float32)
        self.m = np.zeros((H, W), np.float32)
        self.ao = np.ones((H, W), np.float32)
        self.e = np.zeros((H, W, 3), np.float32)
        self.rects = []

    def put(self, name, x, y, el, meta):
        h, w = el["a"].shape
        sl = (slice(y, y + h), slice(x, x + w))
        self.col[sl] = el["col"]
        self.a[sl] = el["a"]
        self.h[sl] = el.get("h", 0)
        self.s[sl] = el.get("s", 0.15)
        self.m[sl] = el.get("m", 0)
        self.ao[sl] = el.get("ao", 1)
        if "e" in el:
            self.e[sl] = el["e"]
        # UV rect, origin bottom-left (Unity)
        meta = dict(meta)
        meta.update({"name": name, "px": [x, y, w, h],
                     "uv": [round(x / self.W, 5), round(1 - (y + h) / self.H, 5), round(w / self.W, 5), round(h / self.H, 5)]})
        self.rects.append(meta)

    def bc(self):
        return np.concatenate([T.lin_to_srgb(np.clip(self.col, 0, 1)), np.clip(self.a, 0, 1)[..., None]], 2)

    def normal(self, strength=2.0):
        return height_to_normal(cv2.GaussianBlur(self.h, (0, 0), 0.7), strength)

    def mask(self):
        return T.pack_mask(self.m, self.ao, np.clip(self.s, 0, 1))


ROWS, SPEC_ROWS, IDS = [], [], set()


def out_path(rel):
    p = os.path.join(READY, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    return p


def emit(mid, rel, arr, typ, note):
    T.save8(arr, out_path(rel))
    ROWS.append({"manifest_id": mid, "file": rel, "target_path": rel, "type": typ, "import_notes": note, "tool": TOOL})
    IDS.add(mid)


def emit_json(mid, rel, data, note):
    with open(out_path(rel), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    ROWS.append({"manifest_id": mid, "file": rel, "target_path": rel, "type": "data_json", "import_notes": note, "tool": TOOL})


def preview(atlas_bc, name, tile_px=128, ground=PLASTER):
    """Composite over the shipped plaster (2 m tile = tile_px) for review."""
    g = T.load(ground, "RGB")
    H, W = atlas_bc.shape[:2]
    g = T.resize_srgb(g, (tile_px, tile_px))
    g = np.tile(g, (int(np.ceil(H / tile_px)), int(np.ceil(W / tile_px)), 1))[:H, :W]
    a = atlas_bc[..., 3:4]
    comp = T.srgb_to_lin(g) * (1 - a) + T.srgb_to_lin(atlas_bc[..., :3]) * a
    os.makedirs(REVIEW, exist_ok=True)
    T.save8(T.lin_to_srgb(comp), os.path.join(REVIEW, name))


# ------------------------------------------------------------------------------------------- element painters


def painted_board(W, H, seed, bg, fg, lines, fonts, sizes, frame=True, wood=True, fade_amt=0.45, flake_amt=0.22, streak=0.45):
    """Hand-painted shop fascia on a wooden board: faded paint, flaking to bare grey wood, grime runs."""
    shape = (H, W)
    yy = np.linspace(0, 1, H, dtype=np.float32)[:, None]
    grain = fbm((H * 6, W), seed, 4, 4)  # squashed vertically -> long horizontal fibres
    grain = cv2.resize(grain, (W, H), interpolation=cv2.INTER_AREA)
    grain = (grain - grain.min()) / max(grain.max() - grain.min(), 1e-6)
    planks = (np.sin(yy * np.pi * 2 * 3.0 + 0.3) > 0.97).astype(np.float32)
    woodc = lin("#6b5a48") * (0.75 + 0.45 * grain)[..., None]
    woodc *= (1 - 0.5 * planks)[..., None]
    fade = np.clip(0.35 + 0.45 * fbm(shape, seed + 1, 160, 3) + 0.2 * (1 - yy), 0, 1)
    bgc = lin(bg) * (1 - fade_amt * fade[..., None]) + lin("#cfc4b0") * fade_amt * fade[..., None]
    bgc *= (0.88 + 0.22 * fbm(shape, seed + 2, 5, 4))[..., None]
    text = np.zeros(shape, np.float32)
    n = len(lines)
    y = 0
    for i, (ln, fnt, sz) in enumerate(zip(lines, fonts, sizes)):
        hh = int(H * sz)
        m = text_mask(ln, fnt, W, hh, fill_h=0.66, margin=0.07)
        text[y:y + hh] = np.maximum(text[y:y + hh], m)
        y += hh
    wob = fbm(shape, seed + 3, 6, 3) - 0.5
    text = np.clip((text - 0.5 + wob * 0.3) * 3.0 + 0.5, 0, 1)
    fgc = lin(fg) * (1 - 0.3 * fade[..., None]) + lin("#d8d0bc") * 0.3 * fade[..., None]
    paint = bgc * (1 - text[..., None]) + fgc * text[..., None]
    lost = flakes(shape, seed + 4, amount=flake_amt, scale=36) if flake_amt > 0 else np.zeros(shape, np.float32)
    if frame:
        f = np.zeros(shape, np.float32)
        b = max(3, int(H * 0.05))
        f[:b, :] = f[-b:, :] = 1
        f[:, :b] = f[:, -b:] = 1
        paint = paint * (1 - f[..., None]) + lin("#2a2420")[None, None, :] * f[..., None] * (0.8 + 0.4 * grain[..., None])
    col = paint * (1 - lost[..., None]) + (woodc if wood else lin("#9a8f80")[None, None, :] * np.ones((H, W, 1))) * lost[..., None]
    g = streaks(shape, seed + 5, streak)
    col *= (1 - g[..., None] * 0.6)
    col *= (1 - 0.35 * np.clip(1 - edge_dist(np.ones(shape)) / max(3.0, H * 0.04), 0, 1))[..., None]  # dirty board edges
    hgt = 0.6 * (1 - lost) + 0.15 * text * (1 - lost) + 0.2 * grain * lost
    sm = 0.22 * (1 - lost) + 0.08 * lost
    return {"col": col, "a": np.ones(shape, np.float32), "h": hgt, "s": sm, "ao": 1 - 0.3 * g}


def wall_lettering(W, H, seed, color, lines, fonts, sizes):
    """Sign-writer's paint straight on plaster: alpha only on the letters, flaked, sun-faded."""
    shape = (H, W)
    text = np.zeros(shape, np.float32)
    y = 0
    for ln, fnt, sz in zip(lines, fonts, sizes):
        hh = int(H * sz)
        m = text_mask(ln, fnt, W, hh, fill_h=0.7, margin=0.05)
        text[y:y + hh] = np.maximum(text[y:y + hh], m)
        y += hh
    # painted rule lines above and below (sign-writer's border)
    rule = np.zeros(shape, np.float32)
    t = max(2, H // 48)
    rule[int(H * 0.04):int(H * 0.04) + t, int(W * 0.04):int(W * 0.96)] = 1
    rule[int(H * 0.96) - t:int(H * 0.96), int(W * 0.04):int(W * 0.96)] = 1
    wob = fbm(shape, seed, 5, 3) - 0.5
    paint = np.clip((np.maximum(text, rule) - 0.5 + wob * 0.35) * 3.0 + 0.5, 0, 1)
    lost = flakes(shape, seed + 1, 0.32, 44)
    yy = np.linspace(0, 1, H, dtype=np.float32)[:, None]
    fade = np.clip(0.3 + 0.55 * fbm(shape, seed + 2, 120, 3) + 0.15 * (1 - yy), 0, 1)
    c = lin(color)[None, None, :] * (1 - fade[..., None] * 0.55) + lin("#b8a690")[None, None, :] * fade[..., None] * 0.55
    c = c * (0.85 + 0.3 * fbm(shape, seed + 3, 5, 4))[..., None]
    alpha = np.clip(paint * (1 - lost) * 0.92 + paint * lost * 0.2, 0, 1)
    return {"col": c, "a": alpha, "h": paint * (1 - lost), "s": 0.25 * paint + 0.1, "ao": np.ones(shape)}


def enamel_plate(W, H, seed, bg, fg, lines, fonts, sizes, border=True, rust=0.5):
    """Vitreous enamel sheet-metal plate: glossy, chipped to black steel with rust halos, rounded corners."""
    shape = (H, W)
    r = int(min(W, H) * 0.12)
    base = np.zeros(shape, np.uint8)
    cv2.rectangle(base, (r, 0), (W - 1 - r, H - 1), 255, -1)
    cv2.rectangle(base, (0, r), (W - 1, H - 1 - r), 255, -1)
    for cx, cy in ((r, r), (W - 1 - r, r), (r, H - 1 - r), (W - 1 - r, H - 1 - r)):
        cv2.circle(base, (cx, cy), r, 255, -1)
    alpha = cv2.GaussianBlur(base.astype(np.float32) / 255.0, (0, 0), 0.8)
    text = np.zeros(shape, np.float32)
    y = 0
    for ln, fnt, sz in zip(lines, fonts, sizes):
        hh = int(H * sz)
        m = text_mask(ln, fnt, W, hh, fill_h=0.62, margin=0.08)
        text[y:y + hh] = np.maximum(text[y:y + hh], m)
        y += hh
    if border:
        d = edge_dist(alpha)
        b = ((d > H * 0.06) & (d < H * 0.06 + max(2, H * 0.035))).astype(np.float32)
        text = np.maximum(text, cv2.GaussianBlur(b, (0, 0), 0.6))
    fade = fbm(shape, seed, 90, 3)
    bgc = lin(bg)[None, None, :] * (0.85 + 0.2 * fade[..., None])
    col = bgc * (1 - text[..., None]) + lin(fg)[None, None, :] * text[..., None]
    ch = np.maximum(chips_near_edges(alpha, seed + 1, width_px=max(6, H * 0.12), amount=0.55),
                    np.clip((fbm(shape, seed + 2, 7, 4) - 0.86) * 20, 0, 1))
    halo = np.clip(cv2.GaussianBlur(ch, (0, 0), 3.0) * 2.5 - ch, 0, 1) * rust
    steel = lin("#1c1a18") + lin("#5a2e14") * fbm(shape, seed + 3, 4, 3)[..., None]
    col = col * (1 - ch[..., None]) + steel * ch[..., None]
    col = col * (1 - halo[..., None]) + lin("#6b3a1c")[None, None, :] * halo[..., None]
    g = streaks(shape, seed + 4, 0.35)
    col *= (1 - 0.5 * g[..., None])
    sm = 0.72 * (1 - ch) * (1 - 0.6 * g) + 0.25 * ch
    return {"col": col, "a": alpha, "h": 0.5 + 0.2 * (1 - ch), "s": sm, "m": 0.6 * ch, "ao": np.ones(shape)}


def paper(W, H, seed, tone="#e9e1cc", torn=0.3, fade=0.3):
    """Pasted printed paper: tone, glue wrinkles, torn edges (alpha), sun fade."""
    shape = (H, W)
    rng = np.random.default_rng(seed)
    base = np.ones(shape, np.float32)
    # torn ragged border
    d = edge_dist(base)
    n = fbm(shape, seed, 10, 4)
    ragged = (d > 2 + 10 * n * (0.4 + torn)).astype(np.float32)
    # torn-away chunks (a corner and maybe a lower piece)
    tear = fbm(shape, seed + 1, 140, 3)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    edge_noise = 0.22 * (fbm(shape, seed + 6, 18, 5) - 0.5) + 0.12 * (tear - 0.5)
    corner = ((xx / W + yy / H + edge_noise) > 1.82 - torn * 0.5).astype(np.float32)
    alpha = ragged * (1 - corner)
    if torn > 0.5:
        lower = (yy / H > 0.62 + 0.18 * fbm(shape, seed + 2, 60, 3)).astype(np.float32)
        alpha *= 1 - lower * (xx / W > 0.15)
    alpha = cv2.GaussianBlur(alpha, (0, 0), 0.7)
    wr = fbm(shape, seed + 3, 24, 4)
    wrinkles = 1.0 - np.abs(wr * 2 - 1)
    wrinkles = np.clip((wrinkles - 0.8) * 5, 0, 1)
    col = lin(tone)[None, None, :] * (0.92 + 0.12 * fbm(shape, seed + 4, 3, 3))[..., None]
    col *= (1 - 0.12 * wrinkles)[..., None]
    stain = np.clip((fbm(shape, seed + 5, 80, 3) - 0.6) * 2.5, 0, 1) * fade
    col = col * (1 - stain[..., None] * 0.35) + lin("#b49a6a")[None, None, :] * stain[..., None] * 0.35
    return {"col": col, "a": alpha, "h": 0.4 + 0.2 * wrinkles, "s": np.full(shape, 0.1, np.float32), "ao": 1 - 0.15 * wrinkles,
            "wrinkle": wrinkles}


def ink_on(el, mask, color, seed, misreg=(1.5, 0.8), mottle=0.25):
    """Print ink (letterpress/offset): slight misregistration, mottled coverage, multiplied over the paper."""
    H, W = mask.shape
    M = np.float32([[1, 0, misreg[0]], [0, 1, misreg[1]]])
    m = cv2.warpAffine(mask, M, (W, H))
    cov = np.clip(m * (1 - mottle * fbm(mask.shape, seed, 3, 3)), 0, 1)
    ink = lin(color)[None, None, :]
    el["col"] = el["col"] * (1 - cov[..., None]) + el["col"] * ink * cov[..., None] * 1.0 + ink * cov[..., None] * 0.0
    el["col"] = el["col"] * (1 - cov[..., None] * 0.15)
    el["h"] = el["h"] + 0.05 * cov
    return el


def sun_fade_blue(el, amount):
    """Old posters lose cyan/blue ink first: shift towards warm and wash out."""
    c = el["col"]
    gray = c.mean(axis=2, keepdims=True)
    warm = np.stack([gray[..., 0] * 1.08, gray[..., 0] * 1.0, gray[..., 0] * 0.82], 2)
    el["col"] = c * (1 - amount) + (warm * 0.8 + 0.18) * amount
    return el


# ------------------------------------------------------------------------------------------------- jobs


def job_signs():
    mid = "DCL_SARDANI_SIGNS"
    A = Atlas(1024, 1024)
    # 1 bakery fascia board (4.2 x 0.8 m)
    el = painted_board(1024, 192, 101, "#2f5a52", "#ece0c2", ["TALVEK ORRAN", "VESKA DUL"], [F_SARDANI, F_SARDANI], [0.66, 0.34])
    A.put("board_teal", 0, 0, el, {"kind": "fascia board", "size_m": [4.2, 0.79], "text_key": "TALVEK ORRAN / VESKA DUL",
                                   "use": "bakery or general store over a shopfront (BLD-02/04 market fronts); opaque, covers a blank SignBoard"})
    # 2 sign-writer lettering on plaster (5 x 0.95 m)
    el = wall_lettering(1024, 192, 202, "#5a1f17", ["VESSAN DROUK", "17 . ULMAR VESK"], [F_SARDANI, F_SARDANI], [0.64, 0.36])
    A.put("wall_oxblood", 0, 192, el, {"kind": "paint on plaster", "size_m": [5.0, 0.94], "text_key": "VESSAN DROUK / 17 ULMAR VESK",
                                       "use": "tailor/cloth merchant: straight on a plastered facade above the shutter (alpha = paint)"})
    # 3 enamel cafe sign (1.6 x 0.6 m)
    el = enamel_plate(512, 192, 303, "#e9e4d6", "#1d3a6b", ["ULMA KESSO", "ORRAN"], [F_SARDANI, F_SARDANI], [0.62, 0.38])
    A.put("enamel_cafe", 0, 384, el, {"kind": "enamel plate", "size_m": [1.6, 0.6], "text_key": "ULMA KESSO / ORRAN",
                                      "use": "cafe (BLD-04) beside the door, screwed to the wall at 2.2 m"})
    # 4 painted tin sign (radio repair) with Latin phone number
    el = painted_board(512, 192, 404, "#c99a2e", "#191714", ["ORRAD VEKTO", "TEL 4-217"], [F_SARDANI, F_COND], [0.6, 0.4],
                       frame=True, wood=False)
    A.put("tin_radio", 512, 384, el, {"kind": "painted tin", "size_m": [1.6, 0.6], "text_key": "ORRAD VEKTO / TEL 4-217",
                                      "use": "radio repair stall, Long Market; Latin numerals allowed (GDD §2.3-3)"})
    # 5/6 curfew notices (0.56 x 0.75 m printed paper, pasted)
    for i, (x, seed, torn, tone) in enumerate(((0, 505, 0.25, "#ebe3cc"), (336, 606, 0.75, "#ddd2b6"))):
        W, H = 336, 448
        p = paper(W, H, seed, tone=tone, torn=torn, fade=0.3 + 0.3 * i)
        head = np.zeros((H, W), np.float32)
        head[18:98] = text_mask("VESKRI KAEL", F_SARDANI, W, 80, fill_h=0.7, margin=0.08)
        head[100:118] = 0
        rule = np.zeros((H, W), np.float32)
        rule[104:108, 24:W - 24] = 1
        big = np.zeros((H, W), np.float32)
        big[120:250] = text_mask("17:30", F_COND, W, 130, fill_h=0.8, margin=0.1, cap_ref="1")
        body = np.zeros((H, W), np.float32)
        lines = ["MORRHAN UVAN TALVISA", "ILO ZAROUDAN VESKRI", "KAEL ORRAN DUL TAVEN", "VESKA ULMAR DROUK OR", "TALVEK"]
        for j, ln in enumerate(lines):
            y = 262 + j * 26
            body[y:y + 24] = text_mask(ln, F_SARDANI, W, 24, fill_h=0.7, margin=0.09, align="left" if j < 4 else "left")
        p = ink_on(p, np.clip(head + rule + big + body, 0, 1), "#1a1714", seed + 1)
        # red rubber stamp (rectangle + Sardani + Latin date), rotated
        st = np.zeros((120, 200), np.float32)
        cv2.rectangle(st, (4, 4), (195, 115), 1.0, 5)
        st[20:70] = np.maximum(st[20:70], text_mask("HARROU", F_SARDANI, 200, 50, fill_h=0.7, margin=0.1))
        st[72:104] = np.maximum(st[72:104], text_mask("12.10.84", F_COND, 200, 32, fill_h=0.7, margin=0.15, cap_ref="1"))
        Mr = cv2.getRotationMatrix2D((100, 60), -9 + 6 * i, 1.0)
        st = cv2.warpAffine(st, Mr, (200, 120))
        st *= np.clip(1.0 - 0.7 * fbm(st.shape, seed + 2, 4, 3), 0, 1)
        stamp = np.zeros((H, W), np.float32)
        stamp[H - 145:H - 25, W - 215:W - 15] = st
        p["col"] = p["col"] * (1 - stamp[..., None] * 0.85) + lin("#a3241c")[None, None, :] * stamp[..., None] * 0.85 * p["col"].mean(2, keepdims=True) * 1.6
        p = sun_fade_blue(p, 0.15 + 0.25 * i)
        A.put(f"curfew_{'ab'[i]}", x, 576, p, {"kind": "printed notice", "size_m": [0.56, 0.75],
                                               "text_key": "VESKRI KAEL / 17:30 / body / stamp HARROU 12.10.84",
                                               "use": "curfew notice pasted on walls, doors, the PA horn posts (Beat 1-4); b = older, half torn"})
    # 7 street plates (0.5 x 0.16 m) x4
    names = [("TALVISA", "2"), ("ULMAR VESK", "7"), ("MORRHAN", "11"), ("KESSO DRAN", "4")]
    for j, (nm, num) in enumerate(names):
        el = enamel_plate(352, 112, 707 + j, "#1f3f78", "#f0ece0", [nm, num], [F_SARDANI, F_COND], [0.62, 0.38], rust=0.7)
        A.put(f"street_{j}", 672, 576 + j * 112, el, {"kind": "enamel street plate", "size_m": [0.5, 0.16], "text_key": f"{nm} {num}",
                                                       "use": "street name plate at 2.6-3 m on corners; Latin house/street number"})
    write_atlas(mid, A, f"{DEC}/sardani_signs", "T_Decal_SardaniSigns",
                "Sardani shop signs, street plates and curfew notices", projector_note="one Decal Projector per rect (UV offset/scale = rect uv); "
                "projector size = size_m, depth 0.3 m; Decal Layer = facades only (never characters or props).")
    return mid


def job_marquee():
    mid = "DCL_MARQUEE_TITLES"
    A = Atlas(1024, 1024)
    titles = [("ORRAN TAVEL", "17.00  19.30"), ("MIRRA KESSEL", "21.00"), ("VESKO DRAN UL", "18.15  20.45")]
    for i, (t, times) in enumerate(titles):
        W, H = 1024, 200
        shape = (H, W)
        # opal backing panel (cream), black letter rails, grime; letters as tiles
        col = lin("#e6dfcc")[None, None, :] * (0.86 + 0.18 * fbm(shape, 900 + i, 60, 3))[..., None]
        rails = np.zeros(shape, np.float32)
        for y in (6, 118, 194):
            rails[y - 3:y + 3] = 1
        l1, t1 = glyph_tiles(t, F_SARDANI, W, 112, 910 + i, tracking=0.1)
        l2, t2 = glyph_tiles(times, F_COND, int(W * 0.6), 74, 920 + i, allow_missing=False, tracking=0.12)
        letters = np.zeros(shape, np.float32)
        tiles = np.zeros(shape, np.float32)
        letters[6:118] = l1
        tiles[6:118] = t1
        x2 = (W - int(W * 0.6)) // 2
        letters[120:194, x2:x2 + int(W * 0.6)] = l2
        tiles[120:194, x2:x2 + int(W * 0.6)] = t2
        g = streaks(shape, 930 + i, 0.5)
        dirt = fbm(shape, 940 + i, 30, 4)
        col = col * (1 - (0.25 * g + 0.12 * dirt)[..., None])
        col = col * (1 - tiles[..., None] * 0.06)
        ink = lin("#121110")[None, None, :]
        col = col * (1 - letters[..., None]) + ink * letters[..., None]
        col = col * (1 - rails[..., None]) + lin("#1b1a19")[None, None, :] * rails[..., None]
        hgt = 0.3 + 0.4 * tiles + 0.2 * letters + 0.5 * rails
        sm = 0.35 - 0.2 * g + 0.15 * letters
        A.put(f"title_{i}", 0, i * 200, {"col": col, "a": np.ones(shape, np.float32), "h": hgt, "s": sm, "ao": 1 - 0.3 * g},
              {"kind": "marquee title panel", "size_m": [4.8, 0.94], "text_key": f"{t} / {times}",
               "use": "Cinema Zenith marquee front letter board (Marquee.Titles anchor, 26 m x 0.95 m): place panels side by side, "
                      "e.g. title_0 | name | title_1 | title_2 (a mid gap is fine); changeable letters, one slot empty"})
    # cinema name panel in Latin Art Deco + Sardani subline
    el = painted_board(1024, 200, 960, "#efe7d2", "#8a1d17", ["CINEMA ZENITH", "ZENITH"], [F_DECO, F_SARDANI], [0.66, 0.34],
                       frame=True, wood=False)
    A.put("name_panel", 0, 600, el, {"kind": "painted name panel", "size_m": [4.8, 0.94], "text_key": "CINEMA ZENITH / Sardani ZENITH",
                                     "use": "centre of the marquee board (Latin letters allowed, GDD §2.2)"})
    # blade sign letters (vertical blade, projector rotated 90 deg): cream enamel with red Deco letters + bulb sockets
    W, H = 1024, 224
    el = enamel_plate(W, H, 970, "#ece4cf", "#9b2a1e", ["ZENITH"], [F_DECO], [1.0], border=True, rust=0.8)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    bulbs = np.zeros((H, W), np.float32)
    for x in np.arange(40, W - 30, 46):
        for y in (20, H - 20):
            bulbs = np.maximum(bulbs, np.clip(1 - np.hypot(xx - x, yy - y) / 7.0, 0, 1))
    el["col"] = el["col"] * (1 - bulbs[..., None]) + lin("#3a3630")[None, None, :] * bulbs[..., None]
    el["h"] = el["h"] + 0.5 * bulbs
    A.put("blade", 0, 800, el, {"kind": "blade sign face", "size_m": [7.0, 1.53], "text_key": "ZENITH",
                                "use": "vertical blade sign (Sign.Blade anchor): projector rotated 90 deg so the letters read top-down; "
                                       "unlit bulb sockets along both edges"})
    write_atlas(mid, A, f"{DEC}/marquee_titles", "T_Decal_MarqueeTitles", "Cinema Zenith marquee titles and blade sign",
                projector_note="projector per panel, size = size_m, depth 0.25 m, Decal Layer = cinema facade/marquee board.")
    return mid


def job_posters():
    mid = "DCL_POSTERS_COMPOSED"
    art = T.load(POSTER_ART, "RGBA")
    AH, AW = art.shape[:2]
    pw = AW // 3
    A = Atlas(1024, 1024)
    titles = [("TALVI ORRAN", "4-11  20.30"), ("NOLVEK", "5-11  21.00"), ("MIRRA KESSEL", "6-11  19.00")]
    titles_b = [("DRAN VESKO", "9-10  20.00"), ("ULSK TAVEN", "2-10  21.30"), ("KESSO MIRRA", "28-9  19.30")]
    for row in range(2):
        for k in range(3):
            src = art[:, k * pw:(k + 1) * pw]
            W, H = 341, 512
            p = {"col": T.srgb_to_lin(T.resize_srgb(src[..., :3], (W, H))), "a": T.resize(src[..., 3], (W, H)) if src.shape[2] == 4 else np.ones((H, W))}
            p["a"] = np.clip(p["a"], 0, 1)
            pap = paper(W, H, 1100 + row * 10 + k, tone="#efe6cf", torn=0.0 if row == 0 else 0.42, fade=0.2 + 0.4 * row)
            p["h"] = pap["h"]
            p["s"] = pap["s"]
            p["ao"] = pap["ao"]
            p["a"] = p["a"] * pap["a"]
            p["col"] = p["col"] * (1 - 0.1 * pap["wrinkle"][..., None])
            # title band: the bottom ~22 % of the plate is blank paper in the source art
            t, d = (titles if row == 0 else titles_b)[k]
            band = np.zeros((H, W), np.float32)
            y0 = int(H * 0.795)
            band[y0:y0 + 56] = text_mask(t, F_SARDANI, W, 56, fill_h=0.66, margin=0.1)
            band[y0 + 58:y0 + 92] = text_mask(d, F_COND, W, 34, fill_h=0.62, margin=0.25, cap_ref="1")
            p = ink_on(p, band, ["#7c1a14", "#13202e", "#1b1411"][k], 1200 + k)
            if row == 1:
                p = sun_fade_blue(p, 0.55)
            A.put(f"poster_{row}{k}", k * 341, row * 512, p,
                  {"kind": "film poster", "size_m": [0.68, 1.02], "text_key": f"{t} / {d}",
                   "use": "cinema lobby frames, market walls; row 1 = current, row 2 = sun-faded and torn older bills"})
    write_atlas(mid, A, f"{DEC}/posters_composed", "T_Decal_PostersComposed", "composed film posters (P1)",
                projector_note="projector per poster, 0.68 x 1.02 m, depth 0.2 m; overlap curfew notices on top of older bills.")
    return mid


def write_atlas(mid, A, sub, stem, title, projector_note):
    bc, n, m = A.bc(), A.normal(2.2), A.mask()
    rels = {s: f"{sub}/{stem}_{s}.png" for s in ("BC", "N", "M")}
    lay = f"{sub}/{stem}_layout.json"
    emit(mid, rels["BC"], bc, "texture_decal_basecolor",
         BC_NOTE + f" Atlas {A.W}x{A.H}: {len(A.rects)} rects listed in {os.path.basename(lay)} (uv = x, y, w, h, origin bottom-left; "
         f"size_m = real size). {projector_note} Sardani glyphs (WP08 font, O3-reviewed) + Latin numerals only.")
    emit(mid, rels["N"], n, "texture_decal_normal", N_NOTE)
    emit(mid, rels["M"], m, "texture_decal_mask", M_NOTE)
    emit_json(mid, lay, {"atlas": rels["BC"], "size_px": [A.W, A.H], "rects": A.rects},
              "Atlas layout (data, editor/builder use only; not referenced at runtime).")
    SPEC_ROWS.append({
        "manifest_id": mid, "material": "M_" + stem[2:], "target_path": f"Assets/_Game/Art/Materials/Decals/M_{stem[2:]}.mat",
        "shader": "Shader Graphs/Decal", "workflow": "URP Decal (Screen Space or DBuffer per renderer feature)",
        "textures": {"Base_Map": rels["BC"], "Normal_Map": rels["N"]}, "floats": {"Normal_Blend": 0.5},
        "toggles": {"Affect BaseColor": True, "Affect Normal": True}, "colors": {"_BaseColor": [1, 1, 1, 1]},
        "layout": lay, "note": f"{title}: one projector per atlas rect (layout json).",
        "optional": {"mask_MAOS": rels["M"], "note": "custom MAOS decal graph only"}})
    os.makedirs(REVIEW, exist_ok=True)
    T.save8(bc, os.path.join(REVIEW, f"{mid}_atlas.png"))
    preview(bc, f"{mid}_on_plaster.png")


def clock_dial(S, seed, roman=True, cream="#e8e0c8", ink="#1b1916", rust=0.6, minute_track=True):
    """Dial face only (the hands are geometry). Alpha = disc."""
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    c = (S - 1) / 2
    r = np.hypot(xx - c, yy - c) / (S / 2)
    ang = np.arctan2(xx - c, -(yy - c))  # 0 at twelve, clockwise
    disc = np.clip((0.995 - r) * S / 2.5, 0, 1)
    marks = np.zeros((S, S), np.float32)
    rim = ((r > 0.93) & (r < 0.955)).astype(np.float32)
    marks = np.maximum(marks, rim)
    if minute_track:
        inner = ((r > 0.80) & (r < 0.815)).astype(np.float32) + ((r > 0.89) & (r < 0.905)).astype(np.float32)
        marks = np.maximum(marks, inner)
        mt = ((np.abs(((ang / (2 * np.pi) * 60) % 1.0) - 0.5) > 0.47) & (r > 0.815) & (r < 0.89)).astype(np.float32)
        marks = np.maximum(marks, mt)
    big = Image.new("L", (S * 4, S * 4), 0)
    d = ImageDraw.Draw(big)
    numerals = ["XII", "I", "II", "III", "IIII", "V", "VI", "VII", "VIII", "IX", "X", "XI"] if roman else \
        ["12", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11"]
    font = ImageFont.truetype(F_SERIF if roman else F_COND, int(S * 4 * (0.105 if roman else 0.12)))
    for i, t in enumerate(numerals):
        a = i / 12 * 2 * np.pi
        rad = 0.66 * S * 2
        cx, cy = S * 2 + np.sin(a) * rad, S * 2 - np.cos(a) * rad
        tw = d.textlength(t, font=font)
        bb = d.textbbox((0, 0), t, font=font)
        tile = Image.new("L", (int(tw) + 40, bb[3] + 40), 0)
        ImageDraw.Draw(tile).text((20, 20 - bb[1] // 2), t, font=font, fill=255)
        if roman:  # Roman dials: numerals radial (feet towards the centre)
            tile = tile.rotate(-np.degrees(a), resample=Image.BICUBIC, expand=True)
        big.paste(tile, (int(cx - tile.size[0] / 2), int(cy - tile.size[1] / 2)), tile)
    num = np.asarray(big.resize((S, S), Image.LANCZOS)).astype(np.float32) / 255.0
    marks = np.maximum(marks, num)
    spindle = np.clip(1 - r / 0.045, 0, 1)
    fade = fbm((S, S), seed, S / 6, 3)
    col = lin(cream)[None, None, :] * (0.82 + 0.25 * fade[..., None])
    stain = np.clip((fbm((S, S), seed + 1, S / 10, 4) - 0.55) * 2.0, 0, 1)
    col = col * (1 - 0.3 * stain[..., None]) + lin("#8a7350")[None, None, :] * 0.3 * stain[..., None]
    # rust runs from the spindle and the rim bolts, downwards
    run = np.zeros((S, S), np.float32)
    run[int(c):, :] = np.clip(1 - np.abs(xx[int(c):] - c - S * 0.03) / (S * 0.06), 0, 1) * np.linspace(1, 0, S - int(c))[:, None] ** 1.5
    run = cv2.GaussianBlur(run, (0, 0), S * 0.03) * rust  # broad stain, never reads as a hand
    marks = np.clip(marks, 0, 1) * (1 - 0.35 * fbm((S, S), seed + 2, 4, 3))
    col = col * (1 - marks[..., None]) + lin(ink)[None, None, :] * marks[..., None]
    col = col * (1 - run[..., None] * 0.6) + lin("#6a3418")[None, None, :] * run[..., None] * 0.6
    col = col * (1 - spindle[..., None]) + lin("#2a2724")[None, None, :] * spindle[..., None]
    return {"col": col, "a": disc, "h": 0.5 + 0.1 * marks + 0.4 * spindle, "s": 0.45 - 0.25 * stain, "ao": 1 - 0.2 * stain}


def job_clocks():
    mid = "PROC_CLOCK_TOWER_FACES"
    sub = f"{PROPS}/clock_faces"
    # tower dial (2.5 m, colonial Roman numerals) and wall dial (0.64 m, Latin numerals)
    for stem, seed, roman, cream, size_m, use in (
            ("T_ClockDialTower", 1301, True, "#e5dcc2", 2.5, "clock tower BLD-05, all four faces (radius 1.25 m disc at the clock stage)"),
            ("T_ClockDialWall", 1302, False, "#efeadb", 0.64, "Relay Hall world-time wall, 12 clocks (radius 0.32 m)")):
        el = clock_dial(512, seed, roman=roman, cream=cream, rust=0.35 if roman else 0.0)
        bc = np.concatenate([T.lin_to_srgb(np.clip(el["col"], 0, 1)), el["a"][..., None]], 2)
        n = height_to_normal(cv2.GaussianBlur(el["h"], (0, 0), 0.7), 2.0)
        mk = T.pack_mask(np.zeros_like(el["a"]), el["ao"], np.clip(el["s"], 0, 1))
        note = (f"Dial face WITHOUT hands (the kit builds iron hands at 6:40, KitStreet.ClockFace) for {use}. "
                f"Texture Type Default, sRGB ON, Alpha Is Transparency ON, Max Size 512, DXT5 Crunched q55, mips on, wrap Clamp. "
                f"Apply as a URP Decal Projector {size_m} x {size_m} m over the disc (depth 0.1 m, Decal Layer = clock face only), "
                "or as Base Map of the KitSlots.ClockFace material when the disc has planar 0..1 UVs (alpha clip 0.5).")
        emit(mid, f"{sub}/{stem}_BC.png", bc, "texture_basecolor", note)
        emit(mid, f"{sub}/{stem}_N.png", n, "texture_normal", "Normal map, OpenGL (+Y), sRGB OFF, Max Size 512, DXT5nm Crunched q60, wrap Clamp.")
        emit(mid, f"{sub}/{stem}_M.png", mk, "texture_mask", "Mask D4, sRGB OFF, Max Size 512, DXT5 Crunched q60: R metal, G AO, B 0, A smoothness.")
        SPEC_ROWS.append({"manifest_id": mid, "material": "M_" + stem[2:], "target_path": f"Assets/_Game/Art/Materials/Props/M_{stem[2:]}.mat",
                          "shader": "Shader Graphs/Decal", "textures": {"Base_Map": f"{sub}/{stem}_BC.png", "Normal_Map": f"{sub}/{stem}_N.png"},
                          "floats": {"Normal_Blend": 0.4}, "toggles": {"Affect BaseColor": True, "Affect Normal": True},
                          "colors": {"_BaseColor": [1, 1, 1, 1]}, "projector": {"size_m": [size_m, size_m], "depth_m": 0.1, "pivot": "center"},
                          "note": f"Dial for {use}; hands are kit geometry at 6:40."})
        T.save8(bc, os.path.join(REVIEW, f"{stem}.png"))
    # 12 city plates (0.9 x 0.18 m) for the world-time wall
    cities = ["ZAROUD", "VESKARA", "ORMUTH", "TALVENNE", "KESSOMIR", "ULLAVAN", "MORRAKAN", "DRUVESK", "ISSOLTA", "VAUNDRET", "ORRADUN", "TEVKARA"]
    A = Atlas(1024, 512)
    for i, nm in enumerate(cities):
        cx, cy = i % 3, i // 3
        el = painted_board(320, 64, 1400 + i, "#2b2117", "#d4b26a", [nm], [F_SARDANI], [1.0], frame=True, wood=True,
                           fade_amt=0.08, flake_amt=0.08, streak=0.15)
        A.put(f"plate_{i:02d}", 10 + cx * 341, 32 + cy * 128, el, {"kind": "name plate", "size_m": [0.9, 0.18], "text_key": nm,
                                                                   "use": f"ClockPlate.{i} (Relay Hall world-time wall), gilt letters on dark wood"})
    bc, n, mk = A.bc(), A.normal(2.0), A.mask()
    stem = "T_ClockCityPlates"
    emit(mid, f"{sub}/{stem}_BC.png", bc, "texture_basecolor",
         "12 city name plates (invented cities in Sardani glyphs) for the world-time clock wall: Default, sRGB ON, Alpha Is "
         f"Transparency ON, Max Size 1024, DXT5 Crunched q55, wrap Clamp. Rects in {stem}_layout.json (uv origin bottom-left); "
         "one Decal Projector 0.9 x 0.18 m per ClockPlate.<i> anchor (depth 0.08 m), or UV-map the SignBoard quads to the rects.")
    emit(mid, f"{sub}/{stem}_N.png", n, "texture_normal", "Normal map, OpenGL (+Y), sRGB OFF, Max Size 1024, DXT5nm Crunched q60.")
    emit(mid, f"{sub}/{stem}_M.png", mk, "texture_mask", "Mask D4, sRGB OFF, Max Size 1024, DXT5 Crunched q60.")
    emit_json(mid, f"{sub}/{stem}_layout.json", {"atlas": f"{sub}/{stem}_BC.png", "size_px": [1024, 512], "rects": A.rects},
              "Atlas layout (data, editor/builder use only).")
    SPEC_ROWS.append({"manifest_id": mid, "material": "M_ClockCityPlates", "target_path": "Assets/_Game/Art/Materials/Props/M_ClockCityPlates.mat",
                      "shader": "Shader Graphs/Decal", "textures": {"Base_Map": f"{sub}/{stem}_BC.png", "Normal_Map": f"{sub}/{stem}_N.png"},
                      "floats": {"Normal_Blend": 0.5}, "toggles": {"Affect BaseColor": True, "Affect Normal": True},
                      "colors": {"_BaseColor": [1, 1, 1, 1]}, "layout": f"{sub}/{stem}_layout.json",
                      "note": "world-time wall plates, one projector per plate (layout json)."})
    T.save8(bc, os.path.join(REVIEW, f"{stem}.png"))
    return mid


def job_filmcans():
    mid = "PROC_FILM_CAN_LABELS"
    sub = f"{PROPS}/film_labels"
    A = Atlas(512, 512)
    labels = [("S-3", "SESSION 11", "03-84"), ("S-3", "SESSION 10", "02-84"), ("S-3", "SESSION 09", "01-84"), ("S-3", "SESSION 08", "12-83")]
    for i, (a_, b_, c_) in enumerate(labels):
        W, H = 512, 128
        shape = (H, W)
        # masking tape strip: torn ends, translucent edges, creases
        base = np.ones(shape, np.float32)
        xx = np.arange(W, dtype=np.float32)[None, :]
        jag = fbm(shape, 1500 + i, 5, 3)
        ends = ((xx > 10 + 10 * jag) & (xx < W - 10 - 10 * jag)).astype(np.float32)
        band = np.zeros(shape, np.float32)
        band[10:H - 10] = 1
        alpha = cv2.GaussianBlur(ends * band, (0, 0), 0.8) * 0.97
        col = lin("#d9c79a")[None, None, :] * (0.9 + 0.12 * fbm(shape, 1510 + i, 3, 3))[..., None]
        dirt = np.clip((fbm(shape, 1520 + i, 40, 3) - 0.5) * 1.5, 0, 1)
        col *= (1 - 0.25 * dirt)[..., None]
        txt = np.zeros(shape, np.float32)
        txt[16:72] = text_mask(f"{a_}  /  {b_}", F_TYPE, W, 56, fill_h=0.6, margin=0.07, align="left")
        txt[70:116] = text_mask(c_, F_TYPE, W, 46, fill_h=0.6, margin=0.07, align="left")
        el = {"col": col, "a": alpha, "h": 0.4 + 0.1 * dirt, "s": np.full(shape, 0.2, np.float32), "ao": np.ones(shape)}
        el = ink_on(el, txt, "#1b1a26", 1530 + i, misreg=(0.6, 0.3), mottle=0.45)
        A.put(f"label_{i}", 0, i * 128, el, {"kind": "masking-tape label", "size_m": [0.16, 0.04], "text_key": f"{a_} / {b_} / {c_}",
                                              "use": ("label_0 = the Session 11 can (MID-B reel, intel O2 close-up); the others on the rack cans "
                                                      "(B8 can rack, all S-3)")})
    bc, n, mk = A.bc(), A.normal(1.5), A.mask()
    stem = "T_FilmCanLabels"
    emit(mid, f"{sub}/{stem}_BC.png", bc, "texture_basecolor",
         "Typewritten masking-tape labels for the film cans (FAL_FILM_CAN lid): Default, sRGB ON, Alpha Is Transparency ON, Max Size 512, "
         f"DXT5 Crunched q55, wrap Clamp. 4 strips (rects in {stem}_layout.json); Decal Projector 0.16 x 0.04 m on the lid, depth 0.02 m. "
         "Latin text (allowed): 'S-3 / SESSION 11 / 03-84' (GDD §3, §7.8).")
    emit(mid, f"{sub}/{stem}_N.png", n, "texture_normal", "Normal map, OpenGL (+Y), sRGB OFF, Max Size 512, DXT5nm Crunched q60.")
    emit(mid, f"{sub}/{stem}_M.png", mk, "texture_mask", "Mask D4, sRGB OFF, Max Size 512, DXT5 Crunched q60.")
    emit_json(mid, f"{sub}/{stem}_layout.json", {"atlas": f"{sub}/{stem}_BC.png", "size_px": [512, 512], "rects": A.rects},
              "Atlas layout (data, editor/builder use only).")
    SPEC_ROWS.append({"manifest_id": mid, "material": "M_FilmCanLabels", "target_path": "Assets/_Game/Art/Materials/Props/M_FilmCanLabels.mat",
                      "shader": "Shader Graphs/Decal", "textures": {"Base_Map": f"{sub}/{stem}_BC.png", "Normal_Map": f"{sub}/{stem}_N.png"},
                      "floats": {"Normal_Blend": 0.3}, "toggles": {"Affect BaseColor": True, "Affect Normal": True},
                      "colors": {"_BaseColor": [1, 1, 1, 1]}, "layout": f"{sub}/{stem}_layout.json", "note": "film-can lid labels."})
    T.save8(bc, os.path.join(REVIEW, f"{stem}.png"))
    return mid


def job_exit():
    mid = "PROC_EXIT_SIGN"
    sub = f"{PROPS}/exit_sign"
    W, H = 512, 256
    shape = (H, W)
    face = np.zeros(shape, np.float32)
    face[18:H - 18, 18:W - 18] = 1
    lettersA = np.zeros(shape, np.float32)
    lettersA[30:190] = text_mask("EXIT", F_COND, W, 160, fill_h=0.78, margin=0.12, tracking=0.08)
    lettersB = np.zeros(shape, np.float32)
    lettersB[190:236] = text_mask("ULVO", F_SARDANI, W, 46, fill_h=0.7, margin=0.3)
    letters = np.clip(lettersA + lettersB, 0, 1)
    frame = 1 - face
    dust = fbm(shape, 1601, 40, 4)
    glass = lin("#0d1a12")[None, None, :] * (0.8 + 0.4 * dust[..., None])
    lit = lin("#2f8f4a")[None, None, :]
    col = glass * (1 - letters[..., None]) + lit * letters[..., None] * (0.85 + 0.2 * dust[..., None])
    col = col * (1 - frame[..., None]) + lin("#3a3733")[None, None, :] * frame[..., None] * (0.7 + 0.5 * dust[..., None])
    bc = np.concatenate([T.lin_to_srgb(np.clip(col, 0, 1)), np.ones(shape + (1,), np.float32)], 2)[..., :3]
    # emission: letters glow (green, uneven tube fill), slight bloom bleed inside the glass
    glow = cv2.GaussianBlur(letters, (0, 0), 6) * 0.25 * face
    tube = 0.75 + 0.25 * np.clip(1 - np.abs(np.linspace(-1, 1, W))[None, :] * 0.6, 0, 1)
    e = (letters * tube + glow)[..., None] * np.array([0.35, 1.0, 0.45], np.float32)[None, None, :]
    e = T.lin_to_srgb(np.clip(e, 0, 1))
    hgt = 0.5 + 0.4 * frame
    n = height_to_normal(cv2.GaussianBlur(hgt, (0, 0), 1.0), 2.0)
    mk = T.pack_mask(0.7 * frame, np.ones(shape), 0.85 * face + 0.3 * frame)
    note = ("Cinema EXIT box face (Latin, allowed by the manifest) with a small Sardani subline: Default, sRGB ON, Max Size 512, "
            "DXT1 Crunched q55, wrap Clamp. Map onto the front quad of the kit ExitSign box (UV 0..1, 2:1, ~0.36 x 0.18 m) or use as a "
            "decal; material M_ExitSign (URP/Lit, emission map _E x2.5 HDR green, flicker driven by the Look practicals).")
    emit(mid, f"{sub}/T_ExitSign_BC.png", bc, "texture_basecolor", note)
    emit(mid, f"{sub}/T_ExitSign_E.png", e, "texture_emissive", "Emission map: sRGB ON, Max Size 512, DXT1 Crunched q60, wrap Clamp (letters lit green).")
    emit(mid, f"{sub}/T_ExitSign_N.png", n, "texture_normal", "Normal map, OpenGL (+Y), sRGB OFF, Max Size 512, DXT5nm Crunched q60.")
    emit(mid, f"{sub}/T_ExitSign_M.png", mk, "texture_mask", "Mask D4, sRGB OFF, Max Size 512, DXT5 Crunched q60: frame metal, glass glossy.")
    SPEC_ROWS.append({"manifest_id": mid, "material": "M_ExitSign", "target_path": "Assets/_Game/Art/Materials/Props/M_ExitSign.mat",
                      "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic",
                      "textures": {"_BaseMap": f"{sub}/T_ExitSign_BC.png", "_BumpMap": f"{sub}/T_ExitSign_N.png",
                                   "_MetallicGlossMap": f"{sub}/T_ExitSign_M.png", "_OcclusionMap": f"{sub}/T_ExitSign_M.png"},
                      "floats": {"_Metallic": 1.0, "_Smoothness": 1.0},
                      "emission": {"_EmissionMap": f"{sub}/T_ExitSign_E.png", "_EmissionColor_linear": [2.5, 2.5, 2.5, 1]},
                      "assign_to_slot": "ExitSign",
                      "note": "Replaces the flat M_Level_ExitSign colour; WP10 Look ExposureFlicker/practicals drive the flicker."})
    T.save8(bc, os.path.join(REVIEW, "T_ExitSign_BC.png"))
    T.save8(e, os.path.join(REVIEW, "T_ExitSign_E.png"))
    return mid


JOBS = {"signs": job_signs, "marquee": job_marquee, "posters": job_posters, "clocks": job_clocks, "filmcans": job_filmcans,
        "exit": job_exit}


def merge(path, rows, key="tool"):
    old = []
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            old = json.load(f)
    files = {r.get("file") or r.get("material") for r in rows}
    keep = [r for r in old if (r.get("file") or r.get("material")) not in files]
    with open(path, "w", encoding="utf-8") as f:
        json.dump(keep + rows, f, indent=1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default=",".join(JOBS))
    a = ap.parse_args()
    os.makedirs(REVIEW, exist_ok=True)
    for j in a.only.split(","):
        print("job", j, "->", JOBS[j]())
    merge(INDEX, ROWS)
    merge(SPECS, SPEC_ROWS)
    print(f"{len(ROWS)} index rows, {len(SPEC_ROWS)} material specs; ids {sorted(IDS)}")


if __name__ == "__main__":
    main()
