#!/usr/bin/env python3
"""Material rows (same format as ready/rows/ph_materials.json) for the WP09 fal booth props and the shared
M_BoothParts, so MaterialFactory (or the editor agent) can build URP/Lit materials and bl_render_shipped.py can preview.
  Tools/.venv/bin/python Tools/images/wp09_fal_mats.py   -> RawAssets/wp09/ready/rows/fal_materials.json
"""
import json, pathlib
ROOT = pathlib.Path(__file__).resolve().parents[2]
READY = ROOT / "RawAssets/wp09/ready"
CFG = json.loads((pathlib.Path(__file__).resolve().parent / "wp09_fal_props.json").read_text())
TEX = "Assets/_Game/Art/Textures/Props"
MAT = "Assets/_Game/Art/Materials/Props"


def lit(mid, name, folder, tex_name=None, smooth=1.0, extra=None):
    t = tex_name or name
    r = {"manifest_id": mid, "material": f"M_{name}", "target_path": f"{MAT}/M_{name}.mat",
         "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic",
         "textures": {"_BaseMap": f"{TEX}/{folder}/T_{t}_BC.png", "_BumpMap": f"{TEX}/{folder}/T_{t}_N.png",
                      "_MetallicGlossMap": f"{TEX}/{folder}/T_{t}_M.png", "_OcclusionMap": f"{TEX}/{folder}/T_{t}_M.png"},
         "floats": {"_Smoothness": smooth, "_SmoothnessTextureChannel": 0, "_Metallic": 1.0, "_OcclusionStrength": 1.0},
         "colors": {"_BaseColor": [1.0, 1.0, 1.0, 1.0]}}
    if extra:
        r.update(extra)
    return r


def main():
    rows = []
    for mid, c in CFG.items():
        if mid.startswith("_"):
            continue
        if not (READY / TEX / c["folder"] / f"T_{c['name']}_BC.png").exists():
            continue
        rows.append(lit(mid, c["name"], c["folder"]))
    if (READY / TEX / "av_cart/T_AVCart_BC.png").exists():
        rows.append(lit("FAL_AV_CART", "AVCartLong", "av_cart", tex_name="AVCart",
                        extra={"note": "SM_AVCartLong (1.7 m CRT cart) reuses the T_AVCart maps; a separate material only so the slot name matches. "
                                        "It may simply share M_AVCart (assign by hand)."}))
    rows.append({"manifest_id": "FAL_SCRAMBLER_BOX", "material": "M_BoothParts", "target_path": f"{MAT}/M_BoothParts.mat",
                 "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic",
                 "textures": {"_BaseMap": f"{TEX}/booth_parts/T_BoothParts_BC.png",
                              "_MetallicGlossMap": f"{TEX}/booth_parts/T_BoothParts_M.png"},
                 "floats": {"_Smoothness": 1.0, "_SmoothnessTextureChannel": 0, "_Metallic": 1.0, "_OcclusionStrength": 1.0},
                 "colors": {"_BaseColor": [1.0, 1.0, 1.0, 1.0]},
                 "note": "Shared by every procedural booth part (scrambler toggle + VU needle, reels, tape packs, tape path, "
                         "PROC_BOOTH_SMALLS). No normal map. Also listed under PROC_BOOTH_SMALLS and FAL_REEL_TO_REEL."})
    out = READY / "rows/fal_materials.json"
    out.write_text(json.dumps(rows, indent=1))
    print(out, len(rows))


if __name__ == "__main__":
    main()
