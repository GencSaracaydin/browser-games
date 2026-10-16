"""WP10_CC0 Poly Haven prop processor (runs INSIDE Blender 5.2, headless).

  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -t 2 -P Tools/cc0/wp10_ph_model.py -- \
      --ids PH_BARREL_01,PH_AMMO_BOX [--config Tools/cc0/wp10_props.json]

Per manifest id (config in Tools/cc0/wp10_props.json):
  import glTF 1k (or FBX) -> bake node transforms -> optional scale -> drop nodes (graffiti) -> split variants /
  parts -> merge split vertices -> collapse-decimate to the LOD0 budget (normals re-derived by angle) ->
  LOD1/LOD2 (Unity *_LODn naming => automatic LODGroup) -> rename materials to M_<Name>[_<Part>] ->
  FBX export (metres, Unity axes: -Z forward / Y up, transforms baked, scale 1) -> re-import check.
  'bake' items: decimated low poly gets fresh UVs and Cycles-baked base colour / normal / ARM from the full scan.
Writes RawAssets/wp10/ready/Assets/_Game/Art/Models/Props/<asset>/SM_<Name>[ _<Variant>].fbx and
RawAssets/wp10/cc0/_reports/props/<ID>.json (consumed by Tools/tex/wp10_props_tex.py).
"""
import bpy, bmesh, json, math, mathutils, os, sys, time

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')
READY = os.path.join(ROOT, 'RawAssets', 'wp10', 'ready')
REP = os.path.join(RAW, '_reports', 'props')
DEF_LODS = [1.0, 0.5, 0.25]


def argv():
    a = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    out = {'ids': None, 'config': os.path.join(ROOT, 'Tools', 'cc0', 'wp10_props.json')}
    for i, k in enumerate(a):
        if k == '--ids':
            out['ids'] = a[i + 1].split(',')
        if k == '--config':
            out['config'] = a[i + 1]
    return out


def tris_of(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def bbox(objs):
    mn = mathutils.Vector((1e9,) * 3)
    mx = -mn
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            mn = mathutils.Vector(map(min, mn, w))
            mx = mathutils.Vector(map(max, mx, w))
    return mn, mx


def select(objs, active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


def join(objs, name):
    objs = [o for o in objs if o.type == 'MESH']
    if not objs:
        return None
    select(objs, objs[0])
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    o.data.name = name
    return o


def set_origin(o, point):
    """Move the object origin to world 'point' without moving the geometry."""
    d = point - o.location
    o.data.transform(mathutils.Matrix.Translation(-d))
    o.location = point


def decimate(o, ratio):
    if ratio >= 0.995:
        return False
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.to_mesh(o.data)
    bm.free()
    select([o])
    try:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    except Exception:
        pass
    m = o.modifiers.new('dec', 'DECIMATE')
    m.decimate_type = 'COLLAPSE'
    m.ratio = max(0.01, ratio)
    m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40), keep_sharp_edges=True)
    return True


def triangulate(o):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.to_mesh(o.data)
    bm.free()


def match(name, pats):
    for p in pats:
        if p.startswith('_'):
            if name.endswith(p) or (p + '_') in name:
                return True
        elif name == p:
            return True
    return False


def base_name(n):
    return n.split('.')[0] if '.' in n and n.rsplit('.', 1)[1].isdigit() else n


def unity_mat_name(gl_name, asset, prop_name):
    s = gl_name
    for pre in (asset + '_', asset.lower() + '_'):
        if s.lower().startswith(pre.lower()):
            s = s[len(pre):]
            break
    else:
        if s.lower() == asset.lower():
            s = ''
    if s.lower() in ('', asset.lower(), 'body', 'm_01', 'main'):
        return f'M_{prop_name}'
    return f'M_{prop_name}_' + ''.join(w[:1].upper() + w[1:] for w in s.replace('-', '_').split('_') if w)


def bake_low(high_objs, low, size, out_dir, prefix):
    """Cycles bake from the full scan (high, own materials) onto the decimated low (fresh UVs)."""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 8
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = 0.04
    sc.render.bake.max_ray_distance = 0.15
    sc.render.bake.margin = 8
    select([low])
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003)
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    mat = bpy.data.materials.new('BAKE_' + prefix)
    mat.use_nodes = True
    low.data.materials.clear()
    low.data.materials.append(mat)
    nt = mat.node_tree
    results = {}
    for key, btype in (('bc', 'DIFFUSE'), ('n', 'NORMAL'), ('arm', 'EMIT')):
        img = bpy.data.images.new(f'{prefix}_{key}', size, size, alpha=False, float_buffer=(key == 'n'))
        img.colorspace_settings.name = 'sRGB' if key == 'bc' else 'Non-Color'
        tn = nt.nodes.new('ShaderNodeTexImage')
        tn.image = img
        for n in nt.nodes:
            n.select = False
        tn.select = True
        nt.nodes.active = tn
        # high materials: for the ARM pass route the glTF ORM image into emission
        swaps = []
        if key == 'arm':
            for ho in high_objs:
                for slot in ho.material_slots:
                    m = slot.material
                    if not m or not m.use_nodes:
                        continue
                    bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
                    src = None
                    for n in m.node_tree.nodes:
                        if n.type == 'TEX_IMAGE' and n.image and ('arm' in n.image.name.lower() or 'metallic' in n.image.name.lower() or 'rough' in n.image.name.lower()):
                            src = n
                    out = next((n for n in m.node_tree.nodes if n.type == 'OUTPUT_MATERIAL'), None)
                    if src and out:
                        em = m.node_tree.nodes.new('ShaderNodeEmission')
                        m.node_tree.links.new(src.outputs['Color'], em.inputs['Color'])
                        old = out.inputs['Surface'].links[0].from_socket if out.inputs['Surface'].links else None
                        m.node_tree.links.new(em.outputs['Emission'], out.inputs['Surface'])
                        swaps.append((m, em, old, out))
        select(high_objs + [low], low)
        kw = {'type': btype}
        if btype == 'DIFFUSE':
            kw['pass_filter'] = {'COLOR'}
        if btype == 'NORMAL':
            kw['normal_space'] = 'TANGENT'
        t0 = time.time()
        bpy.ops.object.bake(**kw)
        for m, em, old, out in swaps:
            if old:
                m.node_tree.links.new(old, out.inputs['Surface'])
            m.node_tree.nodes.remove(em)
        p = os.path.join(out_dir, f'{prefix}_bake_{key}.png')
        img.filepath_raw = p
        img.file_format = 'PNG'
        if key == 'n':
            img.file_format = 'PNG'
        img.save()
        results[key] = p
        print('  baked', key, round(time.time() - t0, 1), 's')
    return results, mat


def make_straight_ladder(name, length=3.6, width=0.47, rung_step=0.3):
    """Procedural straight ladder UV-mapped into the step-ladder atlas (long clean grain strips)."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new('UVMap')

    def zbox(sx, sy, sz, uvrect, xf):
        x0, x1, y0, y1, z0, z1 = -sx / 2, sx / 2, -sy / 2, sy / 2, 0.0, sz
        P = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        vs = [bm.verts.new(xf @ mathutils.Vector(p)) for p in P]
        faces = [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7), (3, 2, 1, 0), (4, 5, 6, 7)]
        u0, v0, u1, v1 = uvrect
        for fi, f in enumerate(faces):
            face = bm.faces.new([vs[i] for i in f])
            for li, loop in enumerate(face.loops):
                if fi < 4:
                    sv = 0.0 if li in (0, 3) else 1.0
                    tv = 0.0 if li in (0, 1) else 1.0
                    loop[uv].uv = (u0 + (u1 - u0) * sv, v0 + (v1 - v0) * tv)
                else:
                    loop[uv].uv = (u0 + (u1 - u0) * (0.3 + 0.4 * (li in (1, 2))), v0 + (v1 - v0) * 0.02 * (li in (2, 3)))
    rail_w, rail_d = 0.045, 0.07
    for i, x in enumerate((-width / 2 + rail_w / 2, width / 2 - rail_w / 2)):
        rect = (0.515, 0.03, 0.56, 0.88) if i == 0 else (0.635, 0.05, 0.68, 0.90)
        zbox(rail_w, rail_d, length, rect, mathutils.Matrix.Translation((x, 0, 0)))
    n = int((length - 0.25) / rung_step)
    inner = width - 2 * rail_w + 0.02
    rot = mathutils.Matrix.Rotation(math.radians(90), 4, 'Y')
    for k in range(n):
        z = 0.28 + k * rung_step
        xf = mathutils.Matrix.Translation((-inner / 2, 0, z)) @ rot
        u = 0.02 + 0.03 * (k % 3)
        zbox(0.034, 0.034, inner, (u, 0.36, u + 0.03, 0.62), xf)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def merge_slots(o):
    """Collapse material slots that resolve to the same Unity material name."""
    names = [s.material.name if s.material else None for s in o.material_slots]
    uniq = []
    for n in names:
        if n not in uniq:
            uniq.append(n)
    if len(uniq) == len(names):
        return
    remap = [uniq.index(n) for n in names]
    mats = [bpy.data.materials.get(n) for n in uniq]
    for p in o.data.polygons:
        p.material_index = remap[p.material_index]
    o.data.materials.clear()
    for m in mats:
        o.data.materials.append(m)


def export_fbx(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    select(objs)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True, mesh_smooth_type='OFF',
                             use_tspace=False, add_leaf_bones=False, bake_anim=False, path_mode='STRIP',
                             embed_textures=False, use_custom_props=False, use_triangles=True)


def verify_fbx(path):
    """Re-import into an empty scene: report bbox (Blender Z-up metres) and tris per object."""
    keep = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in keep]
    meshes = [o for o in new if o.type == 'MESH']
    mn, mx = bbox(meshes)
    info = {'objects': {o.name: tris_of(o) for o in meshes}, 'dims_m': [round(v, 3) for v in (mx - mn)],
            'scales': sorted({tuple(round(s, 3) for s in o.matrix_world.to_scale()) for o in meshes})}
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    return info


def process(mid, cfg):
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    asset = cfg.get('asset')
    srcdir = os.path.join(RAW, cfg.get('src', mid))
    if cfg.get('src_fbx'):
        import glob
        f = glob.glob(os.path.join(srcdir, 'fbx_1k', '*.fbx'))[0]
        bpy.ops.import_scene.fbx(filepath=f)
    else:
        import glob
        f = glob.glob(os.path.join(srcdir, 'gltf_1k', '*.gltf'))[0]
        bpy.ops.import_scene.gltf(filepath=f, import_shading='NORMALS')
    if not asset:
        asset = os.path.basename(f).replace('_1k.gltf', '').replace('_1k.fbx', '')
    name = cfg['name']
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    # bake hierarchy transforms into world-space vertices
    for o in meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
        if o.data.users > 1:
            o.data = o.data.copy()     # instanced meshes (e.g. two crates sharing data)
    select(meshes)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)
    sc = cfg.get('scale', 1.0)
    if abs(sc - 1.0) > 1e-6:
        for o in meshes:
            o.data.transform(mathutils.Matrix.Scale(sc, 4))
    for pat in cfg.get('drop', []):
        for o in list(meshes):
            if pat in o.name:
                meshes.remove(o)
                bpy.data.objects.remove(o, do_unlink=True)
    src_tris = sum(tris_of(o) for o in meshes)
    # glTF material order (to map Blender materials back to the source definitions)
    rep = {'manifest_id': mid, 'asset': asset, 'name': name, 'source_file': os.path.relpath(f, ROOT), 'source_tris': src_tris,
           'scale': sc, 'outputs': [], 'notes': cfg.get('note', '')}
    variants = cfg.get('variants') or {'': None}
    asset_dir = asset if mid not in ('PH_WOODENCHAIR_01', 'PH_DRAWER_CABINET') else asset
    model_dir = os.path.join(READY, 'Assets/_Game/Art/Models/Props', asset_dir)
    groups = {vk: [o for o in meshes if (vp is None or match(base_name(o.name), vp))] for vk, vp in variants.items()}
    n_src_mats = len({base_name(s.material.name) for o in meshes for s in o.material_slots if s.material})
    for vkey, vpats in variants.items():
        vobjs = groups[vkey]
        if not vobjs:
            print('  !! no objects for variant', vkey)
            continue
        vname = name + (('_' + vkey) if vkey else '')
        # material rename (per variant; shared material names stay shared)
        mats = {}
        for o in vobjs:
            for slot in o.material_slots:
                m = slot.material
                if m is None:
                    continue
                un = f'M_{name}' if n_src_mats == 1 else unity_mat_name(base_name(m.name), asset, name)
                mats.setdefault(un, set()).add(base_name(m.name))
                m['unity_name'] = un
        # parts
        parts_cfg = cfg.get('parts') or {}
        part_objs = {}
        rest = list(vobjs)
        for pname, pats in parts_cfg.items():
            sel = [o for o in rest if base_name(o.name) in pats]
            if sel:
                part_objs[pname] = sel
                rest = [o for o in rest if o not in sel]
        outs = []
        if cfg.get('items'):
            for o in rest:
                short = base_name(o.name).replace(asset + '_', '')
                nm = f'{vname}_' + ''.join(w[:1].upper() + w[1:] for w in short.split('_'))
                o.name = nm
                mn, mx = bbox([o])
                set_origin(o, mathutils.Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z)))
                outs.append(('item', o))
        else:
            body = join(rest, vname + '_Body') if rest else None
            if body:
                outs.append(('body', body))
            for pname, objs in part_objs.items():
                po = join(objs, pname)
                mn, mx = bbox([po])
                piv = cfg.get('part_pivot', 'center')
                if piv == 'hinge':
                    bmn, bmx = bbox([body]) if body else (mn, mx)
                    cx = (bmn.x + bmx.x) / 2
                    hx = mn.x if (mn.x + mx.x) / 2 < cx else mx.x
                    pt = mathutils.Vector((hx, (mn.y + mx.y) / 2, mn.z))
                elif piv == 'origin':
                    pt = mathutils.Vector((0, 0, 0))
                else:
                    pt = (mn + mx) / 2
                set_origin(po, pt)
                outs.append(('part', po))
        # variant pivot: move everything so the chosen pivot is at the world origin
        allo = [o for _, o in outs]
        pv = cfg.get('pivot', 'origin')
        if pv in ('bottom', 'top') or vkey:
            mn, mx = bbox(allo)
            off = mathutils.Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z if pv != 'top' else mx.z))
            for o in allo:
                o.location -= off
            bpy.context.view_layer.update()
        # decimate to budget (LOD0)
        tot = sum(tris_of(o) for o in allo)
        target = cfg['tris']
        ratio = min(1.0, target / max(tot, 1))
        if cfg.get('bake'):
            high = [o for o in allo]
            lows = []
            for o in high:
                lo = o.copy()
                lo.data = o.data.copy()
                bpy.context.scene.collection.objects.link(lo)
                decimate(lo, ratio)
                lows.append(lo)
            low = join(lows, vname + '_Low')
            o_dir = os.path.join(RAW, mid, 'bake')
            os.makedirs(o_dir, exist_ok=True)
            baked, bmat = bake_low(high, low, 1024, o_dir, name)
            bmat.name = f'M_{name}'
            bmat['unity_name'] = f'M_{name}'
            for o in high:
                bpy.data.objects.remove(o, do_unlink=True)
            low.name = vname + '_Body'
            outs = [('body', low)]
            allo = [low]
            rep['baked'] = {k: os.path.relpath(v, ROOT) for k, v in baked.items()}
            mats = {f'M_{name}': {'__baked__'}}
        else:
            for o in allo:
                decimate(o, ratio)
        canon = {}
        for o in allo:
            triangulate(o)
            for slot in o.material_slots:
                m = slot.material
                if m is not None and 'unity_name' in m:
                    un = m['unity_name']
                    if un not in canon:
                        canon[un] = m
                        m.name = un
                    slot.material = canon[un]
            merge_slots(o)
        # LODs (only for single-body outputs; articulated props keep LOD0)
        lods = cfg.get('lods', DEF_LODS)
        has_parts = any(k == 'part' for k, _ in outs) or cfg.get('items')
        lod_objs = []
        if not has_parts and len(lods) > 1 and sum(tris_of(o) for o in allo) > 1500:
            body = allo[0]
            body.name = f'SM_{vname}_LOD0'
            lod_objs.append(body)
            for li, r in enumerate(lods[1:], start=1):
                c = body.copy()
                c.data = body.data.copy()
                bpy.context.scene.collection.objects.link(c)
                c.name = f'SM_{vname}_LOD{li}'
                decimate(c, r)
                triangulate(c)
                lod_objs.append(c)
            export_objs = lod_objs
        else:
            for k, o in outs:
                if k == 'body':
                    o.name = f'SM_{vname}' if not has_parts else 'Body'
            export_objs = allo
        fbx = os.path.join(model_dir, f'SM_{vname}.fbx')
        export_fbx(export_objs, fbx)
        ver = verify_fbx(fbx)
        parts_info = {}
        for k, o in outs:
            if k in ('part', 'item'):
                mn, mx = bbox([o])
                d = mx - mn
                # Unity axes: X = Blender X (mirrored), Y = Blender Z, Z = -Blender Y
                parts_info[o.name] = {'pivot_blender': [round(v, 4) for v in o.location],
                                      'pivot_unity': [round(-o.location.x, 4), round(o.location.z, 4), round(-o.location.y, 4)],
                                      'size_unity': [round(d.x, 3), round(d.z, 3), round(d.y, 3)], 'tris': tris_of(o)}
        mn, mx = bbox(export_objs[:1] if lod_objs else export_objs)
        rep['outputs'].append({'variant': vkey, 'fbx': os.path.relpath(fbx, READY), 'unity_name': f'SM_{vname}',
                               'lod_tris': [tris_of(o) for o in lod_objs] if lod_objs else [sum(tris_of(o) for o in allo)],
                               'objects': [o.name for o in export_objs], 'parts': parts_info,
                               'materials': {k: sorted(v) for k, v in mats.items()},
                               'size_unity_m': [round(mx.x - mn.x, 3), round(mx.z - mn.z, 3), round(mx.y - mn.y, 3)],
                               'bottom_y_unity': round(mn.z, 3), 'verify': ver})
        for o in list(bpy.context.scene.objects):
            if o in export_objs:
                bpy.data.objects.remove(o, do_unlink=True)
    if cfg.get('extra') == 'straight_ladder':
        lad = make_straight_ladder('SM_WoodenLadderStraight')
        m = bpy.data.materials.get('M_WoodenStepLadder') or bpy.data.materials.new('M_WoodenStepLadder')
        lad.data.materials.append(m)
        fbx = os.path.join(model_dir, 'SM_WoodenLadderStraight.fbx')
        export_fbx([lad], fbx)
        mn, mx = bbox([lad])
        rep['outputs'].append({'variant': 'Straight', 'fbx': os.path.relpath(fbx, READY), 'unity_name': 'SM_WoodenLadderStraight',
                               'lod_tris': [tris_of(lad)], 'objects': [lad.name], 'parts': {},
                               'materials': {'M_WoodenStepLadder': ['wooden_ladder']},
                               'size_unity_m': [round(mx.x - mn.x, 3), round(mx.z - mn.z, 3), round(mx.y - mn.y, 3)],
                               'bottom_y_unity': 0.0, 'verify': verify_fbx(fbx), 'procedural': True})
    rep['wall_s'] = round(time.time() - t0, 1)
    os.makedirs(REP, exist_ok=True)
    json.dump(rep, open(os.path.join(REP, mid + '.json'), 'w'), indent=1)
    print('DONE', mid, [(o['unity_name'], o['lod_tris'], o['size_unity_m']) for o in rep['outputs']], rep['wall_s'], 's')


def main():
    a = argv()
    cfg = json.load(open(a['config']))
    ids = a['ids'] or [k for k in cfg if not k.startswith('_')]
    for mid in ids:
        try:
            process(mid, cfg[mid])
        except Exception as ex:
            import traceback
            traceback.print_exc()
            print('FAIL', mid, ex)


main()
