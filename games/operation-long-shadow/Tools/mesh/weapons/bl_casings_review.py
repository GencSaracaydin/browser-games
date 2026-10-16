"""Review render of SM_WPN_Casings.fbx: the four casings side by side, mouth up, plus their base (Cycles, studio HDRI).

  Blender -b --factory-startup -t 3 -P Tools/mesh/weapons/bl_casings_review.py -- <ready root> <out.png>
"""
import bpy, math, os, sys
from mathutils import Vector

READY, OUT = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.samples = 48
sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
sc.view_settings.view_transform = "AgX"
bpy.ops.import_scene.fbx(filepath=os.path.join(READY, "Assets/_Game/Art/Models/Weapons/Casings/SM_WPN_Casings.fbx"))
tdir = os.path.join(READY, "Assets/_Game/Art/Textures/Weapons/Casings")
mat = bpy.data.materials.new("rev")
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes["Principled BSDF"]
bc = nt.nodes.new("ShaderNodeTexImage")
bc.image = bpy.data.images.load(os.path.join(tdir, "T_WPN_Casings_BC.png"))
nt.links.new(bc.outputs["Color"], bsdf.inputs["Base Color"])
mk = nt.nodes.new("ShaderNodeTexImage")
mk.image = bpy.data.images.load(os.path.join(tdir, "T_WPN_Casings_M.png"))
mk.image.colorspace_settings.name = "Non-Color"
sep = nt.nodes.new("ShaderNodeSeparateColor")
nt.links.new(mk.outputs["Color"], sep.inputs["Color"])
nt.links.new(sep.outputs["Red"], bsdf.inputs["Metallic"])
inv = nt.nodes.new("ShaderNodeMath")
inv.operation = "SUBTRACT"
inv.inputs[0].default_value = 1.0
nt.links.new(mk.outputs["Alpha"], inv.inputs[1])
nt.links.new(inv.outputs["Value"], bsdf.inputs["Roughness"])
obs = sorted([o for o in sc.objects if o.type == "MESH"], key=lambda o: o.name)
bpy.ops.object.select_all(action="DESELECT")
for o in obs:
    o.select_set(True)
bpy.context.view_layer.objects.active = obs[0]
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)  # case axis now along Blender -Y
row = []
for i, o in enumerate(obs):
    o.data.materials.clear()
    o.data.materials.append(mat)
    for k, (rot, dz) in enumerate((((math.radians(-90), 0, 0), 0.0), ((math.radians(90), 0, 0), -0.07))):
        d = o if k == 0 else o.copy()
        if k:
            d.data = o.data
            sc.collection.objects.link(d)
        d.rotation_euler = rot  # Unity +Z (Blender -Y after import) -> up
        d.location = Vector((i * 0.03 - 0.045, 0, dz))
        print("casing", o.name, "tris", sum(len(p.vertices) - 2 for p in o.data.polygons),
              "dims mm", [round(v * 1000, 1) for v in o.dimensions])
w = bpy.data.worlds.new("W")
sc.world = w
w.use_nodes = True
env = w.node_tree.nodes.new("ShaderNodeTexEnvironment")
env.image = bpy.data.images.load("/Applications/Blender.app/Contents/Resources/5.2/datafiles/studiolights/world/forest.exr")
w.node_tree.links.new(env.outputs["Color"], w.node_tree.nodes["Background"].inputs["Color"])
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 85
cam.location = Vector((0.0, -0.42, 0.05))
cam.rotation_euler = (Vector((0, 0, -0.02)) - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("RENDERED", OUT)
