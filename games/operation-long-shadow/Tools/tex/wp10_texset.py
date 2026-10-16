#!/usr/bin/env python3
"""WP10_CC0: CC0 texture sets -> import-ready T_<Name>_{BC,N,M[,H,A]} PNGs + material specs.

  Tools/.venv/bin/python -I Tools/tex/wp10_texset.py [--ids TEX_SAND_01,...] [--force]

Reads raw downloads from RawAssets/wp10/cc0/<MANIFEST_ID>/ (Tools/cc0/wp10_fetch.py) and writes
RawAssets/wp10/ready/Assets/_Game/Art/Textures/Env/<asset_id>/T_<Name>_<Suffix>.png, then updates
RawAssets/wp10/ready/import_index_cc0.json (rows tagged "tool": "wp10_texset") and
RawAssets/wp10/ready/materials_cc0.json (MaterialFactory specs).

Packing (TECH_PLAN D4): M = R metallic, G AO, B height (terrain layers only; 0 elsewhere), A smoothness
(1 - roughness). Normals stay OpenGL (+Y). Colour is resized in linear light, normals renormalised.
"""
import argparse, glob, json, os, re, sys, time
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wp10_texlib as T  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')
READY = os.path.join(ROOT, 'RawAssets', 'wp10', 'ready')
MANIFEST = os.path.join(ROOT, 'Docs', 'ASSET_MANIFEST.json')
WP09 = {'TEX_ACOUSTIC_TILE', 'TEX_ACG_PAINTEDPLASTER012'}

HERO_2K = {'sand_01', 'painted_plaster_wall', 'concrete_floor_worn_001'}
# Unity Terrain layers (TECH_PLAN 7.15): the mask doubles as the terrain mask map (B = height for blending)
TERRAIN = {'sand_01', 'rocky_trail_02', 'dry_ground_rocks', 'aerial_sand'}
PARALLAX = {'sand_01'}            # the only set that ships a separate _H (hero ground)
# large-area grounds: flatten low-frequency luminance blotches (>~0.1 tile) so the tiling grid does not read
# from the ridge/roofs; macro variation comes back from T_MacroVariation (detail albedo x2 at 20-40 m tiling)
GROUND_FLATTEN = {'sand_01': (0.6, 0.1), 'aerial_sand': (0.6, 0.06), 'dry_ground_rocks': (0.85, 0.06),
                  'rocky_trail_02': (0.5, 0.1), 'gravelly_sand': (0.5, 0.1), 'Ground097': (0.4, 0.1),
                  'road_damaged': (0.55, 0.05), 'mud_cracked_dry_03': (0.4, 0.1), 'floor_tiles_08': (0.45, 0.15)}  # (strength, sigma / tile)
# low-frequency chroma blotches (the dark-red cross in dry_ground_rocks reads as a 4 m grid even after the
# luminance flatten); AO of every GROUND_FLATTEN set gets the same low-frequency flatten as its colour
CHROMA_FLATTEN = {'dry_ground_rocks': (0.7, 0.06), 'aerial_sand': (0.5, 0.06), 'sand_01': (0.4, 0.1)}
# desert grounds: green leaves / grass blades in the scans repeat every tile and break the arid look
DEVEGETATE = {'sand_01', 'aerial_sand', 'dry_ground_rocks', 'rocky_trail_02', 'gravelly_sand', 'Ground097',
              'mud_cracked_dry_03', 'concrete_debris', 'road_damaged'}
# colour grades (wp10_texlib.grade): aerial_sand is mauve-grey next to the yellow-beige sand_01 / dry_ground_rocks
# terrain layers -> half-way palette match; CorrugatedSteel006A is a showroom-saturated green -> sun-faded/chalked
GRADE = {'aerial_sand': dict(target_srgb_mean=(0.652, 0.588, 0.436), amount=0.6),
         'CorrugatedSteel006A': dict(saturation=0.6, chalk=0.12)}
# clone-stamp repair (all maps, one offset) of green leaves/blades that are too big for the re-tint
CLONE_GREEN = {'sand_01'}
AO_FLOOR = (0.65, 0.8)            # mean AO below 0.65 -> rescaled to mean 0.8 (wp10_texlib.ao_floor)
# physical tile size for ambientCG sets (ambientCG metadata has none): judged from the texel content
ACG_TILE_M = {'Plaster001': 2.0, 'Terrazzo003': 1.0, 'MetalPlates013': 2.0, 'PaintedMetal006': 1.0,
              'SheetMetal001': 0.5, 'Fabric066': 0.5, 'Ground097': 4.0, 'Rocks017': 1.5,
              'Concrete042B': 2.0, 'PaintedPlaster006': 2.0, 'Bricks043': 2.0, 'CorrugatedSteel006A': 1.5,
              'CorrugatedSteel005': 1.5, 'Rope001': 0.5, 'PaintedMetal014': 1.0, 'Fabric044': 0.5,
              'Wicker010B': 0.5, 'Cardboard004': 0.5}


def camel(asset_id):
    parts = re.split(r'[_\s]+', asset_id)
    return ''.join(p[:1].upper() + p[1:] for p in parts if p)


def find(d, *pats):
    for p in pats:
        hits = sorted(glob.glob(os.path.join(d, p)))
        if hits:
            return hits[0]
    return None


def tile_from_notes(e):
    m = re.search(r'Tile ([0-9.]+) m', e.get('notes', ''))
    return round(float(m.group(1)), 3) if m else None


def gather_sources(e):
    d = os.path.join(RAW, e['id'])
    aid = e['model_or_asset_id']
    if e['source'] == 'polyhaven':
        s = {'bc': find(d, f'{aid}_diff_*.jpg', f'{aid}_diff_*.png'),
             'n': find(d, f'{aid}_nor_gl_*.jpg', f'{aid}_nor_gl_*.png'),
             'arm': find(d, f'{aid}_arm_*.jpg', f'{aid}_arm_*.png'),
             'disp': find(d, f'{aid}_disp_*.jpg', f'{aid}_disp_*.png'),
             'mask': find(d, f'{aid}_mask_*.jpg', f'{aid}_mask_*.png')}
    else:
        s = {'bc': find(d, '*_Color.jpg', '*_Color.png'), 'n': find(d, '*_NormalGL.jpg', '*_NormalGL.png'),
             'rough': find(d, '*_Roughness.jpg', '*_Roughness.png'),
             'ao': find(d, '*_AmbientOcclusion.jpg', '*_AmbientOcclusion.png'),
             'metal': find(d, '*_Metalness.jpg', '*_Metalness.png'),
             'opacity': find(d, '*_Opacity.jpg', '*_Opacity.png'),
             'disp': find(d, '*_Displacement.jpg', '*_Displacement.png')}
    return s


def process_set(e, force=False):
    aid = e['model_or_asset_id']
    name = camel(aid)
    size = 2048 if aid in HERO_2K else 1024
    out_rel = f'Assets/_Game/Art/Textures/Env/{aid}'
    out = os.path.join(READY, out_rel)
    os.makedirs(out, exist_ok=True)
    s = gather_sources(e)
    if not s['bc'] or not s['n']:
        raise RuntimeError(f'missing maps for {e["id"]}: {s}')
    wh = (size, size)
    bc = T.load(s['bc'])
    if bc.ndim == 2:
        bc = np.stack([bc] * 3, axis=2)
    bc = bc[..., :3]
    pre = {}
    clone = None
    if aid in CLONE_GREEN:
        gm = T.green_mask(bc)
        srcs = {'bc': bc, 'n': T.load(s['n'], 'RGB')}
        if s.get('arm'):
            srcs['arm'] = T.load(s['arm'], 'RGB')
        if s.get('disp'):
            srcs['disp'] = T.load_gray(s['disp'])
        fixed, off, frac = T.clone_fix(srcs, gm)
        bc = fixed['bc']
        pre = fixed
        clone = {'offset_frac': off, 'masked_frac': round(frac, 5)}
    veg = 0.0
    if aid in DEVEGETATE:
        bc, veg = T.devegetate(bc)
    if aid in GROUND_FLATTEN:
        bc = T.macro_flatten(bc, *GROUND_FLATTEN[aid])
    if aid in CHROMA_FLATTEN:
        bc = T.chroma_flatten(bc, *CHROMA_FLATTEN[aid])
    if aid in GRADE:
        bc = T.grade(bc, **GRADE[aid])
    has_alpha = False
    if s.get('opacity'):
        op = T.load_gray(s['opacity'])
        if op.shape != bc.shape[:2]:
            op = T.resize(op, (bc.shape[1], bc.shape[0]))
        bc = np.concatenate([bc, op[..., None]], axis=2)
        has_alpha = True
    bc = T.resize_srgb(bc, wh)
    n = T.resize_normal(pre['n'] if 'n' in pre else T.load(s['n'], 'RGB'), wh)
    if e['source'] == 'polyhaven':
        arm = T.resize(pre['arm'] if 'arm' in pre else T.load(s['arm'], 'RGB'), wh)
        ao, rough, metal = arm[..., 0], arm[..., 1], arm[..., 2]
    else:
        rough = T.resize(T.load_gray(s['rough']), wh)
        ao = T.resize(T.load_gray(s['ao']), wh) if s.get('ao') else np.ones(wh[::-1], np.float32)
        metal = T.resize(T.load_gray(s['metal']), wh) if s.get('metal') else np.zeros(wh[::-1], np.float32)
    ao_src_mean = float(ao.mean())
    if aid in GROUND_FLATTEN:
        ao = T.gray_flatten(np.clip(ao, 0, 1), *GROUND_FLATTEN[aid])
    ao, ao_k = T.ao_floor(np.clip(ao, 0, 1), *AO_FLOOR)
    height = None
    disp = None
    if s.get('disp'):
        disp = pre['disp'] if 'disp' in pre else T.load_gray(s['disp'])
        lo, hi = np.percentile(disp, 0.5), np.percentile(disp, 99.5)
        disp = np.clip((disp - lo) / max(hi - lo, 1e-4), 0, 1)
    if aid in TERRAIN and disp is not None:
        height = T.resize(disp, wh)
    smooth = 1.0 - np.clip(rough, 0, 1)
    m = T.pack_mask(np.clip(metal, 0, 1), np.clip(ao, 0, 1), smooth, height)
    files = {}
    p = os.path.join(out, f'T_{name}_BC.png'); T.save8(bc, p); files['BC'] = p
    p = os.path.join(out, f'T_{name}_N.png'); T.save8(n, p); files['N'] = p
    p = os.path.join(out, f'T_{name}_M.png'); T.save8(m, p); files['M'] = p
    if aid in PARALLAX and disp is not None:
        p = os.path.join(out, f'T_{name}_H.png'); T.save8(T.resize(disp, (1024, 1024)), p); files['H'] = p
    if e['source'] == 'polyhaven' and s.get('mask'):
        # Poly Haven 'Mask' = authoring mask (paint / rebar / weave); shipped as an aux mask for tint variation
        mk = T.resize(T.load_gray(s['mask']), (512, 512))
        p = os.path.join(out, f'T_{name}_A.png'); T.save8(mk, p); files['A'] = p
    # tile size
    tile = None
    info_p = os.path.join(RAW, e['id'], 'ph_info.json')
    tile = tile_from_notes(e)
    if tile is None and e['source'] == 'ambientcg':
        tile = ACG_TILE_M.get(aid, 2.0)
    stats = {'albedo_mean_srgb': [round(float(x), 3) for x in bc[..., :3].reshape(-1, 3).mean(0)],
             'metal_mean': round(float(metal.mean()), 3), 'smooth_mean': round(float(smooth.mean()), 3),
             'ao_mean': round(float(ao.mean()), 3), 'ao_mean_source': round(ao_src_mean, 3), 'ao_rescale_k': ao_k,
             'devegetated_frac': round(veg, 5), 'seam_score_bc': round(T.tile_seam_score(bc), 2),
             'seam_score_n': round(T.tile_seam_score(n), 2)}
    return {'manifest_id': e['id'], 'asset_id': aid, 'name': name, 'size': size, 'tile_m': tile,
            'terrain_layer': aid in TERRAIN, 'has_alpha': has_alpha, 'files': files, 'stats': stats,
            'priority': e['priority'], 'macro_flatten': GROUND_FLATTEN.get(aid), 'chroma_flatten': CHROMA_FLATTEN.get(aid), 'clone_fix': clone, 'grade': GRADE.get(aid), 'dropped': ['disp'] if (disp is not None and aid not in PARALLAX and aid not in TERRAIN) else []}


def index_rows(r):
    rows = []
    rel = lambda p: os.path.relpath(p, READY)
    ms = r['size']
    for suf, p in r['files'].items():
        if suf == 'BC':
            note = (f"Texture Type Default, sRGB ON, Max Size {ms}, DXT{'5' if r['has_alpha'] else '1'} Crunched q55, "
                    f"mips on, aniso {'8' if r['terrain_layer'] else '4'}, wrap Repeat."
                    + (" Alpha = opacity: Alpha Is Transparency OFF, use URP/Lit Alpha Clipping (cutoff 0.5)." if r['has_alpha'] else ''))
            typ = 'texture_basecolor'
        elif suf == 'N':
            note = f"Texture Type Normal map, OpenGL (+Y), sRGB OFF, Max Size {ms}, DXT5nm Crunched q60, wrap Repeat."
            typ = 'texture_normal'
        elif suf == 'M':
            note = (f"Texture Type Default, sRGB OFF, Max Size {ms}, DXT5 Crunched q60, wrap Repeat. Mask packing D4: "
                    "R metallic, G AO, A smoothness (1-roughness); assign to BOTH _MetallicGlossMap and _OcclusionMap, "
                    "Smoothness Source = Metallic Alpha, _Smoothness 1.0, _OcclusionStrength 1.0. "
                    + ("B = height (0..1) so this same texture is the Unity Terrain Layer mask map (R metal, G AO, B height, A smooth)."
                       if r['terrain_layer'] else "B unused (0)."))
            typ = 'texture_mask'
        elif suf == 'H':
            note = "Height for URP/Lit _ParallaxMap (hero ground only), sRGB OFF, Max Size 1024, DXT1 Crunched q55, parallax scale 0.01-0.02."
            typ = 'texture_height'
        else:
            note = "Aux authoring mask from Poly Haven (paint/feature mask), sRGB OFF, Max Size 512, DXT1 Crunched q55. Optional: tint/variation in a custom shader; not used by URP/Lit."
            typ = 'texture_aux'
        rows.append({'manifest_id': r['manifest_id'], 'file': rel(p), 'target_path': rel(p), 'type': typ,
                     'import_notes': note + f" Tile {r['tile_m']} m (world-UV tiling = 1/{r['tile_m']}).",
                     'tool': 'wp10_texset'})
    return rows


def material_spec(r):
    rel = lambda p: os.path.relpath(p, READY)
    f = r['files']
    spec = {'manifest_id': r['manifest_id'], 'material': f"M_{r['name']}",
            'target_path': f"Assets/_Game/Art/Materials/Env/M_{r['name']}.mat",
            'shader': 'Universal Render Pipeline/Lit', 'workflow': 'Metallic',
            'textures': {'_BaseMap': rel(f['BC']), '_BumpMap': rel(f['N']), '_MetallicGlossMap': rel(f['M']),
                         '_OcclusionMap': rel(f['M'])},
            'floats': {'_Smoothness': 1.0, '_SmoothnessTextureChannel': 0, '_Metallic': 1.0, '_BumpScale': 1.0,
                       '_OcclusionStrength': 1.0},
            'tile_m': r['tile_m'], 'world_uv_tiling': round(1.0 / r['tile_m'], 4) if r['tile_m'] else None,
            'alpha_clip': 0.5 if r['has_alpha'] else None, 'terrain_layer': r['terrain_layer']}
    if 'H' in f:
        spec['textures']['_ParallaxMap'] = rel(f['H'])
        spec['floats']['_Parallax'] = 0.012
    if r['terrain_layer']:
        spec['terrain_layer'] = {'asset': f"Assets/_Game/Art/Materials/Env/TL_{r['name']}.terrainlayer",
                                 'diffuse': rel(f['BC']), 'normal': rel(f['N']), 'mask': rel(f['M']),
                                 'tile_size_m': r['tile_m'], 'note': 'mask B = height: enable height-based blend'}
    return spec


def update_json(path, rows, key_tool=None, key_ids=None):
    cur = []
    if os.path.exists(path):
        cur = json.load(open(path))
    ids = {r['manifest_id'] for r in rows} if key_ids is None else key_ids
    if key_tool:
        cur = [c for c in cur if not (c.get('tool') == key_tool and c['manifest_id'] in ids)]
    else:
        cur = [c for c in cur if c['manifest_id'] not in ids]
    cur.extend(rows)
    cur.sort(key=lambda r: (r['manifest_id'], r.get('file', r.get('material', ''))))
    json.dump(cur, open(path, 'w'), indent=1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ids')
    ap.add_argument('--force', action='store_true')
    a = ap.parse_args()
    man = json.load(open(MANIFEST))
    ents = [e for e in man if e['category'] == 'texture' and e['source'] in ('polyhaven', 'ambientcg')
            and e['id'] not in WP09 and e['id'] not in ('TEX_ACG_TERRAIN003', 'TEX_LENS_DIRT')]
    if a.ids:
        want = set(a.ids.split(','))
        ents = [e for e in ents if e['id'] in want]
    results, rows, specs = [], [], []
    for e in ents:
        t0 = time.time()
        try:
            r = process_set(e, a.force)
        except Exception as ex:
            print('FAIL', e['id'], ex)
            continue
        results.append(r)
        rows += index_rows(r)
        specs.append(material_spec(r))
        print(f"{e['id']}: {r['name']} {r['size']} tile {r['tile_m']} seams bc {r['stats']['seam_score_bc']} "
              f"n {r['stats']['seam_score_n']} albedo {r['stats']['albedo_mean_srgb']} {time.time() - t0:.1f}s", flush=True)
    os.makedirs(READY, exist_ok=True)
    update_json(os.path.join(READY, 'import_index_cc0.json'), rows, key_tool='wp10_texset',
                key_ids={r['manifest_id'] for r in results})
    update_json(os.path.join(READY, 'materials_cc0.json'), specs)
    rep = os.path.join(RAW, '_reports', 'texsets.json')
    os.makedirs(os.path.dirname(rep), exist_ok=True)
    old = {}
    if os.path.exists(rep):
        old = {x['manifest_id']: x for x in json.load(open(rep))}
    for r in results:
        old[r['manifest_id']] = r
    json.dump(sorted(old.values(), key=lambda x: x['manifest_id']), open(rep, 'w'), indent=1)


if __name__ == '__main__':
    main()
