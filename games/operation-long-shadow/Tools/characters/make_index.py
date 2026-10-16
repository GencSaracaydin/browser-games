#!/usr/bin/env python3
"""Merge the per-step index rows into RawAssets/wp05/ready/import_index.json and run the WP05 budget checks.

  Tools/.venv/bin/python Tools/characters/make_index.py [--stats RawAssets/wp05/_work/fbx_stats.json]

Inputs: RawAssets/wp05/_work/index/*.json (written by build_bodies.py, build_clips.py, package_local_clips.py).
* drops rows whose file is no longer in ready/ (e.g. clips that were dropped), de-duplicates by file (last wins)
* applies the in-game max texture size of the manifest budget (enemy/booth/pilot sets "all sets 512": the 1024 source
  files ship as "512 in game (1024 source)", like the CHP_* props)
* reports ready files without a row, estimated download per character (crunched texture sizes from TECH_PLAN §8.9 +
  mesh estimate), and tris per FBX when --stats (from Tools/characters/blender/fbx_stats.py) is given
Writes ready/import_index.json and RawAssets/wp05/_work/budget_report.json.
"""
import argparse, json, pathlib, re

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
W5 = ROOT / "RawAssets/wp05"
READY = W5 / "ready"
IDX = W5 / "_work/index"

# manifest budget "all sets 512" -> in-game max size 512 for these texture folders (source files stay 1024)
GAME_512 = {"LegionA", "LegionB", "LegionC", "GreycoatA", "GreycoatB", "Interrogator", "Magpie"}
# crunched download per texture set (BC+N+M) from TECH_PLAN §8.9 (webgl.md §3.7), per single map ~1/3
SET_MB = {2048: 3.5, 1024: 0.87, 512: 0.23, 256: 0.06}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--stats")
    a = ap.parse_args()
    rows = {}
    for f in sorted(IDX.glob("*.json")):
        for r in json.loads(f.read_text()):
            rows[r["file"]] = r
    # fabric tiles (TEX_FABRIC_*, process_fabrics.py) have no step index: rows are generated here
    fab_ids = {"TigerStripe": "TEX_FABRIC_TIGERSTRIPE", "KhakiDrill": "TEX_FABRIC_KHAKI_DRILL", "OliveSateen": "TEX_FABRIC_OLIVE_SATEEN",
               "GreyGreen": "TEX_FABRIC_GREYGREEN", "TanPoplin": "TEX_FABRIC_TAN_SHIRT", "OrangeArmband": "TEX_FABRIC_ORANGE_ARMBAND"}
    for f in sorted((READY / "Assets/_Game/Art/Textures/Characters/Fabrics").glob("T_Fabric_*.png")):
        name, kind = f.stem[len("T_Fabric_"):].rsplit("_", 1)
        rel = str(f.relative_to(READY))
        base = (f"Seamless 1024 fabric tile (fal-ai/patina/material, processed by Tools/characters/process_fabrics.py; ~0.4 m of "
                f"cloth per tile). Wrap Repeat, mipmaps on, max size 1024, Crunch q70. Already BAKED into every character atlas by "
                f"retexture_rocketbox.py, so no shipped character material needs it: keep it out of the build unless a runtime "
                f"material (e.g. a cloth detail map, a dropped-kit prop or a WP10 awning/tarp) references it (Unity only ships "
                f"referenced textures). ")
        note = {"BC": "Texture Type Default, sRGB ON.",
                "N": "Texture Type Normal map (OpenGL +Y, no flip), sRGB OFF.",
                "M": "Mask, sRGB OFF: R metallic 0, G occlusion 1, B 0, A smoothness (1 - roughness)."}[kind]
        rows.setdefault(rel, {"manifest_id": fab_ids.get(name, "TEX_FABRIC_" + name.upper()), "file": rel, "target_path": rel,
                              "type": "texture_tile", "import_notes": base + note})
    kept, dropped = [], []
    for fn, r in rows.items():
        if (READY / fn).exists():
            kept.append(r)
        else:
            dropped.append(fn)
    # ledger-only set ids -> manifest ids (set B was bought as two fal calls: ANIM_MESHY_SET_B + ANIM_MESHY_SET_B2)
    for r in kept:
        if r["manifest_id"] == "ANIM_MESHY_SET_B2":
            r["manifest_id"] = "ANIM_MESHY_SET_B"
            r["import_notes"] += " (Bought in the second Set B call, ledger id ANIM_MESHY_SET_B2.)"
    # in-game sizes for the 512 sets
    for r in kept:
        m = re.match(r"Assets/_Game/Art/Textures/Characters/([^/]+)/", r["file"])
        if r["type"] == "texture" and m and m.group(1) in GAME_512:
            r["import_notes"] = re.sub(r"max size \d+", "max size 512 in game (1024 source; manifest budget 'all sets 512', "
                                       "raise to 1024 only if BudgetReport shows headroom under 220 MB)", r["import_notes"], count=1)
    for r in kept:  # hair cards: no shadow casting (alpha-clipped cards shadowing the scalp read as black streaks)
        if re.search(r"T_\w+_Hair_BC\.png$", r["file"]) and "ShadowCaster" not in r["import_notes"]:
            r["import_notes"] += (" Hair material: URP/Lit Alpha Clipping 0.35, Render Face Both, and disable its ShadowCaster pass "
                                  "(material.SetShaderPassEnabled(\"ShadowCaster\", false)): self-shadowing hair cards show as dark streaks.")
    kept.sort(key=lambda r: (r["type"] != "model_skinned_humanoid", r["file"]))
    files = {r["file"] for r in kept}
    orphans = [str(p.relative_to(READY)) for p in READY.rglob("*") if p.is_file() and p.name != "import_index.json"
               and str(p.relative_to(READY)) not in files]
    (READY / "import_index.json").write_text(json.dumps(kept, indent=1))
    # ---- budget estimate per character folder
    from PIL import Image
    tex_root = READY / "Assets/_Game/Art/Textures/Characters"
    report = {"rows": len(kept), "dropped_rows": dropped, "orphans": orphans, "characters": {}}
    for d in sorted(p for p in tex_root.iterdir() if p.is_dir() and p.name not in ("Props", "Fabrics")):
        mb = 0.0
        for t in d.glob("T_*_BC.png"):
            sz = Image.open(t).size[0]
            if d.name in GAME_512:
                sz = min(sz, 512)
            mb += SET_MB.get(sz, 0.87)
        report["characters"][d.name] = {"textures_mb_est": round(mb, 2)}
    props_mb = sum(SET_MB.get(Image.open(t).size[0], 0.23) for t in (tex_root / "Props").glob("*/T_*_BC.png"))
    report["props_textures_mb_est"] = round(props_mb, 2)
    if a.stats:
        st = json.loads(pathlib.Path(a.stats).read_text())
        report["fbx"] = st
        for name, s in st.items():
            folder = s.get("folder")
            if folder in report["characters"] and s.get("type") == "skinned":
                c = report["characters"][folder]
                c.setdefault("models", {})[name] = {"tris": s["tris"], "mesh_mb_est": round(s["verts"] * 64 / 1e6 * 0.55, 2)}
    total = sum(c["textures_mb_est"] + sum(m["mesh_mb_est"] for m in c.get("models", {}).values())
                for k, c in report["characters"].items() if k != "Harrou")
    report["characters_total_mb_est_shipped"] = round(total + props_mb, 2)
    (W5 / "_work/budget_report.json").write_text(json.dumps(report, indent=1))
    print(f"{len(kept)} rows -> {READY / 'import_index.json'}; dropped {len(dropped)}; orphans {len(orphans)}")
    for o in orphans[:20]:
        print("  ORPHAN", o)
    for k, c in report["characters"].items():
        print(f"  {k:14s} tex {c['textures_mb_est']:5.2f} MB", {m: v["tris"] for m, v in c.get("models", {}).items()})
    print("  props textures", report["props_textures_mb_est"], "MB; characters total (shipped, est.)", report["characters_total_mb_est_shipped"], "MB")


if __name__ == "__main__":
    main()
