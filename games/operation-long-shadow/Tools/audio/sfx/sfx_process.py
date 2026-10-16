"""WP06_SFX processing: raw fal takes (+ our procedural material) -> import-ready clips in
RawAssets/audio/ready/Assets/_Game/Audio/{SFX,Ambience,Music}/ and RawAssets/audio/ready/import_index_sfx.json.

TECH_PLAN §8.7-5 (sfx_process.py). Every recipe picks takes by measurement + the cached machine listen
(RawAssets/audio/sfx/_qa/scan.json from scan.py), cuts the Mirelo pre-roll, shapes, layers, bakes `_far` variants and
zone tails, slices sequences by onset, makes loops seamless and sets per-category loudness (LEVELS below, logged in
Docs/DECISIONS.md). Nothing here calls fal.

usage:  python sfx_process.py [--only weapons,impacts,...] [--ids A,B] [--preview]
        (groups: see GROUPS at the bottom; default = all; the index is merged by manifest id)
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
import zlib

import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import alib as A  # noqa: E402

SR = A.SR
ROOT = pathlib.Path("/Users/gencsaracaydin/Documents/fun/fun-unity")
RAW = ROOT / "RawAssets/audio/sfx"
RAWM = ROOT / "RawAssets/audio/music"
READY = ROOT / "RawAssets/audio/ready"
OUTROOT = READY / "Assets/_Game/Audio"
INDEX = READY / "import_index_sfx.json"
REPORT = RAW / "_qa/build_report_sfx.json"
PREVIEW = RAW / "_preview"
MANIFEST = {e["id"]: e for e in json.loads((ROOT / "Docs/ASSET_MANIFEST.json").read_text())
            if e["category"] in ("sfx", "music")}

# ----------------------------------------------------------------------------- loudness per category (decision log)
# One-shots: momentary max (400 ms) in LUFS; loops/beds: integrated LUFS. All files: true peak <= -1 dBTP.
# Values are the level a clip should have when its cue volume is 1.0, so layers already sit right against each other.
LEVELS = {
    "shot_player": -10.0, "shot_enemy": -11.0, "shot_far": -16.0, "body": -15.0, "mech": -18.0,
    "tail_street": -25.0, "tail_square": -26.0, "tail_interior": -23.0, "tail_desert": -27.0,   # pass 3: D/R 7-11 dB
    "crack": -12.0, "whiz": -17.0, "explosion": -9.0, "explosion_far": -13.0, "debris": -19.0,
    "impact": -16.0, "impact_soft": -19.0, "footstep": -24.0, "foley": -24.0, "land": -21.0,
    "reload": -22.0, "handling": -24.0, "shell": -28.0, "grenade_small": -24.0,
    "setpiece": -19.0, "setpiece_loud": -15.0, "setpiece_quiet": -22.0,
    "loop_machine": -20.0, "loop_quiet": -28.0, "ui": -20.0,
    "amb_outdoor": -22.0, "amb_room": -26.0, "amb_distant": -24.0,
    "music": -16.0, "stinger": -14.0,
}

# ----------------------------------------------------------------------------- import rules (TECH_PLAN §3.12)
RULES = {
    "SFX/Weapons": "Force To Mono ON, Load Type Decompress On Load, Preload Audio Data ON, Quality 70, no sample-rate override",
    "SFX/Weapons/Far": "Force To Mono ON, Load Type Decompress On Load, Preload Audio Data ON, Quality 70, no sample-rate override",
    "SFX/Impacts": "Force To Mono ON, Load Type Decompress On Load, Preload Audio Data ON, Quality 70, no sample-rate override",
    "SFX/Foley": "Force To Mono ON, Load Type Decompress On Load, Preload Audio Data ON, Quality 70, no sample-rate override",
    "SFX/Footsteps": "Force To Mono ON, Load Type Decompress On Load, Preload Audio Data ON, Quality 70, no sample-rate override",
    "SFX/Explosions": "Force To Mono ON, Load Type Decompress On Load, Preload Audio Data ON, Quality 70, no sample-rate override",
    "SFX/Whizz": "Force To Mono ON, Load Type Decompress On Load, Preload Audio Data ON, Quality 70, no sample-rate override",
    "SFX/Synth": "Force To Mono ON, Load Type Decompress On Load, Preload Audio Data ON, Quality 70, no sample-rate override",
    "SFX/SetPieces": "Force To Mono ON, Load Type Compressed In Memory, Preload Audio Data ON, Quality 60, no sample-rate override",
    "SFX/UI": "Force To Mono ON, Load Type Decompress On Load, Preload Audio Data ON, Quality 60, Sample Rate Setting = Override 22050 Hz",
    "Ambience": "Force To Mono OFF (2D stereo bed), Load Type Compressed In Memory, Preload Audio Data OFF, Load In Background ON, Quality 50, no sample-rate override",
    "Music": "Force To Mono OFF (stereo), Load Type Compressed In Memory, Preload Audio Data OFF, Load In Background ON, Quality 60, no sample-rate override",
}

ROWS: dict = {}       # target_path -> index row
REVIEW: dict = {}     # target_path -> measurement row (Docs/AUDIO_REVIEW.md)
LOOPS: dict = {}      # target_path -> loop info (LOOP_POINTS.json)
SCAN = {}


def load_scan():
    global SCAN
    p = RAW / "_qa/scan.json"
    SCAN = json.loads(p.read_text()) if p.exists() else {}
    return SCAN


# ----------------------------------------------------------------------------- candidate helpers
def cands(mid, takes=None, ids=None):
    rs = [r for r in SCAN.values() if r["id"] == (ids or mid) and (takes is None or r["take"] in takes)]
    return sorted(rs, key=lambda r: r["file"])


def listen_of(r):
    ms = [l for l in r.get("listen", []) if l["kind"] == "match"]
    return ms[0] if ms else {}


def lscore(r):
    """Machine-listen score 0..1 (0.5 when not listened)."""
    l = listen_of(r)
    if not l:
        return 0.5
    m, re_ = l.get("match") or 5, l.get("realism") or 5
    s = 0.6 * m / 10 + 0.4 * re_ / 10
    if l.get("unwanted_flag"):
        s -= 0.35
    return s


def guard_yes(r):
    g = [l for l in r.get("listen", []) if l["kind"] == "guardrail"]
    return g[0].get("yes", []) if g else []


def tag(r):
    return f"{r['take']}/{pathlib.Path(r['file']).name[:2]}"


def rel(p):
    return str(pathlib.Path(p).relative_to(ROOT))


def ld(r_or_path, mono=True):
    f = r_or_path["file"] if isinstance(r_or_path, dict) else str(r_or_path)
    x, _ = A.load(f)
    if mono:
        x = A.to_mono_safe(x)
    return x


# ----------------------------------------------------------------------------- output
def notes_for(sub, text, loop=None, stereo=False):
    rule = RULES.get(sub) or RULES.get(sub.split("/")[0] + "/" + sub.split("/")[1] if "/" in sub else sub, "")
    base = f"AudioClip (AudioImportRules, folder Audio/{sub}): {rule}. "
    src = "Source: 48 kHz 16-bit " + ("stereo" if stereo else "mono") + " WAV, true peak <= -1 dBTP, no clipping"
    if loop:
        base += (f"LOOP: layout [lead-in {loop['lead']} samples][loop body {loop['body']} samples][tail-out "
                 f"{loop['tail']} samples]; loop region = samples [{loop['lead']}, {loop['lead'] + loop['body']}) "
                 f"({loop['body'] / SR:.3f} s, also in the WAV 'smpl' chunk and Audio/Music/LOOP_POINTS.json). Play it "
                 "with two AudioSources and PlayScheduled/SetScheduledEndTime over that region (TECH_PLAN §3.12: the "
                 "lead-in absorbs AAC priming); do not loop the whole clip with AudioSource.loop. ")
    return base + text + " " + src + "."


def emit(mid, sub, name, x, *, notes, sources, level=None, kind="oneshot", loop=None, stereo=False, extra=None):
    """Write one clip. x float [n,ch]; loop = dict(lead, body, tail) for packaged loops."""
    x = np.asarray(x, dtype=np.float64)
    if x.ndim == 1:
        x = x[:, None]
    if not stereo and x.shape[1] > 1:
        x = A.to_mono_safe(x)
    if stereo and x.shape[1] == 1:
        x = A.stereo(x)
    tp = A.true_peak_db(x)
    if tp > -1.0:
        x = x * A.undb(-1.0 - tp - 0.02)
    if not loop and kind not in ("synth", "music_proc", "music_oneshot", "stinger"):
        x = trim_tail(x)
    if not loop and sub in ("SFX/Footsteps", "SFX/Impacts") and kind in ("single", "slice"):
        x = align_peak(x)
    if not loop and sub in FAST_SUBS and kind not in NO_LEAD_TRIM and mid != "SFX_RPG_FLYBY":
        x = tight_lead(x)
    out = OUTROOT / sub / f"{name}.wav"
    if loop:
        A.save_loop(out, x, loop["lead"], loop["lead"] + loop["body"])
    else:
        A.save(out, x)
    y, _ = A.load(out)
    m = A.measure(y)
    tgt = f"Assets/_Game/Audio/{sub}/{name}.wav"
    row = dict(manifest_id=mid, file=tgt, target_path=tgt, type="audio",
               import_notes=notes_for(sub, notes, loop, stereo))
    ROWS[tgt] = row
    rv = dict(manifest_id=mid, file=tgt, kind=kind, level_target=level, sources=[rel(s) if str(s).startswith(str(ROOT)) else str(s) for s in sources],
              dur_s=m["dur_s"], ch=m["ch"], lufs_i=m["lufs_i"], lufs_m_max=m["lufs_m_max"],
              true_peak_dbtp=m["true_peak_dbtp"], peak_dbfs=m["peak_dbfs"], clipped=m["clipped"], lead_ms=m["lead_ms"],
              tail_ms=m["tail_ms"], centroid_hz=m["centroid_hz"], low_lt150=m["low_lt150"], bandwidth_hz=m["bandwidth_hz"],
              dc=m["dc"], stereo_corr=m["stereo_corr"], bytes=out.stat().st_size)
    if loop:
        body = y[loop["lead"]:loop["lead"] + loop["body"]]
        rv["seam"] = A.seam_metrics(body)
        LOOPS[tgt] = dict(clip=name, sr=SR, loop_start_sample=loop["lead"], loop_end_sample=loop["lead"] + loop["body"],
                          loop_seconds=round(loop["body"] / SR, 4), **(loop.get("music") or {}))
    if extra:
        rv.update(extra)
    REVIEW[tgt] = rv
    return out


def trim_tail(x, below_db=-65.0, keep_ms=20, fade_ms=15):
    """Resume pass 3 (download budget, §8.9 one-shot <= 2 s): drop the inaudible end of a one-shot, i.e. everything
    after the last 10 ms window within below_db of the peak, plus keep_ms with a short fade."""
    env = A.envelope(x, 10)
    pk = float(env.max()) if len(env) else 0.0
    if pk <= 0:
        return x
    above = np.where(env > pk * A.undb(below_db))[0]
    if not len(above):
        return x
    end = min(len(x), int(above[-1]) + int(keep_ms / 1000 * SR))
    if len(x) - end < int(0.05 * SR):
        return x
    return A.fade(x[:end], 0.0, fade_ms / 1000)


def align_peak(x, max_pre_ms=30.0, keep_pre_ms=12.0, quiet_db=-15.0, event_db=-12.0):
    """Footstep/impact clips must hit on the frame they are triggered: when the first event (within event_db of the
    peak; a heel-toe step peaks on the toe) starts later than max_pre_ms, cut the head so it sits keep_pre_ms in,
    provided everything cut is quiet_db under the peak (a real first event is never removed)."""
    env = A.envelope(x, 2)
    pk = float(env.max())
    i1 = int(np.argmax(env > pk * A.undb(event_db)))
    if i1 <= int(max_pre_ms / 1000 * SR):
        return x
    cut = i1 - int(keep_pre_ms / 1000 * SR)
    if env[:cut].max() > pk * A.undb(quiet_db):
        return x
    return A.fade(x[cut:], 0.002, 0.0)


FAST_SUBS = ("SFX/Weapons", "SFX/Impacts", "SFX/Footsteps", "SFX/Whizz", "SFX/UI", "SFX/Explosions")
NO_LEAD_TRIM = ("weapon_tail", "weapon_mech", "weapon_body", "weapon_far", "explosion_far")


def tight_lead(x, rel_db=-30.0, keep_ms=3.0):
    """Fast one-shots start within ~3 ms of their onset (30 dB under the peak): drop a quiet head (pre-noise)."""
    env = A.envelope(x, 1)
    pk = float(env.max())
    i = int(np.argmax(env > pk * A.undb(rel_db)))
    cut = i - int(keep_ms / 1000 * SR)
    if cut < int(0.006 * SR):
        return x
    return A.fade(x[cut:], 0.001, 0.0)


def level_to(x, cat, mode="mmax"):
    t = LEVELS[cat]
    if mode == "lufs":
        return A.loudness_to(x, t, tp=-1.0)
    return A.mmax_to(x, t, tp=-1.0)


METRIC = {"setpiece_quiet": "s100", "shot_player": "s100", "shot_enemy": "s100", "mech": "s100", "crack": "s100", "impact": "s100",
          "impact_soft": "s100", "footstep": "s100", "shell": "s100", "ui": "s100", "handling": "s100"}


def meter(x, cat):
    if METRIC.get(cat) == "s100":
        return A.momentary_max(x, win=0.1, hop=0.01)
    return A.momentary_max(x)


def loud(x, cat, max_gr=8.0, tp=-1.0, clip=True, target=None):
    """One-shot loudness: the smallest drive that reaches the category target (METRIC: 100 ms or 400 ms momentary
    max). Above the clean-peak gain the extra drive goes into a 4x-oversampled soft clipper (dense like an
    overloaded field recorder) capped at max_gr dB; if the cap is hit the clip stays quieter and the shortfall is
    recorded in the build report."""
    target = LEVELS[cat] if target is None else target
    pk = float(A.db(np.abs(x).max()))
    g_free = (tp - 0.6) - pk
    base = x * A.undb(g_free)

    def render(d):
        if d <= 0:
            return base * A.undb(d)
        return A.clip_os(base, d, tp - 0.6) if clip else A.limit(base, d, tp - 0.6, lookahead_ms=1.0, release_ms=40)

    lo, hi = -40.0, float(max_gr)
    if meter(render(hi), cat) <= target:
        y = render(hi)
    else:
        for _ in range(16):
            mid = 0.5 * (lo + hi)
            if meter(render(mid), cat) < target:
                lo = mid
            else:
                hi = mid
        y = render(hi)
    if A.true_peak_db(y) > tp:
        y = A.finalize(y, tp)
    return y


def loud_set(xs, cat, max_gr=8.0, clip=True, spread=1.5):
    """Level a round-robin set: each clip to the category target, but never more than `spread` dB above the
    quietest one (a variant that cannot reach the target without damage pulls the set down with it)."""
    ys = [loud(x, cat, max_gr=max_gr, clip=clip) for x in xs]
    lv = [meter(y, cat) for y in ys]
    floor = min(lv)
    out = []
    for x, y, l in zip(xs, ys, lv):
        if l > floor + spread:
            y = loud(x, cat, max_gr=max_gr, clip=clip, target=floor + spread)
        out.append(y)
    return out


def tight(y, pre_ms=2.0, on_db=-30):
    """Re-trim a slice so it starts pre_ms before its onset (slices come with the detector's look-back)."""
    i = max(0, A.onset_index(y, on_db, 20) - int(pre_ms / 1000 * SR))
    return A.fade(y[i:], 0.0005, 0.0)


def clean(x, hp=25):
    return A.remove_dc(x, hp)


def pick_diverse(scored, n, key=lambda r: r["take"]):
    """scored: [(score, rec, ...)] best first; pick n preferring different takes, then fill."""
    out, used = [], set()
    for s in scored:
        k = key(s[1])
        if k not in used:
            out.append(s)
            used.add(k)
        if len(out) == n:
            return out
    for s in scored:
        if s not in out:
            out.append(s)
        if len(out) == n:
            break
    return out


# ============================================================================= WEAPONS
WEAPONS = {
    # id: (kind, body layer, body gain dB, far lowpass Hz)
    "SFX_WPN_COMMANDO_SHOT": dict(kind="player", body="SFX_WPN_BODY_RIFLE", body_db=-2, far_lp=2600),
    "SFX_WPN_HUSHPUPPY_SHOT": dict(kind="suppressed", body=None, far_lp=1800),
    "SFX_WPN_AK_SHOT": dict(kind="player", body="SFX_WPN_BODY_RIFLE", body_db=0, far_lp=2200),
    "SFX_WPN_RPK_SHOT": dict(kind="player", body="SFX_WPN_BODY_RIFLE", body_db=1, far_lp=2100),
    "SFX_WPN_M60D_SHOT": dict(kind="player", body="SFX_WPN_BODY_HEAVY", body_db=-3, far_lp=2000),
    "SFX_WPN_AKMS_SHOT": dict(kind="enemy", body="SFX_WPN_BODY_RIFLE", body_db=-6, far_lp=2200),
    "SFX_WPN_SVD_SHOT": dict(kind="distant", body=None, far_lp=1500),
    "SFX_WPN_DSHK_SHOT": dict(kind="heavy", body="SFX_WPN_BODY_HEAVY", body_db=0, far_lp=1700),
}


def cfg_kind(mid):
    return WEAPONS[mid]["kind"]


def hf_tone_db(x, t1=0.5, lo=6000, hi=20000):
    """Persistent narrow tone above 6 kHz (a model whine, not a casing ring): the bin whose power stays highest above
    each frame's median, in dB. Real shots measure 10-35 dB here; > 45 dB is a synthetic artifact."""
    from scipy import signal as _sg
    m = A.mono(x)[: int(t1 * SR), 0]
    if len(m) < 4096:
        return 0.0
    f, _t, S = _sg.spectrogram(m, SR, nperseg=2048, noverlap=1536)
    rel = S / (np.median(S, axis=0, keepdims=True) + 1e-20)
    pers = np.median(rel, axis=1)
    band = (f > lo) & (f < hi)
    return float(10 * np.log10(pers[band].max() + 1e-20))


def shot_candidates(mid):
    out = []
    for r in cands(mid):
        x = clean(ld(r), 30)
        x = A.trim(x, on_db=-30, floor_db=-66, pre_ms=1.5, fade_ms=40, back_ms=30)
        dbl = A.double_shot(x)
        pk0 = A.peaks_after_main(x)
        # resume pass 2: a "flam" is a second *impulsive* event (rise < 4 ms) close in level and brightness inside the
        # 15-150 ms window that shape_close keeps; slower, darker peaks there are ground/wall slaps and the action,
        # which a close field recording of a real rifle has (the old rule rejected 18 of 20 Commando takes for them).
        flam = any(0.015 <= t <= 0.15 and rel_ > -6 and rise < 4.0 and hfr > -6 for t, rel_, rise, hfr in pk0)
        if cfg_kind(mid) in ("distant",):
            flam = any(0.015 <= t <= 0.5 and rel_ > -4 for t, rel_, rise, hfr in pk0)
        l = listen_of(r)
        shots = l.get("shots")
        short = len(x) < int((0.45 if cfg_kind(mid) != "suppressed" else 0.25) * SR)   # shot cut off by the file end
        silent = A.momentary_max(x, win=0.1, hop=0.01) < -45
        tone = hf_tone_db(x)
        off_brief = (shots is not None and shots == 0) or (l.get("match") is not None and l["match"] <= 3)
        bad = dbl or flam or short or silent or tone > 45 or off_brief or (shots is not None and shots >= 2) or l.get("unwanted_flag")
        pk = A.peaks_after_main(x)
        # punch: share of energy in the first 60 ms and low-mid weight in the first 50 ms
        n60 = int(0.06 * SR)
        e = (x ** 2).sum()
        early = float((x[:n60] ** 2).sum() / (e + 1e-12))
        lowmid = float((A.bp(x[:int(0.05 * SR)], 80, 400, order=2) ** 2).sum() / ((x[:int(0.05 * SR)] ** 2).sum() + 1e-12))
        s = lscore(r) + 0.25 * early + 0.25 * min(lowmid, 0.5) - (1.0 if bad else 0.0)
        out.append((s, r, x, dict(double=dbl, flam=flam, short=short, tone_db=round(tone, 1), listen_shots=shots, early=round(early, 3), lowmid=round(lowmid, 3),
                                  peaks=pk[:4], bad=bool(bad))))
    out.sort(key=lambda z: -z[0])
    return out


def shape_close(x, kind):
    """Keep the direct shot and its first slap, pull the embedded echo down (the zone tail layer supplies the space)."""
    if kind == "suppressed":
        keep, duck, end = 0.10, -10, 0.55
    elif kind in ("distant",):
        keep, duck, end = 0.6, -4, 2.2   # the SVD is heard from 130 m: its echo IS the sound
    elif kind == "heavy":
        keep, duck, end = 0.22, -6, 1.4
    elif kind == "enemy":
        keep, duck, end = 0.18, -6, 1.2
    else:
        keep, duck, end = 0.12, -9, 1.0
    n = min(len(x), int(end * SR))
    y = x[:n] * A.gain_env(n, [(0, 0), (keep, 0), (keep + 0.18, duck), (end, duck - 30)])
    return A.fade(y, 0.0003, 0.08)


def punch(x, kind):
    drive = {"player": 8, "enemy": 7, "heavy": 9, "suppressed": 5, "distant": 4}[kind]
    y = A.limit(x, drive, -1.0, lookahead_ms=0.6, release_ms=35)
    return y


_FAR_IR = {}


def far_ir(seed, long=False):
    key = (seed, long)
    if key not in _FAR_IR:
        rng = np.random.default_rng(seed)
        base = [0.11, 0.19, 0.31, 0.46, 0.63, 0.84, 1.08, 1.37, 1.7] + ([2.05, 2.4] if long else [])
        er = [(t * rng.uniform(0.88, 1.12), -6.5 - 1.6 * k + rng.uniform(-2, 2), 1400 - 60 * k) for k, t in enumerate(base)]
        _FAR_IR[key] = A.synth_ir(rt60=2.6 if long else 2.1, predelay=0.07, er=er, lp_hz=1700, lp_end_hz=500,
                                  hp_hz=70, seed=seed)
    return _FAR_IR[key]


def make_far(x, lp_hz, seed, long=False):
    """Distance bake (WebGL has no runtime filters): air absorption low-pass, softened attack, long rolling echo."""
    d = x[: int(0.5 * SR)].copy()
    d = A.fade(d, 0.0003, 0.2)
    d = A.hp(d, 90, order=2)
    d = A.lp(d, lp_hz, order=2)
    d = A.lp(d, lp_hz * 2.2, order=2)
    d = A.compress(d, -24, 3, attack_ms=0.5, release_ms=80, knee_db=6)
    ir = far_ir(seed, long)
    wet = A.convolve(d, ir)
    ed = np.sqrt((d ** 2).sum())
    ew = np.sqrt((wet ** 2).sum()) + 1e-12
    wet = wet * (ed / ew) * A.undb(-1.5)
    y = A.mix((d, 0, 0), (wet, 0, 0))
    n = int((2.9 if long else 2.4) * SR)
    y = A.pad_to(y, n)
    y = A.fade(y, 0.0003, 0.6, curve=2.5)
    return y


def recipe_weapon_shots(only=None):
    report = {}
    for mid, cfg in WEAPONS.items():
        if only and mid not in only:
            continue
        cs = shot_candidates(mid)
        good = [c for c in cs if not c[3]["bad"]]
        if len(good) < 4:
            rest = [c for c in cs if c[3]["bad"] and not c[3]["double"]] + [c for c in cs if c[3]["double"]]
            good = good + rest[: 4 - len(good)]
        sel = pick_diverse(good, 4)
        kind = cfg["kind"]
        cat = "shot_player" if kind in ("player", "suppressed") else "shot_enemy"
        name = mid.replace("_SHOT", "")
        report[mid] = []
        ys = loud_set([shape_close(z[2], kind) for z in sel], cat,
                      max_gr={"player": 10, "enemy": 9, "heavy": 10, "suppressed": 7, "distant": 6}[kind])
        for k, ((s, r, x, info), y) in enumerate(zip(sel, ys), 1):
            emit(mid, "SFX/Weapons", f"{mid}_{k:02d}", y, level=LEVELS[cat], kind="weapon_transient", sources=[r["file"]],
                 notes=(f"Transient layer {k}/4 of {name} ({kind}); WeaponShotPlayer picks one of the 4 at random with pitch "
                        f"+-3 % and volume +-1.5 dB, layered with the mech, body ({cfg['body'] or 'none'}) and zone-tail layers "
                        f"at cue volume 1.0 (levels are pre-balanced). Not looping. Auto fire re-triggers this one-shot at the RPM."),
                 extra=dict(selection=dict(score=round(s, 3), take=tag(r), **info)))
            report[mid].append(dict(take=tag(r), score=round(s, 3), **info))
        # far variants from the two best picks
        for k, (s, r, x, info) in enumerate(sel[:2], 1):
            y = make_far(x, cfg["far_lp"], seed=zlib.crc32(mid.encode()) % 1000 + k, long=(kind in ("heavy", "distant")))
            if kind == "suppressed":
                y = y[: int(1.4 * SR)]
                y = A.fade(y, 0.0003, 0.4)
            y = loud(y, "shot_far", max_gr=6)
            emit(mid, "SFX/Weapons/Far", f"{mid}_FAR_{k:02d}", y, level=LEVELS["shot_far"], kind="weapon_far",
                 sources=[r["file"]],
                 notes=(f"Baked distance variant {k}/2 of {name}: {cfg['far_lp']} Hz air-absorption low-pass, softened attack, "
                        "synthetic rolling outdoor echo (D9: no runtime filters on Web). WeaponShotPlayer crossfades near->far "
                        "by distance (near < 40 m, far > 120 m). Not looping."))
    return report


def recipe_bodies():
    rep = {}
    for mid, lp_hz in (("SFX_WPN_BODY_RIFLE", 260), ("SFX_WPN_BODY_HEAVY", 200)):
        scored = []
        for r in cands(mid):
            x = clean(ld(r), 28)
            x = A.lp(x, lp_hz, order=4)                  # removes the EL clipping harmonics and any crack
            x = A.trim(x, on_db=-24, floor_db=-60, pre_ms=0.5, fade_ms=60, back_ms=10)
            m = A.measure(x)
            s = (1 if m["dur_s"] > 0.35 else 0) + 0.2 * (m["crest_db"] / 20) + 0.3 * lscore(r)
            scored.append((s, r, x))
        scored.sort(key=lambda z: -z[0])
        rep[mid] = []
        for k, (s, r, x) in enumerate(scored[:2], 1):
            n = min(len(x), int(0.75 * SR))
            y = x[:n] * A.gain_env(n, [(0, 0), (0.08, 0), (0.75, -30)])
            y = A.fade(y, 0.0003, 0.08)
            y = loud(y, "body", max_gr=4)
            emit(mid, "SFX/Weapons", f"{mid}_{k:02d}", y, level=LEVELS["body"], kind="weapon_body", sources=[r["file"]],
                 notes=("Low-frequency body layer (< %d Hz) played under the transient at the same instant (sample-aligned "
                        "onset, lead < 1 ms), pre-balanced for cue volume 1.0; pitch follows the transient." % lp_hz))
            rep[mid].append(tag(r))
    return rep


def mech_cluster(x, max_s=0.32):
    """First mechanical event cluster (bolt cycle) up to max_s; the auto-fire re-trigger at 600-900 rpm needs < 0.1 s
    of dense content, the rest is a short decay."""
    n = min(len(x), int(max_s * SR))
    y = x[:n] * A.gain_env(n, [(0, 0), (0.09, 0), (max_s, -24)])
    return A.fade(y, 0.0003, 0.05)


def recipe_mechs():
    rep = {}
    for w in ("COMMANDO", "HUSHPUPPY", "AK", "RPK", "M60D"):
        mid = f"SFX_WPN_{w}_MECH"
        scored = []
        for r in cands(mid):
            x = clean(ld(r), 150)
            x = A.trim(x, on_db=-28, floor_db=-60, pre_ms=1, fade_ms=30)
            ev = A.detect_events(x, min_gap=0.06, rise_db=10, floor_db=-30, min_len=0.01, max_len=0.6)
            m = A.measure(x)
            s = lscore(r) + (0.2 if 1 <= len(ev) <= 4 else 0) + 0.1 * min(m["crest_db"], 25) / 25
            scored.append((s, r, x))
        scored.sort(key=lambda z: -z[0])
        rep[mid] = []
        for k, (s, r, x) in enumerate(scored[:2], 1):
            y = mech_cluster(x, 0.36 if w in ("M60D", "RPK") else 0.3)
            y = loud(y, "mech", max_gr=5)
            emit(mid, "SFX/Weapons", f"{mid}_{k:02d}", y, level=LEVELS["mech"], kind="weapon_mech", sources=[r["file"]],
                 notes=f"Mechanical layer {k}/2 under each {w} shot (bolt/slide cycle), pre-balanced for cue volume 1.0. Not looping.")
            rep[mid].append(tag(r))
    return rep


# ----------------------------------------------------------------------------- zone tails
TAILS = {
    "SFX_WPN_TAIL_STREET": dict(zone="street", rt=1.1, cut_ms=14, lp=7500, dur=1.8, cat="tail_street"),
    "SFX_WPN_TAIL_SQUARE": dict(zone="square", rt=1.6, cut_ms=22, lp=6500, dur=2.8, cat="tail_square"),
    "SFX_WPN_TAIL_INTERIOR": dict(zone="interior", rt=1.15, cut_ms=8, lp=6500, dur=2.0, cat="tail_interior"),
    "SFX_WPN_TAIL_DESERT": dict(zone="desert", rt=2.6, cut_ms=40, lp=5000, dur=3.0, cat="tail_desert"),
}


def late_rerise(x, after_s=0.12, rise_db=11):
    """A second shot inside a tail take: the 20 ms envelope jumps > rise_db above its running minimum."""
    env = A.db(A.envelope(x, 20))
    hop = int(0.005 * SR)
    e = env[::hop]
    start = int(after_s / 0.005) + int(np.argmax(e[: int(0.1 / 0.005)]))
    run_min = np.inf
    for i in range(start, len(e)):
        run_min = min(run_min, e[i])
        if e[i] - run_min > rise_db and e[i] > e.max() - 40:
            return round(i * 0.005, 3)
    return None


def gate_floor(y, below_peak_db=46, rt_after=0.6):
    """Kill a take's noise-floor plateau (composites showed -55 dB hiss to the end of the tail): from the point where
    the 20 ms envelope first sinks under max(peak - below_peak_db, floor + 6 dB) the tail keeps decaying with RT60
    rt_after. floor = 10th percentile of the envelope over the second half."""
    env = A.db(A.envelope(y, 20))
    floor = float(np.percentile(env[len(env) // 2:], 10)) if len(env) > 100 else -120
    thr = max(env.max() - below_peak_db, floor + 6)
    low = np.where((env < thr) & (np.arange(len(env)) > int(np.argmax(env))))[0]
    if len(low):
        i0 = int(low[0])
        t = np.maximum(np.arange(len(y)) - i0, 0) / SR
        y = y * np.exp(-6.91 * t / rt_after)[:, None]
    return y


PROC_TAIL = {  # resume pass 2: the generated square/desert "echo only" takes were erratic (0.4 s squares, hiss plateaus)
    "SFX_WPN_TAIL_SQUARE": dict(rt=1.6, n_walls=8, d=(14, 70), lp=4500, lp_end=1200, diffuse_db=-7, build=0.15),
    "SFX_WPN_TAIL_DESERT": dict(rt=2.6, n_walls=5, d=(70, 330), lp=1500, lp_end=500, diffuse_db=-17, build=0.5),
    # resume pass 3: the generated street/interior "tails" still carried a full gunshot of another gun (machine listen:
    # "full report of a gunshot ... including the loud initial crack"; the 8-14 ms duck could not remove it), so they
    # are rebuilt the same way from a physical model: a 1-D image-source street canyon / a 3-D image-source room.
    "SFX_WPN_TAIL_STREET": dict(rt=1.1, width=(6.0, 10.0), facade_h=6.5, refl=0.88, ends=(18, 75), lp=7000, lp_end=1400,
                                diffuse_db=-16, build=0.08, scatter=0.45),
    "SFX_WPN_TAIL_INTERIOR": dict(rt=1.15, room=((7, 14), (5, 9), (3.0, 4.2)), refl=0.82, lp=6000, lp_end=1100,
                                  diffuse_db=-4, build=0.03, scatter=0.3),
}


_SCATTER_RNG = np.random.default_rng(1234)


def _image_tap(er_ir, t, g_db, lph, order=2, scatter=0.0, spread_s=0.008):
    """One reflection: a low-passed impulse; with scatter s, a share s of its energy is spread as a short decaying
    noise burst (rough facades, windows, balconies scatter part of the wave)."""
    i = int(t * SR)
    if i >= len(er_ir) - 10:
        return
    n = min(len(er_ir) - i, 4096)
    imp = np.zeros((n, 1))
    imp[0] = np.sqrt(1.0 - scatter)
    if scatter > 0:
        k = np.arange(n)
        burst = _SCATTER_RNG.standard_normal(n) * np.exp(-k / (spread_s * SR / 3.0))
        burst *= np.sqrt(scatter) / (np.sqrt((burst ** 2).sum()) + 1e-12)
        imp[:, 0] += burst
    imp = A.lp_causal(imp, lph, order=order)
    er_ir[i:i + len(imp)] += imp * A.undb(g_db)


def _diffuse(L, first, c, rng, er_ir):
    tt = np.arange(L) / SR
    noise = rng.standard_normal((L, 1))
    lo = A.lp(noise, c["lp_end"], order=2) * np.exp(-6.91 * np.maximum(tt - first, 0) / c["rt"])[:, None]
    hi = (A.lp(noise, c["lp"], order=2) - A.lp(noise, c["lp_end"], order=2)) * \
        np.exp(-6.91 * np.maximum(tt - first, 0) / (c["rt"] * 0.5))[:, None]
    diff = (lo + hi) * (1 - np.exp(-np.maximum(tt - first, 0) / c["build"]))[:, None]
    return diff / (np.sqrt((diff ** 2).sum()) + 1e-12) * np.sqrt((er_ir ** 2).sum()) * A.undb(c["diffuse_db"] + 10)


def street_ir(c, seed):
    """Street canyon between two plaster facades (listener = source, x0 from the left facade): 1-D image sources at
    2|k w - x0| and 2|k| w with |image index| bounces (amplitude refl^n / d), a vertical-aperture loss for the
    finite facade height (rays above the eaves escape to the sky), air/scatter low-pass falling with order, a few
    late echoes from the street ends / cross walls, and a diffuse field (RT60 c['rt'], highs twice as fast)."""
    rng = np.random.default_rng(seed)
    w = c["width"][0] + (c["width"][1] - c["width"][0]) * ((seed % 3) / 2.0) + rng.uniform(-0.4, 0.4)   # 3 variants: lane .. street
    x0 = rng.uniform(0.3, 0.45) * w
    L = int((0.15 + c["rt"] * 1.25) * SR)
    er_ir = np.zeros((L, 1))
    taps = []
    for k in range(-14, 15):
        for d, n in ((abs(2 * k * w - 2 * x0), abs(2 * k - 1)), (abs(2 * k * w), abs(2 * k))):
            if n == 0 or d < 0.5:
                continue
            taps.append((d, n))
    for d, n in taps:
        t = d / 343.0
        aperture = min(1.0, c["facade_h"] / (0.5 * d)) ** 0.5
        g = 20 * np.log10(c["refl"] ** n * aperture / d) + rng.uniform(-1.5, 1.5)
        lph = max(1500.0, 10000.0 * 0.86 ** n - 5 * d)
        _image_tap(er_ir, t + rng.uniform(0, 0.0015), g, lph, scatter=c.get("scatter", 0.0))
    for _ in range(int(rng.integers(3, 6))):          # street ends, cross walls, the far facade of a junction
        d = rng.uniform(*c["ends"])
        _image_tap(er_ir, 2 * d / 343.0, 20 * np.log10(0.7 / (2 * d)) - rng.uniform(2, 6), max(700, 4200 - 30 * d), order=4)
    first = w / 343.0 * 0.6
    ir = A.hp(er_ir + _diffuse(L, first, c, rng, er_ir), 90, order=2)
    return ir / (np.sqrt((ir ** 2).sum()) + 1e-12)


def room_ir(c, seed):
    """Plastered/concrete room (house interior, cinema foyer, station rooms): 3-D image sources up to order 3 for a
    shoebox of random size (source and listener 1.6 m apart, 1.5-1.7 m high), amplitude refl^n / d with a growing
    low-pass, over a dense diffuse field with RT60 c['rt'] (highs twice as fast)."""
    rng = np.random.default_rng(seed)
    Lx, Ly, Lz = (rng.uniform(*r) for r in c["room"])
    s = np.array([rng.uniform(0.3, 0.7) * Lx, rng.uniform(0.3, 0.7) * Ly, 1.5])
    r = s + np.array([1.2, 1.0, 0.15])
    L = int((0.1 + c["rt"] * 1.25) * SR)
    er_ir = np.zeros((L, 1))
    d0 = float(np.linalg.norm(r - s))
    first = 1e9
    for nx in range(-3, 4):
        for ny in range(-3, 4):
            for nz in range(-2, 3):
                for px in (0, 1):
                    for py in (0, 1):
                        for pz in (0, 1):
                            img = np.array([(1 - 2 * px) * s[0] + 2 * nx * Lx, (1 - 2 * py) * s[1] + 2 * ny * Ly,
                                            (1 - 2 * pz) * s[2] + 2 * nz * Lz])
                            n = abs(2 * nx - px) + abs(2 * ny - py) + abs(2 * nz - pz)
                            if n == 0 or n > 4:
                                continue
                            d = float(np.linalg.norm(img - r))
                            t = (d - d0) / 343.0           # relative to the direct sound (the shot layer)
                            if t <= 0:
                                continue
                            first = min(first, t)
                            g = 20 * np.log10(c["refl"] ** n / d) + rng.uniform(-1.0, 1.0)
                            _image_tap(er_ir, t, g, max(1200.0, 8000.0 * 0.8 ** n), scatter=c.get("scatter", 0.0), spread_s=0.004)
    ir = A.hp(er_ir + _diffuse(L, first, c, rng, er_ir), 90, order=2)
    return ir / (np.sqrt((ir ** 2).sum()) + 1e-12)


def proc_tail_ir(mid, seed):
    """Wet-only outdoor IR: discrete facade/ridge echoes (delay 2d/c, 1/d level, air-absorption low-pass growing with
    distance; desert ridge echoes smeared into 60-160 ms rolling clusters) over a diffuse late field that builds up
    slowly (diffuse_db re. the echoes, build-up time constant `build`) and decays with RT60 rt (highs twice as fast)."""
    c = PROC_TAIL[mid]
    if mid.endswith("STREET"):
        return street_ir(c, seed)
    if mid.endswith("INTERIOR"):
        return room_ir(c, seed)
    rng = np.random.default_rng(seed)
    L = int((0.1 + c["rt"] * 1.2) * SR)
    er_ir = np.zeros((L, 1))
    first = 1e9
    for _ in range(c["n_walls"]):
        d = rng.uniform(*c["d"])
        t = 2 * d / 343.0
        first = min(first, t)
        g = -6 - 20 * np.log10(d / c["d"][0]) - rng.uniform(0, 4)
        lph = max(700, 6500 - 18 * d) if mid.endswith("SQUARE") else max(500, 2400 - 5 * d)
        if mid.endswith("DESERT"):
            k = int(rng.integers(5, 9))
            span = rng.uniform(0.06, 0.16)
            arr = [(t + span * j / k + rng.uniform(0, 0.01), g - 2.5 * j - rng.uniform(0, 2)) for j in range(k)]
        else:
            arr = [(t, g), (t + rng.uniform(0.004, 0.012), g - rng.uniform(4, 8))]   # facade + cornice/ground double
        for ta, ga in arr:
            i = int(ta * SR)
            if i >= L - 10:
                continue
            imp = np.zeros((min(L - i, 4096), 1))
            imp[0] = 1.0
            imp = A.lp_causal(imp, lph, order=2 if mid.endswith("SQUARE") else 4)
            er_ir[i:i + len(imp)] += imp * A.undb(ga)
    tt = np.arange(L) / SR
    pd = first
    noise = rng.standard_normal((L, 1))
    lo = A.lp(noise, c["lp_end"], order=2) * np.exp(-6.91 * np.maximum(tt - pd, 0) / c["rt"])[:, None]
    hi = (A.lp(noise, c["lp"], order=2) - A.lp(noise, c["lp_end"], order=2)) * \
        np.exp(-6.91 * np.maximum(tt - pd, 0) / (c["rt"] * 0.5))[:, None]
    diff = (lo + hi) * (1 - np.exp(-np.maximum(tt - pd, 0) / c["build"]))[:, None]
    diff = diff / (np.sqrt((diff ** 2).sum()) + 1e-12) * np.sqrt((er_ir ** 2).sum()) * A.undb(c["diffuse_db"] + 10)
    ir = A.hp(er_ir + diff, 90, order=2)
    return ir / (np.sqrt((ir ** 2).sum()) + 1e-12)


TAIL_PEAK_DB = {"street": -12.0, "interior": -12.0, "square": -14.0, "desert": -14.0}
TAIL_MODEL = {"street": "image-source street canyon between two-storey facades, flutter slap-back, street-end echoes",
              "square": "facade echoes at 2d/c with 1/d level and distance low-pass",
              "desert": "ridge/wadi echoes at 2d/c with 1/d level and distance low-pass",
              "interior": "image-source plastered room up to 4th order plus a dense diffuse field"}


def recipe_proc_tail(mid, cfg):
    W = OUTROOT / "SFX/Weapons"
    exc = [W / f"SFX_WPN_{w}_SHOT_01.wav" for w in ("AK", "COMMANDO", "RPK")]
    out = []
    for k, f in enumerate(exc, 1):
        x, _ = A.load(f, mono=True)
        x = A.fade(x[: int(0.15 * SR)], 0.0002, 0.05)          # the dry shot only
        ir = proc_tail_ir(mid, seed=zlib.crc32(mid.encode()) % 997 + 31 * k)
        y = A.convolve(x, ir)
        n = int(cfg["dur"] * SR)
        y = A.pad_to(y[:n], n)
        y = A.hp(y, 110, order=2)
        y = gate_floor(y)
        y = A.fade(y, 0.002, 0.5, curve=2.0)
        if cfg["zone"] in ("street", "interior"):
            # the first ~50 ms of reflections sit under the direct shot (forward masking; the transient take carries its
            # own early reflections): ramp them in so the stack keeps its headroom for the crack (layer balance)
            y = y * A.gain_env(len(y), [(0, -18), (0.05, 0), (10, 0)])
        y = loud(y, cfg["cat"], max_gr=4)
        cap = TAIL_PEAK_DB.get(cfg["zone"])
        if cap is not None and A.db(np.abs(y).max()) > cap:
            # a reflection must stay well under the shot's own peak (first facade echo ~-15..-20 dB re the muzzle blast
            # at the shooter's ear): cap the slap-back peaks, keep the reverberant energy (30 ms release)
            y = A.limit(y, 0.0, cap, lookahead_ms=1.0, release_ms=30)
        emit(mid, "SFX/Weapons", f"{mid}_{k:02d}", y, level=LEVELS[cfg["cat"]], kind="weapon_tail", sources=[f],
             notes=(f"Zone tail {k}/3 for ZoneType {cfg['zone']} (GDD §10.2): reflections only, no direct sound; built by "
                    f"convolving our {f.stem} dry shot with a synthetic {cfg['zone']} IR ({TAIL_MODEL[cfg['zone']]}, "
                    f"RT60 ~{PROC_TAIL[mid]['rt']} s). Played with every shot in that zone at the "
                    "shot's instant, pre-balanced for cue volume 1.0; on full-auto play it on trigger release (and every "
                    "~4th round), not on every round. Not looping."),
             extra=dict(selection=dict(take="procedural", excitation=f.name)))
        out.append(dict(take="procedural", excitation=f.name))
    return out


def recipe_tails():
    rep = {}
    for mid, cfg in TAILS.items():
        if mid in PROC_TAIL:
            rep[mid] = recipe_proc_tail(mid, cfg)
            continue
        scored = []
        for r in cands(mid):
            x = clean(ld(r), 60)
            x = A.trim(x, on_db=-30, floor_db=-66, pre_ms=1, fade_ms=40)
            rr = late_rerise(x)
            rt = A.decay_rt(x)
            dec = A.decays(x[: int(cfg["dur"] * SR)], min_drop_db=15)   # a tail must die away (QA: SQUARE t?/ plateau)
            s = lscore(r) - (1.0 if rr else 0) + (0.2 if np.isfinite(rt) and rt > cfg["rt"] * 0.7 else 0) - (1.0 if not dec else 0)
            scored.append((s, r, x, dict(rerise=rr, rt=round(rt, 2) if np.isfinite(rt) else None, decays=dec)))
        scored.sort(key=lambda z: -z[0])
        rep[mid] = []
        ok = [z for z in scored if not z[3]["rerise"] and z[3]["decays"]]
        if len(ok) < 3:
            ok += [z for z in scored if z not in ok][: 3 - len(ok)]
        for k, (s, r, x, info) in enumerate(pick_diverse(ok, 3), 1):
            y = x.copy()
            # remove the direct sound of the take's own shot: -24 dB at the onset rising to 0 dB at cut_ms
            y = y * A.gain_env(len(y), [(0, -24), (cfg["cut_ms"] / 1000 * 0.5, -10), (cfg["cut_ms"] / 1000, 0), (10, 0)])
            rt = info["rt"] or cfg["rt"]
            y = A.exp_shorten(y, rt, cfg["rt"], start_s=cfg["cut_ms"] / 1000)
            y = A.hp(y, 110, order=2)
            y = A.lp(y, cfg["lp"], order=2)
            # resume pass 2: kill the take's noise-floor plateau (composites showed -55 dB hiss to the end): once the
            # 20 ms envelope is 40 dB under the peak, keep decaying at 120 dB/s
            y = gate_floor(y)
            n = int(cfg["dur"] * SR)
            y = A.pad_to(y, n)
            y = A.fade(y, 0.002, 0.5, curve=2.0)
            y = loud(y, cfg["cat"], max_gr=4)
            emit(mid, "SFX/Weapons", f"{mid}_{k:02d}", y, level=LEVELS[cfg["cat"]], kind="weapon_tail", sources=[r["file"]],
                 notes=(f"Zone tail {k}/3 for ZoneType {cfg['zone']} (GDD §10.2): reflections only (the take's direct sound "
                        f"is ducked over the first {cfg['cut_ms']} ms), decay shaped to RT60 ~{cfg['rt']} s. Played with every "
                        "shot in that zone at the shot's instant, pre-balanced for cue volume 1.0; on full-auto play it on "
                        "trigger release (and every ~4th round), not on every round. Not looping."),
                 extra=dict(selection=dict(score=round(s, 3), take=tag(r), **info)))
            rep[mid].append(dict(take=tag(r), **info))
    return rep


def composite_previews():
    """Offline preview of the in-game layering (not shipped): transient_01 + body_01 + mech_01 + tail per zone."""
    PREVIEW.mkdir(parents=True, exist_ok=True)
    W = OUTROOT / "SFX/Weapons"
    outs = []
    for mid, cfg in WEAPONS.items():
        t = W / f"{mid}_01.wav"
        if not t.exists():
            continue
        w = mid.replace("SFX_WPN_", "").replace("_SHOT", "")
        cls, tg, _rpm = LAYER_CODE[w]
        parts = [(A.load(t, mono=True)[0], 0, 0)]       # exactly as WeaponShotPlayer plays it: every cue at volume 1.0
        if cls:
            parts.append((A.load(W / f"SFX_WPN_BODY_{cls}_01.wav", mono=True)[0], 0, 0))
        mech = W / f"SFX_WPN_{w}_MECH_01.wav"
        if mech.exists():
            parts.append((A.load(mech, mono=True)[0], 0, 0))
        for zone in ("STREET", "SQUARE", "INTERIOR", "DESERT"):
            tl = W / f"SFX_WPN_TAIL_{zone}_01.wav"
            pp = parts + ([(A.load(tl, mono=True)[0], 20 * np.log10(tg), 0)] if tl.exists() else [])
            y = A.mix(*pp)
            p = PREVIEW / f"{mid}_composite_{zone}.wav"
            A.save(p, y)          # not normalised: the true peak shows the real headroom at cue volume 1.0
            outs.append(p)
    return outs



# ----------------------------------------------------------------------------- layer balance (resume pass 3)
# WeaponShotPlayer (WP06 code, Staging/Audio/Core) plays transient + mech + body + zone tail at once, every layer at its
# cue volume (1.0 from AudioLibraryBuilder) and the transient's +-1.5 dB jitter on transient and body. Measured on the
# pass-2 files that sum peaked at +3..+6.3 dBTP (the body and the mech were each normalised near full scale on their
# own) and the body layer was as loud as the transient (60-80 % of the first 100 ms energy under 200 Hz). This step
# re-levels the shared layers against the transients and then limits each transient with a gain curve LINKED to the
# worst case of the other layers, so every combination stays under BAL['ceil_db'] at cue volume 1.0.
LAYER_CODE = {  # mirrors WeaponSoundProfile.For(): body class, tail gain, rpm (WeaponTable)
    "COMMANDO": ("RIFLE", 1.0, 750), "AK": ("RIFLE", 1.0, 600), "AKMS": ("RIFLE", 1.0, 600),
    "RPK": ("HEAVY", 1.0, 600), "M60D": ("HEAVY", 1.0, 550), "SVD": ("HEAVY", 1.0, 17), "DSHK": ("HEAVY", 1.1, 550),
    "HUSHPUPPY": (None, 0.18, 300)}
BAL = dict(body_rel=-10.0, mech_rel=-14.0, ceil_db=-1.2, jitter_db=1.5, mech_peak_db=-14.0, body_peak_db=-10.0,
           suppressed_rel=-11.0, mech_rel_suppressed=-6.0,
           t_drive_db=2.0, burst_tp_db=0.0)
BODY_SHAPE = {"RIFLE": [(0, 0), (0.03, 0), (0.12, -15), (0.25, -35), (0.32, -50)],
              "HEAVY": [(0, 0), (0.04, 0), (0.16, -15), (0.32, -35), (0.40, -50)]}


def _m100(x):
    return A.momentary_max(x, win=0.1, hop=0.01)


def _rewrite(tgt, x, note_add=None, extra=None):
    """Overwrite an emitted one-shot in place (same name/notes) and refresh its measurement row."""
    f = READY / tgt
    x = np.asarray(x, dtype=np.float64)
    if x.ndim == 1:
        x = x[:, None]
    tp = A.true_peak_db(x)
    if tp > -1.0:
        x = x * A.undb(-1.0 - tp - 0.02)
    A.save(f, x)
    y, _ = A.load(f)
    m = A.measure(y)
    rv = REVIEW.get(tgt, {})
    rv.update(dur_s=m["dur_s"], lufs_i=m["lufs_i"], lufs_m_max=m["lufs_m_max"], true_peak_dbtp=m["true_peak_dbtp"],
              peak_dbfs=m["peak_dbfs"], clipped=m["clipped"], lead_ms=m["lead_ms"], tail_ms=m["tail_ms"],
              centroid_hz=m["centroid_hz"], low_lt150=m["low_lt150"], dc=m["dc"], bytes=f.stat().st_size,
              mmax100=round(_m100(y), 1))
    if extra:
        rv.update(extra)
    REVIEW[tgt] = rv
    if note_add and tgt in ROWS and note_add not in ROWS[tgt]["import_notes"]:
        n = ROWS[tgt]["import_notes"]
        cut = n.rfind(" Source: 48 kHz")
        ROWS[tgt]["import_notes"] = (n[:cut] + " " + note_add + n[cut:]) if cut > 0 else n + " " + note_add


def _linked_gain(t, others_abs, ceil, jit):
    """Gain curve for transient t so that |t*g|*jit + others <= ceil (look-ahead 0.5 ms, release 25 ms)."""
    from scipy.ndimage import maximum_filter1d, minimum_filter1d
    from scipy.signal import resample_poly
    la = int(0.0005 * SR)
    up = np.abs(resample_poly(t[:, 0], 4, 1))[: 4 * len(t)]          # true-peak aware (4x oversampled)
    up = np.pad(up, (0, 4 * len(t) - len(up))).reshape(len(t), 4).max(axis=1)
    a = maximum_filter1d(np.maximum(up, np.abs(t[:, 0])), size=2 * la + 1) * jit
    o = np.zeros(len(t))
    o[: min(len(t), len(others_abs))] = others_abs[: len(t)]
    room = np.maximum(ceil - o, 0.0)
    target = np.minimum(1.0, room / np.maximum(a, 1e-9))
    m = minimum_filter1d(target, size=2 * la + 1)          # look-ahead: every sample within +-la already satisfied
    r = np.exp(-1 / (SR * 0.025))
    g = np.empty_like(m)
    cur = 1.0
    for i in range(len(m)):                                 # release 25 ms (cur <= m everywhere)
        v = m[i]
        cur = v if v < cur else r * cur + (1 - r) * v
        g[i] = cur
    h = max(1, la // 2)
    return np.convolve(g, np.ones(2 * h + 1) / (2 * h + 1), mode="same")   # smooth; stays <= target (window <= la)


def _burst(T, B, M, Z, tg, rpm, n, rng, jit_db):
    """One auto burst at cue volume 1.0 the way WeaponShotPlayer plays it: random transient/body/mech per round with
    +-jitter on transient+body, a zone tail on round 1, every 4th round and on release."""
    dt = 60.0 / rpm if rpm > 0 else 0.5
    L = int((dt * n + 2.5) * SR)
    y = np.zeros((L, 1))
    for k in range(n):
        o = int(k * dt * SR)
        gj = A.undb(rng.uniform(-jit_db, jit_db))
        parts = [T[rng.integers(len(T))] * gj]
        if B:
            parts.append(B[rng.integers(len(B))] * gj)
        if M:
            parts.append(M[rng.integers(len(M))])
        if Z and (k % 4 == 0):
            parts.append(Z[rng.integers(len(Z))] * tg)
        for pz in parts:
            e = min(L, o + len(pz))
            y[o:e] += pz[: e - o]
    return y


def balance_layers():
    W = OUTROOT / "SFX/Weapons"
    tgt = lambda name: f"Assets/_Game/Audio/SFX/Weapons/{name}.wav"   # noqa: E731
    ld1 = lambda name: A.load(W / f"{name}.wav", mono=True)[0]         # noqa: E731
    T = {w: [ld1(f"SFX_WPN_{w}_SHOT_{k:02d}") for k in range(1, 5)] for w in LAYER_CODE}
    t_lvl = {w: float(np.median([_m100(x) for x in T[w]])) for w in T}
    t_ref = float(np.median([t_lvl[w] for w in ("AK", "COMMANDO", "RPK", "M60D")]))
    # a suppressed 9 mm is ~25-30 dB quieter than a 7.62 rifle at the shooter's ear; in game terms it sits
    # BAL['suppressed_rel'] dB under the rifles (the player plays it at cue volume 1.0 like any other gun)
    for w in ("HUSHPUPPY",):
        g = t_ref + BAL["suppressed_rel"] - t_lvl[w]
        T[w] = [x * A.undb(g) for x in T[w]]
        t_lvl[w] += g
    rep = dict(t_ref_mmax100=round(t_ref, 1), weapons={})
    # 1. bodies: soft 4 ms onset (the low boom builds up after the crack), shorter decay, level t_ref + body_rel
    B = {}
    for cls in ("RIFLE", "HEAVY"):
        B[cls] = []
        for j in (1, 2):
            name = f"SFX_WPN_BODY_{cls}_{j:02d}"
            x = ld1(name)
            n = min(len(x), int(BODY_SHAPE[cls][-1][0] * SR))
            x = x[:n] * A.gain_env(n, BODY_SHAPE[cls])
            ramp = 0.5 - 0.5 * np.cos(np.pi * np.clip(np.arange(n) / (0.004 * SR), 0, 1))
            # the sub boom blooms after the crack (the transient take carries the blast's own onset): -12 dB to 6 ms,
            # full at 18 ms, so the crack keeps the stack's headroom
            x = A.remove_dc(x * ramp[:, None] * A.gain_env(n, [(0, -12), (0.006, -12), (0.018, 0), (10, 0)]), 20)
            x = A.fade(x, 0.0, 0.03)
            x = x * A.undb(t_ref + BAL["body_rel"] - _m100(x))
            if A.db(np.abs(x).max()) > BAL["body_peak_db"]:
                x = A.limit(x, 0.0, BAL["body_peak_db"], lookahead_ms=1.0, release_ms=30)
            B[cls].append(x)
    # 2. mechs: one cycle (auto weapons fire every 80-110 ms), peaks capped, level per weapon
    M = {}
    for w, (cls, tg, rpm) in LAYER_CODE.items():
        if not (W / f"SFX_WPN_{w}_MECH_01.wav").exists():
            M[w] = []
            continue
        M[w] = []
        for i in (1, 2):
            x = ld1(f"SFX_WPN_{w}_MECH_{i:02d}")
            # the hammer/firing-pin click at t=0 is masked by the blast; the bolt carrier moves after ~10-20 ms
            x = x * A.gain_env(len(x), [(0, -24), (0.03, -6), (0.05, 0), (10, 0)])
            if rpm >= 500:
                n = min(len(x), int(0.2 * SR))
                x = x[:n] * A.gain_env(n, [(0, 0), (0.085, 0), (0.13, -18), (0.2, -40)])
                x = A.fade(x, 0.0, 0.02)
            x = x * A.undb(t_lvl[w] + BAL["mech_rel_suppressed" if w == "HUSHPUPPY" else "mech_rel"] - _m100(x))
            if A.db(np.abs(x).max()) > BAL["mech_peak_db"]:
                x = A.limit(x, 0.0, BAL["mech_peak_db"], lookahead_ms=0.5, release_ms=20)
            M[w].append(x)
    # 3. tails (shared, at their category levels)
    Z = [ld1(f"SFX_WPN_TAIL_{z}_{k:02d}") for z in ("STREET", "SQUARE", "INTERIOR", "DESERT") for k in (1, 2, 3)]
    ceil = A.undb(BAL["ceil_db"])
    jit = 1.0          # bound at nominal volume; the +-1.5 dB jitter worst case is reported (worst_stack_tp)
    rng = np.random.default_rng(7)
    for w, (cls, tg, rpm) in LAYER_CODE.items():
        bs = B.get(cls, []) if cls else []
        ms = M[w]
        L = max(len(x) for x in T[w])
        o_env = np.zeros(L)
        for b in (bs or [np.zeros((1, 1))]):
            for m in (ms or [np.zeros((1, 1))]):
                for z in Z:
                    s_ = np.zeros(L)
                    for part, g in ((b, jit), (m, 1.0), (z, tg)):
                        nn = min(L, len(part))
                        s_[:nn] += part[:nn, 0] * g
                    o_env = np.maximum(o_env, np.abs(s_))
        from scipy.ndimage import maximum_filter1d
        o_env = maximum_filter1d(o_env, size=int(0.0006 * SR) + 1)
        before = [_m100(A.mix((t, 0, 0), *([(bs[0], 0, 0)] if bs else []), *([(ms[0], 0, 0)] if ms else []))) for t in T[w]]
        c_db = BAL["ceil_db"]
        newT = []
        for t in T[w]:
            if w != "HUSHPUPPY":
                t = t * A.undb(BAL["t_drive_db"])
            if rpm >= 500:
                # auto fire: the take's own first slap (60-120 ms) would land on the next round's crack; the zone tail
                # supplies the space, so the take's late part is ducked 6 dB (smaller burst trim, louder crack)
                t = t * A.gain_env(len(t), [(0, 0), (0.05, 0), (0.09, -6), (10, -6)])
            g = _linked_gain(t, o_env, A.undb(c_db), jit)
            newT.append(t * g[:, None])
        others_db = float(A.db(o_env.max()))
        # verify every combination (signed sum, true peak) at nominal volume and with +jitter on transient and body
        worst, worst_j = -99.0, -99.0
        for t in newT:
            for b in (bs or [np.zeros((1, 1))]):
                for m in (ms or [np.zeros((1, 1))]):
                    for z in Z:
                        y = A.mix((t, 0, 0), (b, 0, 0), (m, 0, 0), (z, 20 * np.log10(tg), 0))
                        worst = max(worst, A.true_peak_db(y[: int(0.4 * SR)]))
                        yj = A.mix((t, BAL["jitter_db"], 0), (b, BAL["jitter_db"], 0), (m, 0, 0), (z, 20 * np.log10(tg), 0))
                        worst_j = max(worst_j, A.true_peak_db(yj[: int(0.4 * SR)]))
        bursts = [A.true_peak_db(_burst(newT, bs, ms, Z, tg, rpm, 12, rng, BAL["jitter_db"])) for _ in range(8)] if rpm >= 300 else []
        burst_trim = 0.0
        if bursts and float(np.percentile(bursts, 75)) > BAL["burst_tp_db"]:
            # auto fire overlaps the previous rounds' body/mech/decay: trim this weapon's transients uniformly
            burst_trim = BAL["burst_tp_db"] - float(np.percentile(bursts, 75))
            newT = [t * A.undb(burst_trim) for t in newT]
            bursts = [A.true_peak_db(_burst(newT, bs, ms, Z, tg, rpm, 12, rng, BAL["jitter_db"])) for _ in range(8)]
        stacks = [A.mix((t, 0, 0), *([(bs[0], 0, 0)] if bs else []), *([(ms[0], 0, 0)] if ms else [])) for t in newT]
        after = [_m100(y) for y in stacks]
        n1 = int(0.1 * SR)
        low_share = float(np.median([(A.lp(y[:n1], 200, order=4) ** 2).sum() / ((y[:n1] ** 2).sum() + 1e-12) for y in stacks]))
        for k, t in enumerate(newT, 1):
            _rewrite(tgt(f"SFX_WPN_{w}_SHOT_{k:02d}"), t,
                     note_add=("Layer-balanced (resume pass 3): limited with a gain curve linked to the worst case of the body, "
                               "mech and zone-tail layers, so transient+body+mech+tail at cue volume 1.0 (+1.5 dB jitter) stays "
                               f"under {BAL['ceil_db']} dBTP; keep every layer's cue volume at 1.0."))
        rep["weapons"][w] = dict(transient_mmax100=round(t_lvl[w], 1), stack_mmax100_before=round(float(np.median(before)), 1),
                                 stack_mmax100_after=round(float(np.median(after)), 1), stack_low200_share=round(low_share, 2), worst_stack_tp=round(worst, 2), worst_stack_tp_jitter=round(worst_j, 2),
                                 burst_tp_max=round(max(bursts), 2) if bursts else None, burst_tp_p75=round(float(np.percentile(bursts, 75)), 2) if bursts else None, burst_trim_db=round(burst_trim, 2),
                                 transient_gr_db=round(float(np.median([A.db(np.abs(a).max()) - A.db(np.abs(b).max()) for a, b in zip(newT, T[w])])), 1),
                                 others_worst_dbfs=round(others_db, 2))
    for cls in B:
        for j, x in enumerate(B[cls], 1):
            _rewrite(tgt(f"SFX_WPN_BODY_{cls}_{j:02d}"), x,
                     note_add=(f"Layer-balanced (resume pass 3): 4 ms soft onset, level "
                               f"{BAL['body_rel']} dB under the player transients (100 ms momentary max); short (-15 dB at {BODY_SHAPE[cls][2][0]} s) so auto fire does not pile up. WeaponShotPlayer maps "
                               + ("COMMANDO, AK and AKMS" if cls == "RIFLE" else "RPK, SVD, M60D and DSHK") + " to this body."))
    for w in M:
        for i, x in enumerate(M[w], 1):
            _rewrite(tgt(f"SFX_WPN_{w}_MECH_{i:02d}"), x,
                     note_add=(f"Layer-balanced (resume pass 3): {BAL['mech_rel']} dB under the {w} transients, peaks capped at "
                               f"{BAL['mech_peak_db']} dBFS" + (", one bolt cycle (0.2 s) so auto fire does not pile up." if LAYER_CODE[w][2] >= 500 else ".")))
    # the suppressed pistol's far bake follows its near level
    for k in (1, 2):
        ft = f"Assets/_Game/Audio/SFX/Weapons/Far/SFX_WPN_HUSHPUPPY_SHOT_FAR_{k:02d}.wav"
        if (READY / ft).exists():
            x, _ = A.load(READY / ft, mono=True)
            _rewrite(ft, x * A.undb(BAL["suppressed_rel"]),
                     note_add=f"Level {BAL['suppressed_rel']} dB under the rifle far bakes (suppressed pistol, resume pass 3).")
    rep["bal"] = BAL
    return rep

# ============================================================================= GENERIC RECIPES
def end_hot(x, rel_db=-22):
    """The sound is still going when the file ends (Mirelo onset too late): last 60 ms within rel_db of the peak."""
    env = A.envelope(x, 20)
    return bool(env[-int(0.06 * SR):].max() > env.max() * A.undb(rel_db))


def raw_clipped(r):
    return int(r["meas"]["clipped"])


def oneshot(mid, n, cat, sub="SFX/SetPieces", *, max_len=None, min_len=0.12, hp=40, lp=None, max_gr=4, clip=True,
            notes="", name=None, takes=None, fade_ms=80, on_db=-35, floor_db=-62, pre_ms=3, shape=None, stereo=False,
            mono_src=True, kind="oneshot", start_offset=0.0, focus=None, prefer=None, allow_hot=False, rank=None):
    """focus = dict(gap_s=..., tail_db=...) keeps only the take's main event cluster (A.focus_main);
    prefer = {take: bonus} nudges the ranking towards a retake batch."""
    name = name or mid
    scored = []
    for r in cands(mid, takes):
        x = clean(ld(r, mono=mono_src and not stereo), hp)
        hot = end_hot(x)
        y = A.trim(x, on_db=on_db, floor_db=floor_db, pre_ms=pre_ms, fade_ms=fade_ms)
        finfo = {}
        if focus:
            y, finfo = A.focus_main(y, gap_s=focus.get("gap_s", 0.12), on_db=focus.get("on_db", -30),
                                    tail_db=focus.get("tail_db", -45), pre_ms=pre_ms, fade_ms=min(fade_ms, 60))
        if start_offset:
            y = y[int(start_offset * SR):]
        if max_len and len(y) > max_len * SR:
            y = A.fade(y[: int(max_len * SR)], 0.0005, min(0.25, max_len * 0.25))
        trunc = (hot and not allow_hot) or len(y) < min_len * SR   # allow_hot: a sustained bed cut with a fade
        l = listen_of(r)
        off = l.get("match") is not None and l["match"] <= 3
        tone = hf_tone_db(y) if len(y) > 4096 else 0.0
        sc = lscore(r) - (1.0 if trunc else 0) - (0.6 if raw_clipped(r) > 50 else 0.15 if raw_clipped(r) > 0 else 0) \
            - (1.0 if off else 0) - (0.8 if tone > 45 else 0) + (prefer or {}).get(r["take"], 0) + (rank(y) if rank else 0.0)
        scored.append((sc, r, y, dict(trunc=trunc, clipped=raw_clipped(r), off_brief=off, tone_db=round(tone, 1), **finfo)))
    scored.sort(key=lambda z: -z[0])
    sel = pick_diverse(scored, n)
    ys = []
    for sc, r, y, info in sel:
        if lp:
            y = A.lp(y, lp, order=2)
        if shape:
            y = shape(y)
        ys.append(y)
    ys = loud_set(ys, cat, max_gr=max_gr, clip=clip) if n > 1 else [loud(ys[0], cat, max_gr=max_gr, clip=clip)]
    out = []
    for k, ((sc, r, _y, info), y) in enumerate(zip(sel, ys), 1):
        fname = f"{name}_{k:02d}" if n > 1 or name == mid else name
        emit(mid, sub, fname, y, level=LEVELS[cat], kind=kind, sources=[r["file"]], stereo=stereo,
             notes=(notes + f" Variant {k}/{n}." if n > 1 else notes) + " Not looping.",
             extra=dict(selection=dict(score=round(sc, 3), take=tag(r), **info)))
        out.append(dict(take=tag(r), score=round(sc, 3), **info))
    return out


def sliced(mid, n, cat, sub, *, takes=None, min_gap=0.3, max_len=0.8, min_len=0.06, rise_db=10, floor_db=-40,
           name=None, notes="", hp=40, lp=None, inner_after=0.15, inner_rise=10, max_gr=3, pre_ms=3, fade_ms=40,
           ev_ms=4, level_spread=8, keep_order=True, clip=True, file_ok=None, max_per_file=None):
    """Slice a sequence take (footsteps, impacts, bounces) into one-shots by onset, keep the n cleanest and level them
    consistently (round-robin variants must not jump in level)."""
    name = name or mid
    evs = []
    for r in cands(mid, takes):
        if file_ok and not file_ok(r):
            continue
        x = clean(ld(r), hp)
        ev = A.detect_events(x, min_gap=min_gap, rise_db=rise_db, floor_db=floor_db, min_len=min_len, max_len=max_len, ms=ev_ms)
        for i, (a, b) in enumerate(ev):
            seg = x[max(0, a - int(pre_ms / 1000 * SR)):b].copy()
            seg = A.fade(seg, 0.0005, min(fade_ms / 1000, len(seg) / SR * 0.3))
            if len(seg) < min_len * SR:
                continue
            env = A.db(A.envelope(seg, 10))
            ia = int(inner_after * SR)
            inner = False
            if len(env) > ia + 10:
                runmin = np.minimum.accumulate(env[ia:])
                inner = bool(((env[ia:] - runmin) > inner_rise).any())
            lvl = A.momentary_max(seg, win=0.1, hop=0.01)
            evs.append(dict(r=r, seg=seg, lvl=lvl, inner=inner, pos=i, t=a / SR, crest=float(A.db(np.abs(seg).max()) - A.db(np.sqrt(np.mean(seg ** 2)) + 1e-12))))
    if not evs:
        return []
    med = float(np.median([e["lvl"] for e in evs]))
    for e in evs:
        e["score"] = lscore(e["r"]) - (0.8 if e["inner"] else 0) - max(0, abs(e["lvl"] - med) - 3) / level_spread \
            + 0.01 * min(e["crest"], 20) - (0.5 if e["lvl"] < med - level_spread else 0)
    evs.sort(key=lambda e: -e["score"])
    if max_per_file:
        per, sel = {}, []
        for e in evs:
            f = e["r"]["file"]
            if per.get(f, 0) < max_per_file:
                sel.append(e)
                per[f] = per.get(f, 0) + 1
            if len(sel) == n:
                break
    else:
        sel = evs[:n]
    if keep_order:
        sel.sort(key=lambda e: (e["r"]["file"], e["t"]))
    segs = []
    for e in sel:
        y = tight(e["seg"])
        if lp:
            y = A.lp(y, lp, order=2)
        segs.append(y)
    ys = loud_set(segs, cat, max_gr=max_gr, clip=clip)
    out = []
    for k, (e, y) in enumerate(zip(sel, ys), 1):
        emit(mid, sub, f"{name}_{k:02d}", y, level=LEVELS[cat], kind="slice", sources=[e["r"]["file"]],
             notes=notes + f" Slice {k}/{len(sel)} (round-robin; avoid repeating the last two). Not looping.",
             extra=dict(selection=dict(take=tag(e["r"]), t=round(e["t"], 3), level=round(e["lvl"], 1), inner=e["inner"])))
        out.append(dict(take=tag(e["r"]), t=round(e["t"], 3), lvl=round(e["lvl"], 1), inner=e["inner"]))
    return out


def singles(mid, n, cat, sub, *, takes_prefix="one", gap_s=0.1, max_len=0.6, min_len=0.08, hp=40, lp=None,
            notes="", name=None, max_gr=4, tail_db=-45, n_min=None, on_db=-30, spread=1.5, fallback=None,
            min_impulse_db=12, max_peak_s=0.12, min_decay_db=12, tone_max=45.0):
    """Pick n one-shots from dedicated single-event takes (resume pass 2: 'one*' takes, 4 samples x 3-4 prompts):
    trim, keep the main event cluster (A.focus_main), reject truncated / off-brief / tonal-artifact takes, level the
    round-robin set consistently. If fewer than n_min survive, `fallback()` (the old slicing recipe) fills the set."""
    name = name or mid
    scored = []
    for r in cands(mid):
        if not r["take"].startswith(takes_prefix):
            continue
        x = clean(ld(r), hp)
        y, finfo = A.focus_main(x, gap_s=gap_s, on_db=on_db, tail_db=tail_db, pre_ms=2, max_len=max_len, fade_ms=40)
        if len(y) > 0.03 * SR:
            y = tight(y, pre_ms=2, on_db=-24)            # no soft pre-roll before the hit/step (lead <= ~5 ms)
        # truncated only if the MAIN cluster runs into the end of the file (a later 2nd event there is cut anyway)
        hot = end_hot(x, -30) and finfo["end_s"] >= len(x) / SR - 0.045 and len(y) < max_len * SR - 10
        l = listen_of(r)
        off = l.get("match") is not None and l["match"] <= 4
        tone = hf_tone_db(y) if len(y) > 4096 else 0.0
        short = len(y) < min_len * SR
        crest = float(A.db(np.abs(y).max()) - A.db(np.sqrt(np.mean(y ** 2)) + 1e-12)) if len(y) else 0
        env = A.db(A.envelope(y, 5)) if len(y) else np.zeros(1)
        impulse = float(env.max() - np.median(env))          # an impact/step must stand out of its own decay
        flat = impulse < min_impulse_db
        # the event must start the clip (peak early) and die away (last 30 % well under the peak): rejects noise beds
        # and takes whose step/hit is buried in a sustained crunch (visual QA of the first sand/gravel singles)
        ipk = int(np.argmax(env)) / SR if len(y) else 0
        tail_med = float(np.median(env[int(len(env) * 0.7):])) if len(env) > 10 else env.max()
        late = bool(ipk > max_peak_s)
        undecayed = bool(env.max() - tail_med < min_decay_db)
        bad = hot or short or off or tone > tone_max or flat or late or undecayed
        sc = lscore(r) + 0.01 * min(crest, 20) - (2.0 if bad else 0) - (0.1 if finfo.get("cut_after") else 0)
        scored.append((sc, r, y, dict(trunc=bool(hot), short=short, off_brief=off, tone_db=round(tone, 1),
                                      impulse_db=round(impulse, 1), peak_s=round(ipk, 3), late=late, undecayed=undecayed,
                                      cut_after=finfo.get("cut_after"), bad=bool(bad))))
    scored.sort(key=lambda z: -z[0])
    good = [z for z in scored if not z[3]["bad"]]
    if fallback and len(good) < (n_min or n):
        return dict(fallback=fallback(), singles_good=len(good))
    # ship only clean singles: a smaller round robin beats padding it with rejected takes
    sel = pick_diverse(good, min(n, len(good)), key=lambda r: r["file"]) if len(good) >= (n_min or n) else \
        pick_diverse(good + [z for z in scored if z[3]["bad"]], n, key=lambda r: r["file"])
    # order the round robin by take so neighbours come from different prompts
    sel = sorted(sel, key=lambda z: (z[1]["file"].split("/")[-1][:2], z[1]["take"]))
    ys = [A.lp(z[2], lp, order=2) if lp else z[2] for z in sel]
    ys = loud_set(ys, cat, max_gr=max_gr, spread=spread)
    out = []
    for k, ((sc, r, _y, info), y) in enumerate(zip(sel, ys), 1):
        emit(mid, sub, f"{name}_{k:02d}", y, level=LEVELS[cat], kind="single", sources=[r["file"]],
             notes=notes + f" Variant {k}/{len(sel)} (round-robin; avoid repeating the last two). Not looping.",
             extra=dict(selection=dict(score=round(sc, 3), take=tag(r), **info)))
        out.append(dict(take=tag(r), score=round(sc, 3), **info))
    return out


def loop_body_fix(x, xfade_s=0.75):
    """EL `loop: true` beds are meant to wrap seamlessly. If the native wrap is not ok (seam_metrics), rebuild the
    seam with an equal-power crossfade of the tail into the head and keep whichever version measures better."""
    sm = A.seam_metrics(x)
    if sm["seam_ok"]:
        return x, sm, False
    y = A.rotate_seamless(x, xfade_s)
    sm2 = A.seam_metrics(y)

    def badness(m):
        return max(0, m["click_ratio"] - 2.5) + max(0, m["seam_rms_step_db"] - m["interior_rms_step_p90_db"]) + \
            max(0, m["seam_spec_dist_db"] - m["interior_spec_p90_db"])
    if sm2["seam_ok"] or badness(sm2) < badness(sm):
        return y, sm, True
    return x, sm, False


def el_loop(mid, n, cat, sub, *, stereo=False, takes=None, hp=30, lp=None, notes="", name=None, guard=False,
            trim_s=None, xfade_s=0.75):
    name = name or mid
    scored = []
    for r in cands(mid, takes):
        x = clean(ld(r, mono=not stereo), hp)
        x = x * A.undb(-6 - float(A.db(np.abs(x).max())))   # pre-gain: very quiet EL beds sit under the -70 LUFS gate
        if trim_s:
            x = x[: int(trim_s * SR)]
        yes = guard_yes(r) if guard else []
        body, sm, fixed = loop_body_fix(x, xfade_s)
        quiet = r["meas"]["peak_dbfs"] < -40          # 16-bit EL output this quiet is ~7-bit audio
        sc = lscore(r) - (0.6 if raw_clipped(r) > 50 else 0.15 if raw_clipped(r) > 0 else 0) - (2.0 if yes else 0) \
            - (0.2 if fixed else 0) - (0.8 if quiet else 0)
        scored.append((sc, r, body, dict(clipped=raw_clipped(r), guard_yes=yes, seam_fixed=fixed, seam_raw=sm)))
    scored.sort(key=lambda z: -z[0])
    out = []
    for k, (sc, r, body, info) in enumerate(scored[:n], 1):
        if lp:
            body = A.lp(body, lp, order=2)
        body = A.loudness_to(body, LEVELS[cat], tp=-1.0)
        pk, lead, nb = A.package_loop(body)
        fname = f"{name}_{k:02d}" if n > 1 else name
        emit(mid, sub, fname, pk, level=LEVELS[cat], kind="loop", sources=[r["file"]], stereo=stereo,
             loop=dict(lead=lead, body=nb, tail=A.LEAD),
             notes=notes + (f" Variant {k}/{n} (alternate loop; crossfade between variants every few cycles to hide repetition)." if n > 1 else ""),
             extra=dict(selection=dict(score=round(sc, 3), take=tag(r), **{k2: v for k2, v in info.items() if k2 != 'seam_raw'}),
                        seam_raw=info["seam_raw"]))
        out.append(dict(take=tag(r), score=round(sc, 3), clipped=info["clipped"], guard_yes=info["guard_yes"], seam_fixed=info["seam_fixed"]))
    return out


def make_far_long(x, lp_hz, seed, dur=4.0):
    """Explosion distance bake: first 1.6 s, darker low-pass, long rolling echo."""
    d = A.fade(x[: int(1.6 * SR)].copy(), 0.0003, 0.6)
    d = A.hp(d, 45, order=2)
    d = A.lp(d, lp_hz, order=2)
    d = A.lp(d, lp_hz * 2.0, order=2)
    ir = far_ir(seed + 500, long=True)
    wet = A.convolve(d, ir)
    wet = wet * (np.sqrt((d ** 2).sum()) / (np.sqrt((wet ** 2).sum()) + 1e-12)) * A.undb(-2)
    y = A.pad_to(A.mix((d, 0, 0), (wet, 0, 0)), int(dur * SR))
    return A.fade(y, 0.0003, 1.0, curve=2.5)


# ----------------------------------------------------------------------------- per-group recipes
PROC = RAW / "_procedural"


def recipe_whizz():
    """Cracks: procedural ballistic N-waves (proc_sfx.py; the Mirelo cracks had doubles and pre-roll noise).
    Whizzes: Mirelo, the main swell only (focus), retake batch t3/t4 preferred."""
    out = []
    fs = sorted(PROC.glob("SFX_BULLET_CRACK_proc_*.wav"))
    ys = loud_set([A.load(f, mono=True)[0] for f in fs], "crack", max_gr=6, spread=2.0)
    for k, (f, y) in enumerate(zip(fs, ys), 1):
        emit("SFX_BULLET_CRACK", "SFX/Whizz", f"SFX_BULLET_CRACK_{k:02d}", y, level=LEVELS["crack"], kind="synth",
             sources=[f], notes=("Supersonic bullet crack (ballistic N-wave + ground reflection + wake zip + street/desert "
                                 "slap, synthesised from physics in Tools/audio/sfx/proc_sfx.py) for NearMiss events "
                                 f"within 3 m (GDD §10.2); 3D at the closest point of the bullet's path. Variant {k}/{len(fs)}. "
                                 "Not looping."))
        out.append(f.name)
    return dict(
        crack=out,
        whiz=oneshot("SFX_BULLET_WHIZ", 4, "whiz", "SFX/Whizz", max_len=0.8, min_len=0.12, hp=150, max_gr=4,
                     on_db=-26, focus=dict(gap_s=0.12, on_db=-22, tail_db=-40), prefer={"t3": 0.15, "t4": 0.15},
                     notes="Bullet whiz past the listener for NearMiss events (pairs with SFX_BULLET_CRACK at random)."),
    )


IMPACTS = {"SFX_IMPACT_SAND": ("impact", 0.8), "SFX_IMPACT_STONE": ("impact", 0.7), "SFX_IMPACT_PLASTER": ("impact", 0.7),
           "SFX_IMPACT_WOOD": ("impact", 0.7), "SFX_IMPACT_METAL": ("impact", 1.0), "SFX_IMPACT_FABRIC": ("impact_soft", 0.6),
           "SFX_IMPACT_FLESH": ("impact_soft", 0.6), "SFX_IMPACT_GLASS": ("impact", 1.0), "SFX_IMPACT_FRUIT": ("impact", 0.8)}


IMPACT_LEN = {"SFX_IMPACT_SAND": 0.7, "SFX_IMPACT_STONE": 0.8, "SFX_IMPACT_PLASTER": 0.8, "SFX_IMPACT_WOOD": 0.7,
              "SFX_IMPACT_METAL": 1.0, "SFX_IMPACT_FABRIC": 0.6, "SFX_IMPACT_FLESH": 0.45, "SFX_IMPACT_GLASS": 1.2,
              "SFX_IMPACT_FRUIT": 0.9}


def recipe_impacts():
    """Resume pass 2: dedicated single-impact takes (12 per surface) replace the sliced 8 s sequences, whose slices
    were often sustained noise or several hits (visual QA)."""
    rep = {}
    for mid, (cat, ml) in IMPACTS.items():
        surf = mid.split("_")[-1].lower()
        note = f"Bullet impact on {surf} (SurfaceTag), 3D at the hit point, pitch +-5 %."

        def fb(mid=mid, cat=cat, ml=ml, note=note):
            return sliced(mid, 8, cat, "SFX/Impacts", min_gap=0.35, max_len=ml, min_len=0.1, rise_db=12,
                          floor_db=-36, inner_after=0.12, max_gr=6, notes=note)
        rep[mid] = singles(mid, 8, cat, "SFX/Impacts", gap_s=0.08, max_len=IMPACT_LEN[mid], min_len=0.06,
                           hp=60 if mid != "SFX_IMPACT_FLESH" else 40, tail_db=-48, max_gr=6, n_min=5, on_db=-26, min_impulse_db=16, max_peak_s=0.06, min_decay_db=15,
                           notes=note, fallback=fb)
    return rep


EXPLOSIONS = {"SFX_EXP_FRAG": (4, 4.0, 900), "SFX_EXP_BREACH": (2, 6.0, 800), "SFX_EXP_RPG": (2, 4.0, 900),
              "SFX_EXP_FIREBALL": (2, 3.0, 800), "SFX_EXP_DEBRIS": (2, 3.0, None)}   # pass 3: fireball 5 -> 3 s (budget)


def recipe_explosions():
    rep = {}
    for mid, (n, ml, far_lp) in EXPLOSIONS.items():
        cat = "debris" if mid == "SFX_EXP_DEBRIS" else "explosion"
        shape = None
        if mid in ("SFX_EXP_FRAG", "SFX_EXP_RPG", "SFX_EXP_BREACH"):
            # resume pass 3: the takes end in a -40 dB noise/rumble plateau with stray late events (FRAG_01 at 3.5 s
            # sounded like a second blast): once 38 dB under the peak the tail keeps decaying (RT60 0.9 s)
            shape = lambda y: gate_floor(y, below_peak_db=38, rt_after=0.9)   # noqa: E731
        rep[mid] = oneshot(mid, n, cat, "SFX/Explosions", max_len=ml, min_len=1.0, hp=25, max_gr=11, fade_ms=300,
                           floor_db=-62, on_db=-20 if cat == "explosion" else -35, shape=shape,
                           notes=("Explosion one-shot (3D, big min distance ~10 m, max ~400 m); pairs with SFX_TINNITUS and "
                                  "camera shake within ~12 m." if cat == "explosion" else
                                  "Debris rain layer after a blast (no blast in it), 3D."))
        if far_lp:
            fl = []
            for k, rr in enumerate(rep[mid][:2], 1):
                src = OUTROOT / "SFX/Explosions" / f"{mid}_{k:02d}.wav"
                x, _ = A.load(src, mono=True)
                y = make_far_long(x, far_lp, seed=k + len(mid))
                y = loud(y, "explosion_far", max_gr=4)
                emit(mid, "SFX/Explosions", f"{mid}_FAR_{k:02d}", y, level=LEVELS["explosion_far"], kind="explosion_far",
                     sources=[src], notes=f"Baked distance variant {k}/2 ({far_lp} Hz low-pass, long rolling echo) for blasts "
                     "beyond ~120 m; crossfade near->far by distance like the guns. Not looping.")
                fl.append(k)
    return rep


def recipe_grenade_rpg_knife():
    return dict(
        pin=oneshot("SFX_GREN_PIN_SPOON", 2, "grenade_small", "SFX/Weapons", max_len=1.2, hp=150, max_gr=4,
                    notes="Frag grenade pin pull + spoon flick (player cook/throw)."),
        bounce=singles("SFX_GREN_BOUNCE", 4, "grenade_small", "SFX/Weapons", takes_prefix="r", gap_s=0.25, max_len=1.2,
                       min_len=0.08, hp=60, tail_db=-45, max_gr=4, n_min=3, min_impulse_db=12, max_peak_s=0.12, min_decay_db=12,
                       notes="Grenade bounce/roll one-shot on stone, sand or sheet metal (3D at the grenade, on its first ground contacts).",
                       fallback=lambda: sliced("SFX_GREN_BOUNCE", 4, "grenade_small", "SFX/Weapons", min_gap=0.4, max_len=1.0, min_len=0.06,
                                               rise_db=10, floor_db=-36, inner_after=0.5, max_gr=4, keep_order=True,
                                               notes="Grenade bounce/roll one-shot (3D at the grenade, on its first ground contacts).")),
        tick=oneshot("SFX_GREN_FUSE_TICK", 2, "grenade_small", "SFX/Weapons", max_len=0.35, min_len=0.02, hp=200, max_gr=4,
                     notes="Soft fuse tick near a live grenade (warning cue, plays with the HUD grenade indicator)."),
        rpg_launch=oneshot("SFX_RPG_LAUNCH", 2, "setpiece_loud", "SFX/Weapons", max_len=3.0, min_len=0.8, hp=30, max_gr=8,
                           fade_ms=300, on_db=-24, notes="RPG-7 launch heard from ~30 m (pop + rocket ignition), 3D at the gunner."),
        rpg_flyby=oneshot("SFX_RPG_FLYBY", 2, "setpiece_loud", "SFX/Weapons", max_len=3.0, min_len=0.8, hp=60, max_gr=4,
                          fade_ms=300, notes="Rocket roaring past close (3D on the rocket; Doppler handled by Unity)."),
        knife_swing=oneshot("SFX_KNIFE_SWING", 3, "foley", "SFX/Weapons", max_len=0.6, min_len=0.1, hp=120, max_gr=3,
                            focus=dict(gap_s=0.08, on_db=-24, tail_db=-40), prefer={"one1": 0.1},
                            notes="Melee knife swing whoosh (player)."),
        knife_hit=oneshot("SFX_KNIFE_HIT", 3, "impact_soft", "SFX/Weapons", max_len=0.6, min_len=0.08, hp=60, max_gr=5,
                          notes="Melee knife hit through cloth (no gore)."),
    )


FOOTSTEPS = ["SFX_FS_SAND", "SFX_FS_GRAVEL", "SFX_FS_STONE", "SFX_FS_ROOF", "SFX_FS_TILE", "SFX_FS_WOOD",
             "SFX_FS_METALSTAIR", "SFX_FS_SALTCRUST"]


def recipe_footsteps():
    """Resume pass 2: 12 steps per surface from dedicated single-step takes (16 per surface: 4 prompts x 4), the main
    heel-toe cluster only; the old 12 s walking sequences are the fallback when fewer than 8 singles survive."""
    rep = {}
    for mid in FOOTSTEPS:
        surf = mid.replace("SFX_FS_", "").lower()
        note = (f"Footstep on {surf} (SurfaceTag), one heel-toe step per clip, round-robin; walk at volume 1.0, sprint "
                "uses pitch +5 % and +2 dB (GDD §10.2); squad and enemies use the same set in 3D.")

        def fb(mid=mid, note=note):
            return sliced(mid, 12, "footstep", "SFX/Footsteps", min_gap=0.3, max_len=0.55, min_len=0.15, rise_db=9,
                          floor_db=-38, inner_after=0.32, inner_rise=12, max_gr=4, ev_ms=6, notes=note)
        rep[mid] = singles(mid, 12, "footstep", "SFX/Footsteps", gap_s=0.1, max_len=0.5 if "METAL" not in mid else 0.7,
                           min_len=0.1, hp=50, tail_db=-42, max_gr=4, n_min=8, on_db=-28, notes=note, fallback=fb,
                           min_impulse_db=10, max_peak_s=0.12, min_decay_db=14)
    return rep


def recipe_foley():
    return dict(
        land=singles("SFX_MOVE_LAND", 4, "land", "SFX/Foley", takes_prefix="r", gap_s=0.25, max_len=1.0, min_len=0.15,
                     hp=40, tail_db=-45, max_gr=4, n_min=3, min_impulse_db=10, max_peak_s=0.15, min_decay_db=12,
                     notes="Jump landing with gear rattle (scale volume by fall height).",
                     fallback=lambda: sliced("SFX_MOVE_LAND", 4, "land", "SFX/Foley", min_gap=1.0, max_len=1.2, min_len=0.15,
                                             rise_db=12, floor_db=-34, inner_after=0.45, max_gr=4,
                                             notes="Jump landing with gear rattle (scale volume by fall height).")),
        slide=oneshot("SFX_MOVE_SLIDE", 2, "foley", "SFX/Foley", max_len=1.6, min_len=0.6, hp=50, max_gr=3,
                      notes="Player slide on gravel/sand with gear rattle (~1 s)."),
        mantle=oneshot("SFX_MOVE_MANTLE", 2, "foley", "SFX/Foley", max_len=1.6, min_len=0.5, hp=50, max_gr=3,
                       notes="Vault/mantle over a low wall with hand slaps and gear rattle."),
        ladder=singles("SFX_MOVE_LADDER", 6, "foley", "SFX/Foley", gap_s=0.12, max_len=0.6, min_len=0.08, hp=50,
                       tail_db=-42, max_gr=3, n_min=4, notes="One rung step (boot + hand) on the wooden ladder; play per rung.",
                       fallback=lambda: sliced("SFX_MOVE_LADDER", 6, "foley", "SFX/Foley", min_gap=0.3, max_len=0.6,
                                               min_len=0.08, rise_db=10, floor_db=-36, inner_after=0.3, max_gr=3,
                                               notes="One rung step (boot + hand) on the wooden ladder; play per rung.")),
        gear=el_loop("SFX_FOLEY_GEAR", 2, "foley", "SFX/Foley", notes=("Cloth and webbing rustle bed while moving; "
                     "volume scaled with movement speed (GDD §10.2), 2D on the player / 3D on squadmates.")),
        bodyfall=singles("SFX_BODYFALL", 4, "land", "SFX/Foley", takes_prefix="r", gap_s=0.3, max_len=1.4, min_len=0.2,
                         hp=40, tail_db=-45, max_gr=4, n_min=3, min_impulse_db=10, max_peak_s=0.2, min_decay_db=12,
                         notes="Body fall with gear clatter on sand or stone (ragdoll ground contact).",
                         fallback=lambda: sliced("SFX_BODYFALL", 4, "land", "SFX/Foley", min_gap=1.0, max_len=1.4, min_len=0.2,
                                                 rise_db=12, floor_db=-34, inner_after=0.6, max_gr=4,
                                                 notes="Body fall with gear clatter (ragdoll ground contact).")),
    )


def recipe_reloads():
    rep = {}
    for w in ("COMMANDO", "HUSHPUPPY", "AK", "RPK", "M60D"):
        mid = f"SFX_WPN_{w}_RELOAD"
        rep[mid] = {}
        for part in ("magout", "magin", "charge"):
            rep[mid][part] = oneshot(mid, 2, "reload", "SFX/Weapons", takes={part}, name=f"{mid}_{part.upper()}",
                                     max_len=0.8, min_len=0.08, hp=80, max_gr=4, on_db=-28,
                                     focus=dict(gap_s=0.18, on_db=-26, tail_db=-42),
                                     notes=(f"{w} reload part '{part}' (the reload animation fires mag_out / mag_in / charge "
                                            "events; for the RPK mag = drum, for the M60D magout = feed cover open, magin = belt "
                                            "laid + cover shut). 2D on the player, 3D on others."))
    return rep


HANDLING_PARTS = {"dryfire": "dry-fire click (empty magazine)", "raise": "weapon raise / switch-to",
                  "adsin": "aim-in (ADS) cloth + tick", "adsout": "aim-out cloth", "switch": "fire selector click"}


def recipe_handling():
    rep = {}
    for part, desc in HANDLING_PARTS.items():
        if not cands("SFX_WPN_HANDLING", {part}):
            rep[part] = "missing raw take"
            continue
        rep[part] = oneshot("SFX_WPN_HANDLING", 2, "handling", "SFX/Weapons", takes={part},
                            name=f"SFX_WPN_HANDLING_{part.upper()}", max_len=0.7, min_len=0.04, hp=80, max_gr=4,
                            focus=dict(gap_s=0.15 if part in ("raise", "adsin", "adsout") else 0.08, on_db=-26, tail_db=-42),
                            notes=f"Weapon handling: {desc}. 2D on the player.")
    return rep


def _no_whine(r):
    """Reject a raw take with a constant narrow tone above 6 kHz over its whole length (model whine; a casing ring
    starts at the impact and decays, so it does not reach the 45 dB persistence threshold)."""
    x = clean(ld(r), 60)
    return hf_tone_db(x, t1=min(1.0, len(x) / SR)) <= 45 and (listen_of(r).get("match") or 5) >= 6


def recipe_shells():
    """Resume pass 3: the pass-2 stone slices all carried an 11-12 kHz model whine, and slicing the new single-casing
    takes still produced noise smears; now the casings are dedicated single-event takes (Mirelo + ElevenLabs, takes
    stone_*/sand_*) picked like impacts (main event cluster, must stand out of its own decay and die away)."""
    rep = {}
    for surf in ("stone", "sand"):
        rep[surf] = singles("SFX_WPN_SHELLS", 6, "shell", "SFX/Weapons", takes_prefix=surf + "_", name=f"SFX_WPN_SHELLS_{surf.upper()}",
                            gap_s=0.08, max_len=0.5 if surf == "stone" else 0.16, min_len=0.03, hp=300 if surf == "stone" else 120,
                            tail_db=-45, max_gr=3, n_min=2, on_db=-26, min_impulse_db=14 if surf == "stone" else 18,
                            max_peak_s=0.05, min_decay_db=14, tone_max=200.0,  # a casing rings by nature; in sand it just ticks
                            notes=f"Brass casing landing on {surf} (spawned by the shell-ejection physics on first contact, 3D).")
    return rep


# ----------------------------------------------------------------------------- set pieces, UI, synth, ambience
SET_ONESHOT = {  # id: (n, category, max_len_s, hp_hz, note)
    "SFX_SODIUM_STRIKE": (2, "setpiece", 2.0, 40, "Sodium lamp striking on (relay clunk + rising buzz), 3D at the lamp; the ripple of strikes at s 1.97."),
    "SFX_FLOOD_SLAM_ON": (2, "setpiece_loud", 2.0, 30, "Stadium floodlight contactor slamming on, 3D at the mast."),
    "SFX_FLOOD_DIEOFF": (2, "setpiece", 2.0, 40, "Floodlight shot out: glass pop, sparks, dying buzz."),
    "SFX_PROJECTOR_START": (2, "setpiece", 3.0, 40, "16 mm projector lever thrown, motor spin-up into the clatter (then SFX_PROJECTOR_LOOP)."),
    "SFX_FILM_FLAP": (2, "setpiece", 3.0, 40, "Film jam and the loose end flapping on the reel (MID-B)."),
    "SFX_FILM_CAN": (2, "setpiece_quiet", 2.0, 60, "Metal film can opened and set down, reel rattle."),

    "SFX_DRILL_START_STOP": (2, "setpiece", 3.0, 40, "Safe drill spin-up / spin-down around SFX_DRILL_LOOP."),
    "SFX_SATCHEL_PLANT": (2, "setpiece_quiet", 3.0, 60, "Satchel pressed to the wall, straps, igniter click."),

    "SFX_GATE_CREAK": (2, "setpiece", 4.0, 40, "Heavy iron gate swinging open with a long creak."),
    "SFX_ROTOR_APPROACH": (2, "setpiece_loud", 10.0, 25, "Helicopter approaching from far and flaring to land (insertion I-1/I-7, exfil LZ); 3D on the helicopter or 2D in cinematics."),
    "SFX_ROTOR_FLYBY": (2, "setpiece_loud", 8.0, 25, "Helicopter low overhead and away (insertion I-10 departure, exfil)."),
    "SFX_CRT_DEGAUSS": (2, "setpiece", 2.0, 40, "CRT switching on: degauss thunk + rising whine (booth intro shot 4)."),
    "SFX_FLUORESCENT_FLICKER": (2, "setpiece_quiet", 3.0, 40, "Fluorescent tube stuttering on (ballast clicks) then dying."),
    "SFX_LIGHTER": (2, "setpiece_quiet", 2.0, 60, "Steel flip lighter open/strike/close (interrogator)."),
    "SFX_LEATHER_CREAK": (2, "setpiece_quiet", 3.0, 60, "Leather wrist restraints creaking against the steel chair (Calder)."),
    "SFX_CHAIR_CREAK": (2, "setpiece_quiet", 2.0, 60, "Bolted steel chair creak under a shifting person."),
    "SFX_GLOVE_STUBBLE": (2, "setpiece_quiet", 1.5, 80, "Leather glove brushing stubble, very close (booth)."),
    "SFX_TOGGLE_CLICK": (2, "setpiece_quiet", 1.0, 80, "Heavy toggle switch flicked off."),
    "SFX_PHONE": (2, "setpiece_quiet", 3.0, 60, "Rotary desk phone receiver lifted, faint line hum."),
    "SFX_CAMERA_SHUTTER": (3, "setpiece_quiet", 1.5, 80, "35 mm SLR shutter + film advance (Sparrow, insertion I-5)."),
    "SFX_CHEMLIGHT_CRACK": (2, "setpiece_quiet", 1.5, 80, "Chemlight bent, cracked and shaken (after sunset)."),
    "SFX_SMOKE_GRENADE": (2, "setpiece", 4.0, 40, "Smoke grenade pop and hiss."),
    "SFX_FLARE": (2, "setpiece", 6.0, 40, "Parachute flare popping overhead, then fizzing."),
    "SFX_TRUCK_ARRIVE": (2, "setpiece_loud", 6.0, 30, "Diesel truck arriving fast on dirt and braking, tailgate drop (reinforcements)."),
    "SFX_DOOR_KICK": (2, "setpiece_loud", 2.0, 40, "Heavy wooden double door kicked open against stone."),
    "SFX_DOOR_CREAK": (2, "setpiece", 2.0, 60, "Wooden side door creaking open (cinema side door)."),
    "SFX_SHUTTER_LATCH": (2, "setpiece", 2.0, 60, "Metal window shutter slammed shut and latched (G5 civilians)."),
    "SFX_KETTLE": (1, "setpiece_quiet", 4.0, 100, "Kettle starting to whistle behind a wall (G5)."),
    "SFX_FRUIT_SPILL": (2, "setpiece", 3.0, 60, "Crate of oranges spilling and rolling on stone (fruit crates, Beat 3)."),
    "SFX_AWNING_TEAR": (2, "setpiece", 2.0, 60, "Canvas awning ripped by machine-gun fire."),
    "SFX_PIGEONS": (1, "setpiece", 3.0, 100, "Pigeons fluttering out of a rooftop loft."),
    "SFX_HAND_SLAP": (2, "setpiece_quiet", 1.0, 60, "Hand slapping and gripping a shoulder through canvas."),
}

LOOP_LEVEL = {  # id: (n, integrated LUFS, note)
    "SFX_RADIO_STATIC_LOOP": (1, -26, "PRC-77 handset static bed (Tinker), 3D small source."),
    "SFX_SODIUM_BALLAST": (2, -30, "Sodium lamp ballast buzz, 3D at each lamp after it strikes."),
    "SFX_PROJECTOR_LOOP": (1, -22, "16 mm projector running (MID-B), 3D at the projector."),
    "SFX_TRANSMITTER_HUM": (1, -24, "Clockmaker transmitter room hum with relays (station), 3D at the racks."),
    "SFX_DRILL_LOOP": (1, -20, "Core drill boring into the safe (Beat 9), 3D."),
    "SFX_ROTOR_LOOP": (2, -16, "Helicopter hovering nearby (LZ hold, exfil), 3D on the helicopter."),
    "SFX_HELI_CABIN_LOOP": (1, -18, "Inside the open-door cabin in flight (insertion cabin bed, Beat 11), played 2D (spatial blend 0)."),
    "SFX_HELI_ALARM": (1, -20, "Cockpit warning warble (exfil climax), 2D in the cabin."),
    "SFX_TELETYPE": (2, -22, "Teleprinter clatter (booth / title), loop while printing; STG_TITLE uses it as a layer."),
    "SFX_FLUORESCENT_BUZZ": (1, -30, "Fluorescent tube buzz in a small concrete room (booth), 3D at the fixture."),
    "SFX_TRUCK_ENGINE_LOOP": (1, -20, "1970s diesel truck engine idle/rev (technical, cargo trucks), 3D; pitch 0.9-1.2 with speed."),
    "SFX_FIRE_CRACKLE_LOOP": (1, -22, "Burning vehicle crackle (technical wreck, blue hour), 3D."),
    "SFX_HEARTBEAT": (1, -22, "Slow heavy heartbeat (health < 25 %), 2D."),
    "SFX_BREATH_BOOTH": (2, -24, "Calder's ragged breathing in the booth, 2D close."),
    "SFX_CRT_STATIC_LOOP": (1, -24, "CRT speaker static (booth screens, glitch), 3D at the set."),
}

AMB = {  # id: (integrated LUFS, note)
    "AMB_DESERT_WIND": (-22, "Golden-hour desert wind bed (ridge, approach)."),
    "AMB_CICADAS": (-23, "Cicadas in the olive trees (golden hour; stop at s 1.97)."),
    "AMB_CRICKETS": (-25, "Crickets after sunset (start at s 1.97)."),
    "AMB_TOWN_CURFEW": (-22, "Empty town under curfew at sunset (shutters, far dog, wind in cables)."),
    "AMB_GENERATOR_HUM": (-22, "Diesel generator thump near the station (AmbienceDirector by distance zone)."),
    "AMB_SALTPAN_WIND": (-23, "Blue-hour wind across the salt pan."),
    "AMB_ROOMTONE_BOOTH": (-24, "Booth room tone (underground concrete, hum, ventilation)."),
    "AMB_BASEMENT_TUBES": (-23, "Station basement full of radio equipment (tube hum, relays, ventilation)."),
    "AMB_DISTANT_GUNFIRE": (-24, "Distant sporadic gunfire across town (blue hour / exfil)."),
    "AMB_ROOMTONE_HALL": (-24, "Large stone hall room tone with ceiling fans."),
    "AMB_CINEMA_INTERIOR": (-24, "Abandoned cinema auditorium (creaks, dust, exit-sign buzz, far wind)."),
}


def _parts(mid, prefix, n, *, max_len, hp=40, min_match=6):
    """Best n part takes (take name starts with prefix) by machine listen, trimmed to their main event cluster."""
    sc = []
    for r in cands(mid):
        if not r["take"].startswith(prefix):
            continue
        l = listen_of(r)
        if (l.get("match") or 0) < min_match:
            continue
        x = clean(ld(r), hp)
        y, _fi = A.focus_main(x, gap_s=0.25, on_db=-30, tail_db=-50, pre_ms=3, max_len=max_len, fade_ms=120)
        if len(y) < 0.05 * SR:
            continue
        sc.append((lscore(r) + 0.05 * (l.get("match") or 0), r, y))
    sc.sort(key=lambda z: -z[0])
    return pick_diverse(sc, n, key=lambda r: r["file"])[:n]


def _thump(seed, f0=95.0, dur=0.12):
    """Tube implosion / glass-envelope pop: a short decaying low sine burst with a click (pure synthesis)."""
    rng = np.random.default_rng(seed)
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = f0 * (1 + 0.6 * np.exp(-t / 0.01))
    ph = 2 * np.pi * np.cumsum(f) / SR
    y = np.sin(ph) * np.exp(-t / 0.025)
    clk = rng.standard_normal(n) * np.exp(-t / 0.002)
    return (0.8 * y + 0.25 * A.hp(clk[:, None], 1500, order=2)[:, 0])[:, None]


def recipe_compound():
    """Resume pass 3: set pieces no model renders in one take (every candidate was judged off-brief) are layered from
    separately generated parts, like a sound designer would: bolt-cutter snap + heavy chain fall; tube burst + arc
    crackle + a synthesised implosion pop. Falls back to the best whole take when parts are missing."""
    rep = {}
    snaps, chains = _parts("SFX_BOLTCUTTER_CHAIN", "snap", 2, max_len=0.8), _parts("SFX_BOLTCUTTER_CHAIN", "chain", 2, max_len=2.4)
    if len(snaps) >= 1 and len(chains) >= 1:
        ys, srcs = [], []
        for k in range(2):
            sn, ch_ = snaps[k % len(snaps)], chains[k % len(chains)]
            y = A.mix((sn[2], 0, 0), (ch_[2], -2 - 2 * k, 0.10 + 0.06 * k))
            ys.append(A.fade(y[: int(3.0 * SR)], 0.0005, 0.3))
            srcs.append([sn[1]["file"], ch_[1]["file"]])
        ys = loud_set(ys, "setpiece", max_gr=4)
        for k, (y, sr_) in enumerate(zip(ys, srcs), 1):
            emit("SFX_BOLTCUTTER_CHAIN", "SFX/SetPieces", f"SFX_BOLTCUTTER_CHAIN_{k:02d}", y, level=LEVELS["setpiece"],
                 kind="oneshot", sources=sr_, notes=("Bolt cutters snapping a heavy chain, chain clattering down (layered: "
                 f"cutter snap + chain fall 100-160 ms later). Variant {k}/2. 3D at the gate. Not looping."),
                 extra=dict(selection=dict(take="compound", parts=[pathlib.Path(z).parent.name for z in sr_])))
        rep["SFX_BOLTCUTTER_CHAIN"] = [[pathlib.Path(z).parent.name for z in s_] for s_ in srcs]
    else:
        rep["SFX_BOLTCUTTER_CHAIN"] = oneshot("SFX_BOLTCUTTER_CHAIN", 2, "setpiece", "SFX/SetPieces", max_len=3.0, min_len=0.3,
                                              hp=60, max_gr=4, fade_ms=200, on_db=-28, pre_ms=8, takes={"t1", "r1", "r2", "e1", "e2", "e3", "e4"},
                                              notes="Bolt cutters snapping a chain, chain clattering down.")
    glass, arcs = _parts("SFX_TUBE_RACK_SHATTER", "glass", 4, max_len=2.0, min_match=3), _parts("SFX_TUBE_RACK_SHATTER", "arc", 2, max_len=2.0)
    if not glass:
        glass = _parts("SFX_TUBE_RACK_SHATTER", "e", 2, max_len=2.4, min_match=3)
    if glass and arcs:
        ys, srcs = [], []
        for k in range(2):
            g_, g2, a_ = glass[(2 * k) % len(glass)], glass[(2 * k + 1) % len(glass)], arcs[k % len(arcs)]
            # two tubes burst ~90-140 ms apart (bullets through the rack), then the arc
            y = A.mix((_thump(k, 90 + 25 * k), -6, 0), (g_[2], 0, 0.004), (_thump(k + 7, 110), -9, 0.09 + 0.05 * k),
                      (g2[2], -3, 0.094 + 0.05 * k), (a_[2], -5, 0.12 + 0.05 * k))
            ys.append(A.fade(y[: int(3.0 * SR)], 0.0005, 0.4))
            srcs.append([g_[1]["file"], g2[1]["file"], a_[1]["file"]])
        ys = loud_set(ys, "setpiece_loud", max_gr=4)
        for k, (y, sr_) in enumerate(zip(ys, srcs), 1):
            emit("SFX_TUBE_RACK_SHATTER", "SFX/SetPieces", f"SFX_TUBE_RACK_SHATTER_{k:02d}", y, level=LEVELS["setpiece_loud"],
                 kind="oneshot", sources=sr_, notes=("Vacuum-tube rack shot (basement racks, D066), layered: synthesised "
                 "implosion pops + two glass tubes bursting ~0.1 s apart + high-voltage arc crackle. Variant "
                 f"{k}/2. 3D at the rack. Not looping."),
                 extra=dict(selection=dict(take="compound", parts=[pathlib.Path(z).parent.name for z in sr_])))
        rep["SFX_TUBE_RACK_SHATTER"] = [[pathlib.Path(z).parent.name for z in s_] for s_ in srcs]
    else:
        rep["SFX_TUBE_RACK_SHATTER"] = oneshot("SFX_TUBE_RACK_SHATTER", 2, "setpiece_loud", "SFX/SetPieces", max_len=3.0,
                                               min_len=0.3, hp=40, max_gr=4, fade_ms=200, on_db=-28, pre_ms=8,
                                               takes={"t1", "r1", "r2", "e1", "e2", "e3", "e4"},
                                               notes="Vacuum-tube rack shot: glass bursts and arcing (basement racks, D066).")
    return rep


def _foley_lift(y):
    """Quiet foley sequences (straps, canvas) sit 25-35 dB under their one click: a slow 3:1 compressor 30 dB under
    the peak lifts the handling so it reads in game."""
    pk = float(A.db(np.abs(y).max()))
    return A.compress(y, pk - 30, 3, attack_ms=5, release_ms=150, knee_db=8)


def _rising(y):
    """+0.5 for a take that gets louder over its length (a kettle starting to whistle), 0 for a flat or falling one."""
    e = A.envelope(y, 50)
    n = len(e)
    if n < 8:
        return 0.0
    first, last = float(A.db(e[: n // 4].mean() + 1e-9)), float(A.db(e[3 * n // 4:].mean() + 1e-9))
    return float(np.clip((last - first) / 20.0, 0.0, 0.5))


SET_KW = {  # resume pass 3: per-cue shaping after the visual review of the retakes
    "SFX_AWNING_TEAR": dict(focus=dict(gap_s=0.3, on_db=-30, tail_db=-50)),     # the tear must start the clip
    "SFX_SATCHEL_PLANT": dict(shape=_foley_lift),
    "SFX_KETTLE": dict(allow_hot=True, rank=lambda y: _rising(y)),   # the whistle should still be rising at the fade
    "SFX_FLOOD_SLAM_ON": dict(max_gr=8),     # hero moment (Beat 6): a peaky contactor clunk needs density to reach -15
}


def recipe_setpieces():
    rep = recipe_compound()
    for mid, (n, cat, ml, hp, note) in SET_ONESHOT.items():
        if not cands(mid):
            rep[mid] = "no raw takes"
            continue
        kw = dict(SET_KW.get(mid, {}))
        rep[mid] = oneshot(mid, n, cat, "SFX/SetPieces", max_len=ml, min_len=min(0.3, ml * 0.3), hp=hp, max_gr=kw.pop("max_gr", 4),
                           fade_ms=200 if ml >= 3 else 80, notes=note, on_db=-28, pre_ms=8,
                           prefer={"t3": 0.2} if mid in ("SFX_FLARE", "SFX_FLUORESCENT_FLICKER") else None,
                           **kw)
    # siren: long, distant, not looping, keep the wind-down shape
    rep["SFX_CURFEW_SIREN"] = oneshot("SFX_CURFEW_SIREN", 1, "setpiece", "SFX/SetPieces", max_len=20.0, min_len=8.0,
                                      hp=60, max_gr=2, fade_ms=1500, on_db=-40, floor_db=-70,
                                      notes="Civil-defence hand siren winding down over the town (GDD golden-hour 'siren tail', insertion I-11), 3D far away or 2D in cinematics.")
    rep["SFX_SPARKS"] = sliced("SFX_SPARKS", 4, "setpiece", "SFX/SetPieces", min_gap=0.5, max_len=1.2, min_len=0.15,
                               rise_db=10, floor_db=-36, inner_after=0.5, max_gr=4,
                               notes="Electrical spark burst / arc crackle (racks, floodlights, tube shatter).")
    rep["SFX_PEN_TAP"] = sliced("SFX_PEN_TAP", 4, "setpiece_quiet", "SFX/SetPieces", min_gap=0.2, max_len=0.35,
                                min_len=0.04, rise_db=12, floor_db=-34, inner_after=0.12, max_gr=4,
                                notes="Single metal pen tap on the steel table (interrogator; play 2-4 in a row).")
    for mid, (n, lufs, note) in LOOP_LEVEL.items():
        LEVELS[f"loop_{mid}"] = lufs
        rep[mid] = el_loop(mid, n, f"loop_{mid}", "SFX/SetPieces", hp=25, notes=note + " Mono 3D source.")
    return rep


def recipe_extras():
    """Extras the manifest does not list (GDD intro shot 5f: the booth reel-to-reel playing TALLOW's tape)."""
    LEVELS["loop_EXTRA_SFX_REEL_TO_REEL_LOOP"] = -28
    return dict(
        reel=el_loop("EXTRA_SFX_REEL_TO_REEL_LOOP", 1, "loop_EXTRA_SFX_REEL_TO_REEL_LOOP", "SFX/SetPieces", hp=40,
                     name="SFX_REEL_TO_REEL_LOOP", notes="EXTRA (not in the manifest): booth reel-to-reel deck running under the TALLOW tape (D002/D003), 3D at the deck."),
        transport=oneshot("EXTRA_SFX_TAPE_TRANSPORT", 2, "setpiece_quiet", "SFX/SetPieces", name="SFX_TAPE_TRANSPORT",
                          max_len=3.0, min_len=0.3, hp=50, max_gr=4,
                          notes="EXTRA (not in the manifest): reel-to-reel play/stop key clunk + capstan engage (tape start/stop in the booth)."),
        overheat=oneshot("EXTRA_SFX_WPN_M60D_OVERHEAT", 2, "setpiece_quiet", "SFX/SetPieces", name="SFX_WPN_M60D_OVERHEAT",
                         max_len=4.0, min_len=1.0, hp=60, max_gr=4, fade_ms=600,
                         notes=("EXTRA (WP03 request, REQUESTS.md 11:15): door-gun barrel cooling after a long burst (hiss, "
                                "ticks); WP03 plays cue SFX_WPN_M60D_OVERHEAT when the M60D overheats. 2D on the player.")),
        downwash=oneshot("EXTRA_SFX_DOWNWASH_SAND", 2, "setpiece", "SFX/SetPieces", name="SFX_DOWNWASH_SAND",
                         max_len=6.0, min_len=2.0, hp=40, max_gr=3, fade_ms=800, on_db=-40, allow_hot=True,
                         notes=("EXTRA (CINEMATIC_DIRECTION §8.2a I-7 'sand hiss on the lens'): rotor downwash blasting sand "
                                "grit and wind, no engine; layer under SFX_ROTOR_APPROACH at the LZ (2D in the cinematic, or "
                                "3D at the landing point).")),
        ramp=oneshot("EXTRA_SFX_CINE_RAMP", 2, "setpiece", "SFX/SetPieces", name="SFX_CINE_RAMP",
                     max_len=2.0, min_len=0.4, hp=25, max_gr=3, fade_ms=300, focus=dict(gap_s=0.25, on_db=-30, tail_db=-50),
                     notes=("EXTRA (CINEMATIC_DIRECTION §8.2a I-8 / speed ramps): low body thump + short air whoosh for a "
                            "speed-ramp hero moment, 2D, at most once per sequence.")),
    )


UI_IDS = ["SFX_UI_TYPE_KEY", "SFX_UI_TELETYPE_LINE", "SFX_UI_OBJECTIVE", "SFX_UI_INTEL_CARD", "SFX_UI_MENU_MOVE",
          "SFX_UI_MENU_SELECT", "SFX_UI_HITMARKER", "SFX_UI_HEADSHOT", "SFX_UI_KILL", "SFX_UI_LOW_AMMO", "SFX_UI_PICKUP"]
UI_NOTE = {
    "SFX_UI_TYPE_KEY": "Typewriter key strike for teletype-style objective text reveal (one per character group).",
    "SFX_UI_TELETYPE_LINE": "Teletype line burst when an objective line prints.",
    "SFX_UI_OBJECTIVE": "New objective blip.",
    "SFX_UI_INTEL_CARD": "Intel polaroid sliding in.",
    "SFX_UI_MENU_MOVE": "Menu focus move tick.",
    "SFX_UI_MENU_SELECT": "Menu confirm.",
    "SFX_UI_HITMARKER": "Hit marker tick (0.12 s marker).",
    "SFX_UI_HEADSHOT": "Headshot click layered on the hit marker.",
    "SFX_UI_KILL": "Kill confirm thud (marker turns red).",
    "SFX_UI_LOW_AMMO": "Low-ammo rattle (magazine < 25 %).",
    "SFX_UI_PICKUP": "Ammo pickup (pouch snap).",
}


def recipe_ui():
    rep = {}
    # hit marker: procedural ticks (the EL takes were a double burst and a 0.5 s noise; a marker tick must be < 70 ms)
    fs = sorted(PROC.glob("SFX_UI_HITMARKER_proc_*.wav"))[:2]
    for k, f in enumerate(fs, 1):
        y = loud(A.load(f, mono=True)[0], "ui", max_gr=2)
        emit("SFX_UI_HITMARKER", "SFX/UI", f"SFX_UI_HITMARKER_{k:02d}", y, level=LEVELS["ui"], kind="synth", sources=[f],
             notes=f"{UI_NOTE['SFX_UI_HITMARKER']} Procedural damped metallic tick (proc_sfx.py), 70 ms. Variant {k}/2. 2D UI sound. Not looping.")
    rep["SFX_UI_HITMARKER"] = [f.name for f in fs]
    for mid in UI_IDS:
        if mid == "SFX_UI_HITMARKER":
            continue
        rep[mid] = oneshot(mid, 2, "ui", "SFX/UI", max_len=0.5, min_len=0.02, hp=120 if mid != "SFX_UI_KILL" else 40,
                           max_gr=3, on_db=-30, floor_db=-60, fade_ms=40, notes=UI_NOTE[mid] + " 2D UI sound.")
    return rep


SYNTH = RAWM / "procedural"


def recipe_synth():
    rep = {}
    pairs = [("SFX_RADIO_CLICKS", [f"SFX_RADIO_CLICKS_{n}_{v:02d}" for n in (1, 2, 3) for v in (1, 2)],
              "Greycoat squelch code: {n} PTT click(s) + the alien two-tone tail (1.4 then 2.1 kHz, 60 ms each), radio band."),
             ("SFX_RADIO_KEYUP", [f"SFX_RADIO_KEYUP_{v:02d}" for v in (1, 2, 3)] + [f"SFX_RADIO_RELEASE_{v:02d}" for v in (1, 2, 3)],
              "Radio key-up (PTT + squelch break) / release burst; VO RADIO lines already carry their own, these are for the PRC-77 handset foley."),
             ("SFX_TINNITUS", ["SFX_TINNITUS_01", "SFX_TINNITUS_02"], "Tinnitus whine (4.3 / 5.6 kHz) after near blasts and at the climax; quiet by design (sits over the muffled mix)."),
             ("SFX_LEADER_BEEP", ["SFX_LEADER_BEEP_01", "SFX_LEADER_BEEP_02"], "Film-leader countdown beep (1 kHz, one frame at 24 fps), MID-B reel.")]
    for mid, names, note in pairs:
        rep[mid] = []
        for nm in names:
            f = SYNTH / f"{nm}.wav"
            if not f.exists():
                continue
            x, _ = A.load(f, mono=True)
            x = A.finalize(x, -1.0)
            nn = note.format(n=nm.split("_")[3] if mid == "SFX_RADIO_CLICKS" else "")
            emit(mid, "SFX/Synth", nm, x, kind="synth", sources=[f], notes=nn + " Procedural (Tools/audio/sfx/motif_synth.py). Not looping.")
            rep[mid].append(nm)
    for mid, nm, note in (("SFX_CRT_WHINE", "SFX_CRT_WHINE_body", "CRT 12 kHz whine + 50 Hz mains harmonics (15.7 kHz is inaudible on many speakers), per CRT set, 3D."),
                          ("SFX_CLOCK_TICK_STUCK", "SFX_CLOCK_TICK_STUCK_body", "Stuck clock: tick, the escapement tries to advance and falls back, twice per 2 s (booth wall clock), 3D; synth click + our Mirelo tick layer.")):
        f = SYNTH / f"{nm}.wav"
        x, _ = A.load(f, mono=True)
        if mid == "SFX_CRT_WHINE":
            x = A.loudness_to(x, -30, tp=-1.0)
        pk, lead, nb = A.package_loop(x)
        emit(mid, "SFX/Synth", mid, pk, kind="synth_loop", sources=[f], loop=dict(lead=lead, body=nb, tail=A.LEAD),
             notes=note + " Procedural (motif_synth.py).")
        rep[mid] = [mid]
    return rep


def recipe_ambience():
    rep = {}
    for mid, (lufs, note) in AMB.items():
        LEVELS[f"amb_{mid}"] = lufs
        rep[mid] = el_loop(mid, 2, f"amb_{mid}", "Ambience", stereo=True, hp=35, guard=True,
                           notes=note + " Stereo 2D bed (AmbienceDirector crossfades beds by LightStage and zone).")
    return rep


# ----------------------------------------------------------------------------- procedural beds from OUR material only
VORAW = ROOT / "RawAssets/audio/vo/raw"


def vo_take(line_id):
    """The WP06_VO-selected take of a line (our own TTS, invented language for barks/PA), as mono 48 kHz."""
    sel = json.loads((ROOT / "RawAssets/audio/vo/selection.json").read_text())
    t = sel.get(line_id, {}).get("take", 1)
    d = VORAW / line_id / f"take_{t}"
    fs = sorted(list(d.glob("*.pcm")) + list(d.glob("*.wav")))
    if not fs:
        return None, None
    x, _ = A.load(fs[0], mono=True)
    x = A.trim(A.remove_dc(x, 60), on_db=-35, floor_db=-55, pre_ms=5, fade_ms=40)
    return x, fs[0]


def tiny_speaker(x, seed=3):
    """1960s cassette player / transistor radio speaker: 350 Hz - 4.5 kHz, cone resonance, a little drive, wow, hiss."""
    y = A.hp(x, 350, order=2)
    y = A.lp(y, 4500, order=4)
    y = A.biquad(y, "peak", 1200, 5, 1.2)
    y = A.biquad(y, "peak", 2600, 3, 2.0)
    y = A.soft_clip(y / (np.abs(y).max() + 1e-9), 1.8)
    n = len(y)
    t = np.arange(n) / SR
    rng = np.random.default_rng(seed)
    wow = 1 + 0.004 * np.sin(2 * np.pi * 0.55 * t + rng.uniform(0, 6)) + 0.0015 * np.sin(2 * np.pi * 6.5 * t)
    pos = np.clip(np.cumsum(wow) - wow[0], 0, n - 1)
    y = np.interp(pos, np.arange(n), y[:, 0])[:, None]
    hiss = A.bp(rng.standard_normal((n, 1)), 2000, 9000, order=2)
    y = y + hiss / (np.abs(hiss).max() + 1e-9) * A.undb(-36)
    return y


def through_wall(x, lp_hz=700, seed=5):
    """Behind a plaster wall / shutter: steep low-pass, a boxy room resonance and a short dull room tail."""
    y = A.lp(x, lp_hz, order=4)
    y = A.hp(y, 90, order=2)
    y = A.biquad(y, "peak", 220, 4, 1.5)
    ir = A.synth_ir(rt60=0.35, predelay=0.004, er=[(0.006, -6, 900), (0.013, -9, 700)], lp_hz=900, seed=seed)
    return A.mix((y, 0, 0), (A.convolve(y, ir)[: len(y) + int(0.4 * SR)], -6, 0))


def recipe_beds():
    rep = {}
    sel = json.loads((RAWM / "selection.json").read_text())
    # --- AMB_CASSETTE_TUNE (EL music take, surf-guitar style) -> tiny speaker, bar-cut loop, mono 3D
    import music_loop as ML
    f = ROOT / sel["AMB_CASSETTE_TUNE"]["file"]
    x, _ = A.load(f, mono=True)
    g = ML.bar_grid(x, 130.0, region=(2, 26.5))
    best, _top = ML.choose_loop(x, g, 8, (2, 26.5), xfade_s=0.15)
    _out, _lead, _nb, body = ML.render(x, best["start"], best["end"], xfade_s=0.15)
    sp = tiny_speaker(body)
    LEVELS["loop_cassette"] = -24.0
    sp = A.loudness_to(sp, -24.0)
    pk, lead, nb = A.package_loop(sp)
    emit("AMB_CASSETTE_TUNE", "SFX/SetPieces", "AMB_CASSETTE_TUNE", pk, level=-24.0, kind="loop", sources=[f],
         loop=dict(lead=lead, body=nb, tail=A.LEAD, music=dict(bpm_grid=round(g["bpm"], 2), loop_bars=8)),
         notes=("Diegetic cassette radio in the guard hut (E2-03): an instrumental early-1960s surf-style guitar tune (EL "
                "Music v2.5, no vocals, not regional, not religious) through a tiny-speaker chain, 8-bar loop. Mono 3D, "
                "small max distance (~15 m)."))
    rep["AMB_CASSETTE_TUNE"] = dict(take=sel["AMB_CASSETTE_TUNE"]["take"], loop=best)
    # --- SFX_RADIO_TURNED_DOWN: the same radio behind a shutter, knob turned down over 2 s
    n = int(5.0 * SR)
    src = np.vstack([body, body])[:n]
    y = through_wall(tiny_speaker(src, seed=9), lp_hz=1400)[:n]
    knob = A.lp(A.hp(np.random.default_rng(4).standard_normal((int(0.03 * SR), 1)), 1500, order=2), 6000, order=2)
    knob = A.fade(knob / np.abs(knob).max(), 0.0005, 0.02) * A.undb(-16)
    y = y * A.gain_env(n, [(0, 0), (2.4, 0), (4.4, -32), (5.0, -60)])
    y = A.mix((y, 0, 0), (knob, 0, 2.38))[:n]
    y = A.fade(y, 0.01, 0.3)
    y = loud(y, "setpiece", max_gr=2)
    emit("SFX_RADIO_TURNED_DOWN", "SFX/SetPieces", "SFX_RADIO_TURNED_DOWN", y, level=LEVELS["setpiece"], kind="proc_bed",
         sources=[f], notes=("G5 civilian radio behind a shutter turned down when shooting starts within 40 m: built only "
                             "from our AMB_CASSETTE_TUNE (tiny speaker + through-the-wall chain), knob click at 2.4 s, then "
                             "a 2 s fade. 3D at the shutter. Not looping."))
    # --- SFX_TV_MUFFLED: our invented-language PA/bark takes as a TV programme behind a wall (unintelligible)
    parts, srcs = [], []
    for lid in ("D012", "BK_LEG_14_A", "BK_LEG_15_B", "BK_LEG_14_B", "BK_LEG_15_A"):
        v, fsrc = vo_take(lid)
        if v is not None:
            parts.append(v)
            srcs.append(fsrc)
    L = int(6.0 * SR)
    tv = np.zeros((L, 1))
    t = 0.15
    rng = np.random.default_rng(11)
    k = 0
    while t < 6.0 and parts:
        v = parts[k % len(parts)] * A.undb(rng.uniform(-3, 0))
        i = int(t * SR)
        seg = v[: L - i]
        tv[i:i + len(seg)] += seg
        t += len(v) / SR + rng.uniform(0.15, 0.45)
        k += 1
    # wrap the overhang so the bed loops
    tv_bed = A.bp(np.vstack([body, body, body])[:L], 200, 5000, order=2) * A.undb(-20)   # faint programme music bed
    tv = tv / (np.abs(tv).max() + 1e-9) + tv_bed / (np.abs(tv_bed).max() + 1e-9) * A.undb(-18)
    tv = A.bp(tv, 180, 5500, order=2)                        # TV speaker
    tv = A.pad_to(through_wall(tv, lp_hz=650), L + int(0.4 * SR))
    tv[:int(0.4 * SR)] += tv[L:L + int(0.4 * SR)]            # fold the room tail into the head (seamless)
    tv = tv[:L]
    body_tv, sm, fixed = loop_body_fix(tv, 0.3)
    body_tv = A.loudness_to(body_tv, -28.0)
    pk, lead, nb = A.package_loop(body_tv)
    emit("SFX_TV_MUFFLED", "SFX/SetPieces", "SFX_TV_MUFFLED", pk, level=-28.0, kind="loop", sources=srcs + [f],
         loop=dict(lead=lead, body=nb, tail=A.LEAD),
         notes=("G5 muted TV behind a wall, built only from our own invented-language takes (Harrou D012 PA + Legion "
                "BK_LEG_14/15) and our cassette tune, low-passed at 650 Hz so no word is intelligible. 6 s loop, 3D at the "
                "wall; CivilianEmitter turns it down when shooting starts."))
    rep["SFX_TV_MUFFLED"] = [str(s_) for s_ in srcs]
    # --- AMB_DISTANT_SHOUTING: Legion barks through a distance chain, circular 20 s timeline (seamless), stereo
    sys.path.insert(0, str(ROOT / "Tools/audio/vo"))
    import vo_chains
    barks = []
    for i in range(1, 13):
        for v_ in ("A", "B"):
            v, fsrc = vo_take(f"BK_LEG_{i:02d}_{v_}")
            if v is not None:
                barks.append((v, fsrc))
    rep["AMB_DISTANT_SHOUTING"] = []
    for var in (1, 2):
        rng = np.random.default_rng(100 + var)
        L = int(20.0 * SR)
        bed = np.zeros((L, 2))
        used = []
        t = rng.uniform(0.3, 1.2)
        while t < 19.5:
            v, fsrc = barks[int(rng.integers(len(barks)))]
            dist = rng.uniform(0.0, 1.0)                      # 0 = ~60 m, 1 = ~180 m
            y, _ = vo_chains.chain_far(v[:, 0], sr=SR, seed=int(rng.integers(1e6)))
            y = np.asarray(y)[:, None]
            y = A.lp(y, 2200 - 1200 * dist, order=2)
            y = y / (np.abs(y).max() + 1e-9) * A.undb(-6 - 14 * dist + rng.uniform(-2, 2))
            pan = rng.uniform(-0.8, 0.8)
            st = np.hstack([y * np.sqrt(0.5 * (1 - pan)), y * np.sqrt(0.5 * (1 + pan))])
            i = int(t * SR)
            idx = (np.arange(len(st)) + i) % L
            np.add.at(bed, idx, st)
            used.append(str(fsrc))
            # occasional call-and-answer: a second bark shortly after, from elsewhere
            t += rng.uniform(0.8, 2.6) if rng.random() < 0.35 else rng.uniform(1.6, 3.8)
        bed = A.hp(bed, 120, order=2)
        bed, sm, fixed = loop_body_fix(bed, 0.4)
        bed = A.loudness_to(bed, -24.0)
        pk, lead, nb = A.package_loop(bed)
        LEVELS["amb_shouting"] = -24.0
        emit("AMB_DISTANT_SHOUTING", "Ambience", f"AMB_DISTANT_SHOUTING_{var:02d}", pk, level=-24.0, kind="loop",
             stereo=True, sources=sorted(set(used)), loop=dict(lead=lead, body=nb, tail=A.LEAD),
             notes=("Blue-hour bed: distant Legion shouting across the yard, layered only from our own invented-language "
                    "bark takes (BK_LEG_01-12, voices A/B) through a 60-180 m distance chain (vo_chains FAR + extra air "
                    "absorption), random pan, placed on a circular 20 s timeline so the loop is seamless. Stereo 2D bed. "
                    f"Variant {var}/2."))
        rep["AMB_DISTANT_SHOUTING"].append(len(used))
    return rep


GROUPS = {
    "weapons": lambda: dict(shots=recipe_weapon_shots(), bodies=recipe_bodies(), mechs=recipe_mechs(), tails=recipe_tails(),
                            balance=balance_layers()),
    "weapon_foley": lambda: dict(reloads=recipe_reloads(), handling=recipe_handling(), shells=recipe_shells(),
                                 gren=recipe_grenade_rpg_knife()),
    "whizz": recipe_whizz,
    "impacts": recipe_impacts,
    "explosions": recipe_explosions,
    "footsteps": recipe_footsteps,
    "foley": recipe_foley,
    "setpieces": recipe_setpieces,
    "extras": recipe_extras,
    "ui": recipe_ui,
    "synth": recipe_synth,
    "ambience": recipe_ambience,
    "beds": recipe_beds,
}


def write_index():
    old = json.loads(INDEX.read_text()) if INDEX.exists() else []
    touched = {r["manifest_id"] for r in ROWS.values()}
    keep = {}
    for r in old:   # a manifest id rebuilt this run drops its stale files (e.g. a set that shrank from 12 to 10)
        if r["manifest_id"] in touched and r["target_path"] not in ROWS:
            f = READY / r["file"]
            if f.exists():
                f.unlink()
            continue
        keep[r["target_path"]] = r
    keep.update(ROWS)
    lp_t = "Assets/_Game/Audio/Music/LOOP_POINTS.json"
    keep[lp_t] = dict(manifest_id="LOOP_POINTS", file=lp_t, target_path=lp_t, type="data",
                      import_notes=("TextAsset (no import settings). Loop regions of every looping clip under Audio/ "
                                    "(keyed by the clip's Assets path): loop_start_sample / loop_end_sample at 48 kHz "
                                    "(scale by clip.frequency / 48000 after the Web AAC re-encode at 44.1 kHz), loop_seconds, "
                                    "and for music bpm_grid, bar_seconds, loop_bars, has_intro. AudioLibraryBuilder reads it "
                                    "(AudioCue loop regions, MusicClock bar grid). The same region is in each WAV's smpl chunk."))
    rows = sorted(keep.values(), key=lambda r: r["target_path"])
    A.write_json(INDEX, rows)
    rep = json.loads(REPORT.read_text()) if REPORT.exists() else {}
    rep["files"] = {k: v for k, v in rep.get("files", {}).items() if k in keep}
    rep["files"].update(REVIEW)
    A.write_json(REPORT, rep)
    lp = OUTROOT / "Music/LOOP_POINTS.json"
    old_l = json.loads(lp.read_text()) if lp.exists() else {}
    old_l.update({k: v for k, v in LOOPS.items()})
    A.write_json(lp, dict(sorted(old_l.items())))
    return len(rows)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", help="comma list of groups")
    ap.add_argument("--preview", action="store_true")
    a = ap.parse_args()
    load_scan()
    groups = a.only.split(",") if a.only else list(GROUPS)
    sel = {}
    for g in groups:
        sel[g] = GROUPS[g]()
    if a.preview:
        composite_previews()
    n = write_index()
    rep = json.loads(REPORT.read_text())
    rep.setdefault("selection", {}).update(sel)
    A.write_json(REPORT, rep)
    print(f"{len(ROWS)} files written this run; index has {n} rows")


if __name__ == "__main__":
    main()
