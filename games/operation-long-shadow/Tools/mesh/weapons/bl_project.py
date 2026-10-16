"""Project the photographic reference views onto a processed weapon mesh (Blender headless, Cycles bakes).

  Blender -b --factory-startup -t 3 -P Tools/mesh/weapons/bl_project.py -- project.json
project.json: {"fbx": ".../SM_WPN_AK_FP.fbx", "object": "SM_WPN_AK_FP", "size": 2048, "out": "work dir",
  "views": [{"side": "left", "image": "proj_left_pad.png", "mask": "proj_left_mask.png", "bbox": [x0,y0,x1,y1]},
            {"side": "right", ...}],
  "extra": [{"object": "Mag", "size": 512, "out": "work dir/mag", "occluders": ["SM_WPN_AK_FP"],
             "views": [{"side": "left", "image": "full_pad.png"}]}]}       (optional, uses the body's fit)
For each view it writes <out>/proj_<side>_color.png (the photo projected orthographically along X into the UV atlas)
and <out>/proj_<side>_vis.png (how squarely and unoccluded each texel faces that camera: sun-lamp diffuse bake, 0..1).
The photo-to-mesh mapping starts from the bounding boxes and is refined by a silhouette IoU search
(scale, rotation, shift). tex_pack.py --project blends the colour into the base colour by visibility.
Mesh space (as re-imported from the Unity FBX): weapon left = +X, muzzle = -Y, up = +Z.
"""
import bpy, json, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

J = json.load(open(sys.argv[sys.argv.index("--") + 1]))
ROOT = "/Users/gencsaracaydin/Documents/fun/fun-unity"
P = lambda p: p if os.path.isabs(p) else os.path.join(ROOT, p)
OUT = P(J["out"])
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.device = "CPU"
sc.render.threads_mode = "FIXED"
sc.render.threads = 3
REPORT = {}

bpy.ops.import_scene.fbx(filepath=P(J["fbx"]))
OBJS = {}
for o in list(bpy.data.objects):
    if o.type != "MESH":
        continue
    mw = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = mw
for o in list(bpy.data.objects):
    if o.type != "MESH":
        bpy.data.objects.remove(o, do_unlink=True)
for o in bpy.data.objects:
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    OBJS[o.name] = o
names = J["object"] if isinstance(J["object"], list) else [J["object"]]
sel = [o for n, o in OBJS.items() if any(n == x or n.startswith(x) for x in names)]
if len(sel) > 1:
    # world models with split parts (warhead, mount): they share the UV atlas, so fit and bake them as one mesh
    joined = [o.copy() for o in sel]
    for o in joined:
        o.data = o.data.copy()
        bpy.context.collection.objects.link(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in joined:
        o.select_set(True)
    bpy.context.view_layer.objects.active = joined[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = "PROJ_TARGET"
    for o in sel:
        o.hide_render = True
    OBJS = {n: o for n, o in OBJS.items() if o not in sel}
    OBJS[ob.name] = ob
else:
    ob = sel[0]
me = ob.data
co = np.empty(len(me.vertices) * 3)
me.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)


def load_mask(path):
    img = bpy.data.images.load(P(path))
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    m = a.reshape(h, w, 4)[::-1, :, 0] > 0.5
    bpy.data.images.remove(img)
    return m


def raster_yz(cell):
    me.calc_loop_triangles()
    c2 = co[:, 1:3]
    tri = np.array([t.vertices[:] for t in me.loop_triangles])
    lo = c2.min(0)
    nx, ny = (np.ceil((c2.max(0) - lo) / cell).astype(int) + 2)
    grid = np.zeros((nx, ny), bool)
    gx = (np.arange(nx) + 0.5) * cell + lo[0]
    gy = (np.arange(ny) + 0.5) * cell + lo[1]
    for t in tri:
        p = c2[t]
        mn = np.floor((p.min(0) - lo) / cell).astype(int)
        mx = np.ceil((p.max(0) - lo) / cell).astype(int) + 1
        X, Y = np.meshgrid(gx[mn[0]:mx[0]], gy[mn[1]:mx[1]], indexing="ij")
        (x1, y1), (x2, y2), (x3, y3) = p
        d = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3)
        if abs(d) < 1e-14:
            continue
        l1 = ((y2 - y3) * (X - x3) + (x3 - x2) * (Y - y3)) / d
        l2 = ((y3 - y1) * (X - x3) + (x1 - x3) * (Y - y3)) / d
        grid[mn[0]:mx[0], mn[1]:mx[1]] |= (l1 >= -1e-6) & (l2 >= -1e-6) & (l1 + l2 <= 1 + 1e-6)
    ii, jj = np.nonzero(grid)
    return np.stack([gx[ii], gy[jj]], 1)


y0, y1 = co[:, 1].min(), co[:, 1].max()
z0, z1 = co[:, 2].min(), co[:, 2].max()


def fit_view(v):
    """Affine image(px) <- mesh(Y,Z): x = a*Y + b*Z + c, y = d*Y + e*Z + f."""
    M = load_mask(v["mask"])
    H, W = M.shape
    bx0, by0, bx1, by1 = v["bbox"]
    flip = v["side"] == "right"
    kz = v.get("kz", 1.0)  # vertical px/m multiplier when the mesh cross-section was rescaled
    s = (bx1 - bx0) / (y1 - y0)  # px per m
    # initial: Y -> x (left view: muzzle (y0) at bx0; right view mirrored), Z -> y (up = smaller y)
    zc_img, zc = (by0 + by1) / 2, (z0 + z1) / 2
    G = raster_yz(1.0 / s)
    rng = np.random.default_rng(0)
    area_cell = len(G)
    if len(G) > 15000:
        G = G[rng.choice(len(G), 15000, replace=False)]
    A_M = float(M.sum())
    c = G.mean(0)

    def to_img(Y, Z, k, th, ty, tz):
        D0, D1 = Y - c[0], Z - c[1]
        ct, st = math.cos(th), math.sin(th)
        Yt = c[0] + k * (ct * D0 - st * D1) + ty
        Zt = c[1] + k * (st * D0 + ct * D1) + tz
        x = (bx0 + (Yt - y0) * s) if not flip else (bx1 - (Yt - y0) * s)
        y = zc_img - (Zt - zc) * s * kz
        return x, y

    def score(k, th, ty, tz):
        x, y = to_img(G[:, 0], G[:, 1], k, th, ty, tz)
        xi, yi = np.floor(x).astype(int), np.floor(y).astype(int)
        ok = (xi >= 0) & (xi < W) & (yi >= 0) & (yi < H)
        inside = np.zeros(len(G), bool)
        inside[ok] = M[yi[ok], xi[ok]]
        a = (area_cell / len(G)) * k * k * kz  # image px per sample
        I = inside.sum() * a
        return I / (A_M + len(G) * a - I)

    best = (score(1, 0, 0, 0), 1.0, 0.0, 0.0, 0.0)
    start = best[0]
    xg, yg = to_img(G[:, 0], G[:, 1], 1, 0, 0, 0)
    print("debug: mesh Y", y0, y1, "Z", z0, z1, "G", len(G), "img x", xg.min(), xg.max(), "img y", yg.min(), yg.max(), "mask px", A_M, "bbox", v["bbox"], flush=True)
    L = y1 - y0
    for kr, thr, tr, n in ((0.04, math.radians(1.5), 0.01 * L, 7), (0.01, math.radians(0.4), 0.0025 * L, 7), (0.003, math.radians(0.1), 0.0007 * L, 5)):
        _, k0, th0, ty0, tz0 = best
        for k in np.linspace(k0 - kr, k0 + kr, n):
            for th in np.linspace(th0 - thr, th0 + thr, n):
                for ty in np.linspace(ty0 - tr, ty0 + tr, n):
                    for tz in np.linspace(tz0 - tr, tz0 + tr, n):
                        sc_ = score(k, th, ty, tz)
                        if sc_ > best[0]:
                            best = (sc_, k, th, ty, tz)
    iou, k, th, ty, tz = best
    print(f"view {v['side']}: IoU {start:.3f} -> {iou:.3f}  k {k:.4f} rot {math.degrees(th):.2f} shift {ty*1000:.1f},{tz*1000:.1f} mm", flush=True)
    REPORT[v["side"]] = {"iou_start": round(start, 4), "iou": round(iou, 4), "k": round(k, 5), "rot_deg": round(math.degrees(th), 3),
                         "ty": float(ty), "tz": float(tz), "c": [float(c[0]), float(c[1])]}
    # express as affine on (Y, Z) -> image uv (0..1, v up for Blender images)
    ct, st = math.cos(th), math.sin(th)
    # Yt = c0 + k(ct(Y-c0) - st(Z-c1)) + ty ; Zt = c1 + k(st(Y-c0) + ct(Z-c1)) + tz
    aY, aZ, a0 = k * ct, -k * st, c[0] - k * ct * c[0] + k * st * c[1] + ty
    bY, bZ, b0 = k * st, k * ct, c[1] - k * st * c[0] - k * ct * c[1] + tz
    sx = s if not flip else -s
    ox = bx0 - y0 * s if not flip else bx1 + y0 * s
    # x = ox + sx*Yt ; y = zc_img - (Zt - zc)*s ; u = x/W ; v = 1 - y/H
    ux = (sx * aY / W, sx * aZ / W, (ox + sx * a0) / W)
    vy_y = (-s * kz * bY, -s * kz * bZ, zc_img - s * kz * (b0 - zc))
    vv = (-vy_y[0] / H, -vy_y[1] / H, 1 - vy_y[2] / H)
    return ux, vv


def bake(kind, size, path, setup, target=None, occluders=None):
    target = target or ob
    occ = set(occluders or []) | {target.name}
    for o in OBJS.values():
        o.hide_render = o.name not in occ
    for o in OBJS.values():
        o.select_set(False)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    tme = target.data
    img = bpy.data.images.new(os.path.basename(path), size, size, alpha=False, float_buffer=(kind == "vis"))
    img.colorspace_settings.name = "sRGB" if kind == "color" else "Non-Color"
    mat = bpy.data.materials.new("bake_" + kind)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    outn = nt.nodes.new("ShaderNodeOutputMaterial")
    setup(nt, outn)
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = img
    nt.nodes.active = tn
    saved = list(tme.materials)
    tme.materials.clear()
    tme.materials.append(mat)
    sc.render.bake.margin = 8
    sc.render.bake.use_clear = True
    if kind == "color":
        sc.cycles.samples = 1
        bpy.ops.object.bake(type="EMIT")
    else:
        sc.cycles.samples = 4
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"DIRECT"})
    if kind == "vis":
        a = np.empty(size * size * 4, np.float32)
        img.pixels.foreach_get(a)
        a = a.reshape(size, size, 4)
        mx = np.percentile(a[..., 0], 99.5) or 1.0
        a[..., :3] = np.clip(a[..., :3] / mx, 0, 1)
        out = bpy.data.images.new("v8", size, size, alpha=False)
        out.colorspace_settings.name = "Non-Color"
        out.pixels.foreach_set(a.ravel())
        img = out
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    tme.materials.clear()
    for m in saved:
        tme.materials.append(m)
    print("baked", path, flush=True)


FITS = {}
for v in J["views"]:
    FITS[v["side"]] = fit_view(v)


def setup_color_for(ux, vv, image):
    def setup(nt, outn):
        tc = nt.nodes.new("ShaderNodeTexCoord")
        du = nt.nodes.new("ShaderNodeVectorMath"); du.operation = "DOT_PRODUCT"
        du.inputs[1].default_value = (0, ux[0], ux[1])
        dv = nt.nodes.new("ShaderNodeVectorMath"); dv.operation = "DOT_PRODUCT"
        dv.inputs[1].default_value = (0, vv[0], vv[1])
        nt.links.new(tc.outputs["Object"], du.inputs[0])
        nt.links.new(tc.outputs["Object"], dv.inputs[0])
        au = nt.nodes.new("ShaderNodeMath"); au.operation = "ADD"; au.inputs[1].default_value = ux[2]
        av = nt.nodes.new("ShaderNodeMath"); av.operation = "ADD"; av.inputs[1].default_value = vv[2]
        nt.links.new(du.outputs["Value"], au.inputs[0])
        nt.links.new(dv.outputs["Value"], av.inputs[0])
        cmb = nt.nodes.new("ShaderNodeCombineXYZ")
        nt.links.new(au.outputs[0], cmb.inputs["X"])
        nt.links.new(av.outputs[0], cmb.inputs["Y"])
        it = nt.nodes.new("ShaderNodeTexImage")
        it.image = bpy.data.images.load(P(image))
        it.extension = "EXTEND"
        it.interpolation = "Cubic"
        nt.links.new(cmb.outputs["Vector"], it.inputs["Vector"])
        em = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(it.outputs["Color"], em.inputs["Color"])
        nt.links.new(em.outputs["Emission"], outn.inputs["Surface"])
    return setup


def setup_vis(nt, outn):
    bs = nt.nodes.new("ShaderNodeBsdfDiffuse")
    bs.inputs["Color"].default_value = (1, 1, 1, 1)
    nt.links.new(bs.outputs["BSDF"], outn.inputs["Surface"])


if sc.world is None:
    sc.world = bpy.data.worlds.new("W")
sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.0


def run(target, views, size, vis_size, out, occluders):
    os.makedirs(out, exist_ok=True)
    for v in views:
        ux, vv = FITS[v.get("fit", v["side"])]
        side = 1 if v["side"] == "left" else -1
        bake("color", size, os.path.join(out, f"proj_{v['side']}_color.png"), setup_color_for(ux, vv, v["image"]), target, occluders)
        ld = bpy.data.lights.new("sun", "SUN")
        ld.energy = 3.0
        ld.angle = math.radians(1.0)
        lo = bpy.data.objects.new("sun", ld)
        sc.collection.objects.link(lo)
        lo.rotation_euler = Vector((-side, 0, 0)).to_track_quat("-Z", "Y").to_euler()
        bake("vis", vis_size, os.path.join(out, f"proj_{v['side']}_vis.png"), setup_vis, target, occluders)
        bpy.data.objects.remove(lo, do_unlink=True)


def setup_demark(boxes):
    """Emission mask: 1 where the surface lies inside any model-space box {"side": left|right|both, "box": [y0, z0, y1,
    z1]} (Y along the gun, Z up, metres, pivot at the grip) and faces that side (|normal.x| > 0.25)."""
    def setup(nt, outn):
        tc = nt.nodes.new("ShaderNodeTexCoord")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(tc.outputs["Object"], sep.inputs["Vector"])
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        sn = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(geo.outputs["Normal"], sn.inputs["Vector"])

        def cmp(src, op, val):
            m = nt.nodes.new("ShaderNodeMath")
            m.operation = op
            nt.links.new(src, m.inputs[0])
            m.inputs[1].default_value = val
            return m.outputs[0]

        def mul(a, b):
            m = nt.nodes.new("ShaderNodeMath")
            m.operation = "MULTIPLY"
            nt.links.new(a, m.inputs[0])
            nt.links.new(b, m.inputs[1])
            return m.outputs[0]
        acc = None
        for bx in boxes:
            y0, z0, y1, z1 = bx["box"]
            f = mul(mul(cmp(sep.outputs["Y"], "GREATER_THAN", y0), cmp(sep.outputs["Y"], "LESS_THAN", y1)),
                    mul(cmp(sep.outputs["Z"], "GREATER_THAN", z0), cmp(sep.outputs["Z"], "LESS_THAN", z1)))
            side = bx.get("side", "left")
            if side == "left":
                f = mul(f, cmp(sn.outputs["X"], "GREATER_THAN", 0.25))
            elif side == "right":
                f = mul(f, cmp(sn.outputs["X"], "LESS_THAN", -0.25))
            if acc is None:
                acc = f
            else:
                m = nt.nodes.new("ShaderNodeMath")
                m.operation = "MAXIMUM"
                nt.links.new(acc, m.inputs[0])
                nt.links.new(f, m.inputs[1])
                acc = m.outputs[0]
        em = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(acc, em.inputs["Strength"])
        em.inputs["Color"].default_value = (1, 1, 1, 1)
        nt.links.new(em.outputs["Emission"], outn.inputs["Surface"])
    return setup


run(ob, J["views"], J.get("size", 2048), J.get("vis_size", 1024), OUT, [])
if J.get("demark"):
    bake("color", J.get("size", 2048), os.path.join(OUT, "demark.png"), setup_demark(J["demark"]), ob, [])
for ex in J.get("extra", []):
    tgt = next(o for n, o in OBJS.items() if n.startswith(ex["object"]))
    run(tgt, ex["views"], ex.get("size", 512), ex.get("vis_size", ex.get("size", 512)), P(ex["out"]), ex.get("occluders", []))
    if ex.get("demark"):
        bake("color", ex.get("size", 512), os.path.join(P(ex["out"]), "demark.png"), setup_demark(ex["demark"]), tgt, ex.get("occluders", []))
json.dump(REPORT, open(os.path.join(OUT, "project_report.json"), "w"), indent=1)
print("PROJECT DONE")
