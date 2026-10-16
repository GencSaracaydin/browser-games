"""Measure every raw WP06_SFX candidate and join the cached machine-listen answers.

Writes RawAssets/audio/sfx/_qa/scan.json: {relpath: {id, take, prompt, meas, n_transients, listen{...}}}
usage: python scan.py [--ids A,B] [--workers 4]
"""
from __future__ import annotations

import argparse
import concurrent.futures as cf
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import alib as A  # noqa: E402
import gen  # noqa: E402
import listen  # noqa: E402

ROOT = gen.ROOT
RAW = ROOT / "RawAssets/audio/sfx"
OUT = RAW / "_qa/scan.json"


def listen_index():
    """source path -> list of (prompt, answer) from the listen cache."""
    idx = {}
    for d in (RAW / "_qa/listen").iterdir():
        s, r, q = d / "source.txt", d / "result.json", d / "request.json"
        if not (s.exists() and r.exists()):
            continue
        try:
            src = s.read_text().strip()
            ans = json.loads(r.read_text()).get("output", "")
            pr = json.loads(q.read_text())["arguments"]["prompt"]
        except Exception:
            continue
        idx.setdefault(src, []).append((pr, ans))
    return idx


def candidates(ids=None):
    out = []
    for d in sorted(RAW.iterdir()):
        if not d.is_dir() or d.name.startswith("_"):
            continue
        if ids and d.name not in ids:
            continue
        for take in sorted(d.iterdir()):
            if not take.is_dir() or not gen.done(take):
                continue
            req = json.loads((take / "request.json").read_text())
            args = req["arguments"]
            desc = args.get("prompt") or args.get("text") or ""
            for f in gen.files_of(take):
                out.append((d.name, take.name, str(f), desc))
    return out


def one(c):
    aid, take, f, desc = c
    x, sr = A.load(f)
    m = A.measure(x)
    r = dict(id=aid, take=take, file=f, prompt=desc, meas=m)
    if aid.endswith("_SHOT") or aid.startswith(("SFX_BULLET", "SFX_IMPACT", "SFX_EXP")):
        r["n_transients"] = A.count_transients(x)
    if aid.startswith(("SFX_IMPACT", "SFX_FS_", "SFX_WPN_SHELLS", "SFX_GREN_BOUNCE", "SFX_MOVE_LAND", "SFX_BODYFALL",
                       "SFX_SPARKS", "SFX_PEN_TAP", "SFX_MOVE_LADDER")):
        r["n_events"] = len(A.detect_events(A.mono(x)))
    return f, r


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids")
    ap.add_argument("--workers", type=int, default=4)
    a = ap.parse_args()
    ids = set(a.ids.split(",")) if a.ids else None
    li = listen_index()
    prev = json.loads(OUT.read_text()) if OUT.exists() else {}
    cs = candidates(ids)
    with cf.ProcessPoolExecutor(max_workers=a.workers) as ex:
        for f, r in ex.map(one, cs, chunksize=4):
            ans = li.get(f, [])
            r["listen"] = []
            for pr, t in ans:
                sc = listen.parse_scores(t)
                kind = "guardrail" if pr.startswith("You are auditing") else "match"
                r["listen"].append(dict(kind=kind, text=t, **({} if kind == "guardrail" else sc),
                                        **({"yes": listen.yes_answers(t)} if kind == "guardrail" else {})))
            prev[str(pathlib.Path(f).relative_to(ROOT))] = r
    A.write_json(OUT, prev)
    print(len(cs), "files scanned ->", OUT)


if __name__ == "__main__":
    main()
