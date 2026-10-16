#!/usr/bin/env python3
"""Build the shipped VO: selected take -> every chain the manifest lists -> -18 LUFS / <= -1 dBTP mono 48 kHz
16-bit WAV in RawAssets/audio/ready/Assets/_Game/Audio/VO/<CHAIN>/, plus 30 Hz RMS envelopes and the import index.

  python vo_build.py [--ids D001,...]      # build (idempotent; rebuilds everything listed)
  python vo_build.py --index-only          # rewrite envelopes for the Clockmaker lines + import_index_vo.json

Needs: vo_verify.py score (selection.json) and, for D039/D066/D080/D092, clockmaker_splice.py lines.
"""
import argparse, concurrent.futures as cf, json, re
import numpy as np
from common import (ROOT, WORK, RAW, VO_OUT, ENV_OUT, INDEX, SR, read_audio, write_wav16, lufs, true_peak_db,
                    lead_silence_ms, tail_silence_ms, clipped_samples, envelope30, stable_seed, load_dialogue, load_manifest)
from vo_chains import CHAINS, finalize

PROCEDURAL = {"D039", "D066", "D080", "D092"}
ROTOR_LINES = {"D201": -12.0, "D204": -12.0}
LOWRATE = {"RADIO", "INTERCOM", "HF_AM", "GREYCOAT_SQUELCH", "PA_HORN"}


def clip_name(lid, chain, variant=None):
    return f"VO_{lid}_{chain}" + (f"_{variant}" if variant else "")


def import_note(chain):
    q, sr = (60, 22050) if chain in LOWRATE else (70, 24000)
    return (f"AudioClip (AudioImportRules, folder Audio/VO/{chain}): Force To Mono ON, Load Type Compressed In Memory, "
            f"Preload Audio Data OFF, Load In Background ON, Quality {q}, Sample Rate Setting = Override {sr} Hz, "
            f"not looping (one-shot VO line). Source: 48 kHz 16-bit mono WAV, -18 LUFS integrated, true peak <= -1 dBTP, "
            f"lead silence <= 60 ms; filters/reverb/radio already baked (D9).")


def cut_after_last_word(x, key, take, extra=0.03):
    st = WORK / key / f"stt_take_{take}" / "result.json"
    if not st.exists():
        return x
    words = [w for w in json.loads(st.read_text()).get("words", []) if w.get("type") == "word"]
    if not words:
        return x
    e = min(len(x), int((words[-1]["end"] + extra) * SR))
    y = x[:e].copy()
    f = int(SR * 0.006)
    y[-f:] *= np.linspace(1, 0, f)
    return y


SYNC_CHAINS = {"DRY", "RADIO", "INTERCOM"}  # crossfaded pairs stay sample-synchronous: never trimmed separately


def enforce_lead(y, max_ms=60.0, keep_ms=45.0, fade_ms=25.0):
    """Lead silence <= 60 ms: cut a quiet head (a soft inhale in BOOTH, a fricative onset low-passed away in FAR) to
    keep_ms before the first frame within 40 dB of the loudest one, with a short fade-in. Returns (y, seconds cut)."""
    lead = lead_silence_ms(y)
    if lead <= max_ms:
        return y, 0.0
    cut = int(SR * (lead - keep_ms) / 1000)
    y = y[cut:].copy()
    f = int(SR * fade_ms / 1000)
    y[:f] *= np.linspace(0, 1, f) ** 2
    return y, cut / SR


def build_key(job):
    lid, variant, chains, take, voice_id = job
    key = lid if not variant else f"{lid}_{variant}"
    x, sr = read_audio(WORK / key / f"take_{take}.wav")
    assert sr == SR
    if lid == "D082":  # "confused, then afraid; cut off": the line is interrupted right after the last word
        x = cut_after_last_word(x, key, take)
    rows = []
    voice_norm, _ = finalize(CHAINS["DRY"](x, SR)[0])
    venv = envelope30(voice_norm)
    for ch in chains:
        seed = stable_seed(lid, ch, variant or "")
        kw = {}
        if voice_id == "vo_magpie" and ch == "RADIO":
            kw = {"rotor": True, "rotor_db": -18.0}   # pilot's mic hears the cabin
        if lid in ROTOR_LINES and ch in ("DRY", "RADIO"):
            kw = {"rotor": True, "rotor_db": ROTOR_LINES[lid]}  # shouted in the cabin / at the LZ (manifest note)
        y, info = CHAINS[ch](x, SR, seed=seed, **kw)
        y, m = finalize(y)
        if ch not in SYNC_CHAINS:
            y, cut = enforce_lead(y)
            if cut:
                info = dict(info, offset_s=info["offset_s"] - cut)
                y, m = finalize(y)
        name = clip_name(lid, ch, variant)
        out = VO_OUT / ch / f"{name}.wav"
        write_wav16(out, y, SR, seed=seed)
        env = envelope30(y)
        rows.append({"clip": name, "line": lid, "variant": variant, "chain": ch, "take": take,
                     "file": str(out.relative_to(ROOT / "RawAssets" / "audio" / "ready")),
                     "duration_s": round(len(y) / SR, 3), "speech_offset_s": round(info["offset_s"], 3),
                     "lead_ms": round(lead_silence_ms(y), 1), "tail_ms": round(tail_silence_ms(y), 1),
                     "clipped": clipped_samples(y), **m,
                     "env": [round(float(v), 4) for v in env]})
    return key, rows, [round(float(v), 4) for v in venv], round(len(voice_norm) / SR, 3)


def procedural_rows(lid, chain):
    f = VO_OUT / chain / f"VO_{lid}_{chain}.wav"
    if not f.exists():
        return None
    y, sr = read_audio(f)
    return {"clip": f"VO_{lid}_{chain}", "line": lid, "variant": None, "chain": chain, "take": "splice",
            "file": str(f.relative_to(ROOT / "RawAssets" / "audio" / "ready")), "duration_s": round(len(y) / sr, 3),
            "speech_offset_s": 0.03, "lead_ms": round(lead_silence_ms(y), 1), "tail_ms": round(tail_silence_ms(y), 1),
            "clipped": clipped_samples(y), "lufs": round(lufs(y), 2), "true_peak_db": round(true_peak_db(y), 2),
            "sample_peak_db": round(20 * np.log10(np.abs(y).max() + 1e-12), 2), "env": [round(float(v), 4) for v in envelope30(y)]}


def splice_tokens(lid):
    """Token onsets of a Clockmaker splice (0.03 s + k * 0.9 s), for PositionalRadio (D039 goes 2D at the first 'Seven')."""
    rp = RAW / "clockmaker" / "splice_report.json"
    if not rp.exists():
        return None
    r = json.loads(rp.read_text()).get(lid)
    if not r:
        return None
    return [{"token": t, "t": round(0.03 + k * r["grid_s"], 3)} for k, t in enumerate(r["tokens"])]


def write_envelope(lid, rows, voice=None):
    ENV_OUT.mkdir(parents=True, exist_ok=True)
    doc = {"id": lid, "rate_hz": 30, "hop_samples_48k": SR // 30, "kind": "rms_linear",
           "note": "30 Hz RMS of the shipped clip (D10). Sample at playback time t: rms[min(int(t*30), n-1)]. "
                   "speech_offset_s = where the words start relative to the DRY take (TAPE adds a motor start).",
           "clips": {}}
    if voice:
        doc["voice"] = voice
    toks = splice_tokens(lid) if lid in PROCEDURAL else None
    if toks:
        doc["token_onsets_s"] = toks
    for r in rows:
        doc["clips"][r["clip"]] = {"chain": r["chain"], "variant": r["variant"], "duration_s": r["duration_s"],
                                   "speech_offset_s": r["speech_offset_s"], "rms_max": max(r["env"]) if r["env"] else 0,
                                   "rms": r["env"]}
    p = ENV_OUT / f"VO_{lid}.json"
    p.write_text(json.dumps(doc, separators=(",", ":")))
    return p


def write_index(report):
    man = load_manifest()
    rows = []
    for lid, recs in sorted(report.items()):
        chains = sorted({r["chain"] for r in recs})
        for r in recs:
            extra = []
            if r["variant"]:
                extra.append(f"Legion bark voice variant {r['variant']} (pick A or B at random per speaker).")
            sync = [c for c in chains if c in SYNC_CHAINS and c != r["chain"]]
            if r["chain"] in SYNC_CHAINS and sync:
                extra.append(f"Sample-synchronous with VO_{lid}_{sync[0]} (dry/radio distance crossfade, TECH_PLAN 7.10); start both together.")
            if r["chain"] == "FAR":
                extra.append("Distant variant of the bark (choose by speaker distance; not sample-synchronous with DRY).")
            if r.get("speech_offset_s", 0) > 0.05:
                extra.append(f"Words start {r['speech_offset_s']:.2f} s into the clip (TAPE motor start); see speech_offset_s in the envelope.")
            if lid in ROTOR_LINES:
                extra.append(f"Helicopter rotor bed baked {abs(ROTOR_LINES[lid]):.0f} dB under the voice (CINEMATIC_DIRECTION 2).")
            if lid == "D039":
                first7 = next((t["t"] for t in (splice_tokens(lid) or []) if t["token"] == "seven"), None)
                extra.append(f"PositionalRadio (G9): switch from 3D (Tinker's PRC-77) to 2D radio at the first 'Seven', "
                             f"{first7:.2f} s into the clip; all token onsets are in the envelope (token_onsets_s).")
            if lid == "D066":
                extra.append("Cut mid-word into static and capped at 2.2 s (4th rack dies); plays from the basement room speaker.")
            if lid == "D082":
                extra.append("Ends abruptly 30 ms after the last word (scripted interruption).")
            rows.append({"manifest_id": f"VO_{lid}", "file": r["file"], "target_path": r["file"], "type": "audio",
                         "import_notes": " ".join([import_note(r["chain"])] + extra)})
        envf = f"Assets/_Game/Audio/Envelopes/VO_{lid}.json"
        if (ROOT / "RawAssets" / "audio" / "ready" / envf).exists():
            rows.append({"manifest_id": f"VO_{lid}", "file": envf, "target_path": envf, "type": "json",
                         "import_notes": "TextAsset (JSON). 30 Hz RMS envelope per clip of this line (D10) for the VU needle, "
                                         "jaw flap and voice-activity look-at; read by DialogueImporter. No import settings."})
    missing = [k for k, e in man.items() if e["category"] == "vo" and k.startswith("VO_") and k != "VO_CLOCKMAKER_TOKENS"
               and not any(r["manifest_id"] == k for r in rows)]
    INDEX.write_text(json.dumps(rows, indent=1))
    print(f"index: {len(rows)} rows -> {INDEX.relative_to(ROOT)}; manifest VO lines without files: {missing}")
    return missing


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids", default="")
    ap.add_argument("--index-only", action="store_true")
    a = ap.parse_args()
    ids = set(filter(None, a.ids.split(",")))
    dlg, man = load_dialogue(), load_manifest()
    sel = json.loads((RAW / "selection.json").read_text())
    repp = RAW / "build_report.json"
    report = json.loads(repp.read_text()) if repp.exists() else {}
    if not a.index_only:
        jobs = []
        for lid, line in dlg.items():
            if lid in PROCEDURAL or (ids and lid not in ids):
                continue
            chains = man[f"VO_{lid}"]["variants"]
            variants = ["A", "B"] if line["voice_id"] == "vo_legion" else [None]
            for v in variants:
                key = lid if not v else f"{lid}_{v}"
                if key not in sel:
                    print("no selection for", key); continue
                jobs.append((lid, v, chains, sel[key]["take"], line["voice_id"]))
        done = {}
        with cf.ProcessPoolExecutor(4) as ex:
            for key, rows, venv, vdur in ex.map(build_key, jobs):
                lid = re.sub(r"_[AB]$", "", key)
                done.setdefault(lid, {"rows": [], "voice": {}})
                done[lid]["rows"] += rows
                vname = key[-1] if key != lid else ""
                done[lid]["voice"]["voice" + (f"_{vname}" if vname else "")] = {"duration_s": vdur, "rms": venv}
        for lid, d in done.items():
            rows = sorted(d["rows"], key=lambda r: r["clip"])
            write_envelope(lid, rows, d["voice"])
            report[lid] = [{k: v for k, v in r.items() if k != "env"} for r in rows]
            bad = [r["clip"] for r in rows if r["lead_ms"] > 60 or abs(r["lufs"] + 18) > 1 or r["true_peak_db"] > -1 or r["clipped"]]
            if bad:
                print("CHECK FAIL", bad)
        print(f"built {sum(len(d['rows']) for d in done.values())} clips for {len(done)} lines")
    for lid in PROCEDURAL:
        chain = man[f"VO_{lid}"]["variants"][0]
        r = procedural_rows(lid, chain)
        if r:
            write_envelope(lid, [r])
            report[lid] = [{k: v for k, v in r.items() if k != "env"}]
    repp.write_text(json.dumps(report, indent=1))
    write_index(report)


if __name__ == "__main__":
    main()
