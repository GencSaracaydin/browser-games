"""Print the UV (texture pixel) under 3D points by ray-casting along a direction (Blender headless).
  Blender -b --factory-startup -P Tools/images/bl_uvprobe.py -- --fbx SM_X.fbx --tex 1024 --dir 0,1,0 --pts "x,y,z;x,y,z"
Rays start at each point - 0.5*dir and travel along dir; prints point -> (u,v) and pixel (px, py) with py from the top."""
import bpy, sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree
argv = sys.argv[sys.argv.index("--") + 1:]
A = {argv[i]: argv[i + 1] for i in range(0, len(argv) - 1, 2)}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=A["--fbx"])
ob = next(o for o in bpy.context.scene.objects if o.type == "MESH" and ("LOD0" in o.name or not "_LOD" in o.name))
dg = bpy.context.evaluated_depsgraph_get()
me = ob.data; me.calc_loop_triangles()
mw = ob.matrix_world
bvh = BVHTree.FromObject(ob, dg)
d = Vector(tuple(map(float, A["--dir"].split(","))))
S = int(A.get("--tex", 1024))
uvl = me.uv_layers.active.data
for p in A["--pts"].split(";"):
    P = Vector(tuple(map(float, p.split(","))))
    o = mw.inverted() @ (P - d * 0.5)
    hit, nrm, idx, dist = bvh.ray_cast(o, (mw.inverted().to_3x3() @ d).normalized())
    if hit is None:
        print("PROBE", p, "miss"); continue
    poly = me.polygons[idx]
    # barycentric over the polygon's first triangle fan
    best = None
    for lt in me.loop_triangles:
        if lt.polygon_index != idx: continue
        a, b, c = (me.vertices[v].co for v in lt.vertices)
        from mathutils.geometry import barycentric_transform
        uvs = [uvl[l].uv for l in lt.loops]
        uv = barycentric_transform(hit, a, b, c, Vector((*uvs[0], 0)), Vector((*uvs[1], 0)), Vector((*uvs[2], 0)))
        best = uv; break
    print("PROBE", p, "uv", round(best.x, 4), round(best.y, 4), "px", int(best.x * S), int((1 - best.y) * S), "hit", tuple(round(x, 4) for x in (mw @ hit)))
