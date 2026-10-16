#!/usr/bin/env python3
"""Copy content agents' import-ready outputs into the project (ORCHESTRATION §6, WP00 Phase B).

Content agents write RawAssets/<wp>/ready/Assets/... mirroring the final Unity paths. This copies those files into
Assets/ for the editor holder, safely:

* only into Assets/_Game/ and Assets/StreamingAssets/video/ (anything else in a ready tree is reported, not copied);
* never copies .meta files (Unity creates them; existing metas and GUIDs are kept);
* identical files are skipped; a target that exists with different content is a conflict: reported and left alone
  unless --overwrite (Unity reimports it in place, GUID kept);
* --validate runs Tools/build/validate_assets.py on the same ready trees first and refuses to copy when it reports
  errors (unless --force);
* every run appends to Logs/copy_ready.txt.

Usage (project root, system python3):
  python3 Tools/build/copy_ready.py --list                        # ready trees, file counts, pending/conflicts
  python3 Tools/build/copy_ready.py wp10 audio --dry-run          # plan only
  python3 Tools/build/copy_ready.py wp10 --only Art/Textures/Env --only Art/Materials
  python3 Tools/build/copy_ready.py --all --validate
  options: --overwrite, --force, --dry-run

After copying, in the editor (editor holder only): AssetDatabase.Refresh() (the import rules apply), then
LongShadow/Materials/Build From Ready Specs, LongShadow/Materials/Bind All Model Materials,
LongShadow/Import/Check Rules (Report). Import big batches area by area: crunch compression is slow.
Exit codes: 0 ok, 1 conflicts left or validation errors, 2 usage problem.
"""
from __future__ import annotations

import argparse
import hashlib
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ALLOWED = ("Assets/_Game/", "Assets/StreamingAssets/video/")
LOG = ROOT / "Logs/copy_ready.txt"


def ready_trees():
    raw = ROOT / "RawAssets"
    return sorted(p.name for p in raw.iterdir() if (p / "ready/Assets").is_dir()) if raw.is_dir() else []


def digest(path: Path):
    h = hashlib.sha1()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def plan(wp: str, only):
    base = ROOT / "RawAssets" / wp / "ready"
    rows = []  # (status, src, target)
    for dirpath, _, files in os.walk(base / "Assets"):
        for fn in sorted(files):
            if fn.endswith(".meta") or fn.startswith("."):
                continue
            src = Path(dirpath) / fn
            target = src.relative_to(base).as_posix()  # "Assets/_Game/..."
            if only and not any(target.startswith("Assets/_Game/" + o.strip("/") + "/") or target.startswith(o.strip("/") + "/")
                                for o in only):
                continue
            if not target.startswith(ALLOWED):
                rows.append(("outside", src, target))
                continue
            dst = ROOT / target
            if not dst.exists():
                rows.append(("new", src, target))
            elif dst.stat().st_size == src.stat().st_size and digest(dst) == digest(src):
                rows.append(("same", src, target))
            else:
                rows.append(("conflict", src, target))
    return rows


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("wps", nargs="*", help="ready trees to copy (folder names under RawAssets/, e.g. wp10 audio)")
    ap.add_argument("--all", action="store_true", help="every ready tree")
    ap.add_argument("--list", action="store_true", help="list ready trees with new/same/conflict counts and exit")
    ap.add_argument("--only", action="append", help="limit to a sub-path below Assets/_Game (e.g. Art/Textures/Env); repeatable")
    ap.add_argument("--dry-run", action="store_true", help="print the plan, copy nothing")
    ap.add_argument("--overwrite", action="store_true", help="replace targets whose content differs")
    ap.add_argument("--validate", action="store_true", help="run validate_assets.py on the ready trees first")
    ap.add_argument("--force", action="store_true", help="copy even when validation reports errors")
    args = ap.parse_args(argv)

    trees = ready_trees()
    if args.list:
        print(f"{'tree':<10} {'files':>6} {'new':>6} {'same':>6} {'conflict':>8} {'outside':>8}")
        for wp in trees:
            rows = plan(wp, args.only)
            c = {k: sum(1 for r in rows if r[0] == k) for k in ("new", "same", "conflict", "outside")}
            print(f"{wp:<10} {len(rows):>6} {c['new']:>6} {c['same']:>6} {c['conflict']:>8} {c['outside']:>8}")
        return 0
    wps = trees if args.all else args.wps
    if not wps:
        ap.print_usage()
        print("copy_ready: name ready trees or pass --all (--list shows them)", file=sys.stderr)
        return 2
    for wp in wps:
        if wp not in trees:
            print(f"copy_ready: no ready tree RawAssets/{wp}/ready/Assets", file=sys.stderr)
            return 2

    if args.validate:
        cmd = [sys.executable, str(ROOT / "Tools/build/validate_assets.py"), "--quiet", "--out", "Logs/validate_assets_copy.txt"]
        for wp in wps:
            cmd += ["--ready", wp]
        rc = subprocess.call(cmd, cwd=ROOT)
        if rc == 1 and not args.force:
            print("copy_ready: validation reported errors (Logs/validate_assets_copy.txt); fix them or pass --force")
            return 1
        if rc == 2:
            return 2

    total = {"new": 0, "same": 0, "conflict": 0, "outside": 0, "overwritten": 0}
    log = [f"## {time.strftime('%Y-%m-%d %H:%M:%S')} copy_ready {' '.join(wps)}{' --only ' + ','.join(args.only) if args.only else ''}"
           f"{' (dry run)' if args.dry_run else ''}"]
    for wp in wps:
        for status, src, target in plan(wp, args.only):
            dst = ROOT / target
            if status == "same":
                total["same"] += 1
                continue
            if status == "outside":
                total["outside"] += 1
                log.append(f"  OUTSIDE   {target}  (not copied: only {', '.join(ALLOWED)})")
                continue
            if status == "conflict" and not args.overwrite:
                total["conflict"] += 1
                log.append(f"  CONFLICT  {target}  (differs from RawAssets/{wp}/ready; --overwrite replaces it)")
                continue
            if status == "conflict":
                total["overwritten"] += 1
            else:
                total["new"] += 1
            log.append(f"  {'OVERWRITE' if status == 'conflict' else 'NEW':<9} {target}  <- RawAssets/{wp}/ready")
            if not args.dry_run:
                dst.parent.mkdir(parents=True, exist_ok=True)
                tmp = dst.with_name(dst.name + ".copy_ready_tmp")
                shutil.copy2(src, tmp)
                os.replace(tmp, dst)
    summary = (f"  => {total['new']} new, {total['overwritten']} overwritten, {total['same']} identical, "
               f"{total['conflict']} conflicts left, {total['outside']} outside the allowed roots")
    log.append(summary)
    LOG.parent.mkdir(parents=True, exist_ok=True)
    with open(LOG, "a", encoding="utf-8") as f:
        f.write("\n".join(log) + "\n")
    for line in log if (args.dry_run or len(log) < 60) else log[:1] + [l for l in log if "CONFLICT" in l or "OUTSIDE" in l][:40] + [summary]:
        print(line)
    if not args.dry_run and (total["new"] or total["overwritten"]):
        print("next (editor holder): AssetDatabase.Refresh(); LongShadow/Materials/Build From Ready Specs; "
              "LongShadow/Materials/Bind All Model Materials; LongShadow/Import/Check Rules (Report)")
    return 1 if total["conflict"] else 0


if __name__ == "__main__":
    sys.exit(main())
