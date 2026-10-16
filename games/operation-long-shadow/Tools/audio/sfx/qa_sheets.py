"""Contact sheets of the import-ready WP06_SFX clips (waveform dB envelope + spectrogram per clip), one PNG per
manifest-id group, for visual review. Not shipped.

usage: python qa_sheets.py [--ids SFX_WPN_AK_SHOT,...] [--prefix SFX_WPN_] [--max-dur 3] [--cols 2]
       -> RawAssets/audio/sfx/_qa/sheets/<group>.png
"""
from __future__ import annotations

import argparse
import collections
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import sheet  # noqa: E402

ROOT = pathlib.Path("/Users/gencsaracaydin/Documents/fun/fun-unity")
READY = ROOT / "RawAssets/audio/ready"
OUT = ROOT / "RawAssets/audio/sfx/_qa/sheets"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids")
    ap.add_argument("--prefix")
    ap.add_argument("--max-dur", type=float, default=None)
    ap.add_argument("--cols", type=int, default=2)
    ap.add_argument("--per", type=int, default=24, help="max clips per sheet")
    ap.add_argument("--name")
    a = ap.parse_args()
    rows = json.loads((READY / "import_index_sfx.json").read_text())
    ids = set(a.ids.split(",")) if a.ids else None
    groups = collections.OrderedDict()
    for r in rows:
        mid = r["manifest_id"]
        if ids and mid not in ids:
            continue
        if a.prefix and not mid.startswith(a.prefix):
            continue
        groups.setdefault(mid, []).append(READY / r["file"])
    files = [f for fs in groups.values() for f in fs]
    name = a.name or (a.prefix or "sel").rstrip("_")
    OUT.mkdir(parents=True, exist_ok=True)
    outs = []
    for k in range(0, len(files), a.per):
        chunk = files[k:k + a.per]
        p = OUT / f"{name}_{k // a.per:02d}.png"
        sheet.sheet(p, chunk, [f.stem for f in chunk], max_dur=a.max_dur, cols=a.cols, title=name)
        outs.append(p)
    print("\n".join(str(o) for o in outs))


if __name__ == "__main__":
    main()
