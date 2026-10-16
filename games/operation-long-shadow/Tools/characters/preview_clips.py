#!/usr/bin/env python3
"""Render retarget previews (Calder body + rifle stick) for a set of clip FBX files and a contact sheet.
  Tools/.venv/bin/python Tools/characters/preview_clips.py <out_dir> <map> <clip.fbx> [<clip.fbx> ...]
Writes <out_dir>/<clip>_sheet.png per clip, <out_dir>/all_sheet.png and <out_dir>/metrics_summary.json."""
import json, pathlib, subprocess, sys
ROOT = pathlib.Path(__file__).resolve().parents[2]
B = "/Applications/Blender.app/Contents/MacOS/Blender"
out = pathlib.Path(sys.argv[1]); mp = sys.argv[2]; clips = sys.argv[3:]
out.mkdir(parents=True, exist_ok=True)
M = ROOT / "RawAssets/wp05/_work/Calder/master"
target = ROOT / "RawAssets/wp05/_work/Calder/test/SK_Calder.fbx"
tex = {"M_Calder_Body": {"bc": str(M / "T_Calder_Body_BC.png"), "n": str(M / "T_Calder_Body_N.png"), "m": str(M / "T_Calder_Body_M.png")},
       "M_Calder_Head": {"bc": str(M / "T_Calder_Head_BC.png"), "n": str(M / "T_Calder_Head_N.png"), "m": str(M / "T_Calder_Head_M.png")}}
summary = {}
sheets = []
for c in clips:
    name = pathlib.Path(c).stem
    spec = {"target_fbx": str(target), "textures": tex, "source_fbx": str(c), "map": mp, "frames_frac": [0.0, 0.33, 0.66, 1.0],
            "views": [["q", 35, 6, -45, 0.95]], "out": str(out / name), "stick": mp != "rocketbox", "res": [360, 480]}
    sp = out / f"{name}.json"
    sp.write_text(json.dumps(spec))
    r = subprocess.run([B, "-b", "--factory-startup", "-P", str(ROOT / "Tools/characters/blender/retarget.py"), "--", str(sp)], capture_output=True, text=True)
    if "DONE" not in r.stdout:
        print("FAIL", c, r.stdout[-800:], r.stderr[-800:]); continue
    files = json.loads((out / f"{name}_files.json").read_text())
    subprocess.run([str(ROOT / "Tools/.venv/bin/python"), str(ROOT / "Tools/characters/sheet.py"), str(out / f"{name}_sheet.png"), "4"] + files, capture_output=True)
    m = json.loads((out / f"{name}_metrics.json").read_text())["metrics"]
    d = [x["rh_lh_m"] for x in m]
    summary[name] = {"frames": len(m), "rh_lh_min": min(d), "rh_lh_max": max(d), "rh_lh_mean": round(sum(d) / len(d), 3)}
    sheets.append(str(out / f"{name}_sheet.png"))
    print(name, summary[name])
(out / "metrics_summary.json").write_text(json.dumps(summary, indent=1))
subprocess.run([str(ROOT / "Tools/.venv/bin/python"), str(ROOT / "Tools/characters/sheet.py"), str(out / "all_sheet.png"), "1"] + sheets + ["--label"])
