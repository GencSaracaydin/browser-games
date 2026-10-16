"""Render a GLB/FBX prop from 4 sides with its own materials. Blender -b -P prop_preview.py -- <in> <out_prefix>"""
import bpy, sys, math, mathutils, os
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
src, out = a[0], a[1]
if src.lower().endswith((".glb", ".gltf")):
    bpy.ops.import_scene.gltf(filepath=src)
else:
    bpy.ops.import_scene.fbx(filepath=src)
ms = [o for o in bpy.data.objects if o.type == "MESH"]
pts = [o.matrix_world @ mathutils.Vector(c) for o in ms for c in o.bound_box]
mn = mathutils.Vector([min(p[i] for p in pts) for i in range(3)]); mx = mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
ctr = (mn + mx) / 2; size = max(mx - mn)
tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in ms)
print("BBOX", tuple(round(x, 3) for x in (mx - mn)), "tris", tris)
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"; sc.view_settings.view_transform = "AgX"
sc.render.resolution_x = sc.render.resolution_y = 512
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.5, 0.52, 1)
sun = bpy.data.objects.new("s", bpy.data.lights.new("s", "SUN")); sc.collection.objects.link(sun); sun.data.energy = 3
sun.rotation_euler = (math.radians(45), 0, math.radians(30))
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = size * 1.3
for k, az in enumerate([30, 120, 210, 300]):
    r = size * 4; e = math.radians(20); A = math.radians(az)
    cam.location = ctr + mathutils.Vector((r * math.sin(A) * math.cos(e), -r * math.cos(A) * math.cos(e), r * math.sin(e)))
    cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = f"{out}_{k}.png"; bpy.ops.render.render(write_still=True)
