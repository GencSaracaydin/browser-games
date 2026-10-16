#!/usr/bin/env python3
"""WP10_CC0: MaterialFactory specs for every decal set in ready/import_index_cc0.json (URP Decal Shader Graph).

  Tools/.venv/bin/python -I Tools/tex/wp10_decal_specs.py

Writes/replaces the DCL_* rows of RawAssets/wp10/ready/materials_cc0.json (target Assets/_Game/Art/Materials/
Decals/M_Decal_<Name>.mat). The default URP decal graph (Shader Graphs/Decal) takes Base Map, Normal Map and
Normal Blend; the _M mask is only for a custom MAOS decal graph (listed under 'optional').
"""
import json, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
READY = os.path.join(ROOT, 'RawAssets', 'wp10', 'ready')
# per set: normal blend, affect normal, projector size (w, h, depth m), extra
PER = {
    'AsphaltDamage': (0.8, True, (2.0, 2.0, 0.3), 'atlas of pothole/crack shapes: pick a sub-rect per projector (UV scale/offset).'),
    'TireTracks': (0.7, True, (1.8, 0.45, 0.3), 'chain projectors along the track; sand-tinted, fades at the ends.'),
    'Leaks': (0.5, True, (1.0, 1.5, 0.3), 'atlas rows: Leaking004 grime / Leaking007 rust / Leaking009 grime (see texture notes).'),
    'Scratches': (0.3, False, (0.5, 0.5, 0.1), 'low opacity (Base Color alpha 0.3-0.5).'),
    'Fingerprints': (0.0, False, (0.4, 0.3, 0.05), 'CRT glass smudges: drive smoothness down (custom graph) or low-opacity base.'),
    'Tape': (0.6, True, (0.3, 0.6, 0.05), 'atlas of 5 duct-tape strips.'),
    'CompanyLettering': (0.5, True, (16.0, 4.0, 0.4), 'Relay Hall facade; two rows in the atlas (row A / row B), one projector each (UV offset y 0.5 / 0).'),
    'StandClearRing': (0.0, False, (5.6, 5.6, 1.0), 'tint Base Color with the objective colour; emission = same colour x0.5; hide on completion.'),
}


def main():
    idx = json.load(open(os.path.join(READY, 'import_index_cc0.json')))
    sets = {}
    for r in idx:
        m = re.match(r'Assets/_Game/Art/Textures/Decals/[^/]+/T_Decal_(\w+)_(BC|N|M)\.png$', r['file'])
        if m:
            sets.setdefault((r['manifest_id'], m.group(1)), {})[m.group(2)] = r['file']
    specs = []
    for (mid, name), f in sorted(sets.items()):
        nb, an, size, extra = PER.get(name, (0.6, True, (1.0, 1.0, 0.3), ''))
        sp = {'manifest_id': mid, 'material': f'M_Decal_{name}',
              'target_path': f'Assets/_Game/Art/Materials/Decals/M_Decal_{name}.mat',
              'shader': 'Shader Graphs/Decal', 'workflow': 'URP Decal (Screen Space or DBuffer per renderer feature)',
              'textures': {'Base_Map': f['BC']}, 'floats': {'Normal_Blend': nb if (an and 'N' in f) else 0.0},
              'toggles': {'Affect BaseColor': True, 'Affect Normal': bool(an and 'N' in f)},
              'colors': {'_BaseColor': [1, 1, 1, 1]},
              'projector': {'size_m': list(size[:2]), 'depth_m': size[2], 'pivot': 'center'}, 'note': extra}
        if an and 'N' in f:
            sp['textures']['Normal_Map'] = f['N']
        if 'M' in f:
            sp['optional'] = {'mask_MAOS': f['M'], 'note': 'custom MAOS decal graph only'}
        specs.append(sp)
    p = os.path.join(READY, 'materials_cc0.json')
    cur = [c for c in json.load(open(p)) if not c['manifest_id'].startswith('DCL_')] + specs
    cur.sort(key=lambda r: (r['manifest_id'], r['material']))
    json.dump(cur, open(p, 'w'), indent=1)
    print(len(specs), 'decal specs', [s['material'] for s in specs])


if __name__ == '__main__':
    main()
