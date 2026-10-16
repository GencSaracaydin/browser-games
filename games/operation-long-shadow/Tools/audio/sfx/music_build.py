"""WP06_SFX music build: pick the EL Music takes, cut bar-line loops (music_loop.py), write the procedural cues and
stingers, and publish them to RawAssets/audio/ready/Assets/_Game/Audio/Music/ (+ the import index rows and
LOOP_POINTS.json via sfx_process.emit).

Grid note (logged in Docs/DECISIONS.md): every cue after MUS_01 used REF_PALETTE (72 BPM) as audio_reference and
locked to its grid: MUS_02/MUS_05 play double-time on it (143.6 BPM feel) and MUS_04 at 4/3 (95.7 BPM). Loops are
cut on the measured grid, never on the brief's nominal BPM.

usage:  python music_build.py select      # analyse takes, choose loops, write selection.json (+ stage-3 refs)
        python music_build.py build       # write the ready files (needs select; stage-3 cues when generated)
"""
from __future__ import annotations

import json
import pathlib
import sys

import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import alib as A  # noqa: E402
import music_loop as ML  # noqa: E402
import sfx_process as P  # noqa: E402

SR = A.SR
RAWM = P.RAWM
SEL = RAWM / "selection.json"
QA = RAWM / "_qa"

# cue: grid bpm (measured), beats per bar, loop bars, region (s) where the loop may sit, mode
CUES = {
    "MUS_01_SESSION": dict(bpm=71.8, bars=8, region=(30, 87.5), mode="intro_loop", takes=("take_a", "take_b")),
    "MUS_02_MARKET_LO": dict(bpm=71.8, bars=10, region=(38, 89.5), mode="loop", takes=("take_a", "take_b")),
    "MUS_02_MARKET_HI": dict(bpm=71.8, bars=10, region=(6, 58.5), mode="loop", takes=("take_c", "take_d"),
                             match="MUS_02_MARKET_LO"),
    "MUS_04_STATION_LOW": dict(bpm=95.7, bars=16, region=(20, 87.0), mode="loop", takes=("take_a", "take_b")),
    "MUS_04_STATION_FULL": dict(bpm=95.7, bars=16, region=(20, 87.0), mode="loop", takes=("take_a", "take_b"),
                                match="MUS_04_STATION_LOW"),
    "MUS_05_EXFIL": dict(bpm=71.8, bars=10, region=(36, 88.5), mode="loop", takes=("take_a", "take_b")),
    "MUS_03_LASTLIGHT": dict(bpm=71.8, mode="oneshot", takes=("take_a", "take_b")),
}


OVERRIDE = {  # cue: (take, why) where the measurements tie and the machine-listen description decides
    "MUS_03_LASTLIGHT": ("take_b", "one-shot: take_b reads as a 'poignant and tragic' string elegy (the brief), take_a as "
                                   "'epic, adventurous'; take_b's 10 clipped samples vanish at the -16 LUFS gain"),
    "MUS_05_EXFIL": ("take_a", "loop scores tie (1.696 vs 1.706); take_a's loop region is the fast drum-machine action "
                               "section ('accelerates into a fast-paced, high-energy action cue'), take_b is 'moderate tempo'"),
}


def take_file(cue, take):
    fs = sorted((RAWM / cue / take).glob("*music_generated*.wav"))
    return fs[0] if fs else None


def stability(x, s, e, bpm):
    """Groove stability inside [s, e): onset-autocorrelation at one bar lag, per 4-bar window (min over windows)."""
    import librosa
    seg = A.mono(x[s:e])[:, 0]
    y = librosa.resample(seg, orig_sr=SR, target_sr=22050)
    o = librosa.onset.onset_strength(y=y, sr=22050, hop_length=512)
    ht = 512 / 22050
    bar = int(round(4 * 60 / bpm / ht))
    win = bar * 4
    vals = []
    for i in range(0, len(o) - win - bar, bar * 2):
        a = o[i:i + win]
        b = o[i + bar:i + bar + win]
        vals.append(float(np.corrcoef(a, b)[0, 1]))
    return round(min(vals), 3) if vals else None


def analyse(cue, cfg):
    res = []
    for take in cfg["takes"]:
        f = take_file(cue, take)
        if not f:
            continue
        x, _ = A.load(f)
        if cfg["mode"] == "oneshot":
            m = A.measure(x)
            res.append(dict(take=take, file=str(f), clipped=m["clipped"], lufs=m["lufs_i"], tp=m["true_peak_dbtp"],
                            score=-m["clipped"] / 10))
            continue
        g = ML.bar_grid(x, cfg["bpm"], region=cfg["region"])
        try:
            best, top = ML.choose_loop(x, g, cfg["bars"], cfg["region"], xfade_s=0.3)
        except ValueError as ex:
            res.append(dict(take=take, file=str(f), error=str(ex)))
            continue
        st = stability(x, best["start"], best["end"], cfg["bpm"])
        m = A.measure(x)
        score = best["score"] + 0.5 * (st or 0) - (0.3 if m["clipped"] > 100 else 0)
        res.append(dict(take=take, file=str(f), grid=dict(bpm=round(g["bpm"], 3), bar_s=round(g["bar_s"], 4)),
                        loop=best, stability=st, clipped=m["clipped"], score=round(score, 3)))
    res.sort(key=lambda r: -r.get("score", -9))
    return res


def select():
    sel = json.loads(SEL.read_text()) if SEL.exists() else {}
    report = {}
    for cue, cfg in CUES.items():
        res = analyse(cue, cfg)
        report[cue] = res
        if not res or "error" in res[0]:
            continue
        r = res[0]
        if cue in OVERRIDE:
            r = next(z for z in res if z["take"] == OVERRIDE[cue][0])
        entry = dict(take=r["take"], file=P.rel(r["file"]),
                     why=OVERRIDE[cue][1] if cue in OVERRIDE else f"best loop/stability score of {len(res)} takes",
                     analysis=r)
        if cfg["mode"] != "oneshot" and cue in ("MUS_02_MARKET_LO", "MUS_04_STATION_LOW"):
            # stage-3 reference: the chosen loop body (same key/tempo/palette for the HI / FULL layer)
            x, _ = A.load(r["file"])
            body = x[r["loop"]["start"]:r["loop"]["end"]]
            ref = RAWM / cue / "ref_loop_body.wav"
            A.save(ref, A.fade(body, 0.01, 0.05))
            entry["ref_file"] = P.rel(ref)
        if cue in sel and "ref_file" in sel[cue] and "ref_file" not in entry and cue == "MUS_01_SESSION":
            entry["ref_file"] = sel[cue]["ref_file"]
        sel[cue] = {**sel.get(cue, {}), **entry}
    # stinger + cassette: chosen by listen + measurement (see AUDIO_REVIEW)
    sel["STG_GREYCOAT"] = dict(take="take_d", file=P.rel(take_file("STG_GREYCOAT", "take_d")),
                               why="take_d is the single low sustained swell into silence the brief asks for; take_c is a "
                                   "bell-like melodic figure and clips (12 samples); take_a/b were refused by the EL content filter")
    sel["AMB_CASSETTE_TUNE"] = dict(take="take_a", file=P.rel(take_file("AMB_CASSETTE_TUNE", "take_a")),
                                    why="take_a = clean bright guitars, major key, cheerful (closest to the 1960s surf brief); "
                                        "take_b = distorted driving rock")
    A.write_json(SEL, sel)
    A.write_json(QA / "music_select_report.json", report)
    for cue, res in report.items():
        print(cue, [(r.get("take"), r.get("score"), r.get("stability"), (r.get("loop") or {}).get("start_bar")) for r in res])
    return sel


# ----------------------------------------------------------------------------- build
def build_loop(cue, cfg, sel, match=None):
    s = sel[cue]
    x, _ = A.load(ROOT_ABS(s["file"]))
    lp = s["analysis"]["loop"]
    start, end = lp["start"], lp["end"]
    if match is not None:          # HI/FULL layer: exactly the LO/LOW loop length, so layers stay bar-synced
        n_match = match
        ratio = n_match / (end - start)
        if abs(ratio - 1) > 0.002:
            # resample the whole take so its loop body has exactly n_match samples (pitch shift < 1 %)
            import math
            from scipy import signal as ss
            up = int(round(ratio * 10000))
            g = math.gcd(up, 10000)
            x = ss.resample_poly(x, up // g, 10000 // g, axis=0)
            start = int(round(start * ratio))
        end = start + n_match       # exact same sample count as the partner layer
    keep_intro = cfg["mode"] == "intro_loop"
    best = None
    for xf in (0.3, 0.5, 0.8):     # widen the crossfade until the wrap measures like the interior
        out, lead, nb, body = ML.render(x, start, end, xfade_s=xf, keep_intro=keep_intro)
        sm = A.seam_metrics(body)
        if best is None or sm["seam_ok"]:
            best = (out, lead, nb, body, xf)
        if sm["seam_ok"]:
            break
    out, lead, nb, body, xf = best
    # level: integrated over the loop body
    g = P.LEVELS["music"] - A.lufs(body)
    out = out * A.undb(g)
    if A.true_peak_db(out) > -1.0:
        out = A.limit(out, 0.0, -1.3, lookahead_ms=3, release_ms=120)
    bar_s = s["analysis"]["grid"]["bar_s"] * (1 if match is None else (end - start) / (lp["end"] - lp["start"]))
    music = dict(bpm_grid=s["analysis"]["grid"]["bpm"], bar_seconds=round(bar_s, 5), loop_bars=cfg["bars"],
                 has_intro=keep_intro, source_take=s["take"])
    return out, dict(lead=lead, body=nb, tail=A.LEAD, music=music), end - start


def ROOT_ABS(p):
    p = pathlib.Path(p)
    return p if p.is_absolute() else P.ROOT / p


NOTES = {
    "MUS_01_SESSION": "Intro/booth score (72 BPM): plays from sample 0 through the intro, then loops the last 8 bars (26.7 s; resume pass 2 moved the cut to bars 15-23, where the wrap measures like the interior).",
    "MUS_02_MARKET_LO": "Beats 2-4 action cue, LO mix. Bar-synced with MUS_02_MARKET_HI (identical loop length and grid): "
                        "start both with PlayScheduled on the same dspTime and crossfade their volumes on bar lines.",
    "MUS_02_MARKET_HI": "Beats 2-4 action cue, HI mix (percussion + synth-brass stabs), layer partner of MUS_02_MARKET_LO "
                        "(same loop length, generated with the LO loop as audio_reference).",
    "MUS_04_STATION_LOW": "Beats 6-9 station cue (96 BPM, ticking hi-hat), LOW layer; bar-synced with MUS_04_STATION_FULL.",
    "MUS_04_STATION_FULL": "Beats 6-9 station cue, FULL layer (heavy toms, distorted synth), partner of MUS_04_STATION_LOW.",
    "MUS_05_EXFIL": "Beats 10-11 exfil cue; hard cut on the RPG launch (stop the scheduled sources, no fade).",
}


def build(sel=None):
    sel = sel or json.loads(SEL.read_text())
    P.load_scan()
    lengths = {}
    for cue, cfg in CUES.items():
        if cue not in sel or "analysis" not in sel[cue]:
            print("skip (no selection)", cue)
            continue
        if cfg["mode"] == "oneshot":
            continue
        match = lengths.get(cfg.get("match"))
        out, loop, nbody = build_loop(cue, cfg, sel, match)
        lengths[cue] = nbody
        P.emit(cue, "Music", cue, out, level=P.LEVELS["music"], kind="music_loop", stereo=True, loop=loop,
               sources=[ROOT_ABS(sel[cue]["file"])],
               notes=NOTES[cue] + f" Generated with elevenlabs/music/v2.5 ({sel[cue]['take']}), instrumental, motif palette "
                     "via audio_reference; loop cut on bar lines with a 0.3 s equal-power crossfade.")
    # MUS_03 one-shot: keep the generated ending
    if "MUS_03_LASTLIGHT" in sel:
        x, _ = A.load(ROOT_ABS(sel["MUS_03_LASTLIGHT"]["file"]))
        x = A.trim(x, on_db=-40, floor_db=-70, pre_ms=5, fade_ms=1500)
        x = A.loudness_to(x, P.LEVELS["music"], tp=-1.0)
        P.emit("MUS_03_LASTLIGHT", "Music", "MUS_03_LASTLIGHT", x, level=P.LEVELS["music"], kind="music_oneshot",
               stereo=True, sources=[ROOT_ABS(sel["MUS_03_LASTLIGHT"]["file"])],
               notes="MID-A Last Light one-shot (strings swell over pads, motif in the strings, resolves into silence under "
                     "the numbers). Not looping; start it so the swell lands as the sun touches the ridge.")
    build_procedural(sel)
    P.write_index()


# ----------------------------------------------------------------------------- procedural cues + stingers
PROC = RAWM / "procedural"


def vo_chain(x, name, seed=7):
    sys.path.insert(0, str(P.ROOT / "Tools/audio/vo"))
    import vo_chains  # noqa: E402
    y, info = vo_chains.CHAINS[name](A.mono(x)[:, 0], sr=SR, seed=seed)
    y, meters = vo_chains.finalize(y)
    return np.asarray(y)[:, None]


def build_procedural(sel):
    import motif_synth as MS  # noqa: E402
    # MOTIF + interval signal + outro
    for cue, fn, note, stereo in (
            ("MOTIF_MUSICBOX", "MOTIF_MUSICBOX.wav", "The Clockmaker three-note music-box motif D6-F6-C#6 alone (D15, the "
             "single source of the motif for station, score and outro).", True),
            ("MUS_06_SIXFORTY", "MUS_06_SIXFORTY.wav", "Outro: the music box alone, detuned and winding down, then true "
             "silence. One-shot.", True)):
        x, _ = A.load(PROC / fn)
        P.emit(cue, "Music", cue, x, kind="music_proc", stereo=stereo, sources=[PROC / fn],
               notes=note + " Procedural additive synthesis (Tools/audio/sfx/motif_synth.py). Not looping.")
    iv, _ = A.load(PROC / "INTERVAL_SIGNAL_DRY.wav", mono=True)
    P.emit("INTERVAL_SIGNAL", "Music", "INTERVAL_SIGNAL", iv, kind="music_proc", sources=[PROC / "INTERVAL_SIGNAL_DRY.wav"],
           notes="Clockmaker interval signal, DRY (3D from Tinker's PRC-77 pack): 8 clock ticks 0.5 s apart, then the "
                 "three-note chime; exactly 6.0 s (GDD §10.1). Precedes D039 and D092. Mono. Not looping.")
    for chain in ("HF_AM", "ROOM_SPEAKER"):
        y = vo_chain(iv, chain)
        n = int(6.0 * SR)
        y = A.fade(A.pad_to(y, n), 0.0005, 0.05)
        P.emit("INTERVAL_SIGNAL", "Music", f"INTERVAL_SIGNAL_{chain}", y, kind="music_proc",
               sources=[PROC / "INTERVAL_SIGNAL_DRY.wav"],
               notes=f"Interval signal through the {chain} chain (Tools/audio/vo/vo_chains.py) for "
                     + ("the Clockmaker on the radio (D039/D092)." if chain == "HF_AM" else "the basement room speaker.")
                     + " 6.0 s, mono. Not looping.")
    # NUM_BASEMENT_LOOP via clockmaker_splice (our tokens + interval signal), ROOM_SPEAKER chain
    nb = RAWM / "procedural" / "NUM_BASEMENT_LOOP_raw.wav"
    import subprocess
    p = subprocess.run([str(P.ROOT / "Tools/.venv/bin/python"), str(P.ROOT / "Tools/audio/vo/clockmaker_splice.py"),
                        "basement", "--interval", str(PROC / "INTERVAL_SIGNAL_DRY.wav"), "--out", str(nb), "--seconds", "60"],
                       capture_output=True, text=True, cwd=str(P.ROOT / "Tools/audio/vo"))
    if p.returncode == 0 and nb.exists():
        x, _ = A.load(nb, mono=True)
        body, sm, fixed = P.loop_body_fix(x, 0.05)
        pk, lead, nbs = A.package_loop(body)
        P.emit("NUM_BASEMENT_LOOP", "Music", "NUM_BASEMENT_LOOP", pk, kind="music_loop", sources=[nb],
               loop=dict(lead=lead, body=nbs, tail=A.LEAD),
               notes="Basement live numbers broadcast (interval signal + 5-digit groups spliced from our Clockmaker tokens, "
                     "ROOM_SPEAKER chain), ~60 s loop from the room speaker; NumbersBroadcast cuts it to static on the "
                     "4th rack (D066). Mono.")
    else:
        print("NUM_BASEMENT_LOOP failed:", p.stderr[-800:])
    build_stingers(sel)


def build_stingers(sel):
    import motif_synth as MS  # noqa: E402
    P.LEVELS["stinger"] = -14.0
    # STG_GREYCOAT (EL take): trim to the swell, fade
    if "STG_GREYCOAT" in sel:
        x, _ = A.load(ROOT_ABS(sel["STG_GREYCOAT"]["file"]))
        x = A.trim(x, on_db=-40, floor_db=-60, pre_ms=5, fade_ms=1200)
        x = A.fade(x[: int(8.0 * SR)], 0.005, 2.0)
        x = A.mmax_to(x, -14.0, tp=-1.0)
        P.emit("STG_GREYCOAT", "Music", "STG_GREYCOAT", x, level=-14.0, kind="stinger", stereo=True,
               sources=[ROOT_ABS(sel["STG_GREYCOAT"]["file"])],
               notes="Greycoat arrival stinger: one low synth swell into silence (EL Music v2.5, REF_PALETTE). One-shot, "
                     "duck the running cue -6 dB under it.")
    # STG_TITLE: teletype burst + low hit
    tel = sorted(P.cands("SFX_TELETYPE"), key=lambda r: -P.lscore(r))
    hit = sorted(P.cands("STG_TITLE"), key=lambda r: -P.lscore(r))
    if tel and hit:
        t, _ = A.load(tel[0]["file"], mono=True)
        t = A.hp(t[: int(1.6 * SR)], 300, order=2)
        t = A.fade(t, 0.002, 0.5)
        h, _ = A.load(hit[0]["file"], mono=True)
        h = A.trim(A.remove_dc(h, 25), on_db=-30, floor_db=-60, pre_ms=2, fade_ms=600)[: int(3.0 * SR)]
        h = A.fade(h, 0.0005, 1.0)
        x = A.mix((A.stereo(A.peak_norm(t, -1)), -10, 0.0), (A.stereo(A.peak_norm(h, -1)), 0, 1.25))
        x = A.mmax_to(x, -14.0, tp=-1.0)
        P.emit("STG_TITLE", "Music", "STG_TITLE", x, level=-14.0, kind="stinger", stereo=True,
               sources=[tel[0]["file"], hit[0]["file"]],
               notes="Title stinger: a short teletype burst, then a deep low hit at 1.25 s (lands on the title card). One-shot.")
    # STG_GLITCH: tape stop + static burst (synthesised static: the EL static take clipped)
    stops = [r for r in P.cands("STG_LAYERS_TAPE", {"tape_stop"})]
    if stops:
        best = max(stops, key=lambda r: (P.lscore(r), -abs(r["meas"]["centroid_hz"] - 3500)))
        s_, _ = A.load(best["file"], mono=True)
        s_ = A.trim(s_, on_db=-30, floor_db=-60, pre_ms=2, fade_ms=200)[: int(1.4 * SR)]
        rng = np.random.default_rng(23)
        n = int(0.55 * SR)
        st = A.bp(rng.standard_normal((n, 1)), 300, 9000, order=2)
        crackle = (rng.random((n, 1)) < 0.004) * rng.standard_normal((n, 1)) * 6
        st = st + A.bp(crackle, 1500, 12000, order=2)
        st = st * (0.6 + 0.4 * np.abs(np.sin(np.arange(n) / SR * 2 * np.pi * 50)))[:, None]  # mains-modulated hiss
        st = A.fade(st / np.abs(st).max(), 0.001, 0.08)
        x = A.mix((A.stereo(A.peak_norm(s_, -1)), 0, 0), (A.stereo(st), -4, 0.62))
        x = A.mmax_to(x, -14.0, tp=-1.0)
        P.emit("STG_GLITCH", "Music", "STG_GLITCH", x, level=-14.0, kind="stinger", stereo=True, sources=[best["file"]],
               notes="Booth glitch stinger: reel-to-reel tape stop, then a harsh static burst (procedural). One-shot.")
    rw = [r for r in P.cands("STG_LAYERS_TAPE", {"tape_rewind"})]
    if rw:
        best = max(rw, key=lambda r: P.lscore(r))
        x, _ = A.load(best["file"], mono=True)
        x = A.trim(x, on_db=-30, floor_db=-60, pre_ms=2, fade_ms=400)[: int(2.6 * SR)]
        x = A.fade(x, 0.003, 0.5)
        x = A.mmax_to(A.stereo(x), -14.0, tp=-1.0)
        P.emit("STG_DEATH", "Music", "STG_DEATH", x, level=-14.0, kind="stinger", stereo=True, sources=[best["file"]],
               notes="Death stinger: tape rewinding at high speed with squeal (death insert into the booth). One-shot.")
    # the raw tape layers themselves (manifest STG_LAYERS_TAPE -> SFX/SetPieces)
    for part in ("tape_stop", "tape_rewind"):
        P.oneshot("STG_LAYERS_TAPE", 2, "setpiece", "SFX/SetPieces", takes={part}, name=f"STG_LAYERS_TAPE_{part.split('_')[1].upper()}",
                  max_len=3.0, min_len=0.5, hp=40, max_gr=3, fade_ms=200,
                  notes=f"Source layer ({part.replace('_', ' ')}) also used inside STG_GLITCH / STG_DEATH; usable as a booth set-piece sound.")


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "select"
    if cmd == "select":
        select()
    elif cmd == "build":
        build()
