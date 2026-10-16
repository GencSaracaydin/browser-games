#!/usr/bin/env python3
"""Assemble RawAssets/{wp09,wp08}/ready/import_index.json (+ materials.json) from the per-tool rows in ready/rows/,
validate that every file exists, list orphans, and print a budget report (tris from FBX rows, texture sizes, MB).

  Tools/.venv/bin/python Tools/images/wp09_index.py
"""
import json, pathlib, re
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
VID = {
    "Assets/StreamingAssets/video/VID_Harrou8mm.mp4": ("VID_HARROU_8MM",
        "Intro shot 5c on CRT5: 8.0 s, 18 fps, 512x384 H.264 Main, no audio, ~1.1 MB. Faded Super 8: Harrou (face scratched out of the emulsion for the whole shot) "
        "walks the Legion line in Clock Square; gate weave, flicker, dust, scratches baked in. Produced OFFLINE (fal Kling v3 Pro image-to-video from IMG_HARROU_STILL + "
        "Tools/images/wp09_video.py) instead of the D14 Unity Recorder pass, so it does not need S3_Square or the Legion characters. Stays in StreamingAssets "
        "(VideoPlayer.url = Application.streamingAssetsPath + '/video/VID_Harrou8mm.mp4'); render into RT_CRT5 through SG_CRTScreen. Loop off; hold the last frame."),
    "Assets/StreamingAssets/video/VID_GunCam.mp4": ("VID_GUNCAM",
        "Outro shot 3 on CRT2 (pre-render fallback per D14, usable as the primary): 12.0 s, 24 fps, 512x384 H.264, no audio, ~1.4 MB, monochrome video-tube look. "
        "0-4.2 s low flight over the town (camera fixed to the airframe beside the open cabin door), 4.2-4.6 s an RPG smoke trail streaks up and passes just under the skid, "
        "shake/lurch to ~5.8 s, 9.1 s ONE muzzle flash lights the cabin interior (2 frames + phosphor smear), then flight to the end. Tint green in SG_GunCam "
        "(reference grade RawAssets/wp09/VID_GUNCAM/green_reference.png). Clean plate from fal Kling v3 Pro; RPG/flash/tube look composited in Tools/images/wp09_video.py."),
    "Assets/StreamingAssets/video/VID_FilmLeader.mp4": ("IMG_FILM_LEADER", None),
}


def tex_info(p):
    try:
        im = Image.open(p); return f"{im.size[0]}x{im.size[1]} {im.mode}"
    except Exception:
        return ""


def build(wp):
    ready = ROOT / f"RawAssets/{wp}/ready"
    rows, mats = [], []
    for f in sorted((ready / "rows").glob("*.json")):
        data = json.loads(f.read_text())
        (mats if "materials" in f.name else rows).extend(data)
    if wp == "wp09":
        have = {r["file"] for r in rows}
        for rel, (mid, note) in VID.items():
            if (ready / rel).exists() and rel not in have and note:
                rows.append({"manifest_id": mid, "file": rel, "target_path": rel, "type": "video", "import_notes": note})
    out, missing, seen = [], [], set()
    for r in rows:
        r = {k: r[k] for k in ("manifest_id", "file", "target_path", "type", "import_notes") if k in r} | {k: v for k, v in r.items() if k == "manifest_target_path"}
        if not (ready / r["file"]).exists():
            missing.append(r["file"]); continue
        if r["file"] in seen:
            continue
        if r["file"].endswith(".mp4"):
            r["type"] = "video"  # CONTENT_AUDIT: StreamingAssets clips are video rows, never textures
        seen.add(r["file"])
        if r["file"].endswith("SM_ScramblerBox.fbx"):
            r["import_notes"] = r["import_notes"].replace("pivot bottom centre.", "pivot = bottom of the box at the centre of the whole bounds (the box itself spans Unity x -0.18..+0.08; the coiled cord lies on the -X side and dips 0.024 m below the pivot, i.e. into the trolley top: hide it there or nudge the prop up 5 mm).")
        if r["file"].endswith("SM_AVCartLong.fbx"):
            r["import_notes"] = r["import_notes"].replace("T_AVCartLong_", "T_AVCart_")
            r["import_notes"] += (" TEXTURES: SM_AVCartLong reuses T_AVCart_{BC,N,M} (same UVs; the long variant is the scan stretched in the middle "
                                  "band only, keep 0.12 m rigid at each end) -> assign M_AVCart (or M_AVCartLong from materials.json, same maps). "
                                  "Use: the 1.7 m booth CRT cart at local (0, 0, 1.25); SM_AVCart (1.13 m, top 0.85 m) is the scrambler trolley.")
        out.append(r)
    for r in out:
        p = ready / r["file"]
        r["bytes"] = p.stat().st_size
        if r["type"] in ("texture", "sprite"):
            r["pixels"] = tex_info(p)
    files_on_disk = {str(p.relative_to(ready)) for p in (ready / "Assets").rglob("*") if p.is_file()}
    orphans = sorted(files_on_disk - {r["file"] for r in out})
    (ready / "import_index.json").write_text(json.dumps(out, indent=1))
    if mats:
        (ready / "materials.json").write_text(json.dumps(mats, indent=1))
    tot = sum(r["bytes"] for r in out)
    print(f"[{wp}] rows {len(out)}  materials {len(mats)}  files {tot / 1e6:.1f} MB  missing {missing}  orphans {orphans}")
    return out


if __name__ == "__main__":
    for wp in ("wp09", "wp08"):
        build(wp)
