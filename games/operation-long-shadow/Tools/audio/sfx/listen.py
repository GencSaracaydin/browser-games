"""Machine 'listening' for WP06_SFX guardrails and realism checks via fal-ai/audio-understanding (through falcall,
$0.01 per 5 s of audio). O4 waived the human listen gate; this is an extra objective-ish check on top of the
measurements: it catches voices, singing, bells, chanting or music hiding in ambience beds, and describes takes.

listen(path, kind) -> text answer (cached in RawAssets/audio/sfx/_qa/listen/<hash>/result.json)
flags(text) -> list of guardrail keywords found in the answer (affirmative mentions only)
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import alib as A  # noqa: E402
import gen  # noqa: E402

QA = gen.ROOT / "RawAssets/audio/sfx/_qa/listen"

PROMPTS = {
    "ambience": ("You are auditing a game ambience loop for content rules. List every sound source you can hear, in order of "
                 "loudness. Then answer each with yes or no: (1) any human voices, speech, shouting or whispering? (2) any "
                 "singing, humming, chanting or choir? (3) any music or musical instruments? (4) any bells or chimes? "
                 "(5) any call to prayer or religious sound? (6) any car horns, sirens or modern traffic? Keep it short."),
    "music": ("You are auditing a game music cue. Describe the instruments, tempo and mood in two sentences. Then answer each "
              "with yes or no: (1) any vocals, singing, humming, choir or spoken words? (2) any chanting or religious music? "
              "(3) any church bells? (4) does it use a Middle Eastern or other regional/ethnic scale or instrument? "
              "(5) does it fade out or end within the clip? Keep it short."),
    "sfx": ("You are a game sound designer. Describe exactly what you hear in this clip in two or three sentences. Does it sound "
            "like a realistic recording of the described event, or synthetic, muffled, distorted or cartoonish? Are there any "
            "voices, music or bells? Keep it short."),
    "gunshot": ("You are a game sound designer. How many gunshots are in this clip (0, 1, 2 or more)? Describe the sound: "
                "crack, low-end weight, mechanical noise, echo. Does it sound like a realistic recording of a real firearm, or "
                "synthetic, thin, muffled, distorted or cartoonish? Rate realism 1-10. Keep it short."),
}

NEG_WORDS = r"(no|not|none|without|absent|free of|lack|lacks|isn't|aren't|there are no|there is no)"


def listen(path, kind="sfx", prompt=None, tag="QA_LISTEN"):
    path = pathlib.Path(path)
    x, sr = A.load(path)
    dur = len(x) / sr
    # send a 16-bit 48 kHz WAV (the endpoint takes URLs; the upload is cached by hash)
    h = hashlib.sha1(path.read_bytes() + (prompt or PROMPTS[kind]).encode()).hexdigest()[:16]
    out = QA / h
    res = out / "result.json"
    if not res.exists():
        # the endpoint rejects large files ("Invalid audio" on a 17 MB 90 s stereo WAV): send 24 kHz mono
        small = out / "listen_24k_mono.wav"
        x24, _ = A.load(path, sr=24000)
        A.save(small, A.mono(x24), sr=24000)
        url = gen.upload(small)
        job = dict(id=tag, take=h, endpoint="fal-ai/audio-understanding",
                   args=dict(audio_url=url, prompt=prompt or PROMPTS[kind]), est_usd=gen.est_understanding(dur), out=str(out))
        r = gen.run_one(job)
        if r["status"] not in ("ok", "cached"):
            return f"LISTEN FAILED: {r}"
        (out / "source.txt").write_text(str(path))
    return json.loads(res.read_text()).get("output", "")


def match_prompt(desc, kind="sfx"):
    if kind == "gunshot":
        return (f"Expected sound: '{desc}'. You are a game sound designer. In one sentence describe what you actually hear. "
                "Then answer on separate lines exactly in this format: SHOTS: <number of gunshots>; MATCH: <1-10 how well it "
                "matches the expected sound>; REALISM: <1-10 how much it sounds like a real recording of a real firearm, "
                "not synthetic or cartoonish>; UNWANTED: <voices, music, bells or unrelated sounds you hear, or none>.")
    return (f"Expected sound: '{desc}'. You are a game sound designer. In one sentence describe what you actually hear. Then "
            "answer on separate lines exactly in this format: MATCH: <1-10 how well it matches the expected sound>; REALISM: "
            "<1-10 how much it sounds like a real recording, not synthetic or cartoonish>; UNWANTED: <voices, music, bells or "
            "unrelated sounds you hear, or none>.")


def parse_scores(text):
    def num(key):
        m = re.search(key + r"\W{0,6}(\d+(?:\.\d+)?)", text, flags=re.I)
        return float(m.group(1)) if m else None
    unw = re.search(r"UNWANTED\W{0,6}([^\n]*)", text, flags=re.I)
    u = unw.group(1).strip().strip("*").strip() if unw else ""
    return dict(shots=num("SHOTS"), match=num("MATCH"), realism=num("REALISM"), unwanted=u,
                unwanted_flag=bool(u) and not re.match(r"^(none|no|n/a|nothing)\b", u, flags=re.I))


def yes_answers(text):
    """Parse '(n) ...? yes/no' style answers: returns the numbers answered yes."""
    ys = set()
    for m in re.finditer(r"\(?(\d)\)?[^\n(]*?\b(yes|no)\b", text, flags=re.I):
        if m.group(2).lower() == "yes":
            ys.add(int(m.group(1)))
    return sorted(ys)
