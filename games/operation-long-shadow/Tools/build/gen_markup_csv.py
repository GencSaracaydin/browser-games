#!/usr/bin/env python3
"""Generates Assets/_Game/Data/markup_ids.csv from TECH_PLAN Appendix A (WP00 Phase A).

One row per marker/trigger id: id, kind, component, beat, x, y, z, extents, heading, points, notes.
* kind: Appendix A code (M T N C R CP P I G D K) plus COVER, SPLINE, POLY.
* component: the Core markup component to place (SceneMarker, TriggerVolume, OutOfBoundsVolume,
  SquadNodeSet, SpawnCloset, FlankRoute, CheckpointMarker, PerchNode, IlluminationZone, GlassingPOI,
  GateMarker, CoverNode) or the WP that owns the non-Core type (Spline/SplineVehicle WP10, CriticalPath WP10).
* x y z: centre / anchor in world metres (booth and heli: local frame). Empty when Appendix A gives none.
* extents: "box sx sy sz" | "sphere r" | empty. Boxes without a y range span 1 m below to 4 m above the
  local floor (Appendix A); floor-relative boxes say so in the notes.
* points: "label:x y z|x y z|..." (node-set members HOL/SPA/TIN, route and spline knots).
* notes: the Appendix A text for the id (normalised), plus generator remarks.

Enemy spawn markers (E2-01 ...) take their positions and facings from the GDD §7 encounter tables.

    python3 Tools/build/gen_markup_csv.py            # writes the CSV, prints a summary
    python3 Tools/build/gen_markup_csv.py --check    # exit 1 if the CSV is out of date

After Phase A the CSV is owned by WP10 (TECH_PLAN §11.1): Appendix A and the CSV change together
through Docs/REQUESTS.md; re-running this script overwrites manual edits, so WP10 should edit the CSV
directly once it diverges.
"""
from __future__ import annotations

import argparse
import csv
import io
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TECH_PLAN = ROOT / "Docs" / "TECH_PLAN.md"
GDD = ROOT / "Docs" / "GDD.md"
OUT = ROOT / "Assets" / "_Game" / "Data" / "markup_ids.csv"

NUM = r"-?\d+(?:\.\d+)?"
TRIPLE = re.compile(rf"\(\s*({NUM})\s*,\s*({NUM})\s*,\s*({NUM})\s*\)")
KINDS = ["CoverNodes", "polylines", "spline", "D + I", "CP", "M", "T", "N", "C", "R", "P", "I", "G", "D", "K"]
COMPONENT = {
    "M": "SceneMarker", "T": "TriggerVolume", "N": "SquadNodeSet", "C": "SpawnCloset", "R": "FlankRoute",
    "CP": "CheckpointMarker", "P": "PerchNode", "I": "IlluminationZone", "G": "GlassingPOI",
    "D": "SceneMarker+Destructible(WP03)", "D + I": "SceneMarker+Destructible(WP03)+IlluminationZone",
    "K": "GateMarker", "COVER": "CoverNode", "SPLINE": "Spline/SplineVehicle(WP10)", "POLY": "CriticalPath(WP10)",
}
MEMBERS = ["HOL", "SPA", "TIN"]

# Values Appendix A implies but does not spell out (documented in the notes column).
OVERRIDES = {
    "B5.Plank12": {"y": 8.5, "extents": "box 2 0.1 0.6", "floor_relative": False,
                   "note": "y from the B5 critical-path knot (247, 8.5, 318); plank 2 m along x"},
    "B5.Plank23": {"y": 9.5, "extents": "box 2 0.1 0.6", "floor_relative": False,
                   "note": "y between the tank stand (+9) and R3 (+10); plank 2 m along x"},
    "B5.Stair34": {"y": 11.0, "extents": "", "floor_relative": False,
                   "note": "y from the B5 critical-path knot (276, 11, 320); steel stair x 275-277"},
    "B2.IrrigationWall": {"y": 0.6, "extents": "box 30 1.2 1", "floor_relative": False,
                          "note": "1.2 m high wall x 140-170, z 109-110 with gaps at x 148 and 162"},
    "BOOTH.CRT1": {"pos": (-0.55, 1.00, 1.30), "note": "GDD §8.0"},
    "BOOTH.CRT2": {"pos": (0.00, 1.02, 1.30), "note": "GDD §8.0 (centre CRT)"},
    "BOOTH.CRT3": {"pos": (0.56, 0.98, 1.30), "note": "GDD §8.0"},
    "BOOTH.CRT4": {"pos": (-0.50, 1.48, 1.34), "note": "GDD §8.0"},
    "BOOTH.CRT5": {"pos": (0.04, 1.52, 1.34), "note": "GDD §8.0"},
    "BOOTH.CRT6": {"pos": (0.55, 1.47, 1.34), "note": "GDD §8.0"},
    "BOOTH.Mirror": {"pos": (0.0, 1.5, 2.55), "extents": "box 3.2 1.2 0.02", "note": "GDD §8.0 wall plane z 2.55, x -1.6..1.6, y 0.9..2.1"},
}


def norm(s: str) -> str:
    return (s.replace("−", "-").replace("–", "–").replace("—", "—").replace("…", "…")
             .replace(" ", " ").strip())


def f(x) -> str:
    if x is None or x == "":
        return ""
    v = float(x)
    return str(int(v)) if v.is_integer() else f"{v:.4f}".rstrip("0").rstrip(".")


def appendix_rows():
    text = norm(TECH_PLAN.read_text())
    start = text.index("## Appendix A")
    end = text.index("**Critical-path knots**", start)
    table = text[start:end]
    rows = []
    for line in table.splitlines():
        if not line.startswith("| ") or line.startswith("| Beat") or line.startswith("|---"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 2:
            continue
        rows.append((cells[0], "|".join(cells[1:])))
    knots_start = end
    knots_end = text.index("## Appendix B", knots_start)
    knots = {}
    for line in text[knots_start:knots_end].splitlines():
        m = re.match(r"\|\s*(B\d+)\s*\|(.*)\|\s*$", line)
        if m:
            pts = [tuple(map(float, t)) for t in TRIPLE.findall(m.group(2))]
            knots[m.group(1)] = (pts, m.group(2).strip())
    return rows, knots


def gdd_enemies():
    out = {}
    for line in norm(GDD.read_text()).splitlines():
        m = re.match(r"\|\s*(E\d+-\d+)\s*\|([^|]*)\|([^|]*)\|([^|]*)\|", line)
        if not m:
            continue
        t = TRIPLE.search(m.group(3))
        h = re.search(r"(\d+)\s*°", m.group(4))
        out[m.group(1)] = (tuple(map(float, t.groups())) if t else None, float(h.group(1)) if h else None, m.group(2).strip())
    return out


def split_entries(beat: str, cell: str):
    parts = [p.strip() for p in cell.split(" · ")]
    if beat == "B11":
        merged = []
        for p in parts:
            if re.match(r"N\d \(", p) and merged:
                merged[-1] += " · " + p
            else:
                merged.append(p)
        parts = merged
    return [p for p in parts if p]


def split_definitions(entry: str):
    """'`A` M (..) + `B` T centre r 3' -> ['`A` M (..)', '`B` T centre r 3']; same for ' and `', ' → `', '`A`, `B` M'."""
    m = re.match(r"^((?:`[^`]+`\s*,\s*)+`[^`]+`)\s+(.*)$", entry)
    if m and "…" not in m.group(1):
        ids = re.findall(r"`([^`]+)`", m.group(1))
        return ["`" + i + "` " + m.group(2) for i in ids]
    defs = re.split(r"\s+(?:\+|and|→)\s+(?=`)", entry)
    return defs


def kind_of(rest: str):
    for k in KINDS:
        if rest == k or rest.startswith(k + " ") or rest.startswith(k + ","):
            return k, rest[len(k):].strip()
    return None, rest


def expand_range(start: str, end_token: str):
    m = re.match(r"^(.*?)(\d+)$", start)
    e = re.search(r"(\d+)$", end_token)
    if not m or not e:
        return [start]
    head, digits = m.group(1), m.group(2)
    a, b = int(digits), int(e.group(1))
    return [head + str(n).zfill(len(digits)) for n in range(a, b + 1)]


def parse_box(text: str):
    m = re.search(rf"x ({NUM})–({NUM}), z ({NUM})–({NUM})(?:, [yY] ({NUM})–({NUM}))?", text)
    if m:
        x0, x1, z0, z1 = map(float, m.group(1, 2, 3, 4))
        if m.group(5) is not None:
            y0, y1 = float(m.group(5)), float(m.group(6))
            return ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), f"box {f(abs(x1 - x0))} {f(abs(y1 - y0))} {f(abs(z1 - z0))}", False
        return ((x0 + x1) / 2, 1.5, (z0 + z1) / 2), f"box {f(abs(x1 - x0))} 5 {f(abs(z1 - z0))}", True
    m = re.search(rf"x ({NUM})–({NUM}), z ({NUM})(?![\d–.])", text)
    if m:
        x0, x1, z = map(float, m.group(1, 2, 3))
        return ((x0 + x1) / 2, 2.0, z), f"box {f(abs(x1 - x0))} 4 0.3", True
    m = re.search(rf"x ({NUM}), z ({NUM})–({NUM})", text)
    if m:
        x, z0, z1 = map(float, m.group(1, 2, 3))
        return (x, 2.0, (z0 + z1) / 2), f"box 0.3 4 {f(abs(z1 - z0))}", True
    return None


class Gen:
    def __init__(self):
        self.rows = {}
        self.order = []
        self.warnings = []
        self.enemies = gdd_enemies()

    def add(self, id_, kind, beat, pos=None, extents="", heading=None, points=None, notes="", floor_relative=False):
        if id_ in self.rows:
            self.warnings.append(f"duplicate id {id_} (kept the first definition)")
            return
        comp = COMPONENT.get(kind, "SceneMarker")
        if kind == "T" and id_.startswith("OOB."):
            comp = "OutOfBoundsVolume"
        if kind == "T" and id_ == "B5.T.Stair":
            comp = "TriggerVolume"
        ov = OVERRIDES.get(id_)
        extra = []
        if ov:
            if "pos" in ov:
                pos = ov["pos"]
            if "y" in ov and pos is not None:
                pos = (pos[0], ov["y"], pos[2])
            if "extents" in ov:
                extents = ov["extents"]
            if "floor_relative" in ov:
                floor_relative = ov["floor_relative"]
            extra.append("generator: " + ov["note"])
        if floor_relative:
            extra.append("generator: y is floor-relative (box spans 1 m below to 4 m above the local floor); place on the floor")
        self.rows[id_] = {
            "id": id_, "kind": kind, "component": comp, "beat": beat,
            "x": f(pos[0]) if pos else "", "y": f(pos[1]) if pos else "", "z": f(pos[2]) if pos else "",
            "extents": extents, "heading": f(heading) if heading is not None else "",
            "points": "|".join(((lbl + ":") if lbl else "") + f"{f(p[0])} {f(p[1])} {f(p[2])}" for lbl, p in (points or [])),
            "notes": "; ".join([notes] + extra) if extra else notes,
        }
        self.order.append(id_)

    def definition(self, beat: str, d: str, ctx: dict):
        m = re.match(r"^`([^`]+)`(?:\s*…\s*`([^`]+)`)?\s*(.*)$", d)
        if not m:
            # prose with embedded ids, e.g. "E2 spawn markers `E2-01`…`E2-06` M at GDD positions" / "one `OOB.Block.*` T ..."
            inner = re.search(r"`([^`]+)`(?:\s*…\s*`([^`]+)`)?\s*(.*)$", d)
            if not inner:
                self.warnings.append(f"{beat}: unparsed entry: {d}")
                return
            m = inner
        first, range_end, rest = m.group(1), m.group(2), m.group(3).strip()
        if first.startswith(".") and ctx.get("last_base"):
            first = ctx["last_base"] + first
        ids = expand_range(first, range_end) if range_end else [first]
        kind, rest2 = kind_of(rest)
        if kind is None:
            if beat in ("Booth (local)", "Heli (local)") or ctx.get("force_m"):
                kind = "M"
            elif re.match(r"^CP\d", first):
                kind = "CP"
            elif re.match(r"^E\d+-\d+", first):
                kind = "M"
            else:
                kind = ctx.get("last_kind", "M")
            rest2 = rest
        if kind == "CoverNodes":
            kind = "COVER"
        elif kind == "spline":
            kind = "SPLINE"
        elif kind == "polylines":
            kind = "POLY"
        notes = norm(d)
        beat_label = beat.split(" (")[0]
        if "." in first:
            ctx["last_base"] = first.rsplit(".", 1)[0]
        ctx["last_kind"] = kind

        # --- special shapes
        if first == "CritPath.B1":
            return  # generated from the knots table
        if first.endswith(".*"):
            if first == "OOB.Block.*":
                for blk in ("MarketSouth", "MarketArch", "SideAlley", "R4Stair", "Breach", "Postern"):
                    self.add("OOB.Block." + blk, "T", beat_label, notes="soft OOB 4 m in front of Block." + blk +
                             " on the player side (Appendix A); generator: no coordinates given, place relative to the blocker")
            else:
                self.add(first, kind, beat_label, notes=notes + "; pattern id (one marker per beauty shot)")
            return
        if first == "B1.T.Scree":
            pts = [tuple(map(float, t)) for t in TRIPLE.findall(rest2)]
            a, b = pts[0], pts[1]
            length = math.dist((a[0], a[2]), (b[0], b[2]))
            heading = (math.degrees(math.atan2(b[0] - a[0], b[2] - a[2])) + 360) % 360
            centre = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2 + 1.5, (a[2] + b[2]) / 2)
            self.add(first, "T", beat_label, centre, f"box 4 3 {f(round(length, 2))}", round(heading, 1), [("", a), ("", b)], notes)
            return
        if first == "B5.SunLook":
            o = (291.0, 13.0, 328.0)
            az, el = math.radians(258.0), math.radians(1.5)
            p = (o[0] + 500 * math.cos(el) * math.sin(az), o[1] + 500 * math.sin(el), o[2] + 500 * math.cos(el) * math.cos(az))
            self.add(first, "M", beat_label, tuple(round(v, 2) for v in p), heading=258,
                     notes=notes + "; generator: computed from B5.R4Parapet (291, 13, 328)")
            return
        if first == "B5.T.Stair":
            self.add(first, "T", beat_label, (291, 12, 330), "sphere 1.5", notes=notes + "; PlayerClip at the stair top until MID-A ends")
            return
        if first == "B10.HeliApproach":
            rest2 = rest2.split(", the last")[0]
        if beat == "B11" and first == "B11.Exfil":
            pts = [(lbl, tuple(map(float, t))) for lbl, *t in re.findall(rf"(N\d) \(\s*({NUM})\s*,\s*({NUM})\s*,\s*({NUM})\s*\)", rest2)]
            self.add(first, "SPLINE", "B11", pts[0][1], points=pts, notes=notes)
            return

        for i, id_ in enumerate(ids):
            pos, extents, heading, points, floor_rel = None, "", None, [], False
            hm = re.search(r"(?:^|\s)h(\d+(?:\.\d+)?)\b", rest2)
            if hm:
                heading = float(hm.group(1))
            labeled = re.findall(rf"\b(HOL|SPA|TIN) \(\s*({NUM})\s*,\s*({NUM})\s*,\s*({NUM})\s*\)", rest2)
            triples = [tuple(map(float, t)) for t in TRIPLE.findall(rest2)]
            box = parse_box(rest2)
            sph = re.search(rf"centre(?: \(\s*({NUM})\s*,\s*({NUM})\s*,\s*({NUM})\s*\))? r ({NUM})", rest2) or \
                re.search(rf"\(\s*({NUM})\s*,\s*({NUM})\s*,\s*({NUM})\s*\) r ({NUM})", rest2)
            if kind == "N":
                if labeled:
                    points = [(lbl, (float(x), float(y), float(z))) for lbl, x, y, z in labeled]
                else:
                    points = list(zip(MEMBERS, triples[:3]))
                hol = dict(points).get("HOL")
                pos = hol if hol else (points[0][1] if points else None)
                if "allowDoorway" in rest2:
                    notes = notes + "; flag allowDoorway"
            elif kind in ("R", "SPLINE"):
                points = [("", t) for t in triples]
                pos = triples[0] if triples else None
            elif len(ids) > 1 and len(triples) >= len(ids):
                pos = triples[i]
            elif sph:
                if sph.group(1) is not None:
                    pos = tuple(map(float, sph.group(1, 2, 3)))
                else:
                    pos = ctx.get("last_pos")
                extents = "sphere " + f(sph.group(4))
            elif box:
                pos, extents, floor_rel = box
            elif triples and len(ids) == 1:
                pos = triples[0]
            if pos is None and re.match(r"^E\d+-\d+$", id_):
                e = self.enemies.get(id_)
                if e and e[0]:
                    pos, heading = e[0], e[1]
                    notes_id = f"{e[2]}; generator: position and facing from the GDD §7 encounter table"
                else:
                    notes_id = "generator: no position in the GDD encounter table (closet spawn)"
                self.add(id_, kind, beat_label, pos, extents, heading, points, notes_id)
                continue
            if pos is None and id_.startswith("B3.AW"):
                n = int(id_[5:])
                if n <= 5:
                    pos = (189.0, 4.5, 214 + (n - 1) * 4.0)
                else:
                    pos = (185.0, 4.5, 230 + (n - 5) * 10.0 / 3.0)
                notes_extra = "generator: approximate position spread along the given z range; WP10 places it per GDD §7.3"
                self.add(id_, kind, beat_label, tuple(round(v, 2) for v in pos), extents, heading, points, notes + "; " + notes_extra)
                continue
            if pos is None and id_ not in OVERRIDES:
                self.warnings.append(f"{id_}: no position in Appendix A ({notes[:80]})")
            self.add(id_, kind, beat_label, pos, extents, heading, points, notes, floor_rel)
            if pos is not None:
                ctx["last_pos"] = pos

    def run(self):
        rows, knots = appendix_rows()
        for beat, cell in rows:
            ctx = {}
            for entry in split_entries(beat, cell):
                defs = split_definitions(entry)
                for d in defs:
                    if not d.startswith("`") and "`" not in d:
                        continue
                    self.definition(beat, d, ctx)
        for b in [f"B{i}" for i in range(1, 11)]:
            pts, raw = knots.get(b, ([], ""))
            self.add("CritPath." + b, "POLY", "Global", pts[0] if pts else None, points=[("", p) for p in pts],
                     notes="critical-path knots (Appendix A); ICriticalPath.Progress01 = normalised arc length, never decreasing")
        if "B11" in knots:
            self.add("CritPath.B11", "POLY", "Global", notes="progress = exfil spline time / 50 s (Appendix A); no knots")
        self.insertion_markers()
        return self

    def insertion_markers(self):
        """Docs/CINEMATIC_DIRECTION.md §2 (INSERTION cold open, inside S1_Approach). Added by WP00 so WP09/WP10 have them early."""
        src = "CINEMATIC_DIRECTION §2"
        lz = (-46.0, 12.5, -28.0)
        self.add("INS.LZ0", "M", "Intro", lz, heading=45, notes=f"{src}: skid contact point of the landing, heading 45")
        self.add("INS.Heli.Approach", "SPLINE", "Intro", (-420.0, 34.0, -150.0),
                 points=[("", t) for t in [(-420, 34, -150), (-260, 18, -95), (-120, 9, -50), (-60, 4.5, -33), lz]],
                 notes=f"{src}: low over the dunes out of the sun; nose-up flare over the last 25 m; ends at INS.LZ0")
        self.add("INS.Heli.Depart", "SPLINE", "Intro", lz,
                 points=[("", t) for t in [lz, (-20, 18, -60), (60, 40, -140), (200, 60, -260)]],
                 notes=f"{src}: banks away SSE, clear of the town")
        self.add("INS.Squad.Path", "SPLINE", "Intro", lz,
                 points=[("", t) for t in [lz, (-10, 15, 0), (22, 20, 18), (36, 22, 28)]],
                 notes=f"{src}: crouch-run up the reverse slope; generator: last knot = B1.Squad.Start HOL (36, 22, 28), "
                       "each member ends on its B1.Squad.Start slot")
        self.add("INS.Cam.*", "M", "Intro",
                 notes=f"{src}: one SceneMarker per shot I-1..I-12 (INS.Cam.I01 ... INS.Cam.I12); WP09 places and refines them")
        self.add("OOB.InsertionApron", "T", "OOB", (-110.0, 20.0, -70.0), extents="box 220 40 180",
                 notes=f"{src}: cinematic-only terrain apron x -220..0, z -160..20 is never reachable in gameplay; "
                       "generator: y 0..40 covers the dunes (Y 10-14) up to the reverse slope")

    def csv_text(self) -> str:
        buf = io.StringIO()
        cols = ["id", "kind", "component", "beat", "x", "y", "z", "extents", "heading", "points", "notes"]
        w = csv.DictWriter(buf, fieldnames=cols, lineterminator="\n")
        w.writeheader()
        for i in self.order:
            w.writerow(self.rows[i])
        return buf.getvalue()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    g = Gen().run()
    text = g.csv_text()
    if a.check:
        same = OUT.exists() and OUT.read_text() == text
        print("markup_ids.csv " + ("up to date" if same else "OUT OF DATE"))
        return 0 if same else 1
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text)
    kinds = {}
    for r in g.rows.values():
        kinds[r["kind"]] = kinds.get(r["kind"], 0) + 1
    print(f"wrote {OUT.relative_to(ROOT)}: {len(g.rows)} ids; " + ", ".join(f"{k} {v}" for k, v in sorted(kinds.items())))
    no_pos = [r["id"] for r in g.rows.values() if not r["x"]]
    print(f"{len(no_pos)} ids without coordinates: {', '.join(no_pos)}")
    for w in g.warnings:
        print("warning:", w)
    return 0


if __name__ == "__main__":
    sys.exit(main())
