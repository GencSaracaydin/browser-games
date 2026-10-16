#!/usr/bin/env python3
"""Write RawAssets/wp03/ready/import_index.json (one row per import-ready file) and print a budget table.

  Tools/.venv/bin/python Tools/mesh/weapons/import_index.py [--check]
Rows: {manifest_id, file (path relative to ready/), target_path (Unity path), type, import_notes}.
Triangle counts come from the per-weapon process reports (RawAssets/wp03/_reports/<ID>.json); texture sizes from
the PNG headers. --check exits 1 when a model or texture is over the ASSET_MANIFEST / TECH_PLAN §8.9 budget.
"""
import argparse, json, pathlib, re, sys
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[3]
READY = ROOT / "RawAssets/wp03/ready"
REPORTS = ROOT / "RawAssets/wp03/_reports"
JOBS = pathlib.Path(__file__).resolve().parent / "jobs"

# file-stem name -> manifest id
NAME_ID = {"Commando": "WPN_COMMANDO", "HushPuppy": "WPN_HUSHPUPPY", "AK": "WPN_AK", "RPK": "WPN_RPK",
           "AKMS": "WPN_AKMS", "SVD": "WPN_SVD", "RPG7": "WPN_RPG7", "RPG7_Rocket": "WPN_RPG7_ROCKET",
           "DShK": "WPN_DSHK", "M60D": "WPN_M60D", "M67": "WPN_M67", "Knife": "WPN_KNIFE",
           "SmokeGrenade": "WPN_SMOKE_GRENADE", "Casings": "PROC_SHELL_CASINGS", "Casing": "PROC_SHELL_CASINGS"}
# budgets: (fp tris, world tris, mag tris, max texture px FP/world/mag)
BUDGET = {"WPN_COMMANDO": (20000, 6000, 1500, 2048), "WPN_HUSHPUPPY": (20000, 3000, 800, 2048),
          "WPN_AK": (20000, 6000, 1500, 2048), "WPN_RPK": (20000, 7000, 2500, 2048), "WPN_AKMS": (0, 6000, 0, 1024),
          "WPN_SVD": (0, 7000, 0, 1024), "WPN_RPG7": (0, 6000, 0, 1024), "WPN_RPG7_ROCKET": (0, 1500, 0, 512),
          "WPN_DSHK": (0, 8000, 0, 1024), "WPN_M60D": (20000, 8000, 0, 2048), "WPN_M67": (3000, 1200, 0, 1024),
          "WPN_KNIFE": (3000, 0, 0, 1024), "WPN_SMOKE_GRENADE": (0, 1000, 0, 512), "PROC_SHELL_CASINGS": (0, 60, 0, 256)}

AXIS = ("1 unit = 1 m (Scale Factor 1, Convert Units on); transforms are baked (Bake Axis Conversion off, every object "
        "imports with identity rotation and scale 1): barrel/forward = +Z, up = +Y, the weapon's right side = +X")
MODEL_COMMON = ("Import Normals: Import, Tangents: Calculate Mikktspace (the normal maps were made against these "
                "normals), Materials: Import via MaterialDescription so the slot names survive (slots are named "
                "M_WPN_<Name> / M_WPN_<Name>_Mag; ModelMaterialBinder remaps them to the MaterialFactory materials built "
                "from T_WPN_<Name>_{BC,N,M}; ModelImportRules sets all of this for Art/Models/Weapons), Animation Type: "
                "None, no rig/avatar, Read/Write off, Lightmap UVs off")


def mid_of(name):
    for k in sorted(NAME_ID, key=len, reverse=True):
        if name == k or name.startswith(k + "_"):
            return NAME_ID[k], name[len(k):].lstrip("_")
    return None, name


def model_row(f, rel, reports):
    stem = f.stem  # SM_WPN_AK_FP
    body = stem[len("SM_WPN_"):]
    m = re.match(r"(.+)_(FP|W|Mag)$", body)
    name, kind = (m.group(1), m.group(2)) if m else (body, "")
    mid, _ = mid_of(name)
    rep = reports.get(mid, {})
    notes = [AXIS, MODEL_COMMON]
    if mid == "PROC_SHELL_CASINGS":
        notes = ["One object per calibre: SM_WPN_Casing_556x45 (Commando), _762x39 (AK/RPK/AKMS, lacquered steel), "
                 "_9x19 (Hush Puppy), _762x51 (M60D); each <= 60 tris, case axis along +Z (mouth forward), origin at the "
                 "case centre (spin pivot), real size in metres", MODEL_COMMON.replace("M_WPN_<Name>", "M_WPN_Casings"),
                 "Mesh Compression Off; texture T_WPN_Casings_{BC,M} (no normal map)"]
        return {"manifest_id": mid, "file": rel, "target_path": "Assets/" + rel.split("Assets/", 1)[1], "type": "model",
                "import_notes": "; ".join(notes)}
    if kind == "FP":
        piv = "the pistol grip" if mid not in ("WPN_M60D", "WPN_KNIFE", "WPN_M67") else \
            {"WPN_M60D": "the midpoint between the spade grips", "WPN_KNIFE": "the middle of the handle (blade = +Z, edge = -Y)",
             "WPN_M67": "the grenade's centre (fuze/spoon up = +Y)"}[mid]
        notes.insert(1, f"origin at {piv}")
        notes.append(f"FP view model, {rep.get('fp_tris', '?')} tris; Mesh Compression Off; shadows off (view-model "
                     "layer, set by the prefab builder)")
        if any((f.parent / f"{f.stem.replace('_FP', '_Mag')}.fbx").exists() for _ in [0]):
            notes.append("child 'Mag' = the magazine, its pivot at the magazine well entry (reload motion); "
                         "Socket_* empties: Muzzle, SightFront, SightRear, Grip, LeftHand, Eject, MagWell (+ any extra "
                         "listed in the process report)")
        else:
            notes.append("Socket_* empties as children (see the handoff socket table)")
        notes.append("material slots: M_WPN_<Name> on the body" + (", M_WPN_<Name>_Mag on Mag" if kind == "FP" and
                     (f.parent / f"{f.stem.replace('_FP', '_Mag')}.fbx").exists() else ""))
        tris = rep.get("fp_tris")
    elif kind == "W":
        wt = rep.get("world_tris", [])
        parts = rep.get("world_part_tris", {})
        notes.insert(1, "origin at " + ("the grip" if mid not in ("WPN_DSHK", "WPN_M67", "WPN_SMOKE_GRENADE",
                                                                    "WPN_RPG7_ROCKET", "WPN_M60D") else
                                        {"WPN_DSHK": "the pintle pivot (mount yoke)", "WPN_M67": "the grenade centre",
                                         "WPN_SMOKE_GRENADE": "the can centre (fuze up = +Y)",
                                         "WPN_RPG7_ROCKET": "the rocket centre (nose = +Z)",
                                         "WPN_M60D": "the midpoint between the spade grips (Socket_Pivot = pintle)"}[mid]))
        notes.append(f"world model: root SM_WPN_<Name>_W with children _LOD0/_LOD1/_LOD2 ({wt} tris); Unity builds the "
                     "LODGroup from the _LODn suffixes (suggested transitions 25 % / 10 % / 2 %, cull below); Mesh LOD "
                     "generation stays off (authored LODs; ModelImportRules detects them); Mesh Compression Medium")
        if parts:
            notes.append("separate child parts (not in the LODs, own pivots): " +
                         ", ".join(f"{k} ({v} tris)" for k, v in parts.items()))
        if mid in ("WPN_AK", "WPN_RPK", "WPN_COMMANDO", "WPN_HUSHPUPPY", "WPN_M60D"):
            notes.append("shares the FP UVs and textures (M_WPN_<Name>, + _Mag on the magazine part when present)")
        notes.append("Socket_* empties: Muzzle, LeftHand (+ others per the handoff table)")
        tris = (wt[0] + sum(parts.values())) if wt else None  # LOD0 + split parts (both render at LOD0)
    elif kind == "Mag":
        notes.insert(1, "origin at the magazine-well entry point (the top of the magazine where it seats); stand-alone "
                        "magazine for drops/pickups, same mesh as the FP 'Mag' child")
        notes.append("Mesh Compression Off; material M_WPN_<Name>_Mag")
        tris = None
        for st in rep.get("steps", []):
            m2 = re.match(r"backfaces: SM_WPN_\w+_Mag \d+ visible back faces doubled -> (\d+) tris", st)
            if m2:
                tris = int(m2.group(1))
    else:
        tris = None
    return {"manifest_id": mid if kind != "Mag" else mid + "_MAG", "file": rel,
            "target_path": "Assets/" + rel.split("Assets/", 1)[1], "type": "model", "tris": tris,
            "import_notes": "; ".join(notes)}


def tex_row(f, rel):
    stem = f.stem  # T_WPN_AK_Mag_BC
    if f.parent.name == "_Shared":
        w, h = Image.open(f).size
        return {"manifest_id": "WPN_COMMANDO", "file": rel, "target_path": "Assets/" + rel.split("Assets/", 1)[1],
                "type": "texture", "size": [w, h],
                "import_notes": "shared tiling DETAIL normal for every FP weapon material (manifest WPN_COMMANDO note: "
                                "ambientCG Scratches004, CC0, from RawAssets/wp10/cc0/DCL_SCRATCHES): Texture Type Normal "
                                "map, sRGB OFF, OpenGL, max 512, wrap Repeat, mips on (TextureImportRules Weapons/_Shared); "
                                "MaterialFactory: _DetailNormalMap on M_WPN_{Commando,HushPuppy,AK,RPK,M60D,Knife} with "
                                "_DetailNormalMapScale 0.35 and a tiling of ~1 repeat per 6 cm (UV0 tiling 8 on the 2048 "
                                "atlas); no detail albedo"}
    m = re.match(r"T_WPN_(.+)_(BC|N|M)$", stem)
    name, suf = m.group(1), m.group(2)
    mid, rest = mid_of(name)
    if rest.startswith("Mag") and mid != "PROC_SHELL_CASINGS":
        mid += "_MAG"
    w, h = Image.open(f).size
    if suf == "BC":
        n = (f"base colour, Texture Type Default, sRGB ON, RGB (no alpha), max size {w}, Crunch q80 (TECH_PLAN §3.10 "
             "weapons), mips on, aniso 4")
        if rest and not rest.startswith("Mag"):
            n += f"; colour variant '{rest}' of the same mesh/UVs (swap only the BC on a material copy)"
    elif suf == "N":
        n = (f"tangent-space normal map, Texture Type Normal map, sRGB OFF, OpenGL convention (+Y up = Unity's), max "
             f"size {w}, Crunch q80, mips on")
    else:
        n = (f"mask map (TECH_PLAN D4): R metallic, G ambient occlusion, B unused (0), A smoothness; Texture Type "
             f"Default, sRGB OFF, alpha is data (not transparency), max size {w}, Crunch q80 (DXT5); assign to both "
             "_MetallicGlossMap and _OcclusionMap (smoothness source = metallic alpha)")
    return {"manifest_id": mid, "file": rel, "target_path": "Assets/" + rel.split("Assets/", 1)[1], "type": "texture",
            "size": [w, h], "import_notes": n}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    reports = {p.stem: json.loads(p.read_text()) for p in REPORTS.glob("*.json")}
    rows, bad = [], []
    for f in sorted(READY.rglob("*")):
        if not f.is_file() or f.name == "import_index.json" or f.name.startswith("."):
            continue
        rel = str(f.relative_to(READY))
        if f.suffix.lower() == ".fbx":
            r = model_row(f, rel, reports)
        elif f.suffix.lower() == ".png":
            r = tex_row(f, rel)
        else:
            continue
        rows.append(r)
        b = BUDGET.get(r["manifest_id"].replace("_MAG", "")) if r["manifest_id"] != "PROC_SHELL_CASINGS" else BUDGET["PROC_SHELL_CASINGS"]
        if b and r["type"] == "texture":
            lim = b[3] if not r["manifest_id"].endswith("_MAG") else 1024
            if max(r["size"]) > lim:
                bad.append(f"{rel}: {r['size']} > {lim}")
        if b and r["type"] == "model" and r.get("tris"):
            lim = b[0] if "_FP" in rel else b[1]
            if lim and r["tris"] > lim:
                bad.append(f"{rel}: {r['tris']} tris > {lim}")
    (READY / "import_index.json").write_text(json.dumps(rows, indent=1))
    ids = sorted({r["manifest_id"] for r in rows})
    print(f"import_index.json: {len(rows)} files, {len(ids)} manifest ids: {', '.join(ids)}")
    for b in bad:
        print("OVER BUDGET", b)
    if a.check and bad:
        sys.exit(1)


if __name__ == "__main__":
    main()
