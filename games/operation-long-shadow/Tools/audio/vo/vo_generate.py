#!/usr/bin/env python3
"""TTS takes for every DIALOGUE.json line (and the Clockmaker tokens) through Tools/fal/falcall.py.

  python vo_generate.py lines  [--ids D001,D002] [--voice vo_hollis] [--takes 3] [--dry-run] [--workers 6]
  python vo_generate.py tokens [--takes 3]
  python vo_generate.py show   [--ids ...]          # print the exact request text per line (no calls)

Outputs: RawAssets/audio/vo/raw/<key>/take_<n>/ (request.json, result.json, audio file) where key is the
line id, or <id>_A / <id>_B for Legion barks; tokens go to RawAssets/audio/vo/tokens/<token>/take_<n>/.
Idempotent: a take whose result.json + audio file exist is skipped. Exit code 2 from falcall (cap) stops the run.
"""
import argparse, concurrent.futures as cf, json, math, re, subprocess, sys, threading
from common import (ROOT, RAWDIR, TOKENS, FAL_PY, FALCALL, WP_TAG, load_dialogue, load_casting, load_manifest)

PROCEDURAL = {"D039", "D066", "D080", "D092"}

# ------------------------------------------------------------------ delivery -> acting direction
CHAIN_WORDS = [
    r"SCRAMBLER chain[^;]*", r"TAPE chain[^;]*", r"PA HORN chain[^;]*", r"HF/AM chain[^;]*", r"CLEAN[^;,]*",
    r"bake dry \+ radio variants \(distance crossfade\)", r"record voices A and B", r"radio \+ rotor bed",
    r"\(scrambler toggled off\)", r"\(same take family as the briefing tape\)", r"dry booth", r"radio if > 15 m",
]
OVERRIDES = {  # hand-written acting notes where the DIALOGUE.json delivery mixes in chain/staging notes
    "D014": "whispering, low and calm",
    "D015": "whispering, low, metronomic countdown",
    "D020": "wry, straining with effort",
    "D023": "shouting, with a nervous laugh",
    "D029": "shouting, then clipped urgent orders",
    "D030": "strained, urgent, speaking into a radio handset",
    "D031": "low, speaking quietly into a radio handset",
    "D041": "firm, then suddenly concerned",
    "D043": "low, calm",
    "D051": "very low, quiet, shaken",
    "D054": "whispering, very close to the ear",
    "D055": "urgent, insistent",
    "D068": "calm, professional, speaking into a radio handset",
    "D069": "calm, bored airline-captain voice",
    "D070": "dry, calm, speaking into a radio handset",
    "D074": "calm, matter-of-fact",
    "D075": "calm but clipped, quick",
    "D076": "shouting over loud helicopter rotor wash",
    "D077": "calm, matter-of-fact, speaking into a headset intercom",
    "D078": "shouting over helicopter rotor noise into a headset",
    "D079": "slowing down, scared, quiet",
    "D081": "urgent but controlled, quick",
    "D082": "confused, then afraid, trailing off",
    "D087": "slow, evenly spaced digits, soft",
    "D090": "soft, quiet, final",
    "D091": "quiet, matter-of-fact, speaking into a desk telephone",
    "D093": "shouting, angry and alarmed",
}


def acting(line):
    if line["id"] in OVERRIDES:
        return OVERRIDES[line["id"]]
    d = line["delivery"].split(";")[0]
    for pat in CHAIN_WORDS:
        d = re.sub(pat, "", d)
    for acc in ("West Texas drawl", "Texas drawl", "Cajun tenor", "Cajun"):
        d = d.replace(acc, "")  # identity comes from the preset voice; accent words in tags risk caricature
    d = d.replace("whispered-low", "whispering, low").replace("whispered", "whispering").replace("shouted", "shouting")
    d = re.sub(r"\bshouting, combat\b", "shouting a combat callout", d)
    d = re.sub(r"\bradio\b", "speaking into a radio handset", d)
    d = re.sub(r"\bintercom\b", "speaking into a headset intercom", d)
    d = re.sub(r"\s+,", ",", d)
    d = re.sub(r",\s*,", ",", d).strip(" ,")
    return d


BARK_STYLE_EL = "shouting"

# TTS-only pronunciation respellings for invented words, by (line, variant) -> {first take number: spoken text}.
# The lexicon spelling in DIALOGUE.json stays; this only steers how the voice says it. Used when every take of the
# lexicon spelling is heard as a real word (sound-alike guardrail in vo_verify.py).
RESPELL = {
    ("BK_LEG_07", "A"): {13: "Mehn-nik!", 16: "Menneek!"},  # George says 'Mennik' as 'Manic!'/'Medic!' in 12/12 takes
}


def spoken_text(line, variant, take):
    r = RESPELL.get((line["id"], variant))
    if not r:
        return line["text"]
    starts = [t for t in sorted(r) if t <= take]
    return r[starts[-1]] if starts else line["text"]


def build_requests(line, cast, variant=None):
    """Return (endpoint, args_without_seed, seed_base or None)."""
    vid = line["voice_id"]
    c = cast[vid]
    text = line["text"]
    if c["engine"] == "el":
        if vid == "vo_legion":
            v = c["variants"][variant]
            voice, seed = v["voice"], v["seed"]
            tag = c["base_tag"]
            if line["id"] in ("BK_LEG_13",):
                tag = "shouting in sudden pain"
            if line["id"] in ("BK_LEG_14", "BK_LEG_15"):
                tag = "casual, relaxed, loud enough to carry"  # the light-for-a-cigarette exchange is not combat
        else:
            voice, seed = c["voice"], c["seed"]
            tag = acting(line)
        args = {"text": f"[{tag}] {text}", "voice": voice, "stability": c["stability"],
                "similarity_boost": c["similarity_boost"], "output_format": "pcm_48000"}
        if c.get("language_code"):
            args["language_code"] = c["language_code"]
        return c["endpoint"], args, seed
    style = c["style"]
    if vid == "vo_greycoat":
        pass
    elif vid == "vo_harrou" and line["id"] == "D012":
        style += " This announcement is in an invented local language; pronounce every word exactly as spelled, with the same calm authority."
    else:
        style += f" Delivery for this line: {acting(line)}."
    return c["endpoint"], {"prompt": text, "style_instructions": style, "voice": c["voice"]}, None


def est_usd(endpoint, args):
    if "eleven" in endpoint:
        n = len(args["text"]) * 0.08 / 1000
    else:
        n = (len(args["prompt"]) + len(args.get("style_instructions", ""))) * 0.045 / 1000
    return max(0.0005, math.ceil(n * 10000) / 10000)


def take_done(out):
    t = out / "timing.json"
    if not t.exists():
        return False
    files = json.loads(t.read_text()).get("files", [])
    return any(f.endswith((".pcm", ".wav", ".mp3")) for f in files)


STOP = threading.Event()
LOCK = threading.Lock()


def run_job(job):
    key, tid, endpoint, args, usd, out = job
    if STOP.is_set():
        return key, tid, "skipped"
    out.mkdir(parents=True, exist_ok=True)
    (out / "args.json").write_text(json.dumps(args, indent=1))
    cmd = [FAL_PY, str(FALCALL), "--wp", WP_TAG, "--id", f"VO_{key}_t{tid}", "--endpoint", endpoint,
           "--args", str(out / "args.json"), "--est-usd", f"{usd:.4f}", "--out", str(out)]
    for attempt in range(3):
        p = subprocess.run(cmd, cwd=str(ROOT), capture_output=True, text=True)
        if p.returncode == 2:
            STOP.set()
            return key, tid, "CAP"
        if p.returncode == 0 and take_done(out):
            return key, tid, "ok"
        err = (p.stderr or "")[-300:]
        with LOCK:
            print(f"  retry {key} t{tid} rc={p.returncode} {err.strip()[:200]}", flush=True)
    return key, tid, "fail"


def run(jobs, workers, dry):
    total = sum(j[4] for j in jobs)
    print(f"{len(jobs)} calls, est ${total:.3f}")
    if dry:
        for j in jobs[:400]:
            print(j[0], j[1], j[2], json.dumps(j[3])[:260])
        return
    res = {}
    with cf.ThreadPoolExecutor(workers) as ex:
        for k, t, s in ex.map(run_job, jobs):
            res.setdefault(s, []).append(f"{k}:t{t}")
            if s != "ok":
                print("  ", k, t, s, flush=True)
    print({k: len(v) for k, v in res.items()})
    if STOP.is_set():
        print("CAP REACHED: stopped", file=sys.stderr)
        sys.exit(2)


def line_jobs(ids, voice, takes, start, only_variant=None):
    dlg, cast = load_dialogue(), load_casting()
    man = load_manifest()
    jobs = []
    for lid, line in dlg.items():
        if lid in PROCEDURAL or (ids and lid not in ids) or (voice and line["voice_id"] != voice):
            continue
        if "VO_" + lid not in man:
            continue
        variants = ["A", "B"] if line["voice_id"] == "vo_legion" else [None]
        for v in variants:
            if only_variant and v != only_variant:
                continue
            key = lid if v is None else f"{lid}_{v}"
            endpoint, args, seed = build_requests(line, cast, v)
            for t in range(start, takes + 1):
                out = RAWDIR / key / f"take_{t}"
                if take_done(out):
                    continue
                a = dict(args)
                st = spoken_text(line, v, t)
                if st != line["text"] and "text" in a:
                    a["text"] = a["text"].replace(line["text"], st)
                if seed is not None:
                    a["seed"] = seed + 1000 * (t - 1)
                jobs.append((key, t, endpoint, a, est_usd(endpoint, a), out))
    return jobs


def token_jobs(takes, only=None, start=1):
    cast = load_casting()
    c = cast["vo_clockmaker"]
    jobs = []
    for tok, prompt in cast["tokens"].items():
        if tok.startswith("_") or (only and tok not in only):
            continue
        style = c["style"] + cast["token_style_extra"].get(tok, " Reading a single digit.")
        for t in range(start, takes + 1):
            st = style
            if t >= 4:  # retakes: the flat style drew long tokens out past the 0.9 s grid slot
                st += " Say it briskly and crisply, the whole word in well under one second, like a machine with no pause."
            args = {"prompt": prompt, "style_instructions": st, "voice": c["voice"]}
            out = TOKENS / tok / f"take_{t}"
            if not take_done(out):
                jobs.append((f"TOK_{tok}", t, c["endpoint"], args, est_usd(c["endpoint"], args), out))
    return jobs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["lines", "tokens", "show"])
    ap.add_argument("--ids", default="")
    ap.add_argument("--voice", default="")
    ap.add_argument("--takes", type=int, default=3)
    ap.add_argument("--start", type=int, default=1, help="first take number to (re)generate")
    ap.add_argument("--workers", type=int, default=6)
    ap.add_argument("--variant", default="", help="Legion barks: only this voice variant (A or B)")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()
    ids = set(filter(None, a.ids.split(",")))
    if a.mode == "show":
        dlg, cast = load_dialogue(), load_casting()
        for lid, line in dlg.items():
            if lid in PROCEDURAL or (ids and lid not in ids) or (a.voice and line["voice_id"] != a.voice):
                continue
            for v in (["A", "B"] if line["voice_id"] == "vo_legion" else [None]):
                ep, args, seed = build_requests(line, cast, v)
                print(lid, v or "", ep.split("/")[0], "|", args.get("text") or (args["prompt"] + "  <<" + args["style_instructions"][-90:] + ">>"))
        return
    jobs = line_jobs(ids, a.voice, a.takes, a.start, a.variant or None) if a.mode == "lines" else token_jobs(a.takes, ids or None, a.start)
    run(jobs, a.workers, a.dry_run)


if __name__ == "__main__":
    main()
