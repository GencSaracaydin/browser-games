#!/usr/bin/env python3
"""Unity Editor slot lock (TECH_PLAN §11.2). Optional: the workflow script already guarantees one editor holder
at a time (ORCHESTRATION.md §5), but agents that share the editor outside a workflow use this.

Lock file: .locks/editor.json  {owner, wp, purpose, since, heartbeat, expected_minutes}
Queue file: .locks/editor_queue.json  [ {owner, wp, purpose, queued_at}, ... ]

    python3 Tools/build/editor_lock.py acquire --owner WP10_LOOK --wp WP10 --purpose "LightStage spike" --minutes 60
    python3 Tools/build/editor_lock.py heartbeat --owner WP10_LOOK      # at least every 30 min
    python3 Tools/build/editor_lock.py release --owner WP10_LOOK
    python3 Tools/build/editor_lock.py status
    python3 Tools/build/editor_lock.py queue --owner WP05 --wp WP05 --purpose "Rocketbox import"
    python3 Tools/build/editor_lock.py break --owner WP11               # WP11 only, lock stale > 90 min

Exit codes: 0 ok, 1 lock held by someone else (acquire) / not the owner (release, heartbeat), 2 usage error.
Slots are at most 60 minutes; at slot start run `land.py --pending`, refresh and check the console; at slot end
save scenes/assets, check the console and release.
"""
from __future__ import annotations

import argparse
import contextlib
import datetime as dt
import fcntl
import json
import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LOCK_DIR = ROOT / ".locks"
LOCK = LOCK_DIR / "editor.json"
QUEUE = LOCK_DIR / "editor_queue.json"
MUTEX = LOCK_DIR / ".editor.mutex"
STALE_MINUTES = 90
MAX_SLOT_MINUTES = 60


def now() -> dt.datetime:
    return dt.datetime.now().replace(microsecond=0)


def iso(t: dt.datetime) -> str:
    return t.isoformat()


@contextlib.contextmanager
def mutex():
    LOCK_DIR.mkdir(exist_ok=True)
    MUTEX.touch(exist_ok=True)
    with open(MUTEX, "r+") as fh:
        fcntl.flock(fh, fcntl.LOCK_EX)
        try:
            yield
        finally:
            fcntl.flock(fh, fcntl.LOCK_UN)


def read(path: Path, default):
    try:
        return json.loads(path.read_text())
    except (OSError, ValueError):
        return default


def write(path: Path, data) -> None:
    tmp = path.with_suffix(".tmp")
    tmp.write_text(json.dumps(data, indent=1) + "\n")
    os.replace(tmp, path)


def age_minutes(lock: dict) -> float:
    try:
        hb = dt.datetime.fromisoformat(lock.get("heartbeat") or lock["since"])
    except (KeyError, ValueError):
        return 1e9
    return (now() - hb).total_seconds() / 60.0


def describe(lock: dict) -> str:
    return (f"held by {lock.get('owner')} ({lock.get('wp')}) for '{lock.get('purpose')}' since {lock.get('since')}, "
            f"last heartbeat {age_minutes(lock):.0f} min ago, expected {lock.get('expected_minutes')} min")


def cmd_acquire(a) -> int:
    with mutex():
        lock = read(LOCK, None)
        if lock and lock.get("owner") != a.owner:
            print("busy: " + describe(lock) + (" (STALE: WP11 may break it)" if age_minutes(lock) > STALE_MINUTES else ""))
            return 1
        minutes = min(a.minutes, MAX_SLOT_MINUTES)
        t = iso(now())
        write(LOCK, {"owner": a.owner, "wp": a.wp, "purpose": a.purpose, "since": (lock or {}).get("since", t),
                     "heartbeat": t, "expected_minutes": minutes})
        queue = [q for q in read(QUEUE, []) if q.get("owner") != a.owner]
        write(QUEUE, queue)
        print(f"acquired by {a.owner} for {minutes} min")
        return 0


def cmd_heartbeat(a) -> int:
    with mutex():
        lock = read(LOCK, None)
        if not lock or lock.get("owner") != a.owner:
            print("not the owner: " + (describe(lock) if lock else "lock is free"))
            return 1
        lock["heartbeat"] = iso(now())
        write(LOCK, lock)
        print("heartbeat ok")
        return 0


def cmd_release(a) -> int:
    with mutex():
        lock = read(LOCK, None)
        if not lock:
            print("lock is already free")
            return 0
        if lock.get("owner") != a.owner:
            print("not the owner: " + describe(lock))
            return 1
        LOCK.unlink()
        queue = read(QUEUE, [])
        print("released" + (f"; next in queue: {queue[0].get('owner')} ({queue[0].get('purpose')})" if queue else ""))
        return 0


def cmd_queue(a) -> int:
    with mutex():
        queue = read(QUEUE, [])
        if not any(q.get("owner") == a.owner for q in queue):
            queue.append({"owner": a.owner, "wp": a.wp, "purpose": a.purpose, "queued_at": iso(now())})
            write(QUEUE, queue)
        pos = [q.get("owner") for q in queue].index(a.owner) + 1
        print(f"{a.owner} is #{pos} in the queue")
        return 0


def cmd_break(a) -> int:
    if a.owner != "WP11":
        print("only WP11 may break a stale lock")
        return 1
    with mutex():
        lock = read(LOCK, None)
        if not lock:
            print("lock is free")
            return 0
        if age_minutes(lock) <= STALE_MINUTES:
            print("not stale: " + describe(lock))
            return 1
        LOCK.unlink()
        print("broke stale lock: " + describe(lock))
        return 0


def cmd_status(_a) -> int:
    lock = read(LOCK, None)
    print("editor: " + (describe(lock) if lock else "free"))
    for i, q in enumerate(read(QUEUE, []), 1):
        print(f"  queue #{i}: {q.get('owner')} ({q.get('wp')}) '{q.get('purpose')}' since {q.get('queued_at')}")
    return 0


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Unity Editor slot lock (TECH_PLAN §11.2)")
    ap.add_argument("action", choices=["acquire", "heartbeat", "release", "queue", "status", "break"])
    ap.add_argument("--owner", default=os.environ.get("LS_AGENT", ""))
    ap.add_argument("--wp", default=os.environ.get("LS_WP", "?"))
    ap.add_argument("--purpose", default="")
    ap.add_argument("--minutes", type=int, default=MAX_SLOT_MINUTES)
    a = ap.parse_args(argv)
    if a.action != "status" and not a.owner:
        ap.error("--owner is required")
    return {"acquire": cmd_acquire, "heartbeat": cmd_heartbeat, "release": cmd_release, "queue": cmd_queue,
            "status": cmd_status, "break": cmd_break}[a.action](a)


if __name__ == "__main__":
    sys.exit(main())
