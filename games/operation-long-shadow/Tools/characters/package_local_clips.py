#!/usr/bin/env python3
"""Package the ready-made humanoid clip libraries that need no retargeting (WP05).

  Tools/.venv/bin/python Tools/characters/package_local_clips.py

* Quaternius Universal Animation Library 1 + 2 (CC0): the four Unity FBX files (in place + _RM root motion) are copied
  unchanged (they carry their own mannequin rig and avatar) -> Clips/UAL1/, Clips/UAL2/  (ANIM_UAL1, ANIM_UAL2)
* Unity Standard Assets Characters (Unity Companion License): m@<Clip>.fbx clips renamed A_SAC_<Clip>.fbx plus the avatar
  source rig SK_SAC_DefaultMaleRig.fbx -> Clips/SAC/  (ANIM_SAC_SUBSET local 8 clips, ANIM_SAC_FULL 20 downloaded clips)
Frame ranges, travel and yaw per SAC clip were measured in Blender (RawAssets/wp05/_work/sac_scan.json, see
Tools/characters/blender/clip_scan.py); loop / root-motion flags below follow from them.
Writes RawAssets/wp05/_work/index/clips_local.json (merged into ready/import_index.json by make_index.py) and a
provenance.json next to the copies' raw sources.
"""
import json, pathlib, shutil

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
W5 = ROOT / "RawAssets/wp05"
READY = W5 / "ready"
CLIPS = READY / "Assets/_Game/Art/Animation/Clips"
CC0 = ROOT / "RawAssets/cc0/characters"
UAL1 = CC0 / "quaternius_ual/extracted/Universal Animation Library[Standard]/Unity"
UAL2 = CC0 / "quaternius_ual2/extracted/Universal Animation Library 2[Standard]/Unity"
SAC_LOCAL = CC0 / "unity_standard_assets_characters_UCL"
SAC_DL = W5 / "SAC_FULL"

# name: (manifest, loop, root motion kind, use)   root: "inplace" | "travel" (XZ root motion) | "turn" (yaw + XZ root motion)
SAC = {
    "StrafeIdle": ("ANIM_SAC_SUBSET", True, "inplace", "L0 base idle (aim stance)"),
    "WalkForwards": ("ANIM_SAC_SUBSET", True, "inplace", "L0 walk forward (1.73 m per cycle = 1.73 m/s)"),
    "RunForwards": ("ANIM_SAC_SUBSET", True, "inplace", "L0 jog forward (2.77 m per 0.6 s cycle = 4.6 m/s)"),
    "SprintForwards": ("ANIM_SAC_SUBSET", True, "inplace", "L0 sprint (3.11 m per 0.53 s = 5.8 m/s)"),
    "StrafeWalkForwards": ("ANIM_SAC_SUBSET", True, "inplace", "L0 2D freeform: walk forward, aim stance"),
    "StrafeRight": ("ANIM_SAC_SUBSET", True, "inplace", "L0 2D freeform: walk right (mirror -> A_SAC_StrafeLeft)"),
    "StrafeRightWalkBackwards": ("ANIM_SAC_SUBSET", True, "inplace", "L0 2D freeform: walk back-right (mirror -> back-left)"),
    "StrafeRightRunForwards": ("ANIM_SAC_SUBSET", True, "inplace", "L0 2D freeform: run fwd-right (mirror -> fwd-left)"),
    "Idle": ("ANIM_SAC_FULL", True, "inplace", "relaxed idle"),
    "StrafeRightWalkForwards": ("ANIM_SAC_FULL", True, "inplace", "L0 walk fwd-right (mirror -> fwd-left)"),
    "StrafeRightRunBackwards": ("ANIM_SAC_FULL", True, "inplace", "L0 run back-right (mirror -> back-left)"),
    "RapidStrafeForwards": ("ANIM_SAC_FULL", True, "inplace", "L0 fast aim run forward"),
    "RapidStrafeRight": ("ANIM_SAC_FULL", True, "inplace", "L0 fast run right (mirror -> left)"),
    "RapidStrafeRightRunForward": ("ANIM_SAC_FULL", True, "inplace", "L0 fast run fwd-right (mirror)"),
    "RapidStrafeRightRunBackwards": ("ANIM_SAC_FULL", True, "inplace", "L0 fast run back-right (mirror)"),
    "RapidStrafeRightWalkFowards": ("ANIM_SAC_FULL", True, "inplace", "L0 fast walk fwd-right (mirror); source spelling 'Fowards' kept"),
    "RapidStrafeRightWalkBackwards": ("ANIM_SAC_FULL", True, "inplace", "L0 fast walk back-right (mirror)"),
    "Falling": ("ANIM_SAC_FULL", True, "inplace", "falling loop (drops > 1.5 m)"),
    "FallLand": ("ANIM_SAC_FULL", False, "travel", "land from a fall (root drops 1.1 m: play from the apex)"),
    "FallLandRoll": ("ANIM_SAC_FULL", False, "turn", "land and roll (high drops)"),
    "IdleLeftTurn180": ("ANIM_SAC_FULL", False, "turn", "idle turn on the spot (mirror for right)"),
    "TurningLeft": ("ANIM_SAC_FULL", False, "turn", "180 deg turn in place, 2 s (mirror for right)"),
    "RapidTurningLeft": ("ANIM_SAC_FULL", False, "turn", "180 deg turn in place, 1.5 s (mirror for right)"),
    "WalkForwardTurnLeft180": ("ANIM_SAC_FULL", False, "turn", "walk and reverse (mirror for right)"),
    "RunForwardTurnLeft180": ("ANIM_SAC_FULL", False, "turn", "run and reverse (mirror for right)"),
    "TurningLeftWalkForward": ("ANIM_SAC_FULL", False, "turn", "start walking after a left turn (mirror)"),
    "TurningLeftRunForward": ("ANIM_SAC_FULL", False, "turn", "start running after a left turn (mirror)"),
    "StrafeRightTurnLeft": ("ANIM_SAC_FULL", False, "turn", "pivot out of a right strafe (mirror)"),
}

SAC_AVATAR = "Assets/_Game/Art/Animation/Clips/SAC/SK_SAC_DefaultMaleRig.fbx"


def sac_note(name, loop, root, use, scan):
    s = scan.get(f"m@{name}.fbx", {})
    fr = s.get("frames", [2, 0])
    rot = "Bake Into Pose (Body Orientation)" if root != "turn" else "NOT baked (the clip turns the body: apply root motion)"
    xz = "Bake Into Pose (in place: NavMeshAgent / CharacterController moves the body)" if root == "inplace" else \
        "NOT baked (enable Animator.applyRootMotion while it plays)"
    mirror = ""
    if "Right" in name or "Left" in name:
        other = name.replace("Right", "#").replace("Left", "Right").replace("#", "Left")
        mirror = (f" Mirror: add a SECOND clip entry for the same take named 'A_SAC_{other}' with Mirror ON "
                  "(ANIM_SAC_SUBSET variant mirror_left; Humanoid mirroring is exact for these symmetric clips).")
    return (f"Unity Standard Assets Characters clip (Unity Companion License: credit 'Unity Technologies', keep LICENSE). "
            f"Rig: Animation Type HUMANOID, Avatar Definition 'Copy From Other Avatar' = the avatar created from "
            f"{SAC_AVATAR} (import that rig first as Humanoid, Create From This Model; its default pose is a T-pose). "
            f"Take 'Take 001' -> rename to 'A_SAC_{name}', frames {fr[0]}-{fr[1]} at 30 fps "
            f"(measured: travel {s.get('travel_xy', 0)} m, yaw {s.get('yaw_deg', 0)} deg). "
            f"Loop Time {'ON, Loop Pose ON' if loop else 'OFF'}. Root Transform Rotation: {rot}. "
            f"Root Transform Position (Y): Bake Into Pose (based upon Original). Root Transform Position (XZ): {xz}.{mirror} "
            f"Anim compression Optimal (0.5). Foot IK ON. Use: {use}.")


def ual_rows(lib, src_dir, manifest, takes):
    rows = []
    loops = sorted(k for k in takes if k.endswith("_Loop"))
    once = sorted(k for k in takes if not k.endswith("_Loop") and k != "A_TPose")
    for suffix, rm in (("", False), ("_RM", True)):
        src = src_dir / f"{lib}_Standard{suffix}.fbx"
        dst = CLIPS / lib / f"A_{lib}_Library{suffix}.fbx"
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
        note = (f"Quaternius Universal Animation Library {lib[-1]} Standard (CC0; credit appreciated, 'Quaternius'), "
                f"{'ROOT-MOTION' if rm else 'IN-PLACE'} version, copied unchanged from {src.name}. Model tab: Scale Factor 1, "
                "Convert Units ON, **Bake Axis Conversion ON** (Quaternius' Unity setup). Rig: Animation Type HUMANOID, "
                "Avatar Definition Create From This Model (UE-style mannequin: pelvis, spine_01-03, neck_01, Head, clavicle/upperarm/"
                "lowerarm/hand_l/r, thigh/calf/foot/ball_l/r, finger bones), Skin Weights 4, Strip Bones ON. The mannequin mesh "
                "(13.7k tris) is never referenced (no renderer in any prefab), so it does not ship. Animation: one clip per take, "
                f"rename each to 'A_{lib}_<Take>{'_RM' if rm else ''}' (e.g. A_{lib}_{(loops or once)[0]}{'_RM' if rm else ''}); "
                "Loop Time + Loop Pose ON for every take ending in _Loop "
                f"({', '.join(loops)}), OFF for the others ({', '.join(once)}); skip A_TPose. "
                + ("Root Motion Node = 'Rig/root' (Quaternius setup), Root Transform Rotation / Y / XZ NOT baked: these takes move "
                   "the body (use with applyRootMotion for vaults, climbs, slides, knockbacks, rolls). " if rm else
                   "Root Transform Rotation, Y and XZ: Bake Into Pose (in place). ")
                + "Anim compression Optimal (0.5). Mirror OFF. Frame ranges per take: RawAssets/wp05/_work/ual_takes.json.")
        rows.append({"manifest_id": manifest, "file": str(dst.relative_to(READY)), "target_path": str(dst.relative_to(READY)),
                     "type": "animation_library_humanoid", "import_notes": note})
    return rows


def main():
    rows = []
    takes = json.loads((W5 / "_work/ual_takes.json").read_text())
    rows += ual_rows("UAL1", UAL1, "ANIM_UAL1", takes["UAL1_Standard.fbx"])
    rows += ual_rows("UAL2", UAL2, "ANIM_UAL2", takes["UAL2_Standard.fbx"])
    scan = json.loads((W5 / "_work/sac_scan.json").read_text())
    dst_dir = CLIPS / "SAC"
    dst_dir.mkdir(parents=True, exist_ok=True)
    rig = dst_dir / "SK_SAC_DefaultMaleRig.fbx"
    shutil.copy2(SAC_LOCAL / "defaultmale_rig.fbx", rig)
    rows.append({"manifest_id": "ANIM_SAC_SUBSET", "file": str(rig.relative_to(READY)), "target_path": str(rig.relative_to(READY)),
                 "type": "avatar_source_humanoid",
                 "import_notes": ("Avatar source for every A_SAC_* clip (Unity Standard Assets Characters 'DefaultMale' rig, Unity "
                                  "Companion License). Scale Factor 1, Convert Units ON. Rig: Animation Type HUMANOID, Avatar Definition "
                                  "Create From This Model (69-bone Mixamo-style 'Hips/Spine/...' skeleton, T-pose bind). Import Animation "
                                  "OFF, Materials None. Editor-only reference: the mesh (15k tris) must not be placed in any scene or "
                                  "prefab.")})
    for name, (manifest, loop, root, use) in SAC.items():
        src = SAC_LOCAL / f"m@{name}.fbx"
        if not src.exists():
            src = SAC_DL / f"m@{name}.fbx"
        if not src.exists():
            print("missing", name)
            continue
        dst = dst_dir / f"A_SAC_{name}.fbx"
        shutil.copy2(src, dst)
        rows.append({"manifest_id": manifest, "file": str(dst.relative_to(READY)), "target_path": str(dst.relative_to(READY)),
                     "type": "animation_clip_humanoid", "import_notes": sac_note(name, loop, root, use, scan)})
    out = W5 / "_work/index/clips_local.json"
    out.write_text(json.dumps(rows, indent=1))
    print(len(rows), "rows ->", out)


if __name__ == "__main__":
    main()
