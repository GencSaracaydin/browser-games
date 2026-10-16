"""Procedural 1970s/80s aviator sunglasses (CHP_SUNGLASSES) for the WP05 Rocketbox males (WP05).

Run: Blender -b --factory-startup -P Tools/characters/blender/make_sunglasses.py -- <out.glb> [eye_y eye_z eye_dx]
Built directly in the shared Rocketbox male head space (Blender coords, metres, character faces -Y): eyes at
(+-0.032, -0.094, 1.686). Every Rocketbox male avatar has the same skeleton, so one mesh fits all of them; rb_export.py
skins it 100 % to Bip01 Head. Geometry: two teardrop lenses (curved, double-sided), a 1.1 mm wire rim around each lens,
the double bridge, nose-pad arms and temples that run back over the ears and hook down. UVs point into a 4-cell atlas
made by make_sunglasses_tex() in build_bodies.py: u < 0.5 frame metal, u >= 0.5 lens.
About 0.9k triangles.
"""
import bpy, bmesh, sys, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
out = argv[0]
EYE_Y = float(argv[1]) if len(argv) > 1 else -0.094
EYE_Z = float(argv[2]) if len(argv) > 2 else 1.686
EYE_DX = float(argv[3]) if len(argv) > 3 else 0.032
bpy.ops.wm.read_factory_settings(use_empty=True)

LENS_W, LENS_H = 0.056, 0.047          # one lens (classic 58 mm aviator, scaled to the Rocketbox head)
BRIDGE = 0.016
FRONT_Y = EYE_Y - 0.031                 # lens plane ~3 cm in front of the eye centre (eyelids at -0.11)
WRAP = math.radians(9)                  # each lens turned toward the temple
PANTO = math.radians(7)                 # lens bottoms tilted toward the cheeks
TOP_Z = EYE_Z + 0.019
TEMPLE_X = 0.071                        # half head width at the temples
EAR_Y = 0.004                           # temple tips run back to the ear
N_OUT = 22


def outline():
    """teardrop: flat-ish top, deepest point toward the nose; (u outward 0..1, v down from the top 0..1)"""
    pts = []
    for i in range(N_OUT):
        t = 2 * math.pi * i / N_OUT
        u = 0.5 + 0.5 * math.cos(t)
        v = 0.45 - 0.45 * math.sin(t)          # 0 (top) .. 0.9
        if v > 0.45:                             # lower half: deeper and pulled toward the nose
            k = (v - 0.45) / 0.45
            v = 0.45 + (v - 0.45) * 1.22
            u -= 0.16 * k * (1 - abs(u - 0.5) * 2) + 0.06 * k
        if v < 0.2:                              # flatten the top bar
            v = v * 0.55
        pts.append((min(max(u, 0.0), 1.0), v))
    return pts


def lens_point(side, u, v):
    """side +1 = character left (+x). Returns world position of a lens outline/interior point."""
    x0 = side * BRIDGE / 2
    x = x0 + side * u * LENS_W
    z = TOP_Z - v * LENS_H
    y = FRONT_Y
    # wrap around a vertical axis at the inner edge, pantoscopic tilt about x at the top
    p = Vector((x - x0, 0, z - TOP_Z))
    p = Matrix.Rotation(-side * WRAP, 3, "Z") @ p
    p = Matrix.Rotation(PANTO, 3, "X") @ p
    # slight spherical bulge of the lens
    du, dv = (u - 0.5) * 2, (v - 0.5) * 2
    bulge = 0.004 * max(0.0, 1 - 0.5 * (du * du + dv * dv))
    return Vector((x0, y, TOP_Z)) + p + Vector((0, -bulge, 0))


bm = bmesh.new()
uvl = bm.loops.layers.uv.new("UVMap")


def face(vs, uvs):
    f = bm.faces.new(vs)
    for l, uv in zip(f.loops, uvs):
        l[uvl].uv = uv
    return f


def tube(path, r, seg=5, uv_x=(0.05, 0.45), uv_y=(0.05, 0.95), closed=False):
    """polyline -> tube (frame wire); UVs inside the metal half of the atlas"""
    rings = []
    n = len(path)
    for i, p in enumerate(path):
        a = path[(i + 1) % n] if (closed or i < n - 1) else path[i]
        b = path[(i - 1) % n] if (closed or i > 0) else path[i]
        t = (a - b).normalized()
        ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        nx = t.cross(ref).normalized()
        ny = t.cross(nx).normalized()
        rings.append([bm.verts.new(p + (nx * math.cos(2 * math.pi * k / seg) + ny * math.sin(2 * math.pi * k / seg)) * r)
                      for k in range(seg)])
    m = n if closed else n - 1
    for i in range(m):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for k in range(seg):
            k1 = (k + 1) % seg
            u0 = uv_x[0] + (uv_x[1] - uv_x[0]) * k / seg
            u1 = uv_x[0] + (uv_x[1] - uv_x[0]) * (k + 1) / seg
            v0 = uv_y[0] + (uv_y[1] - uv_y[0]) * i / m
            v1 = uv_y[0] + (uv_y[1] - uv_y[0]) * (i + 1) / m
            face([r0[k], r0[k1], r1[k1], r1[k]], [(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
    if not closed:  # caps
        for ring, rev in ((rings[0], True), (rings[-1], False)):
            c = bm.verts.new(sum((v.co for v in ring), Vector()) / seg)
            for k in range(seg):
                a, b = ring[k], ring[(k + 1) % seg]
                face([c, b, a] if rev else [c, a, b], [(0.25, 0.5)] * 3)


OUT = outline()
for side in (1, -1):
    # lens: centre fan, both sides (inner face offset 0.6 mm back)
    cen_uv = (0.5, 0.45)
    for back in (False, True):
        off = Vector((0, 0.0006 if back else 0, 0))
        c = bm.verts.new(lens_point(side, *cen_uv) + off)
        ring = [bm.verts.new(lens_point(side, u, v) + off) for (u, v) in OUT]
        for i in range(N_OUT):
            a, b = ring[i], ring[(i + 1) % N_OUT]
            uva = (0.5 + 0.48 * OUT[i][0], 0.02 + 0.96 * OUT[i][1])
            uvb = (0.5 + 0.48 * OUT[(i + 1) % N_OUT][0], 0.02 + 0.96 * OUT[(i + 1) % N_OUT][1])
            uvc = (0.5 + 0.48 * cen_uv[0], 0.02 + 0.96 * cen_uv[1])
            vs = [c, a, b] if (side > 0) != back else [c, b, a]
            face(vs, [uvc, uva, uvb] if (side > 0) != back else [uvc, uvb, uva])
    # rim
    rim = [lens_point(side, u, v) for (u, v) in OUT]
    tube(rim, 0.0011, closed=True)
    # temple: from the outer top corner of the lens back over the ear, then hooking down behind it
    top_out = lens_point(side, 1.0, 0.12)
    hinge = Vector((side * TEMPLE_X, top_out.y + 0.006, top_out.z))
    path = [top_out, hinge]
    for k in range(1, 9):
        t = k / 8
        y = hinge.y + (EAR_Y - hinge.y) * t
        x = side * (TEMPLE_X + 0.004 * math.sin(math.pi * t))
        z = hinge.z - 0.004 * t
        path.append(Vector((x, y, z)))
    path += [Vector((side * (TEMPLE_X - 0.003), EAR_Y + 0.016, hinge.z - 0.018)),
             Vector((side * (TEMPLE_X - 0.006), EAR_Y + 0.024, hinge.z - 0.040))]
    tube(path, 0.0012)
    # nose pad arm + pad
    inner_mid = lens_point(side, 0.04, 0.55)
    pad = inner_mid + Vector((-side * 0.006, 0.009, -0.004))
    tube([inner_mid, pad], 0.0007, seg=5)
    tube([pad + Vector((0, 0, 0.006)), pad - Vector((0, 0, 0.006))], 0.0022, seg=6, uv_x=(0.55, 0.6), uv_y=(0.9, 0.95))
# double bridge
for vv, dz in ((0.03, 0.0), (0.22, 0.0)):
    a, b = lens_point(1, 0.0, vv), lens_point(-1, 0.0, vv)
    mid = (a + b) / 2 + Vector((0, -0.003 if vv < 0.1 else 0.0, 0.002 if vv < 0.1 else -0.004))
    tube([b, mid, a], 0.0010)

me = bpy.data.meshes.new("SM_CHP_Sunglasses")
bm.to_mesh(me)
bm.free()
ob = bpy.data.objects.new("SM_CHP_Sunglasses", me)
bpy.context.scene.collection.objects.link(ob)
me.materials.append(bpy.data.materials.new("M_CHP_Sunglasses"))
for p in me.polygons:
    p.use_smooth = True
bpy.ops.object.select_all(action="DESELECT")
ob.select_set(True)
bpy.context.view_layer.objects.active = ob
bpy.ops.export_scene.gltf(filepath=out, use_selection=True, export_format="GLB", export_yup=True)
print("SUNGLASSES", out, "tris", sum(len(p.vertices) - 2 for p in me.polygons))
