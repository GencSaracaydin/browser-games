"""Quick look at a source model (glTF/GLB/FBX) in Blender (headless, Eevee): per-object names, tris, bbox and
three renders (front-left 3/4, back-right 3/4, front).

  Blender -b --factory-startup -t 2 -P Tools/images/bl_preview_src.py -- --in model.gltf --out /tmp/prev [--res 512]
Writes <out>_a.png, <out>_b.png, <out>_c.png and <out>.json.
"""
import bpy, json, math, os, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
A = {argv[i]: argv[i + 1] for i in range(0, len(argv) - 1, 2)}
src, out, res = A["--in"], A["--out"], int(A.get("--res", 512))
bpy.ops.wm.read_factory_settings(use_empty=True)
if src.lower().endswith((".gltf", ".glb")):
    bpy.ops.import_scene.gltf(filepath=src)
elif src.lower().endswith(".fbx"):
    bpy.ops.import_scene.fbx(filepath=src)
else:
    bpy.ops.wm.obj_import(filepath=src)
sc = bpy.context.scene
meshes = [o for o in sc.objects if o.type == "MESH"]
info = {}
mn, mx = Vector((1e9,) * 3), Vector((-1e9,) * 3)
for o in meshes:
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    omn = Vector([min(c) for c in zip(*pts)]) if pts else Vector()
    omx = Vector([max(c) for c in zip(*pts)]) if pts else Vector()
    mn, mx = Vector(map(min, mn, omn)), Vector(map(max, mx, omx))
    info[o.name] = {"tris": sum(len(p.vertices) - 2 for p in o.data.polygons), "min": [round(x, 4) for x in omn],
                    "max": [round(x, 4) for x in omx], "mats": [s.material.name for s in o.material_slots if s.material]}
info["_bbox"] = {"min": [round(x, 4) for x in mn], "max": [round(x, 4) for x in mx], "size": [round(x, 4) for x in (mx - mn)]}
json.dump(info, open(out + ".json", "w"), indent=1)
print(json.dumps(info["_bbox"]))
c = (mn + mx) / 2
r = (mx - mn).length / 2
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x = sc.render.resolution_y = res
sc.render.film_transparent = False
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.5, 0.52, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 3.0; sun.rotation_euler = (math.radians(50), 0, math.radians(30))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 50
for tag, (az, el) in {"a": (-35, 25), "b": (145, 25), "c": (0, 5)}.items():
    d = 2.6 * r / math.tan(math.radians(18))
    az_r, el_r = math.radians(az), math.radians(el)
    # Blender front = -Y
    cam.location = c + Vector((math.sin(az_r) * math.cos(el_r), -math.cos(az_r) * math.cos(el_r), math.sin(el_r))) * d * 0.5
    cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = d * 4
    sc.render.filepath = f"{out}_{tag}.png"
    bpy.ops.render.render(write_still=True)
