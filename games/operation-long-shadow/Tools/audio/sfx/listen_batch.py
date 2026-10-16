"""Batch machine-listen every raw SFX candidate (match/realism/unwanted) and every ambience bed (guardrail prompt).

Results are cached per file+prompt; a summary lands in RawAssets/audio/sfx/_qa/listen_summary.json, keyed by file path.
usage: python listen_batch.py [--ids A,B] [--workers 8]
"""
from __future__ import annotations

import argparse
import concurrent.futures as cf
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import gen  # noqa: E402
import listen  # noqa: E402

ROOT = gen.ROOT
RAW = ROOT / "RawAssets/audio/sfx"
SUMMARY = RAW / "_qa/listen_summary.json"


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
                out.append((d.name, take.name, f, desc))
    return out


def kind_for(asset_id):
    if asset_id.startswith("AMB_"):
        return "ambience"
    if asset_id.endswith("_SHOT"):
        return "gunshot"
    return "sfx"


def run(ids=None, workers=8):
    summ = json.loads(SUMMARY.read_text()) if SUMMARY.exists() else {}
    jobs = candidates(ids)

    def one(j):
        aid, take, f, desc = j
        k = kind_for(aid)
        res = {}
        txt = listen.listen(f, k, prompt=listen.match_prompt(desc[:400], "gunshot" if k == "gunshot" else "sfx"))
        res["match_text"] = txt
        res.update(listen.parse_scores(txt))
        if k == "ambience":
            g = listen.listen(f, "ambience")
            res["guardrail_text"] = g
            res["guardrail_yes"] = listen.yes_answers(g)
        return str(f), dict(id=aid, take=take, **res)

    with cf.ThreadPoolExecutor(max_workers=workers) as ex:
        for k, v in ex.map(one, jobs):
            summ[k] = v
    SUMMARY.parent.mkdir(parents=True, exist_ok=True)
    SUMMARY.write_text(json.dumps(summ, indent=1))
    return summ


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids")
    ap.add_argument("--workers", type=int, default=8)
    a = ap.parse_args()
    ids = set(a.ids.split(",")) if a.ids else None
    print(len(candidates(ids)), "candidate files")
    s = run(ids, a.workers)
    print(len(s), "summarised")
