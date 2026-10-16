#!/usr/bin/env python3
"""Splice the Clockmaker lines from single-token takes (VO_CLOCKMAKER_TOKENS) on a 0.9 s grid (GDD 10.1).

  python clockmaker_splice.py pick            # choose one take per token -> RawAssets/audio/vo/tokens/token_choice.json
  python clockmaker_splice.py lines           # D039 (HF_AM), D066 (ROOM_SPEAKER), D080 (CLEAN), D092 (HF_AM) -> ready/
  python clockmaker_splice.py stt             # transcribe the dry splices (Scribe v2 via falcall) as a check
  python clockmaker_splice.py basement --interval <INTERVAL_SIGNAL.wav> --out <NUM_BASEMENT_LOOP.wav> [--seconds 60]
        (NUM_BASEMENT_LOOP is a music-category asset owned by WP06_SFX/music; this builds a seamless loop from our tokens:
         groups 74410 / 06400 / 28904 / 06400, the D039 group and the 6:40 motif, read twice after "Attention")

Tokens are prepared by `vo_verify.py tokens` (decode, trim, STT). Each token is cut tight (5 ms before onset),
loudness-matched to the others, time-compressed if longer than 0.84 s, and placed at 0.03 s + k * 0.9 s.
"""
import argparse, json, math, subprocess, sys
import numpy as np
from common import (ROOT, TOKENS, RAW, VO_OUT, SR, FAL_PY, FALCALL, WP_TAG, TOOLS, read_audio, write_wav16,
                    frame_rms_db, norm_words, stable_seed)

GRID = 0.9
LEAD = 0.03
LINES = {
    "D039": (["attention", "attention", "seven", "four", "four", "one", "zero", "seven", "four", "four", "one", "zero",
              "zero", "six", "four"], "HF_AM"),
    "D066": (["zero", "six", "four"], "ROOM_SPEAKER"),
    "D080": (["attention", "sundial", "three_name", "zero", "six", "four", "zero"], "CLEAN"),
    "D092": (["attention", "sundial", "two_name"], "HF_AM"),
}
EXPECT = {"two_name": "two", "three_name": "three"}
SPLICE_DIR = RAW / "clockmaker"


def tight(x, sr=SR):
    db = frame_rms_db(x, sr, 5)
    pk = db.max()
    act = np.where(db > pk - 38)[0]
    s = max(0, act[0] * int(sr * 0.005) - int(sr * 0.005))
    alive = np.where(db > pk - 45)[0]
    e = min(len(x), (alive[-1] + 1) * int(sr * 0.005) + int(sr * 0.03))
    y = x[s:e].copy()
    y[: int(sr * 0.002)] *= np.linspace(0, 1, int(sr * 0.002))
    f = min(len(y), int(sr * 0.025))
    y[-f:] *= np.linspace(1, 0, f) ** 2
    return y


MAX_TOKEN = 0.86  # a token may fill its 0.9 s slot up to 40 ms before the next onset


def wsola(x, rate, sr=SR, frame_ms=30, search_ms=8):
    """Waveform-similarity overlap-add time-scale change (rate > 1 = shorter), pitch unchanged. Cleaner on speech than a
    phase vocoder (no phasiness): each output frame is the input frame near the nominal position whose start best
    continues the previous output frame (max normalised cross-correlation within +-search_ms)."""
    N = int(sr * frame_ms / 1000) // 2 * 2
    H = N // 2
    S = int(sr * search_ms / 1000)
    win = np.hanning(N)
    n_out = int(len(x) / rate)
    xp = np.concatenate([np.zeros(S), x, np.zeros(N + S + H)])
    y = np.zeros(n_out + N)
    wsum = np.zeros(n_out + N)
    prev_tail = None
    for k in range(0, n_out, H):
        nom = int(k * rate) + S
        if prev_tail is None:
            best = nom
        else:
            seg = xp[nom - S: nom + S + H]
            cc = np.correlate(seg, prev_tail, mode="valid")
            en = np.sqrt(np.convolve(seg ** 2, np.ones(H), mode="valid")) + 1e-9
            best = nom - S + int(np.argmax(cc / en))
        fr = xp[best: best + N]
        if len(fr) < N:
            break
        y[k: k + N] += fr * win
        wsum[k: k + N] += win
        prev_tail = xp[best + H: best + N]
    return (y / np.maximum(wsum, 1e-3))[:n_out]


def token_takes():
    out = {}
    for td in sorted(TOKENS.iterdir()):
        if not td.is_dir():
            continue
        for tk in sorted(td.glob("take_*")):
            w, m, st = tk / "trim.wav", tk / "trim.json", tk / "stt" / "result.json"
            if w.exists() and m.exists():
                out.setdefault(td.name, []).append({"take": int(tk.name.split("_")[1]), "wav": w, "meta": json.loads(m.read_text()),
                                                    "stt": json.loads(st.read_text()) if st.exists() else None})
    return out


def pick():
    tt = token_takes()
    f0s = [t["meta"]["f0_med"] for v in tt.values() for t in v if t["meta"].get("f0_med")]
    f0_all = float(np.median(f0s)) if f0s else None
    choice, report = {}, {}
    for tok, takes in tt.items():
        want = EXPECT.get(tok, tok)
        rows = []
        for t in takes:
            heard = " ".join(norm_words(t["stt"]["text"])) if t["stt"] else ""
            ok = heard.replace(" ", "") == want.replace(" ", "") or (want == "sundial" and heard in ("sun dial", "sundial"))
            x, sr = read_audio(t["wav"])
            d = len(tight(x, sr)) / sr
            f0 = t["meta"].get("f0_med")
            dev = abs(12 * math.log2(f0 / f0_all)) if (f0 and f0_all) else 3.0
            clip = t["meta"]["raw_clipped"] > 3
            stretch = max(0.0, d / MAX_TOKEN - 1)  # time compression needed to fit the slot
            cost = (0 if ok else 10) + (5 if clip else 0) + dev * 0.3 + 6 * stretch
            rows.append({"take": t["take"], "heard": heard, "ok": ok, "dur": round(d, 3), "f0": f0, "f0_dev_st": round(dev, 2), "cost": round(cost, 2)})
        best = min(rows, key=lambda r: r["cost"])
        choice[tok] = best["take"]
        report[tok] = {"chosen": best["take"], "takes": rows}
        print(f"{tok:11s} take {best['take']}  heard={best['heard']!r:12s} ok={best['ok']} dur={best['dur']}s f0dev={best['f0_dev_st']}st")
    (TOKENS / "token_choice.json").write_text(json.dumps({"choice": choice, "report": report, "f0_all": f0_all}, indent=1))


def load_tokens():
    ch = json.loads((TOKENS / "token_choice.json").read_text())["choice"]
    toks = {}
    for tok, take in ch.items():
        x, sr = read_audio(TOKENS / tok / f"take_{take}" / "trim.wav")
        y = tight(x, sr)
        if len(y) / sr > MAX_TOKEN:
            y = wsola(y, (len(y) / sr) / MAX_TOKEN, sr)
            f = int(sr * 0.02)
            y[-f:] *= np.linspace(1, 0, f) ** 2
        toks[tok] = y
    # loudness match: same active-speech RMS for every token (a machine reads every digit at the same level)
    from vo_chains import speech_rms
    ref = np.median([speech_rms(v) for v in toks.values()])
    return {k: v * ref / speech_rms(v) for k, v in toks.items()}


def static_burst(n, seed):
    from vo_chains import bp, noise
    rng = np.random.default_rng(seed)
    s = bp(noise(n, seed, "white"), 300, 4500, SR, 2)
    e = np.ones(n)
    a = int(SR * 0.004)
    e[:a] = np.linspace(0, 1, a)
    e *= 0.65 + 0.35 * np.abs(np.sin(np.cumsum(rng.uniform(5, 40, n)) / SR * 2 * np.pi))
    crk = np.zeros(n)
    for _ in range(int(n / SR * 12)):
        i = rng.integers(0, n - 150)
        crk[i:i + 150] += rng.standard_normal(150) * np.exp(-np.arange(150) / 30) * rng.uniform(1, 3)
    return (s * e + bp(crk, 300, 4500, SR, 2))


def splice(seq, line_id, toks):
    n_slots = len(seq)
    total = LEAD + n_slots * GRID
    y = np.zeros(int(total * SR))
    for k, tok in enumerate(seq):
        v = toks[tok].copy()
        if line_id == "D066" and k == len(seq) - 1:
            cut = int(len(v) * 0.42)  # "Fo..." cut mid-word
            v = v[:cut]
            i = int((LEAD + k * GRID) * SR)
            y[i:i + len(v)] += v
            st = static_burst(len(y) - (i + len(v)), stable_seed("D066", "static"))
            from vo_chains import speech_rms, rms
            st *= speech_rms(toks["zero"]) * 1.1 / (rms(st) + 1e-12)
            y[i + len(v):] += st
            continue
        i = int((LEAD + k * GRID) * SR)
        y[i:i + len(v)] += v[: len(y) - i]
    if line_id == "D066":
        y = y[: int(2.2 * SR)]
    return y


def lines():
    from vo_chains import CHAINS, finalize
    toks = load_tokens()
    SPLICE_DIR.mkdir(parents=True, exist_ok=True)
    rep = {}
    for lid, (seq, chain) in LINES.items():
        dry = splice(seq, lid, toks)
        write_wav16(SPLICE_DIR / f"{lid}_splice_dry.wav", dry / (np.abs(dry).max() + 1e-9) * 0.7, SR)
        write_wav16(SPLICE_DIR / f"{lid}_splice_dry_16k.wav", __import__("common").to_sr(dry / (np.abs(dry).max() + 1e-9) * 0.7, SR, 16000), 16000)
        y, info = CHAINS[chain](dry, SR, seed=stable_seed(lid, chain))
        if lid == "D066":  # keep the specified 2.2 s: the dead transmitter's static is cut by the game (D066 trigger)
            y = y[: int(2.2 * SR)]
            f = int(SR * 0.05); y[-f:] *= np.linspace(1, 0, f)
        y, m = finalize(y)
        out = VO_OUT / chain / f"VO_{lid}_{chain}.wav"
        write_wav16(out, y, SR, seed=stable_seed(lid))
        rep[lid] = {"chain": chain, "file": str(out.relative_to(ROOT)), "dur": round(len(y) / SR, 3), "slots": len(seq),
                    "grid_s": GRID, "tokens": seq, **m}
        print(lid, chain, rep[lid]["dur"], m)
    (SPLICE_DIR / "splice_report.json").write_text(json.dumps(rep, indent=1))


def stt():
    files = [str(SPLICE_DIR / f"{lid}_splice_dry_16k.wav") for lid in LINES]
    p = subprocess.run([FAL_PY, str(TOOLS / "fal_upload.py"), *files], capture_output=True, text=True, cwd=str(ROOT))
    urls = json.loads(p.stdout)
    for lid in LINES:
        out = SPLICE_DIR / f"stt_{lid}"
        src = SPLICE_DIR / f"{lid}_splice_dry_16k.wav"
        if (out / "result.json").exists() and (out / "result.json").stat().st_mtime >= src.stat().st_mtime:
            continue
        args = {"audio_url": urls[str(SPLICE_DIR / f"{lid}_splice_dry_16k.wav")], "language_code": "eng", "tag_audio_events": True,
                "diarize": False, "keyterms": ["Sundial", "Attention"]}
        out.mkdir(parents=True, exist_ok=True)
        (out / "args.json").write_text(json.dumps(args))
        r = subprocess.run([FAL_PY, str(FALCALL), "--wp", WP_TAG, "--id", f"STT_{lid}_splice", "--endpoint",
                            "fal-ai/elevenlabs/speech-to-text/scribe-v2", "--args", str(out / "args.json"), "--est-usd", "0.0104",
                            "--out", str(out)], capture_output=True, text=True, cwd=str(ROOT))
        if r.returncode == 2:
            sys.exit(2)
    for lid in LINES:
        f = SPLICE_DIR / f"stt_{lid}" / "result.json"
        if f.exists():
            print(lid, json.loads(f.read_text()).get("text"))


def basement(interval, out, seconds=60.0, seed=7, fixed_groups=("74410", "06400", "28904", "06400")):
    """Live basement broadcast, seamless loop: [interval signal, "Attention", a 5-digit group read twice, 0.6 s gap]
    repeated with different groups until >= `seconds` (ends on a segment boundary), then the ROOM_SPEAKER chain with
    its reverb tail wrapped around to the start so the loop point is seamless. Gain-only normalisation (no fades)."""
    from vo_chains import CHAINS, TARGET_LUFS, TP_CEIL, tp_limit
    from common import to_sr, lufs, true_peak_db
    toks = load_tokens()
    iv, sr = read_audio(interval)
    iv = to_sr(iv, sr, SR)
    rng = np.random.default_rng(seed)
    digits = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"]
    parts, groups = [], []
    while sum(len(p) for p in parts) < seconds * SR:
        parts.append(iv / (np.abs(iv).max() + 1e-9) * 0.5)
        k = len(groups)
        grp = [digits[int(c)] for c in fixed_groups[k]] if k < len(fixed_groups) else [digits[i] for i in rng.integers(0, 10, 5)]
        groups.append("".join(str(digits.index(g)) for g in grp))
        parts.append(splice(["attention"] + grp + grp, "BASEMENT", toks))
        parts.append(np.zeros(int(SR * 0.6)))
    dry = np.concatenate(parts)
    n = len(dry)
    wet, _ = CHAINS["ROOM_SPEAKER"](np.concatenate([dry, dry]), SR, seed=seed)  # two passes: steady-state reverb
    y = wet[n: 2 * n].copy()                                                       # second pass = seamless period
    y *= 10 ** ((TARGET_LUFS - lufs(y)) / 20)
    if true_peak_db(y) > TP_CEIL:
        y = tp_limit(y, SR, ceil_db=TP_CEIL - 0.3)
    write_wav16(out, y, SR)
    print("basement loop", out, round(n / SR, 2), "s", {"lufs": round(lufs(y), 2), "tp": round(true_peak_db(y), 2)}, "groups", groups)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["pick", "lines", "stt", "basement"])
    ap.add_argument("--interval"); ap.add_argument("--out"); ap.add_argument("--seconds", type=float, default=60)
    a = ap.parse_args()
    if a.mode == "pick":
        pick()
    elif a.mode == "lines":
        lines()
    elif a.mode == "stt":
        stt()
    else:
        basement(a.interval, a.out, a.seconds)


if __name__ == "__main__":
    main()
