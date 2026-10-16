"""Retarget a clip onto a Rocketbox body (world-space rotation deltas from matching A/T rest poses) for preview
renders, hand-placement metrics, or baking the clip onto the Rocketbox skeleton (WP05).

Run: Blender -b --factory-startup -P Tools/characters/blender/retarget.py -- <spec.json>
Spec:
  target_fbx        Rocketbox body exported by rb_export.py (bind pose = rest)
  textures          {material: {bc,n,m}} for renders (optional)
  source_fbx        clip FBX (Meshy 24-bone rig, Quaternius UAL, Rocketbox Biped, HY-Motion SMPL ...)
  map               "meshy" | "ual" | "rocketbox" | "smpl" | {src bone: dst bone}
  frames            [f, ...] frames to render (source frame numbers) ; "metrics_all": true -> metrics for every frame
  views             like rb_render.py
  out               PNG prefix
  stick             true -> draw a 0.75 m "rifle" stick from the right hand through the left hand
  bake_fbx          optional: export the baked clip on the Rocketbox skeleton (armature only) to this path
  root_motion       true (default) copies the source hips translation (scaled by leg length ratio)
Writes <out>_metrics.json: per frame right->left hand distance, hand height, stick direction vs body forward.
"""
import bpy, sys, json, math, os, mathutils
from mathutils import Matrix, Vector, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:]
spec = json.load(open(argv[0]))
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


def import_fbx(path, anim):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, use_anim=anim, ignore_leaf_bones=False)
    new = [o for o in bpy.data.objects if o not in before]
    return next(o for o in new if o.type == "ARMATURE"), [o for o in new if o.type == "MESH"]


tgt, tgt_meshes = import_fbx(spec["target_fbx"], False)
src, src_meshes = import_fbx(spec["source_fbx"], True)
for o in src_meshes:
    o.hide_render = True
    o.hide_viewport = True
mp = spec.get("map", "meshy")
mp = MAPS[mp] if isinstance(mp, str) else mp
mp = {s: d for s, d in mp.items() if s in src.pose.bones and d in tgt.pose.bones}
# extra: map any identical names (rocketbox -> rocketbox)
if spec.get("map") == "rocketbox":
    mp = {b.name: b.name for b in src.pose.bones if b.name in tgt.pose.bones}
print("mapped bones", len(mp))
act = src.animation_data.action if src.animation_data else None
f0, f1 = (int(act.frame_range[0]), int(act.frame_range[1])) if act else (1, 1)

# rest world rotations
def rest_world(arm, bn):
    return (arm.matrix_world @ arm.data.bones[bn].matrix_local)

src_rest = {s: rest_world(src, s) for s in mp}
tgt_rest = {d: rest_world(tgt, d) for d in mp.values()}
# rest alignment: rotate target rest so the bone direction matches the source rest direction (swing only)
corr = {}
inv0 = {d: s for s, d in mp.items()}
for b in tgt.data.bones:  # parents first
    d = b.name
    if d not in inv0:
        continue
    s = inv0[d]
    sb, db = src.data.bones[s], tgt.data.bones[d]
    sc_ = [c for c in sb.children if c.name in mp]
    if sc_:
        sv = (src.matrix_world @ sc_[0].head_local - src.matrix_world @ sb.head_local).normalized()
        dv = (tgt.matrix_world @ tgt.data.bones[mp[sc_[0].name]].head_local - tgt.matrix_world @ db.head_local).normalized()
        corr[d] = dv.rotation_difference(sv)  # rotates target rest dir onto source rest dir
    else:  # leaf (head, hands, toes): bone tails are arbitrary after FBX import -> inherit the parent's correction
        p = db.parent
        while p is not None and p.name not in corr:
            p = p.parent
        corr[d] = corr[p.name].copy() if p is not None else Quaternion()
src_hips = next(iter(mp))
for s in mp:
    if src.data.bones[s].parent is None or s in ("Hips", "pelvis", "Pelvis", "Bip01 Pelvis"):
        src_hips = s
        break
leg_ratio = 1.0
try:
    sh = (src.matrix_world @ src.data.bones[src_hips].head_local).z
    th = (tgt.matrix_world @ tgt.data.bones[mp[src_hips]].head_local).z
    leg_ratio = th / max(sh, 1e-3)
except Exception:
    pass

order = [b.name for b in tgt.data.bones]  # parents before children
tgt.animation_data_create()
tact = bpy.data.actions.new("retarget")
tgt.animation_data.action = tact
for pb in tgt.pose.bones:
    pb.rotation_mode = "QUATERNION"
inv_map = {d: s for s, d in mp.items()}


def solve_frame(f):
    sc.frame_set(f)
    dg = bpy.context.evaluated_depsgraph_get()
    sw = {}
    for s in mp:
        sw[s] = src.matrix_world @ src.pose.bones[s].matrix
    world = {}
    for bn in order:
        b = tgt.data.bones[bn]
        pb = tgt.pose.bones[bn]
        rest_local = (b.parent.matrix_local.inverted() @ b.matrix_local) if b.parent else b.matrix_local
        pw = world[b.parent.name] if b.parent else tgt.matrix_world
        base = pw @ rest_local
        if bn in inv_map:
            s = inv_map[bn]
            r_src = sw[s].to_quaternion() @ src_rest[s].to_quaternion().inverted()
            r_t = r_src @ corr[bn] @ tgt_rest[bn].to_quaternion()
            loc = base.translation
            if s == src_hips and spec.get("root_motion", True):
                d = (sw[s].translation - src_rest[s].translation) * leg_ratio
                loc = tgt_rest[bn].translation + d
            desired = Matrix.LocRotScale(loc, r_t, Vector((1, 1, 1)))
            basis = base.inverted() @ desired
        else:
            basis = Matrix.Identity(4)
        world[bn] = base @ basis
        pb.matrix_basis = basis
        pb.keyframe_insert("rotation_quaternion", frame=f)
        if b.parent is None or bn == mp.get(src_hips):
            pb.keyframe_insert("location", frame=f)
    return world


metrics = []
frames_all = list(range(f0, f1 + 1))
for f in frames_all:
    w = solve_frame(f)
    rh = w["Bip01 R Hand"].translation
    lh = w["Bip01 L Hand"].translation
    pel = w["Bip01 Pelvis"]
    fwd = (pel.to_3x3() @ Vector((0, -1, 0)))  # Rocketbox pelvis forward is not guaranteed: use body forward from hips
    hipv = (w["Bip01 L Thigh"].translation - w["Bip01 R Thigh"].translation)
    body_fwd = hipv.cross(Vector((0, 0, 1))).normalized()
    stick = (lh - rh)
    metrics.append({"frame": f, "rh_lh_m": round(stick.length, 3), "rh_z": round(rh.z, 3), "lh_z": round(lh.z, 3),
                    "stick_vs_fwd_deg": round(math.degrees(stick.normalized().angle(body_fwd)) if stick.length > 1e-4 else 0, 1)})
json.dump({"source": spec["source_fbx"], "frames": [f0, f1], "metrics": metrics}, open(spec["out"] + "_metrics.json", "w"), indent=0)

if spec.get("bake_fbx"):
    for o in tgt_meshes:
        o.select_set(False)
    bpy.ops.object.select_all(action="DESELECT")
    tgt.select_set(True)
    bpy.context.view_layer.objects.active = tgt
    sc.frame_start, sc.frame_end = f0, f1
    tact.name = spec.get("clip_name", "clip")
    bpy.ops.export_scene.fbx(filepath=spec["bake_fbx"], use_selection=True, object_types={"ARMATURE"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                             bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    print("BAKED", spec["bake_fbx"])

if spec.get("frames_frac"):
    spec["frames"] = sorted({int(round(f0 + (f1 - f0) * x)) for x in spec["frames_frac"]})
if spec.get("frames"):
    exec(open(os.path.join(os.path.dirname(__file__), "render_common.py")).read())
    setup_scene(spec)  # noqa: F821
    for o in tgt_meshes:
        apply_textures(o, spec.get("textures", {}))  # noqa: F821
    stick_obj = None
    if spec.get("stick"):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.018, depth=0.75)
        stick_obj = bpy.context.active_object
        m = bpy.data.materials.new("stick")
        m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.05, 0.05, 1)
        stick_obj.data.materials.append(m)
    files = []
    for f in spec["frames"]:
        sc.frame_set(f)
        if stick_obj:
            rh = (tgt.matrix_world @ tgt.pose.bones["Bip01 R Hand"].matrix).translation
            lh = (tgt.matrix_world @ tgt.pose.bones["Bip01 L Hand"].matrix).translation
            d = (lh - rh)
            if d.length > 1e-4:
                dn = d.normalized()
                stick_obj.location = rh + dn * 0.30
                stick_obj.rotation_euler = dn.to_track_quat("Z", "Y").to_euler()
        files += render_views(spec, f"_f{f:03d}")  # noqa: F821
    json.dump(files, open(spec["out"] + "_files.json", "w"))
print("DONE", len(metrics), "frames")
