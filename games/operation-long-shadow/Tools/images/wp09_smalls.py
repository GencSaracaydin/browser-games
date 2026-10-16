"""PROC_BOOTH_SMALLS: procedural low-poly booth smalls (Blender headless), sharing M_BoothParts (wp09_parts_atlas.py).

  Blender -b --factory-startup --python-exit-code 1 -P Tools/images/wp09_smalls.py
Writes RawAssets/wp09/ready/Assets/_Game/Art/Models/Props/booth_smalls/SM_{BoothPen,Electrode,ElectrodeWire,Cigarette}.fbx
and RawAssets/wp09/PROC_BOOTH_SMALLS/stats.json. Real-world metres, Blender front -Y / up +Z -> FBX forward -Z, up Y.
  SM_BoothPen      steel click pen (0.135 m) lying along X, pivot = centre of its underside (the four-tap emitter).
  SM_Electrode     rubber electrode pad 45 x 32 mm with snap stud, two crossed cloth-tape strips and a 0.16 m lead
                   (red + black); pad in the XY plane facing +Z (= Unity +Y), lead leaves along +X. Parent to the forearm bone.
  SM_ElectrodeWire 0.95 m twisted red/black pair hanging from the chair arm (origin) to the floor toward -Y/+X.
  SM_Cigarette     lit filter cigarette (84 mm) along X, ember ring in its own slot M_CigaretteEmber, child empty SmokeAnchor at the tip.
"""
import bpy, bmesh, json, math, os, sys
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "RawAssets/wp09/ready/Assets/_Game/Art/Models/Props/booth_smalls")
ATLAS = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "wp09_parts_atlas.json")))
SW = {k: v["uv"] for k, v in ATLAS["swatches"].items()}
stats = {}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def lathe(bm, profile, segs, swatch, axis="X", offset=Vector()):
    """Revolve profile [(t, r)...] around the axis; per-ring swatch list or one swatch; caps closed if r==0 at ends."""
    lay = bm.loops.layers.uv.verify()
    rings = []
    for t, r in profile:
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            if axis == "X":
                p = Vector((t, r * math.cos(a), r * math.sin(a)))
            else:
                p = Vector((r * math.cos(a), r * math.sin(a), t))
            ring.append(bm.verts.new(p + offset) if r > 1e-7 else None)
        rings.append(ring if r > 1e-7 else bm.verts.new((Vector((t, 0, 0)) if axis == "X" else Vector((0, 0, t))) + offset))
    faces = []
    for k in range(len(rings) - 1):
        A, B = rings[k], rings[k + 1]
        sw = swatch[k] if isinstance(swatch, list) else swatch
        for i in range(segs):
            j = (i + 1) % segs
            if isinstance(A, list) and isinstance(B, list):
                f = bm.faces.new((A[i], A[j], B[j], B[i]))
            elif isinstance(A, list):
                f = bm.faces.new((A[i], A[j], B))
            else:
                f = bm.faces.new((A, B[j], B[i]))
            for l in f.loops:
                l[lay].uv = SW[sw]
            faces.append(f)
    return faces


def box(bm, c, size, swatch, rot=Matrix.Identity(3)):
    lay = bm.loops.layers.uv.verify()
    sx, sy, sz = (s / 2 for s in size)
    vs = [bm.verts.new(Vector(c) + rot @ Vector((x * sx, y * sy, z * sz))) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
    for q in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        f = bm.faces.new([vs[i] for i in q])
        for l in f.loops:
            l[lay].uv = SW[swatch]


def tube_path(bm, pts, r, segs, swatch):
    lay = bm.loops.layers.uv.verify()
    rings = []
    for k, p in enumerate(pts):
        d = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
        a = d.orthogonal().normalized(); b = d.cross(a).normalized()
        if rings:  # keep the frame from twisting
            prev_a = rings[-1][1]
            a = (prev_a - d * prev_a.dot(d)).normalized(); b = d.cross(a)
        rings.append(([bm.verts.new(p + (a * math.cos(2 * math.pi * i / segs) + b * math.sin(2 * math.pi * i / segs)) * r) for i in range(segs)], a))
    for k in range(len(rings) - 1):
        A, B = rings[k][0], rings[k + 1][0]
        for i in range(segs):
            j = (i + 1) % segs
            f = bm.faces.new((A[i], A[j], B[j], B[i]))
            for l in f.loops:
                l[lay].uv = SW[swatch]


def make_obj(name, bm, mats):
    me = bpy.data.meshes.new(name); bm.normal_update(); bm.to_mesh(me); bm.free()
    for m in mats:
        me.materials.append(mat(m))
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    me.shade_smooth(); me.set_sharp_from_angle(angle=math.radians(50))
    return ob


def export(objs, fn):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, fn), use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type="OFF", use_tspace=False,
                             path_mode="STRIP", embed_textures=False, add_leaf_bones=False, bake_anim=False, use_custom_props=False)
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH"}
    dims = {o.name: [round(v, 4) for v in o.dimensions] for o in objs if o.type == "MESH"}
    stats[fn] = {"tris": tris, "dims_blender_m": dims}


def pen():
    reset(); bm = bmesh.new()
    r = 0.0045; L = 0.135
    prof = [(-L / 2, 0.0), (-L / 2, 0.0028), (-L / 2 + 0.004, r * 0.95), (-L / 2 + 0.03, r), (0.018, r), (0.02, r * 1.02),
            (L / 2 - 0.018, r * 0.97), (L / 2 - 0.006, 0.0022), (L / 2 - 0.001, 0.0006), (L / 2, 0.0)]
    sw = ["chrome", "chrome", "steel", "steel", "chrome", "steel", "chrome", "chrome", "chrome"]
    lathe(bm, prof, 12, sw, "X", Vector((0, 0, r)))
    lathe(bm, [(-L / 2 - 0.007, 0.0), (-L / 2 - 0.007, 0.0024), (-L / 2, 0.0024), (-L / 2, 0.0)], 10, "chrome", "X", Vector((0, 0, r)))  # click button
    box(bm, (-L / 2 + 0.024, 0, 2 * r + 0.0009), (0.042, 0.0032, 0.0012), "chrome")                     # clip
    box(bm, (-L / 2 + 0.006, 0, 2 * r - 0.0004), (0.008, 0.0036, 0.0016), "chrome")
    ob = make_obj("SM_BoothPen", bm, ["M_BoothParts"])
    export([ob], "SM_BoothPen.fbx")


def electrode():
    reset(); bm = bmesh.new()
    # rounded pad (lathe-free: an n-gon slab)
    lay = bm.loops.layers.uv.verify()
    w, h, t, rc = 0.045, 0.032, 0.0028, 0.008
    pts = []
    for cx, cy, a0 in ((w / 2 - rc, h / 2 - rc, 0), (-w / 2 + rc, h / 2 - rc, 90), (-w / 2 + rc, -h / 2 + rc, 180), (w / 2 - rc, -h / 2 + rc, 270)):
        for k in range(4):
            a = math.radians(a0 + k * 30)
            pts.append((cx + rc * math.cos(a), cy + rc * math.sin(a)))
    top = [bm.verts.new((x, y, t)) for x, y in pts]; bot = [bm.verts.new((x, y, 0)) for x, y in pts]
    for f in (bm.faces.new(top), bm.faces.new(bot[::-1])):
        for l in f.loops:
            l[lay].uv = SW["gel_pad"]
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((bot[i], bot[j], top[j], top[i]))
        for l in f.loops:
            l[lay].uv = SW["gel_pad"]
    lathe(bm, [(t, 0.0045), (t + 0.0025, 0.0045), (t + 0.0035, 0.003), (t + 0.0035, 0.0)], 10, "steel", "Z")   # snap stud
    for ang in (-24, 21):                                                                                       # cloth tape
        box(bm, (0.002, 0, t + 0.0012), (0.075, 0.013, 0.0006), "cloth_tape", Matrix.Rotation(math.radians(ang), 3, "Z"))
    for k, (sw, dy) in enumerate((("red_pvc", 0.0013), ("black_pvc", -0.0013))):                               # lead
        pts3 = [Vector((0.0, dy, t + 0.005)), Vector((0.012, dy, t + 0.004)), Vector((0.04, dy + 0.002, t + 0.002)),
                Vector((0.08, dy + 0.004, t + 0.0)), Vector((0.12, dy + 0.003, t - 0.004)), Vector((0.16, dy, t - 0.012))]
        tube_path(bm, pts3, 0.0011, 6, sw)
    ob = make_obj("SM_Electrode", bm, ["M_BoothParts"])
    export([ob], "SM_Electrode.fbx")


def wire():
    reset(); bm = bmesh.new()
    N = 22; L = 0.95
    for k, (sw, ph) in enumerate((("red_pvc", 0.0), ("black_pvc", math.pi))):
        pts = []
        for i in range(N):
            u = i / (N - 1)
            # sag off the armrest (0) to the floor 0.68 below, drifting toward the trolley (-Y front, +X)
            x = 0.12 * u + 0.03 * math.sin(u * 7); y = -0.38 * u; z = -0.68 * (u ** 1.4) + 0.06 * math.sin(u * math.pi)
            tw = 2.0 * math.pi * u * 9 + ph                                                       # loose twist, 9 turns
            pts.append(Vector((x + 0.0016 * math.cos(tw), y, z + 0.0016 * math.sin(tw))))
        pts.append(pts[-1] + Vector((0.18, -0.12, 0.0)))                                        # lies on the floor
        tube_path(bm, pts, 0.0012, 5, sw)
    ob = make_obj("SM_ElectrodeWire", bm, ["M_BoothParts"])
    export([ob], "SM_ElectrodeWire.fbx")


def cigarette():
    reset()
    bm = bmesh.new(); r = 0.0039; L = 0.084
    prof = [(0.0, 0.0), (0.0, r * 0.97), (0.0015, r), (0.021, r), (0.0215, r * 1.003), (L - 0.012, r * 0.99), (L - 0.0035, r * 0.95)]
    sw = ["cork_filter", "cork_filter", "cork_filter", "cig_paper", "cig_paper", "ash"]
    lathe(bm, prof, 10, sw, "X", Vector((-L / 2, 0, r)))
    ob = make_obj("SM_Cigarette", bm, ["M_BoothParts", "M_CigaretteEmber"])
    # ember cap in slot 1
    bm2 = bmesh.new(); bm2.from_mesh(ob.data)
    before = set(bm2.faces)
    lathe(bm2, [(L - 0.0035, r * 0.95), (L - 0.0012, r * 0.8), (L, 0.0)], 10, "ember", "X", Vector((-L / 2, 0, r)))
    for f in bm2.faces:
        if f not in before:
            f.material_index = 1
    bm2.to_mesh(ob.data); bm2.free()
    ob.data.shade_smooth()
    emp = bpy.data.objects.new("SmokeAnchor", None); bpy.context.collection.objects.link(emp)
    emp.parent = ob; emp.location = (L / 2 + 0.001, 0, r)
    export([ob, emp], "SM_Cigarette.fbx")
    stats["SM_Cigarette.fbx"]["SmokeAnchor_unity"] = [-(L / 2 + 0.001), r, 0.0]


for fn in (pen, electrode, wire, cigarette):
    fn()
os.makedirs(os.path.join(ROOT, "RawAssets/wp09/PROC_BOOTH_SMALLS"), exist_ok=True)
json.dump(stats, open(os.path.join(ROOT, "RawAssets/wp09/PROC_BOOTH_SMALLS/stats.json"), "w"), indent=1)
print("SMALLS", json.dumps(stats))
