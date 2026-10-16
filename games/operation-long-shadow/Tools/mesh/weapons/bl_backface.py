"""Visible-backface detection and repair for generated weapon meshes (Blender, headless).

Unity's URP/Lit culls back faces, so a single-sheet wall that the generator left open (feed towers, thin covers,
sling loops) or a face whose winding points inwards shows up as a hole. A face is a *visible backface* when rays
leaving its back side escape the mesh: an outside viewer in those directions looks at its back.

As a library (bl_weapon.py imports it):
    faces = visible_backfaces(ob, rays=40, thr=0.10)
    double_faces(ob, faces)          # reversed copies, offset 0.1 mm behind, U + 1 (bakes write only the 0-1 tile)
CLI (report only):
    Blender -b --factory-startup -P Tools/mesh/weapons/bl_backface.py -- a.fbx [b.fbx ...]
"""
import bpy, bmesh, math, sys
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def _corner_normals(me):
    cn = np.empty(len(me.loops) * 3)
    try:
        me.corner_normals.foreach_get("vector", cn)
    except Exception:
        me.loops.foreach_get("normal", cn)
    return cn.reshape(-1, 3)


def visible_backfaces(ob, rays=40, thr=0.10, seed=1, extra=()):
    """Indices of faces whose back side is seen from outside (fraction of escaping back-hemisphere rays >= thr).
    `extra`: other objects that occlude (e.g. the magazine inside the well)."""
    me = ob.data
    nv = len(me.vertices)
    verts = [v.co.copy() for v in me.vertices]
    polys = [p.vertices[:] for p in me.polygons]
    for e in extra:
        off = len(verts)
        mw = e.matrix_world
        inv = ob.matrix_world.inverted()
        verts += [inv @ (mw @ v.co) for v in e.data.vertices]
        polys += [tuple(i + off for i in p.vertices) for p in e.data.polygons]
    bvh = BVHTree.FromPolygons(verts, polys)
    fn = np.empty(len(me.polygons) * 3)
    me.polygons.foreach_get("normal", fn)
    fn = fn.reshape(-1, 3)
    fc = np.empty(len(me.polygons) * 3)
    me.polygons.foreach_get("center", fc)
    fc = fc.reshape(-1, 3)
    co = np.array([tuple(v.co) for v in me.vertices])
    diag = float(np.linalg.norm(co.max(0) - co.min(0)))
    eps = 2e-5 * diag
    rng = np.random.default_rng(seed)
    u = rng.random((rays, 2))
    r, ph = np.sqrt(u[:, 0]), 2 * np.pi * u[:, 1]
    local = np.stack([r * np.cos(ph), r * np.sin(ph), np.sqrt(1 - u[:, 0])], 1)
    out = []
    need = max(2, int(math.ceil(thr * rays)))
    for i in range(len(fn)):
        n = -fn[i]  # back side
        if not np.isfinite(n).all() or np.linalg.norm(n) < 1e-9:
            continue
        a = np.array([1.0, 0, 0]) if abs(n[0]) < 0.9 else np.array([0, 1.0, 0])
        t = np.cross(n, a)
        t /= np.linalg.norm(t)
        b = np.cross(n, t)
        dirs = local[:, 0:1] * t + local[:, 1:2] * b + local[:, 2:3] * n
        c = Vector(fc[i])
        esc = 0
        for k, d in enumerate(dirs):
            dv = Vector(d)
            if bvh.ray_cast(c + dv * eps, dv)[0] is None:
                esc += 1
                if esc >= need:
                    out.append(i)
                    break
            elif rays - k - 1 + esc < need:
                break
    return out


def visible_backfaces_view(ob, ndirs=64, cell=None, min_hits=2, seed=2):
    """Faces seen from behind by outside orthographic viewers (what a camera / the review render sees): rays from
    `ndirs` directions on the sphere on a grid of `cell` m spacing (default diag/220) over the projected bbox. A face
    hit first, from its back side, by >= min_hits rays is returned. Catches large faces whose back is only partly
    exposed (double walls ~1 mm apart) or seen through slits (gas-tube vents, handguard gaps), which the per-face
    centre test misses. Default spacing diag/450 (2.3 mm on a 1 m rifle); each direction's grid gets a random offset."""
    me = ob.data
    verts = [v.co.copy() for v in me.vertices]
    bvh = BVHTree.FromPolygons(verts, [p.vertices[:] for p in me.polygons])
    co = np.array([tuple(v) for v in verts])
    mn, mx = co.min(0), co.max(0)
    diag = float(np.linalg.norm(mx - mn))
    cell = cell or diag / 450
    corners = np.array([[x, y, z] for x in (mn[0], mx[0]) for y in (mn[1], mx[1]) for z in (mn[2], mx[2])])
    ctr = (mn + mx) / 2
    fn = np.empty(len(me.polygons) * 3)
    me.polygons.foreach_get("normal", fn)
    fn = fn.reshape(-1, 3)
    rng = np.random.default_rng(seed)
    # quasi-uniform directions (Fibonacci sphere) with a random spin
    i = np.arange(ndirs) + 0.5
    phi = np.arccos(1 - 2 * i / ndirs)
    th = math.pi * (1 + 5 ** 0.5) * i + rng.random() * 6.283
    dirs = np.stack([np.cos(th) * np.sin(phi), np.sin(th) * np.sin(phi), np.cos(phi)], 1)
    hits = {}
    for d in dirs:
        a = np.array([1.0, 0, 0]) if abs(d[0]) < 0.9 else np.array([0, 1.0, 0])
        t = np.cross(d, a)
        t /= np.linalg.norm(t)
        b = np.cross(d, t)
        pt, pb = (corners - ctr) @ t, (corners - ctr) @ b
        dv = Vector(d)
        ju, jw = rng.random(2) * cell
        for u in np.arange(pt.min() - cell + ju, pt.max() + cell, cell):
            for w in np.arange(pb.min() - cell + jw, pb.max() + cell, cell):
                org = Vector(ctr + t * u + b * w - d * diag)
                loc, nrm, fi, dist = bvh.ray_cast(org, dv)
                if fi is not None and float(fn[fi] @ d) > 1e-4:
                    hits[fi] = hits.get(fi, 0) + 1
    return [fi for fi, c in hits.items() if c >= min_hits]


def doubled_mask(ob):
    a = ob.data.attributes.get("bf_dbl")
    m = np.zeros(len(ob.data.polygons), int)
    if a is not None:
        a.data.foreach_get("value", m)
    return m


def _diag(ob):
    co = np.array([tuple(v.co) for v in ob.data.vertices])
    return float(np.linalg.norm(co.max(0) - co.min(0)))


def all_visible_backfaces(ob, view=True, div=450, **kw):
    """Union of the per-face test and the view test (grid spacing diag/div, never below 1.2 mm)."""
    s = set(visible_backfaces(ob, **kw))
    if view:
        s |= set(visible_backfaces_view(ob, cell=max(_diag(ob) / div, 0.0012)))
    dm = doubled_mask(ob)
    return sorted(i for i in s if not dm[i])


def fix_all(ob, passes=2, div=450):
    """Detect and double visible back faces, `passes` times (a pass can expose new ones). Returns faces added."""
    n = 0
    for _ in range(passes):
        bf = all_visible_backfaces(ob, div=div)
        if not bf:
            break
        n += double_faces(ob, bf)
    return n


def double_faces(ob, faces, offset=1e-4, u_shift=1.0):
    """Add a reversed copy of each face (own vertices, so every copy sits exactly `offset` m behind its own source
    face), with the negated shading normals of the source corners. The copy's UVs are shifted by u_shift (texture
    wrap Repeat samples the same texels; Cycles bakes only write the 0-1 tile, so bakes ignore the copies)."""
    if not faces:
        return 0
    me = ob.data
    cn = _corner_normals(me)
    F = len(me.polygons)
    all_norm = [tuple(cn[li]) for p in me.polygons for li in p.loop_indices]
    # source corner normals in loop order
    src_loops = {fi: [tuple(cn[li]) for li in me.polygons[fi].loop_indices] for fi in faces}
    bm = bmesh.new()
    bm.from_mesh(me)
    lay = bm.faces.layers.int.get("bf_dbl") or bm.faces.layers.int.new("bf_dbl")
    bm.normal_update()
    bm.faces.ensure_lookup_table()
    uvl = bm.loops.layers.uv.active
    order = []
    srcf = [(fi, bm.faces[fi]) for fi in faces]
    for fi, f in srcf:
        f[lay] = 1  # sources and copies are never doubled again
        n = f.normal.copy()
        loops = list(f.loops)[::-1]
        nv = [bm.verts.new(lp.vert.co - n * offset) for lp in loops]
        try:
            g = bm.faces.new(nv, f)  # example face: material index, smooth flag, layers
        except ValueError:
            continue
        g[lay] = 1
        if uvl is not None:
            for lp_new, lp_src in zip(g.loops, loops):
                uv = lp_src[uvl].uv.copy()
                uv.x += u_shift
                lp_new[uvl].uv = uv
        order.append(fi)
    bm.to_mesh(me)
    bm.free()
    me.update()
    lnorm = list(all_norm)
    for fi in order:
        sl = src_loops[fi][::-1]
        lnorm += [tuple(-x for x in n) for n in sl]
    for p in me.polygons:
        p.use_smooth = True
    me.normals_split_custom_set(lnorm)
    me.update()
    return len(order)


if __name__ == "__main__" and "--" in sys.argv:
    files = sys.argv[sys.argv.index("--") + 1:]
    for f in files:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=f)
        for o in bpy.data.objects:
            if o.type != "MESH":
                continue
            import time
            t0 = time.time()
            bf = visible_backfaces(o)
            t1 = time.time()
            bv = visible_backfaces_view(o)
            print(f"BACKFACE {f.split('/')[-1]} {o.name}: tris {len(o.data.polygons)} face-test {len(bf)} ({t1 - t0:.1f}s) "
                  f"view-test {len(bv)} ({time.time() - t1:.1f}s) union {len(set(bf) | set(bv))}")
