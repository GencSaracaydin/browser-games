#!/usr/bin/env python3
"""WP03 per-weapon build driver: reference prep -> Blender processing -> photo projection -> Unity textures -> review.

  Tools/.venv/bin/python Tools/mesh/weapons/wpn_build.py Tools/mesh/weapons/jobs/WPN_AK.json [--steps all|prep,blender,...]
      [--views left,right,front34,fp] [--samples 24]
Steps (in order; 'all' runs every one):
  prep     object masks / bboxes of the reference views (img_tools padref), muzzle-vs-butt end ratio, magazine mask
           (full ref vs no-magazine edit) -> <work>/prep.json and the Blender job <work>/job_blender.json
  blender  bl_weapon.py: clean, scale, orient, decimate, magazine fit, sockets, AO bake, FBX export into <work>/ready
  tex      glb_textures.py: generator PBR maps of the body and magazine GLBs -> <work>/tex_body, <work>/tex_mag
  project  bl_project.py: the reference photos projected onto the processed mesh (left/right views; magazine from
           the full reference) -> <work>/proj, <work>/proj_mag
  pack     tex_pack.py: T_WPN_<name>_{BC,N,M} (+ _Mag_) into <work>/ready/.../Textures/Weapons/<folder>
  review   review.py contact sheet -> RawAssets/wp03/_review/<name>[_<tag>].png
  install  copy this weapon's files from <work>/ready into RawAssets/wp03/ready (the import-ready tree)
Job keys used here on top of bl_weapon.py's: left_img (image the body was generated from, left side, muzzle left),
right_img (optional right view), full_img (reference with the magazine in, for the magazine mask/projection),
tex {grade_wood, grade_metal, project_metal, ao_strength, bc, n, m}, mag.tex {...}, review {views, fp_cam, tag},
proj (false to skip projection). Paths are relative to the project root.
"""
import argparse, json, pathlib, shutil, subprocess, sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
HERE = pathlib.Path(__file__).resolve().parent
PY = str(ROOT / "Tools/.venv/bin/python")
BL = "/Applications/Blender.app/Contents/MacOS/Blender"
STEPS = ["prep", "blender", "tex", "project", "pack", "review", "install"]


def P(p):
    p = pathlib.Path(p)
    return p if p.is_absolute() else ROOT / p


def run(cmd, tag, log=None):
    print(">>", tag, flush=True)
    r = subprocess.run([str(c) for c in cmd], capture_output=True, text=True)
    out = r.stdout + r.stderr
    if log:
        pathlib.Path(log).write_text(out)
    if r.returncode != 0:
        print(out[-4000:])
        sys.exit(f"step {tag} failed")
    return out


def first_png(p):
    p = P(p)
    if p.is_dir():
        c = sorted(x for x in p.iterdir() if x.suffix.lower() in (".png", ".jpg") and not x.name.startswith("01_"))
        return c[0]
    return p


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("job")
    ap.add_argument("--steps", default="all")
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--views")
    ap.add_argument("--tag")
    ap.add_argument("--hdri", help="review lighting: 'desert' (goegap, golden-hour sun) or an HDRI path; default studio")
    a = ap.parse_args()
    J = json.loads(P(a.job).read_text())
    steps = STEPS if a.steps == "all" else a.steps.split(",")
    work = P(J["work"])
    work.mkdir(parents=True, exist_ok=True)
    ready = work / "ready"
    name, folder = J["name"], J["folder"]
    tdir = ready / "Assets/_Game/Art/Textures/Weapons" / folder
    mdir = ready / "Assets/_Game/Art/Models/Weapons" / folder
    IT = HERE / "img_tools.py"
    prep_p = work / "prep.json"
    prep = json.loads(prep_p.read_text()) if prep_p.exists() else {}

    if "prep" in steps:
        prep = {}
        for side in ("left", "right", "full"):
            k = f"{side}_img"
            if not J.get(k):
                continue
            src = first_png(J[k])
            out = run([PY, IT, "padref", src, work / f"proj_{side}_pad.png", work / f"proj_{side}_mask.png", J.get("mask_core", 100)], f"padref {side}")
            bb = json.loads(out.split("padref", 1)[1].split("->")[0].strip())
            prep[side] = {"src": str(src), "bbox": bb}
        if J.get("mirror_right") and "left" in prep and "right" not in prep:
            # (near-)symmetric objects without a right view: project the mirrored left photo onto the right side
            from PIL import Image, ImageOps
            for kind in ("pad", "mask"):
                ImageOps.mirror(Image.open(work / f"proj_left_{kind}.png")).save(work / f"proj_right_{kind}.png")
            Wd = Image.open(work / "proj_left_mask.png").size[0]
            x0, y0, x1, y1 = prep["left"]["bbox"]
            prep["right"] = {"src": "mirror of left", "bbox": [Wd - 1 - x1, y0, Wd - 1 - x0, y1]}
        out = run([PY, IT, "endratio", first_png(J["left_img"]), J.get("mask_core", 100)], "endratio")
        prep["end_ratio"] = float(out.split()[0])
        mj = J.get("mag")
        if mj and mj.get("fit", {}).get("mode") == "mask" and J.get("full_img"):
            run([PY, IT, "magmask", first_png(J["full_img"]), first_png(J["left_img"]), work / "magmask.json", work / "magmask_dbg.png"], "magmask")
            mm = json.loads((work / "magmask.json").read_text())
            prep["magmask"] = mm
        prep_p.write_text(json.dumps(prep, indent=1))

    bj = dict(J)
    bj["ready"] = str(ready)
    if "weapon_bbox_img" not in J and "left" in prep:
        bj["weapon_bbox_img"] = prep["left"]["bbox"]
    if "img_end_ratio" not in J and "end_ratio" in prep:
        bj["img_end_ratio"] = prep["end_ratio"]
    if bj.get("mag") and bj["mag"].get("fit", {}).get("mode") == "mask" and "magmask" in prep:
        f = dict(bj["mag"]["fit"])
        mm = prep["magmask"]
        f.setdefault("mask_bbox", mm["mag_bbox"])
        f.setdefault("axis_down_img", mm["mag_axis_down_img"])
        f.setdefault("mask_png", str(work / "magmask_mask.png"))
        bj["mag"] = dict(bj["mag"], fit=f)
    (work / "job_blender.json").write_text(json.dumps(bj, indent=1))

    if "blender" in steps:
        if ready.exists():
            shutil.rmtree(ready)
        (work / "tex_out/n_bake_body.png").unlink(missing_ok=True)
        out = run([BL, "-b", "--factory-startup", "--python-exit-code", "1", "-t", "3", "-P", HERE / "bl_weapon.py", "--", work / "job_blender.json"], "blender", work / "blender.log")
        print("\n".join(l for l in out.splitlines() if l.startswith("[")) [-3000:])

    if "tex" in steps:
        run([PY, HERE / "glb_textures.py", P(J["body"]), work / "tex_body"], "tex body")
        if J.get("mag"):
            run([PY, HERE / "glb_textures.py", P(J["mag"]["glb"]), work / "tex_mag"], "tex mag")

    fp = bool(J.get("fp"))
    model = f"SM_WPN_{name}_FP" if fp else f"SM_WPN_{name}_W"
    if "project" in steps and J.get("proj", True):
        views = []
        for side in ("left", "right"):
            if side in prep:
                views.append({"side": side, "image": str(work / f"proj_{side}_pad.png"), "mask": str(work / f"proj_{side}_mask.png"),
                              "bbox": prep[side]["bbox"]})
        rep = json.loads((work / "process_report.json").read_text()) if (work / "process_report.json").exists() else {}
        if rep.get("cross_k"):
            for vw in views:
                vw["kz"] = 1.0 / rep["cross_k"]
        pj = {"fbx": str(mdir / f"{model}.fbx"), "object": model if fp else [f"SM_WPN_{name}_W_LOD0"] + [sp["name"] for sp in J.get("split", [])], "size": 2048 if fp else 1024,
              "vis_size": 2048 if fp else 1024, "out": str(work / "proj"), "views": views}
        if J.get("demark"):
            pj["demark"] = J["demark"]
        if J.get("mag") and fp and "full" in prep and J["mag"].get("project", True):
            pj["extra"] = [{"object": "Mag", "size": J["mag"].get("tex", 512), "out": str(work / "proj_mag"), "occluders": [model],
                            "views": [{"side": "left", "image": str(work / "proj_full_pad.png")}]}]
            if J["mag"].get("demark"):
                pj["extra"][0]["demark"] = J["mag"]["demark"]
        (work / "project.json").write_text(json.dumps(pj, indent=1))
        out = run([BL, "-b", "--factory-startup", "--python-exit-code", "1", "-t", "3", "-P", HERE / "bl_project.py", "--", work / "project.json"], "project", work / "project.log")
        print("\n".join(l for l in out.splitlines() if l.startswith("view")))

    if "pack" in steps:
        t = J.get("tex", {})
        size_bc = t.get("bc", 2048 if fp else 1024)
        cmd = [PY, HERE / "tex_pack.py", "--src", work / "tex_body", "--ao", work / "tex_out/ao_body.png", "--out", tdir,
               "--name", f"T_WPN_{name}", "--bc", size_bc, "--n", t.get("n", size_bc), "--m", t.get("m", 1024 if fp else size_bc),
               "--ao-strength", t.get("ao_strength", 1.0), "--match", t.get("match", "metal")]
        if J.get("proj", True) and (work / "proj/proj_left_color.png").exists():
            cmd += ["--project", work / "proj", "--project-metal", t.get("project_metal", 0.6)]
            for k in ("project_lo", "project_hi"):
                if k in t:
                    cmd += [f"--{k.replace('_', '-')}", t[k]]
        if not fp and (work / "tex_out/n_bake_body.png").exists():
            cmd += ["--normal", work / "tex_out/n_bake_body.png"]
        if t.get("grade_wood"):
            cmd += ["--grade-wood", t["grade_wood"]]
        if t.get("grade_metal"):
            cmd += ["--grade-metal", t["grade_metal"]]
        if t.get("smooth_max"):
            cmd += ["--smooth-max", t["smooth_max"]]
        if t.get("dehighlight"):
            cmd += ["--dehighlight", t["dehighlight"]]
        if t.get("recolor"):
            cmd += ["--recolor", json.dumps(t["recolor"])]
        if J.get("demark") and (work / "proj/demark.png").exists():
            cmd += ["--demark", work / "proj/demark.png", "--demark-sigma", t.get("demark_sigma", 7.0)]
        print(run(cmd, "pack body").strip())
        if J.get("mag"):
            mt = J["mag"].get("texopts", {})
            ms = J["mag"].get("tex", 512)
            cmd = [PY, HERE / "tex_pack.py", "--src", work / "tex_mag", "--ao", work / "tex_out/ao_mag.png", "--out", tdir,
                   "--name", f"T_WPN_{name}_Mag", "--bc", ms, "--n", ms, "--m", ms, "--ao-strength", mt.get("ao_strength", 1.0)]
            if (work / "proj_mag/proj_left_color.png").exists() and J["mag"].get("project", True):
                cmd += ["--project", work / "proj_mag", "--project-metal", mt.get("project_metal", 0.6)]
            if mt.get("grade_metal"):
                cmd += ["--grade-metal", mt["grade_metal"]]
            if mt.get("grade_wood"):
                cmd += ["--grade-wood", mt["grade_wood"]]
            if J["mag"].get("demark") and (work / "proj_mag/demark.png").exists():
                cmd += ["--demark", work / "proj_mag/demark.png", "--demark-sigma", mt.get("demark_sigma", 16.0)]
            print(run(cmd, "pack mag").strip())
        # optional extra skins (e.g. smoke grenade colour bands): {"suffix": "Green", "recolor": {...}}
        for sk in t.get("skins", []):
            cmd = [PY, HERE / "tex_pack.py", "--src", work / "tex_body", "--ao", work / "tex_out/ao_body.png", "--out", work / f"skin_{sk['suffix']}",
                   "--name", f"T_WPN_{name}_{sk['suffix']}", "--bc", size_bc, "--n", size_bc, "--m", t.get("m", size_bc),
                   "--recolor", json.dumps(sk["recolor"])]
            if J.get("proj", True) and (work / "proj/proj_left_color.png").exists():
                cmd += ["--project", work / "proj", "--project-metal", t.get("project_metal", 0.6)]
            print(run(cmd, f"skin {sk['suffix']}").strip())
            shutil.copy(work / f"skin_{sk['suffix']}" / f"T_WPN_{name}_{sk['suffix']}_BC.png", tdir / f"T_WPN_{name}_{sk['suffix']}_BC.png")

    if "review" in steps:
        rv = J.get("review", {})
        tag = a.tag or rv.get("tag", "")
        out = ROOT / f"RawAssets/wp03/_review/{name}{('_' + tag) if tag else ''}.png"
        cmd = [PY, HERE / "review.py", folder, "--ready", ready, "--model", model, "--samples", a.samples, "--out", out,
               "--views", a.views or rv.get("views", "left,right,front34,fp" if fp else "left,right,front34,rear34")]
        if rv.get("fp_cam"):
            cmd += ["--fp-cam", rv["fp_cam"]]
        if a.hdri or rv.get("hdri"):
            cmd += ["--hdri", a.hdri or rv["hdri"]]
        print(run(cmd, "review").strip())

    if "install" in steps:
        dst = ROOT / "RawAssets/wp03/ready"
        n = 0
        for f in ready.rglob("*"):
            if f.is_file():
                d = dst / f.relative_to(ready)
                d.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(f, d)
                n += 1
        rep = json.loads((work / "process_report.json").read_text())
        (ROOT / "RawAssets/wp03/_reports").mkdir(exist_ok=True)
        shutil.copy2(work / "process_report.json", ROOT / f"RawAssets/wp03/_reports/{J['id']}.json")
        print("installed", n, "files; outputs", rep.get("outputs"))


if __name__ == "__main__":
    main()
