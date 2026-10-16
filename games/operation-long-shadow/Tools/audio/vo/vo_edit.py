#!/usr/bin/env python3
"""Deterministic take edits (no new TTS call): keep only the first N words of a take, cut by the STT word timestamps.

ElevenLabs v4 repeats very short or list-like lines ("Behind us! Behind us!", "Zero six four zero six four zero"); the
first reading is a complete, natural performance, so the edit keeps it and drops the repeat.

  python vo_edit.py                 # apply every edit in EDITS (idempotent)

Each edit writes RawAssets/audio/vo/raw/<key>/take_<100 + source take>/edit.wav (+ edit.json with the cut points).
The edited take is then an ordinary take: `vo_verify.py all --ids <key>` re-trims, re-transcribes and re-scores it,
so the edit is verified like any TTS take.
"""
import json
import numpy as np
from common import RAWDIR, WORK, SR, read_audio, write_wav16

EDITS = {  # key: (keep_words, reason)
    "CO_HOL_04": (2, "EL repeats the callout in every take ('Behind us! Behind us!'); keep the first shout"),
    "D087": (4, "EL reads the digit group twice in every take; keep the first, evenly spaced reading"),
}


def edit_take(key, take, keep, reason):
    src = WORK / key / f"take_{take}.wav"
    st = WORK / key / f"stt_take_{take}" / "result.json"
    if not (src.exists() and st.exists()):
        return None
    words = [w for w in json.loads(st.read_text()).get("words", []) if w.get("type") == "word"]
    if len(words) <= keep:
        return None
    x, sr = read_audio(src)
    assert sr == SR
    end_w, next_w = words[keep - 1]["end"], words[keep]["start"]
    gap = next_w - end_w
    cut = end_w + min(0.16, 0.6 * gap)          # natural release, never into the repeat
    n = int(cut * SR)
    y = x[:n].copy()
    f = min(len(y), int(SR * min(0.05, 0.35 * gap)))
    y[-f:] *= np.linspace(1, 0, f) ** 1.5
    out = RAWDIR / key / f"take_{100 + take}"
    out.mkdir(parents=True, exist_ok=True)
    write_wav16(out / "edit.wav", y, SR, seed=take)
    meta = {"edit": "keep_first_words", "source_take": take, "source": str(src.name), "keep_words": keep,
            "words_kept": [w["text"] for w in words[:keep]], "cut_s": round(cut, 3), "next_word_start_s": round(next_w, 3),
            "reason": reason}
    (out / "edit.json").write_text(json.dumps(meta, indent=1))
    return meta


def main():
    for key, (keep, reason) in EDITS.items():
        for take in (1, 2, 3):
            m = edit_take(key, take, keep, reason)
            print(key, take, m and {k: m[k] for k in ("words_kept", "cut_s", "next_word_start_s")})


if __name__ == "__main__":
    main()
