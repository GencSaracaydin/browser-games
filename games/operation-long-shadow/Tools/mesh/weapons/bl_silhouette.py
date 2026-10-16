"""Overlay the processed mesh's side silhouette on the reference image mask (diagnostic for the photo projection).

  Blender -b --factory-startup -P Tools/mesh/weapons/bl_silhouette.py -- <work dir> [side]
Reads <work>/project.json (+ proj/project_report.json for the fitted k/rot/shift) and writes
<work>/silhouette_<side>.png: green = mesh and photo agree, red = mesh only, blue = photo only.
"""
import bpy, json, math, os, sys
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
WORK = args[0]
SIDE = args[1] if len(args) > 1 else "left"
J = json.load(open(os.path.join(WORK, "project.json")))
R = json.load(open(os.path.join(WORK, "proj/project_report.json")))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=J["fbx"])
names = J["object"] if isinstance(J["object"], list) else [J["object"]]
sel = [o for o in bpy.data.objects if o.type == "MESH" and any(o.name == x or o.name.startswith(x) for x in names)]
for o in sel:
    o.data.transform(o.matrix_world)
    o.matrix_world.identity()
bpy.ops.object.select_all(action="DESELECT")
for o in sel:
    o.select_set(True)
bpy.context.view_layer.objects.active = sel[0]
if len(sel) > 1:
    bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
me = ob.data
me.calc_loop_triangles()
co = np.array([v.co[:] for v in me.vertices])
tri = np.array([t.vertices[:] for t in me.loop_triangles])
v = next(x for x in J["views"] if x["side"] == SIDE)
img = bpy.data.images.load(v["mask"])
W, H = img.size
a = np.empty(W * H * 4, np.float32)
img.pixels.foreach_get(a)
M = a.reshape(H, W, 4)[::-1, :, 0] > 0.5
bx0, by0, bx1, by1 = v["bbox"]
y0, y1 = co[:, 1].min(), co[:, 1].max()
z0, z1 = co[:, 2].min(), co[:, 2].max()
s = (bx1 - bx0) / (y1 - y0)
flip = SIDE == "right"
f = R[SIDE]
k, th = f["k"], math.radians(f["rot_deg"])
c = np.array(f.get("c", [(y0 + y1) / 2, (z0 + z1) / 2]))
D0, D1 = co[:, 1] - c[0], co[:, 2] - c[1]
Yt = c[0] + k * (math.cos(th) * D0 - math.sin(th) * D1) + f.get("ty", 0.0)
Zt = c[1] + k * (math.sin(th) * D0 + math.cos(th) * D1) + f.get("tz", 0.0)
x = (bx0 + (Yt - y0) * s) if not flip else (bx1 - (Yt - y0) * s)
y = (by0 + by1) / 2 - (Zt - (z0 + z1) / 2) * s
G = np.zeros((H, W), bool)
yy, xx = np.mgrid[0:H, 0:W]
for t in tri:
    px, py = x[t], y[t]
    xa, xb = int(max(0, np.floor(px.min()))), int(min(W - 1, np.ceil(px.max())))
    ya, yb = int(max(0, np.floor(py.min()))), int(min(H - 1, np.ceil(py.max())))
    if xb < xa or yb < ya:
        continue
    X, Y = np.meshgrid(np.arange(xa, xb + 1) + 0.5, np.arange(ya, yb + 1) + 0.5)
    (x1, y1_), (x2, y2), (x3, y3) = zip(px, py)
    d = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1_ - y3)
    if abs(d) < 1e-9:
        continue
    l1 = ((y2 - y3) * (X - x3) + (x3 - x2) * (Y - y3)) / d
    l2 = ((y3 - y1_) * (X - x3) + (x1 - x3) * (Y - y3)) / d
    G[ya:yb + 1, xa:xb + 1] |= (l1 >= 0) & (l2 >= 0) & (l1 + l2 <= 1)
out = np.ones((H, W, 4), np.float32)
out[..., :3] = 1.0
out[G & M] = (0.2, 0.7, 0.2, 1)
out[G & ~M] = (0.9, 0.1, 0.1, 1)
out[~G & M] = (0.1, 0.2, 0.9, 1)
o = bpy.data.images.new("sil", W, H, alpha=True)
o.pixels.foreach_set(out[::-1].ravel())
o.filepath_raw = os.path.join(WORK, f"silhouette_{SIDE}.png")
o.file_format = "PNG"
o.save()
print("IoU", (G & M).sum() / max(1, (G | M).sum()), "->", o.filepath_raw)
