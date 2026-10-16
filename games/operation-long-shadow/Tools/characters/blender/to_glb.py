"""FBX character -> light GLB for Meshy rigging input.
Run: Blender -b --factory-startup -P to_glb.py -- <in.fbx> <bc.png> <out.glb> [decimate_ratio]"""
import bpy, sys
a = sys.argv[sys.argv.index("--") + 1:]
src, bc, out = a[0], a[1], a[2]
ratio = float(a[3]) if len(a) > 3 else 0.5
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = [o for o in bpy.data.objects if o.type == "ARMATURE"]
ms = [o for o in bpy.data.objects if o.type == "MESH"]
for m in ms:
    for mod in list(m.modifiers):
        m.modifiers.remove(mod)
    mw = m.matrix_world.copy(); m.parent = None; m.matrix_world = mw
for o in arm:
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action="DESELECT")
for m in ms:
    m.select_set(True)
bpy.context.view_layer.objects.active = ms[0]
if len(ms) > 1:
    bpy.ops.object.join()
o = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
o.vertex_groups.clear()
d = o.modifiers.new("d", "DECIMATE"); d.ratio = ratio
bpy.ops.object.modifier_apply(modifier="d")
mat = bpy.data.materials.new("body"); mat.use_nodes = True
img = bpy.data.images.load(bc); img.scale(512, 512)
t = mat.node_tree.nodes.new("ShaderNodeTexImage"); t.image = img
mat.node_tree.links.new(t.outputs[0], mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
o.data.materials.clear(); o.data.materials.append(mat)
print("DIMS", tuple(round(x, 3) for x in o.dimensions), "tris", sum(len(p.vertices) - 2 for p in o.data.polygons))
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True, export_animations=False, export_skins=False)
