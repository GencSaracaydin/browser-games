#!/usr/bin/env python3
"""WP10_CC0: texture packing + import index + material specs for the processed Poly Haven props.

  Tools/.venv/bin/python -I Tools/tex/wp10_props_tex.py [--ids PH_BARREL_01,...]

Inputs: RawAssets/wp10/cc0/_reports/props/<ID>.json (from Tools/cc0/wp10_ph_model.py, run in Blender), the
source glTF material table, guardrail-cleaned base colours in <ID>/clean/ (Tools/tex/wp10_paintout.py) and
Cycles bakes in <ID>/bake/. Outputs T_<Name>[_<Set>]_{BC,N,M,E}.png in
RawAssets/wp10/ready/Assets/_Game/Art/Textures/Props/<asset>/ at the prop's budget size, rows (tool
"wp10_props") in ready/import_index_cc0.json and specs in ready/materials_cc0.json.
"""
import argparse, glob, json, os, sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wp10_texlib as T  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')
READY = os.path.join(ROOT, 'RawAssets', 'wp10', 'ready')
CFG = json.load(open(os.path.join(ROOT, 'Tools', 'cc0', 'wp10_props.json')))
AO_FIXED = []
HERO = {'PH_BINOCULARS', 'PH_FILMSTRIP_PROJECTOR_8MM', 'PH_LARGE_IRON_GATE', 'PH_ROCK_FACE_01'}


def camel(s):
    return ''.join(w[:1].upper() + w[1:] for w in s.replace('-', '_').split('_') if w)


def gl_materials(src_file):
    p = os.path.join(ROOT, src_file)
    if p.endswith('.fbx'):
        p = glob.glob(os.path.join(os.path.dirname(os.path.dirname(p)), 'gltf_1k', '*.gltf'))[0]
    j = json.load(open(p))
    base = os.path.dirname(p)

    def img(t):
        if not t:
            return None
        return os.path.join(base, j['images'][j['textures'][t['index']]['source']]['uri'])
    out = {}
    for m in j['materials']:
        if m['name'] in out:
            continue
        pbr = m.get('pbrMetallicRoughness', {})
        ext = m.get('extensions', {})
        out[m['name']] = {'bc': img(pbr.get('baseColorTexture')), 'bcf': pbr.get('baseColorFactor', [1, 1, 1, 1]),
                          'mr': img(pbr.get('metallicRoughnessTexture')), 'mf': pbr.get('metallicFactor', 1.0),
                          'rf': pbr.get('roughnessFactor', 1.0), 'n': img(m.get('normalTexture')),
                          'ao': img(m.get('occlusionTexture')), 'e': img(m.get('emissiveTexture')),
                          'ef': m.get('emissiveFactor', [0, 0, 0]),
                          'es': ext.get('KHR_materials_emissive_strength', {}).get('emissiveStrength', 1.0),
                          'alpha': m.get('alphaMode', 'OPAQUE'), 'transmission': 'KHR_materials_transmission' in ext}
    return out


def clean_or(mid, path):
    if not path:
        return None
    c = os.path.join(RAW, mid, 'clean', os.path.basename(path))
    return c if os.path.exists(c) else path


def pack_set(mid, bc_p, mr_p, n_p, e_p, size, out_dir, stem, ef=None, es=1.0):
    os.makedirs(out_dir, exist_ok=True)
    wh = (size, size)
    files = {}
    bc = T.load(bc_p, 'RGB')
    if CFG.get(mid, {}).get('devegetate'):
        bc, frac = T.devegetate(bc, thresh=0.008, grow_px=4)
        print('  devegetated', mid, round(frac, 4))
    bc = T.resize_srgb(bc, wh)
    files['BC'] = os.path.join(out_dir, f'{stem}_BC.png'); T.save8(bc, files['BC'])
    if n_p:
        n = T.resize_normal(T.load(n_p, 'RGB'), wh)
        files['N'] = os.path.join(out_dir, f'{stem}_N.png'); T.save8(n, files['N'])
    if mr_p:
        arm = T.resize(T.load(mr_p, 'RGB'), wh)
        ao = np.clip(arm[..., 0], 0, 1)
        if float(ao.mean()) < 0.05:
            # Poly Haven ships some ARM maps with an empty AO channel (metal_trash_can, office_notepads): use the
            # Cycles AO bake from Tools/cc0/wp10_ao_bake.py if present, else no occlusion (SSAO only)
            bk = os.path.join(RAW, mid, 'ao_bake', os.path.splitext(os.path.basename(mr_p))[0] + '_ao.png')
            ao = np.clip(T.resize(T.load_gray(bk), wh), 0, 1) if os.path.exists(bk) else np.ones_like(ao)
            AO_FIXED.append((mid, os.path.basename(mr_p), 'bake' if os.path.exists(bk) else 'ones'))
        m = T.pack_mask(np.clip(arm[..., 2], 0, 1), ao,
                        (1 - np.clip(arm[..., 1], 0, 1)) * float(CFG.get(mid, {}).get('smooth_scale', 1.0)))
        files['M'] = os.path.join(out_dir, f'{stem}_M.png'); T.save8(m, files['M'])
    if e_p:
        e = T.resize_srgb(T.load(e_p, 'RGB'), (min(512, size), min(512, size)))
        files['E'] = os.path.join(out_dir, f'{stem}_E.png'); T.save8(e, files['E'])
    stats = {'albedo_mean_srgb': [round(float(x), 3) for x in bc.reshape(-1, 3).mean(0)]}
    return files, stats


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ids')
    a = ap.parse_args()
    reps = sorted(glob.glob(os.path.join(RAW, '_reports', 'props', 'PH_*.json')))
    if a.ids:
        want = set(a.ids.split(','))
        reps = [r for r in reps if os.path.basename(r)[:-5] in want]
    rows, specs, done = [], [], []
    rel = lambda p: os.path.relpath(p, READY)
    for rp in reps:
        rep = json.load(open(rp))
        mid, name, asset = rep['manifest_id'], rep['name'], rep['asset']
        cfg = CFG[mid]
        size = cfg['tex']
        tex_dir = os.path.join(READY, 'Assets/_Game/Art/Textures/Props', asset)
        glm = gl_materials(rep['source_file'])
        sets = {}       # texture-set key -> files
        mat_specs = {}
        if rep.get('baked'):
            b = {k: os.path.join(ROOT, v) for k, v in rep['baked'].items()}
            files, st = pack_set(mid, b['bc'], b['arm'], b['n'], None, size, tex_dir, f'T_{name}')
            sets['__baked__'] = (files, st)
        for out in rep['outputs']:
            for un, gl_names in out['materials'].items():
                if un in mat_specs:
                    continue
                if cfg.get('no_default_skin') and un == f'M_{name}':
                    continue          # default skin not shipped (hazard art); skins below
                if '__baked__' in gl_names:
                    files, st = sets['__baked__']
                    g = {'alpha': 'OPAQUE', 'transmission': False, 'bcf': [1, 1, 1, 1], 'ef': [0, 0, 0], 'es': 1}
                else:
                    g = glm.get(gl_names[0]) or next(iter(glm.values()))
                    key = (g['bc'], g['mr'], g['n'], g['e'])
                    if key not in sets:
                        if g['bc'] is None:
                            sets[key] = ({}, {})
                        else:
                            # texture stem follows the first material that uses the set
                            stem = f'T_{name}' if un == f'M_{name}' else f'T_{name}_' + un.split(f'M_{name}_')[-1]
                            if any(v[0].get('BC', '').endswith(f'{stem}_BC.png') for v in sets.values() if v[0]):
                                stem += '2'
                            sets[key] = pack_set(mid, clean_or(mid, g['bc']), g['mr'], clean_or(mid, g['n']), g['e'], size, tex_dir, stem, g['ef'], g['es'])
                    files, st = sets[key]
                spec = {'manifest_id': mid, 'material': un, 'target_path': f'Assets/_Game/Art/Materials/Props/{un}.mat',
                        'shader': 'Universal Render Pipeline/Lit', 'workflow': 'Metallic', 'textures': {}, 'floats': {}, 'colors': {}}
                if files.get('BC'):
                    spec['textures']['_BaseMap'] = rel(files['BC'])
                if files.get('N'):
                    spec['textures']['_BumpMap'] = rel(files['N'])
                if files.get('M'):
                    spec['textures']['_MetallicGlossMap'] = rel(files['M'])
                    spec['textures']['_OcclusionMap'] = rel(files['M'])
                    spec['floats'].update({'_Smoothness': 1.0, '_SmoothnessTextureChannel': 0, '_Metallic': 1.0, '_OcclusionStrength': 1.0})
                else:
                    spec['floats'].update({'_Metallic': float(g.get('mf', 0.0) or 0.0), '_Smoothness': round(1 - float(g.get('rf', 0.5) or 0.5), 3)})
                bcf = g.get('bcf') or [1, 1, 1, 1]
                spec['colors']['_BaseColor'] = [round(float(x), 4) for x in bcf[:3]] + [1.0]
                if g.get('alpha') == 'BLEND' or g.get('transmission'):
                    spec['surface'] = 'Transparent'
                    spec['blend'] = 'Alpha'
                    spec['colors']['_BaseColor'][3] = 0.28
                    spec['floats']['_Smoothness'] = 0.95
                    spec['note'] = 'glass/film: Transparent, alpha 0.28, smoothness 0.95, receives shadows, no shadow casting (set renderer Cast Shadows Off for the glass submesh).'
                if files.get('E') or any(float(x) > 0 for x in (g.get('ef') or [0, 0, 0])):
                    ef = g.get('ef') or [1, 1, 1]
                    es = float(g.get('es', 1.0) or 1.0)
                    spec['emission'] = {'_EmissionMap': rel(files['E']) if files.get('E') else None,
                                        '_EmissionColor_linear': [round(float(c) * min(es, 8.0), 3) for c in ef],
                                        'note': 'Enable _EMISSION. Lamp off by default in daylight scenes; drive intensity from PracticalLight (glTF strength %.1f, capped at 8 for bloom threshold 1.0).' % es}
                mat_specs[un] = spec
        # Barrel_01 skins (default 'explosive' skin has hazard art: not shipped)
        if cfg.get('skins'):
            skdir = os.path.join(RAW, mid, 'skins')
            for sk in cfg['skins']:
                bc = glob.glob(os.path.join(skdir, f'*_{sk}_diff_1k.*'))[0]
                arm = glob.glob(os.path.join(skdir, f'*_{sk}_arm_1k.*'))[0]
                nn = glob.glob(os.path.join(skdir, f'*_{sk}_nor_gl_1k.*'))[0]
                files, st = pack_set(mid, bc, arm, nn, None, size, tex_dir, f'T_{name}_{camel(sk)}')
                un = f'M_{name}_{camel(sk)}'
                mat_specs[un] = {'manifest_id': mid, 'material': un, 'target_path': f'Assets/_Game/Art/Materials/Props/{un}.mat',
                                 'shader': 'Universal Render Pipeline/Lit', 'workflow': 'Metallic',
                                 'textures': {'_BaseMap': rel(files['BC']), '_BumpMap': rel(files['N']), '_MetallicGlossMap': rel(files['M']), '_OcclusionMap': rel(files['M'])},
                                 'floats': {'_Smoothness': 1.0, '_SmoothnessTextureChannel': 0, '_Metallic': 1.0, '_OcclusionStrength': 1.0},
                                 'colors': {'_BaseColor': [1, 1, 1, 1]}, 'assign_to_slot': f'M_{name}',
                                 'note': f'Skin for slot M_{name}: pick one of the skins per instance (no default skin ships).'}
                sets[('skin', sk)] = (files, st)
            mat_specs.pop(f'M_{name}', None)
        specs += list(mat_specs.values())
        # index rows: textures
        for key, (files, st) in sets.items():
            for suf, p in files.items():
                typ = {'BC': 'texture_basecolor', 'N': 'texture_normal', 'M': 'texture_mask', 'E': 'texture_emissive'}[suf]
                note = {
                    'BC': f'Default, sRGB ON, Max Size {size}, DXT1 Crunched q55, mips on, aniso 2, wrap Repeat.',
                    'N': f'Normal map, OpenGL (+Y), sRGB OFF, Max Size {size}, DXT5nm Crunched q60, aniso 2.',
                    'M': f'Default, sRGB OFF, Max Size {size}, DXT5 Crunched q60. Mask D4: R metallic, G AO, B 0, A smoothness; assign to _MetallicGlossMap AND _OcclusionMap.',
                    'E': 'Default, sRGB ON, Max Size 512, DXT1 Crunched q60, aniso 1. Emission map.'}[suf]
                rows.append({'manifest_id': mid, 'file': rel(p), 'target_path': rel(p), 'type': typ, 'import_notes': note, 'tool': 'wp10_props'})
        # index rows: models
        for out in rep['outputs']:
            lods = out['lod_tris']
            lm = cfg.get('lm')
            parts = out.get('parts') or {}
            n = (f"Model: Scale Factor 1, Convert Units ON (metres; Blender FBX 'FBX All' scaling), Bake Axis Conversion OFF "
                 f"(axes baked: root rotation 0,0,0, Y up, +Z forward), Normals Import, Tangents Calculate Mikktspace, "
                 f"Materials None (MaterialFactory assigns by slot name: {', '.join(sorted(out['materials']))}), "
                 f"Mesh Compression Medium, Read/Write off, Animation None, Rig None. Size (x,y,z) {out['size_unity_m']} m, "
                 f"pivot at the Poly Haven origin (base centre / mount point). ")
            if len(lods) > 1:
                n += (f"LODs in file: {'/'.join(map(str, lods))} tris (*_LOD0..{len(lods)-1} => Unity creates the LODGroup; "
                      f"suggested transitions 50/20/4 %); turn OFF Unity Mesh LOD generation for this file. ")
            else:
                n += f"LOD0 only: {lods[0]} tris" + (" (articulated/near-field: Unity Mesh LOD may be enabled if needed). " if parts else ". ")
            if parts:
                n += 'Children: ' + '; '.join(f"{k} pivot {v['pivot_unity']} size {v['size_unity']}" for k, v in parts.items()) + '. '
            n += ('Generate Lightmap UVs ON (large static prop, lightmapped). ' if lm else 'Generate Lightmap UVs OFF (light-probe GI, D8). ')
            if cfg.get('note'):
                n += cfg['note']
            rows.append({'manifest_id': mid, 'file': out['fbx'], 'target_path': out['fbx'], 'type': 'model_fbx', 'import_notes': n,
                         'tool': 'wp10_props', 'lod_tris': lods, 'size_m': out['size_unity_m']})
        done.append(mid)
        print(mid, name, 'sets', len(sets), 'mats', list(mat_specs), [o['lod_tris'] for o in rep['outputs']])
    for x in AO_FIXED:
        print('AO channel empty in source ARM -> replaced:', x)
    # merge into shared json files
    idx = os.path.join(READY, 'import_index_cc0.json')
    cur = json.load(open(idx)) if os.path.exists(idx) else []
    cur = [c for c in cur if not (c.get('tool') == 'wp10_props' and c['manifest_id'] in done)] + rows
    cur.sort(key=lambda r: (r['manifest_id'], r['file']))
    json.dump(cur, open(idx, 'w'), indent=1)
    mp = os.path.join(READY, 'materials_cc0.json')
    cm = json.load(open(mp)) if os.path.exists(mp) else []
    cm = [c for c in cm if not (c['manifest_id'] in done and c['target_path'].startswith('Assets/_Game/Art/Materials/Props/'))] + specs
    cm.sort(key=lambda r: (r['manifest_id'], r['material']))
    json.dump(cm, open(mp, 'w'), indent=1)


if __name__ == '__main__':
    main()
