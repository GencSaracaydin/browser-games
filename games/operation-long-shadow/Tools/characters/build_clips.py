#!/usr/bin/env python3
"""Bake and package the WP05 humanoid clip libraries (WP05).

  Tools/.venv/bin/python Tools/characters/build_clips.py meshy|rocketbox|hymotion|poses|local|all [--only NAME ...] [--no-preview]

meshy      : every Meshy clip in clips_spec.MESHY (owned research clips + bought sets), baked onto the Calder Rocketbox
             skeleton (bake_clip.py), finger pose baked in, + derived A_Meshy_ProneIdle.
rocketbox  : Rocketbox Biped clips (local + downloaded booth set), world rotations copied 1:1 onto the fixed hierarchy.
hymotion   : HY-Motion (fal-ai/hunyuan-motion) SMPL clips in RawAssets/wp05/ANIM_HYMOTION_SIGNALS/<name>_t*/.
poses      : finger pose clips A_Pose_* (pose_clips.py).
local      : copies Quaternius UAL1/UAL2 and Unity SAC files unchanged (their own Humanoid avatars).
Outputs: RawAssets/wp05/ready/Assets/_Game/Art/Animation/Clips/<Source>/A_<Source>_<Name>.fbx, preview sheets in
RawAssets/wp05/_review/clips_<source>_*.png, index rows in RawAssets/wp05/_work/index/clips_<source>.json.
"""
import argparse, json, math, pathlib, re, shutil, subprocess, sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import clips_spec as CS  # noqa: E402

ROOT = HERE.parents[1]
PY = str(ROOT / "Tools/.venv/bin/python")
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
W5 = ROOT / "RawAssets/wp05"
WORK = W5 / "_work/clips"
READY = W5 / "ready"
CLIPS = READY / "Assets/_Game/Art/Animation/Clips"
REVIEW = W5 / "_review"
TARGET = READY / "Assets/_Game/Art/Models/Characters/Calder/SK_Calder.fbx"
TEX = READY / "Assets/_Game/Art/Textures/Characters"
BODY_TEX = {"M_Calder_Body": {k.lower(): str(TEX / f"Calder/T_Calder_Body_{k}.png") for k in ("BC", "N", "M")},
            "M_Calder_Head": {k.lower(): str(TEX / f"Calder/T_Calder_Head_{k}.png") for k in ("BC", "N", "M")},
            "M_CHP_Boonie": {k.lower(): str(TEX / f"Props/CHP_Boonie/T_CHP_Boonie_{k}.png") for k in ("BC", "N", "M")},
            "M_CHP_ChestRig": {k.lower(): str(TEX / f"Props/CHP_ChestRig/T_CHP_ChestRig_{k}.png") for k in ("BC", "N", "M")}}
AVATAR_NOTE = ("Rig: Animation Type HUMANOID, Avatar Definition 'Copy From Other Avatar' = the avatar of "
               "Art/Models/Characters/Calder/SK_Calder.fbx (the clip is baked on that exact Rocketbox skeleton: same bone names "
               "and hierarchy, thighs under Bip01 Pelvis, clavicles under Bip01 Spine2). Do not use Create From This Model on clip files "
               "(armature-only, no T-pose). Import Materials off, no meshes.")


def run(cmd, tag):
    r = subprocess.run([str(c) for c in cmd], capture_output=True, text=True)
    if r.returncode != 0 or "Traceback" in r.stdout + r.stderr:
        print(r.stdout[-2500:], r.stderr[-2500:])
        raise SystemExit(f"{tag} failed")
    return r.stdout


def lib_name(i):
    return json.loads((W5 / "_downloads/meshy_docs/meshy_animation_library.json").read_text())[str(i)]["name"]


def meshy_sources():
    src = {}
    for sid, fn in CS.MESHY_OWNED.items():
        src[sid] = (ROOT / "RawAssets/fal3d/05_soldier_meshy_rig_multianim" / fn, "ANIM_MESHY_OWNED")
    for st in CS.MESHY_SETS:
        d = W5 / st / "take1"
        if not (d / "result.json").exists():
            continue
        res = json.loads((d / "result.json").read_text())
        files = list(d.glob("*.fbx"))
        for a in res.get("animations", []):
            tok = a["animation_fbx"]["url"].split("/")[-1]
            hit = [f for f in files if f.name.endswith(tok)]
            if hit and a["action_id"] not in src:
                src[a["action_id"]] = (hit[0], st)
    return src


def foot_events(metrics, loop):
    """Foot_L / Foot_R contact times (normalized) from the baked foot heights: local minima near the clip minimum."""
    rows = metrics["rows"]
    n = len(rows)
    if n < 8 or not loop:
        return {}
    ev = {}
    for key, name in (("lf", "Foot_L"), ("rf", "Foot_R")):
        z = [r[key] for r in rows]
        zmin, zmax = min(z), max(z)
        if zmax - zmin < 0.03:
            continue
        thr = zmin + 0.25 * (zmax - zmin)
        times = []
        for i in range(n):
            a, b, c = z[(i - 1) % n], z[i], z[(i + 1) % n]
            # landing: first frame of a contact phase (height drops under the threshold)
            if b <= thr and a > thr:
                times.append(round(i / (n - 1), 3))
        ev[name] = times
    return ev


def bake(src, mapname, out, name, fingers, root="full", frames=None, extra=None):
    out.parent.mkdir(parents=True, exist_ok=True)
    WORK.mkdir(parents=True, exist_ok=True)
    spec = {"target_fbx": str(TARGET), "source_fbx": str(src), "map": mapname, "out_fbx": str(out), "clip_name": name,
            "fingers": fingers or "rest", "root": root, "metrics_out": str(WORK / f"{name}_metrics.json")}
    if frames:
        spec["frames"] = frames
    if extra:
        spec.update(extra)
    sp = WORK / f"{name}_bake.json"
    sp.write_text(json.dumps(spec, indent=1))
    o = run([BLENDER, "-b", "--factory-startup", "-P", HERE / "blender/bake_clip.py", "--", sp], "bake " + name)
    return json.loads((WORK / f"{name}_metrics.json").read_text()), [l for l in o.splitlines() if l.startswith(("BAKED", "hip"))]


def preview(name, clip, rifle, loop, tag):
    out = WORK / "prev" / name
    out.parent.mkdir(parents=True, exist_ok=True)
    spec = {"body_fbx": str(TARGET), "textures": BODY_TEX, "clip_fbx": str(clip), "out": str(out), "res": [300, 400],
            "frames_frac": [0, 0.25, 0.5, 0.75] if loop else [0, 0.33, 0.66, 1.0], "views": [["q", 40, 8, 2.0, 0.85]],
            "rifle": bool(rifle)}
    sp = WORK / "prev" / f"{name}.json"
    sp.write_text(json.dumps(spec))
    run([BLENDER, "-b", "--factory-startup", "-P", HERE / "blender/clip_preview.py", "--", sp], "preview " + name)
    return json.loads((WORK / "prev" / f"{name}_files.json").read_text())


def sheet(rows, out):
    """rows: [(label, [png...])] -> one labelled contact sheet."""
    from PIL import Image, ImageDraw
    ims = [[Image.open(f).convert("RGB") for f in fs] for _, fs in rows]
    w, h = ims[0][0].size
    cols = max(len(r) for r in ims)
    S = Image.new("RGB", (w * cols, h * len(ims)), (20, 20, 20))
    for y, ((label, _), r) in enumerate(zip(rows, ims)):
        for x, im in enumerate(r):
            S.paste(im, (x * w, y * h))
        ImageDraw.Draw(S).text((4, y * h + 4), label, fill=(255, 255, 0))
    S.save(out)
    return out


def clip_note(name, loop, rm, use, events, frames, fps, extra=""):
    ev = "; ".join(f"{k} at {v}" for k, v in events.items() if v)
    return (f"{AVATAR_NOTE} Take: rename the single take to '{name}'. {frames} frames at {fps:.0f} fps. "
            f"Loop Time {'ON, Loop Pose ON' if loop else 'OFF'}. Root Transform Rotation: Bake Into Pose (Body Orientation). "
            f"Root Transform Position (Y): Bake Into Pose (based upon Feet). Root Transform Position (XZ): "
            + ("NOT baked: the clip travels; enable Animator.applyRootMotion while it plays (HumanoidBody action state)."
               if rm else "Bake Into Pose (in place; NavMeshAgent/CharacterController moves the body).")
            + " Mirror OFF (author a mirrored duplicate clip for the opposite side where noted). Anim compression Optimal, "
              "rotation/position error 0.5. Foot IK ON for locomotion. "
            + (f"Suggested AnimationEvents (normalized time): {ev}. " if ev else "")
            + f"Use: {use}. {extra}")


def do_meshy(a):
    src = meshy_sources()
    rows, review = [], []
    for aid, (nm, loop, root, rm, fingers, use) in CS.MESHY.items():
        if aid not in src:
            print("  missing source for", aid, nm)
            continue
        f, setid = src[aid]
        name = f"A_Meshy_{nm}"
        if a.only and name not in a.only:
            continue
        out = CLIPS / "Meshy" / f"{name}.fbx"
        m, log = bake(f, "meshy", out, name, fingers, root)
        ev = foot_events(m, loop and ("Walk" in nm or "Run" in nm or "Sprint" in nm or "Charge" in nm or "Crouch" in nm))
        print(" ", name, log[-1].split(" frames ")[-1] if log else "", "events", ev)
        manifest = CS.MESHY_MANIFEST.get(aid, setid)
        lib = lib_name(aid) if aid else "Idle"
        rows.append({"manifest_id": manifest, "file": str(out.relative_to(READY)), "target_path": str(out.relative_to(READY)),
                     "type": "animation_clip_humanoid",
                     "import_notes": clip_note(name, loop, rm, use, ev, len(m["rows"]), m["fps"],
                                               f"Source: Meshy animation library #{aid} {lib} (fal-ai/meshy/rigging/multi-animation), "
                                               f"finger pose '{fingers}' baked in.")})
        if not a.no_preview:
            review.append((name, preview(name, out, fingers == "RifleGrip", loop, "meshy")))
    # derived prone idle: the first pose of Prone_Reload held with a small breathing sway
    if 168 in src and (not a.only or "A_Meshy_ProneIdle" in a.only):
        name = "A_Meshy_ProneIdle"
        out = CLIPS / "Meshy" / f"{name}.fbx"
        m, log = bake(src[168][0], "meshy", out, name, "RifleGrip", "full", extra={"hold_frame": 1, "hold_seconds": 4.0,
                                                                                    "breath": 0.012})
        rows.append({"manifest_id": "ANIM_INSERTION_SET", "file": str(out.relative_to(READY)), "target_path": str(out.relative_to(READY)),
                     "type": "animation_clip_humanoid",
                     "import_notes": clip_note(name, True, False, "prone idle with rifle (INSERTION I-11 crest, Beat 1 prone)", {},
                                               len(m["rows"]), m["fps"], "Derived: frame 1 of Meshy #168 Prone_Reload held 4 s with a "
                                               "procedural 0.25 Hz breathing sway on the spine (bake_clip.py hold_frame).")})
        if not a.no_preview:
            review.append((name, preview(name, out, True, True, "meshy")))
    write_index("meshy", rows)
    if review:
        for k in range(0, len(review), 12):
            print(sheet(review[k:k + 12], REVIEW / f"clips_meshy_{k // 12}.png"))


def do_rocketbox(a):
    rows, review = [], []
    for stem, (nm, loop, rm, use, manifest) in CS.ROCKETBOX.items():
        f = None
        for d in (CS.RB_CLIPS_LOCAL, CS.RB_CLIPS_DL):
            p = d / f"{stem}.max.fbx"
            if p.exists():
                f = p
                break
        if f is None:
            print("  missing", stem)
            continue
        name = f"A_Rocketbox_{nm}"
        if a.only and name not in a.only:
            continue
        out = CLIPS / "Rocketbox" / f"{name}.fbx"
        m, log = bake(f, "rocketbox", out, name, None, "full")
        ev = foot_events(m, loop and ("walk" in stem or "run" in stem))
        print(" ", name, log[-1].split(" frames ")[-1] if log else "")
        rows.append({"manifest_id": manifest, "file": str(out.relative_to(READY)), "target_path": str(out.relative_to(READY)),
                     "type": "animation_clip_humanoid",
                     "import_notes": clip_note(name, loop, rm, use, ev, len(m["rows"]), m["fps"],
                                               f"Source: Microsoft Rocketbox {stem}.max.fbx (MIT, all_animations_max_motextr_static), world "
                                               "rotations copied 1:1 onto the fixed hierarchy (no FixRocketboxMaxImport needed). Fingers and "
                                               "face bones are animated by the source.")})
        if not a.no_preview:
            review.append((name, preview(name, out, False, loop, "rocketbox")))
    write_index("rocketbox", rows)
    for k in range(0, len(review), 12):
        print(sheet(review[k:k + 12], REVIEW / f"clips_rocketbox_{k // 12}.png"))


HY = {  # take folder stem -> (clip name, loop, root motion, use)
    "fist_hold": ("FistHold", False, False, "Hollis fist signal: halt (B5); L1 UpperBody"),
    "move_up": ("MoveUp", False, False, "'move up' arm signal (INSERTION I-9, follow mode); L1 UpperBody"),
    "crouch_run_cover": ("CrouchRunCover", False, True, "crouch-run to cover, kneel and raise the rifle (squad node arrival)"),
    "plant_charge": ("PlantCharge", False, False, "kneel and plant a charge (Tinker satchel B6)"),
    "check_watch": ("CheckWatch", False, False, "check wristwatch (MID-A watch check, squad idle)"),
    "haul_bag": ("HaulBag", False, False, "haul a bag into the helicopter (exfil)"),
    "one_finger": ("OneFinger", False, False, "seated: raise one finger 'One minute!' (INSERTION I-3, Hollis)"),
}


def do_hymotion(a):
    rows, review = [], []
    base = W5 / "ANIM_HYMOTION_SIGNALS"
    for stem, (nm, loop, rm, use) in HY.items():
        takes = sorted(base.glob(f"{stem}_t*"), key=lambda p: int(p.name.rsplit("_t", 1)[1]))
        takes = [t for t in takes if list(t.glob("*.fbx"))]
        if not takes:
            print("  missing", stem)
            continue
        if a.only and f"A_HYMotion_{nm}" not in a.only:
            continue
        pick = json.loads((base / "picks.json").read_text()).get(stem) if (base / "picks.json").exists() else None
        t = base / pick if pick else takes[-1]
        f = next(t.glob("*.fbx"))
        name = f"A_HYMotion_{nm}"
        out = CLIPS / "HYMotion" / f"{name}.fbx"
        m, log = bake(f, "smpl", out, name, None, "full")
        print(" ", name, t.name, log[-1].split(" frames ")[-1] if log else "")
        req = json.loads((t / "request.json").read_text())["arguments"]
        rows.append({"manifest_id": "ANIM_HYMOTION_SIGNALS", "file": str(out.relative_to(READY)), "target_path": str(out.relative_to(READY)),
                     "type": "animation_clip_humanoid",
                     "import_notes": clip_note(name, loop, rm, use, {}, len(m["rows"]), m["fps"],
                                               f"Source: fal-ai/hunyuan-motion take {t.name} (SMPL-H skeleton incl. fingers), prompt: "
                                               f"\"{req['prompt']}\" seed {req.get('seed')}. Baked onto the Rocketbox skeleton (fingers mapped).")})
        if not a.no_preview:
            review.append((name, preview(name, out, False, loop, "hymotion")))
    write_index("hymotion", rows)
    for k in range(0, len(review), 12):
        print(sheet(review[k:k + 12], REVIEW / f"clips_hymotion_{k // 12}.png"))


def do_poses(a):
    """A_Pose_* finger pose clips (ANIM_FINGER_POSES) on the Calder skeleton + hand close-up renders."""
    out = CLIPS / "Poses"
    rdir = WORK / "poses_prev"
    rdir.mkdir(parents=True, exist_ok=True)
    tj = WORK / "poses_tex.json"
    tj.write_text(json.dumps(BODY_TEX))
    o = run([BLENDER, "-b", "--factory-startup", "-P", HERE / "blender/pose_clips.py", "--", TARGET, out, rdir, tj], "poses")
    print("\n".join(l for l in o.splitlines() if l.startswith("POSE")))
    files = json.loads((rdir / "files.json").read_text())
    rows_r = []
    for clip in ("RifleGrip", "PistolGrip", "Fist", "Relaxed", "FlatHands"):
        rows_r.append((f"A_Pose_{clip}", [f for f in files if f"A_Pose_{clip}_" in f]))
    print(sheet(rows_r, REVIEW / "clips_poses_0.png"))
    uses = {"RifleGrip": "L3 hands layer while holding a rifle (right hand on the pistol grip, index along the guard; left hand "
                         "wrapped round the handguard)", "PistolGrip": "L3 hands layer with the pistol (Hush Puppy) / two-hand grip",
            "Fist": "fist signal, booth Clench (outro), melee", "Relaxed": "default open hands (unarmed, seated)",
            "FlatHands": "flat hands: booth hands on the table, push, signals"}
    rows = []
    for clip, use in uses.items():
        f = out / f"A_Pose_{clip}.fbx"
        rows.append({"manifest_id": "ANIM_FINGER_POSES", "file": str(f.relative_to(READY)), "target_path": str(f.relative_to(READY)),
                     "type": "animation_pose_humanoid",
                     "import_notes": (f"{AVATAR_NOTE} Take: rename to 'A_Pose_{clip}'. 2 identical frames at 30 fps (a static pose). "
                                      "Loop Time ON, Loop Pose ON. Root Transform Rotation / Y / XZ: Bake Into Pose. Mirror OFF. "
                                      "Anim compression OFF (keeps exact finger angles; the clip is tiny). Use ONLY on a layer with the "
                                      "hands mask AM_Hands (Humanoid mask: left and right hand + fingers, Hand IK off) or as a "
                                      "finger-only Humanoid mask: the body of this clip is the A-pose bind and must never reach the body. "
                                      "Authored offline with the same angles as FingerPoseAuthoring would set as Humanoid finger muscles "
                                      "(Tools/characters/blender/finger_poses.py: per finger proximal/middle/distal curl in degrees + thumb "
                                      f"opposition); FingerPoseAuthoring may overwrite it in-editor. Use: {use}.")})
    write_index("poses", rows)


def write_index(tag, rows):
    p = W5 / "_work/index" / f"clips_{tag}.json"
    p.parent.mkdir(parents=True, exist_ok=True)
    old = json.loads(p.read_text()) if p.exists() else []
    keep = {r["file"]: r for r in old}
    for r in rows:
        keep[r["file"]] = r
    p.write_text(json.dumps(list(keep.values()), indent=1))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("what", choices=["meshy", "rocketbox", "hymotion", "poses", "local", "all"])
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--no-preview", action="store_true")
    a = ap.parse_args()
    if a.what in ("meshy", "all"):
        do_meshy(a)
    if a.what in ("rocketbox", "all"):
        do_rocketbox(a)
    if a.what in ("hymotion", "all"):
        do_hymotion(a)
    if a.what in ("poses", "all"):
        do_poses(a)
    if a.what in ("local", "all"):
        subprocess.run([PY, str(HERE / "package_local_clips.py")], check=True)


if __name__ == "__main__":
    main()
