"""Review renders of a shipped WP09 FBX with its shipped textures (Blender headless, Eevee).

  Blender -b --factory-startup -t 2 -P Tools/images/bl_render_shipped.py -- --fbx SM_X.fbx --mats materials.json
        --out /tmp/x [--res 640] [--screen test.png] [--views a,b,c,d,e] [--show LOD0]
materials.json: list of specs {material, textures{_BaseMap,_BumpMap,_MetallicGlossMap}, colors{_BaseColor}, surface,
emission} with paths relative to RawAssets/wp09/ready (or absolute). Slot names in the FBX are matched to 'material'.
M maps are decoded the URP way (R metal, G AO, A smoothness). *_Screen slots get --screen as an emissive image so the
CRT UV orientation can be checked. Writes <out>_<view>.png.
Views: a front-left 3/4, b back-right 3/4, c front, d high 3/4, e close-up front-left, w wireframe-ish (flat grey).
"""
import bpy, json, math, os, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
A = {argv[i]: argv[i + 1] for i in range(0, len(argv) - 1, 2)}
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
READY = os.path.join(ROOT, "RawAssets", "wp09", "ready")
RES = int(A.get("--res", 640))
views = A.get("--views", "a,b,c,d,e").split(",")
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=A["--fbx"])
objs = [o for o in sc.objects if o.type == "MESH"]
show = A.get("--show")
for o in objs:
    if "_LOD" in o.name and not o.name.endswith(show or "_LOD0"):
        o.hide_render = True
specs = json.load(open(A["--mats"])) if A.get("--mats") else []
specs = {s["material"]: s for s in specs}


def img(p, noncolor=False):
    if not p:
        return None
    p = p if os.path.isabs(p) else os.path.join(READY, p)
    if not os.path.exists(p):
        return None
    im = bpy.data.images.load(p, check_existing=True)
    if noncolor:
        im.colorspace_settings.name = "Non-Color"
    return im


def build(mname):
    s = specs.get(mname) or next((v for k, v in specs.items() if mname.startswith(k)), None)
    m = bpy.data.materials.new(mname + "_prev")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    if mname.endswith("_Screen") and A.get("--screen"):
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = img(A["--screen"])
        nt.links.new(t.outputs["Color"], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 1.2
        b.inputs["Base Color"].default_value = (0.02, 0.02, 0.02, 1); b.inputs["Roughness"].default_value = 0.1
        return m
    if not s:
        b.inputs["Base Color"].default_value = (0.5, 0.5, 0.5, 1)
        return m
    tx = s.get("textures", {})
    col = s.get("colors", {}).get("_BaseColor", [1, 1, 1, 1])
    bc = img(tx.get("_BaseMap"))
    if bc:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = bc
        if col[:3] != [1, 1, 1] and col[:3] != [1.0, 1.0, 1.0]:
            mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1.0
            nt.links.new(t.outputs["Color"], mix.inputs[6]); mix.inputs[7].default_value = (*col[:3], 1)
            nt.links.new(mix.outputs[2], b.inputs["Base Color"])
        else:
            nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
        if s.get("alpha_clip") or s.get("surface") == "Transparent":
            nt.links.new(t.outputs["Alpha"], b.inputs["Alpha"])
    else:
        b.inputs["Base Color"].default_value = (*col[:3], 1)
    n = img(tx.get("_BumpMap"), True)
    if n:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = n
        nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(t.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    mm = img(tx.get("_MetallicGlossMap"), True)
    if mm:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = mm
        sp = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(t.outputs["Color"], sp.inputs["Color"])
        nt.links.new(sp.outputs["Red"], b.inputs["Metallic"])
        inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
        nt.links.new(t.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs["Roughness"])
        # AO: multiply base colour by G (approximation of URP occlusion on indirect light)
        if bc:
            mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"; mul.inputs[0].default_value = 0.6
            src = b.inputs["Base Color"].links[0].from_socket
            nt.links.new(src, mul.inputs[6]); nt.links.new(sp.outputs["Green"], mul.inputs[7])
            nt.links.new(mul.outputs[2], b.inputs["Base Color"])
    else:
        fl = s.get("floats", {})
        b.inputs["Metallic"].default_value = fl.get("_Metallic", 0.0)
        b.inputs["Roughness"].default_value = 1 - fl.get("_Smoothness", 0.5)
    if s.get("surface") == "Transparent":
        b.inputs["Alpha"].default_value = col[3] if len(col) > 3 else 0.3
        try:
            m.surface_render_method = "BLENDED"
        except Exception:
            pass
    em = s.get("emission")
    if em and em.get("_EmissionMap"):
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = img(em["_EmissionMap"])
        nt.links.new(t.outputs["Color"], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = float(A.get("--emit", 0.0))
    return m


cache = {}
for o in objs:
    for sl in o.material_slots:
        if sl.material:
            nm = sl.material.name.split(".")[0]
            if nm not in cache:
                cache[nm] = build(nm)
            sl.material = cache[nm]
vis = [o for o in objs if not o.hide_render]
mn = Vector((1e9,) * 3); mx = -mn
for o in vis:
    for v in o.data.vertices:
        w = o.matrix_world @ v.co
        mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
c = (mn + mx) / 2
r = max((mx - mn).length / 2, 0.02)
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x = sc.render.resolution_y = RES
sc.view_settings.view_transform = "AgX"
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.42, 0.43, 0.45, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = 0.7
sun = bpy.data.objects.new("key", bpy.data.lights.new("key", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 3.2; sun.rotation_euler = (math.radians(50), 0, math.radians(-35)); sun.data.angle = math.radians(8)
fill = bpy.data.objects.new("rim", bpy.data.lights.new("rim", "SUN")); sc.collection.objects.link(fill)
fill.data.energy = 1.2; fill.rotation_euler = (math.radians(60), 0, math.radians(150))
# ground
bpy.ops.mesh.primitive_plane_add(size=r * 12, location=(c.x, c.y, mn.z - 0.0005))
g = bpy.context.active_object; gm = bpy.data.materials.new("ground"); gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.25, 0.25, 0.25, 1); g.data.materials.append(gm)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 50
cam.data.clip_start = r * 0.02
V = {"a": (-35, 22, 1.0), "b": (145, 25, 1.0), "c": (0, 4, 1.0), "d": (-30, 55, 1.0), "e": (-25, 15, 0.55), "w": (-35, 22, 1.0)}
for k in views:
    az, el, zoom = V[k]
    d = r / math.tan(math.radians(17)) * zoom * 1.05
    az_r, el_r = math.radians(az), math.radians(el)
    cam.location = c + Vector((math.sin(az_r) * math.cos(el_r), -math.cos(az_r) * math.cos(el_r), math.sin(el_r))) * d
    tgt = c if k != "e" else c + Vector((0, 0, (mx.z - mn.z) * 0.1))
    cam.rotation_euler = (tgt - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = d * 10
    sc.render.filepath = f"{A['--out']}_{k}.png"
    bpy.ops.render.render(write_still=True)
