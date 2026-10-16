#!/usr/bin/env python3
"""Decode, trim, measure, transcribe and score every TTS take; pick one take per line.

  python vo_verify.py prep   [--ids ...]     # raw -> work/<key>/take_<n>.wav (48 kHz, trimmed) + metrics json
  python vo_verify.py stt    [--ids ...]     # fal speech-to-text (ElevenLabs Scribe v2 via falcall.py) per take
  python vo_verify.py score  [--ids ...]     # WER + checks + choice -> RawAssets/audio/vo/selection.json
  python vo_verify.py all    [--ids ...]

Checks per take (English): normalised WER vs the DIALOGUE.json text (fuzzy word match for proper nouns, numbers
expanded), delivery-tag words spoken aloud, cut-off (last word missing or audio still loud at the end of the raw
file), clipping in the raw file, implausible speech rate, long internal gaps, pitch outlier vs the voice median.
Invented-language barks: duration / syllable plausibility, a sound-alike guardrail (every transcript word looked up
with wordfreq in 27 top-20/priority languages; zipf >= 3.2 rejects the take unless listed in ACCEPTED_SOUNDALIKES),
and letter similarity transcript <-> spelling in the take cost (prefer the take that says what we wrote).
Edited takes (take_101.. from vo_edit.py) are ordinary takes here. `score --ids` updates the reports in place.
Needs Tools/.venv with numpy scipy soundfile pyloudnorm librosa wordfreq unidecode.
"""
import argparse, concurrent.futures as cf, difflib, json, math, re, subprocess, sys
import numpy as np
from common import (ROOT, RAWDIR, WORK, TOKENS, RAW, FAL_PY, FALCALL, WP_TAG, SR, TOOLS, load_dialogue, load_casting,
                    read_audio, to_sr, write_wav16, frame_rms_db, clipped_samples, wer, allowed_errors, norm_words)

INVENTED = {"D012"} | {f"BK_LEG_{i:02d}" for i in range(1, 16)} | {f"BK_GRY_{i:02d}" for i in range(1, 10)}
KEYTERMS = ["Zaroud", "Calder", "Sundial", "Tallow", "Hollis", "Tinker", "Sparrow", "Magpie", "Harrou", "Clockmaker",
            "Castellanos", "Hollister", "Greycoats", "Tinsmiths", "cher", "cordite", "postern", "Weighbridge", "Nathan",
            "marquee", "codebook", "awnings", "Leapfrog", "Legion", "insignia", "S-3"]
TAG_WORDS = {"shouting", "shouted", "whispering", "whispered", "calm", "soft", "unhurried", "briefing", "metronomic",
             "countdown", "strained", "urgent", "handset", "intercom", "radio", "delivery", "tenor", "drawl", "breathing",
             "matter", "fact", "bored", "airline", "captain", "insistent", "clinical", "patient", "gentle", "warm",
             "pleased", "amused", "trailing", "afraid", "confused", "decisive", "firm", "clipped", "relieved", "sharp"}


# Sound-alike guardrail for invented-language takes (GDD 2.3-5, O3): a transcript word that is a common word
# (wordfreq zipf >= REALWORD_ZIPF, i.e. more than ~1.6 per million) in a top-20 world language or a priority language
# means the take *sounds like* real speech. Japanese/Chinese/Korean are skipped (wordfreq needs extra tokenizers).
REALWORD_ZIPF = 3.2
REALWORD_LANGS = ["en", "es", "fr", "de", "it", "pt", "ru", "uk", "pl", "cs", "sk", "bg", "mk", "sh", "sl", "ar", "fa",
                  "he", "tr", "hi", "ur", "bn", "id", "vi", "ta"]


# Reviewed exceptions (logged in Docs/DECISIONS.md): a sound-alike that is uncommon, harmless and unavoidable for the
# lexicon word. Every other hit rejects the take.
ACCEPTED_SOUNDALIKES = {
    "pira": "BK_LEG_04 'Pirrha' is always heard as 'pira' (pt/it/es 'pyre', pt slang 'freak out'; zipf <= 3.3, "
            "harmless); takes heard as 'perra' (es insult, zipf 4.2) stay rejected",
}


def real_words(transcript, line_text):
    from wordfreq import zipf_frequency, tokenize
    own = set(norm_words(line_text))
    hits = []
    for w in set(tokenize(transcript or "", "xx")):
        w = w.strip("'").lower()
        if not w or w in own or any(len(w) >= 3 and difflib.SequenceMatcher(None, w, o).ratio() >= 0.85 for o in own):
            continue
        best = max(((round(zipf_frequency(w, l), 2), l) for l in REALWORD_LANGS), default=(0, ""))
        if best[0] >= REALWORD_ZIPF and w not in ACCEPTED_SOUNDALIKES:
            hits.append(f"{w}({best[1]} {best[0]})")
    return sorted(hits)


def phon_sim(transcript, line_text):
    """Letter similarity of the (transliterated) transcript to the invented spelling: did the TTS say what we wrote?"""
    from unidecode import unidecode
    a = re.sub(r"[^a-z]", "", unidecode(transcript or "").lower())
    b = re.sub(r"[^a-z]", "", line_text.lower())
    return round(difflib.SequenceMatcher(None, a, b).ratio(), 3) if a and b else 0.0


def key_line(key):
    return re.sub(r"_[AB]$", "", key)


def take_audio(take_dir):
    for f in sorted(take_dir.iterdir()):
        if f.suffix in (".pcm", ".wav", ".mp3") and not f.name.startswith("trim"):
            return f
    return None


def all_takes(ids=None, tokens=False):
    base = TOKENS if tokens else RAWDIR
    out = []
    if not base.exists():
        return out
    for kd in sorted(base.iterdir()):
        if not kd.is_dir():
            continue
        if ids and key_line(kd.name) not in ids and kd.name not in ids:
            continue
        for td in sorted(kd.glob("take_*")):
            a = take_audio(td)
            if a:
                out.append((kd.name, int(td.name.split("_")[1]), a))
    return out


# ------------------------------------------------------------------ trim + metrics
def trim_speech(x, sr, keep_breath=False, lead_ms=40, tail_ms=140):
    db = frame_rms_db(x, sr, 5)
    pk = db.max()
    floor = np.percentile(db, 10)
    thr = max(pk - (48 if keep_breath else 40), floor + 10, -62 if keep_breath else -58)
    above = db > thr
    run = np.convolve(above.astype(int), np.ones(3, int), mode="valid") == 3  # 15 ms sustained
    on = int(np.argmax(run)) if run.any() else 0
    alive = np.where(db > max(pk - 50, floor + 8, -66))[0]
    off = int(alive[-1]) + 1 if len(alive) else len(db)
    s = max(0, on * int(sr * 0.005) - int(sr * lead_ms / 1000))
    e = min(len(x), off * int(sr * 0.005) + int(sr * tail_ms / 1000))
    y = x[s:e].copy()
    fi = int(sr * 0.003)
    y[:fi] *= np.linspace(0, 1, fi)
    fo = min(len(y), int(sr * 0.04))
    y[-fo:] *= np.linspace(1, 0, fo) ** 1.5
    return y, s / sr, e / sr


def f0_stats(x, sr):
    import librosa
    y = to_sr(x, sr, 16000).astype(np.float32)
    try:
        f0, vflag, vprob = librosa.pyin(y, fmin=60, fmax=450, sr=16000, frame_length=1024, hop_length=256)
    except Exception:
        return None, 0.0
    v = f0[vflag & np.isfinite(f0)] if f0 is not None else []
    if len(v) < 5:
        return None, 0.0
    return float(np.median(v)), float(len(v) / max(len(f0), 1))


def prep_one(item):
    key, tid, path, tokens = item
    lid = key_line(key)
    wd = (TOKENS / key / f"take_{tid}") if tokens else (WORK / key)
    wd.mkdir(parents=True, exist_ok=True)
    out = wd / (f"trim.wav" if tokens else f"take_{tid}.wav")
    meta = out.with_suffix(".json")
    if meta.exists() and meta.stat().st_mtime >= path.stat().st_mtime and out.exists():
        return key, tid, json.loads(meta.read_text())
    x, sr = read_audio(path)
    raw_dur = len(x) / sr
    clip = clipped_samples(x)
    raw_pk = float(20 * np.log10(max(np.abs(x).max(), 1e-12)))
    db = frame_rms_db(x, sr, 10)
    end_db = float(db[-3:].max() - db.max()) if len(db) > 3 else 0.0
    x48 = to_sr(x, sr, SR)
    keep_breath = (not tokens) and lid.startswith("D0") and load_dialogue().get(lid, {}).get("voice_id") == "vo_calder"
    y, s, e = trim_speech(x48, SR, keep_breath=keep_breath)
    write_wav16(out, y, SR)
    y16 = to_sr(y, SR, 16000)
    write_wav16(out.with_name(out.stem + "_16k.wav"), y16, 16000)
    f0, vr = f0_stats(y, SR)
    # internal gaps (frames 45 dB under peak for > 0.9 s)
    d5 = frame_rms_db(y, SR, 10)
    quiet = d5 < d5.max() - 45
    gaps, cur = [], 0
    for q in quiet:
        cur = cur + 1 if q else 0
        gaps.append(cur)
    m = {"key": key, "take": tid, "src": str(path.relative_to(ROOT)), "src_sr": sr, "raw_dur": round(raw_dur, 3),
         "raw_clipped": clip, "raw_peak_db": round(raw_pk, 2), "raw_end_rel_db": round(end_db, 1),
         "trim_start": round(s, 3), "trim_end": round(e, 3), "dur": round(len(y) / SR, 3),
         "f0_med": round(f0, 1) if f0 else None, "voiced_ratio": round(vr, 3),
         "max_gap_s": round(max(gaps) * 0.01 if gaps else 0, 2)}
    meta.write_text(json.dumps(m, indent=1))
    return key, tid, m


def prep(ids, tokens=False):
    items = [(k, t, p, tokens) for k, t, p in all_takes(ids, tokens)]
    with cf.ProcessPoolExecutor(4) as ex:
        res = list(ex.map(prep_one, items, chunksize=4))
    print(f"prepped {len(res)} takes")
    return res


# ------------------------------------------------------------------ STT
def stt(ids, tokens=False):
    dlg = load_dialogue()
    todo = []
    for key, tid, path in all_takes(ids, tokens):
        wd = (TOKENS / key / f"take_{tid}") if tokens else (WORK / key)
        f16 = wd / ("trim_16k.wav" if tokens else f"take_{tid}_16k.wav")
        sttdir = wd / (f"stt" if tokens else f"stt_take_{tid}")
        if (sttdir / "result.json").exists() or not f16.exists():
            continue
        todo.append((key, tid, f16, sttdir))
    if not todo:
        print("stt: nothing to do"); return
    # upload in batches
    urls = {}
    for i in range(0, len(todo), 40):
        batch = [str(t[2]) for t in todo[i:i + 40]]
        p = subprocess.run([FAL_PY, str(TOOLS / "fal_upload.py"), *batch], capture_output=True, text=True, cwd=str(ROOT))
        if p.returncode != 0:
            print(p.stderr[-500:]); sys.exit(3)
        urls.update(json.loads(p.stdout))

    def call(t):
        key, tid, f16, sttdir = t
        url = urls.get(str(f16), "")
        if not url.startswith("http"):
            return key, tid, "upload-fail"
        lid = key_line(key)
        args = {"audio_url": url, "tag_audio_events": True, "diarize": False}
        est = 0.008
        if tokens:
            args["language_code"] = "eng"
        elif lid not in INVENTED:
            args["language_code"] = "eng"
            text = dlg[lid]["text"].lower()
            kt = [k for k in KEYTERMS if k.lower() in text]
            if kt:
                args["keyterms"] = kt
                est = 0.0104
        sttdir.mkdir(parents=True, exist_ok=True)
        (sttdir / "args.json").write_text(json.dumps(args))
        cmd = [FAL_PY, str(FALCALL), "--wp", WP_TAG, "--id", f"STT_{key}_t{tid}", "--endpoint",
               "fal-ai/elevenlabs/speech-to-text/scribe-v2", "--args", str(sttdir / "args.json"), "--est-usd", str(est),
               "--out", str(sttdir)]
        for attempt in range(3):
            p = subprocess.run(cmd, capture_output=True, text=True, cwd=str(ROOT))
            if p.returncode == 2:
                return key, tid, "CAP"
            if p.returncode == 0:
                return key, tid, "ok"
        return key, tid, "fail:" + p.stderr[-200:]

    with cf.ThreadPoolExecutor(8) as ex:
        res = list(ex.map(call, todo))
    bad = [r for r in res if r[2] != "ok"]
    print(f"stt: {len(res) - len(bad)} ok, {len(bad)} not ok {bad[:5]}")
    if any(r[2] == "CAP" for r in res):
        sys.exit(2)


def load_stt(sttdir):
    f = sttdir / "result.json"
    if not f.exists():
        return None
    return json.loads(f.read_text())


# ------------------------------------------------------------------ scoring
def syllables(text):
    return max(1, len(re.findall(r"[aeiouy]+", text.lower())))


def judge(key, tid, m, st, line, voice_f0, line_f0=None):
    lid = key_line(key)
    issues = []
    r = {"key": key, "take": tid, **{k: m[k] for k in ("dur", "raw_clipped", "f0_med", "max_gap_s", "raw_end_rel_db")}}
    if m["raw_clipped"] > 3:
        issues.append(f"clipped:{m['raw_clipped']}")
    if m["raw_end_rel_db"] > -28:
        issues.append(f"cut_off_raw_end:{m['raw_end_rel_db']}")
    if st is None:
        issues.append("no_stt")
        r.update(transcript=None, wer=None)
    else:
        hyp = st.get("text", "")
        r["transcript"] = hyp
        r["stt_lang"] = f"{st.get('language_code')}:{round(st.get('language_probability') or 0, 2)}"
        words = [w for w in st.get("words", []) if w.get("type") == "word"]
        if words:
            r["speech_start"] = round(words[0]["start"], 3)
            r["speech_end"] = round(words[-1]["end"], 3)
        if lid in INVENTED:
            n_ref = len(norm_words(line["text"]))
            n_hyp = len(norm_words(hyp))
            r["wer"] = None
            if n_hyp > n_ref + 2:
                issues.append(f"extra_words:{n_hyp}>{n_ref}")
            syl = syllables(line["text"])
            lo, hi = 0.10 * syl + 0.1, 0.45 * syl + 0.9
            if lid == "D012":  # public-address cadence with long pauses between phrases
                hi = 0.6 * syl + 2.5
            if not (lo <= m["dur"] <= hi):
                issues.append(f"dur_implausible:{m['dur']} not in [{lo:.2f},{hi:.2f}]")
            rw = real_words(hyp, line["text"])
            r["real_words"] = rw
            if rw and not re.fullmatch(r"\s*\[[^\]]*\]\s*", hyp or ""):
                issues.append(f"sounds_like_real_word:{','.join(rw)}")
            r["phon_sim"] = phon_sim(hyp, line["text"]) if not re.fullmatch(r"\s*\[[^\]]*\]\s*", hyp or "") else None
        else:
            w = wer(line["text"], hyp)
            r.update(wer=w["wer"], errors=w["errors"], n_words=w["n"], ops=w["ops"])
            if w["errors"] > allowed_errors(w["n"]):
                issues.append(f"wer:{w['wer']}")
            ins = [o[2] for o in w["ops"] if o[0] in ("I", "S") and o[2] in TAG_WORDS and o[2] not in w["ref"]]
            if ins:
                issues.append(f"tag_spoken:{ins}")
            # cut-off: last reference word must be present (unless the line trails off with ...)
            if w["ops"] and not line["text"].rstrip().endswith("...") and w["ref"]:
                if any(o[0] == "D" and o[1] == w["ref"][-1] for o in w["ops"][-2:]):
                    issues.append("last_word_missing")
            if words:
                span = words[-1]["end"] - words[0]["start"]
                wps = w["n"] / max(span, 0.2)
                r["wps"] = round(wps, 2)
                if w["n"] >= 4 and not (1.0 <= wps <= 5.2):
                    issues.append(f"rate:{wps:.2f}wps")
    pause_ok = "..." in line["text"] or "pause" in line["delivery"]
    if m["max_gap_s"] > (2.2 if pause_ok else 1.3):
        issues.append(f"long_gap:{m['max_gap_s']}")
    if m["f0_med"] and voice_f0:
        r["f0_dev_st"] = round(12 * math.log2(m["f0_med"] / voice_f0), 2)  # info: expressive lines move a lot
    whisper = "whisper" in line["delivery"]
    if m["f0_med"] and line_f0 and not whisper:
        dl = 12 * math.log2(m["f0_med"] / line_f0)
        r["f0_dev_line_st"] = round(dl, 2)
        if abs(dl) > 6.0:  # this take is far from its sibling takes of the same line: odd voice or octave jump
            issues.append(f"pitch_vs_takes:{dl:+.1f}st")
    r["issues"] = issues
    r["pass"] = not issues
    return r


def score(ids):
    dlg = load_dialogue()
    takes = {}
    for key, tid, path in all_takes(None):
        wd = WORK / key
        mj = wd / f"take_{tid}.json"
        if not mj.exists():
            continue
        takes.setdefault(key, []).append((tid, json.loads(mj.read_text()), load_stt(wd / f"stt_take_{tid}")))
    # voice medians (pitch) from all takes, per voice id (+ Legion variant)
    vf = {}
    for key, ts in takes.items():
        v = dlg[key_line(key)]["voice_id"] + (key[-2:] if dlg[key_line(key)]["voice_id"] == "vo_legion" else "")
        for tid, m, st in ts:
            if m["f0_med"]:
                vf.setdefault(v, []).append(m["f0_med"])
    vmed = {v: float(np.median(a)) for v, a in vf.items()}
    selp = RAW / "selection.json"
    sel = json.loads(selp.read_text()) if selp.exists() else {}
    repp = RAW / "verify_report.json"
    report = json.loads(repp.read_text()).get("takes", {}) if (ids and repp.exists()) else {}  # --ids: update in place
    report = {k: v for k, v in report.items() if k in takes}
    for key, ts in sorted(takes.items()):
        if ids and key_line(key) not in ids and key not in ids:
            continue
        line = dlg[key_line(key)]
        v = line["voice_id"] + (key[-2:] if line["voice_id"] == "vo_legion" else "")
        lf = [m["f0_med"] for _, m, _ in ts if m["f0_med"]]
        line_f0 = float(np.median(lf)) if len(lf) >= 3 else None
        rs = [judge(key, tid, m, st, line, vmed.get(v), line_f0) for tid, m, st in sorted(ts)]
        passing = [r for r in rs if r["pass"]]
        pool = passing or rs
        md = float(np.median([r["dur"] for r in pool]))

        def cost(r):
            c = (r["wer"] or 0) * 3 + 0.6 * abs(math.log(max(r["dur"], 0.05) / md)) + 0.05 * abs(r.get("f0_dev_st") or 0)
            # a content word heard as a different word ("charge" -> "church") is worse than a dropped "is"
            c += 0.4 * sum(1 for o in (r.get("ops") or []) if o[0] == "S" and len(o[1] or "") >= 4)
            if key_line(key) in INVENTED:  # prefer the take that says what we spelled; a pure non-speech sound
                ps = r.get("phon_sim")       # (pain grunt, transcript "[grunts]") counts as a perfect match
                c += 2.0 * (1 - (ps if ps is not None else 1.0))
            return c + 5 * len(r["issues"])
        best = min(pool, key=cost)
        prev = sel.get(key, {})
        if prev.get("locked"):
            best = next((r for r in rs if r["take"] == prev["take"]), best)
        sel[key] = {"take": best["take"], "pass": best["pass"], "issues": best["issues"], "wer": best["wer"],
                    "n_takes": len(rs), "n_pass": len(passing), "locked": prev.get("locked", False),
                    "note": prev.get("note", "")}
        report[key] = rs
    selp.write_text(json.dumps(sel, indent=1, sort_keys=True))
    repp.write_text(json.dumps({"voice_f0_median": vmed, "takes": report}, indent=1))
    sel = {k: v for k, v in sel.items() if k in takes}
    selp.write_text(json.dumps(sel, indent=1, sort_keys=True))
    fails = {k: v for k, v in sel.items() if not v["pass"] and (not ids or key_line(k) in ids or k in ids)}
    print(f"scored {len(report)} keys; {len(fails)} without a passing take")
    for k, v in sorted(fails.items()):
        print("  FAIL", k, v["n_takes"], v["issues"])
    print("voice f0 medians:", {k: round(v, 1) for k, v in vmed.items()})


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["prep", "stt", "score", "all", "tokens"])
    ap.add_argument("--ids", default="")
    a = ap.parse_args()
    ids = set(filter(None, a.ids.split(",")))
    if a.mode == "tokens":
        prep(ids, tokens=True); stt(ids, tokens=True); return
    if a.mode in ("prep", "all"):
        prep(ids)
    if a.mode in ("stt", "all"):
        stt(ids)
    if a.mode in ("score", "all"):
        score(ids)


if __name__ == "__main__":
    main()
