"""Bake a humanoid clip from any source rig onto the WP05 Rocketbox skeleton (Bip01, fixed hierarchy) (WP05).

Run: Blender -b --factory-startup -P Tools/characters/blender/bake_clip.py -- <spec.json>
Spec keys:
  target_fbx     skeleton reference: a body exported by rb_export.py (e.g. ready/.../SK_Calder.fbx)
  source_fbx     clip FBX: Meshy 24-bone rig ("meshy"), Quaternius UAL ("ual"), HY-Motion SMPL ("smpl"),
                 Rocketbox Biped .max.fbx ("rocketbox": same bone names, world rotations copied 1:1)
  map            one of the names above or {src bone: dst bone}
  out_fbx        armature-only clip FBX (one take named clip_name), same hierarchy/names as the target body
  clip_name      take / action name (e.g. A_Meshy_KneelingReload)
  frames         optional [first, last] source frames (default: the action range)
  root           "full" (default: hips XYZ motion scaled by the hip-height ratio) | "inplace_xy" (removes the
                 average XY travel, keeps bob/sway) | "fixed" (pelvis held at its rest XY, Z kept)
  fingers        "rest" (default) | name of a pose in finger_poses.py ("RifleGrip", "PistolGrip", "Fist",
                 "Relaxed", "FlatHands") | {"L": pose, "R": pose}; only used when the source has no finger bones
  ground         true (default): shift the whole clip vertically so the lowest foot/toe joint over the clip
                 matches the source's lowest foot joint (scaled) -> no floating / sinking from leg proportions
  metrics_out    optional JSON: per-frame hand distance, foot heights, pelvis height (source vs target)
Math: per mapped bone, target world rotation = D(f) * C * R_t0, where D(f) = R_s(f) * R_s0^-1 is the source
bone's world-space rotation delta from its rest and C is the swing that turns the target rest bone direction
(head -> aim child) onto the source rest direction. Bone directions therefore follow the source exactly;
twist follows the source delta. Unmapped target bones keep their rest local rotation (fingers, face).
"""
import bpy, sys, json, math, os
from mathutils import Matrix, Vector, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:]
spec = json.load(open(argv[0]))
HERE = os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

MAPS = {
    "meshy": {"Hips": "Bip01 Pelvis", "Spine02": "Bip01 Spine", "Spine01": "Bip01 Spine1", "Spine": "Bip01 Spine2",
              "neck": "Bip01 Neck", "Head": "Bip01 Head",
              "LeftShoulder": "Bip01 L Clavicle", "LeftArm": "Bip01 L UpperArm", "LeftForeArm": "Bip01 L Forearm", "LeftHand": "Bip01 L Hand",
              "RightShoulder": "Bip01 R Clavicle", "RightArm": "Bip01 R UpperArm", "RightForeArm": "Bip01 R Forearm", "RightHand": "Bip01 R Hand",
              "LeftUpLeg": "Bip01 L Thigh", "LeftLeg": "Bip01 L Calf", "LeftFoot": "Bip01 L Foot", "LeftToeBase": "Bip01 L Toe0",
              "RightUpLeg": "Bip01 R Thigh", "RightLeg": "Bip01 R Calf", "RightFoot": "Bip01 R Foot", "RightToeBase": "Bip01 R Toe0"},
    "ual": {"pelvis": "Bip01 Pelvis", "spine_01": "Bip01 Spine", "spine_02": "Bip01 Spine1", "spine_03": "Bip01 Spine2",
            "neck_01": "Bip01 Neck", "Head": "Bip01 Head",
            "clavicle_l": "Bip01 L Clavicle", "upperarm_l": "Bip01 L UpperArm", "lowerarm_l": "Bip01 L Forearm", "hand_l": "Bip01 L Hand",
            "clavicle_r": "Bip01 R Clavicle", "upperarm_r": "Bip01 R UpperArm", "lowerarm_r": "Bip01 R Forearm", "hand_r": "Bip01 R Hand",
            "thigh_l": "Bip01 L Thigh", "calf_l": "Bip01 L Calf", "foot_l": "Bip01 L Foot", "ball_l": "Bip01 L Toe0",
            "thigh_r": "Bip01 R Thigh", "calf_r": "Bip01 R Calf", "foot_r": "Bip01 R Foot", "ball_r": "Bip01 R Toe0"},
    "smpl": {"Pelvis": "Bip01 Pelvis", "Spine1": "Bip01 Spine", "Spine2": "Bip01 Spine1", "Spine3": "Bip01 Spine2", "Neck": "Bip01 Neck",
             "Head": "Bip01 Head", "L_Collar": "Bip01 L Clavicle", "L_Shoulder": "Bip01 L UpperArm", "L_Elbow": "Bip01 L Forearm",
             "L_Wrist": "Bip01 L Hand", "R_Collar": "Bip01 R Clavicle", "R_Shoulder": "Bip01 R UpperArm", "R_Elbow": "Bip01 R Forearm",
             "R_Wrist": "Bip01 R Hand", "L_Hip": "Bip01 L Thigh", "L_Knee": "Bip01 L Calf", "L_Ankle": "Bip01 L Foot", "L_Foot": "Bip01 L Toe0",
             "R_Hip": "Bip01 R Thigh", "R_Knee": "Bip01 R Calf", "R_Ankle": "Bip01 R Foot", "R_Foot": "Bip01 R Toe0"},
}
for _s in "LR":  # SMPL-H fingers (HY-Motion)
    for _f, _c in (("Thumb", "0"), ("Index", "1"), ("Middle", "2"), ("Ring", "3"), ("Pinky", "4")):
        MAPS["smpl"][f"{_s}_{_f}1"] = f"Bip01 {_s} Finger{_c}"
        MAPS["smpl"][f"{_s}_{_f}2"] = f"Bip01 {_s} Finger{_c}1"
        MAPS["smpl"][f"{_s}_{_f}3"] = f"Bip01 {_s} Finger{_c}2"
# target bone -> child that defines its direction (rest alignment). Missing = inherit the parent's correction.
AIM = {"Bip01 Pelvis": "Bip01 Spine", "Bip01 Spine": "Bip01 Spine1", "Bip01 Spine1": "Bip01 Spine2", "Bip01 Spine2": "Bip01 Neck",
       "Bip01 Neck": "Bip01 Head"}
for s in "LR":
    AIM.update({f"Bip01 {s} Clavicle": f"Bip01 {s} UpperArm", f"Bip01 {s} UpperArm": f"Bip01 {s} Forearm",
                f"Bip01 {s} Forearm": f"Bip01 {s} Hand", f"Bip01 {s} Thigh": f"Bip01 {s} Calf",
                f"Bip01 {s} Calf": f"Bip01 {s} Foot", f"Bip01 {s} Foot": f"Bip01 {s} Toe0"})
    for c in "01234":
        AIM.update({f"Bip01 {s} Finger{c}": f"Bip01 {s} Finger{c}1", f"Bip01 {s} Finger{c}1": f"Bip01 {s} Finger{c}2"})


def import_fbx(path, anim):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, use_anim=anim, ignore_leaf_bones=False)
    new = [o for o in bpy.data.objects if o not in before]
    return next(o for o in new if o.type == "ARMATURE"), new


tgt, tnew = import_fbx(spec["target_fbx"], False)
for o in tnew:
    if o.type == "MESH":
        bpy.data.objects.remove(o, do_unlink=True)
for pb in tgt.pose.bones:  # clear the T-pose node defaults: work from the bind pose
    pb.matrix_basis = Matrix.Identity(4)
fps_target = spec.get("fps")
src, snew = import_fbx(spec["source_fbx"], True)
for o in snew:
    if o.type == "MESH":
        o.hide_render = o.hide_viewport = True
fps = sc.render.fps / sc.render.fps_base
mapname = spec.get("map", "meshy")
copy_world = mapname == "rocketbox"
if copy_world:
    mp = {b.name: b.name for b in src.data.bones if b.name in tgt.data.bones}
else:
    mp = MAPS[mapname] if isinstance(mapname, str) else mapname
    mp = {s: d for s, d in mp.items() if s in src.data.bones and d in tgt.data.bones}
inv = {d: s for s, d in mp.items()}
print("mapped bones", len(mp))

# the source action range (object-level root animation is evaluated through matrix_world)
acts = [o.animation_data.action for o in snew if o.animation_data and o.animation_data.action]
f0 = min(int(a.frame_range[0]) for a in acts) if acts else 1
f1 = max(int(a.frame_range[1]) for a in acts) if acts else 1
if spec.get("frames"):
    f0, f1 = spec["frames"]


def wmat(arm, bn, pose):
    return arm.matrix_world @ (arm.pose.bones[bn].matrix if pose else arm.data.bones[bn].matrix_local)


sc.frame_set(f0)
# source rest: bind pose (data bones) unless the file has none useful -> use spec "src_rest": "frame" to use frame f0
src_rest_pose = spec.get("src_rest") == "frame"
Rs0 = {s: wmat(src, s, src_rest_pose).to_quaternion() for s in mp}
Ps0 = {s: wmat(src, s, src_rest_pose).translation.copy() for s in mp}
Rt0 = {d: wmat(tgt, d, False).to_quaternion() for d in mp.values()}
Pt0 = {d: wmat(tgt, d, False).translation.copy() for d in mp.values()}

# rest alignment C per target bone
C = {}
for b in tgt.data.bones:  # parents first
    d = b.name
    if d not in inv:
        continue
    if copy_world:
        C[d] = Rs0[inv[d]] @ Rt0[d].inverted()  # full rotation: identical skeletons
        continue
    ch = AIM.get(d)
    if ch and ch in inv:
        sv = (Ps0[inv[ch]] - Ps0[inv[d]]).normalized()
        dv = (Pt0[ch] - Pt0[d]).normalized()
        C[d] = dv.rotation_difference(sv)
    else:
        p = b.parent
        while p is not None and p.name not in C:
            p = p.parent
        C[d] = C[p.name].copy() if p is not None else Quaternion()

hips_s = next(s for s, d in mp.items() if d == "Bip01 Pelvis")
# hip-height ratio measured from the rest feet (some sources, e.g. HY-Motion SMPL, have their origin at the hips)
_sf = [s for s, d in mp.items() if d in ("Bip01 L Foot", "Bip01 R Foot")]
_tf = [d for d in ("Bip01 L Foot", "Bip01 R Foot") if d in Pt0]
if _sf and _tf:
    ratio = spec.get("scale") or ((Pt0["Bip01 Pelvis"].z - min(Pt0[d].z for d in _tf)) /
                                  max(Ps0[hips_s].z - min(Ps0[s].z for s in _sf), 1e-3))
else:
    ratio = spec.get("scale") or (Pt0["Bip01 Pelvis"].z / max(Ps0[hips_s].z, 1e-3))
if copy_world:
    ratio = spec.get("scale", 1.0)
print("hip ratio", round(ratio, 4), "fps", fps, "frames", f0, f1)

# finger pose (only when the source has no finger bones)
finger_rot = {}
has_fingers = any("Finger" in d for d in mp.values())
fp = spec.get("fingers", "rest")
if fp != "rest" and not has_fingers:
    ns = {}
    exec(open(os.path.join(HERE, "finger_poses.py")).read(), ns)
    finger_rot = ns["finger_locals"](tgt, fp if isinstance(fp, dict) else {"L": fp, "R": fp})

order = [b.name for b in tgt.data.bones]
tgt.animation_data_create()
act = bpy.data.actions.new(spec.get("clip_name", "clip"))
tgt.animation_data.action = act
for pb in tgt.pose.bones:
    pb.rotation_mode = "QUATERNION"

frames = list(range(f0, f1 + 1))
if spec.get("hold_frame"):  # single held pose (e.g. prone idle from the first frame of Prone_Reload)
    frames = [int(spec["hold_frame"])]
samples = []
for f in frames:
    sc.frame_set(f)
    sw = {s: wmat(src, s, True) for s in mp}
    world = {}
    pose = {}
    for bn in order:
        b = tgt.data.bones[bn]
        rest_local = (b.parent.matrix_local.inverted() @ b.matrix_local) if b.parent else b.matrix_local
        pw = world[b.parent.name] if b.parent else tgt.matrix_world
        base = pw @ rest_local
        if bn in inv:
            s = inv[bn]
            if copy_world:
                r_t = sw[s].to_quaternion() @ Rs0[s].inverted() @ C[bn] @ Rt0[bn]
            else:
                r_t = (sw[s].to_quaternion() @ Rs0[s].inverted()) @ C[bn] @ Rt0[bn]
            loc = base.translation
            if bn == "Bip01 Pelvis":
                loc = Pt0[bn] + (sw[s].translation - Ps0[s]) * ratio
            desired = Matrix.LocRotScale(loc, r_t, Vector((1, 1, 1)))
            basis = base.inverted() @ desired
        elif bn in finger_rot:
            basis = Matrix.LocRotScale(Vector(), finger_rot[bn], Vector((1, 1, 1)))
        else:
            basis = Matrix.Identity(4)
        world[bn] = base @ basis
        pose[bn] = basis
    samples.append((f, pose, world, sw))

if spec.get("hold_frame"):  # replicate the held pose for hold_seconds with a slow breathing sway on the spine
    f, pose, world, sw = samples[0]
    n = int(round(spec.get("hold_seconds", 4.0) * fps))
    amp = spec.get("breath", 0.012)
    side = Vector((1, 0, 0))
    out = []
    for i in range(n):
        ph = math.sin(2 * math.pi * i / n)  # exactly one breath per loop -> seamless
        p2 = dict(pose)
        for bn, k in (("Bip01 Spine1", 1.0), ("Bip01 Spine2", 0.8), ("Bip01 Neck", -0.6)):
            ax_local = (world[bn].to_quaternion().inverted() @ side).normalized()
            p2[bn] = pose[bn] @ Quaternion(ax_local, amp * k * ph).to_matrix().to_4x4()
        out.append((f0 + i, p2, dict(world), sw))
    samples = out
    frames = [s_[0] for s_ in samples]

# root post-processing
pel = [w["Bip01 Pelvis"].translation.copy() for _, _, w, _ in samples]
shift = Vector((0, 0, 0))
root_mode = spec.get("root", "full")
if spec.get("ground", True):
    # absolute grounding: the lowest ankle over the clip sits at the target's standing ankle height (feet touch the
    # ground at least once in every clip we use; source rest poses are not always grounded, e.g. HY-Motion SMPL)
    feet = ["Bip01 L Foot", "Bip01 R Foot"]
    tmin = min(min(w[n].translation.z for n in feet) for _, _, w, _ in samples)
    tt0 = min(Pt0[n].z if n in Pt0 else (tgt.matrix_world @ tgt.data.bones[n].head_local).z for n in feet)
    shift.z = tt0 - tmin
avg = sum(pel, Vector()) / len(pel)
drift = (pel[-1] - pel[0])
for i, (f, pose, world, sw) in enumerate(samples):
    off = shift.copy()
    if root_mode == "inplace_xy":
        t = i / max(1, len(samples) - 1)
        off.x -= (pel[0].x + drift.x * t) - Pt0["Bip01 Pelvis"].x
        off.y -= (pel[0].y + drift.y * t) - Pt0["Bip01 Pelvis"].y
    elif root_mode == "fixed":
        off.x -= pel[i].x - Pt0["Bip01 Pelvis"].x
        off.y -= pel[i].y - Pt0["Bip01 Pelvis"].y
    if off.length > 0:
        b = tgt.data.bones["Bip01 Pelvis"]
        base = tgt.matrix_world @ b.matrix_local  # pelvis is the root bone
        desired = Matrix.Translation(off) @ world["Bip01 Pelvis"]
        pose["Bip01 Pelvis"] = base.inverted() @ desired
        # world of children shifts by the same offset (metrics only)
        for k in world:
            world[k] = Matrix.Translation(off) @ world[k]
    for bn, basis in pose.items():
        pb = tgt.pose.bones[bn]
        pb.matrix_basis = basis
        pb.keyframe_insert("rotation_quaternion", frame=f - f0 + 1)
        if bn == "Bip01 Pelvis":
            pb.keyframe_insert("location", frame=f - f0 + 1)

# metrics
if spec.get("metrics_out"):
    rows = []
    for (f, pose, world, sw) in samples:
        r = {"f": f}
        rh, lh = world["Bip01 R Hand"].translation, world["Bip01 L Hand"].translation
        r["t_hands"] = round((rh - lh).length, 3)
        r["t_pelvis_z"] = round(world["Bip01 Pelvis"].translation.z, 3)
        r["t_foot_min"] = round(min(world[n].translation.z for n in ("Bip01 L Foot", "Bip01 R Foot")), 3)
        r["lf"] = round(world["Bip01 L Foot"].translation.z, 4)
        r["rf"] = round(world["Bip01 R Foot"].translation.z, 4)
        r["pel"] = [round(x, 4) for x in world["Bip01 Pelvis"].translation]
        sh = [s for s, d in mp.items() if d in ("Bip01 R Hand", "Bip01 L Hand")]
        if len(sh) == 2:
            r["s_hands"] = round((sw[sh[0]].translation - sw[sh[1]].translation).length * ratio, 3)
        sf = [s for s, d in mp.items() if d in ("Bip01 L Foot", "Bip01 R Foot")]
        if sf:
            r["s_foot_min"] = round(min(sw[s].translation.z for s in sf) * ratio, 3)
        r["s_pelvis_z"] = round(sw[hips_s].translation.z * ratio, 3)
        rows.append(r)
    json.dump({"source": spec["source_fbx"], "fps": fps, "frames": [f0, f1], "ratio": ratio, "shift_z": shift.z, "rows": rows},
              open(spec["metrics_out"], "w"), indent=0)

# export: armature only, one take
for o in list(bpy.data.objects):
    if o != tgt:
        bpy.data.objects.remove(o, do_unlink=True)
for a in list(bpy.data.actions):
    if a != act:
        bpy.data.actions.remove(a)
sc.frame_start, sc.frame_end = 1, len(frames)
sc.render.fps = int(round(fps))
sc.render.fps_base = 1.0
bpy.ops.object.select_all(action="DESELECT")
tgt.select_set(True)
bpy.context.view_layer.objects.active = tgt
os.makedirs(os.path.dirname(os.path.abspath(spec["out_fbx"])), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=spec["out_fbx"], use_selection=True, object_types={"ARMATURE"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                         add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", armature_nodetype="NULL",
                         bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                         bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
                         bake_anim_simplify_factor=0.0)
print("BAKED", spec["out_fbx"], "frames", len(frames), "fps", fps, "shift_z", round(shift.z, 3))
