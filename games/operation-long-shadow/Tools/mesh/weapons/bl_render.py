"""Render a review contact sheet of exported weapon FBX files with their Unity textures (Cycles, studio HDRI).

  Blender -b --factory-startup -t 3 -P Tools/mesh/weapons/bl_render.py -- render.json
render.json: {"out": "sheet.png", "items": [{"fbx": "...fbx", "objects": ["SM_..."] (optional filter),
              "tex": {"M_WPN_AK": "Textures/.../T_WPN_AK", "M_WPN_AK_Mag": ".../T_WPN_AK_Mag"}}],
              "views": ["left", "right", "top", "front34", "rear34", "fp"], "res": [960, 540], "samples": 48,
              "wire": false, "backface_check": true}
The FBX is re-imported with Blender's importer (which undoes the Unity axis conversion), so what is rendered is
exactly what ships. Materials: BC -> base colour, N -> normal map, M: R metallic, G AO (multiplied into base for the
preview only), A smoothness -> roughness = 1 - A. Backface check: faces seen from behind render magenta.
"""
import bpy, json, math, os, re, sys
from mathutils import Vector

J = json.load(open(sys.argv[sys.argv.index("--") + 1]))
ROOT = "/Users/gencsaracaydin/Documents/fun/fun-unity"
P = lambda p: p if os.path.isabs(p) else os.path.join(ROOT, p)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.device = "CPU"
sc.cycles.samples = J.get("samples", 48)
sc.cycles.use_denoising = True
sc.render.threads_mode = "FIXED"
sc.render.threads = 3
sc.render.resolution_x, sc.render.resolution_y = J.get("res", [960, 540])
sc.render.film_transparent = False
sc.view_settings.view_transform = "AgX"
sc.view_settings.look = "AgX - Medium High Contrast"
sc.view_settings.exposure = J.get("exposure", -0.4)

# world: studio HDRI for lighting and reflections, flat grey to the camera
w = bpy.data.worlds.new("W")
sc.world = w
w.use_nodes = True
nt = w.node_tree
nt.nodes.clear()
env = nt.nodes.new("ShaderNodeTexEnvironment")
hdri = J.get("hdri", "/Applications/Blender.app/Contents/Resources/5.2/datafiles/studiolights/world/studio.exr")
env.image = bpy.data.images.load(hdri)
bg = nt.nodes.new("ShaderNodeBackground")
bg.inputs["Strength"].default_value = J.get("hdri_strength", 1.0)
nt.links.new(env.outputs["Color"], bg.inputs["Color"])
grey = nt.nodes.new("ShaderNodeBackground")
grey.inputs["Color"].default_value = (0.18, 0.18, 0.19, 1)
lp = nt.nodes.new("ShaderNodeLightPath")
mix = nt.nodes.new("ShaderNodeMixShader")
nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs["Fac"])
nt.links.new(bg.outputs["Background"], mix.inputs[1])
nt.links.new(grey.outputs["Background"], mix.inputs[2])
out = nt.nodes.new("ShaderNodeOutputWorld")
nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])

# key + rim lights (sun-like soft area)
for name, loc, energy, size in (("key", (1.2, -0.8, 1.4), 120, 1.2), ("rim", (-1.0, 1.2, 0.8), 60, 1.0)):
    ld = bpy.data.lights.new(name, "AREA")
    ld.energy = energy * J.get("light_scale", 1.0)
    ld.size = size
    lo = bpy.data.objects.new(name, ld)
    sc.collection.objects.link(lo)
    lo.location = loc
    d = Vector((0, 0, 0)) - lo.location
    lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def tex_node(m, path, cs):
    n = m.node_tree.nodes.new("ShaderNodeTexImage")
    n.image = bpy.data.images.load(P(path))
    n.image.colorspace_settings.name = cs
    return n


def make_material(name, base, backface):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    t = m.node_tree
    bsdf = t.nodes["Principled BSDF"]
    if base:
        bc = tex_node(m, base + "_BC.png", "sRGB")
        mk = tex_node(m, base + "_M.png", "Non-Color")
        nm = tex_node(m, base + "_N.png", "Non-Color")
        sep = t.nodes.new("ShaderNodeSeparateColor")
        t.links.new(mk.outputs["Color"], sep.inputs["Color"])
        mul = t.nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 0.8
        t.links.new(bc.outputs["Color"], mul.inputs[6])
        t.links.new(sep.outputs["Green"], mul.inputs[7])
        t.links.new(mul.outputs[2], bsdf.inputs["Base Color"])
        t.links.new(sep.outputs["Red"], bsdf.inputs["Metallic"])
        inv = t.nodes.new("ShaderNodeMath")
        inv.operation = "SUBTRACT"
        inv.inputs[0].default_value = 1.0
        t.links.new(mk.outputs["Alpha"], inv.inputs[1])
        t.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
        nmap = t.nodes.new("ShaderNodeNormalMap")
        t.links.new(nm.outputs["Color"], nmap.inputs["Color"])
        t.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    else:
        bsdf.inputs["Base Color"].default_value = (0.6, 0.6, 0.6, 1)
        bsdf.inputs["Roughness"].default_value = 0.5
    if backface:
        geo = t.nodes.new("ShaderNodeNewGeometry")
        em = t.nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = (1, 0, 1, 1)
        mx = t.nodes.new("ShaderNodeMixShader")
        outn = t.nodes["Material Output"]
        # only faces the camera itself sees from behind (holes under backface culling); a backface seen in a
        # glossy reflection is not a defect (Unity reflects probes, never the mesh itself)
        lp = t.nodes.new("ShaderNodeLightPath")
        mm = t.nodes.new("ShaderNodeMath")
        mm.operation = "MULTIPLY"
        t.links.new(geo.outputs["Backfacing"], mm.inputs[0])
        t.links.new(lp.outputs["Is Camera Ray"], mm.inputs[1])
        t.links.new(mm.outputs[0], mx.inputs["Fac"])
        t.links.new(bsdf.outputs["BSDF"], mx.inputs[1])
        t.links.new(em.outputs["Emission"], mx.inputs[2])
        t.links.new(mx.outputs["Shader"], outn.inputs["Surface"])
    return m


objs = []
for it in J["items"]:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=P(it["fbx"]))
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type != "MESH":
            continue
        if it.get("objects") and not any(o.name.startswith(n) for n in it["objects"]):
            o.hide_render = True
            continue
        if not it.get("objects") and it.get("exclude_lods", True) and re.search(r"_LOD[1-9]", o.name):
            o.hide_render = True  # LOD1+ overlap LOD0 at the same place; render them only when asked for by name
            continue
        for i, slot in enumerate(o.material_slots):
            mname = slot.material.name.split(".")[0] if slot.material else ""
            base = it.get("tex", {}).get(mname)
            slot.material = make_material(mname + "_preview", base, J.get("backface_check", True))
        o.location += Vector(it.get("offset", (0, 0, 0)))
        objs.append(o)

bpy.context.view_layer.update()
pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
mn = Vector([min(p[i] for p in pts) for i in range(3)])
mx = Vector([max(p[i] for p in pts) for i in range(3)])
ctr = (mn + mx) / 2
size = (mx - mn).length
cam_d = bpy.data.cameras.new("cam")
cam = bpy.data.objects.new("cam", cam_d)
sc.collection.objects.link(cam)
sc.camera = cam
VIEWS = {  # direction from the centre to the camera (Blender: weapon left = +X, muzzle = -Y, up = +Z)
    "left": (1, 0, 0.0001), "right": (-1, 0, 0.0001), "top": (0.0001, 0, 1),
    "front34": (0.8, -0.9, 0.5), "rear34": (-0.8, 0.9, 0.45), "under": (0.5, 0.2, -0.8),
    "front": (0.0001, -1, 0.05), "iso": (0.9, 0.6, 0.6),
}
outdir = os.path.dirname(P(J["out"]))
os.makedirs(outdir, exist_ok=True)
frames = []
for v in J.get("views", ["left", "right", "top", "front34", "rear34", "fp"]):
    if isinstance(v, dict) and "box" in v:  # ortho close-up of a model-space Y/Z box from one side (demark boxes)
        y0, z0, y1, z1 = v["box"]
        sgn = 1 if v.get("side", "left") == "left" else -1
        cam.location = Vector((sgn * 1.0, (y0 + y1) / 2, (z0 + z1) / 2))
        d = Vector((-sgn, 0, 0.0))
        cam_d.type = "ORTHO"
        rx, ry = sc.render.resolution_x, sc.render.resolution_y
        cam_d.ortho_scale = max(y1 - y0, (z1 - z0) * rx / ry)
        v = v["name"]
    elif isinstance(v, dict):  # custom camera {"name", "loc", "target", "lens"}
        cam.location = Vector(v["loc"])
        d = Vector(v["target"]) - cam.location
        cam_d.type = "PERSP"
        cam_d.lens = v.get("lens", 50)
        v = v["name"]
    elif v == "fp":
        # rough first-person framing: eye 7 cm above and 25 cm behind the grip, 4 cm to the left, looking forward
        fp = J.get("fp_cam", {"loc": [0.06, 0.32, 0.11], "target": [-0.02, -0.5, 0.03], "lens": 26})
        cam.location = Vector(fp["loc"])
        d = Vector(fp["target"]) - cam.location
        cam_d.lens = fp["lens"]
        cam_d.type = "PERSP"
    else:
        d = Vector(VIEWS[v]).normalized()
        cam_d.type = "ORTHO" if v in ("left", "right", "top", "front", "under") else "PERSP"
        ext = mx - mn
        span = {"left": max(ext.y, ext.z * 16 / 9), "right": max(ext.y, ext.z * 16 / 9), "top": max(ext.y, ext.x * 16 / 9),
                "front": max(ext.x, ext.z * 16 / 9), "under": max(ext.y, ext.x * 16 / 9)}.get(v, size)
        cam_d.ortho_scale = span * 1.08
        cam_d.lens = 70
        dist = size * 1.9 * max(1.0, 2.1 * ext.z / size)  # upright items (grenades): fit the height in 16:9
        cam.location = ctr + d * dist
        d = -d
    cam_d.clip_start = 0.005
    cam_d.clip_end = 50
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    f = os.path.join(outdir, f"_view_{v}.png")
    sc.render.filepath = f
    bpy.ops.render.render(write_still=True)
    frames.append(f)
json.dump({"frames": frames, "out": P(J["out"])}, open(os.path.join(outdir, "_frames.json"), "w"))
print("RENDERED", frames)
