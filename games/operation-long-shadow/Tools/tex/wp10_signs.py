#!/usr/bin/env python3
"""WP10_CC0: offline-baked Latin decals that need no Sardani glyph font (pure image generation, no fal):

  Tools/.venv/bin/python -I Tools/tex/wp10_signs.py [--only company,ring]

  DCL_COMPANY_LETTERING  "TRANS-DESERT TELEGRAPH & CABLE CO." in faded, flaking Art Deco capitals (OFL font
                         Limelight, Sorkin Type, downloaded from github.com/google/fonts into
                         RawAssets/wp10/cc0/_fonts/ with its OFL.txt). 1024x512 atlas, two rows of 1024x256:
                         row A "TRANS-DESERT TELEGRAPH", row B "& CABLE CO." (same cap height) -> place stacked
                         (two-line sign) or side by side on the Relay Hall facade.
  DCL_STAND_CLEAR_RING   objective ring for B6.StandClear (296, 0, 310), r 2.5 m: white soft ring + 24 inner
                         ticks on alpha, tinted to the objective colour by the material (no text: the HUD shows
                         "STAND CLEAR").
The Sardani-script decals (DCL_SARDANI_SIGNS, DCL_MARQUEE_TITLES, DCL_POSTERS_COMPOSED) need WP08's glyph font and
stay with SignBaker (editor). SignBaker may also re-bake the company lettering; this file is the offline version.
Outputs: RawAssets/wp10/ready/Assets/_Game/Art/Textures/Decals/<sub>/T_Decal_<Name>_{BC,N,M}.png, rows tagged
"wp10_signs" in ready/import_index_cc0.json, contact sheets RawAssets/wp10/cc0/_contact/<ID>.jpg.
"""
import argparse, json, os, sys
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wp10_texlib as T  # noqa: E402
from wp10_texset import update_json, READY, RAW  # noqa: E402
from wp10_contact import sheet  # noqa: E402

DEC = 'Assets/_Game/Art/Textures/Decals'
FONT = os.path.join(RAW, '_fonts', 'Limelight-Regular.ttf')
BC_NOTE = ('Decal base map: Texture Type Default, sRGB ON, Alpha Is Transparency ON, Max Size 1024, DXT5 Crunched q60, '
           'mips on, aniso 2, wrap Clamp. URP Decal Projector (Shader Graphs/Decal), alpha = opacity.')
N_NOTE = 'Normal map, OpenGL (+Y), sRGB OFF, Max Size 1024, DXT5nm Crunched q60, wrap Clamp. Decal Normal Blend 0.5.'
M_NOTE = ('Mask, sRGB OFF, Max Size 1024, DXT5 Crunched q60, wrap Clamp: R metallic, G AO, B 0, A smoothness '
          '(custom MAOS decal graph only; the default URP decal graph ignores it).')


def out(rel):
    p = os.path.join(READY, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    return p


def row(mid, rel, typ, notes):
    return {'manifest_id': mid, 'file': rel, 'target_path': rel, 'type': typ, 'import_notes': notes, 'tool': 'wp10_signs'}


def fbm(shape, seed, base_px, octaves=5):
    """Tileable-enough value-noise fbm in [0,1] (bicubic-upsampled random grids)."""
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
        px /= 2.0
    acc /= tot
    return (acc - acc.min()) / max(acc.max() - acc.min(), 1e-6)


def height_to_normal(hgt, strength):
    gy, gx = np.gradient(hgt * strength)
    n = np.stack([-gx, gy, np.ones_like(hgt)], 2)     # OpenGL: +Y up (image rows grow downward -> +gy)
    return T.encode_normal(n)


def text_mask(lines, W, H, ss=4, margin=26):
    """Render each line into its own row (H / len(lines) tall) at the same cap height; returns float mask HxW."""
    rows = len(lines)
    rh = H // rows
    big = Image.new('L', (W * ss, H * ss), 0)
    d = ImageDraw.Draw(big)
    # one font size for every row: the widest line fills the row width, cap height <= 72 % of the row
    size = 400
    while True:
        f = ImageFont.truetype(FONT, size)
        wmax = max(d.textbbox((0, 0), ln, font=f)[2] - d.textbbox((0, 0), ln, font=f)[0] for ln in lines)
        cap = d.textbbox((0, 0), 'H', font=f)
        if wmax <= (W - 2 * margin) * ss and (cap[3] - cap[1]) <= 0.72 * rh * ss:
            break
        size -= 4
    for i, ln in enumerate(lines):
        bb = d.textbbox((0, 0), ln, font=f)
        cap = d.textbbox((0, 0), 'H', font=f)
        x = (W * ss - (bb[2] - bb[0])) // 2 - bb[0]
        y = i * rh * ss + (rh * ss - (cap[3] - cap[1])) // 2 - cap[1]
        d.text((x, y), ln, font=f, fill=255)
    m = np.asarray(big.resize((W, H), Image.LANCZOS)).astype(np.float32) / 255.0
    return m, size / ss


def company():
    mid = 'DCL_COMPANY_LETTERING'
    W, H = 1024, 512
    m, fsize = text_mask(['TRANS-DESERT TELEGRAPH', '& CABLE CO.'], W, H)
    # hand-painted edge: the stroke boundary wanders by ~1 px (sign-writer's brush)
    wob = fbm((H, W), 3, 6, 3) - 0.5
    edge = np.clip((m - 0.5 + wob * 0.35) * 3.0 + 0.5, 0, 1)
    # flaking: large flakes (fbm threshold) + small chips; flaked paint leaves a faint stain (ghost)
    flake_big = fbm((H, W), 11, 48, 5)
    chips = fbm((H, W), 17, 9, 4)
    lost = np.clip((flake_big - 0.70) * 40.0, 0, 1)            # hard-edged flakes
    lost = np.maximum(lost, np.clip((chips - 0.75) * 40.0, 0, 1))
    # hairline cracks following the plaster: thin ridges of a mid-scale noise
    rid = 1.0 - np.abs(fbm((H, W), 23, 70, 4) * 2.0 - 1.0)
    crack = np.clip((rid - 0.93) * 25.0, 0, 1)
    lost = np.maximum(lost, crack * 0.9)
    lost = cv2.GaussianBlur(lost, (0, 0), 0.5)
    paint = edge * (1.0 - lost)
    ghost = edge * lost * 0.22
    # sun fade: top of each row bleaches more (the south facade takes the afternoon sun)
    yy = (np.arange(H)[:, None] % (H // 2)) / (H // 2)
    fade = np.clip(0.25 + 0.55 * fbm((H, W), 31, 120, 3) + 0.15 * (1 - yy), 0, 1)
    deep = T.srgb_to_lin(np.array([0.33, 0.15, 0.11], np.float32))      # oxblood sign paint
    pale = T.srgb_to_lin(np.array([0.58, 0.42, 0.35], np.float32))      # sun-bleached
    col = deep[None, None, :] * (1 - fade[..., None]) + pale[None, None, :] * fade[..., None]
    col *= (0.86 + 0.28 * fbm((H, W), 41, 5, 4))[..., None]             # brush texture / chalking
    alpha = np.clip(paint * 0.93 + ghost, 0, 1)
    bc = np.concatenate([T.lin_to_srgb(np.clip(col, 0, 1)), alpha[..., None]], 2)
    hgt = cv2.GaussianBlur(paint, (0, 0), 0.8)
    n = height_to_normal(hgt, 2.5)
    smooth = 0.32 * paint + 0.12 * (1 - paint)
    mk = T.pack_mask(np.zeros_like(alpha), 1.0 - 0.25 * ghost, smooth)
    sub = f'{DEC}/company_lettering'
    rows = []
    for suf, a, typ, note in (('BC', bc, 'texture_decal_basecolor', BC_NOTE), ('N', n, 'texture_decal_normal', N_NOTE),
                              ('M', mk, 'texture_decal_mask', M_NOTE)):
        rel = f'{sub}/T_Decal_CompanyLettering_{suf}.png'
        T.save8(a, out(rel))
        if suf == 'BC':
            note += (' Atlas 1024x512, UV rects (x,y,w,h, origin bottom-left): row A "TRANS-DESERT TELEGRAPH" (0,0.5,1,0.5) 4:1, '
                     'row B "& CABLE CO." (0,0,1,0.5) 4:1 (text centred, same cap height). On the Relay Hall facade (BLD-10, 40 m): '
                     'row A projector ~16 x 4 m, cap height ~2.1 m, centred above the double door; row B below it or to its right. '
                     'Faded oxblood paint, flaked, on top of the wall plaster; Decal Layer = facade only. Font: Limelight (OFL 1.1, '
                     'Sorkin Type Co) - credit in the font list.')
        rows.append(row(mid, rel, typ, note))
    # facade preview: 1024 px = 16 m of row A -> painted_plaster_wall (2 m tile) is 128 px
    preview(bc, 'Assets/_Game/Art/Textures/Env/painted_plaster_wall/T_PaintedPlasterWall_BC.png', 128, (1, 1, 1),
            f'{mid}_preview.jpg')
    sheet(f'{mid}  T_Decal_CompanyLettering', bc, n, mk, os.path.join(RAW, '_contact', f'{mid}.jpg'),
          ['T_Decal_CompanyLettering 1024x512 (2 rows)', 'Limelight OFL, faded oxblood', f'font px {fsize:.0f}'])
    return mid, rows


def ring():
    mid = 'DCL_STAND_CLEAR_RING'
    S = 1024
    span_m = 5.6                      # projector size: ring radius 2.5 m + glow margin
    px_m = S / span_m
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    c = (S - 1) / 2.0
    r = np.hypot(xx - c, yy - c) / px_m          # metres from the centre
    ang = np.arctan2(yy - c, xx - c)
    R, w = 2.5, 0.09                              # ring centre line radius / half width (m)
    core = np.clip(1.0 - (np.abs(r - R) - w) / 0.025, 0, 1)                   # 0.18 m solid band, 2.5 cm AA edge
    glow = np.exp(-((r - R) / 0.28) ** 2) * 0.35                              # soft halo for readability on sand
    # 24 short ticks inside the ring (0.25 m long), pointing inward
    k = 24
    a_frac = (ang / (2 * np.pi) * k) % 1.0
    tick_ang = np.clip(1.0 - (np.abs(a_frac - 0.5) * (2 * np.pi * R / k) - 0.03) / 0.02, 0, 1)
    tick_rad = np.clip(1.0 - (np.abs(r - (R - 0.33)) - 0.11) / 0.02, 0, 1)
    ticks = tick_ang * tick_rad * 0.85
    alpha = np.clip(np.maximum(core, ticks) + glow * (1 - core), 0, 1)
    alpha *= np.clip((span_m / 2 - r) / 0.15, 0, 1)                            # clean fade before the projector edge
    bc = np.concatenate([np.ones((S, S, 3), np.float32), alpha[..., None]], 2)
    n = np.stack([np.full((S, S), 0.5), np.full((S, S), 0.5), np.ones((S, S))], 2).astype(np.float32)
    mk = T.pack_mask(np.zeros((S, S)), np.ones((S, S)), np.full((S, S), 0.1))
    sub = f'{DEC}/stand_clear_ring'
    rows = []
    rel = f'{sub}/T_Decal_StandClearRing_BC.png'
    T.save8(bc, out(rel))
    rows.append(row(mid, rel, 'texture_decal_basecolor',
                    BC_NOTE + ' Objective ring for B6.StandClear at (296, 0, 310): projector 5.6 x 5.6 m, depth 1 m, pointing down; '
                    'ring radius 2.5 m (0.18 m band + soft halo, 24 inner ticks). RGB white: tint with the objective colour '
                    '(material Base Color) and optionally the same colour as emission x0.5 so it reads at dusk; Affects Normal OFF. '
                    'No text (the HUD carries "STAND CLEAR"). Hide the projector on objective completion.'))
    preview(bc, 'Assets/_Game/Art/Textures/Env/sand_01/T_Sand01_BC.png', int(1.5 * px_m), (1.0, 0.78, 0.2),
            f'{mid}_preview.jpg', emis=0.5)
    sheet(f'{mid}  T_Decal_StandClearRing', bc, n, mk, os.path.join(RAW, '_contact', f'{mid}.jpg'),
          ['T_Decal_StandClearRing 1024 = 5.6 m', 'white, tint = objective colour', 'r 2.5 m, band 0.18 m'])
    return mid, rows


def preview(bc, ground_rel, tile_px, tint, out_name, emis=0.0):
    """Composite the decal over a shipped env texture (tiled at its real scale) for review."""
    g = T.load(os.path.join(READY, ground_rel), 'RGB')
    H, W = bc.shape[:2]
    g = T.resize_srgb(g, (tile_px, tile_px))
    reps = (int(np.ceil(H / tile_px)), int(np.ceil(W / tile_px)), 1)
    g = np.tile(g, reps)[:H, :W]
    lin = T.srgb_to_lin(g)
    dc = T.srgb_to_lin(bc[..., :3]) * np.array(tint, np.float32)[None, None, :]
    a = bc[..., 3:4]
    comp = lin * (1 - a) + dc * a + dc * a * emis
    T.save8(T.lin_to_srgb(np.clip(comp, 0, 1)), os.path.join(RAW, '_contact', out_name))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--only', default='company,ring')
    a = ap.parse_args()
    rows, ids = [], set()
    for job in a.only.split(','):
        mid, rr = {'company': company, 'ring': ring}[job]()
        rows += rr
        ids.add(mid)
        print(mid, [r['file'] for r in rr])
    update_json(os.path.join(READY, 'import_index_cc0.json'), rows, key_tool='wp10_signs', key_ids=ids)


if __name__ == '__main__':
    main()
