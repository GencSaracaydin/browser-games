"""CONTENT_AUDIT review render of SM_DiveWatch from six axis directions (w_<axis>.png; the dial faces Blender -Y = Unity +Z).
  Blender -b --factory-startup -P Tools/audit/blender_watch_review.py -- <SM_DiveWatch.fbx> <texture dir> <out prefix .png>
"""
import bpy, sys, os, math
from mathutils import Vector
argv = sys.argv[sys.argv.index("--")+1:]
fbx, tex, out = argv
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx, axis_forward="-Z", axis_up="Y")
obs=[o for o in bpy.data.objects if o.type=="MESH"]
for o in obs: o.hide_render = not o.name.endswith("LOD0")
m=bpy.data.materials.new("W"); m.use_nodes=True; nt=m.node_tree; bs=nt.nodes["Principled BSDF"]
def img(s,col):
    n=nt.nodes.new("ShaderNodeTexImage"); n.image=bpy.data.images.load(os.path.join(tex,f"T_DiveWatch_{s}.png"))
    if not col: n.image.colorspace_settings.name="Non-Color"
    return n
b=img("BC",True); nt.links.new(b.outputs[0], bs.inputs["Base Color"])
mk=img("M",False); sep=nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(mk.outputs[0],sep.inputs[0]); nt.links.new(sep.outputs[0],bs.inputs["Metallic"])
inv=nt.nodes.new("ShaderNodeMath"); inv.operation="SUBTRACT"; inv.inputs[0].default_value=1; nt.links.new(mk.outputs["Alpha"],inv.inputs[1]); nt.links.new(inv.outputs[0],bs.inputs["Roughness"])
for o in obs: o.data.materials.clear(); o.data.materials.append(m)
sc=bpy.context.scene; sc.render.engine="BLENDER_EEVEE"; sc.render.resolution_x=sc.render.resolution_y=800
w=bpy.data.worlds.new("w"); sc.world=w; w.use_nodes=True; w.node_tree.nodes["Background"].inputs[0].default_value=(0.6,0.6,0.62,1); w.node_tree.nodes["Background"].inputs[1].default_value=1.0
sun=bpy.data.objects.new("s",bpy.data.lights.new("s","SUN")); sun.data.energy=3; sun.rotation_euler=(math.radians(40),0,math.radians(20)); bpy.context.collection.objects.link(sun)
cam=bpy.data.objects.new("c",bpy.data.cameras.new("c")); bpy.context.collection.objects.link(cam); sc.camera=cam
import sys as _s
views=[("px",(1,0,0)),("nx",(-1,0,0)),("py",(0,1,0)),("ny",(0,-1,0)),("pz",(0,0,1)),("nz",(0,0,-1))]
ctr=Vector((0.0,0.0,0.036))
for name,d in views:
    d=Vector(d); cam.location=ctr+d*0.16; cam.rotation_euler=(-d).to_track_quat("-Z","Y" if abs(d.z)<0.9 else "Y").to_euler(); cam.data.lens=50
    sc.render.filepath=out.replace(".png","_"+name+".png"); bpy.ops.render.render(write_still=True)
