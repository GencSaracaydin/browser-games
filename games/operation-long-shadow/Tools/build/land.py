#!/usr/bin/env python3
"""Lands staged code into Assets/ behind the compile gate (TECH_PLAN D18, §10.1, §11.2).

    Staging/<System path>/**  ->  Assets/_Game/Scripts/<System path>/**
    Staging/Tests/**          ->  Assets/_Game/Tests/**            (with the system they belong to)
    Staging/Assets/**         ->  Assets/**                        (non-code assets, only with --assets)

A system lands only if ``compile_check.py`` is green for it against the current landed state
(player + editor passes), including every landed assembly that depends on it (so a Core
change can never break a landed system). Warnings in the staged files block landing unless
``--allow-warnings``.

Rules:
* Only ``.cs`` and ``.asmdef`` files are landed from code staging; other files are reported and ignored.
* Staged ``.asmdef`` files may only replace an existing asmdef of the same name and may only
  reference ``LongShadow.*`` assemblies allowed by TECH_PLAN §4.3 (see ls_layout.py).
* Unchanged files are not rewritten (no needless reimports). Writes are atomic.
* land.py never deletes a file it did not create. ``DELETE.txt`` in a staged folder lists
  paths (relative to that folder) to remove; only files recorded in the landing ledger
  (``Tools/build/landed_files.json``) are removed, together with their ``.meta``.
* Every attempt is appended to ``Docs/LANDING_LOG.md``.
* Staging is never modified: keep editing there and land again.

Usage:
    python3 Tools/build/land.py Player --wp WP02
    python3 Tools/build/land.py World.Look --wp WP10 --dry-run
    python3 Tools/build/land.py --all --wp WP00          # every system with staged changes, dependency order
    python3 Tools/build/land.py --assets --wp WP01        # Staging/Assets/** (no compile gate)
    python3 Tools/build/land.py --status                  # what differs between Staging and Assets

After landing, the editor holder must refresh the AssetDatabase (Unity_RunCommand:
AssetDatabase.Refresh()) and check the console. Exit codes: 0 ok / nothing to do, 1 a system
failed its gate, 2 configuration error.
"""
from __future__ import annotations

import argparse
import contextlib
import datetime as dt
import fcntl
import filecmp
import json
import os
import sys
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import compile_check as cc  # noqa: E402
import ls_layout as L  # noqa: E402

ROOT = cc.PROJECT_ROOT
STAGING = cc.DEFAULT_STAGING
LEDGER = HERE / "landed_files.json"
LOCK = HERE / ".land.lock"
LOG = ROOT / "Docs" / "LANDING_LOG.md"
CODE_EXT = {".cs", ".asmdef"}

LOG_HEADER = """# Landing log

Appended by `Tools/build/land.py` (one row per attempt). Newest at the bottom.

| When | WP | System | Result | Files (+new ~changed -deleted) | Gate | Notes |
|---|---|---|---|---|---|---|
"""


# --------------------------------------------------------------------------------------
# helpers
# --------------------------------------------------------------------------------------


@contextlib.contextmanager
def land_lock():
    LOCK.touch(exist_ok=True)
    with open(LOCK, "r+") as fh:
        fcntl.flock(fh, fcntl.LOCK_EX)
        try:
            yield
        finally:
            fcntl.flock(fh, fcntl.LOCK_UN)


def load_ledger() -> Dict[str, List[str]]:
    try:
        return json.loads(LEDGER.read_text())
    except (OSError, ValueError):
        return {}


def save_ledger(ledger: Dict[str, List[str]]) -> None:
    tmp = LEDGER.with_suffix(".tmp")
    tmp.write_text(json.dumps({k: sorted(set(v)) for k, v in sorted(ledger.items())}, indent=1) + "\n")
    os.replace(tmp, LEDGER)


DRY_RUN = False


def append_log(rows: List[str]) -> None:
    if DRY_RUN:
        return
    LOG.parent.mkdir(parents=True, exist_ok=True)
    if not LOG.exists() or LOG.stat().st_size == 0:
        LOG.write_text(LOG_HEADER)
    with open(LOG, "a") as fh:
        for r in rows:
            fh.write(r + "\n")


def md(s: str) -> str:
    return s.replace("|", "\\|").replace("\n", " ")


def now() -> str:
    return dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def atomic_copy(src: Path, dst: Path) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    tmp = dst.parent / ("." + dst.name + ".landtmp")  # dot-files are ignored by Unity
    tmp.write_bytes(src.read_bytes())
    os.replace(tmp, dst)


def system_order(systems: Set[str]) -> List[str]:
    order = ["Core", "Input"] + [s for s in L.SYSTEMS if s not in ("Core", "Input", "Integration")] + ["Integration"]
    known = [s for s in order if s in systems]
    return known + sorted(s for s in systems if s not in order)


# --------------------------------------------------------------------------------------
# planning
# --------------------------------------------------------------------------------------


class Plan:
    def __init__(self, system: str):
        self.system = system
        self.copies: List[Tuple[Path, str]] = []  # (staged file, landed rel path)
        self.unchanged: List[str] = []
        self.deletes: List[str] = []
        self.refused_deletes: List[str] = []
        self.ignored: List[str] = []
        self.problems: List[str] = []

    @property
    def new(self) -> List[str]:
        return [d for _, d in self.copies if not (ROOT / d).exists()]

    @property
    def changed(self) -> List[str]:
        return [d for _, d in self.copies if (ROOT / d).exists()]

    @property
    def empty(self) -> bool:
        return not self.copies and not self.deletes


def make_plans(project: "cc.Project", systems: List[str], ledger: Dict[str, List[str]]) -> Dict[str, Plan]:
    plans: Dict[str, Plan] = {}
    for s in systems:
        p = Plan(s)
        for v, real in project.staged_by_system.get(s, []):
            if real.name == "DELETE.txt":
                base = str(Path(v).parent.as_posix())
                for rel in sorted(cc.read_delete_list(real.parent)):
                    target = base + "/" + rel
                    if not (ROOT / target).exists():
                        continue
                    if target in ledger.get(s, []):
                        p.deletes.append(target)
                    else:
                        p.refused_deletes.append(target)
                continue
            if real.suffix not in CODE_EXT:
                p.ignored.append(cc.rel(real))
                continue
            dst = ROOT / v
            if dst.exists() and filecmp.cmp(real, dst, shallow=False):
                p.unchanged.append(v)
                continue
            p.copies.append((real, v))
        # report non-code files in the system's staging folders
        for folder in cc.staging_dirs_for_system(STAGING, s):
            if folder.is_dir():
                for f in folder.rglob("*"):
                    if f.is_file() and f.suffix not in CODE_EXT and f.name not in ("DELETE.txt", ".gitkeep", ".DS_Store") \
                            and cc.rel(f) not in p.ignored and not f.name.endswith(".meta"):
                        p.ignored.append(cc.rel(f))
        plans[s] = p
    return plans


def validate_asmdefs(plan: Plan) -> None:
    for real, v in plan.copies:
        if real.suffix != ".asmdef":
            continue
        try:
            data = cc.load_json_lenient(real)
        except (OSError, ValueError) as e:
            plan.problems.append(f"{cc.rel(real)}: invalid JSON ({e})")
            continue
        landed = ROOT / v
        if not landed.exists():
            plan.problems.append(f"{cc.rel(real)}: new asmdefs are not landed automatically; file a request in Docs/REQUESTS.md")
            continue
        try:
            old = cc.load_json_lenient(landed)
        except (OSError, ValueError):
            old = {}
        name = data.get("name")
        if name != old.get("name"):
            plan.problems.append(f"{cc.rel(real)}: assembly name must stay '{old.get('name')}' (got '{name}')")
            continue
        allowed = set(L.allowed_our_refs(name))
        ours = [r for r in data.get("references", []) if r.startswith(cc.OUR_PREFIX)]
        bad = [r for r in ours if r not in allowed]
        if bad:
            plan.problems.append(f"{cc.rel(real)}: references not allowed by TECH_PLAN §4.3: {', '.join(bad)} "
                                 f"(allowed: {', '.join(sorted(allowed)) or 'none'})")
        guid_refs = [r for r in data.get("references", []) if r.startswith("GUID:")]
        if guid_refs:
            plan.problems.append(f"{cc.rel(real)}: use assembly names, not GUID references ({', '.join(guid_refs)})")


# --------------------------------------------------------------------------------------
# landing
# --------------------------------------------------------------------------------------


def gate(system: str, allow_warnings: bool, staged_paths: Set[str]) -> Tuple[bool, dict, List[str]]:
    report = cc.run_check([system], False, "targets", ["player", "editor"], STAGING, True, False, 4, True, False, False)
    msgs = list(report["problems"])
    errors = [d for d in report["diagnostics"] if d.severity == "error"]
    warnings = [d for d in report["diagnostics"] if d.severity == "warning" and d.file in staged_paths]
    ok = report["ok"]
    if ok and warnings and not allow_warnings:
        ok = False
        msgs.append(f"{len(warnings)} warning(s) in staged files (use --allow-warnings to land anyway)")
    for d in errors[:40] + warnings[:20]:
        msgs.append(d.format())
    return ok, report, msgs


def land_one(plan: Plan, args, ledger: Dict[str, List[str]]) -> bool:
    s = plan.system
    staged_paths = {cc.rel(real) for real, _ in plan.copies}
    validate_asmdefs(plan)
    if plan.problems:
        for p in plan.problems:
            print(f"  [{s}] config: {p}")
        append_log([f"| {now()} | {args.wp} | {s} | REFUSED | +{len(plan.new)} ~{len(plan.changed)} -{len(plan.deletes)} | - | {md('; '.join(plan.problems))[:400]} |"])
        return False
    ok, report, msgs = gate(s, args.allow_warnings, staged_paths)
    gate_txt = f"{len(report['assemblies'])} asm, {report['seconds']:.1f}s"
    for m in msgs:
        print(f"  [{s}] {m}")
    if not ok:
        first = next((m for m in msgs if " error " in m or "config" in m or "warning" in m), msgs[0] if msgs else "red")
        append_log([f"| {now()} | {args.wp} | {s} | RED | +{len(plan.new)} ~{len(plan.changed)} -{len(plan.deletes)} | {gate_txt} | {md(first)[:300]} |"])
        print(f"  [{s}] NOT LANDED (compile gate red)")
        return False
    new, changed = plan.new, plan.changed
    if args.dry_run:
        print(f"  [{s}] dry run: would land +{len(new)} ~{len(changed)} -{len(plan.deletes)} (gate green: {gate_txt})")
        return True
    for real, v in plan.copies:
        atomic_copy(real, ROOT / v)
        if v in new:
            ledger.setdefault(s, []).append(v)
    for d in plan.deletes:
        for f in (ROOT / d, ROOT / (d + ".meta")):
            if f.exists():
                f.unlink()
        ledger[s] = [x for x in ledger.get(s, []) if x != d]
    save_ledger(ledger)
    notes = []
    if plan.refused_deletes:
        notes.append("refused deletes (not created by land.py): " + ", ".join(plan.refused_deletes))
    if plan.ignored:
        notes.append(f"{len(plan.ignored)} non-code file(s) ignored")
    files = ", ".join(sorted(new + changed + [d + " (deleted)" for d in plan.deletes]))
    append_log([f"| {now()} | {args.wp} | {s} | LANDED | +{len(new)} ~{len(changed)} -{len(plan.deletes)} | {gate_txt} | "
                f"{md('; '.join(notes) + (' — ' if notes else '') + files)[:900]} |"])
    print(f"  [{s}] LANDED +{len(new)} ~{len(changed)} -{len(plan.deletes)} (gate green: {gate_txt})")
    for n in notes:
        print(f"  [{s}] note: {n}")
    return True


def land_assets(args, ledger: Dict[str, List[str]]) -> int:
    src_root = STAGING / "Assets"
    if not src_root.is_dir():
        print("nothing staged under Staging/Assets")
        return 0
    copied, skipped, refused = [], [], []
    for f in sorted(src_root.rglob("*")):
        if not f.is_file() or f.name in (".gitkeep", ".DS_Store") or f.name.endswith(".meta"):
            continue
        relp = "Assets/" + f.relative_to(src_root).as_posix()
        if relp.startswith(("Assets/_Game/Scripts/", "Assets/_Game/Tests/")) or f.suffix in CODE_EXT:
            refused.append(relp + " (code goes through system staging)")
            continue
        dst = ROOT / relp
        if dst.exists():
            if filecmp.cmp(f, dst, shallow=False):
                skipped.append(relp)
                continue
            if relp not in ledger.get("Assets", []) and not args.overwrite:
                refused.append(relp + " (exists and was not created by land.py; use --overwrite)")
                continue
        if not args.dry_run:
            was_new = not dst.exists()
            atomic_copy(f, dst)
            if was_new:
                ledger.setdefault("Assets", []).append(relp)
        copied.append(relp)
    if not args.dry_run:
        save_ledger(ledger)
        append_log([f"| {now()} | {args.wp} | Assets | {'LANDED' if copied else 'NOOP'} | +{len(copied)} | - | "
                    f"{md(', '.join(copied) + ('; refused: ' + ', '.join(refused) if refused else ''))[:900]} |"])
    for c in copied:
        print(("would copy " if args.dry_run else "copied ") + c)
    for r in refused:
        print("refused " + r)
    print(f"assets: {len(copied)} copied, {len(skipped)} unchanged, {len(refused)} refused")
    return 1 if refused else 0


def cmd_status() -> int:
    project = cc.Project(STAGING, set(), True)
    ledger = load_ledger()
    plans = make_plans(project, system_order(set(project.staged_by_system)), ledger)
    if not plans:
        print("Staging is empty")
    for s, p in plans.items():
        state = "up to date" if p.empty else f"+{len(p.new)} ~{len(p.changed)} -{len(p.deletes)} pending"
        print(f"{s:14} {state}; {len(p.unchanged)} unchanged" + (f"; {len(p.ignored)} ignored" if p.ignored else ""))
        for _, v in p.copies:
            print(f"    {'new ' if not (ROOT / v).exists() else 'mod '} {v}")
        for d in p.deletes:
            print(f"    del  {d}")
    for prob in project.problems:
        print("problem:", prob)
    return 0


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(description="Land Staging/<System> into Assets/ behind the compile gate.")
    ap.add_argument("systems", nargs="*", help="systems to land (Core, Player, World.Look, ...)")
    ap.add_argument("--all", "--pending", dest="all", action="store_true", help="every system with staged changes, dependency order")
    ap.add_argument("--wp", default=os.environ.get("LS_WP", "?"), help="your work package tag for the log (WP02, ...)")
    ap.add_argument("--dry-run", action="store_true", help="run the gate and print the plan; copy nothing")
    ap.add_argument("--allow-warnings", action="store_true", help="land even if staged files have compiler warnings")
    ap.add_argument("--assets", action="store_true", help="land Staging/Assets/** (non-code assets, no compile gate)")
    ap.add_argument("--overwrite", action="store_true", help="with --assets: overwrite files land.py did not create")
    ap.add_argument("--status", action="store_true", help="show pending changes and exit")
    args = ap.parse_args(argv)
    global DRY_RUN
    DRY_RUN = args.dry_run
    if args.status:
        return cmd_status()
    with land_lock():
        ledger = load_ledger()
        if args.assets:
            return land_assets(args, ledger)
        project = cc.Project(STAGING, set(), True)
        for prob in project.problems:
            print("config:", prob)
        if args.all:
            systems = set(project.staged_by_system)
        else:
            if not args.systems:
                ap.error("name at least one system, or use --all / --assets / --status")
            systems = set()
            for s in args.systems:
                exp = project.expand_system(s)
                if not exp:
                    print(f"unknown system '{s}' (known: {', '.join(sorted(project.known_systems))})")
                    return 2
                systems |= exp
        order = system_order(systems)
        plans = make_plans(project, order, ledger)
        results: Dict[str, bool] = {}
        for s in order:
            p = plans[s]
            if p.empty:
                print(f"  [{s}] nothing to land ({len(p.unchanged)} file(s) already identical)")
                if p.refused_deletes:
                    print(f"  [{s}] refused deletes (not created by land.py): {', '.join(p.refused_deletes)}")
                results[s] = True
                continue
            results[s] = land_one(p, args, ledger)
        failed = [s for s, ok in results.items() if not ok]
        if not args.dry_run and any(not plans[s].empty for s in results if results[s]):
            print("editor holder: refresh the AssetDatabase and check the console (Unity_RunCommand: AssetDatabase.Refresh()).")
        print("land: " + ("all green" if not failed else "FAILED: " + ", ".join(failed)))
        return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
