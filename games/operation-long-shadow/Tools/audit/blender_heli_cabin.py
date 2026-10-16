"""VEH_HELI_CABIN (CONTENT_AUDIT gap fill, owner WP10): Huey-style cabin interior kit for S8_Heli and the insertion
cinematic, built procedurally in Blender (headless), plus a doors-open copy of WP10_FAL's SM_Helicopter.

  Blender -b --factory-startup -t 2 -P Tools/audit/blender_heli_cabin.py -- --ready RawAssets/wp10/ready --out <work>

Frames (Unity, metres): cabin frame = GDD §8.0 (+Z nose, +X right, cabin floor Y = 0, left door-gun pivot
(-1.05, 1.10, 0.30)). The cabin sits in SM_Helicopter at local (0, 1.00, 2.375): floor 1.0 m above the skids
(VehicleRoutes.HeliCabinFloorHeight) and the door centre (measured dark door panels: heli z 2.00-3.35, y 1.08-2.50)
on the gun pivot. FBX export = WP10 convention (axis_forward -Z, up Y, bake space transform; Unity = (-bx, bz, -by)).
UVs are metric (1 UV = 1 m) on every tiling surface so the WP10 env materials (M_GreenMetalRust, M_MetalPlate,
M_Fabric066, M_MetalPlates013) bind by slot name; the instrument faces use 0..1 UVs of T_HeliCabin_Gauges.
"""
import bpy, bmesh, json, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
READY = argv[argv.index("--ready") + 1]
OUT = argv[argv.index("--out") + 1]
RENDER = "--render" in argv
os.makedirs(OUT, exist_ok=True)
VEH = os.path.join(READY, "Assets/_Game/Art/Models/Vehicles")
TEXV = os.path.join(READY, "Assets/_Game/Art/Textures/Vehicles")
ENV = os.path.join(READY, "Assets/_Game/Art/Textures/Env")
CABIN_IN_HELI = (0.0, 1.0, 2.375)  # Unity local position of the cabin root under SM_Helicopter
DOOR = dict(z0=-0.38, z1=0.98, y0=0.06, y1=1.44)  # cabin-frame door opening (both sides)
HALF_W = 1.36     # interior wall half width
WALL_TOP = 1.46   # top of the vertical wall
CEIL = 1.72       # ceiling clamp (hull roof inner side is ~1.70-1.80 above the floor)
CEIL_HW = 1.08    # ceiling half width (after the upper chamfer)
Z_AFT = -1.36     # rear bulkhead
Z_FWD = 3.25      # windshield base / nose limit

MATS = ["M_GreenMetalRust", "M_MetalPlate", "M_HeliCabin_Canvas", "M_MetalPlates013", "M_HeliCabin_Dark", "M_HeliCabin_Glass",
        "M_HeliCabin_RedLamp", "M_HeliCabin_Gauges"]


def B(p):
    """Unity cabin-frame point -> Blender."""
    return Vector((-p[0], -p[2], p[1]))


class Kit:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.mat_index = {m: i for i, m in enumerate(MATS)}

    def face(self, mat, pts_u, facing_u=None, uvs=None, raw=False):
        """Quad/ngon; pts and facing in Unity cabin coords (raw=True: already Blender coords)."""
        conv = (lambda p: Vector(p)) if raw else B
        vs = [self.bm.verts.new(conv(p)) for p in pts_u]
        f = self.bm.faces.new(vs)
        f.material_index = self.mat_index[mat]
        f.normal_update()
        if facing_u is not None:
            want = Vector(facing_u) if raw else (B(facing_u) - B((0, 0, 0)))
            if f.normal.dot(want) < 0:
                f.normal_flip()
        # metric planar UVs (Unity axes) unless given
        n = f.normal
        nu = Vector((-n.x, n.z, -n.y))  # Blender normal -> Unity
        ax = max(range(3), key=lambda i: abs(nu[i]))
        for l in f.loops:
            co = l.vert.co
            u = (-co.x, co.z, -co.y)
            if uvs is not None:
                continue
            if ax == 1:
                l[self.uv].uv = (u[0], u[2])
            elif ax == 0:
                l[self.uv].uv = (u[2], u[1])
            else:
                l[self.uv].uv = (u[0], u[1])
        if uvs is not None:
            loops = list(f.loops)
            # uvs are given in the order of pts_u; a flip reverses the loop order
            order = [v.co.copy() for v in vs]
            for l in loops:
                k = min(range(len(order)), key=lambda i: (order[i] - l.vert.co).length)
                l[self.uv].uv = uvs[k]
        return f

    def box(self, mat, c, s, skip=(), rot_y=0.0):
        """Axis-aligned box in Unity coords (optional yaw about Y through the centre); skip any of
        '+x','-x','+y','-y','+z','-z'."""
        cx, cy, cz = c
        hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
        ca, sa = math.cos(rot_y), math.sin(rot_y)

        def P(x, y, z):
            return (cx + x * ca + z * sa, cy + y, cz - x * sa + z * ca)

        def N(x, y, z):
            return (x * ca + z * sa, y, -x * sa + z * ca)
        quads = {
            "+x": ([(hx, -hy, -hz), (hx, hy, -hz), (hx, hy, hz), (hx, -hy, hz)], (1, 0, 0)),
            "-x": ([(-hx, -hy, hz), (-hx, hy, hz), (-hx, hy, -hz), (-hx, -hy, -hz)], (-1, 0, 0)),
            "+y": ([(-hx, hy, -hz), (-hx, hy, hz), (hx, hy, hz), (hx, hy, -hz)], (0, 1, 0)),
            "-y": ([(-hx, -hy, -hz), (hx, -hy, -hz), (hx, -hy, hz), (-hx, -hy, hz)], (0, -1, 0)),
            "+z": ([(-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz)], (0, 0, 1)),
            "-z": ([(hx, -hy, -hz), (-hx, -hy, -hz), (-hx, hy, -hz), (hx, hy, -hz)], (0, 0, -1)),
        }
        for k, (pts, n) in quads.items():
            if k in skip:
                continue
            self.face(mat, [P(*p) for p in pts], N(*n))

    def cyl(self, mat, a, b, r, segs=8, caps=True):
        a, b = Vector(a), Vector(b)
        d = (b - a)
        L = d.length
        d.normalize()
        t = Vector((1, 0, 0)) if abs(d.x) < 0.9 else Vector((0, 1, 0))
        u = d.cross(t).normalized()
        v = d.cross(u).normalized()
        ra, rb = [], []
        for i in range(segs):
            ang = 2 * math.pi * i / segs
            o = (u * math.cos(ang) + v * math.sin(ang)) * r
            ra.append(a + o)
            rb.append(b + o)
        for i in range(segs):
            j = (i + 1) % segs
            mid = (ra[i] + rb[j]) / 2
            out = (mid - (a + d * ((mid - a).dot(d)))).normalized()
            f = self.face(mat, [tuple(ra[i]), tuple(ra[j]), tuple(rb[j]), tuple(rb[i])], tuple(out), raw=True)
            f.smooth = True
        if caps:
            self.face(mat, [tuple(p) for p in ra], tuple(-d), raw=True)
            self.face(mat, [tuple(p) for p in rb], tuple(d), raw=True)

    def finish(self, name):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in MATS:
            mat = bpy.data.materials.get(m) or bpy.data.materials.new(m)
            me.materials.append(mat)
        ob = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(ob)
        return ob


def door_frames(k, sx, xw):
    """Cargo door trims on one side (sx = -1 left, +1 right); xw = shell half width at the door."""
    d = DOOR
    pz = 0.07
    xi = sx * (xw - 0.05)
    for zc in (d["z0"] - pz / 2, d["z1"] + pz / 2):
        k.box("M_GreenMetalRust", (xi, d["y1"] / 2 + 0.02, zc), (0.12, d["y1"] + 0.04, pz))
    k.box("M_GreenMetalRust", (xi, d["y1"] + 0.045, (d["z0"] + d["z1"]) / 2), (0.12, 0.09, d["z1"] - d["z0"] + 2 * pz))
    k.box("M_MetalPlates013", (sx * (xw - 0.01), d["y1"] + 0.03, (d["z0"] + d["z1"]) / 2), (0.04, 0.04, d["z1"] - d["z0"]))
    k.box("M_MetalPlates013", (sx * (xw - 0.06), 0.02, (d["z0"] + d["z1"]) / 2), (0.12, 0.04, d["z1"] - d["z0"]))


def hull_shell(k, hull):
    """Interior skin that follows the Tripo hull: rings of rays from the cabin axis to the hull (inset 3 cm), clamped to
    the floor (y 0), a ceiling (CEIL) and a max half width; faces in the cargo door rect are left open, faces whose
    hull hit is a dark painted window become glass. Returns the half width at the door and the floor outline."""
    mw = hull.matrix_world
    inv = mw.inverted()
    inv3 = inv.to_3x3()
    me = hull.data
    img = bpy.data.images.load(os.path.join(TEXV, "helicopter", "T_Helicopter_BC.png"))
    W, H = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)
    uvl = me.uv_layers.active.data
    lum = np.zeros(len(me.polygons), np.float32)
    for p in me.polygons:
        u = sum(uvl[i].uv[0] for i in p.loop_indices) / p.loop_total
        v = sum(uvl[i].uv[1] for i in p.loop_indices) / p.loop_total
        lum[p.index] = px[min(H - 1, max(0, int(v * H))), min(W - 1, max(0, int(u * W))), :3].mean()
    off = Vector(CABIN_IN_HELI)

    def cast(o, d):
        ob = B((o[0] + off.x, o[1] + off.y, o[2] + off.z))
        db = (B(d) - B((0, 0, 0))).normalized()
        hit, loc, nor, idx = hull.ray_cast(inv @ ob, (inv3 @ db).normalized())
        if not hit:
            return None
        wb = mw @ loc
        nb = (mw.to_3x3() @ nor).normalized()
        hu = Vector((-wb.x - off.x, wb.z - off.y, -wb.y - off.z))
        nu = Vector((-nb.x, nb.z, -nb.y))
        return (hu - Vector(o)).length, float(lum[idx]), nu

    INSET, XMAX, N = 0.05, 1.42, 48
    zs = sorted(set([round(z, 3) for z in np.arange(Z_AFT, 4.2, 0.1)] + [DOOR["z0"], DOOR["z1"]]))
    rings = []
    for z in zs:
        up = cast((0, -4.0, z), (0, 1, 0))
        dn = cast((0, 6.0, z), (0, -1, 0))
        if up is None or dn is None:
            break
        ybot, ytop = -4.0 + up[0], 6.0 - dn[0]
        lo, hi = max(ybot, 0.0), min(ytop - INSET, CEIL)
        if hi - lo < 0.45:
            break
        yc = (lo + hi) / 2
        ring, last = [], None
        for i in range(N):
            th = 2 * math.pi * i / N
            d = Vector((math.sin(th), math.cos(th), 0.0))
            r = cast((0, yc, z), tuple(d))
            if r is None:
                r = last or (0.5, 1.0, -d)
            dist, l, nu = r
            last = r
            # inset 5 cm along the hull normal (oriented against the ray), so chords between rings never poke out
            if nu.dot(d) < 0:
                nu = -nu
            cosang = max(0.25, abs(nu.dot(d)))
            t = dist - INSET / cosang
            clamped = False
            for lim, comp in ((CEIL, 1), (0.0, 1)):
                if (comp == 1 and lim == CEIL and d.y > 1e-6 and yc + d.y * t > CEIL) or (lim == 0.0 and d.y < -1e-6 and yc + d.y * t < 0.0):
                    t = (lim - yc) / d.y
                    clamped = True
            if abs(d.x) > 1e-6 and abs(d.x * t) > XMAX:
                t = XMAX / abs(d.x)
                clamped = True
            p = (d.x * t, yc + d.y * t, z)
            ring.append((p, 1.0 if clamped else l, p[1] <= 1e-4))
        rings.append((z, yc, ring))
    # faces
    def in_door(c):
        return abs(c[0]) > 0.8 and DOOR["z0"] - 1e-4 <= c[2] <= DOOR["z1"] + 1e-4 and DOOR["y0"] <= c[1] <= DOOR["y1"] + 0.05
    n_glass = n_wall = 0
    floor_outline = []
    # classify every face on the (slice, angle) grid, then turn each connected dark region (painted window on the
    # hull) into its bounding rectangle in grid space, so window openings get straight edges instead of stair steps
    S_, grid = len(rings) - 1, {}
    for si in range(S_):
        (z0, yc0, r0), (z1, yc1, r1) = rings[si], rings[si + 1]
        for i in range(N):
            j = (i + 1) % N
            q = [r0[i], r0[j], r1[j], r1[i]]
            if all(v[2] for v in q):
                continue
            pts = [v[0] for v in q]
            c = tuple(sum(p[k_] for p in pts) / 4 for k_ in range(3))
            if in_door(c):
                continue
            grid[(si, i)] = (pts, c, sum(v[1] for v in q) / 4 < 0.17 and c[1] > 0.30)
    seen, glass = set(), set()
    for key, (_, _, dk) in grid.items():
        if not dk or key in seen:
            continue
        comp, stack = [], [key]
        seen.add(key)
        while stack:
            a_ = stack.pop()
            comp.append(a_)
            for nb in ((a_[0] + 1, a_[1]), (a_[0] - 1, a_[1]), (a_[0], (a_[1] + 1) % N), (a_[0], (a_[1] - 1) % N)):
                if nb in grid and grid[nb][2] and nb not in seen:
                    seen.add(nb)
                    stack.append(nb)
        if len(comp) < 4:
            continue
        ii = [c_[1] for c_ in comp]
        if min(ii) < 6 and max(ii) > N - 6:
            ii = [x if x >= N // 2 else x + N for x in ii]
        s0, s1 = min(c_[0] for c_ in comp), max(c_[0] for c_ in comp)
        i0, i1 = min(ii), max(ii)
        for si in range(s0, s1 + 1):
            for i in range(i0, i1 + 1):
                glass.add((si, i % N))
    for key, (pts, c, dk) in grid.items():
        si = key[0]
        z0, yc0, _ = rings[si]
        z1, yc1, _ = rings[si + 1]
        axis = (0, (yc0 + yc1) / 2, (z0 + z1) / 2)
        facing = (axis[0] - c[0], axis[1] - c[1], 0.0)
        isg = key in glass
        k.face("M_HeliCabin_Glass" if isg else "M_GreenMetalRust", pts, facing)
        n_glass += isg
        n_wall += not isg
    # floor between the floor-clamped points of consecutive rings
    def floor_x(ring):
        xs = [v[0][0] for v in ring if v[2]]
        return (min(xs), max(xs)) if xs else None
    for si in range(len(rings) - 1):
        a, b_ = floor_x(rings[si][2]), floor_x(rings[si + 1][2])
        if a and b_:
            z0, z1 = rings[si][0], rings[si + 1][0]
            k.face("M_MetalPlate", [(a[0], 0, z0), (a[1], 0, z0), (b_[1], 0, z1), (b_[0], 0, z1)], (0, 1, 0))
            floor_outline.append((z0, a))
    # rear bulkhead and nose closure (fans)
    z0, yc0, r0 = rings[0]
    k.face("M_GreenMetalRust", [v[0] for v in r0], (0, 0, 1))
    zl, ycl, rl = rings[-1]
    k.face("M_HeliCabin_Dark", [v[0] for v in rl], (0, 0, -1))
    # half width at the door centre
    dz = min(rings, key=lambda r: abs(r[0] - (DOOR["z0"] + DOOR["z1"]) / 2))
    xw = max(abs(v[0][0]) for v in dz[2])
    ceil_at = [(r[0], max(v[0][1] for v in r[2] if abs(v[0][0]) < 0.25)) for r in rings]
    SHELL_CEIL[:] = ceil_at
    SHELL_RINGS[:] = rings
    return {"rings": len(rings), "z_end": zl, "glass_faces": n_glass, "wall_faces": n_wall, "door_half_width": xw}


SHELL_CEIL = []


def ceil_y(z):
    """Shell ceiling height (|x| < 0.25) at z, from the last hull_shell run."""
    if not SHELL_CEIL:
        return CEIL
    return min(SHELL_CEIL, key=lambda c: abs(c[0] - z))[1]


def ceil_min(z0, z1):
    return min([c[1] for c in SHELL_CEIL if z0 - 0.05 <= c[0] <= z1 + 0.05] or [CEIL])


def ribs(k):
    """Fuselage frames (C-channel ribs) on the walls and across the ceiling; stringers along the chamfer."""
    for z in (-1.0, d_mid(), 1.3):
        # ceiling arch: two chamfer pieces + a cross beam
        k.box("M_GreenMetalRust", (0, ceil_y(z) - 0.045, z), (1.5, 0.07, 0.06), skip=("+y",))
    # stringers along the ceiling
    for x in (-0.55, 0.55):
        k.box("M_GreenMetalRust", (x, ceil_min(-1.3, 1.2) - 0.03, -0.05), (0.05, 0.05, 2.5), skip=("+y",))


def d_mid():
    return (DOOR["z0"] + DOOR["z1"]) / 2


def floor_and_ceiling(k):
    # soundproofing blankets (quilted canvas) on the ceiling and upper aft walls between the ribs
    for z0, z1 in ((-0.95, -0.42), (0.34, 1.2)):
        yb = ceil_min(z0, z1) - 0.035
        k.face("M_HeliCabin_Canvas", [(-0.8, yb, z0), (0.8, yb, z0), (0.8, yb, z1), (-0.8, yb, z1)], (0, -1, 0))
    # tie-down rings (recessed cups) in a grid
    for z in (-1.0, -0.2, 0.6):
        for x in (-0.9, -0.3, 0.3, 0.9):
            k.cyl("M_MetalPlates013", (-x, -z, 0.002), (-x, -z, 0.012), 0.035, segs=8)
    # rear bulkhead with the transmission well cover
    ct = ceil_min(-1.35, -0.55)
    k.box("M_GreenMetalRust", (0, ct - 0.11, -0.95), (0.9, 0.22, 0.8), skip=("+y",))
    # red cabin lamp (GDD: (0, 1.90, 0); the Tripo hull roof is at ~1.75, so the lamp hangs at 1.62) + white dome light aft
    cy0 = ceil_y(0.0)
    k.cyl("M_HeliCabin_Dark", (0, 0, cy0), (0, 0, cy0 - 0.05), 0.075, segs=12)
    k.cyl("M_HeliCabin_RedLamp", (0, 0, cy0 - 0.05), (0, 0, cy0 - 0.075), 0.06, segs=12)
    k.cyl("M_HeliCabin_Dark", (0, 0.95, ct - 0.22), (0, 0.95, ct - 0.25), 0.06, segs=10)
    # grab rails along the ceiling
    for x in (-0.7, 0.7):
        gy = ceil_min(-1.2, 0.9)
        k.cyl("M_MetalPlates013", (-x, 1.2, gy - 0.09), (-x, -0.9, gy - 0.09), 0.016, segs=6)
        for z in (-1.2, 0.9):
            k.cyl("M_MetalPlates013", (-x, -z, gy - 0.09), (-x, -z, gy), 0.012, segs=6, caps=False)


def troop_bench(k):
    """Canvas troop seat across the rear bulkhead (tubular frame, canvas pan and back, webbing straps)."""
    z_back = Z_AFT + 0.04
    zf = Z_AFT + 0.48
    y_seat = 0.42
    T = "M_MetalPlates013"
    k.cyl(T, B((-1.22, y_seat, zf)), B((1.22, y_seat, zf)), 0.018, segs=8)
    k.cyl(T, B((-1.22, y_seat, z_back + 0.02)), B((1.22, y_seat, z_back + 0.02)), 0.016, segs=6)
    for x in (-1.15, -0.4, 0.4, 1.15):
        k.cyl(T, B((x, 0.0, zf - 0.04)), B((x, y_seat, zf)), 0.015, segs=6)
    k.box("M_HeliCabin_Canvas", (0, y_seat - 0.01, (zf + z_back) / 2), (2.4, 0.02, zf - z_back - 0.03))
    k.box("M_HeliCabin_Canvas", (0, 0.78, z_back + 0.015), (2.4, 0.62, 0.02))
    k.cyl(T, B((-1.22, 1.1, z_back + 0.04)), B((1.22, 1.1, z_back + 0.04)), 0.015, segs=6)
    for x in (-1.0, -0.5, 0.0, 0.5, 1.0):
        k.box("M_HeliCabin_Dark", (x, 0.76, z_back + 0.03), (0.05, 0.66, 0.008))
    # lap belts hanging off the front tube
    for x in (-0.95, -0.25, 0.45, 1.05):
        k.box("M_HeliCabin_Dark", (x, y_seat - 0.12, zf + 0.012), (0.045, 0.24, 0.006))


def gunner_station(k, sx):
    """Door-gun post at (sx*1.05, 0..1.10, 0.30) with pintle sleeve, brace and ammo tray; folding gunner seat inboard."""
    px, pz = sx * 1.05, 0.30
    D = "M_HeliCabin_Dark"
    k.cyl(D, B((px, 0.0, pz)), B((px, 1.02, pz)), 0.035, segs=10)
    k.cyl(D, B((px, 1.02, pz)), B((px, 1.10, pz)), 0.05, segs=10)
    k.cyl(D, B((px, 0.0, pz - 0.35)), B((px, 0.6, pz)), 0.018, segs=6)
    k.box(D, (px, 0.03, pz), (0.22, 0.06, 0.22))
    # ammo can tray + can (olive) beside the post
    k.box("M_GreenMetalRust", (sx * 0.92, 0.17, 0.62), (0.15, 0.24, 0.30))
    k.box("M_HeliCabin_Dark", (sx * 0.92, 0.30, 0.62), (0.16, 0.02, 0.31))
    # gunner seat: canvas on a tube frame, facing outboard, pan at 0.45 (eye 1.25)
    T = "M_MetalPlates013"
    sxs = sx * 0.62
    for dz in (-0.2, 0.2):
        k.cyl(T, B((sxs - sx * 0.18, 0.0, pz + dz)), B((sxs - sx * 0.18, 0.45, pz + dz)), 0.016, segs=6)
        k.cyl(T, B((sxs + sx * 0.18, 0.0, pz + dz)), B((sxs + sx * 0.18, 0.45, pz + dz)), 0.016, segs=6)
    k.box("M_HeliCabin_Canvas", (sxs, 0.45, pz), (0.40, 0.03, 0.44))
    k.box("M_HeliCabin_Canvas", (sxs - sx * 0.21, 0.72, pz), (0.03, 0.40, 0.44))
    k.cyl(T, B((sxs - sx * 0.22, 0.45, pz - 0.22)), B((sxs - sx * 0.22, 0.95, pz - 0.22)), 0.014, segs=6)
    k.cyl(T, B((sxs - sx * 0.22, 0.45, pz + 0.22)), B((sxs - sx * 0.22, 0.95, pz + 0.22)), 0.014, segs=6)


SHELL_RINGS = []


def half_width_at(z, y, tol=0.12):
    """Shell half width at height y for the ring nearest z (from hull_shell)."""
    if not SHELL_RINGS:
        return HALF_W
    r = min(SHELL_RINGS, key=lambda r: abs(r[0] - z))
    xs = [abs(v[0][0]) for v in r[2] if abs(v[0][1] - y) < tol]
    return min(xs) if xs else 0.0


def cockpit(k):
    """Pilot seats, pedestal, instrument panel and overhead console, placed from the measured hull: the panel sits
    0.12 m behind the last ring that is still 0.9 m wide at panel height (the Tripo windshield is further aft than
    a real UH-1's), and everything else is laid out from there."""
    D = "M_HeliCabin_Dark"
    O = "M_GreenMetalRust"
    zp = 1.6
    for r in SHELL_RINGS:
        if r[0] > 1.6 and half_width_at(r[0], 1.0) >= 0.92 and half_width_at(r[0], 1.28) >= 0.88:
            zp = r[0]
    zp -= 0.12
    pw = min(1.7, 2 * (half_width_at(zp, 1.0) - 0.08))
    zs = zp - 1.05            # seat cushion centre
    for sx in (-1, 1):
        x = sx * 0.52
        # armoured seat: olive shell (side armour wings, back plate), canvas-olive cushions, open frame below
        k.box(O, (x, 0.38, zs), (0.50, 0.05, 0.50))
        k.box(O, (x, 0.82, zs - 0.27), (0.50, 0.84, 0.05))
        for side in (-1, 1):
            k.box(O, (x + side * 0.235, 0.66, zs - 0.12), (0.03, 0.52, 0.32))
        k.box("M_HeliCabin_Canvas", (x, 0.44, zs + 0.01), (0.44, 0.07, 0.44))
        k.box("M_HeliCabin_Canvas", (x, 0.78, zs - 0.235), (0.42, 0.56, 0.05))
        k.box("M_HeliCabin_Canvas", (x, 1.14, zs - 0.235), (0.26, 0.14, 0.05))
        for side in (-1, 1):
            for dz in (-0.2, 0.2):
                k.cyl("M_MetalPlates013", B((x + side * 0.2, 0.0, zs + dz)), B((x + side * 0.2, 0.36, zs + dz)), 0.015, segs=6)
        # cyclic, collective, pedals
        k.cyl(D, B((x, 0.0, zs + 0.42)), B((x, 0.70, zs + 0.38)), 0.016, segs=6)
        k.cyl(D, B((x, 0.70, zs + 0.38)), B((x, 0.80, zs + 0.38)), 0.022, segs=6)
        k.cyl(D, B((x - sx * 0.30, 0.30, zs - 0.05)), B((x - sx * 0.30, 0.52, zs + 0.45)), 0.02, segs=6)
        k.box(D, (x, 0.10, zp - 0.08), (0.30, 0.05, 0.10))
    # centre pedestal (radios)
    k.box(D, (0, 0.36, zp - 0.5), (0.34, 0.72, 0.9), skip=("+y",))
    k.face("M_HeliCabin_Gauges", [(-0.17, 0.721, zp - 0.95), (0.17, 0.721, zp - 0.95), (0.17, 0.721, zp - 0.05), (-0.17, 0.721, zp - 0.05)],
           (0, 1, 0), uvs=[(0.70, 0.02), (0.98, 0.02), (0.98, 0.48), (0.70, 0.48)])
    # instrument panel + glare shield
    k.box(D, (0, 1.0, zp + 0.05), (pw, 0.56, 0.10))
    k.face("M_HeliCabin_Gauges", [(-pw / 2 + 0.01, 0.74, zp - 0.001), (pw / 2 - 0.01, 0.74, zp - 0.001), (pw / 2 - 0.01, 1.27, zp - 0.001),
                                  (-pw / 2 + 0.01, 1.27, zp - 0.001)], (0, 0, -1), uvs=[(0.02, 0.52), (0.98, 0.52), (0.98, 0.98), (0.02, 0.98)])
    k.box(D, (0, 1.30, zp - 0.02), (pw + 0.04, 0.04, 0.26))
    # overhead console
    oy = ceil_min(zp - 1.0, zp - 0.2) - 0.03
    k.box(D, (0, oy - 0.045, zp - 0.55), (0.30, 0.09, 0.6), skip=("+y",))
    k.face("M_HeliCabin_Gauges", [(-0.14, oy - 0.091, zp - 0.85), (0.14, oy - 0.091, zp - 0.85), (0.14, oy - 0.091, zp - 0.25), (-0.14, oy - 0.091, zp - 0.25)],
           (0, -1, 0), uvs=[(0.02, 0.02), (0.30, 0.02), (0.30, 0.48), (0.02, 0.48)])
    COCKPIT.update({"panel_z": round(zp, 3), "panel_w": round(pw, 3), "seat_z": round(zs, 3)})


COCKPIT = {}


def build_cabin(hull):
    k = Kit()
    shell = hull_shell(k, hull)
    floor_and_ceiling(k)
    for sx in (-1, 1):
        door_frames(k, sx, shell["door_half_width"])
        gunner_station(k, sx)
    ribs(k)
    troop_bench(k)
    cockpit(k)
    ob = k.finish("SM_HeliCabin")
    # shading: smooth tubes, flat boxes (sharp by angle)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    except Exception:
        for p in ob.data.polygons:
            p.use_smooth = False
    # empties for the code: gun pivots, eye, lamp, squad marks (Unity cabin frame)
    marks = {"Socket_DoorGunPivot_L": (-1.05, 1.10, 0.30), "Socket_DoorGunPivot_R": (1.05, 1.10, 0.30),
             "Mark_PlayerEye": (-0.85, 1.25, 0.30), "Mark_Hollis": (0.10, 1.55, -0.60), "Mark_Sparrow": (0.60, 1.00, 0.90),
             "Mark_Tinker": (1.05, 1.10, 0.30), "Mark_RedLamp": (0.0, ceil_y(0.0) - 0.08, 0.0),
             "Mark_PilotR": (0.52, 0.47, COCKPIT.get("seat_z", 1.9)), "Mark_PilotL": (-0.52, 0.47, COCKPIT.get("seat_z", 1.9))}
    empties = []
    for n, p in marks.items():
        e = bpy.data.objects.new(n, None)
        e.empty_display_size = 0.05
        e.location = B(p)
        e.parent = ob
        bpy.context.collection.objects.link(e)
        empties.append(e)
    return ob, empties, shell


def tri(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def imp(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, axis_forward="-Z", axis_up="Y")
    return [o for o in bpy.data.objects if o not in before and o.type == "MESH"]


def cut_doors(objs):
    """Doors-open copy of SM_Helicopter: delete the baked dark door panels (both sides) on every LOD."""
    off = B(CABIN_IN_HELI)
    removed = {}
    for o in objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        # Tripo shells have some inward-facing faces; harmless from outside, but they show through the cabin
        # glass from inside, so orient everything outwards first (the hull is closed before the door cut)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mw = o.matrix_world
        kill = []
        for f in bm.faces:
            c = mw @ f.calc_center_median()
            n = (mw.to_3x3() @ f.normal).normalized()
            # Unity heli frame: x = -c.x, y = c.z, z = -c.y; door panels: |x| > 1.2, z 2.00..3.35, y 1.08..2.50
            ux, uy, uz = -c.x, c.z, -c.y
            if abs(ux) > 1.15 and 1.98 <= uz <= 3.37 and 1.06 <= uy <= 2.50:
                kill.append(f)
        removed[o.name] = len(kill)
        bmesh.ops.delete(bm, geom=kill, context="FACES")
        bm.to_mesh(o.data)
        bm.free()
        o.name = o.name.replace("SM_Helicopter", "SM_Helicopter_DoorsOpen")
        o.data.name = o.name
    return removed


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="OFF", use_tspace=False, path_mode="STRIP", embed_textures=False,
                             add_leaf_bones=False, bake_anim=False, use_custom_props=False)


# ---------------------------------------------------------------------------------------------- preview render


def tex_mat(name, folder, stem, tile_m=1.0, tint=None, alpha=None, emission=None, uv_scale=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = alpha is None
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bs.outputs[0], out.inputs[0])
    if folder:
        tc = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        s = uv_scale if uv_scale else 1.0 / tile_m
        mp.inputs["Scale"].default_value = (s, s, 1)
        nt.links.new(tc.outputs["UV"], mp.inputs[0])

        def img(suffix, color):
            p = os.path.join(folder, f"{stem}_{suffix}.png")
            if not os.path.exists(p):
                return None
            n = nt.nodes.new("ShaderNodeTexImage")
            n.image = bpy.data.images.load(p)
            if not color:
                n.image.colorspace_settings.name = "Non-Color"
            nt.links.new(mp.outputs[0], n.inputs[0])
            return n
        bc = img("BC", True)
        if bc:
            if tint:
                mul = nt.nodes.new("ShaderNodeVectorMath")
                mul.operation = "MULTIPLY"
                mul.inputs[1].default_value = tint
                nt.links.new(bc.outputs[0], mul.inputs[0])
                nt.links.new(mul.outputs[0], bs.inputs["Base Color"])
            else:
                nt.links.new(bc.outputs[0], bs.inputs["Base Color"])
        nn = img("N", False)
        if nn:
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(nn.outputs[0], nm.inputs["Color"])
            nt.links.new(nm.outputs[0], bs.inputs["Normal"])
        mk = img("M", False)
        if mk:
            sep = nt.nodes.new("ShaderNodeSeparateColor")
            nt.links.new(mk.outputs[0], sep.inputs[0])
            nt.links.new(sep.outputs[0], bs.inputs["Metallic"])
            inv = nt.nodes.new("ShaderNodeMath")
            inv.operation = "SUBTRACT"
            inv.inputs[0].default_value = 1.0
            nt.links.new(mk.outputs["Alpha"], inv.inputs[1])
            nt.links.new(inv.outputs[0], bs.inputs["Roughness"])
        e = img("E", True)
        if e:
            nt.links.new(e.outputs[0], bs.inputs["Emission Color"])
            bs.inputs["Emission Strength"].default_value = 1.5
    elif tint:
        bs.inputs["Base Color"].default_value = (*tint, 1)
    if alpha is not None:
        bs.inputs["Alpha"].default_value = alpha
        bs.inputs["Roughness"].default_value = 0.05
        m.surface_render_method = "BLENDED"
    if emission:
        bs.inputs["Emission Color"].default_value = (*emission, 1)
        bs.inputs["Emission Strength"].default_value = 6.0
    return m


def preview_materials(gauge_dir):
    tex_mat("M_GreenMetalRust", os.path.join(ENV, "green_metal_rust"), "T_GreenMetalRust", 1.0)
    tex_mat("M_MetalPlate", os.path.join(ENV, "metal_plate"), "T_MetalPlate", 0.5)
    tex_mat("M_HeliCabin_Canvas", os.path.join(ENV, "Fabric066"), "T_Fabric066", 0.5, tint=(0.62, 0.6, 0.42))
    tex_mat("M_MetalPlates013", os.path.join(ENV, "MetalPlates013"), "T_MetalPlates013", 2.0)
    tex_mat("M_HeliCabin_Dark", os.path.join(ENV, "MetalPlates013"), "T_MetalPlates013", 2.0, tint=(0.16, 0.16, 0.15))
    tex_mat("M_HeliCabin_Glass", None, None, tint=(0.3, 0.34, 0.32), alpha=0.18)
    tex_mat("M_HeliCabin_RedLamp", None, None, tint=(0.3, 0.02, 0.02), emission=(1.0, 0.05, 0.03))
    tex_mat("M_HeliCabin_Gauges", gauge_dir, "T_HeliCabin_Gauges", uv_scale=1.0)
    tex_mat("M_Helicopter", os.path.join(TEXV, "helicopter"), "T_Helicopter", uv_scale=1.0)


def render_views(cabin, heli_objs, gauge_dir):
    preview_materials(gauge_dir)
    for o in heli_objs:
        o.data.materials.clear()
        o.data.materials.append(bpy.data.materials["M_Helicopter"])
        o.hide_render = not o.name.endswith("LOD0")
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 960, 600
    world = bpy.data.worlds.new("W")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.62, 0.75, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 4.0
    sun.rotation_euler = (math.radians(50), 0, math.radians(120))
    bpy.context.collection.objects.link(sun)
    lamp = bpy.data.objects.new("Red", bpy.data.lights.new("Red", "POINT"))
    lamp.data.energy = 40
    lamp.data.color = (1, 0.15, 0.1)
    bpy.context.collection.objects.link(lamp)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    bpy.context.collection.objects.link(cam)
    sc.camera = cam
    off = Vector(CABIN_IN_HELI)

    def H(p):  # cabin-frame Unity point -> Blender in the heli frame
        return B((p[0] + off.x, p[1] + off.y, p[2] + off.z))
    cabin.location = B(CABIN_IN_HELI)
    lamp.location = H((0, 1.5, 0))
    views = {
        "gunner_out": ((-0.85, 1.25, 0.30), (-3.0, -0.6, 1.0), 75, True),
        "gunner_fwd": ((-0.85, 1.25, 0.30), (-0.6, 0.9, 3.0), 75, True),
        "across_from_right": ((1.15, 1.3, 0.3), (-1.3, 0.7, 0.0), 75, True),
        "cabin_rear": ((0.6, 1.3, 1.6), (-0.2, 0.6, -1.4), 70, True),
        "cockpit": ((0.0, 1.35, 0.6), (0.0, 1.0, 3.2), 70, False),
        "ext_left": ((-6.5, 1.6, 1.2), (0.0, 0.8, 0.3), 45, False),
        "ext_top": ((-6.0, 6.5, 4.0), (0.0, 1.2, 0.8), 40, False),
        "ext_front": ((-2.5, 2.0, 9.0), (0.0, 1.0, 1.0), 40, False),
    }
    views["gunner_fwd_nohull"] = views["gunner_fwd"]
    views["ext_front_nocabin"] = views["ext_front"]
    for name, (eye, look, fov, red) in views.items():
        for o in heli_objs:
            o.hide_render = (not o.name.endswith("LOD0")) or name.endswith("nohull")
        cabin.hide_render = name.endswith("nocabin")
        cam.location = H(eye)
        d = (H(look) - H(eye)).normalized()
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        cam.data.angle = math.radians(fov)
        lamp.data.energy = 25 if red else 0
        sc.render.filepath = os.path.join(OUT, f"cabin_{name}.png")
        bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    heli = imp(os.path.join(VEH, "helicopter", "SM_Helicopter.fbx"))
    lod0 = [o for o in heli if o.name.endswith("LOD0")][0]
    cabin, empties, shell = build_cabin(lod0)
    stats = {"cabin_tris": tri(cabin), "shell": shell, "cockpit": COCKPIT}
    export([cabin] + empties, os.path.join(VEH, "heli_cabin", "SM_HeliCabin.fbx"))
    stats["door_faces_removed"] = cut_doors(heli)
    stats["heli_tris"] = {o.name: tri(o) for o in heli}
    export(sorted(heli, key=lambda o: o.name), os.path.join(VEH, "helicopter", "SM_Helicopter_DoorsOpen.fbx"))
    if RENDER:
        render_views(cabin, heli, os.path.join(READY, "Assets/_Game/Art/Textures/Vehicles/heli_cabin"))
    json.dump(stats, open(os.path.join(OUT, "cabin_stats.json"), "w"), indent=1)
    print("STATS", json.dumps(stats))


main()
