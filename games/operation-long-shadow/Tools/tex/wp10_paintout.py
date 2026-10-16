#!/usr/bin/env python3
"""WP10_CC0 guardrail paint-out (GDD 2.3-3, D17): removes real-world words, brands and logos from
Poly Haven prop base-colour textures before they are packed. Latin numerals / generic codes stay.

  Tools/.venv/bin/python -I Tools/tex/wp10_paintout.py

Writes RawAssets/wp10/cc0/<ID>/clean/<texture>.jpg (preferred by wp10_props_tex.py) and a before/after
sheet RawAssets/wp10/cc0/_contact/PAINTOUT_<ID>.jpg. Boxes are 1k texture pixels (x0, y0, x1, y1).
Normal maps (*_nor_gl_*) are cleaned the same way (embossed brand lettering): 'all' inpaints the box flat.
Modes: bright / dark (stroke mask vs local median), deviant (pixels off the box's dominant colour),
all (whole box), fill (paint the box with a sampled flat colour + the original fine grain).
"""
import os
import numpy as np
import cv2

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')

JOBS = {
    'PH_AMMO_BOX': [('ammo_box_diff_1k.jpg', [((28, 515, 165, 910), 'bright')])],          # "200 CARTRIDGES 7.62 MM ... CARTONS"
    'PH_CASSETTE_PLAYER': [
        ('cassette_player_body_diff_1k.jpg', [((378, 793, 545, 820), 'all')]),             # brand + model line
        ('cassette_player_tape_nor_gl_1k.jpg', [                                     # embossed shell lettering (brand + HIGH FIDELITY)
            ((178, 280, 465, 322), 'all'), ((70, 462, 570, 500), 'all'), ((70, 732, 570, 772), 'all'), ((175, 880, 465, 948), 'all')]),
        ('cassette_player_tape_orangeblue_diff_1k.jpg', [
            ((178, 280, 465, 322), 'dark'), ((70, 466, 570, 498), 'dark'), ((514, 422, 584, 437), 'all'),
            ((70, 736, 570, 768), 'dark'), ((40, 795, 115, 813), 'all'), ((175, 905, 465, 948), 'dark')])],
    'PH_FILMSTRIP_PROJECTOR_8MM': [('filmstrip_projector_8mm_diff_1k.jpg', [((286, 486, 347, 520), 'all')])],
    'PH_PROJECTOR_SCREEN': [('projector_screen_frame_diff_1k.jpg', [((698, 456, 750, 548), 'all')])],
    'PH_OLD_MILITARY_CRATE': [('old_military_crate_diff_1k.jpg', [((834, 800, 866, 1004), 'bright')])],  # "HANDLE WITH CARE"
    'PH_PORTABLE_GENERATOR': [
        ('portable_generator_diff_1k.jpg', [((390, 980, 536, 1020), 'dark'), ((306, 878, 352, 898), 'all'),
                                            ((573, 880, 605, 900), 'all'),            # DANGER sticker
                                            ((345, 926, 390, 937), 'dark'),           # AC OUT 120V
                                            ((432, 926, 466, 937), 'dark'),           # CIRCUIT BREAKER
                                            ((516, 924, 562, 943), 'all'),            # white spec label
                                            ((447, 940, 460, 969), 'bright'),         # OFF / ON
                                            ((568, 967, 606, 978), 'dark'),           # ENGINE SW.
                                            ((666, 429, 706, 442), 'fill:663,432,3,5'),  # black spec plate text
                                            ((722, 560, 760, 593), 'all'),            # recoil-starter brand sticker
                                            ((746, 736, 835, 765), 'all')]),          # warning label (whole sticker)
        ('portable_generator_nor_gl_1k.jpg', [((388, 978, 538, 1022), 'all'),       # embossed "EN2500" model name
                                              ((56, 856, 113, 869), 'all')])],         # embossed lettering on the tank
    'PH_PORTABLE_SEARCHLIGHT': [
        ('portable_searchlight_diff_1k.jpg', [((520, 128, 566, 142), 'bright')]),    # "SENTINEL XL4" brand line
        ('portable_searchlight_nor_gl_1k.jpg', [((626, 832, 672, 994), 'all')])],   # embossed "PIXLITE" brand
    'PH_CLIPBOARD': [('clipboard_nor_gl_1k.jpg', [((541, 170, 572, 305), 'all'), ((738, 170, 767, 305), 'all')])],  # embossed "Supplyhaven"
    'PH_WOODEN_CRATE_02': [('wooden_crate_02_diff_1k.jpg', [((22, 740, 150, 860), 'dark'), ((245, 750, 372, 870), 'dark')])],  # W.I.C. company monogram (real historical mark)
    'PH_PROPANE_TANK': [('propane_tank_diff_1k.jpg', [((284, 512, 398, 596), 'deviant'), ((420, 524, 468, 598), 'deviant')])],
    'PH_UTILITY_BOX_01': [('utility_box_01_diff_1k.jpg', [((389, 905, 452, 922), 'dark'), ((389, 942, 452, 956), 'dark'),
                                                           ((388, 957, 453, 977), 'fill:400,925,10,8')])],
}


def stroke_mask(img, box, mode):
    x0, y0, x1, y1 = box
    sub = img[y0:y1, x0:x1].astype(np.float32) / 255.0
    lum = sub @ np.array([0.299, 0.587, 0.114], np.float32)
    if mode in ('bright', 'dark'):
        med = cv2.medianBlur((lum * 255).astype(np.uint8), 21).astype(np.float32) / 255.0
        d = lum - med if mode == 'bright' else med - lum
        mad = np.median(np.abs(d - np.median(d))) + 1e-4
        m = d > max(0.05, 3.0 * mad)
    elif mode == 'deviant':
        ref = np.median(sub.reshape(-1, 3), axis=0)
        m = np.linalg.norm(sub - ref, axis=2) > 0.10
    else:
        m = np.ones(lum.shape, bool)
    m = cv2.dilate(m.astype(np.uint8), np.ones((3, 3), np.uint8), iterations=2)
    full = np.zeros(img.shape[:2], np.uint8)
    full[y0:y1, x0:x1] = m
    return full


def main():
    for mid, jobs in JOBS.items():
        for tex, boxes in jobs:
            src = os.path.join(RAW, mid, 'gltf_1k', 'textures', tex)
            img = cv2.imread(src, cv2.IMREAD_COLOR)
            orig = img.copy()
            for box, mode in boxes:
                if mode.startswith('fill'):
                    sx, sy, sw, sh = map(int, mode.split(':')[1].split(','))
                    col = np.median(img[sy:sy + sh, sx:sx + sw].reshape(-1, 3), axis=0)
                    x0, y0, x1, y1 = box
                    g = img[y0:y1, x0:x1].astype(np.float32)
                    rng = np.random.default_rng(7)
                    grain = cv2.GaussianBlur(rng.normal(0, 4.0, g.shape[:2]).astype(np.float32), (0, 0), 0.8)[..., None]
                    img[y0:y1, x0:x1] = np.clip(col[None, None, :] + grain, 0, 255).astype(np.uint8)
                    continue
                m = stroke_mask(img, box, mode)
                img = cv2.inpaint(img, m, 5, cv2.INPAINT_TELEA)
            out = os.path.join(RAW, mid, 'clean', tex)
            os.makedirs(os.path.dirname(out), exist_ok=True)
            cv2.imwrite(out, img, [cv2.IMWRITE_JPEG_QUALITY, 97])
            # review sheet: crops of each box, before | after
            tiles = []
            for box, _ in boxes:
                x0, y0, x1, y1 = box
                pad = 12
                xa, ya, xb, yb = max(0, x0 - pad), max(0, y0 - pad), min(img.shape[1], x1 + pad), min(img.shape[0], y1 + pad)
                a, b = orig[ya:yb, xa:xb], img[ya:yb, xa:xb]
                s = max(1, int(220 / max(a.shape[:2])))
                a = cv2.resize(a, None, fx=s, fy=s, interpolation=cv2.INTER_NEAREST)
                b = cv2.resize(b, None, fx=s, fy=s, interpolation=cv2.INTER_NEAREST)
                tiles.append(np.concatenate([a, np.zeros((a.shape[0], 6, 3), np.uint8), b], 1))
            W = max(t.shape[1] for t in tiles)
            sheet = np.concatenate([np.pad(t, ((0, 8), (0, W - t.shape[1]), (0, 0))) for t in tiles], 0)
            cv2.imwrite(os.path.join(RAW, '_contact', f'PAINTOUT_{mid}_{tex[:-4]}.jpg'), sheet, [cv2.IMWRITE_JPEG_QUALITY, 90])
            print(mid, tex, 'boxes', len(boxes))


if __name__ == '__main__':
    main()
