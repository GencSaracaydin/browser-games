#!/usr/bin/env python3
"""Build import-ready Rocketbox bodies (WP05): 1984 retexture masters -> fixed Humanoid FBX with fitted props ->
budget textures -> preview sheets -> import_index rows.

  Tools/.venv/bin/python Tools/characters/build_bodies.py                 # every body in bodies_spec.BODIES
  Tools/.venv/bin/python Tools/characters/build_bodies.py Calder Hollis   # some
  options: --retex (redo the retexture masters), --no-export, --no-preview, --only-preview
Outputs:
  RawAssets/wp05/ready/Assets/_Game/Art/Models/Characters/<Folder>/<Mesh>.fbx
  RawAssets/wp05/ready/Assets/_Game/Art/Textures/Characters/<Folder>/T_<Variant>_<Part>_{BC,N,M}.png
  RawAssets/wp05/ready/Assets/_Game/Art/Textures/Characters/Props/<Prop>/T_<Prop>_{BC,N,M}.png (512)
  RawAssets/wp05/_review/<Name>_sheet.png      preview sheet (front, side, back, 3/4, head close-up)
  RawAssets/wp05/_work/index/<Name>.json       import_index rows (merged by Tools/characters/make_index.py)
"""
import argparse, json, pathlib, subprocess, sys
import numpy as np
from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from bodies_spec import BODIES, PROPS, src_fbx  # noqa: E402

ROOT = HERE.parents[1]
PY = str(ROOT / "Tools/.venv/bin/python")
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
W5 = ROOT / "RawAssets/wp05"
WORK = W5 / "_work"
READY = W5 / "ready"
REVIEW = W5 / "_review"
MODELS = "Assets/_Game/Art/Models/Characters"
TEXS = "Assets/_Game/Art/Textures/Characters"


def run(cmd, tag):
    r = subprocess.run([str(c) for c in cmd], capture_output=True, text=True)
    if r.returncode != 0 or "Traceback" in r.stdout + r.stderr:
        print(r.stdout[-3000:], r.stderr[-3000:])
        raise SystemExit(f"{tag} failed")
    return r.stdout


def blender(script, spec_path, tag):
    out = run([BLENDER, "-b", "--factory-startup", "-P", HERE / "blender" / script, "--", spec_path], tag)
    return "\n".join(l for l in out.splitlines() if l.startswith(("EXPORTED", "HEAD", "STRAP", "RESTRAINTS", "RENDERED", "BAKED", "SLIMMED")))


def sunglasses_textures(wd, name="CHP_Sunglasses", size=256):
    """atlas for make_sunglasses.py: u < 0.5 brushed gold-tone wire frame (metal), u >= 0.5 G-15 style grey-green lens."""
    rng = np.random.default_rng(5)
    H = W = size
    xx = np.mgrid[0:H, 0:W][1] / W
    metal = xx < 0.5
    bc = np.zeros((H, W, 3))
    n1 = np.asarray(Image.fromarray((rng.random((H, 8)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)) / 255.0
    bc[metal] = np.array([0.62, 0.52, 0.33]) * (0.85 + 0.25 * n1[metal, None])      # worn brass/gold plating
    bc[~metal] = np.array([0.055, 0.065, 0.055])                                       # G-15: dark grey-green
    m = np.zeros((H, W, 4))
    m[..., 1] = 1.0
    m[metal, 0] = 1.0
    m[metal, 3] = 0.62 + 0.12 * n1[metal]
    m[~metal, 0] = 0.0
    m[~metal, 3] = 0.93
    wd.mkdir(parents=True, exist_ok=True)
    Image.fromarray((bc.clip(0, 1) * 255).astype(np.uint8)).save(wd / f"T_{name}_BC.png")
    Image.fromarray(np.full((H, W, 3), (128, 128, 255), np.uint8)).save(wd / f"T_{name}_N.png")
    Image.fromarray((m * 255).astype(np.uint8), "RGBA").save(wd / f"T_{name}_M.png")


def prop_file(p):
    f = pathlib.Path(p["file"])
    if f.is_dir():
        f = next(f.glob("*_model.glb"))
    return f


def resize_tex(src, dst, size, kind):
    im = Image.open(src)
    dst.parent.mkdir(parents=True, exist_ok=True)
    if kind == "N":
        a = np.asarray(im.convert("RGB").resize((size, size), Image.LANCZOS)).astype(np.float32) / 127.5 - 1
        a /= np.linalg.norm(a, axis=2, keepdims=True) + 1e-6
        Image.fromarray(((a + 1) * 127.5 + 0.5).clip(0, 255).astype(np.uint8)).save(dst, optimize=True)
    elif kind == "M":
        im.convert("RGBA").resize((size, size), Image.LANCZOS).save(dst, optimize=True)
    else:
        mode = "RGBA" if im.mode in ("RGBA", "LA") else "RGB"
        im.convert(mode).resize((size, size), Image.LANCZOS).save(dst, optimize=True)


def restraint_textures(dst_dir, size=256):
    """Procedural 256 atlas: leather (u 0-0.5), steel buckle (0.5-0.75, v<0.5), tape (0.75-1, v<0.5), electrode (0.75-1, v>0.5)."""
    rng = np.random.default_rng(7)
    H = W = size
    yy, xx = np.mgrid[0:H, 0:W] / size
    v = 1 - yy
    n1 = np.asarray(Image.fromarray((rng.random((64, 64)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)) / 255.0
    n2 = rng.random((H, W))
    bc = np.zeros((H, W, 3))
    sm = np.zeros((H, W))
    met = np.zeros((H, W))
    hgt = np.zeros((H, W))
    leather = xx < 0.5
    bc[leather] = (np.array([0.20, 0.12, 0.07]) * (0.75 + 0.35 * n1[leather, None]) * (0.9 + 0.2 * n2[leather, None]))
    edge = leather & ((np.abs(v - 0.12) < 0.012) | (np.abs(v - 0.88) < 0.012)) & ((xx * 64) % 1 < 0.6)
    bc[edge] = [0.32, 0.25, 0.17]
    sm[leather] = 0.38 + 0.1 * n1[leather]
    hgt[leather] = n2[leather] * 0.3 + n1[leather] * 0.5
    hgt[edge] -= 0.6
    steel = (xx >= 0.5) & (xx < 0.75)
    bc[steel] = np.array([0.42, 0.42, 0.43])[None] * (0.8 + 0.3 * n1[steel, None])
    met[steel] = 1.0
    sm[steel] = 0.55
    tape = (xx >= 0.75) & (v < 0.5)
    bc[tape] = np.array([0.80, 0.78, 0.70])[None] * (0.92 + 0.1 * n2[tape, None])
    sm[tape] = 0.18
    hgt[tape] = n2[tape] * 0.2
    elec = (xx >= 0.75) & (v >= 0.5)
    r = np.hypot(xx - 0.875, v - 0.75)
    bc[elec] = np.where((r[elec] < 0.05)[:, None], [0.28, 0.28, 0.29], [0.62, 0.62, 0.63])
    met[elec] = np.where(r[elec] < 0.05, 0.0, 0.8)
    sm[elec] = 0.5
    gy, gx = np.gradient(hgt * 4)
    nrm = np.dstack([-gx, gy, np.ones_like(gx)])
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    dst_dir.mkdir(parents=True, exist_ok=True)
    Image.fromarray((bc.clip(0, 1) ** (1 / 2.2) * 255).astype(np.uint8)).save(dst_dir / "T_Calder_Restraints_BC.png")
    Image.fromarray(((nrm + 1) * 127.5).astype(np.uint8)).save(dst_dir / "T_Calder_Restraints_N.png")
    m = np.dstack([met, np.ones_like(met), np.zeros_like(met), sm])
    Image.fromarray((m * 255).astype(np.uint8), "RGBA").save(dst_dir / "T_Calder_Restraints_M.png")


def tex_rows(manifest, files, size, part):
    rows = []
    for f in files:
        rel = str(f.relative_to(READY))
        k = f.stem.rsplit("_", 1)[1]
        if k == "BC":
            note = f"Texture Type Default, sRGB ON, max size {size}, Crunch q70 (heads q80), mipmaps on." + (
                " RGBA: alpha = hair-card opacity -> material Alpha Clipping 0.35, Render Face Both." if part == "Hair" else "")
        elif k == "N":
            note = f"Texture Type Normal map (OpenGL / +Y up, Blender and Unity convention, no green flip), max size {size}."
        else:
            note = (f"Mask, sRGB OFF, max size {size}: R metallic, G occlusion, B unused, A smoothness. URP/Lit (Metallic workflow): "
                    "assign to Metallic Map AND Occlusion Map (TECH_PLAN D4), Smoothness source = Metallic Alpha, smoothness scale 1.")
        rows.append({"manifest_id": manifest, "file": rel, "target_path": rel, "type": "texture", "import_notes": note})
    return rows


def build(name, a):
    b = BODIES[name]
    avatar = b["avatar"]
    rows = []
    # 1) retexture masters
    variants = sorted({v for (_, v, _, _) in b["mats"].values()})
    for v in variants:
        need = [WORK / v / "master" / f"T_{v}_{part}_BC.png" for (_, vv, part, _) in b["mats"].values() if vv == v]
        if a.retex or not all(p.exists() for p in need):
            print("  retexture", v)
            run([PY, HERE / "retexture_rocketbox.py", v], "retexture " + v)
    # 2) prop textures (1024 work copies from the Tripo GLB, 512 in game)
    prop_tex = {}
    for key, _ in b["props"]:
        p = PROPS[key]
        if not p.get("tex"):
            continue
        wd = WORK / "props" / p["tex"]
        if not (wd / f"T_{p['tex']}_BC.png").exists():
            if p.get("procedural") == "sunglasses":
                sunglasses_textures(wd, p["tex"])
            else:
                run([PY, HERE / "prop_textures.py", prop_file(p), wd, p["tex"], "--size", "1024"], "prop tex " + key)
        td = READY / TEXS / "Props" / p["tex"]
        files = []
        for k in ("BC", "N", "M"):
            dst = td / f"T_{p['tex']}_{k}.png"
            resize_tex(wd / f"T_{p['tex']}_{k}.png", dst, p.get("tex_size", 512), k)
            files.append(dst)
        prop_tex["M_" + p["tex"]] = {k.lower(): str(td / f"T_{p['tex']}_{k}.png") for k in ("BC", "N", "M")}
        rows += tex_rows(p["id"], files, p.get("tex_size", 512), "Prop")
    # 3) textures to budget
    tex_map = {}
    for smat, (gmat, v, part, size) in b["mats"].items():
        td = READY / TEXS / b["folder"]
        files = []
        for k in ("BC", "N", "M"):
            src = WORK / v / "master" / f"T_{v}_{part}_{k}.png"
            dst = td / f"T_{v}_{part}_{k}.png"
            resize_tex(src, dst, size, k)
            files.append(dst)
        tex_map[gmat] = {"bc": str(files[0]), "n": str(files[1]), "m": str(files[2]), "alpha": part == "Hair"}
        rows += tex_rows(b["manifest"], files, size, part)
    if b.get("extra", {}).get("cuffs"):
        td = READY / TEXS / b["folder"]
        restraint_textures(td)
        files = [td / f"T_Calder_Restraints_{k}.png" for k in ("BC", "N", "M")]
        tex_map["M_Calder_Restraints"] = {"bc": str(files[0]), "n": str(files[1]), "m": str(files[2])}
        rows += tex_rows(b["manifest"], files, 256, "Restraints")
    tex_map.update(prop_tex)
    if any(k == "prc77" for k, _ in b["props"]):
        t10 = ROOT / "RawAssets/wp10/ready/Assets/_Game/Art/Textures/Props/prc77"
        tex_map["M_PRC77"] = {"bc": str(t10 / "T_PRC77_BC.png"), "n": str(t10 / "T_PRC77_N.png"), "m": str(t10 / "T_PRC77_M.png")}
    # 4) export
    fbx = READY / MODELS / b["folder"] / f"{b['mesh']}.fbx"
    mat_names = {s: g for s, (g, _, _, _) in b["mats"].items()}
    spec = {"src_fbx": str(src_fbx(avatar)), "dump": str(WORK / avatar / "dump.json"), "out_fbx": str(fbx), "mesh_name": b["mesh"],
            "material_names": mat_names, "delete_materials": b.get("delete_materials", []), "delete_islands": b.get("delete_islands", []),
            "props": [], "tpose": b.get("extra", {}).get("tpose", True)}
    for k, v in (b.get("extra") or {}).items():
        if k == "keep_faces":
            spec["keep_faces"] = v
        if k == "cuffs":
            spec["restraints"] = True
        if k == "slim_vest":
            spec["slim_vest"] = v
    for key, over in b["props"]:
        p = PROPS[key]
        fit = dict(p.get("fit") or {})
        fit.update(over)
        for t in fit.get("trim", []):
            t["material"] = mat_names.get(t["material"], t["material"])
        suffix = "".join(w.capitalize() for w in key.split("_")) if key != "nvg" else "NVG"
        if key == "prc77":
            suffix = "PRC77"
        ps = {"file": str(prop_file(p)), "name": f"{b['mesh']}_{suffix}", "material": p.get("material") or "M_" + p["tex"],
              "bone": p["bone"]}
        if fit:
            ps["fit"] = fit
        if p.get("decimate"):
            ps["decimate"] = p["decimate"]
        spec["props"].append(ps)
    sp = WORK / "specs" / f"export_{name}.json"
    sp.parent.mkdir(parents=True, exist_ok=True)
    sp.write_text(json.dumps(spec, indent=1))
    if not a.no_export and not a.only_preview:
        print(" ", blender("rb_export.py", sp, "export " + name).replace("\n", "\n  "))
    # 5) preview sheet
    if not a.no_preview:
        REVIEW.mkdir(parents=True, exist_ok=True)
        out = WORK / "previews" / name
        out.mkdir(parents=True, exist_ok=True)
        hz = 1.62 if "Sparrow" in name else 1.66
        rs = {"fbx": str(fbx), "out": str(out / "prev.png"), "textures": tex_map, "res": [520, 780],
              "views": [["front", 0, 4, 2.05, 0.95], ["side", 90, 4, 2.05, 0.95], ["back", 180, 4, 2.05, 0.95],
                        ["q3", 35, 8, 2.05, 0.95], ["head", 25, 4, 0.55, hz]]}
        if "tex_override" in b:
            rs["textures"].update(b["tex_override"])
        rp = out / "render.json"
        rp.write_text(json.dumps(rs, indent=1))
        blender("rb_render.py", rp, "render " + name)
        files = json.loads((out / "prev_files.json").read_text())
        run([PY, HERE / "sheet.py", REVIEW / f"{name}_sheet.png", str(len(files))] + files, "sheet")
    # 6) index rows for the model
    props_txt = ", ".join(f"{b['mesh']}_{s['name'].rsplit('_', 1)[1]} ({s['material']})" for s in spec["props"]) or "none"
    mats_txt = "; ".join(f"{g} -> T_{v}_{part}_*" for (g, v, part, _) in b["mats"].values())
    notes = ("Skinned character FBX (Rocketbox, MIT). Scale Factor 1, Convert Units ON (file is metres), faces +Z, Y up, no root rotation. "
             "Rig: Animation Type HUMANOID, Avatar Definition: Create From This Model. The hierarchy is already fixed offline "
             "(thighs under Bip01 Pelvis, clavicles under Bip01 Spine2 = FixRocketboxMaxImport usingMixamoAnimations=false), and the "
             "file's default node pose is a T-pose (arms horizontal, palms down) while the skin keeps the A-pose bind, so the avatar "
             "needs no Enforce T-Pose and RocketboxImporter must NOT re-parent or rotate bones again. Bone map: Hips=Bip01 Pelvis, "
             "Spine=Bip01 Spine, Chest=Bip01 Spine1, UpperChest=Bip01 Spine2, Neck=Bip01 Neck, Head=Bip01 Head, Jaw=Bip01 MJaw, "
             "LeftEye/RightEye=Bip01 LEye/REye, shoulders=Bip01 L/R Clavicle, arms=UpperArm/Forearm/Hand, legs=Thigh/Calf/Foot/Toe0, "
             "fingers Finger0x thumb .. Finger4x little (auto-map gets all of these; check Jaw is not mapped to a face bone). "
             "Import Animation OFF (no clips in the file). Materials: Material Creation Mode None; build URP/Lit per slot: "
             f"{mats_txt}. Props are separate skinned meshes in the same file: {props_txt}; their textures live in "
             "Art/Textures/Characters/Props/<Prop>/. Optimize Game Objects ON with exposed transforms Bip01 R Hand, Bip01 L Hand, "
             "Bip01 Head, Bip01 Spine2 (weapon/prop sockets). Skin weights 4 bones. Normals Import, Tangents Calculate Mikk. "
             "Mesh compression Off. Blend shapes none. " + b.get("note", ""))
    rows.insert(0, {"manifest_id": b["manifest"], "file": str(fbx.relative_to(READY)), "target_path": str(fbx.relative_to(READY)),
                    "type": "model_skinned_humanoid", "import_notes": notes})
    (WORK / "index").mkdir(parents=True, exist_ok=True)
    (WORK / "index" / f"body_{name}.json").write_text(json.dumps(rows, indent=1))


def build_reel():
    """CHR_CALDER variant reel_shaved_head: T_CalderReel_Head_* (shaved, gaunt 'Session 11' head) for the booth body's
    M_Calder_Head slot (material swap; same UVs). Preview: SK_Calder_Booth with the reel head."""
    v = "CalderReel"
    if not (WORK / v / "master" / f"T_{v}_Head_BC.png").exists():
        run([PY, HERE / "retexture_rocketbox.py", v], "retexture " + v)
    td = READY / TEXS / "Calder"
    files = []
    for k in ("BC", "N", "M"):
        dst = td / f"T_{v}_Head_{k}.png"
        resize_tex(WORK / v / "master" / f"T_{v}_Head_{k}.png", dst, 1024, k)
        files.append(dst)
    rows = tex_rows("CHR_CALDER", files, 1024, "Head")
    for r in rows:
        r["import_notes"] += (" Variant reel_shaved_head (MID-B 'Session 11' film): create material M_CalderReel_Head (URP/Lit, same "
                              "settings as M_Calder_Head) and swap it onto the M_Calder_Head slot of SK_Calder_Booth for booth shots 4-6.")
    out = WORK / "previews" / "CalderReel"
    out.mkdir(parents=True, exist_ok=True)
    tb = READY / TEXS / "Calder"
    tex = {"M_CalderBooth_Body": {k.lower(): str(tb / f"T_CalderBooth_Body_{k}.png") for k in ("BC", "N", "M")},
           "M_Calder_Head": {k.lower(): str(tb / f"T_CalderReel_Head_{k}.png") for k in ("BC", "N", "M")},
           "M_Calder_Restraints": {k.lower(): str(tb / f"T_Calder_Restraints_{k}.png") for k in ("BC", "N", "M")}}
    rs = {"fbx": str(READY / MODELS / "Calder/SK_Calder_Booth.fbx"), "out": str(out / "prev.png"), "textures": tex, "res": [520, 780],
          "views": [["front", 0, 4, 2.05, 0.95], ["q3", 35, 8, 2.05, 0.95], ["head", 25, 4, 0.55, 1.66], ["headside", 80, 4, 0.55, 1.66]]}
    rp = out / "render.json"
    rp.write_text(json.dumps(rs, indent=1))
    blender("rb_render.py", rp, "render reel")
    pf = json.loads((out / "prev_files.json").read_text())
    run([PY, HERE / "sheet.py", REVIEW / "CalderReel_sheet.png", str(len(pf))] + pf, "sheet")
    (WORK / "index").mkdir(parents=True, exist_ok=True)
    (WORK / "index" / "body_CalderReel.json").write_text(json.dumps(rows, indent=1))


PROP_FOLDERS = {"boonie": "boonie", "ballcap": "ballcap", "beret": "beret", "aviator_goggles": "aviator_goggles",
                "helmet_goggles": "helmet_goggles", "nvg": "nvg", "chest_rig": "chest_rig", "armor_vest": "armor_vest",
                "flight_helmet": "flight_helmet", "scarf": "scarf", "sunglasses": "sunglasses"}


def build_props():
    """Loose CHP_* meshes (static FBX) + their 512 textures + index rows."""
    specs, rows = [], []
    for key, folder in PROP_FOLDERS.items():
        p = PROPS[key]
        fit = dict(p.get("fit") or {})
        fit.update(p.get("loose", {}))
        name = "SM_" + p["tex"]
        out = READY / MODELS / "Props" / folder / f"{name}.fbx"
        out.parent.mkdir(parents=True, exist_ok=True)
        sp = {"file": str(prop_file(p)), "out": str(out), "name": name, "material": "M_" + p["tex"], "rotz": fit.get("rotz", -90),
              "pivot": "bottom" if fit.get("anchor") == "head" else "center"}
        if isinstance(fit.get("size"), list):
            sp["size_axis"], sp["size"] = "x", fit["size"][0]
        else:
            sp["size_axis"], sp["size"] = fit.get("size_axis", "x"), fit["size"]
        if fit.get("strap_cut_frac"):
            sp["strap_cut_frac"] = fit["strap_cut_frac"]
        specs.append(sp)
        wd = WORK / "props" / p["tex"]
        if not (wd / f"T_{p['tex']}_BC.png").exists():
            if p.get("procedural") == "sunglasses":
                sunglasses_textures(wd, p["tex"])
            else:
                run([PY, HERE / "prop_textures.py", prop_file(p), wd, p["tex"], "--size", "1024"], "prop tex " + key)
        td = READY / TEXS / "Props" / p["tex"]
        files = []
        ts = p.get("tex_size", 512)
        for k in ("BC", "N", "M"):
            dst = td / f"T_{p['tex']}_{k}.png"
            resize_tex(wd / f"T_{p['tex']}_{k}.png", dst, ts, k)
            files.append(dst)
        src_txt = "procedural Blender mesh (Tools/characters/blender/make_sunglasses.py)" if p.get("procedural") else "Tripo P2 via fal"
        rows.append({"manifest_id": p["id"], "file": str(out.relative_to(READY)), "target_path": str(out.relative_to(READY)),
                     "type": "model_static",
                     "import_notes": (f"Static character prop ({src_txt}, 1984 fictional kit, no markings). Scale Factor 1, Convert Units ON "
                                      "(metres), faces +Z, Y up; pivot at the " + ("bottom centre of the head opening" if sp["pivot"] == "bottom" else "bbox centre")
                                      + f". Animation Type None, Materials None -> URP/Lit M_{p['tex']} with T_{p['tex']}_BC/_N/_M (mask in Metallic + Occlusion). "
                                      "Read/Write off, mesh compression Medium. The fitted, skinned copies of this prop already ship inside the SK_<Character>.fbx "
                                      "files (separate renderers); use this loose mesh for swaps, pickups and dropped hats.")})
        rows += tex_rows(p["id"], files, ts, "Prop")
    sp_path = WORK / "specs" / "props.json"
    sp_path.parent.mkdir(parents=True, exist_ok=True)
    sp_path.write_text(json.dumps(specs, indent=1))
    out = run([BLENDER, "-b", "--factory-startup", "-P", HERE / "blender" / "export_props.py", "--", sp_path], "props")
    print("\n".join(l for l in out.splitlines() if l.startswith("PROP")))
    (WORK / "index").mkdir(parents=True, exist_ok=True)
    (WORK / "index" / "props.json").write_text(json.dumps(rows, indent=1))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*")
    ap.add_argument("--retex", action="store_true")
    ap.add_argument("--no-export", action="store_true")
    ap.add_argument("--no-preview", action="store_true")
    ap.add_argument("--only-preview", action="store_true")
    ap.add_argument("--props", action="store_true", help="only build the loose CHP_* prop meshes")
    ap.add_argument("--reel", action="store_true", help="only build the Calder 'Session 11' reel head variant")
    a = ap.parse_args()
    if a.props:
        build_props()
        return
    if a.reel:
        build_reel()
        return
    for n in a.names or list(BODIES):
        print("==", n)
        build(n, a)


if __name__ == "__main__":
    main()
