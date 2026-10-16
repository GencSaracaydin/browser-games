"""EL Music v2.5 cues for WP06_SFX (GDD §10.3, TECH_PLAN §8.7-6, D15).

Every cue uses a composition_plan so that a chunk can carry an audio_reference (the v2.5 schema only accepts
audio_reference inside chunks; force_instrumental is prompt-only, so instrumental is enforced with lyric-free
chunk text plus negative styles, then checked by listening with fal-ai/audio-understanding).

Stages (each needs the selections of the previous one in RawAssets/audio/music/selection.json):
  1  MUS_01_SESSION            ref: MOTIF_REF (procedural music box motif over a drone, 72 BPM)
  2  MUS_02_MARKET_LO, MUS_03_LASTLIGHT, MUS_04_STATION_LOW, MUS_05_EXFIL, STG_GREYCOAT
                               ref: REF_PALETTE = approved MUS_01 loop excerpt with the procedural motif overlaid
     AMB_CASSETTE_TUNE         prompt mode, no reference (must not share the score palette)
  3  MUS_02_MARKET_HI          chunk-1 ref: approved MUS_02_MARKET_LO (same key/tempo), chunk-2 ref: REF_PALETTE
     MUS_04_STATION_FULL       chunk-1 ref: approved MUS_04_STATION_LOW, chunk-2 ref: REF_PALETTE

usage: python music_generate.py --stage 1|2|3 [--dry-run] [--takes a,b]
"""
from __future__ import annotations

import argparse
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import gen  # noqa: E402

ROOT = gen.ROOT
RAWM = ROOT / "RawAssets/audio/music"
SEL = RAWM / "selection.json"

NEG = ["vocals", "singing", "choir", "humming", "spoken word", "lyrics", "chant", "church bells", "religious music",
       "middle eastern", "arabic scale", "oud", "sitar", "ethnic percussion", "EDM drop", "dubstep", "trap beat"]


def ch(text, ms, pos, neg=None, adherence=None, ref=None, strength=None):
    c = {"text": text, "duration_ms": int(ms), "positive_styles": pos, "negative_styles": (neg or []) + NEG}
    if adherence:
        c["context_adherence"] = adherence
    if ref:
        c["audio_reference"] = {"audio_url": ref, "start_ms": 0, "strength": strength or "medium"}
    return c


def bar_ms(bpm, bars):
    return round(bars * 4 * 60000 / bpm)


PALETTE = ["1980s Cold War espionage film score", "analog polysynth", "tape hiss", "D minor", "instrumental"]


def plans(refs, sel):
    """cue -> (stage, [take plans]) ; each take plan = (take, seed, chunks)."""
    P = {}
    m01 = refs.get("MOTIF_REF")
    pal = refs.get("REF_PALETTE")
    # ---- MUS_01_SESSION: 72 BPM, bar 3.333 s; the last 30 s (9 bars) loops
    def m01_chunks(strength):
        return [
            ch("[Intro]\n{the three-note music box motif alone over a low analog drone}", bar_ms(72, 8),
               PALETTE + ["dark analog synth drone", "music box playing a simple three-note motif", "72 BPM", "sparse",
                          "dread", "slow"], ref=m01, strength=strength),
            ch("[Build]\n{a slow analog synth pulse enters at 72 BPM, the drone deepens}", bar_ms(72, 8),
               PALETTE + ["slow analog synth bass pulse", "Prophet-5 pads", "music box motif returns", "creeping dread",
                          "steady tempo"], adherence="high"),
            ch("[Loop]\n{steady and hypnotic, constant intensity, the motif repeats}", bar_ms(72, 9),
               PALETTE + ["steady 72 BPM pulse", "constant intensity", "hypnotic", "music box motif", "no ending"],
               adherence="high"),
            ch("[Continue]\n{the same groove continues}", bar_ms(72, 2),
               PALETTE + ["steady 72 BPM pulse", "constant intensity", "no ending", "no fade"], adherence="high"),
        ]
    P["MUS_01_SESSION"] = (1, [("take_a", 1984, m01_chunks("high")), ("take_b", 1985, m01_chunks("medium"))])

    # ---- MUS_02_MARKET_LO / HI: 110 BPM, 16-bar loops
    def m02(hi, ref1, s1):
        extra = ["driving percussion", "synth brass stabs", "high intensity"] if hi else ["tense but restrained"]
        base = PALETTE + ["1980s Cold War action film score", ("same tempo and key as the reference" if hi else "110 BPM"),
                          "sequenced analog bass arpeggio",
                          "gated toms", "string machine pads", "no intro"] + extra
        return [
            ch("[Main A]\n{the sequenced bass arpeggio starts immediately, tense action underscore}", bar_ms(110, 16),
               base, neg=(["slow intro", "ambient intro"] + ([] if hi else ["drum fill crescendo"])), ref=ref1, strength=s1),
            ch("[Main B]\n{same groove and instrumentation, constant intensity}", bar_ms(110, 16),
               base + ["constant intensity", "no breakdown"], adherence="high", ref=(pal if hi else None),
               strength="low" if hi else None),
            ch("[Main A]\n{the main groove again}", bar_ms(110, 8), base + ["constant intensity"], adherence="high"),
            ch("[Continue]\n{the groove continues}", bar_ms(110, 2), base + ["no ending", "no fade"], adherence="high"),
        ]
    P["MUS_02_MARKET_LO"] = (2, [("take_a", 2001, m02(False, pal, "medium")), ("take_b", 2002, m02(False, pal, "low"))])
    lo = refs.get("MUS_02_MARKET_LO")
    P["MUS_02_MARKET_HI"] = (3, [("take_a", 2011, m02(True, lo, "high")), ("take_b", 2012, m02(True, lo, "xhigh"))])

    # ---- MUS_03_LASTLIGHT: 75 s one-shot, keeps its ending
    def m03(s):
        base = ["slow elegiac film score", "warm analog synth pads", "string section", "sunset", "melancholy",
                "D minor", "instrumental", "1980s film score"]
        return [
            ch("[Opening]\n{warm synth pads, the strings enter softly}", 20000, base, ref=pal, strength=s),
            ch("[Swell]\n{the violins carry the three-note motif and swell as the sun touches the ridge}", 30000,
               base + ["strings swelling", "three-note motif carried by violins", "emotional crescendo"], adherence="high"),
            ch("[Resolution]\n{the music thins out and resolves into silence}", 25000,
               base + ["resolving", "final sustained chord", "fading into silence"], adherence="medium"),
        ]
    P["MUS_03_LASTLIGHT"] = (2, [("take_a", 3001, m03("low")), ("take_b", 3002, m03("medium"))])

    # ---- MUS_04_STATION_LOW / FULL: 96 BPM, 16-bar loops
    def m04(full, ref1, s1):
        extra = ["heavy toms", "distorted analog synth", "full intensity"] if full else ["sparse", "menacing"]
        base = PALETTE + ["dark 1980s thriller score", "96 BPM", "ticking closed hi-hat like a clock",
                          "low analog synth bass", "no intro"] + extra
        return [
            ch("[Main]\n{dark pulse with a ticking hi-hat like a clock, starts immediately}", bar_ms(96, 16), base,
               neg=["slow intro"], ref=ref1, strength=s1),
            ch("[Main continues]\n{same groove, constant intensity}", bar_ms(96, 16), base + ["constant intensity"],
               adherence="high", ref=(pal if full else None), strength="low" if full else None),
            ch("[Continue]\n{the groove continues}", bar_ms(96, 4), base + ["no ending", "no fade"], adherence="high"),
        ]
    P["MUS_04_STATION_LOW"] = (2, [("take_a", 4001, m04(False, pal, "medium")), ("take_b", 4002, m04(False, pal, "low"))])
    low = refs.get("MUS_04_STATION_LOW")
    P["MUS_04_STATION_FULL"] = (3, [("take_a", 4011, m04(True, low, "high")), ("take_b", 4012, m04(True, low, "xhigh"))])

    # ---- MUS_05_EXFIL: 120 BPM, 16-bar loop
    def m05(s):
        base = PALETTE + ["driving 1980s action film score", "120 BPM", "rotor-like pulsing synth ostinato",
                          "heroic but wrong", "minor key", "analog synths and strings", "no intro"]
        return [
            ch("[Main]\n{driving action cue with a rotor-like pulse, starts immediately}", bar_ms(120, 16), base,
               neg=["slow intro"], ref=pal, strength=s),
            ch("[Main B]\n{same groove, constant intensity}", bar_ms(120, 16), base + ["constant intensity"], adherence="high"),
            ch("[Main A]\n{the main groove again}", bar_ms(120, 12), base + ["constant intensity"], adherence="high"),
            ch("[Continue]\n{the groove continues}", bar_ms(120, 2), base + ["no ending", "no fade"], adherence="high"),
        ]
    P["MUS_05_EXFIL"] = (2, [("take_a", 5001, m05("medium")), ("take_b", 5002, m05("low"))])

    # ---- STG_GREYCOAT: 10 s stinger
    def stg(s):
        return [ch("[Stinger]\n{a single low synth-brass swell, ominous, then silence}", 10000,
                   ["single low synth brass swell", "ominous stinger", "dark analog synth", "1980s thriller score",
                    "instrumental", "swell then silence"], neg=["drums", "melody"], ref=pal, strength=s)]
    P["STG_GREYCOAT"] = (2, [("take_a", 6001, stg("low")), ("take_b", 6002, stg("medium"))])
    return P


CASSETTE_PROMPT = ("A scratchy early-1960s instrumental surf rock tune in a major key: twangy spring-reverb electric "
                   "guitar melody, walking bass and light brushed drums, sunny and upbeat Western rock and roll, "
                   "played back through a tiny cassette-player speaker, lo-fi, instrumental, no vocals, no exotic scales")


def load_refs():
    refs = {}
    procedural = RAWM / "procedural"
    if (procedural / "MOTIF_REF_72bpm.wav").exists():
        refs["MOTIF_REF"] = gen.upload(procedural / "MOTIF_REF_72bpm.wav")
    sel = json.loads(SEL.read_text()) if SEL.exists() else {}
    if (RAWM / "REF_PALETTE.wav").exists():
        refs["REF_PALETTE"] = gen.upload(RAWM / "REF_PALETTE.wav")
    for k in ("MUS_02_MARKET_LO", "MUS_04_STATION_LOW"):
        if k in sel and pathlib.Path(sel[k]["ref_file"]).exists():
            refs[k] = gen.upload(sel[k]["ref_file"])
    return refs, sel


def build(stage, takes=None):
    refs, sel = load_refs()
    jobs = []
    for cue, (st, tk) in plans(refs, sel).items():
        if st != stage:
            continue
        for take, seed, chunks in tk:
            if takes and take not in takes:
                continue
            missing = [c for c in chunks if "audio_reference" in c and not c["audio_reference"]["audio_url"]]
            if missing:
                raise SystemExit(f"{cue}: reference missing for stage {stage}; run the previous stage and select first")
            ms = sum(c["duration_ms"] for c in chunks)
            args = {"composition_plan": {"chunks": chunks}, "seed": seed, "output_format": "pcm_48000"}
            jobs.append(dict(id=cue, take=take, endpoint="elevenlabs/music/v2.5", args=args, est_usd=gen.est_el_music(ms),
                             out=f"RawAssets/audio/music/{cue}/{take}"))
    if stage == 2:
        for take in ("take_a", "take_b"):
            if takes and take not in takes:
                continue
            args = {"prompt": CASSETTE_PROMPT, "music_length_ms": 30000, "force_instrumental": True, "output_format": "pcm_48000"}
            jobs.append(dict(id="AMB_CASSETTE_TUNE", take=take, endpoint="elevenlabs/music/v2.5", args=args,
                             est_usd=gen.est_el_music(30000), out=f"RawAssets/audio/music/AMB_CASSETTE_TUNE/{take}"))
    return jobs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--stage", type=int, required=True)
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--takes")
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    jobs = build(a.stage, set(a.takes.split(",")) if a.takes else None)
    print(f"stage {a.stage}: {len(jobs)} jobs, est ${sum(j['est_usd'] for j in jobs):.2f}")
    if a.dry_run:
        for j in jobs:
            print(j["id"], j["take"], j["est_usd"], json.dumps(j["args"])[:300])
        return
    gen.run_jobs(jobs, workers=6, force=a.force)


if __name__ == "__main__":
    main()


# ---- resume pass: MUS_02_MARKET_HI retakes locked to the LO take's real grid (LO plays 143.6 BPM on the 72 BPM
# palette grid; the first HI takes drifted to 159 and 128 BPM, which cannot be bar-synced with LO).
def hi_retake_jobs():
    refs, sel = load_refs()
    lo = refs.get("MUS_02_MARKET_LO")
    base = PALETTE + ["1980s Cold War action film score", "144 BPM", "double-time drums", "sequenced analog bass arpeggio",
                      "gated toms", "string machine pads", "driving percussion", "synth brass stabs", "high intensity",
                      "no intro"]
    jobs = []
    for take, seed, strength in (("take_c", 2021, "xhigh"), ("take_d", 2022, "high")):
        chunks = [
            ch("[Main]\n{the same groove as the reference at 144 BPM, now with driving percussion and synth-brass stabs}",
               30000, base, neg=["slow intro", "tempo change"], ref=lo, strength=strength),
            ch("[Main continues]\n{same groove, same tempo, constant intensity}", 27000, base + ["constant intensity"],
               neg=["tempo change", "breakdown"], adherence="high"),
            ch("[Continue]\n{the groove continues}", 3000, base + ["no ending", "no fade"], adherence="high"),
        ]
        args = {"composition_plan": {"chunks": chunks}, "seed": seed, "output_format": "pcm_48000"}
        jobs.append(dict(id="MUS_02_MARKET_HI", take=take, endpoint="elevenlabs/music/v2.5", args=args,
                         est_usd=gen.est_el_music(60000), out=f"RawAssets/audio/music/MUS_02_MARKET_HI/{take}"))
    return jobs
