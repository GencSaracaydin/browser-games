"""CONTENT_AUDIT helper: dump every LOD0 triangle of an FBX (Unity-space centre, normal, area, 3 UVs, 3 vertex
positions) to JSON for texture-space repaints (Tools/audit/dive_watch_dial.py).
  Blender -b --factory-startup -P Tools/audit/blender_uv_probe.py -- <fbx> <out.json>
Unity = (-bx, bz, -by) for the WP10 FBX convention (import axis_forward -Z, up Y). Tripo meshes are triangles.
"""
import bpy, sys, json
import numpy as np
argv = sys.argv[sys.argv.index("--")+1:]
fbx, out = argv
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx, axis_forward="-Z", axis_up="Y")
o=[x for x in bpy.data.objects if x.type=="MESH" and x.name.endswith("LOD0")][0]
mw=o.matrix_world; me=o.data; uvl=me.uv_layers.active.data
rows=[]
for p in me.polygons:
    c=mw@p.center; n=(mw.to_3x3()@p.normal).normalized()
    rows.append([-c.x,c.z,-c.y,-n.x,n.z,-n.y,p.area]+[v for li in p.loop_indices for v in (uvl[li].uv[0],uvl[li].uv[1])][:6]+[ (lambda v:(-v.x,v.z,-v.y))(mw@me.vertices[me.loops[li].vertex_index].co)[k] for li in p.loop_indices[:3] for k in range(3)])
json.dump(rows, open(out,"w"))
print("N", len(rows))
