#!/usr/bin/env python3
"""Index rows + material specs for PROC_BOOTH_SMALLS (run after Blender wp09_smalls.py)."""
import json, pathlib
ROOT = pathlib.Path(__file__).resolve().parents[2]
READY = ROOT / "RawAssets/wp09/ready"
S = json.loads((ROOT / "RawAssets/wp09/PROC_BOOTH_SMALLS/stats.json").read_text())
M = "Assets/_Game/Art/Models/Props/booth_smalls"
BASE = ("Static mesh FBX (Blender 5.2, FBX_SCALE_ALL, forward -Z / up Y, baked space transform): Scale Factor 1, Convert Units on "
        "(1 unit = 1 m), identity rotation; Animation None, Import Materials None, Normals Import, tangents Calculate Mikk, "
        "Mesh LOD off (tiny). Material slot M_BoothParts (shared atlas T_BoothParts_BC/M). ")
NOTES = {
 "SM_BoothPen.fbx": "Interrogator's steel click pen, 0.135 m, lying along local X, pivot = centre of its underside (rests on the table). Four-tap emitter: put the pen-tap AudioSource on it; tap animation = a 6-8 deg pitch about the pivot-side end per tap.",
 "SM_Electrode.fbx": "Electrode pad (45 x 32 mm) with snap stud, two crossed cloth-tape strips and a 0.16 m red/black lead leaving along local +X. Pad faces local +Y (Unity). Parent one to each of Calder's forearm bones (booth seated body), pad flat on the inner forearm, lead toward the elbow.",
 "SM_ElectrodeWire.fbx": "0.95 m twisted red/black pair: origin = the end taped to the chair armrest; it sags 0.68 m to the floor drifting 0.38 m toward local +Z (the chair's front, toward the trolley) and 0.12 m toward local -X, then lies ~0.2 m on the floor. Static; one per arm (scale X -1 for the left arm).",
 "SM_Cigarette.fbx": "Lit filter cigarette 84 mm along local X (filter at -X), child empty SmokeAnchor at the ember tip = the smoke particle emitter (GDD 8.0 smoke at (-0.6, 1.7, -0.6) is the interrogator's hand-held one; this one rests in the ashtray notch). Second material slot M_CigaretteEmber: URP Lit, base (0.25, 0.05, 0.02), emission (1.0, 0.35, 0.08) x 2-4 with a 0.3-0.6 s random breathing flicker.",
}
rows = []
for fn, st in S.items():
    rel = f"{M}/{fn}"
    rows.append({"manifest_id": "PROC_BOOTH_SMALLS", "file": rel, "target_path": rel, "type": "model",
                 "import_notes": BASE + f"Tris {sum(st['tris'].values())}. " + NOTES[fn]})
(READY / "rows/smalls.json").write_text(json.dumps(rows, indent=1))
mats = [{"manifest_id": "PROC_BOOTH_SMALLS", "material": "M_CigaretteEmber", "target_path": "Assets/_Game/Art/Materials/Props/M_CigaretteEmber.mat",
         "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic", "textures": {},
         "floats": {"_Smoothness": 0.1, "_Metallic": 0.0}, "colors": {"_BaseColor": [0.25, 0.05, 0.02, 1.0]},
         "emission": {"_EmissionColor_linear": [2.0, 0.7, 0.16], "note": "Enable _EMISSION; flicker 0.6-1.2x."}}]
(READY / "rows/smalls_materials.json").write_text(json.dumps(mats, indent=1))
print(len(rows), "rows")
