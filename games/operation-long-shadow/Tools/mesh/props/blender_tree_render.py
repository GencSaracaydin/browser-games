"""Review renders (and the far impostor) of a procedural tree FBX with its Unity textures (Eevee, headless). WP10_FAL.

  Blender -b --factory-startup -P blender_tree_render.py -- --fbx SM_OliveTree_A.fbx --tex <tex dir> --name OliveTree
        --out work/render [--res 640] [--impostor T_OliveTree_A_Impostor_BC.png]
Views: v0 front-left 3/4 elevated (like the reference), v1 player eye height (1.7 m) at ~3 tree heights, v2 back-right,
v3 top-down, v4 close-up of the canopy edge, v5 LOD1, v6 LOD2, v7 wireframe LOD0.
--impostor renders the LOD0 orthographically from the front (-Y) on a transparent film, 512 x 512, for a 2-plane
cross impostor (S9_TownFar / grove far LOD).
"""
import bpy, sys, math, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]


def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d


FBX, TEX, NAME, OUT = arg("--fbx"), arg("--tex"), arg("--name"), arg("--out")
RES = int(arg("--res", 640))
IMP = arg("--impostor")

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=FBX)
objs = {o.name: o for o in sc.objects if o.type == "MESH"}


def img(p, noncolor):
    i = bpy.data.images.load(p)
    if noncolor:
        i.colorspace_settings.name = "Non-Color"
    return i


def make_mat(kind):
    m = bpy.data.materials.new(f"{kind}_prev"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    base = os.path.join(TEX, f"T_{NAME}_{kind}_")
    tb = nt.nodes.new("ShaderNodeTexImage"); tb.image = img(base + "BC.png", False)
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img(base + "N.png", True)
    tm = nt.nodes.new("ShaderNodeTexImage"); tm.image = img(base + "M.png", True)
    nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    sep = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(tm.outputs["Color"], sep.inputs["Color"])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
    nt.links.new(tm.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs["Roughness"])
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"; mix.inputs["Factor"].default_value = 0.7
    nt.links.new(tb.outputs["Color"], mix.inputs["A"]); nt.links.new(sep.outputs["Green"], mix.inputs["B"])
    nt.links.new(mix.outputs["Result"], b.inputs["Base Color"])
    if kind == "Leaves":
        # alpha clip 0.5 like the Unity material + some translucency for back-lit leaves
        gt = nt.nodes.new("ShaderNodeMath"); gt.operation = "GREATER_THAN"; gt.inputs[1].default_value = 0.5
        nt.links.new(tb.outputs["Alpha"], gt.inputs[0]); nt.links.new(gt.outputs[0], b.inputs["Alpha"])
        try:
            m.surface_render_method = "DITHERED"
        except Exception:
            pass
        try:
            b.inputs["Transmission Weight"].default_value = 0.0
            b.inputs["Subsurface Weight"].default_value = 0.15
        except Exception:
            pass
        m.use_backface_culling = False
    return m


MB, ML = make_mat("Bark"), make_mat("Leaves")
for o in objs.values():
    for i, s in enumerate(o.material_slots):
        nm = s.material.name if s.material else ""
        s.material = ML if "Leaves" in nm else MB


def only(suffix):
    sel = []
    for n, o in objs.items():
        vis = n.endswith(suffix)
        o.hide_render = not vis; o.hide_set(not vis)
        if vis:
            sel.append(o)
    return sel


lod0 = only("_LOD0") or list(objs.values())
pts = [o.matrix_world @ Vector(c) for o in lod0 for c in o.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
ctr = (mn + mx) / 2
rad = (mx - mn).length / 2
H = mx.z

sc.render.engine = "BLENDER_EEVEE"
try:
    sc.eevee.taa_render_samples = 48
except Exception:
    pass
sc.view_settings.view_transform = "AgX"
cam_d = bpy.data.cameras.new("cam"); cam_d.lens = 50; cam_d.clip_end = 500
cam = bpy.data.objects.new("cam", cam_d); sc.collection.objects.link(cam); sc.camera = cam

if IMP:
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = 512
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.75, 0.78, 0.85, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    sun = bpy.data.lights.new("sun", "SUN"); sun.energy = 2.0
    so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so); so.rotation_euler = (math.radians(35), 0, math.radians(-20))
    cam_d.type = "ORTHO"
    half = max(mx.x - mn.x, mx.y - mn.y, mx.z - mn.z) / 2 * 1.02
    cam_d.ortho_scale = half * 2
    cam.location = (0, -60, H / 2); cam.rotation_euler = (math.radians(90), 0, 0)
    # square frame centred on the tree: ortho box from z = H/2 - half .. H/2 + half
    sc.render.filepath = IMP
    bpy.ops.render.render(write_still=True)
    print("IMPOSTOR", IMP, "half", half, "H", H)
    sys.exit(0)

sc.render.resolution_x = RES; sc.render.resolution_y = RES
bpy.ops.mesh.primitive_plane_add(size=max(rad * 30, 40), location=(0, 0, 0))
g = sc.objects["Plane"]; gm = bpy.data.materials.new("ground"); gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.45, 0.38, 0.30, 1)
gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.95
g.data.materials.append(gm)
sun = bpy.data.lights.new("sun", "SUN"); sun.energy = 3.2; sun.angle = math.radians(1.5)
so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(62), 0, math.radians(-40))   # low golden-hour-ish sun
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.65, 0.82, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7
bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.8, location=(mx.x + 0.8, ctr.y, 0.9))
hm = bpy.data.materials.new("human"); hm.use_nodes = True
hm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.1, 0.25, 0.6, 1)
sc.objects["Cylinder"].data.materials.append(hm)


def shoot(path, loc=None, az=None, el=None, dist=None, target=None, lens=50):
    cam_d.lens = lens
    t = target or ctr
    if loc is None:
        a, e = math.radians(az), math.radians(el)
        loc = Vector((t.x + dist * math.cos(e) * math.cos(a), t.y + dist * math.cos(e) * math.sin(a), t.z + dist * math.sin(e)))
    cam.location = loc
    cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


shoot(f"{OUT}_v0.png", az=240, el=22, dist=rad * 2.7)
shoot(f"{OUT}_v1.png", loc=Vector((-H * 1.2, -H * 2.6, 1.7)), target=Vector((0, 0, H * 0.45)), lens=35)
shoot(f"{OUT}_v2.png", az=60, el=15, dist=rad * 2.7)
shoot(f"{OUT}_v3.png", az=270, el=89, dist=rad * 2.6)
shoot(f"{OUT}_v4.png", az=230, el=10, dist=rad * 0.9, target=Vector((mn.x * 0.6, ctr.y, H * 0.72)))
for i, suf in ((5, "_LOD1"), (6, "_LOD2")):
    if only(suf):
        shoot(f"{OUT}_v{i}.png", az=240, el=22, dist=rad * 2.7)
lod0 = only("_LOD0") or list(objs.values())
for o in lod0:
    wf = o.copy(); wf.data = o.data.copy(); sc.collection.objects.link(wf)
    m = wf.modifiers.new("wf", "WIREFRAME"); m.thickness = rad * 0.0015; m.use_relative_offset = False
    wm = bpy.data.materials.new("wire"); wm.use_nodes = True
    wm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.02, 0.02, 0.02, 1)
    wf.data.materials.clear(); wf.data.materials.append(wm)
shoot(f"{OUT}_v7.png", az=240, el=22, dist=rad * 2.7)
print("RENDER_DONE", OUT)
