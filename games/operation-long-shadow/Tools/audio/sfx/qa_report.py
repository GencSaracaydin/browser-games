"""Objective QA of every import-ready WP06_SFX file (O4: no human listen gate; measurements + machine listen).

Measures each file in RawAssets/audio/ready/import_index_sfx.json from disk and checks:
  - true peak <= -1.0 dBTP, no clipped samples, |DC| < 0.002
  - channels: SFX/** mono; Ambience + EL music stereo (procedural Music cues may be mono)
  - lead (onset 40 dB under peak) <= 10 ms for fast one-shots (Weapons, Impacts, Footsteps, Whizz, UI, Explosions)
  - loops: seam metrics of the loop body from LOOP_POINTS.json (click, RMS step and spectral step vs the interior)
  - persistent HF tone artefact (> 45 dB) in one-shots
  - level vs the category target recorded at build time (build_report_sfx.json)
  - guardrails: machine-listen answers on the selected source takes (ambience/music prompts)
  - download estimate (AAC at an assumed bitrate per folder; the real size comes from the build's BudgetReport)
Writes RawAssets/audio/sfx/_qa/qa_report.json and RawAssets/audio/sfx/_qa/qa_summary.md.

usage: python qa_report.py
"""
from __future__ import annotations

import collections
import json
import pathlib
import sys

import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import alib as A  # noqa: E402

ROOT = pathlib.Path("/Users/gencsaracaydin/Documents/fun/fun-unity")
READY = ROOT / "RawAssets/audio/ready"
QA = ROOT / "RawAssets/audio/sfx/_qa"
FAST = ("SFX/Weapons", "SFX/Impacts", "SFX/Footsteps", "SFX/Whizz", "SFX/UI", "SFX/Explosions")
# AAC kbps per channel by import quality (WP00B decision: Web = AAC 44.1 kHz, validate_assets.py assumes 56/64/80 kbps
# per channel at q50/60/70): Ambience q50, Music/SetPieces/UI q60, other SFX q70
KBPS_CH = {"Ambience": 56, "Music": 64, "SFX/SetPieces": 64, "SFX/UI": 64}


# by design: zone tails are reflections only (the first echo arrives 10-500 ms after the shot, which plays the direct
# sound); a flyby builds up as the rocket approaches
LEAD_EXEMPT = ("weapon_tail", "weapon_mech")   # mech: firing-pin click masked by design (layer balance)
LEAD_EXEMPT_IDS = ("SFX_RPG_FLYBY",)
# one-shots that are long by design (set pieces, explosions with their far bake, siren, rotor passes)
LONG_KINDS = ("explosion_far",)
# square-wave / beep UI sounds are tonal by design (the HF-tone check looks for model whine in noisy one-shots)
TONAL_BY_DESIGN = ("SFX_WPN_SHELLS",  # brass casing ring: the clip's spectral maximum, starts at the impact
                   "SFX_UI_OBJECTIVE", "SFX_UI_MENU_SELECT", "SFX_UI_MENU_MOVE", "SFX_LEADER_BEEP", "SFX_TINNITUS", "SFX_CRT_WHINE")


def barline_seam(body, bar_s, sr=48000):
    m = A.mono(body)[:, 0]
    w = int(0.05 * sr)

    def pair(a, b):
        fa = np.abs(np.fft.rfft(a * np.hanning(len(a))))
        fb = np.abs(np.fft.rfft(b * np.hanning(len(b))))
        return (float(np.mean(np.abs(A.db(fa + 1e-9) - A.db(fb + 1e-9)))),
                abs(float(A.db(np.sqrt(np.mean(a ** 2)) + 1e-12) - A.db(np.sqrt(np.mean(b ** 2)) + 1e-12))))
    bar = int(bar_s * sr)
    bl = [pair(m[i - w:i], m[i:i + w]) for i in range(bar, len(m) - w, bar)]
    spec, step = pair(m[-w:], m[:w])
    if not bl:
        return {}
    p90f = float(np.percentile([b[0] for b in bl], 90))
    p90s = float(np.percentile([b[1] for b in bl], 90))
    clk = A.seam_metrics(body)["click_ratio"]
    return dict(barline_spec_p90_db=round(p90f, 2), barline_rms_step_p90_db=round(p90s, 2),
                barline_ok=bool(clk < 2.5 and spec <= p90f + 1.0 and step <= p90s + 1.0))


def folder_of(f):
    parts = f.split("/")
    return "/".join(parts[3:-1])


def hf_tone(x):
    from scipy import signal as sg
    m = A.mono(x)[: int(0.5 * A.SR), 0]
    if len(m) < 4096:
        return 0.0
    f, _t, S = sg.spectrogram(m, A.SR, nperseg=2048, noverlap=1536)
    rel = S / (np.median(S, axis=0, keepdims=True) + 1e-20)
    pers = np.median(rel, axis=1)
    band = (f > 6000) & (f < 20000)
    return float(10 * np.log10(pers[band].max() + 1e-20))


def main():
    idx = json.loads((READY / "import_index_sfx.json").read_text())
    loops = json.loads((READY / "Assets/_Game/Audio/Music/LOOP_POINTS.json").read_text())
    build = json.loads((QA / "build_report_sfx.json").read_text()).get("files", {})
    scan = json.loads((QA / "scan.json").read_text())
    by_src = {str(ROOT / k): v for k, v in scan.items()}
    rows, fails = [], collections.defaultdict(list)
    for r in idx:
        f = r["file"]
        if not f.endswith(".wav"):
            continue
        x, sr = A.load(READY / f)
        m = A.measure(x)
        fo = folder_of(f)
        row = dict(file=f, manifest_id=r["manifest_id"], folder=fo, dur_s=m["dur_s"], ch=m["ch"], sr=sr,
                   true_peak_dbtp=m["true_peak_dbtp"], peak_dbfs=m["peak_dbfs"], clipped=m["clipped"],
                   lufs_i=m["lufs_i"], mmax400=m["lufs_m_max"], lead_ms=m["lead_ms"], dc=m["dc"],
                   bytes_wav=(READY / f).stat().st_size)
        row["mmax100"] = round(A.momentary_max(x, win=0.1, hop=0.01), 1) if m["dur_s"] < 30 else None
        kb = KBPS_CH.get(fo, KBPS_CH.get(fo.split("/")[0], 80)) * m["ch"]
        row["est_download_kb"] = round(m["dur_s"] * kb / 8, 1)
        b = build.get(f, {})
        row["kind"] = b.get("kind")
        row["level_target"] = b.get("level_target")
        lp = loops.get(f)
        if lp:
            body = x[lp["loop_start_sample"]:lp["loop_end_sample"]]
            row["seam"] = A.seam_metrics(body)
            row["loop_s"] = lp["loop_seconds"]
            if lp.get("bar_seconds"):
                # music: a bar-line loop is judged against the cue's own bar lines (a random interior cut point is not
                # a fair reference for a cut that lands on a chord change)
                row["seam"].update(barline_seam(body, lp["bar_seconds"]))
        elif m["dur_s"] < 3:
            row["hf_tone_db"] = round(hf_tone(x), 1)
        # guardrail answers of the source takes
        g = []
        for s in b.get("sources", []):
            sv = by_src.get(str(ROOT / s)) or by_src.get(s)
            if sv:
                for l in sv.get("listen", []):
                    if l["kind"] == "guardrail":
                        g.append(dict(src=pathlib.Path(s).name, yes=l.get("yes", [])))
        if g:
            row["guardrail"] = g
        # checks
        chk = []
        if m["true_peak_dbtp"] > -1.0:
            chk.append("true_peak")
        if m["clipped"]:
            chk.append("clipped")
        if abs(m["dc"]) > 0.002:
            chk.append("dc")
        if fo.startswith("SFX") and m["ch"] != 1:
            chk.append("not_mono")
        if fo in ("Ambience",) and m["ch"] != 2:
            chk.append("not_stereo")
        if fo.startswith(FAST) and not lp and m["lead_ms"] > 10 and row.get("kind") not in LEAD_EXEMPT \
                and r["manifest_id"] not in LEAD_EXEMPT_IDS:
            chk.append("lead")
        seam_ok = row["seam"].get("barline_ok", row["seam"]["seam_ok"]) if lp else True
        if lp and not seam_ok and row.get("kind") not in ("synth_loop",):
            chk.append("seam")
        if fo.startswith(("SFX/Footsteps", "SFX/Impacts")):
            env = A.envelope(x, 2)
            row["peak_ms"] = round(float(np.argmax(env)) / sr * 1000, 1)
            # first event within 12 dB of the peak (a heel-toe step peaks on the toe; the heel must start the clip)
            row["first_event_ms"] = round(float(np.argmax(env > env.max() * A.undb(-12))) / sr * 1000, 1)
            if row["first_event_ms"] > 45:
                chk.append("late_event")
        # per-file download budgets (TECH_PLAN §8.9)
        if fo.startswith("SFX") and fo != "SFX/SetPieces" and not lp and row["est_download_kb"] > 30 and row.get("kind") not in LONG_KINDS:
            chk.append("oneshot_budget")
        if fo == "Ambience" and row["est_download_kb"] > 450:
            chk.append("ambience_budget")
        if fo == "Music" and row["est_download_kb"] > 1300:
            chk.append("music_budget")
        if row.get("hf_tone_db", 0) > 45 and r["manifest_id"] not in TONAL_BY_DESIGN:
            chk.append("hf_tone")
        if any(z["yes"] for z in g):
            chk.append("guardrail_yes")
        row["checks_failed"] = chk
        for c in chk:
            fails[c].append(f)
        rows.append(row)
    A.write_json(QA / "qa_report.json", dict(rows=rows, fails=fails))
    # summary
    agg = collections.OrderedDict()
    for r in sorted(rows, key=lambda r: r["folder"]):
        a = agg.setdefault(r["folder"], dict(n=0, dur=0.0, kb=0.0, tp=-99, lead=[], fails=0))
        a["n"] += 1
        a["dur"] += r["dur_s"]
        a["kb"] += r["est_download_kb"]
        a["tp"] = max(a["tp"], r["true_peak_dbtp"])
        a["fails"] += bool(r["checks_failed"])
    lines = ["| Folder | Files | Total s | Max true peak (dBTP) | Est. download (MB) | Files with a failed check |",
             "|---|---|---|---|---|---|"]
    tot = 0
    for fo, a in agg.items():
        lines.append(f"| `{fo}` | {a['n']} | {a['dur']:.1f} | {a['tp']:.2f} | {a['kb'] / 1000:.2f} | {a['fails']} |")
        tot += a["kb"]
    lines.append(f"| **Total** | {len(rows)} | {sum(a['dur'] for a in agg.values()):.1f} | "
                 f"{max(a['tp'] for a in agg.values()):.2f} | **{tot / 1000:.2f}** | {sum(a['fails'] for a in agg.values())} |")
    (QA / "qa_summary.md").write_text("\n".join(lines) + "\n\nFailed checks: " +
                                      json.dumps({k: len(v) for k, v in fails.items()}) + "\n")
    print("\n".join(lines))
    print({k: (len(v), [pathlib.Path(z).name for z in v[:12]]) for k, v in fails.items()})


if __name__ == "__main__":
    main()
