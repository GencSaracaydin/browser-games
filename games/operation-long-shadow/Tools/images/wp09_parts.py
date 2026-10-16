"""Moving child parts for the WP09 fal booth props. Executed inside vendor/blender_prop.py (spec "extras_script")
after LOD0 exists and before the FBX export, with globals bpy, bmesh, np, math, Vector, Matrix, objs, lod0, spec,
stats, tri_count, log. Coordinates are Blender metres of the processed prop (front -Y, up +Z, pivot bottom centre),
measured on Tools/images/bl_ortho.py renders and bl_uvprobe.py hits (RawAssets/wp09/_ortho/).

Parts use the shared material M_BoothParts (atlas Tools/images/wp09_parts_atlas.py). Unity mapping of a Blender
point (x, y, z) is (-x, z, -y); a part's pivot is its object origin.
  ScramblerBox: Toggle (bat lever, ON pose = tilted up), Needle (VU needle, rest pose = full left).
  RotaryPhone : Handset split off the scanned body (pivot at its centre), holes capped.
  ReelToReel  : ReelLeft / ReelRight (7-inch metal reels, spin about local up), TapePackLeft / TapePackRight
                (children of the reels; scale X/Z = pack radius, 1.0 full, 0.42 empty), TapePath (static ribbon).
"""
import json, os
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(spec["extras_script"])
ATLAS = json.load(open(os.path.join(HERE, "wp09_parts_atlas.json")))
SW = {k: v["uv"] for k, v in ATLAS["swatches"].items()}
PM = bpy.data.materials.get("M_BoothParts") or bpy.data.materials.new("M_BoothParts")
parts = stats.setdefault("parts", {})


def to_unity(p):
    return [round(-p[0], 5), round(p[2], 5), round(-p[1], 5)]


def finish(bm, name, pivot, parent=None, note=""):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    me.materials.append(PM)
    par = parent or lod0
    ob.parent = par
    ob.location = Vector(pivot)
    objs.append(ob)
    parts[name] = {"pivot_blender": [round(v, 5) for v in pivot], "pivot_unity": to_unity(pivot),
                   "parent": par.name, "tris": sum(len(f.vertices) - 2 for f in me.polygons), "note": note}
    return ob


def uv_fill(bm, uv):
    lay = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for l in f.loops:
            l[lay].uv = uv
    return lay


def tube(bm, segs, rings, axis_pts, uv=None):
    """rings: list of radii, axis_pts: list of centres (Vector) along the local axis; returns created faces."""
    lay = bm.loops.layers.uv.verify()
    loops = []
    for c, r, d in rings:
        # d: direction of the axis at this ring
        d = d.normalized()
        a = d.orthogonal().normalized(); b = d.cross(a).normalized()
        loops.append([bm.verts.new(c + (a * math.cos(2 * math.pi * i / segs) + b * math.sin(2 * math.pi * i / segs)) * r)
                      for i in range(segs)])
    faces = []
    for k in range(len(loops) - 1):
        for i in range(segs):
            j = (i + 1) % segs
            faces.append(bm.faces.new((loops[k][i], loops[k][j], loops[k + 1][j], loops[k + 1][i])))
    for cap, flip in ((loops[0], True), (loops[-1], False)):
        f = bm.faces.new(cap[::-1] if flip else cap)
        faces.append(f)
    if uv:
        for f in faces:
            for l in f.loops:
                l[lay].uv = uv
    return faces


# ---------------------------------------------------------------- ScramblerBox
def scrambler():
    dz = (spec.get("post_translate") or [0, 0, 0])[2]
    # Toggle: bat lever out of the jack bushing at panel lower-left. ON pose = tilted 25 deg up.
    P = Vector((-0.027, -0.0915, 0.075 + dz))
    bm = bmesh.new()
    rot = Matrix.Rotation(math.radians(-25), 3, "X")
    out = rot @ Vector((0, -1, 0))
    rings = [(Vector((0, 0.0016, 0)), 0.0042, Vector((0, -1, 0))), (Vector((0, -0.0012, 0)), 0.0042, Vector((0, -1, 0)))]
    tube(bm, 12, rings, None, SW["chrome"])                     # collar
    rings = [(out * 0.0, 0.0022, out), (out * 0.019, 0.0025, out), (out * 0.0255, 0.0031, out), (out * 0.027, 0.0016, out)]
    tube(bm, 10, rings, None, SW["chrome"])                     # bat
    finish(bm, "Toggle", P, note="Bat-lever toggle, ON = as exported (lever up). OFF: rotate localEulerAngles.x by +50 "
                                  "(Unity: +X pitch turns the lever down). Click SFX on the flip; pivot is the bushing.")
    # VU needle: pivot under the bezel lip; rest pose = full left (-47 deg as seen from the front).
    N = Vector((-0.021, -0.0885, 0.116 + dz))
    bm = bmesh.new()
    lay = bm.loops.layers.uv.verify()
    w0, w1, t, L = 0.0007, 0.00035, 0.00025, 0.0315
    vs = [bm.verts.new(v) for v in ((-w0, 0, 0.0), (w0, 0, 0.0), (w1, 0, L), (-w1, 0, L),
                                     (-w0, t, 0.0), (w0, t, 0.0), (w1, t, L), (-w1, t, L))]
    for q in ((0, 1, 2, 3), (5, 4, 7, 6), (4, 0, 3, 7), (1, 5, 6, 2), (3, 2, 6, 7), (4, 5, 1, 0)):
        bm.faces.new([vs[i] for i in q])
    uv_fill(bm, SW["matte_black"])
    tube(bm, 10, [(Vector((0, -0.0002, 0)), 0.0022, Vector((0, -1, 0))), (Vector((0, 0.0006, 0)), 0.0022, Vector((0, -1, 0)))],
         None, SW["matte_black"])
    bmesh.ops.rotate(bm, verts=bm.verts, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.radians(-47), 3, "Y"))
    finish(bm, "Needle", N, note="VU needle, exported at rest (full left). Drive localEulerAngles.z = 94 * level "
                                  "(0..1; positive swings it right as seen from the front, verify sign once in the editor). "
                                  "Outro toggle OFF: ease to 0 over 0.15 s.")


# ---------------------------------------------------------------- RotaryPhone
def phone():
    me = lod0.data
    bm = bmesh.new(); bm.from_mesh(me)
    sel = set()
    for f in bm.faces:
        c = f.calc_center_median()
        end = (c.x < -0.048 or c.x > 0.072) and c.z > 0.086 and c.y > -0.035
        mid = (-0.048 <= c.x <= 0.072) and c.z > 0.137 and c.y > -0.03
        if end or mid:
            sel.add(f.index)
    hb = bm.copy()
    bmesh.ops.delete(hb, geom=[f for f in hb.faces if f.index not in sel], context="FACES")
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index in sel], context="FACES")
    for b in (bm, hb):
        lay = b.loops.layers.uv.active
        edges = [e for e in b.edges if e.is_boundary]
        try:
            res = bmesh.ops.holes_fill(b, edges=edges, sides=0)
            bmesh.ops.triangulate(b, faces=res["faces"])
        except Exception as ex:  # noqa
            log("holes_fill failed", ex)
        newf = [f for f in b.faces if any(l[lay].uv.length == 0 for l in f.loops)] if lay else []
        for f in newf:   # capped faces: borrow a neighbouring loop's UV (black bakelite)
            nb = next((l2 for l in f.loops for l2 in l.vert.link_loops if l2.face is not f and l2[lay].uv.length > 0), None)
            if nb is not None:
                for l in f.loops:
                    l[lay].uv = nb[lay].uv.copy()
    bm.to_mesh(me); bm.free(); me.update()
    co = [v.co for v in hb.verts]
    mn = Vector((min(v.x for v in co), min(v.y for v in co), min(v.z for v in co)))
    mx = Vector((max(v.x for v in co), max(v.y for v in co), max(v.z for v in co)))
    piv = (mn + mx) / 2
    bmesh.ops.translate(hb, verts=hb.verts, vec=-piv)
    hme = bpy.data.meshes.new("Handset"); hb.to_mesh(hme); hb.free()
    ob = bpy.data.objects.new("Handset", hme); bpy.context.collection.objects.link(ob)
    hme.materials.append(lod0.data.materials[0] if lod0.data.materials else PM)
    ob.parent = lod0; ob.location = piv
    objs.append(ob)
    parts["Handset"] = {"pivot_blender": [round(v, 5) for v in piv], "pivot_unity": to_unity(piv), "parent": lod0.name,
                        "tris": sum(len(f.vertices) - 2 for f in hme.polygons), "size_m": [round(v, 4) for v in (mx - mn)],
                        "note": "Handset split from the scan (same material M_RotaryPhone); lift it as a whole. The top of the coiled "
                                "cord stays on the handset, the rest on the body: keep the lift framed away from the cord or hide the gap."}


# ---------------------------------------------------------------- ReelToReel
def reel_mesh(segs=30, R=0.089, r_hub=0.029, r_win0=0.036, r_win1=0.078, t=0.0012, gap=0.007):
    """7-inch metal reel: two two-sided flanges with three windows, rolled rim strips, core cylinder. Local origin =
    centre of the bottom flange's underside; spin axis +Z."""
    bm = bmesh.new()
    lay = bm.loops.layers.uv.verify()
    fl = ATLAS["flange"]; cu, cv = fl["uv_centre"]; ur = fl["uv_radius"]
    radii = [0.0048, r_win0, r_win1, R]
    win = [(0, 6), (10, 16), (20, 26)]               # segment ranges that are windows (of 30): 72 deg windows, 48 deg spokes
    def is_win(i):
        return any(a <= i < b for a, b in win)
    for z0 in (0.0, t + gap):
        for zz, up in ((z0 + t, True),):   # upper faces only: the undersides face the deck / the pack and are never seen
            grid = [[bm.verts.new((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), zz))
                     for i in range(segs)] for r in radii]
            for k in range(len(radii) - 1):
                for i in range(segs):
                    if k == 1 and is_win(i):
                        continue
                    j = (i + 1) % segs
                    q = (grid[k][i], grid[k][j], grid[k + 1][j], grid[k + 1][i])
                    f = bm.faces.new(q[::-1] if up else q)
                    for l in f.loops:
                        p = l.vert.co
                        l[lay].uv = (cu + p.x / R * ur, cv + p.y / R * ur)
        # rim strip and spindle-hole strip joining the two faces of this flange
        for r, outward in ((R, True),):   # the spindle-hole wall (1.2 mm) is never visible: skipped for budget
            ring0 = [bm.verts.new((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), z0)) for i in range(segs)]
            ring1 = [bm.verts.new((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), z0 + t)) for i in range(segs)]
            for i in range(segs):
                j = (i + 1) % segs
                q = (ring0[i], ring0[j], ring1[j], ring1[i])
                f = bm.faces.new(q if outward else q[::-1])
                for l in f.loops:
                    l[lay].uv = SW["brushed_alu"]
    # core cylinder between the flanges (visible through the windows when the pack is small)
    ring0 = [bm.verts.new((r_hub * math.cos(2 * math.pi * i / segs), r_hub * math.sin(2 * math.pi * i / segs), t)) for i in range(segs)]
    ring1 = [bm.verts.new((r_hub * math.cos(2 * math.pi * i / segs), r_hub * math.sin(2 * math.pi * i / segs), t + gap)) for i in range(segs)]
    for i in range(segs):
        j = (i + 1) % segs
        f = bm.faces.new((ring0[i], ring0[j], ring1[j], ring1[i]))
        for l in f.loops:
            l[lay].uv = SW["hub_alu"]
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    return bm


def pack_mesh(segs=30, R=0.075, z0=0.00125, z1=0.00815):
    bm = bmesh.new(); lay = bm.loops.layers.uv.verify()
    tp = ATLAS["tape"]; cu, cv = tp["uv_centre"]; ur = tp["uv_radius"]
    top = [bm.verts.new((R * math.cos(2 * math.pi * i / segs), R * math.sin(2 * math.pi * i / segs), z1)) for i in range(segs)]
    bot = [bm.verts.new((R * math.cos(2 * math.pi * i / segs), R * math.sin(2 * math.pi * i / segs), z0)) for i in range(segs)]
    f = bm.faces.new(top)
    for l in f.loops:
        l[lay].uv = (cu + l.vert.co.x / R * ur, cv + l.vert.co.y / R * ur)
    for i in range(segs):
        j = (i + 1) % segs
        f = bm.faces.new((bot[i], bot[j], top[j], top[i]))
        for l in f.loops:
            l[lay].uv = SW["tape_oxide"]
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    return bm


def tangent(P, C, r, side):
    """Tangent point from P to circle (C, r); side +1/-1 picks one of the two."""
    d = (P - C); L = d.length
    a = math.acos(min(1.0, r / L)); base = math.atan2(d.y, d.x)
    ang = base + side * a
    return Vector((C.x + r * math.cos(ang), C.y + r * math.sin(ang), P.z))


def reels():
    bvh = BVHTree.FromObject(lod0, bpy.context.evaluated_depsgraph_get())
    cents = {"Left": Vector((-0.104, 0.066, 0)), "Right": Vector((0.105, 0.064, 0))}
    pack_scale = {"Left": 1.0, "Right": 0.42}
    piv = {}
    for side, c in cents.items():
        zs = []
        for dx, dy in ((0.03, 0), (-0.03, 0), (0, 0.03), (0, -0.03)):
            hit = bvh.ray_cast(Vector((c.x + dx, c.y + dy, 0.5)), Vector((0, 0, -1)))
            if hit[0] is not None:
                zs.append(hit[0].z)
        piv[side] = max(zs) if zs else 0.12
    zt = max(piv.values()) + 0.0015            # both reels on one plane (the scanned reel tables differ by ~3 mm)
    for side, c in cents.items():
        P = Vector((c.x, c.y, zt)); piv[side] = P
        reel = finish(reel_mesh(), f"Reel{side}", P, note="7-inch metal reel; spin about local Y (Unity up). Playback: supply (left) "
                      "and take-up (right) both turn counter-clockwise seen from above (tape threaded on the outer sides), i.e. a negative "
                      "localEulerAngles.y rate in Unity; deg/s = ips*0.0254/(2*pi*r_pack)*360, 7.5 ips, r_pack = 0.075*packScale (m).")
        pk = pack_mesh()
        me = bpy.data.meshes.new(f"TapePack{side}"); pk.to_mesh(me); pk.free()
        ob = bpy.data.objects.new(f"TapePack{side}", me); bpy.context.collection.objects.link(ob); me.materials.append(PM)
        # flat hierarchy: FBX bake_space_transform mis-rotates grandchildren, so the pack is a sibling of its reel
        ob.parent = lod0; ob.location = P; ob.scale = (pack_scale[side], pack_scale[side], 1.0)
        objs.append(ob)
        parts[f"TapePack{side}"] = {"pivot_blender": [round(v, 5) for v in P], "pivot_unity": to_unity(P), "parent": lod0.name,
                                    "tris": sum(len(f.vertices) - 2 for f in me.polygons), "scale_unity": [pack_scale[side], 1, pack_scale[side]],
                                    "note": f"Tape pack radius = 0.075 m x localScale.x (= z). Full 1.0, empty 0.42 (hub). Animate supply down / take-up up. Sibling of Reel{side} at the same pivot (radially symmetric, so it need not spin; reparent under the reel in the prefab if wanted)."}
    # static tape ribbon: supply pack -> left idler -> head block -> right idler -> take-up pack
    zc = (piv["Left"].z + piv["Right"].z) / 2 + 0.0047
    h = 0.00318
    L, Rr = piv["Left"], piv["Right"]
    i1, i2 = Vector((-0.118, -0.052, zc)), Vector((0.122, -0.052, zc))
    tL = tangent(i1, Vector((L.x, L.y, zc)), 0.075 * pack_scale["Left"], +1)
    tR = tangent(i2, Vector((Rr.x, Rr.y, zc)), 0.075 * pack_scale["Right"], -1)
    if tL.x > L.x: tL = tangent(i1, Vector((L.x, L.y, zc)), 0.075 * pack_scale["Left"], -1)
    if tR.x < Rr.x: tR = tangent(i2, Vector((Rr.x, Rr.y, zc)), 0.075 * pack_scale["Right"], +1)
    pts = [tL, i1, i2, tR]
    bm = bmesh.new(); lay = bm.loops.layers.uv.verify()
    for a, b in zip(pts[:-1], pts[1:]):
        for back in (False, True):
            v = [bm.verts.new(a + Vector((0, 0, -h))), bm.verts.new(b + Vector((0, 0, -h))), bm.verts.new(b + Vector((0, 0, h))), bm.verts.new(a + Vector((0, 0, h)))]
            f = bm.faces.new(v[::-1] if back else v)
            for l in f.loops:
                l[lay].uv = SW["tape_oxide"]
    finish(bm, "TapePath", Vector((0, 0, 0)), note="Static tape ribbon threaded from the supply pack via the idlers past the head block to the take-up pack (drawn for full supply / empty take-up).")


fn = {"ScramblerBox": scrambler, "RotaryPhone": phone, "ReelToReel": reels}.get(spec["name"])
if fn:
    fn()
    log("parts", json.dumps(parts))
