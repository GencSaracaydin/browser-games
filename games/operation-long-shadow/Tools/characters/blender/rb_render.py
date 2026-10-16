"""Preview sheet renderer for exported character FBX files (WP05).

Run: Blender -b --factory-startup -P Tools/characters/blender/rb_render.py -- <spec.json>
Spec:
  fbx            exported character FBX
  textures       {material name: {"bc":..., "n":..., "m":..., "alpha": bool}}
  out            output PNG (contact sheet); per-view PNGs go next to it
  views          list of [name, azimuth deg (0 = front), elevation deg, ortho_scale m, target z]
  anim           optional: {"fbx": clip FBX on the same skeleton, "frames": [..]} (pose from the clip, same bone names)
  pose_fn        optional: python file defining pose(arm) applied before rendering (finger poses, retarget previews)
  hdri           optional exr (default Blender studiolight sunset.exr); sun on by default
  res            [w, h] per view (default 512x768)
"""
import bpy, sys, json, math, os, mathutils

argv = sys.argv[sys.argv.index("--") + 1:]
spec = json.load(open(argv[0]))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=spec["fbx"], automatic_bone_orientation=False, use_anim=bool(spec.get("anim_in_file")))
sc = bpy.context.scene
arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)


def make_mat(m, t):
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bs.outputs[0], out.inputs[0])
    if t.get("bc"):
        i = nt.nodes.new("ShaderNodeTexImage")
        i.image = bpy.data.images.load(t["bc"])
        nt.links.new(i.outputs[0], bs.inputs["Base Color"])
        if t.get("alpha"):
            nt.links.new(i.outputs[1], bs.inputs["Alpha"])
            try:
                m.surface_render_method = "DITHERED"
            except Exception:
                pass
    if t.get("n"):
        i = nt.nodes.new("ShaderNodeTexImage")
        i.image = bpy.data.images.load(t["n"])
        i.image.colorspace_settings.name = "Non-Color"
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(i.outputs[0], nm.inputs["Color"])
        nt.links.new(nm.outputs[0], bs.inputs["Normal"])
    if t.get("m"):
        i = nt.nodes.new("ShaderNodeTexImage")
        i.image = bpy.data.images.load(t["m"])
        i.image.colorspace_settings.name = "Non-Color"
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(i.outputs[0], sep.inputs[0])
        nt.links.new(sep.outputs[0], bs.inputs["Metallic"])
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation = "SUBTRACT"
        inv.inputs[0].default_value = 1.0
        nt.links.new(i.outputs[1], inv.inputs[1])
        nt.links.new(inv.outputs[0], bs.inputs["Roughness"])
    if t.get("color"):
        bs.inputs["Base Color"].default_value = list(t["color"]) + [1]
        bs.inputs["Roughness"].default_value = t.get("roughness", 0.6)


tex = spec.get("textures", {})
for m in bpy.data.materials:
    key = m.name.split(".")[0]
    if key in tex:
        make_mat(m, tex[key])
    elif "*" in tex:
        make_mat(m, tex["*"])

# optional pose from another clip (same bone names)
if spec.get("anim"):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=spec["anim"]["fbx"], automatic_bone_orientation=False, use_anim=True)
    src = next(o for o in bpy.data.objects if o not in before and o.type == "ARMATURE")
    for o in [o for o in bpy.data.objects if o not in before and o.type == "MESH"]:
        o.hide_render = True
    for pb in arm.pose.bones:
        if pb.name in src.pose.bones:
            c = pb.constraints.new("COPY_ROTATION")
            c.target = src
            c.subtarget = pb.name
            if pb.parent is None or pb.name.endswith("Pelvis"):
                c2 = pb.constraints.new("COPY_LOCATION")
                c2.target = src
                c2.subtarget = pb.name
    src.hide_render = True
if spec.get("pose_fn"):
    ns = {}
    exec(open(spec["pose_fn"]).read(), ns)
    ns["pose"](arm, spec)

# world + sun
w = bpy.data.worlds.new("w")
sc.world = w
w.use_nodes = True
env = w.node_tree.nodes.new("ShaderNodeTexEnvironment")
hd = spec.get("hdri") or os.path.join(os.path.dirname(bpy.app.binary_path), "..", "Resources",
                                         f"{bpy.app.version[0]}.{bpy.app.version[1]}", "datafiles", "studiolights", "world", "sunset.exr")
env.image = bpy.data.images.load(os.path.abspath(hd))
bg = w.node_tree.nodes["Background"]
bg.inputs[1].default_value = spec.get("hdri_strength", 0.6)
w.node_tree.links.new(env.outputs[0], bg.inputs[0])
if spec.get("sun", True):
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sc.collection.objects.link(sun)
    sun.data.energy = spec.get("sun_energy", 3.5)
    sun.data.angle = math.radians(1.0)
    sun.rotation_euler = (math.radians(spec.get("sun_el", 52)), 0, math.radians(spec.get("sun_az", -35)))
# ground plane catches shadows
bpy.ops.mesh.primitive_plane_add(size=6)
gp = bpy.context.active_object
gm = bpy.data.materials.new("ground")
gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.32, 0.28, 0.23, 1)
gp.data.materials.append(gm)
try:
    sc.render.engine = "BLENDER_EEVEE"
except Exception:
    sc.render.engine = "BLENDER_EEVEE_NEXT"
try:
    sc.eevee.use_shadows = True
    sc.eevee.use_raytracing = True
except Exception:
    pass
sc.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in sc.view_settings.bl_rna.properties["view_transform"].enum_items] else "Filmic"
res = spec.get("res", [512, 768])
sc.render.resolution_x, sc.render.resolution_y = res
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam
views = spec.get("views") or [["front", 0, 5, 2.0, 0.92], ["side", 90, 5, 2.0, 0.92], ["back", 180, 5, 2.0, 0.92], ["three_q", 35, 8, 2.0, 0.92]]
frames = (spec.get("anim") or {}).get("frames") or [None]
files = []
base = os.path.splitext(spec["out"])[0]
for fr in frames:
    if fr is not None:
        sc.frame_set(int(fr))
    for name, az, el, osc, tz in views:
        r = 6.0
        a, e = math.radians(az), math.radians(el)
        # character faces -Y (Blender) after import of our Unity-axis FBX
        cam.location = (r * math.sin(a) * math.cos(e), -r * math.cos(a) * math.cos(e), tz + r * math.sin(e))
        d = mathutils.Vector((0, 0, tz)) - cam.location
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        if osc > 0:
            cam.data.type = "ORTHO"
            cam.data.ortho_scale = osc
        else:
            cam.data.type = "PERSP"
            cam.data.lens = -osc
        fn = f"{base}_{name}" + (f"_f{fr}" if fr is not None else "") + ".png"
        sc.render.filepath = fn
        bpy.ops.render.render(write_still=True)
        files.append(fn)
json.dump(files, open(base + "_files.json", "w"))
print("RENDERED", len(files))
