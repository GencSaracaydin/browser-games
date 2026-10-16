"""CONTENT_AUDIT: list the UV polygons of SM_Helicopter's windshield glass into a texture-space mask (for
Tools/audit/heli_windshield.py). Glass faces = LOD0 faces in the canopy region (Unity heli z > 4.0, y > 1.85) facing
forward/up/out whose texels are not the beige frame paint.
  Blender -b --factory-startup -P Tools/audit/blender_heli_glass_mask.py -- <fbx> <bc.png> <out_polys.json>
"""
import bpy, sys
import numpy as np
argv = sys.argv[sys.argv.index("--") + 1:]
fbx, bcp, outp = argv
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx, axis_forward="-Z", axis_up="Y")
o = [x for x in bpy.data.objects if x.type == "MESH" and x.name.endswith("LOD0")][0]
img = bpy.data.images.load(bcp)
W, H = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)
me = o.data
mw = o.matrix_world
uvl = me.uv_layers.active.data
import json
polys = []
n_sel = 0
for p in me.polygons:
    c = mw @ p.center
    n = (mw.to_3x3() @ p.normal).normalized()
    ux, uy, uz = -c.x, c.z, -c.y
    nux, nuy, nuz = -n.x, n.z, -n.y
    canopy = uz > 4.0 and 1.85 < uy < 3.0 and (nuz >= 0.15 or nuy >= 0.4)
    # side windows (measured dark panels): aft cabin windows and the cockpit door windows, both sides
    side = abs(nux) > 0.35 and ((0.0 <= uz <= 1.7 and 1.60 <= uy <= 2.40) or (3.5 <= uz <= 5.0 and 1.60 <= uy <= 2.55))
    if not (canopy or side):
        continue
    uvs = [uvl[i].uv for i in p.loop_indices]
    cols = [px[min(H - 1, max(0, int(u[1] * H))), min(W - 1, max(0, int(u[0] * W))), :3] for u in uvs]
    cu = sum(u[0] for u in uvs) / len(uvs); cv = sum(u[1] for u in uvs) / len(uvs)
    cols.append(px[min(H - 1, int(cv * H)), min(W - 1, int(cu * W)), :3])
    col = np.mean(cols, axis=0)
    if col[0] - col[2] > 0.10 and col.mean() > 0.35:   # beige frame paint: keep
        continue
    polys.append([[u[0], u[1]] for u in uvs])
    n_sel += 1
json.dump({"size": [W, H], "polys": polys}, open(outp, "w"))
print("GLASS_FACES", n_sel)
