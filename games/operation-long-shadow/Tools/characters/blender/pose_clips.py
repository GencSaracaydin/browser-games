"""Finger pose clips A_Pose_* on the WP05 Rocketbox skeleton (ANIM_FINGER_POSES) + hand close-up renders (WP05).

Run: Blender -b --factory-startup -P Tools/characters/blender/pose_clips.py -- <target_fbx> <out_dir> <render_dir> [tex.json]
Each clip: 2 identical frames (30 fps) of the bind pose with the fingers posed by finger_poses.py. Use on the AM_Hands
masked layer (L3) or a Humanoid finger-only mask; the body part of the clip is the A-pose bind and must be masked out.
"""
import bpy, sys, json, math, os
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
target, out_dir, rdir = argv[0], argv[1], argv[2]
tex = json.load(open(argv[3])) if len(argv) > 3 else {}
HERE = os.path.dirname(os.path.abspath(__file__))
ns = {}
exec(open(os.path.join(HERE, "finger_poses.py")).read(), ns)
POSES = {"RifleGrip": "RifleGrip", "PistolGrip": "PistolGrip", "Fist": "Fist", "Relaxed": "Relaxed", "FlatHands": "FlatHands"}
os.makedirs(out_dir, exist_ok=True)
os.makedirs(rdir, exist_ok=True)
renders = []
for clip, pose in POSES.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = 30
    bpy.ops.import_scene.fbx(filepath=target, automatic_bone_orientation=False, use_anim=False)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.matrix_basis = Matrix.Identity(4)
    rots = ns["finger_locals"](arm, {"L": pose, "R": pose})
    arm.animation_data_create()
    act = bpy.data.actions.new(f"A_Pose_{clip}")
    arm.animation_data.action = act
    for fr in (1, 2):
        for pb in arm.pose.bones:
            pb.matrix_basis = Matrix.LocRotScale(Vector(), rots[pb.name], Vector((1, 1, 1))) if pb.name in rots else Matrix.Identity(4)
            pb.keyframe_insert("rotation_quaternion", frame=fr)
            if pb.parent is None:
                pb.keyframe_insert("location", frame=fr)
    sc.frame_start, sc.frame_end = 1, 2
    sc.frame_set(1)
    # hand close-up renders (both hands) with the body textures
    if tex:
        exec(open(os.path.join(HERE, "render_common.py")).read())
        for o in meshes:
            apply_textures(o, tex)  # noqa: F821
        setup_scene({"res": [480, 360], "sun_el": 60})  # noqa: F821
        cam = sc.camera
        for side in "LR":
            h = (arm.matrix_world @ arm.pose.bones[f"Bip01 {side} Hand"].matrix).translation
            m = (arm.matrix_world @ arm.pose.bones[f"Bip01 {side} Finger2"].matrix).translation
            c = (h + m) / 2
            for vn, d in (("front", Vector((0, -1, 0.25))), ("below", Vector((0.0, -0.4, -1)))):
                cam.data.type = "PERSP"
                cam.data.lens = 60
                cam.location = c + d.normalized() * 0.45
                cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
                fn = os.path.join(rdir, f"A_Pose_{clip}_{side}_{vn}.png")
                sc.render.filepath = fn
                bpy.ops.render.render(write_still=True)
                renders.append(fn)
    for o in meshes:
        bpy.data.objects.remove(o, do_unlink=True)
    for o in list(bpy.data.objects):
        if o.type not in ("ARMATURE",):
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    fp = os.path.join(out_dir, f"A_Pose_{clip}.fbx")
    bpy.ops.export_scene.fbx(filepath=fp, use_selection=True, object_types={"ARMATURE"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", armature_nodetype="NULL",
                             bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                             bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    print("POSE", fp)
json.dump(renders, open(os.path.join(rdir, "files.json"), "w"))
