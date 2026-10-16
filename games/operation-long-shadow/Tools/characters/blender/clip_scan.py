"""Scan clip FBX files: frame range, hip travel, yaw, hip height (WP05).
Run: Blender -b --factory-startup -P Tools/characters/blender/clip_scan.py -- <out.json> <clip.fbx> [...]
Used for the SAC clips (RawAssets/wp05/_work/sac_scan.json -> package_local_clips.py loop/root-motion flags).
"""
import bpy, sys, json, math
argv = sys.argv[sys.argv.index("--")+1:]
out = {}
for p in argv[1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=p, automatic_bone_orientation=False, use_anim=True)
    a = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    act = bpy.data.actions[0]
    f0, f1 = (int(x) for x in act.frame_range)
    sc = bpy.context.scene
    hips = next(b for b in a.pose.bones if b.name.lower() in ("hips", "pelvis") or b.name.endswith("Hips"))
    def hp(f):
        sc.frame_set(f); m = a.matrix_world @ hips.matrix
        fwd = (m.to_quaternion() @ __import__("mathutils").Vector((0, 0, 1)))
        return m.translation.copy(), m.to_euler()
    p0, e0 = hp(f0); p1, e1 = hp(f1)
    d = p1 - p0
    out[p.split("/")[-1]] = {"frames": [f0, f1], "n": f1 - f0 + 1, "hips": hips.name, "travel_xy": round(math.hypot(d.x, d.y), 3),
                             "dz": round(d.z, 3), "yaw_deg": round(math.degrees(e1.z - e0.z), 1), "hip_z": round(p0.z, 3)}
    print("SCAN", p.split("/")[-1], out[p.split("/")[-1]])
json.dump(out, open(argv[0], "w"), indent=1)
