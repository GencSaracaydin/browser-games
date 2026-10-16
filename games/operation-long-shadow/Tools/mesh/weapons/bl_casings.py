"""PROC_SHELL_CASINGS: procedural spent cartridge cases (Blender headless), <= 60 tris each, real dimensions.

  Blender -b --factory-startup -P Tools/mesh/weapons/bl_casings.py -- <ready dir> <work dir>
Writes Assets/_Game/Art/Models/Weapons/Casings/SM_WPN_Casings.fbx with one object per calibre:
  SM_WPN_Casing_556x45 (Commando), SM_WPN_Casing_762x39 (AK, RPK, AKMS; lacquered steel),
  SM_WPN_Casing_9x19 (Hush Puppy), SM_WPN_Casing_762x51 (M60D door gun)
and a shared 256 px texture set T_WPN_Casings_{BC,M} (no normal map; smooth normals carry the roundness).
Axis: case axis along Unity +Z (mouth forward), origin at the centre of the case (spin pivot), metres.
UV layout: u 0.00-0.47 brass strip, u 0.53-1.00 lacquered steel strip (v = along the case, mouth at the top),
base/primer discs in the bottom-left (brass) / bottom-right (steel) 0.2 x 0.2 corners.
"""
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector

READY, WORK = sys.argv[sys.argv.index("--") + 1:][:2]
os.makedirs(WORK, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

# real dimensions (mm): profile rings (z, radius) from the base; 'steel' selects the lacquered-steel strip
CASES = {
    "556x45": {"rings": [(0, 4.80), (1.1, 4.80), (1.6, 4.30), (2.3, 4.78), (36.5, 4.50), (39.4, 3.22), (44.7, 3.22)], "seg": 8},
    "762x39": {"rings": [(0, 5.67), (1.3, 5.67), (1.9, 5.00), (2.6, 5.62), (30.5, 5.03), (33.0, 4.30), (38.7, 4.30)], "seg": 8, "steel": True},
    "9x19": {"rings": [(0, 4.98), (1.2, 4.98), (1.8, 4.40), (2.5, 4.96), (19.15, 4.83)], "seg": 8},
    "762x51": {"rings": [(0, 5.97), (1.3, 5.97), (1.9, 5.10), (2.7, 5.95), (39.6, 5.77), (43.4, 4.39), (51.2, 4.39)], "seg": 8},
}
# triangle budget: drop the extractor-groove rings when the full profile would exceed 60 tris
MAXT = 60


def tri_count(nr, seg):
    return (nr - 1) * seg * 2 + (seg - 2) * 2


def build(name, spec):
    rings = list(spec["rings"])
    seg = spec["seg"]
    while tri_count(len(rings), seg) > MAXT:
        if len(rings) > 4:
            del rings[1]  # drop rings next to the base first (rim/groove detail is invisible at this size)
        else:
            seg -= 1
    steel = spec.get("steel", False)
    L = rings[-1][0]
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    vs = []
    for z, r in rings:
        row = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            row.append(bm.verts.new((r * math.cos(a) / 1000, r * math.sin(a) / 1000, (z - L / 2) / 1000)))
        vs.append(row)
    u0, u1 = (0.53, 1.0) if steel else (0.0, 0.47)
    for k in range(len(rings) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            f = bm.faces.new((vs[k][i], vs[k][j], vs[k + 1][j], vs[k + 1][i]))
            for lp, (ii, kk) in zip(f.loops, ((i, k), (i + 1, k), (i + 1, k + 1), (i, k + 1))):
                lp[uvl].uv = (u0 + (u1 - u0) * ii / seg, 0.22 + 0.78 * rings[kk][0] / L)
    # base disc (primer) and recessed mouth cap (dark interior)
    cx = 0.1 if not steel else 0.9
    base = bm.faces.new(list(reversed(vs[0])))
    for lp in base.loops:
        x, y = lp.vert.co.x, lp.vert.co.y
        rr = rings[0][1] / 1000
        lp[uvl].uv = (cx + 0.09 * x / rr, 0.1 + 0.09 * y / rr)
    cap = bm.faces.new(vs[-1])  # mouth: flat cap mapped to a dark interior texel
    for lp in cap.loops:
        lp[uvl].uv = (0.5, 0.02)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(f"SM_WPN_Casing_{name}")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(me.name, me)
    bpy.context.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    # hard edges at the base and mouth caps
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50))
    ob.select_set(False)
    ntri = len(me.polygons)
    mat = bpy.data.materials.get("M_WPN_Casings") or bpy.data.materials.new("M_WPN_Casings")
    me.materials.append(mat)
    print("casing", name, "tris", ntri, "length mm", L, "seg", seg, "rings", len(rings))
    return ob


objs = [build(k, v) for k, v in CASES.items()]
for i, o in enumerate(objs):
    o.location.x = 0  # all at origin; the prefab builder instantiates them separately

# texture atlas 256 px: brass strip, lacquered steel strip, primer discs, dark interior texel
S = 256
rng = np.random.default_rng(3)
bc = np.zeros((S, S, 3), np.float32)
msk = np.zeros((S, S, 4), np.float32)
yy, xx = np.mgrid[0:S, 0:S] / S  # v from top in image rows -> v = 1 - y
v = 1 - yy
brass = np.array([0.80, 0.58, 0.30])
steel = np.array([0.105, 0.098, 0.052])  # olive-brown lacquer over steel (linear albedo, sRGB ~ 92,89,64)
noise = rng.normal(0, 1, (S, S)) * 0.02
for (u0, u1, col, metal, smooth) in ((0.0, 0.5, brass, 1.0, 0.72), (0.5, 1.0, steel, 0.25, 0.45)):
    sel = (xx >= u0) & (xx < u1)
    tint = np.ones((S, S, 1))
    # mouth end darker (powder fouling / heat), slight streaks along the length
    tint *= (1 - 0.35 * np.clip((v - 0.85) / 0.15, 0, 1))[..., None]
    tint *= (1 + noise[..., None] + 0.03 * np.sin(xx * 90)[..., None])
    bc[sel] = (col * tint)[sel]
    msk[sel, 0] = metal
    msk[sel, 3] = smooth
# primer discs (base): case head brass/steel ring with a copper-coloured primer cup in the centre
for cx, col, metal in ((0.1, brass, 1.0), (0.9, steel, 0.25)):
    d = np.sqrt((xx - cx) ** 2 + (v - 0.1) ** 2)
    sel = d < 0.1
    bc[sel] = col * 0.95
    pr = d < 0.035
    bc[pr] = np.array([0.72, 0.45, 0.30])
    ring = (d > 0.033) & (d < 0.04)
    bc[ring] *= 0.5
    msk[sel, 0] = metal
    msk[pr, 0] = 1.0
    msk[sel, 3] = 0.6
# dark interior texel region (around u 0.5, v 0.02)
sel = (np.abs(xx - 0.5) < 0.03) & (v < 0.05)
bc[sel] = 0.05
msk[sel, 0] = 0.0
msk[sel, 3] = 0.2
msk[..., 1] = 1.0  # AO
from bpy import data as D
tdir = os.path.join(READY, "Assets/_Game/Art/Textures/Weapons/Casings")
os.makedirs(tdir, exist_ok=True)


def save_png(arr, path, alpha):
    h, w = arr.shape[:2]
    img = D.images.new(os.path.basename(path), w, h, alpha=alpha)
    px = np.ones((h, w, 4), np.float32)
    px[..., :arr.shape[2]] = arr
    img.pixels.foreach_set(px[::-1].ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


# BC: stored as sRGB; generated values above are linear-ish albedo, convert
save_png(np.clip(bc, 0, 1) ** (1 / 2.2), os.path.join(tdir, "T_WPN_Casings_BC.png"), False)
save_png(np.clip(msk, 0, 1), os.path.join(tdir, "T_WPN_Casings_M.png"), True)

mdir = os.path.join(READY, "Assets/_Game/Art/Models/Weapons/Casings")
os.makedirs(mdir, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for o in objs:
    o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
# Blender +Z (case axis) must become Unity +Z: rotate so the case axis lies along Blender -Y before export
for o in objs:
    o.data.transform(__import__("mathutils").Matrix.Rotation(math.radians(90), 4, "X"))
bpy.ops.export_scene.fbx(filepath=os.path.join(mdir, "SM_WPN_Casings.fbx"), use_selection=True, object_types={"MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                         bake_space_transform=True, mesh_smooth_type="OFF", use_triangles=True, path_mode="STRIP",
                         embed_textures=False, add_leaf_bones=False, bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(WORK, "casings.blend"))
print("DONE casings")
