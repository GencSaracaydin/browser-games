"""WP10_CC0: Cycles ambient-occlusion bake for Poly Haven props whose ARM map ships with an EMPTY AO channel
(metal_trash_can, office_notepads). Runs INSIDE Blender 5.2, headless:

  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -t 2 -P Tools/cc0/wp10_ao_bake.py -- \
      [--ids PH_METAL_TRASH_CAN,PH_OFFICE_NOTEPADS] [--size 1024] [--samples 128]

Imports the source glTF 1k (same UV layout as the shipped FBX: wp10_ph_model.py keeps the source UVs), bakes AO
per material with only the meshes of that material renderable (no cross-occlusion between variants that sit side
by side in the source scene), 16 px margin, and writes
  RawAssets/wp10/cc0/<ID>/ao_bake/<arm texture basename>_ao.png   (8-bit grey, linear, 1 = unoccluded)
Tools/tex/wp10_props_tex.py picks these up automatically for any ARM whose AO channel is empty.
"""
import bpy, glob, json, os, sys, time

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')
# material name in the source glTF -> AO ray distance (m)
JOBS = {
    'PH_METAL_TRASH_CAN': {'metal_trash_can': 0.25, 'metal_trash_can_rust': 0.25},
    'PH_OFFICE_NOTEPADS': {'office_notepads': 0.04},
}


def argv():
    a = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    o = {'ids': list(JOBS), 'size': 1024, 'samples': 128}
    for i, k in enumerate(a):
        if k == '--ids':
            o['ids'] = a[i + 1].split(',')
        if k == '--size':
            o['size'] = int(a[i + 1])
        if k == '--samples':
            o['samples'] = int(a[i + 1])
    return o


def arm_basename(mat):
    """The image used by the glTF metallic-roughness input (Poly Haven: <name>_arm_1k)."""
    for n in mat.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image and '_arm_' in n.image.name:
            return os.path.splitext(n.image.name)[0]
    return mat.name + '_arm_1k'


def bake_one(mid, mats, size, samples):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src = glob.glob(os.path.join(RAW, mid, 'gltf_1k', '*.gltf'))[0]
    bpy.ops.import_scene.gltf(filepath=src)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = False
    sc.render.bake.margin = 16
    sc.render.bake.use_selected_to_active = False
    if sc.world is None:
        sc.world = bpy.data.worlds.new('World')
    meshes = [o for o in sc.objects if o.type == 'MESH']
    out_dir = os.path.join(RAW, mid, 'ao_bake')
    os.makedirs(out_dir, exist_ok=True)
    done = {}
    for mname, dist in mats.items():
        mat = bpy.data.materials.get(mname)
        if mat is None:
            print('  missing material', mname)
            continue
        users = [o for o in meshes if any(s.material == mat for s in o.material_slots)]
        for o in meshes:
            o.hide_render = o not in users
        sc.world.light_settings.distance = dist
        img = bpy.data.images.new(f'{mname}_AO', size, size, alpha=False, float_buffer=True)
        img.colorspace_settings.name = 'Non-Color'
        dummies = []
        for o in users:
            for s in o.material_slots:
                m = s.material
                if m is None:
                    continue
                node = m.node_tree.nodes.new('ShaderNodeTexImage')
                if m == mat:
                    node.image = img
                else:            # other slots on the same object: throwaway target so the bake does not fail
                    d = bpy.data.images.new(f'dummy_{m.name}', 64, 64)
                    node.image = d
                    dummies.append(d)
                m.node_tree.nodes.active = node
        bpy.ops.object.select_all(action='DESELECT')
        for o in users:
            o.select_set(True)
        bpy.context.view_layer.objects.active = users[0]
        t0 = time.time()
        bpy.ops.object.bake(type='AO')
        p = os.path.join(out_dir, arm_basename(mat) + '_ao.png')
        img.filepath_raw = p
        img.file_format = 'PNG'
        sc.render.image_settings.color_depth = '8'
        img.save()
        px = list(img.pixels)[0::4]
        mean = sum(px) / len(px)
        done[mname] = {'file': os.path.relpath(p, ROOT), 'objects': [o.name for o in users], 'ray_m': dist,
                       'mean_all_px': round(mean, 3), 'bake_s': round(time.time() - t0, 1)}
        print('  baked', mid, mname, done[mname], flush=True)
    json.dump({'manifest_id': mid, 'source': os.path.relpath(src, ROOT), 'size': size, 'samples': samples, 'bakes': done},
              open(os.path.join(out_dir, 'ao_bake.json'), 'w'), indent=1)


def main():
    o = argv()
    for mid in o['ids']:
        bake_one(mid, JOBS[mid], o['size'], o['samples'])


main()
