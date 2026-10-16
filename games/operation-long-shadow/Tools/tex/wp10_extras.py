#!/usr/bin/env python3
"""WP10_CC0 extras: decals (ambientCG), lens dirt, Terrain003 heightmap, shared detail/macro maps.

  Tools/.venv/bin/python -I Tools/tex/wp10_extras.py [--only decals,lens,terrain,shared]

Outputs under RawAssets/wp10/ready/Assets/_Game/Art/Textures/{Decals,FX,Env}/... and rows (tool "wp10_extras")
in RawAssets/wp10/ready/import_index_cc0.json. Contact sheets: RawAssets/wp10/cc0/_contact/<id>.jpg.
"""
import argparse, glob, json, os, sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wp10_texlib as T  # noqa: E402
from wp10_texset import update_json, READY, RAW  # noqa: E402
from wp10_contact import sheet  # noqa: E402

DEC = 'Assets/_Game/Art/Textures/Decals'


def acg(d, key):
    hits = sorted(glob.glob(os.path.join(d, f'*_{key}.jpg')))
    return hits[0] if hits else None


def out_path(rel):
    p = os.path.join(READY, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    return p


def row(mid, rel, typ, notes):
    return {'manifest_id': mid, 'file': rel, 'target_path': rel, 'type': typ, 'import_notes': notes, 'tool': 'wp10_extras'}


DECAL_BC = ('Decal base map: Texture Type Default, sRGB ON, Alpha Is Transparency ON, Max Size 1024, DXT5 Crunched q60, '
            'mips on, aniso 2, wrap Clamp. URP Decal Projector (Shader Graphs/Decal, Screen Space technique); '
            'alpha = opacity.')
DECAL_N = ('Normal map, OpenGL (+Y), sRGB OFF, Max Size 1024, DXT5nm Crunched q60, wrap Clamp. Decal Normal Blend 0.6-1.0.')
DECAL_M = ('Mask (decal MAOS-style), sRGB OFF, Max Size 1024, DXT5 Crunched q60, wrap Clamp: R metallic, G AO, B 0, '
           'A smoothness. Only for a custom decal graph with MAOS; the default URP decal graph ignores it.')


def decal_set(mid, name, rel_dir, bc_rgba, n, m=None, extra=''):
    rows = []
    rel = f'{rel_dir}/T_Decal_{name}_BC.png'; T.save8(bc_rgba, out_path(rel)); rows.append(row(mid, rel, 'texture_decal_basecolor', DECAL_BC + extra))
    rel = f'{rel_dir}/T_Decal_{name}_N.png'; T.save8(n, out_path(rel)); rows.append(row(mid, rel, 'texture_decal_normal', DECAL_N))
    if m is not None:
        rel = f'{rel_dir}/T_Decal_{name}_M.png'; T.save8(m, out_path(rel)); rows.append(row(mid, rel, 'texture_decal_mask', DECAL_M))
    mm = m if m is not None else np.stack([np.zeros(n.shape[:2]), np.ones(n.shape[:2]), np.zeros(n.shape[:2]), np.full(n.shape[:2], 0.3)], 2)
    sheet(f'{mid}  T_Decal_{name}', bc_rgba, n, mm, os.path.join(RAW, '_contact', f'{mid}.jpg'),
          [f'T_Decal_{name}  {bc_rgba.shape[1]}x{bc_rgba.shape[0]}', 'alpha shown over checker', extra[:60]])
    return rows


def mask_from(rough=None, ao=None, metal=None, shape=None):
    h, w = shape
    r = rough if rough is not None else np.full((h, w), 0.7, np.float32)
    a = ao if ao is not None else np.ones((h, w), np.float32)
    mt = metal if metal is not None else np.zeros((h, w), np.float32)
    return T.pack_mask(mt, a, 1 - r)


def do_decals():
    rows = []
    # --- tyre tracks: alpha from displacement deviation (no opacity map); colour = sand_01 mean darkened by AO
    d = os.path.join(RAW, 'DCL_TIRE_TRACKS')
    disp = T.load_gray(acg(d, 'Displacement'))
    ao = T.load_gray(acg(d, 'AmbientOcclusion'))
    n = T.load(acg(d, 'NormalGL'), 'RGB')
    rough = T.load_gray(acg(d, 'Roughness'))
    med = np.median(disp)
    dev = np.abs(disp - med)
    a = np.clip(dev / max(np.percentile(dev, 97), 1e-3), 0, 1) ** 0.7
    a = T.blur_wrap(a, 6.0)
    a = np.clip(a * 1.6, 0, 1)
    h, w = a.shape
    xx = np.linspace(0, 1, w)[None, :]
    yy = np.linspace(0, 1, h)[:, None]
    edge = np.clip(np.minimum(np.minimum(xx, 1 - xx) / 0.06, np.minimum(yy, 1 - yy) / 0.12), 0, 1)
    a = a * edge * 0.85
    sand = np.array([0.652, 0.585, 0.436], np.float32)
    col_lin = T.srgb_to_lin(sand)[None, None, :] * (0.55 + 0.45 * ao[..., None]) * (0.9 + 0.1 * (disp[..., None] - med + 0.5))
    bc = np.concatenate([T.lin_to_srgb(col_lin), a[..., None]], 2)
    nn = T.decode_normal(n)
    nn[..., :2] *= edge[..., None]
    rows += decal_set('DCL_TIRE_TRACKS', 'TireTracks', f'{DEC}/tire_tracks', bc, T.encode_normal(nn),
                      mask_from(rough, ao, None, a.shape),
                      ' Projector size 1.8 x 0.45 m (texture 4:1). Colour is sand_01 darkened by tread AO; alpha derived from displacement (decal_alpha method).')
    # --- leaks atlas 1024x1024: Leaking004 (grime, rows 0-511), Leaking007 (rust, 512-767), Leaking009 (grime, 768-1023)
    d = os.path.join(RAW, 'DCL_LEAKS')
    tiles = []
    tints = {'Leaking004': ('grime', np.array([0.075, 0.062, 0.048])), 'Leaking007': ('rust', np.array([0.24, 0.095, 0.035])),
             'Leaking009': ('grime', np.array([0.085, 0.07, 0.055]))}
    for lid in ('Leaking004', 'Leaking007', 'Leaking009'):
        sd = os.path.join(d, lid)
        col = T.load(acg(sd, 'Color'), 'RGB')
        op = T.load_gray(acg(sd, 'Opacity'))
        nrm = T.load(acg(sd, 'NormalGL'), 'RGB')
        rgh = T.load_gray(acg(sd, 'Roughness'))
        # source colour is green algae (damp climate): keep its luminance structure, re-tint for a dry town
        lum = T.srgb_to_lin(col).mean(axis=2)
        lum = lum / max(lum.mean(), 1e-4)
        kind, tint = tints[lid]
        lin = np.clip(tint[None, None, :] * (0.6 + 0.4 * lum[..., None]), 0, 1)
        tiles.append((np.concatenate([T.lin_to_srgb(lin), op[..., None]], 2), nrm, mask_from(rgh, None, None, op.shape)))
    bc = np.concatenate([t[0] for t in tiles], 0)
    nrm = np.concatenate([t[1] for t in tiles], 0)
    mk = np.concatenate([t[2] for t in tiles], 0)
    rows += decal_set('DCL_LEAKS', 'Leaks', f'{DEC}/leaks', bc, nrm, mk,
                      ' Atlas 1024x1024, UV rects (x,y,w,h, origin bottom-left): Leaking004 grime (0,0.5,1,0.5) 2:1; '
                      'Leaking007 rust (0,0.25,1,0.25) 4:1; Leaking009 grime (0,0,1,0.25) 4:1. Re-tinted from green algae to dry grime/rust.')
    # --- asphalt damage atlas, scratches, fingerprints, tape: straight repack
    simple = [('DCL_ASPHALT_DAMAGE', 'AsphaltDamage', 'asphalt_damage', ' AsphaltDamageSet001 atlas (pothole/crack shapes as authored by ambientCG).'),
              ('DCL_SCRATCHES', 'Scratches', 'scratches', ' Scratch overlay (grey on black). Use as detail/overlay mask on FP weapons and glass; as a decal keep opacity low (0.3-0.5).'),
              ('DCL_FINGERPRINTS', 'Fingerprints', 'fingerprints', ' Smudge overlay for CRT glass: drive smoothness down where alpha > 0.'),
              ('DCL_TAPE', 'Tape', 'tape', ' Silver duct-tape atlas (5 strips).')]
    for mid, name, sub, note in simple:
        d = os.path.join(RAW, mid)
        col = T.load(acg(d, 'Color'), 'RGB')
        op = T.load_gray(acg(d, 'Opacity'))
        nrm = T.load(acg(d, 'NormalGL'), 'RGB')
        rp = acg(d, 'Roughness')
        rgh = T.load_gray(rp) if rp else None
        metal = None
        if mid == 'DCL_TAPE':
            metal = np.zeros_like(op)  # duct tape: polymer with aluminium-look pigment, dielectric
        bc = np.concatenate([col, op[..., None]], 2)
        rows += decal_set(mid, name, f'{DEC}/{sub}', bc, nrm, mask_from(rgh, None, metal, op.shape), note)
    return rows


def do_lens():
    d = os.path.join(RAW, 'TEX_LENS_DIRT')
    col = T.load_gray(acg(d, 'Color'))
    op = T.load_gray(acg(d, 'Opacity'))
    src = np.maximum(col, op)
    hp = src - T.blur_wrap(src, 24.0) * 0.85
    lo, hi = np.percentile(hp, 40), np.percentile(hp, 99.7)
    v = np.clip((hp - lo) / max(hi - lo, 1e-4), 0, 1) ** 1.35
    # a few soft dust blooms (seeded) so the dirt also reads on big bright sources
    rng = np.random.default_rng(640)
    h, w = v.shape
    yy, xx = np.mgrid[0:h, 0:w]
    spots = np.zeros_like(v)
    for _ in range(90):
        cx, cy, r = rng.uniform(0, w), rng.uniform(0, h), rng.uniform(3, 22)
        spots += rng.uniform(0.15, 0.6) * np.exp(-(((xx - cx) ** 2 + (yy - cy) ** 2) / (2 * r * r)))
    v = np.clip(v * 0.85 + spots * 0.5, 0, 1)
    rgb = np.stack([v * 1.0, v * 0.97, v * 0.92], 2)  # faintly warm, like dusty glass
    rel = 'Assets/_Game/Art/Textures/FX/T_LensDirt_BC.png'
    T.save8(rgb, out_path(rel))
    sheet('TEX_LENS_DIRT  T_LensDirt_BC', rgb, np.stack([np.full_like(v, .5), np.full_like(v, .5), np.ones_like(v)], 2),
          np.stack([np.zeros_like(v), np.ones_like(v), np.zeros_like(v), v], 2), os.path.join(RAW, '_contact', 'TEX_LENS_DIRT.jpg'),
          ['bloom dirt: black = clean, bright = dirt', 'from SurfaceImperfections003 (high-pass + levels) + dust specks'])
    return [row('TEX_LENS_DIRT', rel, 'texture_fx',
                'Bloom lens-dirt (VP_Base Bloom.dirtTexture, intensity 0.8). Texture Type Default, sRGB ON, Max Size 1024, '
                'DXT1 Crunched q60, mips OFF, wrap Clamp. Black = clean glass.')]


def do_terrain():
    import OpenEXR
    d = os.path.join(RAW, 'TEX_ACG_TERRAIN003')
    f = OpenEXR.File(os.path.join(d, 'Terrain003_2K.exr'))
    hgt = f.parts[0].channels['RGBA'].pixels[..., 0].astype(np.float32)
    hgt = (hgt - hgt.min()) / max(hgt.max() - hgt.min(), 1e-6)
    base = 'Assets/_Game/Art/Textures/Env/Terrain003'
    rows = []
    rel = f'{base}/T_Terrain003_H.png'; T.save16_gray(hgt, out_path(rel))
    rows.append(row('TEX_ACG_TERRAIN003', rel, 'texture_height16',
                    'EditorOnly source for PROC_RIDGE_SILHOUETTE / far hills: 16-bit grey PNG 2048x2048, 0..1 = 0..200 m over 500 x 500 m '
                    '(ambientCG metadata). Import: Default, sRGB OFF, Read/Write ON, compression None, Max Size 2048, no mips. '
                    'Never referenced at runtime (the generated mesh ships).'))
    raw = (np.clip(hgt, 0, 1) * 65535 + 0.5).astype('<u2').tobytes()
    rel = f'{base}/Terrain003_height_2048_r16.bytes'
    with open(out_path(rel), 'wb') as fh:
        fh.write(raw)
    rows.append(row('TEX_ACG_TERRAIN003', rel, 'data_heightmap_r16',
                    'Same heightmap as raw little-endian uint16, 2048 x 2048, row 0 = image top (north). TextAsset (.bytes): read with '
                    'TextAsset.bytes in the far-hills/ridge mesh generator. EditorOnly. Scale: 500 m x 500 m, 200 m vertical.'))
    meta = {'source': 'ambientCG Terrain003 (CC0)', 'size_px': [2048, 2048], 'extent_m': [500, 500], 'height_range_m': 200,
            'row0': 'top/north', 'files': [f'{base}/T_Terrain003_H.png', f'{base}/Terrain003_height_2048_r16.bytes'],
            'note': 'colour map dropped: it shows green vegetation (wrong biome); the ridge uses SG_Backdrop.'}
    rel = f'{base}/Terrain003_heightmap.json'
    json.dump(meta, open(out_path(rel), 'w'), indent=1)
    rows.append(row('TEX_ACG_TERRAIN003', rel, 'data_json', 'Heightmap metadata (TextAsset). EditorOnly.'))
    # contact: hillshade
    gy, gx = np.gradient(hgt * 200.0, 500.0 / 2048)
    nn = np.stack([-gx, gy, np.ones_like(gx)], 2)
    nn /= np.linalg.norm(nn, axis=2, keepdims=True)
    shade = np.clip((nn * np.array([-0.5, 0.5, 0.7])).sum(2), 0, 1)
    vis = np.stack([shade * 0.8 + hgt * 0.2] * 3, 2)
    sheet('TEX_ACG_TERRAIN003  heightmap (hillshade)', vis, T.encode_normal(nn), np.stack([hgt * 0, hgt * 0 + 1, hgt, hgt * 0], 2),
          os.path.join(RAW, '_contact', 'TEX_ACG_TERRAIN003.jpg'), ['500 m x 500 m, 0-200 m', 'M.B shows raw height'])
    return rows


def fbm(size, octaves, base_cells, seed, persistence=0.55):
    """Tileable value-noise fBm via bicubic upsampling of random lattices (wraps exactly)."""
    import cv2
    rng = np.random.default_rng(seed)
    acc = np.zeros((size, size), np.float32)
    amp, tot = 1.0, 0.0
    cells = base_cells
    for _ in range(octaves):
        lat = rng.random((cells, cells)).astype(np.float32)
        pad = np.pad(lat, 3, mode='wrap')
        scale = size / cells
        up = cv2.resize(pad, (int(round(pad.shape[1] * scale)), int(round(pad.shape[0] * scale))), interpolation=cv2.INTER_CUBIC)
        o = int(round(3 * scale))
        acc += amp * up[o:o + size, o:o + size]
        tot += amp
        amp *= persistence
        cells *= 2
        if cells > size // 2:
            break
    return acc / tot


def do_shared():
    rows = []
    base = 'Assets/_Game/Art/Textures/Env/_Shared'
    # macro variation (detail albedo x2, neutral = sRGB 0.5)
    n1 = fbm(1024, 6, 4, 11)
    n2 = fbm(1024, 4, 3, 23)
    n1 = (n1 - n1.mean()) / (n1.std() + 1e-6)
    n2 = (n2 - n2.mean()) / (n2.std() + 1e-6)
    lum = 0.5 + 0.07 * n1
    warm = 0.012 * n2
    rgb = np.stack([lum + warm, lum, lum - warm * 1.5], 2)
    rel = f'{base}/T_MacroVariation_BC.png'; T.save8(np.clip(rgb, 0, 1), out_path(rel))
    rows.append(row('TEX_SAND_01', rel, 'texture_detail_albedo',
                    'Shared macro-variation map for large grounds/walls: URP/Lit Detail Albedo x2 (sRGB 0.5 = neutral, +-0.07 luminance, '
                    'faint warm/cool), tiling 1/25 m (world UV) to break texture repetition. Texture Type Default, sRGB ON, Max Size 512, '
                    'DXT1 Crunched q55, wrap Repeat. Pair with a flat/no detail normal.'))
    # detail normals (high-passed, tileable)
    for mid, src_id, name, sig in (('TEX_SANDSTONE_BLOCKS_05', 'rock_face_03', 'DetailRock', 10.0),
                                   ('TEX_CONCRETE_WALL_007', 'white_rough_plaster', 'DetailPlaster', 10.0)):
        p = os.path.join(READY, f'Assets/_Game/Art/Textures/Env/{src_id}')
        nm = glob.glob(os.path.join(p, 'T_*_N.png'))[0]
        n = T.load(nm, 'RGB')
        hp = T.highpass_normal(n, sig)
        hp = T.resize_normal(hp, (512, 512))
        rel = f'{base}/T_{name}_N.png'; T.save8(hp, out_path(rel))
        rows.append(row(mid, rel, 'texture_detail_normal',
                        f'Shared tiling detail normal (high-passed {src_id}) for close-ups: URP/Lit Detail Normal Map, scale 0.6-1.0, '
                        'tiling 1/0.6 m; Normal map, OpenGL, sRGB OFF, Max Size 512, DXT5nm Crunched q60, wrap Repeat.'))
    flat_m = np.stack([np.zeros((512, 512)), np.ones((512, 512)), np.zeros((512, 512)), np.full((512, 512), 0.4)], 2)
    sheet('SHARED  T_MacroVariation_BC + T_DetailRock_N', T.resize(np.clip(rgb, 0, 1), (512, 512)), T.load(os.path.join(READY, base, 'T_DetailRock_N.png'), 'RGB'),
          flat_m, os.path.join(RAW, '_contact', 'SHARED_DETAIL.jpg'), ['macro variation (left) and rock detail normal (relit)'])
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--only', default='decals,lens,terrain,shared')
    a = ap.parse_args()
    rows = []
    for k in a.only.split(','):
        rows += {'decals': do_decals, 'lens': do_lens, 'terrain': do_terrain, 'shared': do_shared}[k]()
        print('done', k)
    files = {r['file'] for r in rows}
    idx = os.path.join(READY, 'import_index_cc0.json')
    cur = json.load(open(idx)) if os.path.exists(idx) else []
    cur = [c for c in cur if c['file'] not in files]
    cur.extend(rows)
    cur.sort(key=lambda r: (r['manifest_id'], r['file']))
    json.dump(cur, open(idx, 'w'), indent=1)
    print(len(rows), 'rows')


if __name__ == '__main__':
    main()
