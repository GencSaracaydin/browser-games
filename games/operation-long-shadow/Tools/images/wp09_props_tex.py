#!/usr/bin/env python3
"""WP09: texture packing, import rows and material specs for the processed Poly Haven booth props.

  Tools/.venv/bin/python -I Tools/images/wp09_props_tex.py [--ids PH_TELEVISION_01,...]

Reuses Tools/tex/wp10_props_tex.py (gl_materials, pack_set; D4 mask = R metal, G AO, A smoothness) with the raw
folder pointed at RawAssets/wp09 and the config Tools/images/wp09_props.json. Inputs: the reports written by
Tools/images/wp09_ph_model.py and the paint-outs of Tools/images/wp09_paintout.py (<ID>/clean/).
Outputs: T_<Name>[_<Set>]_{BC,N,M,E}.png under RawAssets/wp09/ready/Assets/_Game/Art/Textures/Props/<asset>/,
rows in RawAssets/wp09/ready/rows/ph_props.json and material specs in ready/rows/ph_materials.json
(merged into ready/import_index.json and ready/materials_wp09.json by build_index.py).
"""
import argparse, glob, importlib.util, json, os, pathlib, sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/tex"))
spec = importlib.util.spec_from_file_location("wp10_props_tex", ROOT / "Tools/tex/wp10_props_tex.py")
W = importlib.util.module_from_spec(spec)
spec.loader.exec_module(W)
W.RAW = str(ROOT / "RawAssets/wp09")
W.READY = str(ROOT / "RawAssets/wp09/ready")
W.CFG = json.load(open(ROOT / "Tools/images/wp09_props.json"))
READY = W.READY
ROWS = pathlib.Path(READY) / "rows"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids")
    a = ap.parse_args()
    reps = sorted(glob.glob(os.path.join(W.RAW, "_reports", "props", "PH_*.json")))
    if a.ids:
        reps = [r for r in reps if os.path.basename(r)[:-5] in set(a.ids.split(","))]
    rows, specs = [], []
    rel = lambda p: os.path.relpath(p, READY)
    for rp in reps:
        rep = json.load(open(rp))
        mid, name, asset = rep["manifest_id"], rep["name"], rep["asset"]
        cfg = W.CFG[mid]
        size = cfg["tex"]
        tex_dir = os.path.join(READY, "Assets/_Game/Art/Textures/Props", asset)
        glm = W.gl_materials(rep["source_file"])
        sets, mat_specs = {}, {}
        for out in rep["outputs"]:
            for un, gl_names in out["materials"].items():
                if un in mat_specs:
                    continue
                if un.endswith("_Screen"):
                    mat_specs[un] = {"manifest_id": mid, "material": un, "target_path": f"Assets/_Game/Art/Materials/Props/{un}.mat",
                                     "shader": "LongShadow/SG_CRTScreen (WP09); placeholder URP/Lit", "textures": {},
                                     "floats": {"_Metallic": 0.0, "_Smoothness": 0.92}, "colors": {"_BaseColor": [0.035, 0.045, 0.04, 1.0]},
                                     "note": "CRT glass submesh. UV0 is 0..1 over the glass (u = viewer's right, v = up), aspect ~4:3 "
                                             "for RT_CRT1..6 (512x384). Off state: dark green-grey glass with high smoothness."}
                    continue
                g = glm.get(gl_names[0]) or next(iter(glm.values()))
                key = (g["bc"], g["mr"], g["n"], g["e"])
                if key not in sets:
                    if g["bc"] is None:
                        sets[key] = ({}, {})
                    else:
                        stem = f"T_{name}" if un == f"M_{name}" else f"T_{name}_" + un.split(f"M_{name}_")[-1]
                        sets[key] = W.pack_set(mid, W.clean_or(mid, g["bc"]), g["mr"], W.clean_or(mid, g["n"]), g["e"], size,
                                               tex_dir, stem, g["ef"], g["es"])
                files, st = sets[key]
                sp = {"manifest_id": mid, "material": un, "target_path": f"Assets/_Game/Art/Materials/Props/{un}.mat",
                      "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic", "textures": {}, "floats": {}, "colors": {}}
                if files.get("BC"):
                    sp["textures"]["_BaseMap"] = rel(files["BC"])
                if files.get("N"):
                    sp["textures"]["_BumpMap"] = rel(files["N"])
                if files.get("M"):
                    sp["textures"]["_MetallicGlossMap"] = rel(files["M"])
                    sp["textures"]["_OcclusionMap"] = rel(files["M"])
                    sp["floats"].update({"_Smoothness": 1.0, "_SmoothnessTextureChannel": 0, "_Metallic": 1.0, "_OcclusionStrength": 1.0})
                else:
                    sp["floats"].update({"_Metallic": float(g.get("mf", 0.0) or 0.0), "_Smoothness": round(1 - float(g.get("rf", 0.5) or 0.5), 3)})
                sp["colors"]["_BaseColor"] = [round(float(x), 4) for x in (g.get("bcf") or [1, 1, 1, 1])[:3]] + [1.0]
                if g.get("alpha") == "BLEND" or g.get("transmission") or un.endswith("_Glass"):
                    sp.update({"surface": "Transparent", "blend": "Alpha"})
                    sp["colors"]["_BaseColor"][3] = 0.22
                    sp["floats"]["_Smoothness"] = 0.95
                    sp["note"] = "Glass: Transparent, alpha ~0.22, smoothness 0.95, no shadow casting."
                if g.get("alpha") == "MASK":
                    sp.update({"alpha_clip": True})
                    sp["floats"]["_Cutoff"] = 0.5
                if files.get("E") or any(float(x) > 0 for x in (g.get("ef") or [0, 0, 0])):
                    ef = g.get("ef") or [1, 1, 1]
                    es = float(g.get("es", 1.0) or 1.0)
                    sp["emission"] = {"_EmissionMap": rel(files["E"]) if files.get("E") else None,
                                      "_EmissionColor_linear": [round(float(c) * min(es, 8.0), 3) for c in ef],
                                      "note": "Enable _EMISSION; drive intensity from the booth tube flicker (0 when the tube dies in outro shot 10)."}
                mat_specs[un] = sp
        specs += list(mat_specs.values())
        for key, (files, st) in sets.items():
            for suf, p in files.items():
                typ = {"BC": "texture_basecolor", "N": "texture_normal", "M": "texture_mask", "E": "texture_emissive"}[suf]
                note = {"BC": f"Default, sRGB ON, Max Size {size}, DXT1 Crunched q55 (Props/**), mips on, aniso 2.",
                        "N": f"Normal map, OpenGL +Y, sRGB OFF, Max Size {size}, DXT5nm Crunched q60.",
                        "M": f"Default, sRGB OFF, Max Size {size}, DXT5 Crunched q60. Mask D4: R metallic, G AO, B 0, A smoothness; assign to _MetallicGlossMap AND _OcclusionMap.",
                        "E": "Default, sRGB ON, Max Size 512, DXT1 Crunched q60. Emission map."}[suf]
                rows.append({"manifest_id": mid, "file": rel(p), "target_path": rel(p), "type": typ, "import_notes": note})
        for out in rep["outputs"]:
            lods = out["lod_tris"]
            parts = out.get("parts") or {}
            n = (f"Model: Scale Factor 1, Convert Units ON (metres), Bake Axis Conversion OFF (axes baked: root rotation 0, Y up, front +Z), "
                 f"Normals Import, Tangents Calculate Mikktspace, Materials None (MaterialFactory assigns by slot name: "
                 f"{', '.join(sorted(out['materials']))}), Mesh Compression Medium, Read/Write off, Animation None. Size (x,y,z) "
                 f"{out['size_unity_m']} m. ")
            if len(lods) > 1:
                n += f"LODs in file {'/'.join(map(str, lods))} tris (*_LOD0..{len(lods)-1}: Unity builds the LODGroup, transitions 50/20/4 %); Unity Mesh LOD generation OFF. "
            else:
                n += f"Single LOD, {lods[0]} tris. "
            if parts:
                n += "Children: " + "; ".join(f"{k} pivot {v['pivot_unity']} size {v['size_unity']}" for k, v in parts.items()) + ". "
            n += "Generate Lightmap UVs OFF (light-probe GI, D8). " + cfg.get("note", "")
            rows.append({"manifest_id": mid, "file": out["fbx"], "target_path": out["fbx"], "type": "model_fbx", "import_notes": n,
                         "lod_tris": lods, "size_m": out["size_unity_m"]})
        print(mid, name, "sets", len(sets), "mats", list(mat_specs))
    ROWS.mkdir(parents=True, exist_ok=True)
    (ROWS / "ph_props.json").write_text(json.dumps(rows, indent=1))
    (ROWS / "ph_materials.json").write_text(json.dumps(specs, indent=1))


if __name__ == "__main__":
    main()
