"""Top + front views of a prop with axis markers (red = +X, green = -Y 'front' after import). For orienting props."""
import bpy, sys, math, mathutils
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a[0])
ms = [o for o in bpy.data.objects if o.type == "MESH"]
pts = [o.matrix_world @ mathutils.Vector(c) for o in ms for c in o.bound_box]
mn = mathutils.Vector([min(p[i] for p in pts) for i in range(3)]); mx = mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
ctr = (mn + mx) / 2; size = max(mx - mn)
for col, d in (((1, 0, 0, 1), (1, 0, 0)), ((0, 1, 0, 1), (0, -1, 0))):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=size * 0.05, location=ctr + mathutils.Vector(d) * size * 0.65)
    m = bpy.data.materials.new("m"); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = col
    m.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 3
    bpy.context.active_object.data.materials.append(m)
sc = bpy.context.scene; sc.render.engine = "BLENDER_EEVEE"; sc.render.resolution_x = sc.render.resolution_y = 384
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.6, 0.6, 0.6, 1)
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = size * 1.6
for name, loc in (("top", (0, 0, 1)), ("front", (0, -1, 0)), ("side", (1, 0, 0))):
    cam.location = ctr + mathutils.Vector(loc) * size * 3
    up = "Y"
    cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y" if name != "top" else "Y").to_euler()
    sc.render.filepath = f"{a[1]}_{name}.png"; bpy.ops.render.render(write_still=True)
