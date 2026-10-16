"""Procedural game tree (gnarled olive / multi-stem pomegranate) with alpha leaf cards, for WP10_FAL.

  Blender -b --factory-startup -P blender_tree.py -- --spec tree_job.json

Spec keys: species (olive|pomegranate), seed, name (SM_<name>), fbx, height, leaf_tiles (list of [u0,v0,u1,v1]
in atlas UV, v up), fruit_uv ([u,v] centre of an opaque fruit patch, optional), bark_tile_m (metres per bark
texture repeat), lods (list of LOD presets 0..2), preview (optional png path).
Mesh layout: material 0 = bark (tiling UV), material 1 = leaves (atlas UV, alpha cutout).
Vertex colours ("Col", exported to FBX): R = sway weight (0 trunk base -> 1 outer leaves),
G = random phase per branch/cluster, B = 1 on leaves/fruit (flutter), 0 on wood. A = 1.
Leaf card normals are bent outward from the canopy centre (soft volumetric shading).
Front = -Y (Unity +Z); origin at the trunk base on the ground; trunk extends 0.25 m below ground.
"""
import bpy, bmesh, json, math, os, random, sys
from mathutils import Vector, Matrix, Quaternion, noise

argv = sys.argv[sys.argv.index("--") + 1:]
spec = json.load(open(argv[argv.index("--spec") + 1]))
SP = spec["species"]


class Branch:
    def __init__(self, pts, radii, level, parent=None, phase=0.0):
        self.pts, self.radii, self.level, self.parent, self.phase = pts, radii, level, parent, phase
        self.children = []


def grow(start, direction, length, r0, r1, level, rng, seg_len, curl, droop, up_bias=0.0):
    pts, radii = [start.copy()], [r0]
    d = direction.normalized()
    n = max(2, int(length / seg_len))
    p = start.copy()
    # per-branch offset into the noise field: neighbouring branches must not share the same meander (one-sided crowns)
    off = Vector((rng.uniform(-80, 80), rng.uniform(-80, 80), rng.uniform(-80, 80)))
    for i in range(1, n + 1):
        t = i / n
        # meander: low-frequency noise on the direction
        nv = noise.noise_vector(p * 0.9 + off)
        d = (d + nv * curl + Vector((0, 0, up_bias - droop * t))).normalized()
        p = p + d * (length / n)
        pts.append(p.copy())
        radii.append(r0 + (r1 - r0) * t)
    return Branch(pts, radii, level, phase=rng.random())


def perp(v):
    a = Vector((0, 0, 1)) if abs(v.z) < 0.9 else Vector((1, 0, 0))
    return v.cross(a).normalized()


def olive_skeleton(rng, H):
    trunks = []
    lean = Vector((rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), 1)).normalized()
    trunk_len = H * rng.uniform(0.22, 0.28)
    trunk = grow(Vector((0, 0, -0.25)), lean, trunk_len + 0.25, 0.36, 0.25, 0, rng, 0.16, 0.35, 0.0)
    trunks.append(trunk)
    top = trunk.pts[-1]
    nlimb = rng.choice([3, 3, 4])
    az0 = rng.uniform(0, math.tau)
    tips = []
    for k in range(nlimb):
        az = az0 + k * math.tau / nlimb + rng.uniform(-0.4, 0.4)
        tilt = math.radians(rng.uniform(34, 54))
        d = Vector((math.cos(az) * math.sin(tilt), math.sin(az) * math.sin(tilt), math.cos(tilt)))
        L = H * rng.uniform(0.40, 0.50)
        limb = grow(top - lean * 0.25, d, L, 0.2, 0.06, 1, rng, 0.18, 0.32, 0.0, 0.05)
        trunk.children.append(limb)
        # level-2 branches along the limb
        n2 = rng.randint(6, 8)
        for j in range(n2):
            idx = int(len(limb.pts) * rng.uniform(0.3, 0.98)) - 1
            idx = max(1, min(len(limb.pts) - 1, idx))
            base = limb.pts[idx]
            ld = (limb.pts[idx] - limb.pts[idx - 1]).normalized()
            side = perp(ld)
            side.rotate(Quaternion(ld, rng.uniform(0, math.tau)))
            out = Vector((base.x, base.y, 0)).normalized() if base.xy.length > 0.05 else side
            d2 = (ld * 0.6 + side * 0.6 + out * 0.9 + Vector((0, 0, 0.25))).normalized()
            L2 = H * rng.uniform(0.18, 0.28)
            r2 = limb.radii[idx] * 0.5
            b2 = grow(base, d2, L2, r2, 0.015, 2, rng, 0.14, 0.6, 0.12, 0.06)
            limb.children.append(b2)
            n3 = rng.randint(3, 5)
            for m in range(n3):
                i3 = max(1, min(len(b2.pts) - 1, int(len(b2.pts) * rng.uniform(0.3, 1.0)) - 1))
                bd = (b2.pts[i3] - b2.pts[i3 - 1]).normalized()
                s3 = perp(bd); s3.rotate(Quaternion(bd, rng.uniform(0, math.tau)))
                d3 = (bd + s3 * 0.9 + Vector((0, 0, rng.uniform(-0.2, 0.3)))).normalized()
                b3 = grow(b2.pts[i3], d3, H * rng.uniform(0.06, 0.11), b2.radii[i3] * 0.6, 0.007, 3, rng, 0.1, 0.7, 0.3)
                b2.children.append(b3)
                tips.append(b3)
            tips.append(b2)
        tips.append(limb)
    return trunks, tips


def pomegranate_skeleton(rng, H):
    trunks, tips = [], []
    ns = rng.randint(4, 6)
    az0 = rng.uniform(0, math.tau)
    for k in range(ns):
        az = az0 + k * math.tau / ns + rng.uniform(-0.3, 0.3)
        tilt = math.radians(rng.uniform(6, 18))
        d = Vector((math.cos(az) * math.sin(tilt), math.sin(az) * math.sin(tilt), math.cos(tilt)))
        o = Vector((math.cos(az), math.sin(az), 0)) * rng.uniform(0.02, 0.08) + Vector((0, 0, -0.25))
        L = H * rng.uniform(0.42, 0.55)
        st = grow(o, d, L + 0.25, rng.uniform(0.045, 0.06), 0.03, 0, rng, 0.15, 0.35, 0.0, 0.05)
        trunks.append(st)
        n1 = rng.randint(3, 5)
        for j in range(n1):
            idx = max(1, min(len(st.pts) - 1, int(len(st.pts) * rng.uniform(0.55, 1.0)) - 1))
            bd = (st.pts[idx] - st.pts[idx - 1]).normalized()
            s = perp(bd); s.rotate(Quaternion(bd, rng.uniform(0, math.tau)))
            out = Vector((st.pts[idx].x, st.pts[idx].y, 0))
            out = out.normalized() if out.length > 0.02 else s
            d1 = (bd * 0.6 + s * 0.5 + out * 0.7).normalized()
            b1 = grow(st.pts[idx], d1, H * rng.uniform(0.22, 0.34), st.radii[idx] * 0.6, 0.012, 1, rng, 0.12, 0.6, 0.15, 0.1)
            st.children.append(b1)
            n2 = rng.randint(3, 5)
            for m in range(n2):
                i2 = max(1, min(len(b1.pts) - 1, int(len(b1.pts) * rng.uniform(0.3, 1.0)) - 1))
                bd2 = (b1.pts[i2] - b1.pts[i2 - 1]).normalized()
                s2 = perp(bd2); s2.rotate(Quaternion(bd2, rng.uniform(0, math.tau)))
                d2 = (bd2 + s2 * 0.8 + Vector((0, 0, rng.uniform(-0.3, 0.2)))).normalized()
                b2 = grow(b1.pts[i2], d2, H * rng.uniform(0.08, 0.15), b1.radii[i2] * 0.6, 0.006, 2, rng, 0.1, 0.7, 0.35)
                b1.children.append(b2)
                tips.append(b2)
            tips.append(b1)
    return trunks, tips



def sc_skeleton(rng, H):
    """Space-colonisation skeleton: attractors fill a crown envelope, nodes grow toward them (natural forking),
    radii from the pipe model. Returns (roots, tips) as Branch chains (a chain follows the thickest child)."""
    import numpy as np
    olive = SP == "olive"
    P = spec.get("sc", {})
    # envelope: a dome (ellipsoid cut below the crown base), wider than tall for olive
    cz = H * P.get("cz", 0.6 if olive else 0.58)
    ax = H * P.get("rxy", 0.56 if olive else 0.48)
    az_ = H * P.get("rz", 0.42 if olive else 0.42)
    base = H * P.get("base", 0.34 if olive else 0.22)
    na = P.get("attractors", 1500 if olive else 1100)
    pts = []
    sq = rng.uniform(0.8, 1.0)                      # slight elliptic footprint
    lob = [(rng.uniform(0, math.tau), rng.uniform(0.08, 0.2)) for _ in range(3)]   # lumpy, irregular crown
    while len(pts) < na:
        v = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1)))
        if v.length > 1:
            continue
        ang = math.atan2(v.y, v.x)
        rr = 1 + sum(a * math.cos(2 * (ang - ph)) for ph, a in lob)
        q = Vector((v.x * ax * rr, v.y * ax * sq * rr, v.z * az_ + cz))
        if q.z < base:
            continue
        # bias to the outer shell (leaves live there) and keep a few holes
        if v.length < 0.55 and rng.random() < 0.7:
            continue
        pts.append(q)
    A = np.array([tuple(p) for p in pts])
    alive = np.ones(len(A), bool)
    # roots: olive = one massive leaning trunk forking low; pomegranate = 4-6 thin stems from the ground
    nodes, parent = [], []
    if olive:
        lean = Vector((rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), 1)).normalized()
        tl = H * P.get("trunk", rng.uniform(0.2, 0.26))
        n = int(tl / 0.15) + 2
        for i in range(n + 1):
            p = Vector((0, 0, -0.25)) + lean * ((tl + 0.25) * i / n)
            p += Vector((noise.noise(Vector((i * 0.3, 1.7, 0.2))) * 0.08, noise.noise(Vector((i * 0.3, 5.1, 0.9))) * 0.08, 0))
            nodes.append(p); parent.append(len(nodes) - 2 if i else -1)
    else:
        ns = rng.randint(4, 6)
        a0 = rng.uniform(0, math.tau)
        for k in range(ns):
            az = a0 + k * math.tau / ns + rng.uniform(-0.3, 0.3)
            tilt = math.radians(rng.uniform(8, 20))
            d = Vector((math.cos(az) * math.sin(tilt), math.sin(az) * math.sin(tilt), math.cos(tilt)))
            o = Vector((math.cos(az), math.sin(az), 0)) * rng.uniform(0.02, 0.07) + Vector((0, 0, -0.25))
            L = H * rng.uniform(0.18, 0.26) + 0.25
            n = int(L / 0.15) + 1
            for i in range(n + 1):
                nodes.append(o + d * (L * i / n)); parent.append(len(nodes) - 2 if i else -1)
    D = P.get("step", 0.13)
    di, dk = P.get("influence", 1.4), P.get("kill", 0.18)
    for it in range(260):
        N = np.array([tuple(p) for p in nodes])
        idx_alive = np.nonzero(alive)[0]
        if len(idx_alive) == 0:
            break
        Aa = A[idx_alive]
        d2 = ((Aa[:, None, :] - N[None, :, :]) ** 2).sum(-1)
        near = d2.argmin(1)
        dmin = np.sqrt(d2[np.arange(len(Aa)), near])
        # kill reached attractors
        kill = dmin < dk
        alive[idx_alive[kill]] = False
        sel = (~kill) & (dmin < di)
        if not sel.any():
            # nothing in range: let the nearest node take one step toward the nearest attractor (bridges gaps)
            j = int(dmin.argmin())
            near_sel = {int(near[j]): [Aa[j]]}
        else:
            near_sel = {}
            for j in np.nonzero(sel)[0]:
                near_sel.setdefault(int(near[j]), []).append(Aa[j])
        grew = 0
        for ni, al in near_sel.items():
            p = nodes[ni]
            dv = Vector((0, 0, 0))
            for q in al:
                w = Vector(tuple(q)) - p
                dv += w / max(w.length, 1e-4)
            dv = dv.normalized() + Vector((0, 0, P.get("up", 0.12))) + noise.noise_vector(p * 1.3) * P.get("wiggle", 0.35)
            q = p + dv.normalized() * D
            nodes.append(q); parent.append(ni); grew += 1
        if grew == 0:
            break
    n = len(nodes)
    kids = [[] for _ in range(n)]
    for i, pa in enumerate(parent):
        if pa >= 0:
            kids[pa].append(i)
    # pipe-model radii
    r = [0.0] * n
    order = range(n - 1, -1, -1)       # children always have larger indices than their parent
    rt = P.get("tip_r", 0.005)
    ex = P.get("pipe", 2.3)
    for i in order:
        if not kids[i]:
            r[i] = rt
        else:
            r[i] = sum(r[c] ** ex for c in kids[i]) ** (1 / ex)
    roots = [i for i in range(n) if parent[i] < 0]
    rb = P.get("base_r", 0.36 if olive else 0.055)
    k = rb / max(r[i] for i in roots)
    r = [x * k for x in r]
    # chains: follow the thickest child; other children start new chains at the fork
    chains, tips = [], []

    def chain_from(start_parent, i, lvl):
        ch_pts = [nodes[start_parent]] if start_parent >= 0 else []
        ch_r = [r[i]] if start_parent >= 0 else []
        cur = i
        while True:
            ch_pts.append(nodes[cur]); ch_r.append(r[cur])
            if not kids[cur]:
                break
            ks = sorted(kids[cur], key=lambda c: -r[c])
            for c in ks[1:]:
                pending.append((cur, c, lvl + 1))
            cur = ks[0]
        # side chain: start inside the parent tube (its open first ring must not poke out at the fork)
        if start_parent >= 0 and len(ch_pts) >= 2:
            dv = (ch_pts[1] - ch_pts[0])
            if dv.length > 1e-5:
                ch_pts[0] = ch_pts[0] - dv.normalized() * r[start_parent] * 0.9
                ch_r[0] = ch_r[0] * 0.85
        # resample by arc length per radius class (fewer rings on thin wood)
        seg = 0.22 if ch_r[0] > 0.1 else 0.32 if ch_r[0] > 0.03 else 0.45
        keep, acc = [0], 0.0
        for j in range(1, len(ch_pts)):
            acc += (ch_pts[j] - ch_pts[j - 1]).length
            if acc >= seg:
                keep.append(j); acc = 0.0
        if keep[-1] != len(ch_pts) - 1:
            keep.append(len(ch_pts) - 1)
        if len(keep) < 2:
            keep = [0, len(ch_pts) - 1]
        br = Branch([ch_pts[j] for j in keep], [ch_r[j] for j in keep], lvl, phase=rng.random())
        br.r0 = ch_r[0]
        chains.append(br)
        if len(ch_pts) >= 2 and ch_r[-1] <= rt * k * 1.01:
            tips.append(br)
        return br

    pending = [(-1, i, 0) for i in roots]
    while pending:
        sp_, i, lvl = pending.pop()
        if sp_ >= 0 and len(nodes) and _len_to_tip(i, kids) < 1:
            continue
        chain_from(sp_, i, lvl)
    for br in chains:
        rr0 = br.radii[0]
        br.level = 0 if rr0 > 0.15 else 1 if rr0 > 0.045 else 2 if rr0 > 0.016 else 3
        if not olive:
            br.level = 0 if rr0 > 0.04 else 1 if rr0 > 0.02 else 2 if rr0 > 0.01 else 3
    return chains, tips


def _depth(i, parent, _c={}):
    key = (id(parent), i)
    if key in _c:
        return _c[key]
    d, j = 0, i
    while parent[j] >= 0:
        j = parent[j]; d += 1
    _c[key] = d
    return d


def _len_to_tip(i, kids):
    n = 0
    while kids[i]:
        i = kids[i][0]; n += 1
    return n

def all_branches(roots):
    out, stack = [], list(roots)
    while stack:
        b = stack.pop()
        out.append(b)
        stack.extend(b.children)
    return out


class MeshBuilder:
    def __init__(self):
        self.v, self.f, self.uv, self.col, self.mat, self.fn = [], [], [], [], [], []   # fn: per-loop custom normals or None

    def add_face(self, idx, uvs, cols, mat, normals=None):
        self.f.append(idx); self.uv.append(uvs); self.col.append(cols); self.mat.append(mat); self.fn.append(normals)


def tube(mb, br, sides, flute, H, bark_m, sway0, sway1):
    pts, radii = br.pts, br.radii
    n = len(pts)
    # parallel-transport frames
    t0 = (pts[1] - pts[0]).normalized()
    nrm = perp(t0)
    rings, vlen = [], [0.0]
    for i in range(1, n):
        vlen.append(vlen[-1] + (pts[i] - pts[i - 1]).length)
    circ = math.tau * radii[0]
    urep = max(1, round(circ / bark_m))
    for i in range(n):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        nrm = (nrm - t * nrm.dot(t)).normalized()
        bin_ = t.cross(nrm)
        ring = []
        s = vlen[i]
        z = pts[i].z
        for k in range(sides + 1):
            th = math.tau * k / sides
            r = radii[i]
            if flute > 0:   # twisted rope-like flutes (olive), stronger near the ground
                base_boost = max(0.0, 1.0 - max(z, 0) / 0.9)
                r *= 1 + flute * (0.6 + 0.8 * base_boost) * math.sin(4 * th + s * 2.2) * 0.5 \
                    + 0.12 * flute * noise.noise(Vector((math.cos(th) * 2, math.sin(th) * 2, s * 1.7)))
                if br.level == 0 and z < 0.35:
                    r *= 1 + 0.55 * (0.35 - max(z, -0.25)) / 0.6     # root flare
            p = pts[i] + (nrm * math.cos(th) + bin_ * math.sin(th)) * r
            mb.v.append(p)
            ring.append((len(mb.v) - 1, k / sides * urep, s / bark_m))
        rings.append(ring)
    for i in range(n - 1):
        w0 = sway0 + (sway1 - sway0) * (vlen[i] / max(vlen[-1], 1e-6))
        w1 = sway0 + (sway1 - sway0) * (vlen[i + 1] / max(vlen[-1], 1e-6))
        for k in range(sides):
            a, b = rings[i][k], rings[i][k + 1]
            c, d = rings[i + 1][k + 1], rings[i + 1][k]
            mb.add_face([a[0], b[0], c[0], d[0]], [(a[1], a[2]), (b[1], b[2]), (c[1], c[2]), (d[1], d[2])],
                        [(w0, br.phase, 0), (w0, br.phase, 0), (w1, br.phase, 0), (w1, br.phase, 0)], 0)


def leaf_card(mb, center, normal, up, size, tile, sway, phase, canopy_c, bend=0.22):
    """A card folded along its long axis (4 tris); tile = (u0,v0,u1,v1)."""
    normal = normal.normalized()
    side = up.cross(normal).normalized()
    up = normal.cross(side).normalized()
    hw, hh = size * 0.5, size * 0.5
    fold = normal * (bend * size * 0.5)
    corners = [center - side * hw - up * hh, center - up * hh + fold, center + side * hw - up * hh,
               center + side * hw + up * hh, center + up * hh + fold, center - side * hw + up * hh]
    u0, v0, u1, v1 = tile
    uvs = [(u0, v0), ((u0 + u1) / 2, v0), (u1, v0), (u1, v1), ((u0 + u1) / 2, v1), (u0, v1)]
    base = len(mb.v)
    mb.v.extend(corners)
    out = []
    for p in corners:
        bn = (p - canopy_c).normalized() * 0.75 + normal * 0.25
        out.append(bn.normalized())
    col = (sway, phase, 1.0)
    for q in ((0, 1, 4, 5), (1, 2, 3, 4)):
        mb.add_face([base + j for j in q], [uvs[j] for j in q], [col] * 4, 1, [out[j] for j in q])


def fruit(mb, c, r, uvc, sway, phase):
    bm_s, rings = 6, 4
    base = len(mb.v)
    verts = []
    for i in range(rings + 1):
        phi = math.pi * i / rings
        for k in range(bm_s):
            th = math.tau * k / bm_s
            verts.append(c + Vector((math.sin(phi) * math.cos(th), math.sin(phi) * math.sin(th), math.cos(phi))) * r)
    mb.v.extend(verts)
    col = (sway, phase, 1.0)
    du = 0.006
    for i in range(rings):
        for k in range(bm_s):
            a = base + i * bm_s + k; b = base + i * bm_s + (k + 1) % bm_s
            c2 = base + (i + 1) * bm_s + (k + 1) % bm_s; d = base + (i + 1) * bm_s + k
            uv = [(uvc[0] - du, uvc[1] - du), (uvc[0] + du, uvc[1] - du), (uvc[0] + du, uvc[1] + du), (uvc[0] - du, uvc[1] + du)]
            nr = [(mb.v[x] - c).normalized() for x in (a, b, c2, d)]
            mb.add_face([a, b, c2, d], uv, [col] * 4, 1, nr)


def build(lod, seed):
    rng = random.Random(seed)
    noise.seed_set(seed) if hasattr(noise, "seed_set") else None
    H = spec["height"]
    if spec.get("skeleton") == "sc":
        brs, tips = sc_skeleton(random.Random(seed), H)
    else:
        trunks, tips = (olive_skeleton if SP == "olive" else pomegranate_skeleton)(random.Random(seed), H)
        brs = all_branches(trunks)
    mb = MeshBuilder()
    bark_m = spec.get("bark_tile_m", 0.7)
    sides_by_level = {0: [12, 8, 6][lod], 1: [8, 6, 5][lod], 2: [5, 4, 0][lod], 3: [4, 0, 0][lod]}
    if spec.get("skeleton") == "sc":
        sides_by_level = {0: [10, 7, 5][lod], 1: [6, 5, 4][lod], 2: [4, 3, 0][lod], 3: [0, 0, 0][lod]}
    elif SP == "pomegranate":
        sides_by_level = {0: [7, 5, 4][lod], 1: [5, 4, 0][lod], 2: [3, 0, 0][lod]}
    sway_rng = {0: (0.0, 0.08), 1: (0.08, 0.35), 2: (0.35, 0.65), 3: (0.65, 0.85)}
    for br in brs:
        sd = sides_by_level.get(br.level, 0)
        if sd < 3:
            continue
        flute = 0.5 if (SP == "olive" and br.level == 0) else (0.22 if SP == "olive" and br.level == 1 else 0.0)
        s0, s1 = sway_rng.get(br.level, (0.7, 0.9))
        tube(mb, br, sd, flute, H, bark_m, s0, s1)
    # canopy centre from tips
    tp = [t.pts[-1] for t in tips]
    cc = sum(tp, Vector()) / len(tp)
    cc.z -= 0.25 * H * 0.2
    tiles = spec["leaf_tiles"]
    keep = [1.0, 0.5, 0.22][lod]
    scale = [1.0, 1.3, 1.85][lod]
    lrng = random.Random(seed * 7 + 1)
    per_tip = spec.get("cards_per_tip", 3)
    min_level = spec.get("leaf_min_level", 2 if SP == "olive" else 1)
    shell = spec.get("shell", 0.45 if SP == "olive" else 0.3)
    # crown ellipsoid from the twig tips: leaves live in its outer shell so the limbs stay visible inside
    tq = [t.pts[-1] for t in tips if t.level >= min_level] or tp
    cx = sum(tq, Vector()) / len(tq)
    rx = max(0.3, max(abs(q.x - cx.x) for q in tq)); ry = max(0.3, max(abs(q.y - cx.y) for q in tq))
    rz = max(0.3, max(abs(q.z - cx.z) for q in tq))
    for t in tips:
        if t.level < min_level:
            continue
        nseg = len(t.pts)
        for k in range(per_tip):
            if lrng.random() > keep:
                continue
            # along the last 55 % of the twig
            idx = int(nseg * lrng.uniform(0.45, 1.0))
            idx = max(1, min(nseg - 1, idx))
            p = t.pts[idx]
            e = math.sqrt(((p.x - cx.x) / rx) ** 2 + ((p.y - cx.y) / ry) ** 2 + ((p.z - cx.z) / rz) ** 2)
            if e < shell and lrng.random() < 0.85:
                continue
            out = (p - cc)
            out = out.normalized() if out.length > 1e-3 else Vector((0, 0, 1))
            nrm = (out * 0.8 + Vector((lrng.uniform(-1, 1), lrng.uniform(-1, 1), lrng.uniform(-0.3, 1))) * 0.7).normalized()
            up = (t.pts[idx] - t.pts[idx - 1]).normalized()
            if abs(up.dot(nrm)) > 0.95:
                up = perp(nrm)
            up.rotate(Quaternion(nrm, lrng.uniform(-0.8, 0.8)))
            size = spec.get("card_size", 0.6) * lrng.uniform(0.75, 1.2) * scale
            c = p + out * size * 0.25
            sway = min(1.0, 0.7 + 0.3 * (c - Vector((0, 0, 0))).length / H)
            tw = spec.get("tile_weights")
            ti = lrng.choices(range(len(tiles)), weights=tw)[0] if tw else lrng.randrange(len(tiles))
            leaf_card(mb, c, nrm, up, size, tiles[ti], sway, lrng.random(), cc)
    if spec.get("fruit_uv") and lod < 2:
        frng = random.Random(seed * 13 + 5)
        nf = [spec.get("fruit_count", 12), spec.get("fruit_count", 12) // 2][lod]
        cand = [t for t in tips if t.level >= 1]
        for i in range(nf):
            t = cand[frng.randrange(len(cand))]
            p = t.pts[-1] + Vector((0, 0, -0.06))
            out = (p - cc)
            if out.length > 0.01:
                p = p - out.normalized() * 0.05
            fruit(mb, p, spec.get("fruit_r", 0.045), spec["fruit_uv"], 0.9, frng.random())
    # normalise: the grown skeleton lands at 80-100 % of the target height; scale uniformly so max z = H
    zmax = max(v.z for v in mb.v)
    k = H / max(zmax, 1e-3)
    mb.v = [v * k for v in mb.v]
    return mb


def to_object(mb, name):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in mb.v], [], mb.f)
    me.update()
    uvl = me.uv_layers.new(name="UVMap")
    ca = me.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")
    li = 0
    loop_normals = []
    for fi, poly in enumerate(me.polygons):
        poly.material_index = mb.mat[fi]
        for j, l in enumerate(poly.loop_indices):
            uvl.data[l].uv = mb.uv[fi][j]
            c = mb.col[fi][j]
            ca.data[l].color = (c[0], c[1], c[2], 1.0)
        poly.use_smooth = True
    # custom normals: leaves bent outward, wood keeps auto smooth normals
    me.calc_loop_triangles()
    ln = [None] * len(me.loops)
    for fi, poly in enumerate(me.polygons):
        if mb.fn[fi] is not None:
            for j, l in enumerate(poly.loop_indices):
                ln[l] = tuple(mb.fn[fi][j])
    # fill wood loops with their smooth vertex normals
    vn = [v.normal.copy() for v in me.vertices]
    for poly in me.polygons:
        for l in poly.loop_indices:
            if ln[l] is None:
                ln[l] = tuple(vn[me.loops[l].vertex_index])
    me.normals_split_custom_set(ln)
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    for mn in ("M_" + spec["name"] + "_Bark", "M_" + spec["name"] + "_Leaves"):
        m = bpy.data.materials.get(mn) or bpy.data.materials.new(mn)
        me.materials.append(m)
    return ob


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    seed = spec["seed"]
    objs = []
    lods = spec.get("lods", [0, 1, 2])
    for lod in lods:
        mb = build(lod, seed)
        nm = f"SM_{spec['name']}_LOD{lod}" if len(lods) > 1 else f"SM_{spec['name']}"
        objs.append(to_object(mb, nm))
    stats = {o.name: tri_count(o) for o in objs}
    ob = objs[0]
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    stats["dims"] = [round(max(p[i] for p in bb) - min(p[i] for p in bb), 3) for i in range(3)]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    os.makedirs(os.path.dirname(spec["fbx"]), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=spec["fbx"], use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="OFF", use_tspace=False, path_mode="STRIP",
                             embed_textures=False, add_leaf_bones=False, bake_anim=False, colors_type="LINEAR")
    print("TREE_STATS", json.dumps(stats))
    if spec.get("stats"):
        json.dump(stats, open(spec["stats"], "w"))


main()
