#!/usr/bin/env python3
"""Collect every RawAssets/wp10/fal/<ID>/ready_*.json into RawAssets/wp10/ready/import_index_fal.json and write
materials_fal.json (one URP material spec per mesh/decal/texture set), checking that every file exists. WP10_FAL.

  Tools/.venv/bin/python Tools/mesh/props/build_index.py [--exclude ID ...]
Also prints the download-size estimate per asset (Crunch q50 rule of thumb from TECH_PLAN §8.9 + FBX size).
"""
import argparse, json, pathlib, re

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
RAW = ROOT / "RawAssets/wp10/fal"
READY = ROOT / "RawAssets/wp10/ready"

# in-game max size per texture (MB per BC+N+M set at Crunch, TECH_PLAN §8.9: 2048 3.5, 1024 0.87, 512 0.23)
SET_MB = {2048: 3.5, 1024: 0.87, 512: 0.23, 256: 0.06}


def maxsize(note):
    m = re.search(r"Max Size (\d+)", note or "")
    return int(m.group(1)) if m else 1024


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--exclude", nargs="*", default=[])
    a = ap.parse_args()
    rows, missing, seen = [], [], set()
    for f in sorted(RAW.glob("*/ready_*.json")):
        aid = f.parent.name
        if aid in a.exclude:
            continue
        for r in json.loads(f.read_text()):
            if r["file"] in seen:
                continue
            seen.add(r["file"])
            if not (READY / r["file"]).exists():
                missing.append(r["file"]); continue
            r.setdefault("tool", "process_prop")
            r["source"] = "fal (WP10_FAL)"
            rows.append(r)
    vp = RAW / "_vehicle_extras/vehicle_points.json"
    if vp.exists():
        V = json.loads(vp.read_text())
        for r in rows:
            if r["type"] == "model" and r["file"].endswith("SM_Technical.fbx"):
                r["import_notes"] += (f" DShK mount: top centre of the empty pedestal post at local {V['technical']['dshk_mount_top_unity']} "
                                      "(Unity metres); parent WPN_DSHK's cradle pivot there. Burnt variant material M_TechnicalBurnt (same mesh).")
            if r["type"] == "model" and r["file"].endswith("SM_Helicopter.fbx"):
                h = V["helicopter"]
                r["import_notes"] += (f" Rotors are separate (SM_HeliMainRotor at local {h['main_rotor_hub_unity']}, SM_HeliTailRotor at local "
                                      f"{h['tail_rotor_hub_unity']}). The cabin door openings are baked dark (closed shell): the cabin interior is the "
                                      "VEH_HELI_CABIN kit; hide/clip the door panel region when the kit is shown.")
            if r["type"] == "model" and r["file"].endswith("SM_CargoTruck.fbx"):
                r["import_notes"] += " Burnt variant material M_CargoTruckBurnt (same mesh) for the E11 trucks after their fireball."
    (READY / "import_index_fal.json").write_text(json.dumps(rows, indent=1))
    # materials
    mats = []
    by_dir = {}
    for r in rows:
        by_dir.setdefault((r["manifest_id"], str(pathlib.Path(r["file"]).parent)), []).append(r)
    for f in sorted(RAW.glob("*/material_*.json")):
        mats.append(json.loads(f.read_text()))
    for r in rows:
        if r["type"] != "model":
            continue
        fbx = pathlib.Path(r["file"])
        name = fbx.stem[3:]                      # SM_<Name>
        kind = fbx.parts[4]                      # Assets/_Game/Art/Models/<Kind>/<folder>/SM_x.fbx
        tdir = pathlib.Path("Assets/_Game/Art/Textures") / kind / fbx.parts[5]
        if kind == "Vegetation" and name.startswith("DryGrass"):
            continue                              # M_DryGrass comes from the foliage-cutout texture row
        if kind == "Vegetation":
            base = re.sub(r"_[A-Z]$", "", name)
            for part, extra in (("Bark", {}), ("Leaves", {"alpha_clip": 0.5, "render_face": "Both", "shader_alt": "SG_FoliageWind"})):
                mn = f"M_{base}_{part}"
                if any(m["material"] == mn for m in mats):
                    continue
                mats.append({"manifest_id": r["manifest_id"], "material": mn, "target_path": f"Assets/_Game/Art/Materials/Vegetation/{mn}.mat",
                             "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic",
                             "textures": {"_BaseMap": str(tdir / f"T_{base}_{part}_BC.png"), "_BumpMap": str(tdir / f"T_{base}_{part}_N.png"),
                                          "_MetallicGlossMap": str(tdir / f"T_{base}_{part}_M.png"), "_OcclusionMap": str(tdir / f"T_{base}_{part}_M.png")},
                             "floats": {"_Smoothness": 1.0, "_SmoothnessTextureChannel": 0, "_Metallic": 1.0, "_OcclusionStrength": 1.0},
                             "submesh_slot": 0 if part == "Bark" else 1, **extra})
            continue
        if name.startswith("HeliMainRotor") or name.startswith("HeliTailRotor"):
            for mn, tex, extra in (("M_HeliRotor", {"_BaseMap": str(tdir / "T_HeliRotor_BC.png"), "_MetallicGlossMap": str(tdir / "T_HeliRotor_M.png")}, {}),
                                   ("M_HeliRotorBlur", {"_BaseMap": str(tdir / "T_HeliRotorBlur_BC.png")},
                                    {"surface": "Transparent", "blend": "Alpha", "render_face": "Both", "zwrite": False})):
                if any(m["material"] == mn for m in mats):
                    continue
                mats.append({"manifest_id": r["manifest_id"], "material": mn, "target_path": f"Assets/_Game/Art/Materials/Vehicles/{mn}.mat",
                             "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic", "textures": tex,
                             "floats": {"_Smoothness": 1.0, "_SmoothnessTextureChannel": 0, "_Metallic": 1.0}, **extra})
            continue
        tex = {"_BaseMap": str(tdir / f"T_{name}_BC.png"), "_BumpMap": str(tdir / f"T_{name}_N.png"),
               "_MetallicGlossMap": str(tdir / f"T_{name}_M.png"), "_OcclusionMap": str(tdir / f"T_{name}_M.png")}
        m = {"manifest_id": r["manifest_id"], "material": f"M_{name}", "target_path": f"Assets/_Game/Art/Materials/{kind}/M_{name}.mat",
             "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic", "textures": tex,
             "floats": {"_Smoothness": 1.0, "_SmoothnessTextureChannel": 0, "_Metallic": 1.0, "_BumpScale": 1.0, "_OcclusionStrength": 1.0}}
        if (READY / tdir / f"T_{name}_E.png").exists():
            m["textures"]["_EmissionMap"] = str(tdir / f"T_{name}_E.png"); m["emission"] = {"color": [1, 1, 1], "intensity_hdr": 4.0}
        mats.append(m)
        if (READY / tdir / f"T_{name}Burnt_BC.png").exists():
            mats.append({**m, "material": f"M_{name}Burnt", "target_path": f"Assets/_Game/Art/Materials/{kind}/M_{name}Burnt.mat",
                         "textures": {**tex, "_BaseMap": str(tdir / f"T_{name}Burnt_BC.png"), "_MetallicGlossMap": str(tdir / f"T_{name}Burnt_M.png"),
                                      "_OcclusionMap": str(tdir / f"T_{name}Burnt_M.png")}, "note": "swap after the fireball"})
    for r in rows:
        if r["type"] == "texture_decal_basecolor":
            p = pathlib.Path(r["file"]); nm = p.stem[2:-3]           # T_<Decal_X>_BC
            n = p.with_name(f"T_{nm}_N.png")
            mats.append({"manifest_id": r["manifest_id"], "material": f"M_{nm}", "target_path": f"Assets/_Game/Art/Materials/Decals/M_{nm}.mat",
                         "shader": "Shader Graphs/Decal", "textures": {"Base_Map": r["file"], **({"Normal_Map": str(n)} if (READY / n).exists() else {})},
                         "floats": {"Normal_Blend": 0.8 if (READY / n).exists() else 0.0, "DrawOrder": 0},
                         "affect": {"BaseColor": True, "Normal": (READY / n).exists(), "MAOS": False},
                         "layout": str(p.with_name(f"T_{nm}_layout.json"))})
        if r["type"] == "texture_foliage_cutout":
            p = pathlib.Path(r["file"]); nm = p.stem[2:-3]
            mats.append({"manifest_id": r["manifest_id"], "material": f"M_{nm}", "target_path": f"Assets/_Game/Art/Materials/Vegetation/M_{nm}.mat",
                         "shader": "Universal Render Pipeline/Lit", "textures": {"_BaseMap": r["file"]}, "alpha_clip": 0.5, "render_face": "Both",
                         "floats": {"_Smoothness": 0.15, "_Metallic": 0.0}, "shader_alt": "SG_FoliageWind"})
    (READY / "materials_fal.json").write_text(json.dumps(mats, indent=1))
    # size estimate
    est = {}
    for r in rows:
        f = READY / r["file"]
        mb = 0.0
        if r["type"] == "model":
            mb = f.stat().st_size / 1e6 * 0.35          # FBX -> Unity mesh (Medium compression, ~35 % of FBX)
        elif r["file"].endswith(".png"):
            ms = maxsize(r["import_notes"])
            mb = SET_MB.get(ms, 0.87) / 3
            if "1536" in r["import_notes"]:
                mb = 0.6
        est[r["manifest_id"]] = est.get(r["manifest_id"], 0) + mb
    tot = sum(est.values())
    print(json.dumps({"rows": len(rows), "materials": len(mats), "missing": missing, "est_download_mb_total": round(tot, 1),
                      "per_id_mb": {k: round(v, 2) for k, v in sorted(est.items(), key=lambda kv: -kv[1])}}, indent=1))


if __name__ == "__main__":
    main()
