"""Preview renders of an exported FBX with its final Unity textures (Eevee, headless).

  Blender -b --factory-startup -P blender_render.py -- --fbx SM_X.fbx --bc T_X_BC.png --n T_X_N.png --m T_X_M.png
        [--e T_X_E.png] [--alpha] --out work/render [--res 640] [--human]
Writes <out>_v0.png ... (front-left 3/4, back-right 3/4, side, high 3/4, front, wireframe, close-up).
The M map is decoded the URP way: R metallic, G occlusion, A smoothness.
"""
import bpy, sys, math, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]


def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d


FBX, BC, N, M, E, OUT = arg("--fbx"), arg("--bc"), arg("--n"), arg("--m"), arg("--e"), arg("--out")
RES = int(arg("--res", 640))
ALPHA = "--alpha" in argv
HUMAN = "--human" in argv
LOD = arg("--lod")  # which object to show (default the first/LOD0)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=FBX)
objs = [o for o in sc.objects if o.type == "MESH"]
if LOD:
    show = [o for o in objs if o.name.endswith(LOD)]
else:
    show = [o for o in objs if o.name.endswith("_LOD0")] or [o for o in objs if "_LOD" not in o.name and "_COL" not in o.name]
for o in objs:
    if o not in show:
        o.hide_render = True
        o.hide_set(True)


def img(p, noncolor):
    if not p or not os.path.exists(p):
        return None
    i = bpy.data.images.load(p)
    if noncolor:
        i.colorspace_settings.name = "Non-Color"
    return i


mat = bpy.data.materials.new("preview")
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes["Principled BSDF"]
ib = img(BC, False)
if ib:
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = ib
    nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    if ALPHA:
        nt.links.new(t.outputs["Alpha"], bsdf.inputs["Alpha"])
        try:
            mat.surface_render_method = "DITHERED"
        except Exception:
            pass
in_ = img(N, True)
if in_:
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = in_
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(t.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
im = img(M, True)
if im:
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = im
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(t.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs["Red"], bsdf.inputs["Metallic"])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
    nt.links.new(t.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    if ib:  # multiply AO into base colour for the preview (URP applies it to indirect light)
        mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 0.6
        tb = [n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image == ib][0]
        nt.links.new(tb.outputs["Color"], mix.inputs["A"])
        nt.links.new(sep.outputs["Green"], mix.inputs["B"])
        nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
ie = img(E, False)
if ie:
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = ie
    nt.links.new(t.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 3.0
for o in show:
    o.data.materials.clear()
    o.data.materials.append(mat)

# bounds
pts = [o.matrix_world @ Vector(c) for o in show for c in o.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
ctr = (mn + mx) / 2
rad = (mx - mn).length / 2

# ground, light, world
bpy.ops.mesh.primitive_plane_add(size=max(rad * 12, 4), location=(ctr.x, ctr.y, mn.z - 0.001))
g = sc.objects["Plane"]
gm = bpy.data.materials.new("ground"); gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.42, 0.38, 0.33, 1)
gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
g.data.materials.append(gm)
sun = bpy.data.lights.new("sun", "SUN"); sun.energy = 2.6; sun.angle = math.radians(2)
so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(50), 0, math.radians(-35))
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes["Background"]; bg.inputs["Color"].default_value = (0.55, 0.62, 0.75, 1); bg.inputs["Strength"].default_value = 0.65

if HUMAN:  # 1.8 m scale reference
    bpy.ops.mesh.primitive_cylinder_add(radius=0.18, depth=1.8, location=(mx.x + 0.6, ctr.y, mn.z + 0.9))
    hm = bpy.data.materials.new("human"); hm.use_nodes = True
    hm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.1, 0.25, 0.6, 1)
    sc.objects["Cylinder"].data.materials.append(hm)

sc.render.engine = "BLENDER_EEVEE"
try:
    sc.eevee.taa_render_samples = 32
except Exception:
    pass
sc.render.resolution_x = RES
sc.render.resolution_y = RES
sc.view_settings.view_transform = "AgX"
try:
    sc.view_settings.look = "AgX - Punchy"
except Exception:
    pass
cam_d = bpy.data.cameras.new("cam"); cam_d.lens = 50
cam = bpy.data.objects.new("cam", cam_d); sc.collection.objects.link(cam); sc.camera = cam


def shoot(az, el, path, dist_mul=2.6, target=None, dist=None):
    t = target or ctr
    d = dist or rad * dist_mul
    a, e = math.radians(az), math.radians(el)
    cam.location = (t.x + d * math.cos(e) * math.cos(a), t.y + d * math.cos(e) * math.sin(a), t.z + d * math.sin(e))
    direction = t - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


# azimuth measured from +X CCW; front faces -Y so the front-left 3/4 camera sits at az 225 (-X,-Y)... front is -Y (az 270)
views = [(240, 22), (60, 22), (180, 10), (300, 45), (270, 8)]
for i, (az, el) in enumerate(views):
    shoot(az, el, f"{OUT}_v{i}.png")
# close-up on the front-left upper area
shoot(235, 18, f"{OUT}_v5.png", target=Vector((ctr.x, ctr.y, ctr.z + (mx.z - mn.z) * 0.15)), dist=rad * 1.2)
# wireframe
for o in show:
    o.data.materials.clear()
    cm = bpy.data.materials.new("clay"); cm.use_nodes = True
    cm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.8, 0.8, 0.8, 1)
    o.data.materials.append(cm)
    wf = o.copy(); wf.data = o.data.copy(); sc.collection.objects.link(wf)
    m = wf.modifiers.new("wf", "WIREFRAME"); m.thickness = rad * 0.0025; m.use_relative_offset = False
    wm = bpy.data.materials.new("wire"); wm.use_nodes = True
    wm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.02, 0.02, 0.02, 1)
    wf.data.materials.clear(); wf.data.materials.append(wm)
shoot(240, 22, f"{OUT}_v6.png")
print("RENDER_DONE", OUT)
