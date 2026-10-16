#!/usr/bin/env python3
"""Independent audit of the shipped VO (reads the files on disk, not the build report).

  python vo_audit.py            # prints failures; writes RawAssets/audio/vo/audit.json

Checks per WAV: mono, 48 kHz, 16-bit PCM; -18 LUFS +-1; true peak <= -1 dBTP; lead silence <= 60 ms; no clipped samples;
file belongs to a manifest line + chain. Per line: one clip per manifest chain (x2 voices for Legion barks), envelope
JSON present with every clip and n = ceil(duration * 30) values; DRY/RADIO/INTERCOM pairs sample-synchronous
(envelope cross-correlation lag 0 +-1 frame). Also: no stale files, and the estimated build size per chain.
"""
import json, math, re, sys
import numpy as np
import soundfile as sf
from common import ROOT, RAW, VO_OUT, ENV_OUT, INDEX, load_dialogue, load_manifest, lufs, true_peak_db, lead_silence_ms, clipped_samples

KBPS = {"RADIO": 48, "INTERCOM": 48, "HF_AM": 48, "GREYCOAT_SQUELCH": 48, "PA_HORN": 48}  # rough AAC rates at the import rules


def main():
    dlg, man = load_dialogue(), load_manifest()
    expect = {}
    for lid, l in dlg.items():
        e = man.get(f"VO_{lid}")
        if not e:
            continue
        for ch in e["variants"]:
            for v in (["A", "B"] if l["voice_id"] == "vo_legion" else [None]):
                expect[f"VO_{lid}_{ch}" + (f"_{v}" if v else "")] = (lid, ch)
    files = {p.stem: p for p in VO_OUT.rglob("*.wav")}
    fails, rows = [], {}
    for name, p in sorted(files.items()):
        if name not in expect:
            fails.append(f"stale/unknown file {p.relative_to(ROOT)}")
            continue
        info = sf.info(str(p))
        x, sr = sf.read(str(p))
        r = {"dur": round(len(x) / sr, 3), "ch": info.channels, "sr": sr, "subtype": info.subtype}
        if info.channels != 1 or sr != 48000 or info.subtype != "PCM_16":
            fails.append(f"{name}: format {info.channels}ch {sr} {info.subtype}")
        r["lufs"] = round(lufs(x, sr), 2)
        r["tp"] = round(true_peak_db(x, sr), 2)
        r["lead_ms"] = lead_silence_ms(x, sr)
        r["clipped"] = clipped_samples(x)
        if abs(r["lufs"] + 18) > 1: fails.append(f"{name}: LUFS {r['lufs']}")
        if r["tp"] > -1.0: fails.append(f"{name}: TP {r['tp']}")
        if r["lead_ms"] > 60: fails.append(f"{name}: lead {r['lead_ms']} ms")
        if r["clipped"]: fails.append(f"{name}: clipped {r['clipped']}")
        if p.parent.name != expect[name][1]: fails.append(f"{name}: wrong folder {p.parent.name}")
        rows[name] = r
    for name in expect:
        if name not in files:
            fails.append(f"missing {name}")
    # envelopes
    for lid in sorted({v[0] for v in expect.values()}):
        ep = ENV_OUT / f"VO_{lid}.json"
        if not ep.exists():
            fails.append(f"missing envelope {ep.name}"); continue
        env = json.loads(ep.read_text())
        clips = env.get("clips", {})
        for name, (l2, ch) in expect.items():
            if l2 != lid: continue
            if name not in clips:
                fails.append(f"{ep.name}: no envelope for {name}"); continue
            if name in rows and len(clips[name]["rms"]) != math.ceil(rows[name]["dur"] * 48000 / 1600 - 1e-9):
                fails.append(f"{ep.name}: {name} has {len(clips[name]['rms'])} values for {rows[name]['dur']} s")
        # sync of crossfade pairs
        pair = [n for n in clips if re.search(r"_(DRY|RADIO|INTERCOM)$", n)]
        if len(pair) == 2:
            a, b = (np.array(clips[n]["rms"]) for n in pair)
            n = min(len(a), len(b))
            a, b = a[:n] - a[:n].mean(), b[:n] - b[:n].mean()
            lags = range(-5, 6)
            cc = [np.dot(a[max(0, -k): n - max(0, k)], b[max(0, k): n - max(0, -k)]) for k in lags]
            lag = list(lags)[int(np.argmax(cc))]
            if abs(lag) > 1:
                fails.append(f"{lid}: {pair} out of sync by {lag} frames")
    stale_env = [p.name for p in ENV_OUT.glob("VO_*.json") if p.stem[3:] not in {v[0] for v in expect.values()}]
    fails += [f"stale envelope {s}" for s in stale_env]
    idx = json.loads(INDEX.read_text())
    for r in idx:
        if not (ROOT / "RawAssets" / "audio" / "ready" / r["file"]).exists():
            fails.append(f"index row without file {r['file']}")
    by_chain = {}
    for name, r in rows.items():
        ch = expect[name][1]
        by_chain.setdefault(ch, [0, 0.0])
        by_chain[ch][0] += 1
        by_chain[ch][1] += r["dur"]
    est = {ch: {"clips": n, "seconds": round(s, 1), "est_kb": round(s * KBPS.get(ch, 56) / 8)} for ch, (n, s) in sorted(by_chain.items())}
    total_kb = sum(v["est_kb"] for v in est.values())
    summary = {"clips": len(rows), "expected": len(expect), "envelopes": len(list(ENV_OUT.glob("VO_*.json"))),
               "index_rows": len(idx), "lufs_range": [min(r["lufs"] for r in rows.values()), max(r["lufs"] for r in rows.values())],
               "tp_max": max(r["tp"] for r in rows.values()), "lead_max_ms": max(r["lead_ms"] for r in rows.values()),
               "total_seconds": round(sum(r["dur"] for r in rows.values()), 1), "est_build_kb": total_kb,
               "avg_est_kb_per_clip": round(total_kb / max(len(rows), 1), 1), "by_chain": est,
               "wav_mb_on_disk": round(sum(p.stat().st_size for p in files.values()) / 1e6, 1), "fails": fails}
    (RAW / "audit.json").write_text(json.dumps({"summary": summary, "clips": rows}, indent=1))
    print(json.dumps({k: v for k, v in summary.items() if k != "by_chain"}, indent=1))
    for ch, v in est.items():
        print(f"  {ch:17s} {v}")
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()
