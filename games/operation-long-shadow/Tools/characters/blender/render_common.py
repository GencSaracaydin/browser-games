# Shared preview-render helpers (exec'd by retarget.py / pose renders). Blender python.
import bpy, math, os, mathutils


def _make_mat(m, t):
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bs.outputs[0], out.inputs[0])
    if t.get("bc"):
        i = nt.nodes.new("ShaderNodeTexImage")
        i.image = bpy.data.images.load(t["bc"], check_existing=True)
        nt.links.new(i.outputs[0], bs.inputs["Base Color"])
        if t.get("alpha"):
            nt.links.new(i.outputs[1], bs.inputs["Alpha"])
            try:
                m.surface_render_method = "DITHERED"
            except Exception:
                pass
    if t.get("n"):
        i = nt.nodes.new("ShaderNodeTexImage")
        i.image = bpy.data.images.load(t["n"], check_existing=True)
        i.image.colorspace_settings.name = "Non-Color"
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(i.outputs[0], nm.inputs["Color"])
        nt.links.new(nm.outputs[0], bs.inputs["Normal"])
    if t.get("m"):
        i = nt.nodes.new("ShaderNodeTexImage")
        i.image = bpy.data.images.load(t["m"], check_existing=True)
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


def apply_textures(obj, tex):
    for slot in obj.material_slots:
        m = slot.material
        if not m:
            continue
        key = m.name.split(".")[0]
        if key in tex:
            _make_mat(m, tex[key])
        elif "*" in tex:
            _make_mat(m, tex["*"])


def setup_scene(spec):
    sc = bpy.context.scene
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
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sc.collection.objects.link(sun)
    sun.data.energy = spec.get("sun_energy", 3.5)
    sun.rotation_euler = (math.radians(spec.get("sun_el", 52)), 0, math.radians(spec.get("sun_az", -35)))
    bpy.ops.mesh.primitive_plane_add(size=8)
    gp = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.32, 0.28, 0.23, 1)
    gp.data.materials.append(gm)
    try:
        sc.render.engine = "BLENDER_EEVEE"
    except Exception:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.view_settings.view_transform = "AgX"
    sc.render.resolution_x, sc.render.resolution_y = spec.get("res", [512, 768])
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam


def render_views(spec, suffix=""):
    sc = bpy.context.scene
    cam = sc.camera
    files = []
    views = spec.get("views") or [["front", 0, 5, 2.2, 0.92], ["side", 90, 5, 2.2, 0.92], ["three_q", 35, 8, 2.2, 0.92]]
    for name, az, el, osc, tz in views:
        r = 6.0
        a, e = math.radians(az), math.radians(el)
        tx, ty = spec.get("target_xy", [0, 0])
        cam.location = (tx + r * math.sin(a) * math.cos(e), ty - r * math.cos(a) * math.cos(e), tz + r * math.sin(e))
        d = mathutils.Vector((tx, ty, tz)) - cam.location
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        if osc > 0:
            cam.data.type = "ORTHO"
            cam.data.ortho_scale = osc
        else:
            cam.data.type = "PERSP"
            cam.data.lens = -osc
        fn = f"{spec['out']}_{name}{suffix}.png"
        sc.render.filepath = fn
        bpy.ops.render.render(write_still=True)
        files.append(fn)
    return files
