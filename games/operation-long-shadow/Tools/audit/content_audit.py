#!/usr/bin/env python3
"""Content audit (CONTENT_AUDIT, after the content phase): every ASSET_MANIFEST id against every
RawAssets/<wp>/ready/import_index*.json, plus files on disk, naming, budgets and a download estimate per
TECH_PLAN §8.10 line split by priority and by "ships / not referenced".

  Tools/.venv/bin/python Tools/audit/content_audit.py            # report to stdout + RawAssets/_audit/content_audit.json
  Tools/.venv/bin/python Tools/audit/content_audit.py --md FILE  # also write the per-id markdown table (for CONTENT_AUDIT.md)

Reuses Tools/build/validate_assets.py (its texture-rule mirror, triangle counter, audio probe and download basis),
so the numbers match `validate_assets.py --ready`; the only difference is that estimates are attributed per file,
which lets this tool split them by manifest priority. Read-only on the ready trees; stdlib + validate_assets
(numpy/Pillow only if --pixels).

Status per manifest id:
  ready        at least one import-index row with its file on disk
  offline      realised without an Assets/ file (voice castings -> the VO lines; Clockmaker tokens stay in
               RawAssets/audio/vo/tokens; UI_FONT_FALLBACK is TMP's bundled LiberationSans)
  editor       procedural, built in the editor by a builder/generator (KitGenerator, BuildingGenerator,
               TerrainBuilder, FXPrefabBuilder, ...): nothing for a content agent to deliver
  cut          intentionally not produced (reason in CUT)
  missing      none of the above
"""
from __future__ import annotations

import argparse
import glob
import json
import os
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/build"))
import validate_assets as va  # noqa: E402

MANIFEST = ROOT / "Docs/ASSET_MANIFEST.json"
OUT_JSON = ROOT / "RawAssets/_audit/content_audit.json"

# Ids delivered by content agents that are not (yet) manifest entries: owner and the priority of what uses them.
EXTRA_IDS = {
    "LOOP_POINTS": ("WP06", "P0", "loop regions of every music/ambience loop (data)"),
    "EXTRA_SFX_CINE_RAMP": ("WP06", "P0", "insertion cinematic ramp (CINEMATIC_DIRECTION)"),
    "EXTRA_SFX_DOWNWASH_SAND": ("WP06", "P0", "rotor downwash sand bed (insertion, LZ)"),
    "EXTRA_SFX_REEL_TO_REEL_LOOP": ("WP06", "P0", "booth reel-to-reel transport loop"),
    "EXTRA_SFX_TAPE_TRANSPORT": ("WP06", "P0", "booth tape transport one-shots"),
    "EXTRA_SFX_WPN_M60D_OVERHEAT": ("WP06", "P0", "door-gun overheat (WP03 request)"),
    "CHP_FLIGHT_HELMET": ("WP05", "P2", "Magpie's SPH-4-style helmet (CHR_MAGPIE is P2)"),
    "CHP_SCARF": ("WP05", "P1", "Legion C olive scarf (CHR_LEGION_C is P1)"),
    "CHP_SUNGLASSES": ("WP05", "P0", "Tinker's aviators (insertion shot I-6) and Harrou"),
}
CUT = {
    "CHR_FALLBACK_MESHY": "P2 fallback only needed if O2 (Rocketbox download) was refused; O2 was approved",
}
# Whole ids that never ship (editor-only sources); their files count as "not referenced".
EDITOR_ONLY_IDS = {
    "CHR_HARROU": "editor-only body for the VID_HARROU_8MM pre-render (WP05_ART); the clip itself ships as video",
}
OFFLINE = {
    "VO_CLOCKMAKER_TOKENS": "RawAssets/audio/vo/tokens/ (offline splice material, never imported; TECH_PLAN §3.12)",
    "UI_FONT_FALLBACK": "TMP Essential Resources LiberationSans SDF (com.unity.ugui), nothing to deliver",
}
# Procedural ids built in the editor: where the builder lives (staged code at audit time, 2026-10-16).
L, K = "Staging/World/Level/", "Staging/World/Look/"
EDITOR_BUILDERS = {
    "PROC_KIT_WALLS": L + "Editor/Kit/KitGenerator.cs, Geometry/Wall*.cs, Kit*.cs (section builders merge in place)",
    "PROC_KIT_ROOFS": L + "Editor/Kit/KitGenerator.cs, Geometry/KitRoofProps.cs, Landmarks.cs",
    "PROC_KIT_INTERIORS": L + "Geometry/Interiors.cs, CinemaGeometry.cs, RelayHallGeometry.cs",
    "PROC_AWNINGS": L + "Geometry/KitStructures.cs + AwningPanel.cs (tear)",
    "PROC_BUILDINGS": L + "Editor/Buildings/BuildingGenerator.cs, Geometry/BuildingGeometry.cs",
    "PROC_FAR_PROXY": L + "Editor/Proxy/FarProxyBuilder.cs",
    "PROC_TERRAIN_S1": L + "Editor/Terrain/TerrainBuilder.cs, Editor/Builders/S1ApproachBuilder.cs",
    "PROC_RIDGE_SILHOUETTE": L + "Editor/Proxy/BackdropBuilder.cs",
    "PROC_SALT_PAN": L + "Editor/Proxy/BackdropBuilder.cs",
    "PROC_MAST": L + "Geometry/KitIndustrial.cs (+ " + K + "Practicals/AviationLights.cs)",
    "PROC_RAZOR_WIRE": L + "Geometry/KitIndustrial.cs",
    "PROC_SQUARE_PROPS": L + "Editor/Builders/S3SquareBuilder.cs, Geometry/KitStreet.cs",
    "PROC_CABLES_LAUNDRY": L + "Geometry/KitRoofProps.cs, Editor/Builders/TownKit.cs",
    "PROC_LAMP_POSTS": L + "Data/LampLayout.cs, Editor/Builders/TownKit.cs",
    "PROC_PITCH_PROPS": L + "Editor/Builders/S7PitchBuilder.cs, Geometry/KitIndustrial.cs",
    "PROC_CHEMLIGHT": "Staging/AI/Squad/Chemlight.cs (runtime emissive stick, WP04)",
    "PROC_FIRE_PROP": K + "Practicals/FireProp.cs + Editor/FXPrefabBuilder.cs (T_VFX_Fire flipbook from WP10_CC0)",
    "PROC_LIGHT_SHAFTS": K + "Atmosphere/LightShaft.cs, DustMotes.cs + Editor/FXPrefabBuilder.cs",
    "PROC_PARACHUTE_FLARE": K + "Editor/FXPrefabBuilder.cs (flare FX), Practicals/FlickerNoise.cs",
    "VFX_ROTOR_DOWNWASH": K + "FX/RotorDownwash.cs + Editor/FXPrefabBuilder.cs (PF_FX_RotorDownwash)",
}

# Rows whose file is not referenced by shipped content (re-bake spares, editor references, licence texts).
UNREF = re.compile(r"(EDITOR-ONLY|Editor-only reference|^EditorOnly|EditorOnly source|for a live TMP re-bake|"
                   r"in-editor re-bake|for an editor re-bake|for live TMP typing|otherwise do not import|"
                   r"never shipped|not referenced at runtime|must not be placed in any scene)", re.I)

OWNER_RULES = [  # TECH_PLAN §11.1 manifest ownership (first match wins)
    (re.compile(r"^(WPN_|PROC_SHELL_CASINGS$)"), "WP03"),
    (re.compile(r"^(CHR_|CHP_|ANIM_|TEX_FABRIC_)"), "WP05"),
    (re.compile(r"^(FAL_SCRAMBLER_BOX|FAL_INTERROGATION_CHAIR|FAL_ROTARY_PHONE|FAL_ASHTRAY|FAL_REEL_TO_REEL|FAL_AV_CART|"
                r"FAL_FILM_CAMERA|PH_TELEVISION_01|PH_TELEVISION_02|PH_WALL_CLOCK|PH_VINTAGE_LIGHTER|"
                r"PH_MOUNTED_FLUORESCENT_LIGHTS|PROC_BOOTH_SHELL|PROC_BOOTH_SMALLS|TEX_ACOUSTIC_TILE|"
                r"TEX_ACG_PAINTEDPLASTER012|VID_.*)$"), "WP09"),
    (re.compile(r"^IMG_CONCEPT_KEYFRAMES$"), "WP10"),
    (re.compile(r"^IMG_"), "WP09"),
    (re.compile(r"^UI_"), "WP08"),
]

# TECH_PLAN §4.2 naming by target folder (validate_assets covers textures/models/VO; this adds the rest).
NAME_RULES = [
    # Single-take cues keep the manifest id as file name (no _nn; WP06 CueNaming parses both); the manifest itself
    # places AMB_CASSETTE_TUNE and STG_LAYERS_TAPE under SFX/SetPieces.
    (re.compile(r"/Audio/SFX/"), re.compile(r"^(SFX|AMB|STG)_[A-Z0-9]+(_[A-Z0-9]+)*?(_FAR)?(_\d{2})?\.wav$"), "SFX_<Group>_<Name>[_<nn>].wav"),
    (re.compile(r"/Audio/Ambience/"), re.compile(r"^AMB_[A-Z0-9_]+_\d{2}\.wav$"), "AMB_<Name>_<nn>.wav"),
    (re.compile(r"/Audio/Music/.*\.wav$"), re.compile(r"^(MUS|STG|NUM|INTERVAL|MOTIF)_[A-Z0-9_]+\.wav$"), "MUS_<Id>_<Layer> / STG_<Name>"),
    (re.compile(r"/Audio/Envelopes/"), re.compile(r"^VO_[A-Z0-9_]+\.json$"), "VO_<LineId>.json"),
    (re.compile(r"/StreamingAssets/video/"), re.compile(r"^VID_[A-Za-z0-9]+\.mp4$"), "VID_<Name>.mp4"),
    (re.compile(r"/UI/Sprites/"), re.compile(r"^T_UI_[A-Za-z0-9_]+\.png$"), "T_UI_<Name>.png"),
    (re.compile(r"/Art/Textures/Images/"), re.compile(r"^T_[A-Za-z0-9_]+_BC\.png$"), "T_<Name>_BC.png"),
]
TYPE_BY_EXT = {".mp4": {"video"}, ".wav": {"audio"}}


def owner_of(mid: str, entry: dict | None) -> str:
    if mid in EXTRA_IDS:
        return EXTRA_IDS[mid][0]
    if entry and entry["category"] in ("sfx", "vo", "music"):
        return "WP06"
    for rx, wp in OWNER_RULES:
        if rx.search(mid):
            return wp
    return "WP10"


def load_rows():
    rows = []
    for f in sorted(glob.glob(str(ROOT / "RawAssets/*/ready/import_index*.json"))):
        root = Path(f).parent
        for i, r in enumerate(json.load(open(f, encoding="utf-8"))):
            r = dict(r)
            r["_index"] = str(Path(f).relative_to(ROOT))
            r["_root"] = root
            r["_src"] = root / r["file"]
            r["_i"] = i
            rows.append(r)
    return rows


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--md", help="write the per-id markdown table to this file")
    ap.add_argument("--pixels", action="store_true", help="also run validate_assets pixel checks")
    ap.add_argument("--json", default=str(OUT_JSON))
    a = ap.parse_args(argv)

    manifest = json.load(open(MANIFEST, encoding="utf-8"))
    by_id = {e["id"]: e for e in manifest}
    rows = load_rows()
    rows_by_id = defaultdict(list)
    for r in rows:
        rows_by_id[r["manifest_id"]].append(r)

    problems = []  # (severity, code, where, message)

    def prob(sev, code, where, msg):
        problems.append((sev, code, where, msg))

    # ---- index integrity
    targets = defaultdict(list)
    for r in rows:
        where = f'{r["_index"]}#{r["_i"]} {r["file"]}'
        for k in ("manifest_id", "file", "target_path", "type", "import_notes"):
            if not r.get(k):
                prob("error", "ROW_FIELD", where, f"empty '{k}'")
        if not r["_src"].is_file():
            prob("error", "ROW_NO_FILE", where, "file missing on disk")
        if r.get("file") != r.get("target_path"):
            prob("error", "ROW_TARGET", where, f'file != target_path ({r.get("target_path")})')
        tp = r.get("target_path", "")
        if not (tp.startswith("Assets/_Game/") or tp.startswith("Assets/StreamingAssets/video/")):
            prob("error", "ROW_ROOT", where, "target outside Assets/_Game and StreamingAssets/video")
        ext = Path(tp).suffix.lower()
        if ext in TYPE_BY_EXT and r.get("type") not in TYPE_BY_EXT[ext]:
            prob("warning", "ROW_TYPE", where, f'type "{r.get("type")}" for a {ext} file')
        if r["manifest_id"] not in by_id and r["manifest_id"] not in EXTRA_IDS:
            prob("warning", "ROW_UNKNOWN_ID", where, "manifest_id not in ASSET_MANIFEST.json")
        targets[tp].append(r)
        name = Path(tp).name
        for frx, nrx, want in NAME_RULES:
            if frx.search(tp) and not nrx.match(name) and not UNREF.search(r.get("import_notes", "")):
                prob("warning", "NAMING", where, f"expected {want}")
    for tp, rs in targets.items():
        if len(rs) > 1:
            prob("error", "ROW_DUP_TARGET", tp, "indexed by " + ", ".join(f'{r["_index"]}:{r["manifest_id"]}' for r in rs))
    indexed = {r["_src"].resolve() for r in rows}
    for base in sorted({r["_root"] for r in rows}):
        for dp, _, fns in os.walk(base / "Assets"):
            for fn in fns:
                if fn.startswith(".") or fn.endswith(".meta"):
                    continue
                p = (Path(dp) / fn).resolve()
                if p not in indexed:
                    prob("warning", "FILE_UNINDEXED", str(p.relative_to(ROOT)), "file in a ready tree without an index row")

    # ---- validate_assets per file (issues + attributed download estimate)
    class Args:
        pixels = a.pixels
    v = va.Validator(Args())
    est_by_src = {}
    issues_by_target = defaultdict(list)
    items = va.collect(type("A", (), {"path": None, "ready": []})())
    for it in items:
        n0, i0 = len(v.report.estimates), len(v.report.issues)
        ext = Path(it.target).suffix.lower()
        if ext in va.TEXTURE_EXT:
            v.check_texture(it)
        elif ext in va.MODEL_EXT:
            v.check_model(it)
        elif ext in va.AUDIO_EXT:
            v.check_audio(it)
        else:
            v.check_other(it)
        est_by_src[it.source.resolve()] = [(e.line, e.bytes) for e in v.report.estimates[n0:]]
        for iss in v.report.issues[i0:]:
            issues_by_target[iss.path].append(iss)
    v.finish_texture_sets()
    va.check_specs(v.report)
    va_issues = [i for i in v.report.issues]

    # ---- §8.9 per-file download caps for audio, images and video (estimate attributed to each file)
    AUDIO_CAPS = [  # (folder regex, cap bytes, label)
        (re.compile(r"/Audio/SFX/(Weapons|Impacts|Foley|Footsteps|Explosions|Whizz|Synth|UI)(/|$)"), 30_000, "SFX one-shot <= 30 KB"),
        (re.compile(r"/Audio/Music/"), 1_300_000, "music cue <= 1.3 MB"),
        (re.compile(r"/Audio/Ambience/"), 450_000, "ambience loop <= 0.45 MB"),
        (re.compile(r"/Art/Textures/Images/"), 500_000, "CRT/intel image <= 0.5 MB"),
    ]
    vo_chain = defaultdict(list)
    for r in rows:
        tp = r["target_path"]
        b = sum(x[1] for x in est_by_src.get(r["_src"].resolve(), []))
        m = re.search(r"/Audio/VO/([A-Z_]+)/", tp)
        if m:
            vo_chain[m.group(1)].append(b)
            continue
        for rx, cap, label in AUDIO_CAPS:
            loop = tp.endswith(".wav") and bool((va.probe_wav(r["_src"]) or {}).get("smpl"))
            if rx.search(tp) and b > cap and not loop and not UNREF.search(r.get("import_notes", "")):
                prob("info", "FILE_DL_CAP", tp, f"estimated {b / 1e3:.0f} KB > {label} (§8.9)")
        if tp.endswith(".mp4"):
            size = r["_src"].stat().st_size
            if size > 14_000_000:
                prob("error", "VIDEO_SIZE", tp, f"{size / 1e6:.1f} MB > 14 MB")
    allvo = [b for bs in vo_chain.values() for b in bs]
    vo_avg = sum(allvo) / max(1, len(allvo))
    vo_note = f"VO: {len(allvo)} clips, {sum(allvo) / 1e6:.1f} MB, average {vo_avg / 1e3:.1f} KB per clip (§8.9 cap: 30 KB average); " + \
        ", ".join(f"{c} {sum(bs) / max(1, len(bs)) / 1e3:.0f} KB" for c, bs in sorted(vo_chain.items()))
    if vo_avg > 30_000:
        prob("warning", "VO_AVG_CAP", "Audio/VO", vo_note)

    # ---- per-id status
    table = []
    est = defaultdict(float)  # (line, priority, ships) -> bytes
    for e in manifest + [{"id": k, "category": "extra", "priority": p, "source": "content agent", "target_path": "",
                          "model_or_asset_id": n} for k, (_, p, n) in EXTRA_IDS.items()]:
        mid, pri = e["id"], e["priority"]
        rs = rows_by_id.get(mid, [])
        ok = [r for r in rs if r["_src"].is_file()]
        mb_ship = mb_unref = 0.0
        for r in ok:
            unref = bool(UNREF.search(r.get("import_notes", ""))) or mid in EDITOR_ONLY_IDS
            for line, b in est_by_src.get(r["_src"].resolve(), []):
                est[(line, pri, "unref" if unref else "ships")] += b
                if unref:
                    mb_unref += b / 1e6
                else:
                    mb_ship += b / 1e6
        if ok:
            status = "ready"
        elif mid in CUT:
            status = "cut"
        elif mid in OFFLINE or mid.startswith("VOICE_"):
            status = "offline"
        elif e.get("source") == "procedural" or (e.get("model_or_asset_id") or "").startswith(("KitGenerator", "SignBaker")) \
                or mid.startswith("VFX_"):
            status = "editor"
        else:
            status = "missing"
        if status == "missing" and pri in ("P0", "P1"):
            prob("error" if pri == "P0" else "warning", "ID_MISSING", mid, f"{pri} {e['category']} has no import-ready output")
        folders = sorted({str(Path(r["file"]).parent) for r in ok})
        idxs = sorted({r["_index"].split("/")[1] for r in ok})
        if status == "offline":
            where = OFFLINE.get(mid, "realised by the VO_* lines of this voice (casting entry; no file of its own)")
        elif status == "cut":
            where = CUT[mid]
        elif status == "editor":
            where = f'built in the editor: {EDITOR_BUILDERS.get(mid, e.get("model_or_asset_id") or "editor")} -> {e.get("target_path")}'
        else:
            where = "; ".join(folders[:3]) + (f" (+{len(folders) - 3} more)" if len(folders) > 3 else "")
        table.append({"id": mid, "category": e["category"], "priority": pri, "owner": owner_of(mid, e if e["category"] != "extra" else None),
                      "status": status, "files": len(ok), "ready_trees": idxs, "where": where,
                      "mb_ships": round(mb_ship, 3), "mb_unref": round(mb_unref, 3)})

    # ---- report
    budget_mb = va.BUDGETS.get("download", {}).get("budget_mb", {})
    lines = sorted({k[0] for k in est} | set(budget_mb))
    est_table = []
    for line in lines:
        p0 = est.get((line, "P0", "ships"), 0) / 1e6
        p1 = est.get((line, "P1", "ships"), 0) / 1e6
        p2 = est.get((line, "P2", "ships"), 0) / 1e6
        un = sum(est.get((line, p, "unref"), 0) for p in ("P0", "P1", "P2")) / 1e6
        est_table.append({"line": line, "P0": round(p0, 2), "P1": round(p1, 2), "P2": round(p2, 2), "unref": round(un, 2),
                          "budget": budget_mb.get(line)})
    st = Counter((t["priority"], t["status"]) for t in table)
    out = {"manifest_entries": len(manifest), "rows": len(rows), "status_counts": {f"{k[0]} {k[1]}": n for k, n in sorted(st.items())},
           "problems": [dict(zip(("severity", "code", "where", "message"), p)) for p in problems],
           "validate_assets_issues": [i.__dict__ for i in va_issues if i.severity in ("error", "warning")],
           "estimate_mb": est_table, "ids": table}
    jp = Path(a.json)
    jp.parent.mkdir(parents=True, exist_ok=True)
    jp.write_text(json.dumps(out, indent=1), encoding="utf-8")

    print(f"manifest {len(manifest)} entries (+{len(EXTRA_IDS)} extra ids), {len(rows)} index rows")
    for pri in ("P0", "P1", "P2"):
        print(f"  {pri}: " + ", ".join(f"{s} {st[(pri, s)]}" for s in ("ready", "offline", "editor", "cut", "missing") if st[(pri, s)]))
    pc = Counter((p[0], p[1]) for p in problems)
    print("problems:", dict(pc) or "none")
    for p in problems:
        if p[0] == "error" or p[1] not in ("FILE_UNINDEXED",):
            print(f"  {p[0].upper():7} {p[1]:16} {p[2]}  {p[3]}")
    vc = Counter((i.severity, i.code) for i in va_issues)
    print(vo_note)
    print("validate_assets:", {f"{s} {c}": n for (s, c), n in sorted(vc.items())})
    print("\ndownload estimate (MB, upper bound per validate_assets basis): line | P0 | P1 | P2 | not referenced | budget")
    tot = Counter()
    for r in est_table:
        print(f"  {r['line']:<38} {r['P0']:7.1f} {r['P1']:6.1f} {r['P2']:6.1f} {r['unref']:6.1f}   {r['budget']}")
        for k in ("P0", "P1", "P2", "unref"):
            tot[k] += r[k]
    print(f"  {'TOTAL content':<38} {tot['P0']:7.1f} {tot['P1']:6.1f} {tot['P2']:6.1f} {tot['unref']:6.1f}")
    for t in table:
        if t["status"] == "missing":
            print("  MISSING", t["priority"], t["id"], t["where"])
    print("\nheaviest ids (MB that ships):")
    for t in sorted(table, key=lambda t: -t["mb_ships"])[:30]:
        print(f"  {t['mb_ships']:6.2f}  {t['priority']}  {t['id']}")

    if a.md:
        md = ["| Manifest id | Cat | Pri | Owner | Status | Files | MB (ships) | Import-ready path / note |", "|---|---|---|---|---|---|---|---|"]
        order = {"P0": 0, "P1": 1, "P2": 2}
        for t in sorted(table, key=lambda t: (order[t["priority"]], t["owner"], t["category"], t["id"])):
            ready_root = ", ".join(f"RawAssets/{x}/ready" for x in t["ready_trees"])
            where = (f"`{ready_root}/` → " if ready_root else "") + t["where"].replace("|", "/")
            md.append(f"| {t['id']} | {t['category']} | {t['priority']} | {t['owner']} | {t['status']} | {t['files']} | "
                      f"{t['mb_ships']:.2f} | {where} |")
        Path(a.md).write_text("\n".join(md) + "\n", encoding="utf-8")
        print("wrote", a.md)
    return 1 if any(p[0] == "error" for p in problems) else 0


if __name__ == "__main__":
    sys.exit(main())
