"""Vehicle extras for WP10_FAL (Blender, headless):
  * helicopter: procedural Bell-212-style 2-blade main rotor (14.7 m) and tail rotor (2.6 m) as separate meshes with the
    pivot at the hub, plus motion-blur discs; hub positions measured on SM_Helicopter.
  * technical: DShK pedestal top (mount point for WPN_DSHK) measured on SM_Technical.
  * check renders of both.

  Blender -b --factory-startup -P blender_vehicle_extras.py -- --ready <ready root> --out <work dir>
Writes Models/Vehicles/helicopter/SM_HeliMainRotor.fbx, SM_HeliTailRotor.fbx and <out>/vehicle_points.json
(Blender and Unity coordinates; Unity = (-x, z, -y) for this FBX export).
"""
import bpy, bmesh, json, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
READY = argv[argv.index("--ready") + 1]
OUT = argv[argv.index("--out") + 1]
os.makedirs(OUT, exist_ok=True)
MOD = os.path.join(READY, "Assets/_Game/Art/Models/Vehicles")
TEX = os.path.join(READY, "Assets/_Game/Art/Textures/Vehicles")


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def imp(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, axis_forward="-Z", axis_up="Y")
    return [o for o in bpy.data.objects if o not in before and o.type == "MESH"]


def verts(ob):
    n = len(ob.data.vertices)
    co = np.empty(n * 3); ob.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    M = np.array(ob.matrix_world)
    return co @ M[:3, :3].T + M[:3, 3]


def unity(p):
    return [round(-p[0], 4), round(p[2], 4), round(-p[1], 4)]


def blade_mesh(name, radius, root, chord_root, chord_tip, thick, twist_deg, n_span=7, droop=0.0):
    """Two opposite blades along +-X, airfoil sections (8 pts), linear taper and twist, tip cap. UV: u along span, v chord."""
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    sec = [(1.0, 0.0), (0.75, 0.045), (0.45, 0.06), (0.15, 0.05), (0.0, 0.0), (0.15, -0.03), (0.45, -0.035), (0.75, -0.02)]
    for side in (1, -1):
        rings = []
        for i in range(n_span + 1):
            t = i / n_span
            r = root + (radius - root) * t
            c = chord_root + (chord_tip - chord_root) * t
            tw = math.radians(twist_deg * (1 - t))
            ring = []
            for (sx, sz) in sec:
                cx = (sx - 0.3) * c            # quarter-chord-ish on the feathering axis
                cz = sz * c * (thick / 0.06)
                y = cx * math.cos(tw) - cz * math.sin(tw)
                z = cx * math.sin(tw) + cz * math.cos(tw) - droop * t * t
                ring.append(bm.verts.new((side * r, side * y, z)))
            rings.append(ring)
        ns = len(sec)
        for i in range(n_span):
            for k in range(ns):
                a, b = rings[i][k], rings[i][(k + 1) % ns]
                c2, d = rings[i + 1][(k + 1) % ns], rings[i + 1][k]
                f = bm.faces.new((a, b, c2, d))      # side -1 is a 180 deg rotation of side +1: same winding
                for l, (u, v) in zip(f.loops, ((i / n_span, k / ns), (i / n_span, (k + 1) / ns),
                                              ((i + 1) / n_span, (k + 1) / ns), ((i + 1) / n_span, k / ns))):
                    l[uv_layer].uv = (u * 0.98 + 0.01, v * 0.98 + 0.01) if side > 0 else (u * 0.98 + 0.01, v * 0.98 + 0.01)
        for ring, rev in ((rings[0], True), (rings[-1], False)):
            f = bm.faces.new(ring[::-1] if rev else ring)
            for l in f.loops:
                l[uv_layer].uv = (0.995 if not rev else 0.005, 0.5)
    # hub: short cylinder + grip
    bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=root * 0.55, radius2=root * 0.55, depth=root * 0.6)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    return ob


def disc(name, radius, z=0.0, axis="Z"):
    bpy.ops.mesh.primitive_circle_add(vertices=32, radius=radius, fill_type="TRIFAN", location=(0, 0, z))
    ob = bpy.context.active_object; ob.name = name; ob.data.name = name
    if axis == "X":
        ob.data.transform(Matrix.Rotation(math.radians(90), 4, "Y"))
    me = ob.data
    uvl = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        for l in p.loop_indices:
            co = me.vertices[me.loops[l].vertex_index].co
            if axis == "X":
                uvl.data[l].uv = (0.5 + co.y / (2 * radius), 0.5 + co.z / (2 * radius))
            else:
                uvl.data[l].uv = (0.5 + co.x / (2 * radius), 0.5 + co.y / (2 * radius))
    return ob


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="OFF", use_tspace=False, path_mode="STRIP", embed_textures=False, add_leaf_bones=False,
                             bake_anim=False)


def tri(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


points = {}
# ---------------- helicopter ----------------
reset()
heli = imp(os.path.join(MOD, "helicopter/SM_Helicopter.fbx"))
h0 = [o for o in heli if o.name.endswith("_LOD0")][0]
V = verts(h0)
ymin, ymax = V[:, 1].min(), V[:, 1].max()
# mast: highest vertices within |x| < 0.5 and the middle 40 % of the length
mid = V[(np.abs(V[:, 0]) < 0.5) & (V[:, 1] > ymin + 0.25 * (ymax - ymin)) & (V[:, 1] < ymin + 0.6 * (ymax - ymin))]
top = mid[mid[:, 2] > mid[:, 2].max() - 0.12]
hub = Vector((float(top[:, 0].mean()), float(top[:, 1].mean()), float(mid[:, 2].max())))
# tail rotor: rear 8 % of the length, upper part; hub at the outermost lateral point of that cluster
rear = V[V[:, 1] > ymax - 0.08 * (ymax - ymin)]
rear = rear[rear[:, 2] > rear[:, 2].max() - 1.4]
side_i = int(np.argmax(np.abs(rear[:, 0])))
sx = float(np.sign(rear[side_i, 0]))
cl = rear[np.abs(rear[:, 0]) > np.abs(rear[side_i, 0]) - 0.15]
thub = Vector((float(cl[:, 0].mean()), float(cl[:, 1].mean()), float(cl[:, 2].mean())))
points["helicopter"] = {"main_rotor_hub_blender": [round(x, 4) for x in hub], "main_rotor_hub_unity": unity(hub),
                        "tail_rotor_hub_blender": [round(x, 4) for x in thub], "tail_rotor_hub_unity": unity(thub),
                        "tail_rotor_side": "left (Unity -X... see sign)" if sx > 0 else "right",
                        "bbox_blender": [[round(float(x), 3) for x in V.min(0)], [round(float(x), 3) for x in V.max(0)]]}
# rotors (built at origin = hub)
mr = blade_mesh("SM_HeliMainRotor_Blades", 7.35, 0.45, 0.59, 0.52, 0.06, 8, n_span=8, droop=0.18)
mb = disc("SM_HeliMainRotor_Blur", 7.35, z=0.0)
tr = blade_mesh("SM_HeliTailRotor_Blades", 1.3, 0.12, 0.29, 0.26, 0.06, 6, n_span=4)
# tail rotor spins in the vertical plane (axis lateral X): blades along Z after rotating about Y
tr.data.transform(Matrix.Rotation(math.radians(90), 4, "Y"))
tr.data.transform(Matrix.Rotation(math.radians(90), 4, "X"))
tr.data.transform(Matrix.Rotation(math.radians(90), 4, "Y"))
tb = disc("SM_HeliTailRotor_Blur", 1.3, axis="X")
os.makedirs(os.path.join(MOD, "helicopter"), exist_ok=True)
export([mr, mb], os.path.join(MOD, "helicopter/SM_HeliMainRotor.fbx"))
export([tr, tb], os.path.join(MOD, "helicopter/SM_HeliTailRotor.fbx"))
points["helicopter"]["tris"] = {o.name: tri(o) for o in (mr, mb, tr, tb)}


def mat_tex(name, bc, n=None, m=None, alpha=False):
    mt = bpy.data.materials.new(name); mt.use_nodes = True
    nt = mt.node_tree; b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(bc)
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    if alpha:
        nt.links.new(t.outputs["Alpha"], b.inputs["Alpha"])
        try:
            mt.surface_render_method = "BLENDED"
        except Exception:
            pass
    if n:
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images.load(n); tn.image.colorspace_settings.name = "Non-Color"
        nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    if m:
        tm = nt.nodes.new("ShaderNodeTexImage"); tm.image = bpy.data.images.load(m); tm.image.colorspace_settings.name = "Non-Color"
        sp = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(tm.outputs["Color"], sp.inputs["Color"])
        nt.links.new(sp.outputs["Red"], b.inputs["Metallic"])
        inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1
        nt.links.new(tm.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs["Roughness"])
    return mt


def setup_render(res=900):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"; sc.render.resolution_x = res; sc.render.resolution_y = int(res * 0.62)
    sc.view_settings.view_transform = "AgX"
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.62, 0.75, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7
    sun = bpy.data.lights.new("sun", "SUN"); sun.energy = 3.0
    so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so); so.rotation_euler = (math.radians(55), 0, math.radians(-35))
    bpy.ops.mesh.primitive_plane_add(size=80)
    g = bpy.context.active_object; gm = bpy.data.materials.new("g"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.42, 0.37, 0.31, 1); g.data.materials.append(gm)
    cd = bpy.data.cameras.new("c"); cd.lens = 40; cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
    return sc, cam


def shoot(sc, cam, loc, tgt, path):
    cam.location = loc; cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)


# check render: helicopter + blades at the measured hubs
hm = mat_tex("heli", os.path.join(TEX, "helicopter/T_Helicopter_BC.png"), os.path.join(TEX, "helicopter/T_Helicopter_N.png"),
             os.path.join(TEX, "helicopter/T_Helicopter_M.png"))
for o in heli:
    o.hide_render = o is not h0
    o.data.materials.clear(); o.data.materials.append(hm)
rm = mat_tex("rotor", os.path.join(TEX, "helicopter/T_HeliRotor_BC.png"), m=os.path.join(TEX, "helicopter/T_HeliRotor_M.png"))
for o, p in ((mr, hub), (tr, thub)):
    o.location = p; o.data.materials.clear(); o.data.materials.append(rm)
for o in (mb, tb):
    o.hide_render = True
mr.rotation_euler = (0, 0, math.radians(65))
sc, cam = setup_render()
shoot(sc, cam, (-14, -13, 7), (0, 0, 2.0), os.path.join(OUT, "heli_rotors_a.png"))
shoot(sc, cam, (12, 6, 3.5), (0, 3, 2.5), os.path.join(OUT, "heli_rotors_b.png"))
mb.hide_render = False; mb.location = hub; tb.hide_render = False; tb.location = thub; mr.hide_render = True; tr.hide_render = True
blm = mat_tex("blur", os.path.join(TEX, "helicopter/T_HeliRotorBlur_BC.png"), alpha=True)
mb.data.materials.append(blm); tb.data.materials.append(blm)
shoot(sc, cam, (-14, -13, 7), (0, 0, 2.0), os.path.join(OUT, "heli_rotors_blur.png"))

# ---------------- technical ----------------
reset()
tech = imp(os.path.join(MOD, "technical/SM_Technical.fbx"))
t0 = [o for o in tech if o.name.endswith("_LOD0")][0]
V = verts(t0)
ymin, ymax = V[:, 1].min(), V[:, 1].max()
bed = V[(np.abs(V[:, 0]) < 0.3) & (V[:, 1] > ymin + 0.5 * (ymax - ymin))]
ztop = bed[:, 2].max()
tp = bed[bed[:, 2] > ztop - 0.04]
mount = Vector((float(tp[:, 0].mean()), float(tp[:, 1].mean()), float(ztop)))
points["technical"] = {"dshk_mount_top_blender": [round(x, 4) for x in mount], "dshk_mount_top_unity": unity(mount),
                       "bbox_blender": [[round(float(x), 3) for x in V.min(0)], [round(float(x), 3) for x in V.max(0)]],
                       "note": "top centre of the empty welded pedestal post in the bed; WPN_DSHK cradle pivot goes here (gun "
                               "facing +Z by default, traverse about Unity Y)."}
tm = mat_tex("tech", os.path.join(TEX, "technical/T_Technical_BC.png"), os.path.join(TEX, "technical/T_Technical_N.png"),
             os.path.join(TEX, "technical/T_Technical_M.png"))
tbm = mat_tex("techb", os.path.join(TEX, "technical/T_TechnicalBurnt_BC.png"), os.path.join(TEX, "technical/T_Technical_N.png"),
              os.path.join(TEX, "technical/T_TechnicalBurnt_M.png"))
for o in tech:
    o.hide_render = o is not t0
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.06, location=mount)
mk = bpy.context.active_object; mm = bpy.data.materials.new("mk"); mm.use_nodes = True
mm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (1, 0.05, 0.05, 1); mk.data.materials.append(mm)
sc, cam = setup_render()
t0.data.materials.clear(); t0.data.materials.append(tm)
shoot(sc, cam, (-5.5, -5.0, 3.2), (0, 0.3, 0.9), os.path.join(OUT, "tech_mount.png"))
mk.hide_render = True
t0.data.materials.clear(); t0.data.materials.append(tbm)
shoot(sc, cam, (-5.5, -5.0, 3.2), (0, 0.3, 0.9), os.path.join(OUT, "tech_burnt.png"))
truck = imp(os.path.join(MOD, "cargo_truck/SM_CargoTruck.fbx"))
for o in truck:
    o.hide_render = not o.name.endswith("_LOD0")
    o.location.x += 4.5
    cbm = mat_tex("truckb", os.path.join(TEX, "cargo_truck/T_CargoTruckBurnt_BC.png"), os.path.join(TEX, "cargo_truck/T_CargoTruck_N.png"),
                  os.path.join(TEX, "cargo_truck/T_CargoTruckBurnt_M.png"))
    o.data.materials.clear(); o.data.materials.append(cbm)
shoot(sc, cam, (-6, -11, 4.5), (2.5, 0.5, 1.2), os.path.join(OUT, "burnt_pair.png"))
json.dump(points, open(os.path.join(OUT, "vehicle_points.json"), "w"), indent=1)
print("VEHICLE_POINTS", json.dumps(points))
