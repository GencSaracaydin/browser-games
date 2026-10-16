"""Orthographic measuring renders of an FBX (Blender headless) so part positions can be read in metres.

  Blender -b --factory-startup -t 2 -P Tools/images/bl_ortho.py -- --fbx SM_X.fbx --bc T_X_BC.png --out /tmp/x [--res 1200]
Writes <out>_front.png (camera at -Y looking +Y: the prop's front, X right, Z up), <out>_top.png (camera above,
X right, Y up = toward the back), <out>_side.png (camera at -X looking +X: image right = -Y = the front; u is -y) and
<out>_ortho.json {view: {cx, cy, scale_m, res}} where pixel (px, py) -> metres:
  u = cx + (px/res - 0.5) * scale_m ; v = cy - (py/res - 0.5) * scale_m
Coordinates are Blender metres after FBX import (front -Y, up +Z); Unity = (x, z, -y) with front +Z.
"""
import bpy, json, math, os, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
A = {argv[i]: argv[i + 1] for i in range(0, len(argv) - 1, 2)}
RES = int(A.get("--res", 1200))
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=A["--fbx"])
objs = [o for o in sc.objects if o.type == "MESH"]
for o in objs:
    if "_LOD" in o.name and not o.name.endswith("_LOD0"):
        o.hide_render = True
mat = bpy.data.materials.new("prev"); mat.use_nodes = True
b = mat.node_tree.nodes["Principled BSDF"]
if A.get("--bc"):
    t = mat.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(A["--bc"])
    mat.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    mat.node_tree.links.new(t.outputs["Color"], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 0.6
b.inputs["Roughness"].default_value = 0.8
for o in objs:
    if o.name.startswith("SM_") or not A.get("--keep-mats"):
        o.data.materials.clear(); o.data.materials.append(mat)
vis = [o for o in objs if not o.hide_render]
mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
for o in vis:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
ctr = (mn + mx) / 2
size = max(mx - mn) * 1.08
sc.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE_NEXT"
sc.render.resolution_x = sc.render.resolution_y = RES
sc.render.film_transparent = False
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.9, 0.9, 0.95, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 1.2
cam_d = bpy.data.cameras.new("c"); cam_d.type = "ORTHO"; cam_d.ortho_scale = size
cam = bpy.data.objects.new("c", cam_d); sc.collection.objects.link(cam); sc.camera = cam
out = {}
views = {"front": (Vector((ctr.x, mn.y - 2, ctr.z)), (math.radians(90), 0, 0), (ctr.x, ctr.z)),
         "top": (Vector((ctr.x, ctr.y, mx.z + 2)), (0, 0, 0), (ctr.x, ctr.y)),
         "side": (Vector((mn.x - 2, ctr.y, ctr.z)), (math.radians(90), 0, math.radians(-90)), (-ctr.y, ctr.z))}
for name, (loc, rot, c2) in views.items():
    cam.location = loc; cam.rotation_euler = rot
    cam_d.clip_end = 100
    sc.render.filepath = f"{A['--out']}_{name}.png"
    bpy.ops.render.render(write_still=True)
    out[name] = {"cx": c2[0], "cy": c2[1], "scale_m": size, "res": RES}
out["bbox_min"] = list(mn); out["bbox_max"] = list(mx)
json.dump(out, open(f"{A['--out']}_ortho.json", "w"), indent=1)
print("ORTHO", json.dumps(out))
