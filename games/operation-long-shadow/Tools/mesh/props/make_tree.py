#!/usr/bin/env python3
"""Procedural olive / pomegranate tree variants -> FBX (LOD0-2) + impostor + review sheet + index rows (WP10_FAL).

  Tools/.venv/bin/python Tools/mesh/props/make_tree.py VEG_OLIVE_TREE [--variants A,B,C] [--no-render] [--set card_size=0.55 ...]

Needs RawAssets/wp10/fal/<ID>/tree_tex.json from tree_tex.py (leaf tiles, fruit uv, texture paths).
Geometry: Tools/mesh/props/blender_tree.py (skeleton growth, fluted trunk, folded alpha leaf cards, vertex-colour wind data).
"""
import argparse, json, pathlib, subprocess, sys
from PIL import Image, ImageDraw, ImageFont

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
RAW = ROOT / "RawAssets/wp10/fal"
READY = ROOT / "RawAssets/wp10/ready"
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"

SPEC = {
    "VEG_OLIVE_TREE": dict(species="olive", name="OliveTree", folder="olive_tree", height=4.5, bark_tile_m=0.8, card_size=0.45,
                           cards_per_tip=3, seeds={"A": 11, "B": 23, "C": 37}, budget=8000,
                           skeleton="sc", shell=0.0, tile_weights=[0.3, 0.14, 0.3, 0.26]),
    "VEG_POMEGRANATE_TREE": dict(species="pomegranate", name="PomegranateTree", folder="pomegranate_tree", height=3.0,
                                 bark_tile_m=0.5, card_size=0.42, cards_per_tip=5, fruit_count=14, fruit_r=0.045,
                                 seeds={"A": 5, "B": 17, "C": 29}, budget=6000,
                                 skeleton="sc", shell=0.0, leaf_min_level=1, tile_weights=[0.42, 0.14, 0.03, 0.41],
                                 sc={"attractors": 1600, "kill": 0.15, "base": 0.3}),
}


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stdout[-2500:], r.stderr[-2500:])
        raise SystemExit("failed: " + " ".join(map(str, cmd[:5])))
    return r.stdout


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id"); ap.add_argument("--variants", default="A,B,C"); ap.add_argument("--no-render", action="store_true")
    ap.add_argument("--set", nargs="*", default=[])
    a = ap.parse_args()
    S = dict(SPEC[a.id])
    for kv in a.set:
        k, v = kv.split("=", 1)
        S[k] = json.loads(v)
    tt = json.loads((RAW / a.id / "tree_tex.json").read_text())
    name, folder = S["name"], S["folder"]
    mdir = READY / f"Assets/_Game/Art/Models/Vegetation/{folder}"
    tdir = READY / f"Assets/_Game/Art/Textures/Vegetation/{folder}"
    work = RAW / a.id / "work_proc"
    work.mkdir(parents=True, exist_ok=True)
    rows, sheets, allstats = [], [], {}
    for var in a.variants.split(","):
        vname = f"{name}_{var}"
        fbx = mdir / f"SM_{vname}.fbx"
        spec = {"species": S["species"], "seed": S["seeds"][var], "name": vname, "fbx": str(fbx), "height": S["height"],
                "leaf_tiles": tt["leaf_tiles"], "fruit_uv": tt.get("fruit_uv"), "bark_tile_m": S["bark_tile_m"],
                "card_size": S["card_size"], "cards_per_tip": S["cards_per_tip"], "lods": [0, 1, 2]}
        for k in ("fruit_count", "fruit_r", "skeleton", "sc", "shell", "leaf_min_level", "tile_weights"):
            if k in S:
                spec[k] = S[k]
        sp = work / f"tree_{var}.json"; sp.write_text(json.dumps(spec, indent=1))
        out = run([BLENDER, "-b", "--factory-startup", "-t", "4", "-P", str(HERE / "blender_tree.py"), "--", "--spec", str(sp)])
        st = json.loads(next(l for l in out.splitlines() if l.startswith("TREE_STATS")).split(" ", 1)[1])
        allstats[vname] = st
        imp = tdir / f"T_{vname}_Impostor_BC.png"
        run([BLENDER, "-b", "--factory-startup", "-t", "4", "-P", str(HERE / "blender_tree_render.py"), "--", "--fbx", str(fbx),
             "--tex", str(tdir), "--name", name, "--out", str(work / f"r_{var}"), "--impostor", str(imp)])
        if not a.no_render:
            run([BLENDER, "-b", "--factory-startup", "-t", "4", "-P", str(HERE / "blender_tree_render.py"), "--", "--fbx", str(fbx),
                 "--tex", str(tdir), "--name", name, "--out", str(work / f"r_{var}"), "--res", "560"])
            sheets.append((var, vname, st))
        rel = lambda p: str(p.relative_to(READY))
        lt = ", ".join(f"{k}: {v}" for k, v in st.items() if k != "dims")
        rows.append({"manifest_id": a.id, "file": rel(fbx), "target_path": rel(fbx), "type": "model",
                     "import_notes": f"Procedural tree variant {var} (Blender 5.2 FBX, forward -Z / up Y, baked space transform): Scale Factor 1, "
                     f"Convert Units on -> 1 unit = 1 m; origin at the trunk base on the ground (trunk extends 0.25 m below ground for "
                     f"slopes); size {st['dims']} m (X, Y(depth), Z(height) in Blender). Tris {lt}. Contains SM_{vname}_LOD0..2: LODGroup "
                     "0.45/0.15/0.04 then the 2-plane impostor (or cull 0.01); importer Mesh LOD generation OFF. Two material slots: "
                     f"0 = M_{name}_Bark (URP/Lit opaque, tiling UVs: T_{name}_Bark_*), 1 = M_{name}_Leaves (SG_FoliageWind or URP/Lit "
                     f"Alpha Clipping 0.5, Render Face Both, T_{name}_Leaves_*). Vertex colour (import Vertex Colors): R sway weight "
                     "(0 trunk base -> 1 leaf tips), G per-branch phase, B 1 on leaves/fruit (flutter). Leaf-card normals are bent "
                     "outward from the canopy centre: keep Normals = Import (do not recalculate). Collider: capsule on the trunk "
                     "(r 0.25 m olive / 0.12 m pomegranate, height 1.5 m); no canopy collider."})
        rows.append({"manifest_id": a.id, "file": rel(imp), "target_path": rel(imp), "type": "texture_impostor",
                     "import_notes": "Far impostor: orthographic front render of LOD0 on transparent, 512 px square frame centred at "
                     f"half the tree height (frame size = tree max extent x 1.02). sRGB ON, Alpha Is Transparency ON, Max Size 256, "
                     "DXT5 Crunched, mips on (Preserve Coverage, cutoff 0.5). Use on two crossed quads (S9_TownFar / grove far LOD)."})
    # textures once per species
    for kind in ("Bark", "Leaves"):
        for suf in ("BC", "N", "M"):
            p = tdir / f"T_{name}_{kind}_{suf}.png"
            rel = str(p.relative_to(READY))
            if kind == "Bark":
                note = {"BC": "Tiling bark base colour, sRGB ON, Max Size 512, DXT1 Crunched q55, wrap Repeat.",
                        "N": "Tiling bark normal, Normal map, OpenGL (+Y), sRGB OFF, Max Size 512, wrap Repeat.",
                        "M": "Tiling bark mask: R metal 0, G AO, B 0, A smoothness. sRGB OFF, Max Size 512, DXT5 Crunched, wrap Repeat."}[suf]
            else:
                note = {"BC": "Leaf-card atlas (2x2 twigs), RGBA: sRGB ON, Alpha Is Transparency ON, Max Size 1024, DXT5 Crunched q55, mips "
                              "on with Mip Maps Preserve Coverage (alpha cutoff 0.5), wrap Clamp.",
                        "N": "Leaf-card normal, Normal map, OpenGL (+Y), sRGB OFF, Max Size 512, wrap Clamp.",
                        "M": "Leaf mask: R metal 0, G AO, B 0, A smoothness (waxy leaves). sRGB OFF, Max Size 512, DXT5 Crunched, wrap Clamp."}[suf]
            rows.append({"manifest_id": a.id, "file": rel, "target_path": rel, "type": "texture", "import_notes": note})
    for r in rows:
        r["tool"] = "make_tree"
    (RAW / a.id / f"ready_{name}.json").write_text(json.dumps(rows, indent=1))
    (work / "stats.json").write_text(json.dumps(allstats, indent=1))
    if sheets:
        cell = 360
        labels = ["3/4 elevated", "eye height 1.7 m", "back-right", "top", "canopy close-up", "LOD1", "LOD2", "wire LOD0"]
        ref = RAW / a.id / "ref_t1.png"
        im = Image.new("RGB", (cell * 9, (cell + 20) * len(sheets) + 30), (25, 25, 25))
        d = ImageDraw.Draw(im); f = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 13)
        for r_, (var, vname, st) in enumerate(sheets):
            y = r_ * (cell + 20)
            d.text((4, y + 2), f"SM_{vname}  {st}", fill=(255, 255, 120), font=f)
            for c in range(9):
                p = (work / f"r_{var}_v{c}.png") if c < 8 else ref
                if c == 8:
                    imp = tdir / f"T_{vname}_Impostor_BC.png"
                    p = imp if imp.exists() else ref
                if not p.exists():
                    continue
                t = Image.open(p)
                if t.mode == "RGBA":
                    bg = Image.new("RGB", t.size, (150, 170, 200)); bg.paste(t, (0, 0), t); t = bg
                t = t.convert("RGB"); t.thumbnail((cell, cell))
                im.paste(t, (c * cell, y + 20))
                if r_ == 0:
                    pass
        d.text((4, (cell + 20) * len(sheets) + 6), " | ".join(labels + ["impostor"]), fill=(200, 200, 200), font=f)
        outp = RAW / "_review" / f"{a.id}__proc.png"
        im.save(outp)
        print("SHEET", outp)
    print(json.dumps(allstats))


if __name__ == "__main__":
    main()
