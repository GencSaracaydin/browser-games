"""WP03 weapon processing in Blender (headless).

  Blender -b --factory-startup -t 3 -P Tools/mesh/weapons/bl_weapon.py -- Tools/mesh/weapons/jobs/WPN_AK.json

Steps (job JSON keys in brackets):
 1. import the body GLB [body], join, apply transforms; orient (barrel along Blender -Y, up +Z, the weapon's left
    side facing +X; Tripo already uses this when the reference shows the left side with the muzzle to the left);
    centre X on the median plane; scale uniformly to the real length [length] (along Y, or Z with length_axis "Z").
 2. clean: merge by distance (1e-5 x length), delete loose verts/edges, recalc normals (make consistent), delete
    floaters (tiny components away from the main body); restore the original shading by transferring the custom
    split normals from the pristine import, so the generator's tangent-space normal map stays valid.
 3. decimate (collapse) to the budgets [fp.tris] / [world.tris], LODs [world.lods] (ratios of LOD0).
 4. magazine [mag]: import, orient, rotate to the magazine axis of the reference, scale and place it in the
    magazine well from the full-vs-no-magazine image mask (or a manual anchor), origin at the well entry point.
 5. sockets [sockets_img] from reference-image pixels (image x -> +Y, image y -> -Z, mm per pixel from the body
    length); pivot (object origin) at [pivot] (the pistol grip for hand-held weapons).
 6. bake ambient occlusion (Cycles) for the mask map G channel [bake_ao].
 7. export FBX (external textures, Unity axis: Blender -Y -> Unity +Z forward, +Z -> +Y up, 1 unit = 1 m,
    transforms baked so every object imports with identity rotation and scale 1).
Unity convention of the results: barrel along +Z, up +Y, the weapon's left side towards -X, origin at the grip.
"""
import bpy, bmesh, json, math, os, sys, time
import numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_backface import all_visible_backfaces, double_faces, fix_all  # noqa: E402

T0 = time.time()
JOB = json.load(open(sys.argv[sys.argv.index("--") + 1]))
ROOT = JOB.get("root", "/Users/gencsaracaydin/Documents/fun/fun-unity")
P = lambda p: p if os.path.isabs(p) else os.path.join(ROOT, p)
WORK = P(JOB["work"])
READY = P(JOB.get("ready", "RawAssets/wp03/ready"))
os.makedirs(WORK, exist_ok=True)
REPORT = {"id": JOB["id"], "steps": []}


def log(*a):
    s = " ".join(str(x) for x in a)
    print(f"[{time.time() - T0:7.1f}s] {s}", flush=True)
    REPORT["steps"].append(s)


bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.threads_mode = "FIXED"
scene.render.threads = 3
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0


# ---------------------------------------------------------------- helpers
def coords(ob):
    a = np.empty(len(ob.data.vertices) * 3)
    ob.data.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def import_glb(path, name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=P(path))
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    for o in meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    for o in new:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    select_only(meshes)
    if len(meshes) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    ob.name = name
    ob.data.name = name
    for p in ob.data.polygons:
        p.use_smooth = True
    log("imported", path, "verts", len(ob.data.vertices), "tris", tris(ob))
    return ob


def duplicate(ob, name):
    d = ob.copy()
    d.data = ob.data.copy()
    d.name = name
    d.data.name = name
    bpy.context.collection.objects.link(d)
    return d


def apply_modifier(ob, mod):
    select_only([ob])
    bpy.ops.object.modifier_apply(modifier=mod.name)


def end_heights(co, ax=1, frac=0.04):
    lo, hi = co[:, ax].min(), co[:, ax].max()
    L = hi - lo
    a = co[co[:, ax] < lo + frac * L]
    b = co[co[:, ax] > hi - frac * L]
    return (a[:, 2].max() - a[:, 2].min()), (b[:, 2].max() - b[:, 2].min())


def orient(ob, mode, img_end_ratio=None):
    """mode 'side': long axis -> Y with the muzzle at -Y. 'upright': keep as generated (grenades)."""
    if mode == "upright":
        return
    co = coords(ob)
    ext = co.max(0) - co.min(0)
    ax = int(np.argmax(ext))
    if ax == 0:
        ob.data.transform(Matrix.Rotation(math.radians(90), 4, "Z"))
        log("orient: long axis was X, rotated 90 about Z")
    elif ax == 2:
        log("orient: WARNING long axis is Z; keeping (check the reference)")
    co = coords(ob)
    hl, hr = end_heights(co)
    flip = False
    if img_end_ratio is not None:
        r3 = hl / max(hr, 1e-6)
        if (img_end_ratio < 1) != (r3 < 1) and (abs(math.log(max(img_end_ratio, 1e-3))) > 0.3 and abs(math.log(max(r3, 1e-3))) > 0.3):
            flip = True
        log(f"orient: image end ratio {img_end_ratio:.2f}, mesh end ratio {r3:.2f}, flip {flip}")
    if flip:
        ob.data.transform(Matrix.Rotation(math.radians(180), 4, "Z"))


def normalise(ob, length, axis="Y"):
    co = coords(ob)
    ai = "XYZ".index(axis)
    ext = co.max(0) - co.min(0)
    s = length / ext[ai]
    cx = float(np.median(co[:, 0]))
    cy = (co[:, 1].max() + co[:, 1].min()) / 2
    cz = (co[:, 2].max() + co[:, 2].min()) / 2
    ob.data.transform(Matrix.Translation((-cx, -cy, -cz)))
    ob.data.transform(Matrix.Scale(s, 4))
    co = coords(ob)
    log("normalised: scale", round(s, 4), "size m", [round(float(v), 4) for v in co.max(0) - co.min(0)])
    return s


def components(bm):
    bm.faces.ensure_lookup_table()
    seen = set()
    comps = []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack = [f]
        seen.add(f.index)
        comp = []
        while stack:
            g = stack.pop()
            comp.append(g)
            for v in g.verts:
                for h in v.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        comps.append(comp)
    return comps


def flip_by_shading_normals(ob):
    """Re-orient faces so the winding agrees with the generator's own vertex normals (reliable outward direction
    on these non-manifold meshes, where topological 'recalc outside' guesses wrong)."""
    me = ob.data
    nl = len(me.loops)
    cn = np.empty(nl * 3)
    try:
        me.corner_normals.foreach_get("vector", cn)
    except Exception:
        me.loops.foreach_get("normal", cn)
    cn = cn.reshape(-1, 3)
    fn = np.empty(len(me.polygons) * 3)
    me.polygons.foreach_get("normal", fn)
    fn = fn.reshape(-1, 3)
    ls = np.empty(len(me.polygons), int)
    me.polygons.foreach_get("loop_start", ls)
    tot = np.empty(len(me.polygons), int)
    me.polygons.foreach_get("loop_total", tot)
    acc = np.add.reduceat(cn, ls, axis=0) if len(ls) else cn
    bad = np.einsum("ij,ij->i", acc, fn) < 0
    if bad.any():
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        bmesh.ops.reverse_faces(bm, faces=[bm.faces[i] for i in np.nonzero(bad)[0]])
        bm.to_mesh(me)
        bm.free()
        me.update()
    log(f"orient faces: {int(bad.sum())} faces flipped to match the shading normals")


def fix_orientation(ob, rays=24, seed=0, two_sided=True):
    """Make every face's winding point outwards, also on generator meshes whose parts are inside-out (Tripo low
    face counts turn whole receivers inside-out, with matching inward vertex normals, so they vanish under backface
    culling). Per face: count rays (cosine-weighted hemisphere) that escape the mesh on the +normal side vs the
    -normal side; flip when clearly more escape behind it. The generator's smooth normals are kept (negated on the
    flipped region) as custom normals. Run before the pristine copy so normal transfers stay consistent."""
    me = ob.data
    nl = len(me.loops)
    cn = np.empty(nl * 3)
    try:
        me.corner_normals.foreach_get("vector", cn)
    except Exception:
        me.loops.foreach_get("normal", cn)
    cn = cn.reshape(-1, 3)
    lv = np.empty(nl, int)
    me.loops.foreach_get("vertex_index", lv)
    nv = len(me.vertices)
    vn = np.zeros((nv, 3))
    np.add.at(vn, lv, cn)
    fn = np.empty(len(me.polygons) * 3)
    me.polygons.foreach_get("normal", fn)
    fn = fn.reshape(-1, 3)
    fc = np.empty(len(me.polygons) * 3)
    me.polygons.foreach_get("center", fc)
    fc = fc.reshape(-1, 3)
    ls = np.empty(len(me.polygons), int)
    me.polygons.foreach_get("loop_start", ls)
    # 1. winding vs the generator's normals
    acc = np.add.reduceat(cn, ls, axis=0)
    flip = np.einsum("ij,ij->i", acc, fn) < 0
    fn_eff = np.where(flip[:, None], -fn, fn)
    # 2. visibility: which side of each face sees the outside
    bvh = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [p.vertices[:] for p in me.polygons])
    rng = np.random.default_rng(seed)
    co_ = coords(ob)
    diag = float(np.linalg.norm(co_.max(0) - co_.min(0)))
    eps = 1e-4 * diag
    u = rng.random((rays, 2))
    r, ph = np.sqrt(u[:, 0]), 2 * np.pi * u[:, 1]
    local = np.stack([r * np.cos(ph), r * np.sin(ph), np.sqrt(1 - u[:, 0])], 1)  # cosine-weighted, z = normal
    vis_flip = np.zeros(len(fn), bool)
    open_both = np.zeros(len(fn), bool)
    for i in range(len(fn)):
        n = fn_eff[i]
        if not np.isfinite(n).all() or np.linalg.norm(n) < 1e-9:
            continue
        a = np.array([1.0, 0, 0]) if abs(n[0]) < 0.9 else np.array([0, 1.0, 0])
        t = np.cross(n, a); t /= np.linalg.norm(t)
        b = np.cross(n, t)
        dirs = local[:, 0:1] * t + local[:, 1:2] * b + local[:, 2:3] * n
        c = Vector(fc[i])
        ep = em = 0
        for d in dirs:
            dv = Vector(d)
            if bvh.ray_cast(c + dv * eps, dv)[0] is None:
                ep += 1
            if bvh.ray_cast(c - dv * eps, -dv)[0] is None:
                em += 1
        if em - ep >= max(3, rays // 3):
            vis_flip[i] = True
        if min(ep, em) >= 0.55 * rays:
            open_both[i] = True
    total = flip ^ vis_flip
    # vertex signs: majority of the adjacent faces' (generator-normal) agreement after the fix
    sign_f = np.where(vis_flip, -1.0, 1.0)
    vs = np.zeros(nv)
    tot_l = np.empty(len(me.polygons), int)
    me.polygons.foreach_get("loop_total", tot_l)
    face_of_loop = np.repeat(np.arange(len(me.polygons)), tot_l)
    np.add.at(vs, lv, sign_f[face_of_loop])
    vsign = np.where(vs < 0, -1.0, 1.0)
    vnn = vn / np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-9)
    n_two = int(open_both.sum()) if two_sided else 0
    F = len(me.polygons)
    dup_src_face, dup_src_vert = [], {}
    if total.any() or n_two:
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        if total.any():
            bmesh.ops.reverse_faces(bm, faces=[bm.faces[i] for i in np.nonzero(total)[0]])
        if n_two:
            # single-sheet walls seen from both sides (open shells): give them a reversed back copy so they do
            # not vanish under backface culling
            src = [bm.faces[i] for i in np.nonzero(open_both)[0]]
            res = bmesh.ops.duplicate(bm, geom=src)
            fmap, vmap = res["face_map"], res["vert_map"]
            dfaces = [e for e in res["geom"] if isinstance(e, bmesh.types.BMFace)]
            bmesh.ops.reverse_faces(bm, faces=dfaces)
            old_of_new_f = {}
            for a_, b_ in fmap.items():
                (o_, n_) = (a_, b_) if a_ in src else (b_, a_)
                old_of_new_f[n_] = o_.index
            old_of_new_v = {}
            for a_, b_ in vmap.items():
                (o_, n_) = (a_, b_) if a_.index < nv else (b_, a_)
                old_of_new_v[n_] = o_.index
            bm.faces.index_update()
            bm.verts.index_update()
            dup_src_face = {f.index: old_of_new_f[f] for f in dfaces}
            dup_src_vert = {v.index: i for v, i in old_of_new_v.items()}
        bm.to_mesh(me)
        bm.free()
        me.update()
    # per-corner custom normals: the generator's smooth normal, negated on faces the visibility test turned
    # around (and on the back copies), so every corner agrees with its own face
    sign_face = np.where(vis_flip, -1.0, 1.0)
    lnorm = []
    for poly in me.polygons:
        if poly.index < F:
            sgn, fi = sign_face[poly.index], poly.index
        else:
            fi = dup_src_face.get(poly.index, 0)
            sgn = -sign_face[fi]
        for li in poly.loop_indices:
            vi = me.loops[li].vertex_index
            vi = dup_src_vert.get(vi, vi)
            lnorm.append(tuple(vnn[vi] * sgn))
    me.normals_split_custom_set(lnorm)
    me.update()
    ob["orientation_fixed"] = 1
    log(f"orientation: {int(flip.sum())} faces flipped to the generator normals, {int(vis_flip.sum())} inside-out faces flipped by ray visibility, {n_two} open single-sheet faces doubled")


def clean(ob, length):
    if not ob.get("orientation_fixed"):
        flip_by_shading_normals(ob)
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    nv = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5 * length)
    merged = nv - len(bm.verts)
    loose_v = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose_v, context="VERTS")
    loose_e = [e for e in bm.edges if not e.link_faces]
    bmesh.ops.delete(bm, geom=loose_e, context="EDGES")
    comps = components(bm)
    comps.sort(key=len, reverse=True)
    # surface distance to everything else (big components), not vertex distance: adaptive meshes have few verts
    big = [c for c in comps if len(c) >= 0.02 * len(bm.faces)] or comps[:1]
    vmap = {}
    verts, polys = [], []
    for c in big:
        for f in c:
            idx = []
            for v in f.verts:
                if v.index not in vmap:
                    vmap[v.index] = len(verts)
                    verts.append(v.co.copy())
                idx.append(vmap[v.index])
            polys.append(idx)
    bvh = BVHTree.FromPolygons(verts, polys)
    total = len(bm.faces)
    kill = []
    for c in comps:
        if any(c is b for b in big):
            continue
        vs = {v for f in c for v in f.verts}
        dmin = min((bvh.find_nearest(v.co)[3] for v in vs), default=1e9)
        pts = np.array([tuple(v.co) for v in vs])
        diag = float(np.linalg.norm(pts.max(0) - pts.min(0)))
        if len(c) < 4 or (len(c) < 0.02 * total and dmin > 0.012 * length) or (diag < 0.004 * length and dmin > 0.004 * length):
            kill.extend(c)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(me)
    bm.free()
    me.update()
    log(f"clean: merged {merged} verts, loose v/e {len(loose_v)}/{len(loose_e)}, components {len(comps)}, floater faces removed {len(kill)}")


def fill_holes(ob, spec, bmap, length):
    """Close small open boundary loops the generator left in a shell (e.g. a torn magazine side) so the inside does
    not show as a dark hole. spec: true or {"max_perimeter_m": 0.05, "boxes_img": [[x0, y0, x1, y1], ...]} (only
    loops whose centre lies in one of the reference-image boxes; none = anywhere). The new faces take the UVs of
    their boundary vertices when those sit in one UV island, else one UV (a flat patch of the neighbouring colour);
    their winding is matched to the neighbouring faces. Muzzle bores etc. are left open by keeping the perimeter
    limit small or by boxes."""
    if not spec:
        return 0
    spec = {} if spec is True else spec
    maxp = spec.get("max_perimeter_m", 0.06 * length)
    boxes = []
    for b in spec.get("boxes_img", []):
        ya, za = bmap.yz(b[0], b[1])
        yb, zb = bmap.yz(b[2], b[3])
        boxes.append((min(ya, yb), min(za, zb), max(ya, yb), max(za, zb)))
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    uvl = bm.loops.layers.uv.active
    bnd = {e for e in bm.edges if e.is_boundary}
    loops, seen = [], set()
    for e0 in bnd:
        if e0 in seen:
            continue
        comp, stack = [], [e0]
        seen.add(e0)
        while stack:
            e = stack.pop()
            comp.append(e)
            for v in e.verts:
                for e2 in v.link_edges:
                    if e2 in bnd and e2 not in seen:
                        seen.add(e2)
                        stack.append(e2)
        loops.append(comp)
    filled, nfaces = 0, 0
    for comp in sorted(loops, key=lambda c: sum(e.calc_length() for e in c)):
        per = sum(e.calc_length() for e in comp)
        vs = {v for e in comp for v in e.verts}
        c = sum((v.co for v in vs), Vector()) / len(vs)
        inbox = not boxes or any(b[0] <= c.y <= b[2] and b[1] <= c.z <= b[3] for b in boxes)
        img = ""
        if bmap is not None:
            img = f" img ({bmap.bx0 + (c.y - bmap.y0) / bmap.s:.0f}, {(bmap.zoff - c.z) / bmap.s:.0f})"
        if per > maxp or not inbox:
            if per < 2 * maxp:
                log(f"fill_holes: kept open loop {len(comp)} edges, {per * 1000:.1f} mm{img}")
            continue
        old = set(bm.faces)
        res = bmesh.ops.holes_fill(bm, edges=comp, sides=0)
        new = [f for f in res["faces"] if f not in old]
        if not new:
            continue
        # winding: a shared boundary edge must be walked in opposite directions by the old and the new face
        for f in new:
            for lp in f.loops:
                e = lp.edge
                nb = [l2 for l2 in e.link_loops if l2.face not in new]
                if nb:
                    if nb[0].vert == lp.vert:
                        f.normal_flip()
                    break
        if uvl is not None:
            vuv = {}
            for v in vs:
                for lp in v.link_loops:
                    if lp.face not in new:
                        vuv[v] = lp[uvl].uv.copy()
                        break
            uvs = list(vuv.values())
            spread = max(((a - b).length for a in uvs for b in uvs), default=0.0)
            med = sorted(uvs, key=lambda u: (u - sum(uvs, Vector((0, 0))) / len(uvs)).length)[0] if uvs else Vector((0, 0))
            for f in new:
                for lp in f.loops:
                    lp[uvl].uv = vuv.get(lp.vert, med) if spread < 0.04 else med
        for f in new:
            f.smooth = True
            nb = [l2.face for lp in f.loops for l2 in lp.edge.link_loops if l2.face not in new]
            if nb:
                f.material_index = nb[0].material_index
        filled += 1
        nfaces += len(new)
        log(f"fill_holes: filled loop {len(comp)} edges, {per * 1000:.1f} mm{img}")
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    log(f"fill_holes: {ob.name} {filled} of {len(loops)} open loops filled ({nfaces} faces)")
    REPORT.setdefault("holes_filled", {})[ob.name] = filled
    return filled


def flat_normals(ob, specs):
    """Reset the shading normals of flat faces the generator smoothed together with their neighbours (the smoke
    grenade's lid came with corner normals tilted 30-70 deg, which renders as dark spikes): every corner of a face whose
    normal points along `axis` (dot > `dot`) and whose centre lies above `min_frac` of the height along that axis gets
    the face normal. spec: {"axis": "+Z", "dot": 0.95, "min_frac": 0.5}."""
    if not specs:
        return
    me = ob.data
    cn = [tuple(c.vector) for c in me.corner_normals]
    co = coords(ob)
    n = 0
    for sp in specs:
        ax = "XYZ".index(sp.get("axis", "+Z")[-1])
        sg = -1.0 if sp.get("axis", "+Z").startswith("-") else 1.0
        lo, hi = co[:, ax].min(), co[:, ax].max()
        thr = lo + sp.get("min_frac", 0.5) * (hi - lo) if sg > 0 else hi - sp.get("min_frac", 0.5) * (hi - lo)
        for p in me.polygons:
            if p.normal[ax] * sg > sp.get("dot", 0.95) and (p.center[ax] - thr) * sg > 0:
                for li in p.loop_indices:
                    cn[li] = tuple(p.normal)
                n += 1
    for p in me.polygons:
        p.use_smooth = True
    me.normals_split_custom_set(cn)
    me.update()
    log(f"flat_normals: {ob.name} {n} faces reset to their face normal")


def cap_top(ob, sp):
    """Fix the top of an upright can whose lid the generator painted black (smoke grenade: the reference's lid is olive
    with emission holes; Tripo rendered it as a dark opening). mode "existing" (default): the generator's own upward
    faces at the rim height (|z - rim| < band_m, inside radius_frac of the can radius) get a planar UV patch around a
    UV sampled from the painted wall just below the rim, sharp edges around them and flat (+Z) shading normals.
    mode "disc": add a flat `seg`-triangle disc offset_m below the rim instead (for a can that really is open).
    The lid faces share the wall's UV patch, so the AO bake skips them (CAPS). sp: {"mode": "existing",
    "band_m": 0.003, "radius_frac": 0.98, "offset_m": 0.003, "seg": 24, "uv_radius": 0.012}."""
    sp = {} if sp is True else sp
    co = coords(ob)
    z0, z1 = co[:, 2].min(), co[:, 2].max()
    H = z1 - z0
    # the lower part of the can carries no spoon/fuze: its bounding box gives the can axis and radius
    low = co[co[:, 2] < z0 + 0.3 * H]
    cx, cy = float(low[:, 0].max() + low[:, 0].min()) / 2, float(low[:, 1].max() + low[:, 1].min()) / 2
    R = float((low[:, 0].max() - low[:, 0].min()) + (low[:, 1].max() - low[:, 1].min())) / 4
    r_all = np.hypot(co[:, 0] - cx, co[:, 1] - cy)
    wall = co[(r_all > 0.85 * R) & (r_all < 1.02 * R)]
    # rim height: top of the wall per 16 angular sectors, median over sectors (the spoon hugs one or two of them)
    ang = np.floor((np.arctan2(wall[:, 1] - cy, wall[:, 0] - cx) + math.pi) / (2 * math.pi) * 16).astype(int) % 16
    tops = [float(wall[ang == k, 2].max()) for k in range(16) if (ang == k).any()]
    zrim = float(np.median(tops))
    me = ob.data
    uvl = me.uv_layers.active
    best, uv = 1e9, (0.5, 0.5)
    zt = zrim - 0.15 * H
    for p in me.polygons:
        c = p.center
        if abs(p.normal.z) < 0.3 and 0.85 * R < math.hypot(c.x - cx, c.y - cy) < 1.02 * R and abs(c.z - zt) < best:
            best = abs(c.z - zt)
            uv = tuple(uvl.data[p.loop_indices[0]].uv) if uvl else uv
    k_uv = sp.get("uv_radius", 0.012)
    rr = sp.get("radius_frac", 0.98) * R
    band = sp.get("band_m", 0.003)
    bm = bmesh.new()
    bm.from_mesh(me)
    uvb = bm.loops.layers.uv.active
    if sp.get("mode", "existing") == "disc":
        zc = zrim - sp.get("offset_m", 0.003)
        n = sp.get("seg", 24)
        c = bm.verts.new((cx, cy, zc))
        ring = [bm.verts.new((cx + rr * math.cos(2 * math.pi * i / n), cy + rr * math.sin(2 * math.pi * i / n), zc)) for i in range(n)]
        sel = [bm.faces.new((c, ring[i], ring[(i + 1) % n])) for i in range(n)]  # counter-clockwise from +Z = up
        band = 0.0008
    else:
        zc = zrim
        bm.normal_update()
        sel = [f for f in bm.faces if f.normal.z > 0.9 and abs(f.calc_center_median().z - zrim) < band
               and math.hypot(f.calc_center_median().x - cx, f.calc_center_median().y - cy) < rr]
    selset = set(sel)
    for f in sel:
        if uvb is not None:
            # a small planar patch around the sampled UV (not a single point: zero-area UVs break the tangent frame)
            for lp in f.loops:
                lp[uvb].uv = (uv[0] + (lp.vert.co.x - cx) / R * k_uv, uv[1] + (lp.vert.co.y - cy) / R * k_uv)
        for e in f.edges:
            if any(lf not in selset for lf in e.link_faces):
                e.smooth = False
        f.smooth = True
    bm.faces.index_update()
    idx = [f.index for f in sel]
    bm.to_mesh(me)
    bm.free()
    me.update()
    cn = [tuple(c_.vector) for c_ in me.corner_normals]
    for i in idx:
        for li in me.polygons[i].loop_indices:
            cn[li] = (0.0, 0.0, 1.0)
    me.normals_split_custom_set(cn)
    me.update()
    log(f"cap_top: {sp.get('mode', 'existing')} lid at z {zc * 1000:.1f} mm (rim {zrim * 1000:.1f}, can r {R * 1000:.1f} mm), "
        f"{len(idx)} faces repainted, uv {tuple(round(v, 4) for v in uv)}")
    CAPS.append({"c": Vector((cx, cy, zc)), "r": rr, "band": band})  # body space; moved with the pivot


CAPS = []  # cap discs: their UV patch overlaps the can wall, so the AO bake must not write them


def drop_caps(ob):
    """Delete the cap-disc faces of a bake duplicate (AO stays the wall's under the shared UV patch)."""
    if not CAPS:
        return
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        for cp in CAPS:
            if abs(c.z - cp["c"].z) < cp["band"] and f.normal.z > 0.9 and math.hypot(c.x - cp["c"].x, c.y - cp["c"].y) < cp["r"]:
                kill.append(f)
                break
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(ob.data)
    bm.free()


def transfer_normals(dst, src):
    m = dst.modifiers.new("nt", "DATA_TRANSFER")
    m.object = src
    m.use_loop_data = True
    m.data_types_loops = {"CUSTOM_NORMAL"}
    m.loop_mapping = "POLYINTERP_NEAREST"
    apply_modifier(dst, m)


def decimate(ob, target, src_for_normals=None):
    t = tris(ob)
    it = 0
    while t > target and it < 4:
        m = ob.modifiers.new("dec", "DECIMATE")
        m.decimate_type = "COLLAPSE"
        m.ratio = max(0.01, (target - 2) / t)
        m.use_collapse_triangulate = True
        apply_modifier(ob, m)
        t = tris(ob)
        it += 1
    if src_for_normals is not None:
        transfer_normals(ob, src_for_normals)
    log("decimate", ob.name, "->", t, "tris (target", target, ")")
    return t


def triangulate(ob):
    m = ob.modifiers.new("tri", "TRIANGULATE")
    m.keep_custom_normals = True
    apply_modifier(ob, m)


def fix_backfaces(ob, budget=None, src_for_normals=None, div=450):
    """Give every face whose back side is visible from outside (open single sheets, leftover inside-out faces, double
    walls ~1 mm apart) a reversed copy, so nothing vanishes under Unity's backface culling (bl_backface.py). Keeps
    the triangle budget: when the copies would push the mesh over it, the mesh is decimated by that many first."""
    if not JOB.get("fix_backfaces", True):
        return 0
    bf = all_visible_backfaces(ob, div=div)
    if budget and tris(ob) + int(len(bf) * 1.15) + 2 > budget and src_for_normals is not None:
        decimate(ob, budget - int(len(bf) * 1.15) - 4, src_for_normals)
        triangulate(ob)
        bf = all_visible_backfaces(ob, div=div)
    n = double_faces(ob, bf)
    n += fix_all(ob, passes=1, div=div)
    log(f"backfaces: {ob.name} {n} visible back faces doubled -> {tris(ob)} tris")
    REPORT.setdefault("backfaces_doubled", {})[ob.name] = n
    return n


# ---------------------------------------------------------------- image <-> body mapping
class Map:
    def __init__(self, body, job):
        co = coords(body)
        self.y0, self.y1 = co[:, 1].min(), co[:, 1].max()
        self.z0, self.z1 = co[:, 2].min(), co[:, 2].max()
        bx0, by0, bx1, by1 = job["weapon_bbox_img"]
        self.bx0, self.by0, self.bx1, self.by1 = bx0, by0, bx1, by1
        self.s = (self.y1 - self.y0) / (bx1 - bx0)
        h_img = (by1 - by0) * self.s
        log(f"map: {self.s * 1000:.4f} mm/px, image height {h_img:.4f} m vs mesh height {self.z1 - self.z0:.4f} m")
        # vertical anchor: the average of the top and bottom alignment
        self.zoff = ((self.z1 + by0 * self.s) + (self.z0 + by1 * self.s)) / 2

    def yz(self, x, y):
        return self.y0 + (x - self.bx0) * self.s, self.zoff - y * self.s


def surface_x(body, Y, Z, side):
    """X of the body surface hit by a ray cast from outside on the given side (-1 = right side of the weapon)."""
    dg = bpy.context.evaluated_depsgraph_get()
    bvh = BVHTree.FromObject(body, dg)
    o = Vector((side * 1.0, Y, Z))
    hit = bvh.ray_cast(o, Vector((-side, 0, 0)))
    return float(hit[0].x) if hit[0] is not None else 0.0


# ---------------------------------------------------------------- magazine
def pca_angle(pts2):
    c = pts2 - pts2.mean(0)
    w, v = np.linalg.eigh(c.T @ c)
    ax = v[:, -1]
    if ax[1] > 0:  # make it point down (-Z)
        ax = -ax
    return math.atan2(ax[0], -ax[1])  # angle of the downward axis from -Z towards +Y


def place_mag(mag, body_map, mj):
    orient_mode = mj.get("orient", "side_short")
    co = coords(mag)
    # magazines are generated from a side view too: X = thickness, Y = front/back, Z = up
    cx = float(np.median(co[:, 0]))
    mag.data.transform(Matrix.Translation((-cx, 0, 0)))
    s = body_map.s
    fit = mj["fit"]
    if fit["mode"] == "mask":
        mx0, my0, mx1, my1 = fit["mask_bbox"]
        # 1. rotate so the mesh's principal axis matches the mask's
        if "axis_down_img" in fit:
            dx, dy = fit["axis_down_img"]
            target = math.atan2(dx, dy)  # image: +x -> +Y, +y down -> -Z
            co = coords(mag)
            cur = pca_angle(co[:, 1:3])
            d = target - cur
            if abs(math.degrees(d)) < 25:
                mag.data.transform(Matrix.Rotation(d, 4, "X"))
                log(f"mag: rotated {math.degrees(d):.1f} deg to match the mask axis")
            else:
                log(f"mag: axis differs by {math.degrees(d):.1f} deg, not rotated")
        co = coords(mag)
        w_img = (mx1 - mx0) * s
        k = w_img / (co[:, 1].max() - co[:, 1].min()) * fit.get("scale_mul", 1.0)
        mag.data.transform(Matrix.Scale(k, 4))
        co = coords(mag)
        Ya, _ = body_map.yz(mx0, 0)
        _, Zb = body_map.yz(0, my1)
        mag.data.transform(Matrix.Translation((0, Ya - co[:, 1].min() + fit.get("dy_m", 0.0), Zb - co[:, 2].min() + fit.get("dz_m", 0.0))))
        well_x = fit.get("well_x", (mx0 + mx1) / 2)
        Yw, Zw = body_map.yz(well_x, my0)
        if fit.get("mask_png"):
            register_mag(mag, body_map, fit["mask_png"], Zw)
            co = coords(mag)
            top = co[co[:, 2] > co[:, 2].max() - 0.15 * (co[:, 2].max() - co[:, 2].min())]
            if "well_x" not in fit:
                Yw = float(top[:, 1].mean())
    else:  # manual: anchor 'bottom' (baseplate) at an image point, axis angle, real length
        co = coords(mag)
        cur = pca_angle(co[:, 1:3])
        target = math.radians(fit["axis_deg"])  # positive: bottom towards the rear (+Y)
        mag.data.transform(Matrix.Rotation(target - cur, 4, "X"))
        co = coords(mag)
        # length along the axis
        axis = np.array([0, math.sin(target), -math.cos(target)])
        proj = co @ axis
        k = fit["length_m"] / (proj.max() - proj.min())
        mag.data.transform(Matrix.Scale(k, 4))
        co = coords(mag)
        proj = co @ axis
        bottom = co[proj > proj.max() - 0.03 * (proj.max() - proj.min())].mean(0)
        Yb, Zb = body_map.yz(*fit["bottom_img"])
        mag.data.transform(Matrix.Translation((0, Yb - bottom[1] + fit.get("dy_m", 0.0), Zb - co[:, 2].min() + fit.get("dz_m", 0.0))))
        co = coords(mag)
        top = co[proj < proj.min() + 0.04 * (proj.max() - proj.min())]
        Yw, Zw = body_map.yz(*fit["well_img"]) if "well_img" in fit else (float(top[:, 1].mean()), float(co[:, 2].max()))
    if mj.get("mirror_half"):
        # generators guess the unseen side of a face-on object (the RPK drum came back domed): keep the side that
        # faced the camera (+X = the weapon's left) and mirror it, so both faces are the photographed one
        co = coords(mag)
        cx = float((co[:, 0].max() + co[:, 0].min()) / 2) if mj["mirror_half"] == "bbox" else 0.0
        bm = bmesh.new()
        bm.from_mesh(mag.data)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6, plane_co=(cx, 0, 0),
                               plane_no=(1, 0, 0), clear_inner=True)
        res = bmesh.ops.duplicate(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:])
        dv = [e for e in res["geom"] if isinstance(e, bmesh.types.BMVert)]
        for v in dv:
            v.co.x = 2 * cx - v.co.x
        dfaces = [e for e in res["geom"] if isinstance(e, bmesh.types.BMFace)]
        bmesh.ops.reverse_faces(bm, faces=dfaces)
        # mirrored copy samples the same texels via a +1 U offset (texture wrap Repeat): bakes only write the 0-1
        # tile, so AO / projection bakes of the photographed half are not overwritten by the mirrored half
        uvl = bm.loops.layers.uv.active
        if uvl is not None:
            for f in dfaces:
                for lp in f.loops:
                    lp[uvl].uv.x += 1.0
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=2e-4)
        bm.normal_update()
        vnorm = [tuple(v.normal) for v in bm.verts]
        bm.to_mesh(mag.data)
        bm.free()
        mag.data.update()
        for p_ in mag.data.polygons:
            p_.use_smooth = True
        # the copied custom normals of the mirrored half still point the old way: rebuild them from the geometry
        mag.data.normals_split_custom_set_from_vertices(vnorm)
        mag.data.update()
        log("mag: mirrored the +X half")
    if mj.get("thickness_m"):
        co = coords(mag)
        cx = float((co[:, 0].max() + co[:, 0].min()) / 2)
        k = mj["thickness_m"] / float(co[:, 0].max() - co[:, 0].min())
        mag.data.transform(Matrix.Translation((cx, 0, 0)) @ Matrix.Diagonal((k, 1, 1, 1)) @ Matrix.Translation((-cx, 0, 0)))
        log(f"mag: thickness scaled x{k:.3f} to {mj['thickness_m']} m")
    co = coords(mag)
    log(f"mag placed: size m {[round(float(v), 4) for v in co.max(0) - co.min(0)]}, well point Y {Yw:.4f} Z {Zw:.4f}")
    return Vector((0.0, Yw, Zw))


def fit_mag_thickness(mag, body, margin):
    """The part of the magazine that sits inside the receiver's silhouette must stay inside the receiver (generated
    bodies are solid and generated magazines come out a few mm too thick, so the magazine would poke through the
    well walls and z-fight). Scale the magazine along X about its centre until those vertices are `margin` inside.
    Only body sections at least 0.6x as wide as the magazine count as the well (thin parts such as a trigger guard
    in front of the magazine are ignored), and the 5th percentile of the required scales is used."""
    bvh = BVHTree.FromPolygons([v.co.copy() for v in body.data.vertices], [p.vertices[:] for p in body.data.polygons])
    co = coords(mag)
    cx = float((co[:, 0].max() + co[:, 0].min()) / 2)
    th = float(co[:, 0].max() - co[:, 0].min())
    ks = []
    n_in = 0
    for x, y, z in co[:: max(1, len(co) // 4000)]:
        hp = bvh.ray_cast(Vector((1.0, y, z)), Vector((-1, 0, 0)))
        hm = bvh.ray_cast(Vector((-1.0, y, z)), Vector((1, 0, 0)))
        if hp[0] is None or hm[0] is None:
            continue
        xp, xm = hp[0].x, hm[0].x
        if xp - xm < 0.6 * th:
            continue
        n_in += 1
        if x > cx:
            ks.append((xp - margin - cx) / max(x - cx, 1e-6))
        elif x < cx:
            ks.append((cx - xm - margin) / max(cx - x, 1e-6))
    k = 1.0
    if ks:
        k = float(min(1.0, max(0.5, np.percentile(ks, 5))))
    if k < 0.999:
        mag.data.transform(Matrix.Translation((cx, 0, 0)) @ Matrix.Diagonal((k, 1, 1, 1)) @ Matrix.Translation((-cx, 0, 0)))
    co = coords(mag)
    log(f"mag fit: {n_in} vertices inside the receiver silhouette, thickness x{k:.3f} -> {co[:, 0].max() - co[:, 0].min():.4f} m")
    REPORT["mag_thickness_scale"] = round(k, 4)


def load_mask(path):
    img = bpy.data.images.load(P(path))
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    m = a.reshape(h, w, 4)[::-1, :, 0] > 0.5  # Blender rows are bottom-up
    bpy.data.images.remove(img)
    return m


def raster_yz(ob, cell):
    """Fill the side projection (Y, Z) of the mesh on a grid; returns the filled cell centres (N, 2)."""
    me = ob.data
    me.calc_loop_triangles()
    co = coords(ob)[:, 1:3]
    tri = np.array([t.vertices[:] for t in me.loop_triangles])
    lo = co.min(0)
    nx, ny = (np.ceil((co.max(0) - lo) / cell).astype(int) + 2)
    grid = np.zeros((nx, ny), bool)
    gx = (np.arange(nx) + 0.5) * cell + lo[0]
    gy = (np.arange(ny) + 0.5) * cell + lo[1]
    for t in tri:
        p = co[t]
        mn = np.floor((p.min(0) - lo) / cell).astype(int)
        mx = np.ceil((p.max(0) - lo) / cell).astype(int) + 1
        X, Y = np.meshgrid(gx[mn[0]:mx[0]], gy[mn[1]:mx[1]], indexing="ij")
        (x1, y1), (x2, y2), (x3, y3) = p
        d = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3)
        if abs(d) < 1e-14:
            continue
        l1 = ((y2 - y3) * (X - x3) + (x3 - x2) * (Y - y3)) / d
        l2 = ((y3 - y1) * (X - x3) + (x1 - x3) * (Y - y3)) / d
        ins = (l1 >= -1e-6) & (l2 >= -1e-6) & (l1 + l2 <= 1 + 1e-6)
        grid[mn[0]:mx[0], mn[1]:mx[1]] |= ins
    ii, jj = np.nonzero(grid)
    return np.stack([gx[ii], gy[jj]], 1)


def register_mag(mag, bmap, mask_path, well_z):
    """Similarity fit (rotation about X, uniform scale, Y/Z shift) of the magazine's side silhouette to the
    image mask, using only the part below the magazine-well line (the top of the magazine is hidden inside)."""
    M = load_mask(mask_path)
    H, W = M.shape
    s = bmap.s
    ys, xs = np.nonzero(M)
    MY = bmap.y0 + (xs + 0.5 - bmap.bx0) * s
    MZ = bmap.zoff - (ys + 0.5) * s
    cut = well_z - 2 * s
    A_M = float(((MZ < cut).sum()) * s * s)

    def in_mask(Y, Z):
        x = np.floor(bmap.bx0 + (Y - bmap.y0) / s).astype(int)
        y = np.floor((bmap.zoff - Z) / s).astype(int)
        ok = (x >= 0) & (x < W) & (y >= 0) & (y < H)
        r = np.zeros(len(Y), bool)
        r[ok] = M[y[ok], x[ok]]
        return r

    G = raster_yz(mag, s)
    rng = np.random.default_rng(0)
    area_cell = s * s * len(G)
    if len(G) > 12000:
        G = G[rng.choice(len(G), 12000, replace=False)]
    area_per = area_cell / len(G)
    c = G.mean(0)
    D = G - c

    def score(th, k, ty, tz):
        ct, st = math.cos(th), math.sin(th)
        Py = c[0] + k * (ct * D[:, 0] - st * D[:, 1]) + ty
        Pz = c[1] + k * (st * D[:, 0] + ct * D[:, 1]) + tz
        below = Pz < cut
        inside = in_mask(Py[below], Pz[below])
        a = area_per * k * k
        I = inside.sum() * a
        AB = below.sum() * a
        return I / (A_M + AB - I + 1e-12)

    best = (score(0, 1, 0, 0), 0.0, 1.0, 0.0, 0.0)
    log(f"mag register: start IoU {best[0]:.3f}")
    for th_r, k_r, t_r, steps in ((math.radians(8), 0.18, 30 * s, 7), (math.radians(2), 0.04, 6 * s, 7), (math.radians(0.5), 0.01, 1.5 * s, 7)):
        _, th0, k0, ty0, tz0 = best
        for th in np.linspace(th0 - th_r, th0 + th_r, steps):
            for k in np.linspace(k0 - k_r, k0 + k_r, steps):
                for ty in np.linspace(ty0 - t_r, ty0 + t_r, steps):
                    for tz in np.linspace(tz0 - t_r, tz0 + t_r, steps):
                        sc = score(th, k, ty, tz)
                        if sc > best[0]:
                            best = (sc, th, k, ty, tz)
    sc, th, k, ty, tz = best
    log(f"mag register: IoU {sc:.3f}, rot {math.degrees(th):.2f} deg, scale {k:.3f}, shift {ty * 1000:.1f}/{tz * 1000:.1f} mm")
    Mx = (Matrix.Translation((0, c[0] + ty, c[1] + tz)) @ Matrix.Scale(k, 4) @ Matrix.Rotation(th, 4, "X")
          @ Matrix.Translation((0, -c[0], -c[1])))
    mag.data.transform(Mx)
    REPORT["mag_register"] = {"iou": round(sc, 4), "rot_deg": round(math.degrees(th), 3), "scale": round(k, 4)}
    return sc


def set_origin(ob, point):
    """Move the object origin to `point` (object space) without moving the geometry in world space."""
    ob.data.transform(Matrix.Translation(-Vector(point)))
    ob.location = ob.location + Vector(point)


# ---------------------------------------------------------------- materials / bake
def make_mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    return m


def bake_ao(ob, path, size, distance, samples=96):
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    if scene.world is None:
        scene.world = bpy.data.worlds.new("W")
    scene.world.light_settings.distance = distance
    img = bpy.data.images.new("AO_" + ob.name, size, size, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"
    tmp = duplicate(ob, ob.name + "_bake")
    drop_caps(tmp)
    tmp.data.materials.clear()
    mat = bpy.data.materials.new("bake_" + ob.name)
    mat.use_nodes = True
    n = mat.node_tree.nodes.new("ShaderNodeTexImage")
    n.image = img
    mat.node_tree.nodes.active = n
    tmp.data.materials.append(mat)
    # occluders: the object itself plus optional others
    hide = [o for o in bpy.data.objects if o is not tmp and o.type == "MESH"]
    for o in hide:
        o.hide_render = True
    select_only([tmp])
    scene.render.bake.margin = 6
    scene.render.bake.use_clear = True
    t = time.time()
    bpy.ops.object.bake(type="AO")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    for o in hide:
        o.hide_render = False
    bpy.data.objects.remove(tmp, do_unlink=True)
    log(f"baked AO {size}px in {time.time() - t:.1f}s -> {path}")


def bake_normal(lows, high, path, size, extrusion, max_dist, samples=4):
    """Bake a tangent-space normal map from the high-poly source (its generator normal map included, through its
    imported glTF material) onto the decimated low-poly in the low-poly's own UV layout (collapse decimation keeps the
    generator atlas, so the result replaces the generator normal map 1:1). Used where decimation removed > 75 % of the
    triangles (world-only models from 40 k Hunyuan meshes): the lost geometric detail goes into the map instead of
    being dropped. `lows`: the low-poly objects that share the texture (LOD0 + split parts), baked before back-face
    doubling so every texel has one owner."""
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    img = bpy.data.images.new("N_bake", size, size, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"
    img.generated_color = (0.5, 0.5, 1.0, 1.0)
    tmps = [duplicate(o, o.name + "_nb") for o in lows]
    for t, o in zip(tmps, lows):
        t.parent = None
        t.matrix_world = o.matrix_world.copy()
    select_only(tmps)
    if len(tmps) > 1:
        bpy.ops.object.join()
    tgt = bpy.context.view_layer.objects.active
    tgt.data.materials.clear()
    mat = bpy.data.materials.new("nbake")
    mat.use_nodes = True
    n = mat.node_tree.nodes.new("ShaderNodeTexImage")
    n.image = img
    mat.node_tree.nodes.active = n
    tgt.data.materials.append(mat)
    hv = high.hide_render
    high.hide_render = False
    bpy.ops.object.select_all(action="DESELECT")
    high.select_set(True)
    tgt.select_set(True)
    bpy.context.view_layer.objects.active = tgt
    bk = scene.render.bake
    bk.use_selected_to_active = True
    bk.use_cage = False
    bk.cage_extrusion = extrusion
    bk.max_ray_distance = max_dist
    bk.normal_space = "TANGENT"
    bk.margin = 8
    bk.margin_type = "EXTEND"
    bk.use_clear = True
    t = time.time()
    bpy.ops.object.bake(type="NORMAL")
    bk.use_selected_to_active = False
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    high.hide_render = hv
    bpy.data.objects.remove(tgt, do_unlink=True)
    log(f"baked normal {size}px from {tris(high)} onto {sum(tris(o) for o in lows)} tris in {time.time() - t:.1f}s -> {path}")
    REPORT["normal_baked"] = {"high_tris": tris(high), "low_tris": sum(tris(o) for o in lows), "size": size}


def export_fbx(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    select_only(objs)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type="OFF", use_tspace=False,
                             use_triangles=True, use_custom_props=False, add_leaf_bones=False, bake_anim=False,
                             path_mode="STRIP", embed_textures=False, colors_type="NONE")
    log("exported", path, f"{os.path.getsize(path) / 1024:.0f} KB")


def add_empty(name, loc, parent):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "ARROWS"
    e.empty_display_size = 0.03
    bpy.context.collection.objects.link(e)
    e.location = loc
    e.parent = parent
    return e


def split_parts(ob, parts, bmap, pivot):
    """Separate faces whose centroid lies in an image-space box into their own objects (e.g. the RPG warhead that
    the game hides after firing, the DShK pintle post). Each part keeps the shared UVs/material; origin at
    origin_img (or the part's bbox centre). Returns [(name, object)]."""
    out = []
    for pj in parts:
        x0, y0, x1, y1 = pj["box_img"]
        Ya, Za = bmap.yz(x0, y1)
        Yb, Zb = bmap.yz(x1, y0)
        Ya, Yb = Ya - pivot.y, Yb - pivot.y
        Za, Zb = Za - pivot.z, Zb - pivot.z
        me = ob.data
        bm = bmesh.new()
        bm.from_mesh(me)
        sel = [f for f in bm.faces if Ya <= f.calc_center_median().y <= Yb and Za <= f.calc_center_median().z <= Zb]
        if not sel:
            bm.free()
            log("split", pj["name"], "no faces in box")
            continue
        part = duplicate(ob, ob.name + "_" + pj["name"])
        part.parent = None
        bm2 = bmesh.new()
        bm2.from_mesh(part.data)
        bm2.faces.ensure_lookup_table()
        keep = {f.index for f in sel}
        bmesh.ops.delete(bm2, geom=[f for f in bm2.faces if f.index not in keep], context="FACES")
        bm2.to_mesh(part.data)
        bm2.free()
        bmesh.ops.delete(bm, geom=sel, context="FACES")
        bm.to_mesh(me)
        bm.free()
        me.update()
        part.data.update()
        if pj.get("tris") and tris(part) > pj["tris"]:
            decimate(part, pj["tris"], pristine)
            triangulate(part)
        if "origin_img" in pj:
            Yo, Zo = bmap.yz(*pj["origin_img"])
            org = Vector((0.0, Yo - pivot.y, Zo - pivot.z))
        else:
            c = coords(part)
            org = Vector(((c.min(0) + c.max(0)) / 2).tolist())
        set_origin(part, org)
        part.name = pj["name"]
        part.data.name = f"SM_WPN_{JOB['name']}_{pj['name']}"
        REPORT.setdefault("split", {})[pj["name"]] = {"tris": tris(part), "origin_unity": [round(-org.x, 4), round(org.z, 4), round(-org.y, 4)]}
        log("split", pj["name"], tris(part), "tris; body now", tris(ob))
        out.append(part)
    return out


# ---------------------------------------------------------------- main
name = JOB["name"]
L = JOB["length"]
body = import_glb(JOB["body"], f"SM_WPN_{name}")
orient(body, JOB.get("orient", "side"), JOB.get("img_end_ratio"))
normalise(body, L, JOB.get("length_axis", "Y"))
if JOB.get("cross_section_m"):
    # the reference image got the proportions wrong (e.g. a fat PG-7 warhead): scale the cross-section only
    co = coords(body)
    k = JOB["cross_section_m"] / float(max(co[:, 0].max() - co[:, 0].min(), co[:, 2].max() - co[:, 2].min()))
    body.data.transform(Matrix.Diagonal((k, 1.0, k, 1.0)))
    log(f"cross-section scaled x{k:.3f} to {JOB['cross_section_m']} m")
    REPORT["cross_k"] = k
if JOB.get("cap"):
    cap_top(body, JOB["cap"])
if JOB.get("flat_normals"):
    flat_normals(body, JOB["flat_normals"])
if JOB.get("fix_orientation", True):
    fix_orientation(body)
pristine = duplicate(body, "pristine")
clean(body, L)
if JOB.get("fill_holes"):
    fill_holes(body, JOB["fill_holes"], Map(body, JOB) if "weapon_bbox_img" in JOB else None, L)
transfer_normals(body, pristine)
REPORT["source_tris"] = tris(body)

bmap = Map(body, JOB) if "weapon_bbox_img" in JOB else None

# sockets (body space, before the pivot move)
sockets = {}
for k, v in JOB.get("sockets_img", {}).items():
    Y, Z = bmap.yz(v[0], v[1])
    X = 0.0
    if k == "Eject":
        X = surface_x(body, Y, Z, -1)  # right side of the weapon is -X in Blender
    sockets[k] = Vector((X, Y, Z))
for k, v in JOB.get("sockets_m", {}).items():
    sockets[k] = Vector(v)
for k, dx in JOB.get("sockets_dx", {}).items():  # lateral offsets in metres (Blender +X = the weapon's left side)
    if k in sockets:
        sockets[k].x += dx

# magazine
mag = None
if JOB.get("mag"):
    mj = JOB["mag"]
    mag = import_glb(mj["glb"], f"SM_WPN_{name}_Mag")
    well = place_mag(mag, bmap, mj)
    sockets["MagWell"] = well
    if mj.get("fit_thickness", True):
        fit_mag_thickness(mag, body, mj.get("fit_margin_m", 0.0006))
    if JOB.get("fix_orientation", True):
        fix_orientation(mag)
    mag_pristine = duplicate(mag, "mag_pristine")
    clean(mag, 0.25)
    transfer_normals(mag, mag_pristine)
    decimate(mag, mj["tris"], mag_pristine)

# pivot
pivot = sockets.get(JOB.get("pivot", "Grip"), Vector((0, 0, 0))) if JOB.get("pivot") != "origin" else Vector((0, 0, 0))
if isinstance(JOB.get("pivot"), list):
    pivot = Vector(JOB["pivot"])
for ob in [body, pristine] + ([mag, mag_pristine] if mag else []):
    ob.data.transform(Matrix.Translation(-pivot))
for k in sockets:
    sockets[k] = sockets[k] - pivot
for cp in CAPS:
    cp["c"] = cp["c"] - pivot


def smooth_boxes(ob, boxes):
    """Flatten generator relief (pseudo-text, serial-like squiggles) inside model-space boxes: vertices with Y/Z in
    the box whose normal faces that side are smoothed along X only (no sliding along the surface)."""
    if not boxes:
        return
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.normal_update()
    tot = 0
    for bx in boxes:
        y0, z0, y1, z1 = bx["box"]
        sgn = 1 if bx.get("side", "left") == "left" else -1
        vs = [v for v in bm.verts if y0 <= v.co.y <= y1 and z0 <= v.co.z <= z1 and v.normal.x * sgn > 0.6]
        for _ in range(bx.get("iters", 12)):
            bmesh.ops.smooth_vert(bm, verts=vs, factor=0.8, use_axis_x=True, use_axis_y=False, use_axis_z=False)
        tot += len(vs)
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    log(f"smooth_boxes: {ob.name} {tot} verts flattened")


def recess_ends(ob, specs):
    """Open up an end the generator capped (e.g. the RPG-7 venturi): the faces of the end cap facing out along the
    long axis are pushed inwards as a paraboloid dish (depth at the centre, 0 at radius_frac of the end radius), so
    the nozzle reads as open; the AO bake then darkens it. spec: {"end": "+Y"|"-Y", "depth_m": 0.07,
    "radius_frac": 0.9, "normal_dot": 0.7, "band_m": 0.02}."""
    if not specs:
        return
    me = ob.data
    co = coords(ob)
    for sp in specs:
        sg = 1.0 if sp.get("end", "+Y") == "+Y" else -1.0
        yend = co[:, 1].max() if sg > 0 else co[:, 1].min()
        band = sp.get("band_m", 0.02)
        endv = co[np.abs(co[:, 1] - yend) < 0.004]
        cx, cz = float(np.median(endv[:, 0])), float(np.median(endv[:, 2]))
        R = float(np.sqrt((endv[:, 0] - cx) ** 2 + (endv[:, 2] - cz) ** 2).max())
        rlim = sp.get("radius_frac", 0.9) * R
        sel = set()
        for p in me.polygons:
            if p.normal.y * sg > sp.get("normal_dot", 0.7) and abs(p.center.y - yend) < band:
                sel.update(p.vertices)
        n = 0
        for i in sel:
            v = me.vertices[i]
            r = math.hypot(v.co.x - cx, v.co.z - cz)
            if r < rlim:
                v.co.y -= sg * sp.get("depth_m", 0.07) * (1 - (r / rlim) ** 2)
                n += 1
        me.update()
        log(f"recess: {ob.name} end {sp.get('end', '+Y')} radius {R * 1000:.1f} mm, {n} verts pushed in")


for ob in [body, pristine]:
    smooth_boxes(ob, JOB.get("smooth_boxes", []))
    recess_ends(ob, JOB.get("recess", []))
REPORT["sockets_unity"] = {k: [round(-v.x, 4), round(v.z, 4), round(-v.y, 4)] for k, v in sockets.items()}
REPORT["sockets_blender"] = {k: [round(v.x, 4), round(v.y, 4), round(v.z, 4)] for k, v in sockets.items()}
co = coords(body)
REPORT["bbox_blender_min"] = [round(float(v), 4) for v in co.min(0)]
REPORT["bbox_blender_max"] = [round(float(v), 4) for v in co.max(0)]
log("sockets (Unity local, m):", json.dumps(REPORT["sockets_unity"]))

mat_body = make_mat(f"M_WPN_{name}")
body.data.materials.clear()
body.data.materials.append(mat_body)
if mag:
    mat_mag = make_mat(f"M_WPN_{name}_Mag")
    mag.data.materials.clear()
    mag.data.materials.append(mat_mag)

folder = JOB["folder"]
mdir = os.path.join(READY, "Assets/_Game/Art/Models/Weapons", folder)
tdir = os.path.join(WORK, "tex_out")
os.makedirs(tdir, exist_ok=True)
outputs = []

# ---- FP model
if JOB.get("fp"):
    fp = JOB["fp"]
    decimate(body, fp["tris"], pristine)
    triangulate(body)
    body_nd = duplicate(body, "body_nodouble")  # undoubled copy: world model and LODs are decimated from it
    fix_backfaces(body, fp["tris"], pristine)
    REPORT["fp_tris"] = tris(body)
    if mag:
        mag_nd = duplicate(mag, "mag_nodouble")
        fix_backfaces(mag, JOB["mag"]["tris"], mag_pristine, div=300)
    if JOB.get("bake_ao", True):
        bake_ao(body, os.path.join(tdir, "ao_body.png"), fp.get("ao_size", 1024), JOB.get("ao_distance", 0.05 * L / 0.88))
        if mag:
            bake_ao(mag, os.path.join(tdir, "ao_mag.png"), JOB["mag"].get("ao_size", 512), JOB.get("ao_distance", 0.05 * L / 0.88) * 0.6)
    objs = [body]
    if mag:
        magfp = duplicate(mag, f"SM_WPN_{name}_Mag")
        magfp.name = f"Mag"
        set_origin(magfp, sockets["MagWell"])
        magfp.parent = body
        objs.append(magfp)
    for k, v in sockets.items():
        objs.append(add_empty(f"Socket_{k}", v, body))
    fp_name = f"SM_WPN_{name}_FP"
    body.name = fp_name
    body.data.name = fp_name
    export_fbx(objs, os.path.join(mdir, fp_name + ".fbx"))
    outputs.append(fp_name + ".fbx")
    for o in objs[1:]:
        if o.type == "EMPTY":
            bpy.data.objects.remove(o, do_unlink=True)
    if mag:
        bpy.data.objects.remove(magfp, do_unlink=True)
        # stand-alone magazine (drops, pickups): origin at its well entry point
        magsa = duplicate(mag, f"SM_WPN_{name}_Mag")
        mag.name = "mag_work"
        magsa.name = f"SM_WPN_{name}_Mag"
        set_origin(magsa, sockets["MagWell"])
        magsa.location = (0, 0, 0)
        export_fbx([magsa], os.path.join(mdir, f"SM_WPN_{name}_Mag.fbx"))
        outputs.append(f"SM_WPN_{name}_Mag.fbx")
        bpy.data.objects.remove(magsa, do_unlink=True)

# ---- world model (+ LODs): body and magazine decimated separately (each keeps its own shading), then joined
if JOB.get("world"):
    wj = JOB["world"]
    mag_share = wj.get("mag_tris", int(wj["tris"] * 0.1)) if mag else 0
    src_b = body_nd if JOB.get("fp") else body
    src_m = (mag_nd if JOB.get("fp") else mag) if mag else None
    wb = duplicate(src_b, "wbody")
    wb.parent = None
    world_only = not JOB.get("fp")
    decimate(wb, wj["tris"] - mag_share, pristine)
    triangulate(wb)
    if world_only and JOB.get("bake_ao", True):
        bake_ao(wb, os.path.join(tdir, "ao_body.png"), wj.get("ao_size", 1024), JOB.get("ao_distance", 0.05 * L / 0.88))
    wb_nd = duplicate(wb, "wbody_nd")
    wparts = split_parts(wb, JOB.get("split", []), bmap, pivot) if JOB.get("split") else []
    wb_lod_src = duplicate(wb, "wb_lod_src")  # after the split: LODs never contain the split parts
    nb = JOB.get("bake_normal", "auto")
    if nb == "auto":
        nb = world_only and tris(pristine) > 4 * (tris(wb) + sum(tris(p_) for p_ in wparts))
    # split parts get their back-face copies later (their open cut shows the inside): reserve those triangles too
    parts_final = sum(tris(p_) + ((int(len(all_visible_backfaces(p_, div=300)) * 1.15) + 2) if JOB.get("fix_backfaces", True) else 0)
                      for p_ in wparts)
    budget_b = wj["tris"] - mag_share - parts_final
    if nb:
        # make room for the back-face copies first, so the geometry the map is baked for is the geometry shipped
        if JOB.get("fix_backfaces", True):
            nbf = len(all_visible_backfaces(wb, div=300))
            if tris(wb) + int(nbf * 1.15) + 2 > budget_b:
                decimate(wb, budget_b - int(nbf * 1.15) - 4, pristine)
                triangulate(wb)
        bake_normal([wb] + wparts, pristine, os.path.join(tdir, "n_bake_body.png"), wj.get("nbake_size", 2048),
                    JOB.get("nbake_extrusion", 0.004 * L), JOB.get("nbake_distance", 0.012 * L))
        fix_backfaces(wb, None, None, div=300)
    else:
        fix_backfaces(wb, budget_b, pristine, div=300)
    for p_ in wparts:
        fix_backfaces(p_, div=300)
    wm = None
    if mag:
        wm = duplicate(src_m, "wmag")
        wm.location = (0, 0, 0)
        decimate(wm, mag_share, mag_pristine)
        triangulate(wm)
        wm_lod_src = duplicate(wm, "wm_lod_src")
        fix_backfaces(wm, mag_share, mag_pristine, div=200)
    levels = [(wb, wm)]
    for r in wj.get("lods", [0.5, 0.25]):
        lb = duplicate(wb_lod_src, "lb")
        decimate(lb, int(tris(wb_lod_src) * r), pristine)
        triangulate(lb)
        fix_backfaces(lb, div=200)
        lm = None
        if wm:
            lm = duplicate(wm_lod_src, "lm")
            decimate(lm, max(60, int(tris(wm_lod_src) * r)), mag_pristine)
            triangulate(lm)
            fix_backfaces(lm, div=150)
        levels.append((lb, lm))
    for o_ in [wb_nd, wb_lod_src] + ([wm_lod_src] if mag else []):
        bpy.data.objects.remove(o_, do_unlink=True)
    lods = []
    for i, (lb, lm) in enumerate(levels):
        parts = [lb] + ([lm] if lm else [])
        select_only(parts)
        if len(parts) > 1:
            bpy.ops.object.join()
        o = bpy.context.view_layer.objects.active
        o.name = f"SM_WPN_{name}_W_LOD{i}"
        o.data.name = o.name
        lods.append(o)
    REPORT["world_tris"] = [tris(o) for o in lods]
    log("world LOD tris", REPORT["world_tris"])
    root = bpy.data.objects.new(f"SM_WPN_{name}_W", None)
    bpy.context.collection.objects.link(root)
    for l in lods:
        l.parent = root
    for prt in wparts:
        prt.parent = root
    REPORT["world_part_tris"] = {p.name: tris(p) for p in wparts}
    wobjs = [root] + lods + wparts
    for k in wj.get("sockets", ["Muzzle", "LeftHand"]):
        if k in sockets:
            wobjs.append(add_empty(f"Socket_{k}", sockets[k], root))
    export_fbx(wobjs, os.path.join(mdir, f"SM_WPN_{name}_W.fbx"))
    outputs.append(f"SM_WPN_{name}_W.fbx")

REPORT["outputs"] = outputs
REPORT["seconds"] = round(time.time() - T0, 1)
json.dump(REPORT, open(os.path.join(WORK, "process_report.json"), "w"), indent=1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(WORK, f"{JOB['id']}_processed.blend"))
log("done", outputs)
