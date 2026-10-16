"""Preview a baked WP05 clip (Rocketbox skeleton) on a textured body, optionally holding the WP03 AK (WP05).

Run: Blender -b --factory-startup -P Tools/characters/blender/clip_preview.py -- <spec.json>
spec: body_fbx, textures {material: {bc,n,m,alpha}}, clip_fbx, frames [..] or frames_frac [..], views
      [[name, az, el, ortho_scale|-lens, target_z]], out (prefix), res [w,h], rifle (true/false), follow (true: camera
      target follows the pelvis XY), fp_views [[name, pitch deg, yaw deg, lens mm]] (first-person check: camera at the
      eyes, looking along the rifle / hands direction, then pitched and yawed; used for SK_Calder_FPArms)
The rifle (RawAssets/wp03/ready/.../SM_WPN_AK_W.fbx, pivot = pistol grip, Unity +Z = muzzle) is placed with its grip at
the right palm and its LeftHand socket aimed at the left palm, i.e. what runtime two-hand IK would converge to.
Writes <out>_<view>_f<frame>.png and <out>_files.json.
"""
import bpy, sys, json, math, os, mathutils
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
spec = json.load(open(argv[0]))
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
exec(open(os.path.join(HERE, "render_common.py")).read())

before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=spec["body_fbx"], automatic_bone_orientation=False, use_anim=False)
body = [o for o in bpy.data.objects if o not in before]
arm = next(o for o in body if o.type == "ARMATURE")
for o in body:
    if o.type == "MESH":
        apply_textures(o, spec.get("textures", {}))  # noqa: F821
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=spec["clip_fbx"], automatic_bone_orientation=False, use_anim=True)
clip = [o for o in bpy.data.objects if o not in before]
carm = next(o for o in clip if o.type == "ARMATURE")
act = carm.animation_data.action
for o in clip:
    o.hide_render = o.hide_viewport = True
# Drive the body by world-space copies of the clip skeleton (same bone names). Assigning the action directly would be
# wrong: Blender stores FBX curves relative to each file's own rest pose, and an armature-only clip's rest differs.
for pb in arm.pose.bones:
    pb.matrix_basis = Matrix.Identity(4)
    if pb.name in carm.pose.bones:
        c = pb.constraints.new("COPY_TRANSFORMS")
        c.target = carm
        c.subtarget = pb.name
f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])

rifle = None
if spec.get("rifle"):
    rb = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, "RawAssets/wp03/ready/Assets/_Game/Art/Models/Weapons/AK/SM_WPN_AK_W.fbx"))
    new = [o for o in bpy.data.objects if o not in rb]
    for o in new:
        if o.type == "MESH" and "LOD0" not in o.name:
            bpy.data.objects.remove(o, do_unlink=True)
    rifle = next(o for o in bpy.data.objects if o not in rb and o.type == "MESH")
    t = os.path.join(ROOT, "RawAssets/wp03/ready/Assets/_Game/Art/Textures/Weapons/AK/")
    apply_textures(rifle, {"M_WPN_AK": {"bc": t + "T_WPN_AK_BC.png", "n": t + "T_WPN_AK_N.png", "m": t + "T_WPN_AK_M.png"},  # noqa: F821
                           "M_WPN_AK_Mag": {"bc": t + "T_WPN_AK_Mag_BC.png", "n": t + "T_WPN_AK_Mag_N.png", "m": t + "T_WPN_AK_Mag_M.png"}})
    mw = rifle.matrix_world.copy()
    rifle.parent = None
    rifle.matrix_world = mw
    bpy.ops.object.select_all(action="DESELECT")
    rifle.select_set(True)
    bpy.context.view_layer.objects.active = rifle
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # rifle frame in Blender coords: grip at origin; barrel axis = direction to the farthest vertex end (muzzle)
    vs = [v.co for v in rifle.data.vertices]
    ax = max(range(3), key=lambda i: max(v[i] for v in vs) - min(v[i] for v in vs))
    sgn = 1 if max(v[ax] for v in vs) > -min(v[ax] for v in vs) else -1
    BARREL = Vector([0, 0, 0]); BARREL[ax] = sgn
    UP = Vector((0, 0, 1)) if ax != 2 else Vector((0, 1, 0))
    LH_LOCAL = BARREL * 0.316 + UP * 0.054
    for o in [o for o in bpy.data.objects if o not in rb and o != rifle]:
        bpy.data.objects.remove(o, do_unlink=True)


def palm(side):
    pb = arm.pose.bones
    h = (arm.matrix_world @ pb[f"Bip01 {side} Hand"].matrix).translation
    m = (arm.matrix_world @ pb[f"Bip01 {side} Finger2"].matrix).translation
    return h + (m - h) * 0.55


def place_rifle():
    rh, lh = palm("R"), palm("L")
    d = lh - rh
    if d.length < 1e-4:
        return
    # rifle local: grip -> LeftHand socket vector = LH_LOCAL; world: rh -> lh
    want = d.normalized()
    have = LH_LOCAL.normalized()
    up_w = Vector((0, 0, 1))
    # build orthonormal frames (barrel-ish dir + up) and map local -> world
    def frame(fwd, up):
        x = fwd.normalized()
        z = (up - x * up.dot(x)).normalized()
        y = z.cross(x)
        return Matrix((x, y, z)).transposed()
    R = frame(want, up_w) @ frame(have, UP).inverted()
    rifle.matrix_world = Matrix.Translation(rh) @ R.to_4x4()


setup_scene(spec)  # noqa: F821
frames = spec.get("frames") or sorted({int(round(f0 + (f1 - f0) * x)) for x in spec.get("frames_frac", [0, 0.33, 0.66, 1])})
files = []
for f in frames:
    sc.frame_set(f)
    if rifle:
        place_rifle()
    if spec.get("follow", True):
        p = (arm.matrix_world @ arm.pose.bones["Bip01 Pelvis"].matrix).translation
        spec["target_xy"] = [p.x, p.y]
    if spec.get("fp_views"):
        hb = arm.matrix_world @ arm.pose.bones["Bip01 Head"].matrix
        nb = (arm.matrix_world @ arm.pose.bones["Bip01 Neck"].matrix).translation
        hands = (palm("R") + palm("L")) / 2
        fwd = hands - hb.translation
        fwd.z = 0
        fwd.normalize()
        eye = hb.translation + fwd * 0.09 + Vector((0, 0, 0.075))
        for name, pitch, yaw, lens in spec["fp_views"]:
            d = Matrix.Rotation(math.radians(yaw), 3, "Z") @ fwd
            side = d.cross(Vector((0, 0, 1))).normalized()
            d = Matrix.Rotation(math.radians(pitch), 3, side) @ d
            cam = sc.camera
            cam.data.type = "PERSP"
            cam.data.lens = lens
            cam.data.clip_start = 0.02
            cam.location = eye
            cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
            fn = f"{spec['out']}_{name}_f{f:03d}.png"
            sc.render.filepath = fn
            bpy.ops.render.render(write_still=True)
            files.append(fn)
    else:
        files += render_views(spec, f"_f{f:03d}")  # noqa: F821
json.dump(files, open(spec["out"] + "_files.json", "w"))
print("PREVIEWED", len(files))
