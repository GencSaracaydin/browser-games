"""WP10_CC0 prop review renders (runs INSIDE Blender, Eevee, headless).

  Blender -b --factory-startup -t 2 -P Tools/cc0/wp10_prop_preview.py -- [--ids PH_BARREL_01,...]

Loads the SHIPPED files only: ready/.../SM_*.fbx + ready/materials_cc0.json (+ the packed T_* textures),
rebuilds each material the way URP/Lit reads it (BC sRGB, N OpenGL, M: R metal, G AO, A smoothness), lights it
with the sun-free kiara_8_sunset EXR + a low warm sun, and renders front-3/4, back-3/4 and each LOD.
PNG tiles go to RawAssets/wp10/cc0/_contact/props/<ID>__<variant>__<view>.png (assembled by wp10_prop_sheet.py).
"""
import bpy, json, math, mathutils, os, sys, glob

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')
READY = os.path.join(ROOT, 'RawAssets', 'wp10', 'ready')
OUT = os.path.join(RAW, '_contact', 'props')
EXR = os.path.join(READY, 'Assets/_Game/Art/HDRI/EditorOnly/kiara_8_sunset_nosun_2k.exr')


def args():
    a = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    return a[a.index('--ids') + 1].split(',') if '--ids' in a else None


def tex_node(nt, path, non_color):
    n = nt.nodes.new('ShaderNodeTexImage')
    n.image = bpy.data.images.load(os.path.join(READY, path), check_existing=True)
    n.image.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
    return n


def build_material(spec):
    m = bpy.data.materials.new('PRE_' + spec['material'])
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    t = spec.get('textures', {})
    col = spec.get('colors', {}).get('_BaseColor', [1, 1, 1, 1])
    if '_BaseMap' in t:
        bc = tex_node(nt, t['_BaseMap'], False)
        if '_MetallicGlossMap' in t:
            mk = tex_node(nt, t['_MetallicGlossMap'], True)
            sep = nt.nodes.new('ShaderNodeSeparateColor')
            nt.links.new(mk.outputs['Color'], sep.inputs['Color'])
            mul = nt.nodes.new('ShaderNodeMix')
            mul.data_type = 'RGBA'
            mul.blend_type = 'MULTIPLY'
            mul.inputs['Factor'].default_value = 1.0
            nt.links.new(bc.outputs['Color'], mul.inputs['A'])
            nt.links.new(sep.outputs['Green'], mul.inputs['B'])          # AO baked into the preview albedo
            nt.links.new(mul.outputs['Result'], bsdf.inputs['Base Color'])
            nt.links.new(sep.outputs['Red'], bsdf.inputs['Metallic'])
            inv = nt.nodes.new('ShaderNodeMath')
            inv.operation = 'SUBTRACT'
            inv.inputs[0].default_value = 1.0
            nt.links.new(mk.outputs['Alpha'], inv.inputs[1])
            nt.links.new(inv.outputs['Value'], bsdf.inputs['Roughness'])
        else:
            nt.links.new(bc.outputs['Color'], bsdf.inputs['Base Color'])
    else:
        bsdf.inputs['Base Color'].default_value = col[:3] + [1]
        bsdf.inputs['Metallic'].default_value = spec.get('floats', {}).get('_Metallic', 0)
        bsdf.inputs['Roughness'].default_value = 1 - spec.get('floats', {}).get('_Smoothness', 0.5)
    if '_BumpMap' in t:
        nm = tex_node(nt, t['_BumpMap'], True)
        nmap = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(nm.outputs['Color'], nmap.inputs['Color'])
        nt.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    if spec.get('surface') == 'Transparent':
        bsdf.inputs['Alpha'].default_value = col[3]
        bsdf.inputs['Roughness'].default_value = 0.05
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
    em = spec.get('emission')
    if em and em.get('_EmissionMap'):
        e = tex_node(nt, em['_EmissionMap'], False)
        nt.links.new(e.outputs['Color'], bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value = 0.6
    return m


def setup_world(sc):
    w = bpy.data.worlds.new('w')
    sc.world = w
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes['Background']
    if os.path.exists(EXR):
        env = nt.nodes.new('ShaderNodeTexEnvironment')
        env.image = bpy.data.images.load(EXR)
        nt.links.new(env.outputs['Color'], bg.inputs['Color'])
        bg.inputs['Strength'].default_value = 1.6
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sc.collection.objects.link(sun)
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.86, 0.68)
    sun.data.angle = math.radians(0.53)
    sun.rotation_euler = (math.radians(62), 0, math.radians(-35))
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x = 420
    sc.render.resolution_y = 420
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Base Contrast'
    try:
        sc.eevee.use_shadows = True
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass


def frame(sc, objs, dirv, name):
    mn = mathutils.Vector((1e9,) * 3)
    mx = -mn
    for o in objs:
        for v in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(v)
            mn = mathutils.Vector(map(min, mn, w))
            mx = mathutils.Vector(map(max, mx, w))
    c = (mn + mx) / 2
    r = max((mx - mn).length / 2, 0.02)
    cam = sc.camera
    d = mathutils.Vector(dirv).normalized()
    dist = r / math.sin(math.radians(cam.data.angle * 180 / math.pi / 2)) * 1.02
    cam.location = c + d * dist
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    cam.data.clip_start = dist / 100
    cam.data.clip_end = dist * 10
    sc.render.filepath = os.path.join(OUT, name + '.png')
    bpy.ops.render.render(write_still=True)


def main():
    ids = args()
    specs = {s['material']: s for s in json.load(open(os.path.join(READY, 'materials_cc0.json')))}
    skin_for = {}
    for s in specs.values():
        if s.get('assign_to_slot'):
            skin_for.setdefault(s['assign_to_slot'], s['material'])
    os.makedirs(OUT, exist_ok=True)
    for rp in sorted(glob.glob(os.path.join(RAW, '_reports', 'props', 'PH_*.json'))):
        rep = json.load(open(rp))
        mid = rep['manifest_id']
        if ids and mid not in ids:
            continue
        for out in rep['outputs']:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            sc = bpy.context.scene
            setup_world(sc)
            cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
            sc.collection.objects.link(cam)
            sc.camera = cam
            cam.data.lens = 60
            bpy.ops.import_scene.fbx(filepath=os.path.join(READY, out['fbx']))
            objs = [o for o in sc.objects if o.type == 'MESH']
            for o in objs:
                for slot in o.material_slots:
                    if slot.material is None:
                        continue
                    nm = slot.material.name.split('.')[0]
                    sp = specs.get(nm) or specs.get(skin_for.get(nm, ''))
                    if sp:
                        slot.material = build_material(sp)
            v = out['variant'] or 'main'
            lods = sorted([o for o in objs if '_LOD' in o.name], key=lambda o: o.name)
            base = [o for o in objs if '_LOD' not in o.name] + lods[:1]
            for o in lods[1:]:
                o.hide_render = True
            frame(sc, base, (0.62, -1.0, 0.5), f'{mid}__{v}__front')
            frame(sc, base, (-0.7, 1.0, 0.35), f'{mid}__{v}__back')
            for i, o in enumerate(lods[1:], start=1):
                for p in lods:
                    p.hide_render = (p is not o)
                frame(sc, [o], (0.62, -1.0, 0.5), f'{mid}__{v}__lod{i}')
            print('PREVIEW', mid, v)


main()
