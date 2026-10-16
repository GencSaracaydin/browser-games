#!/usr/bin/env python3
"""Sardani glyph font (WP08, manifest UI_FONT_SARDANI, GDD §2.3-3).

Generates the invented Sardani script as a TrueType font that TMP turns into an SDF font asset
(LongShadow/UI/2 Build Theme -> Assets/_Game/Art/Fonts/SDF/Sardani-Regular SDF.asset) and that WP10's SignBaker uses
for shop signs, posters and the marquee. Latin input maps onto Sardani glyphs: A-Z and a-z share 26 letters, 0-9 are
Sardani numerals, and a few punctuation marks exist, so signage strings stay readable in the code.

Design rules (to resemble no real alphabet; reviewed with the proof sheet, DECISIONS.md [WP08] O3):
  * every letter hangs on a *kinked spine*: a vertical stroke that steps sideways once through a 45 degree jog
    (the Art Deco "stepped" motif of the colonial town);
  * attachments are short horizontal arms, quarter-round "petals" and small lozenge dots (above, below or beside);
  * numerals are a separate family: a short kinked stem crowned by an open lozenge, with arms;
  * monoline strokes, tall narrow proportions, no closed counters in letters, no symmetric crossbars.

Outputs (idempotent; --force to overwrite):
  RawAssets/wp08/ready/Assets/_Game/Art/Fonts/Sardani-Regular.ttf   (+ Sardani-Regular_LICENSE.txt, CC0, ours)
  RawAssets/wp08/_review/sardani_proof.png                          (glyph table + sample signs at several sizes)
  RawAssets/wp08/ready/rows/sardani.json                            (import rows) and the two rows in import_index.json

Run:  Tools/.venv/bin/python Tools/ui/glyph_font.py [--force]
Needs: fontTools, skia-pathops (union of the stroke primitives), Pillow (proof sheet).
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import pathops
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

ROOT = Path(__file__).resolve().parents[2]
READY = ROOT / "RawAssets/wp08/ready"
TARGET_REL = "Assets/_Game/Art/Fonts/Sardani-Regular.ttf"
LICENSE_REL = "Assets/_Game/Art/Fonts/Sardani-Regular_LICENSE.txt"
REVIEW = ROOT / "RawAssets/wp08/_review/sardani_proof.png"
ROWS = READY / "rows/sardani.json"
INDEX = READY / "import_index.json"

UPM = 1000
CAP = 700          # letter height
STROKE = 80        # monoline weight
ADVANCE = 520      # narrow, Deco proportions
SPACE = 300
DOT = 64           # lozenge half-diagonal
NUM_CAP = 560      # numerals are shorter

# ---------------------------------------------------------------------------------------------------- primitives


def poly(points):
    p = pathops.Path()
    p.moveTo(*points[0])
    for pt in points[1:]:
        p.lineTo(*pt)
    p.close()
    return p


def ensure_cw(points):
    area = 0.0
    for i in range(len(points)):
        x1, y1 = points[i]
        x2, y2 = points[(i + 1) % len(points)]
        area += x1 * y2 - x2 * y1
    return points if area < 0 else list(reversed(points))


def rect(x0, y0, x1, y1):
    return poly(ensure_cw([(x0, y0), (x1, y0), (x1, y1), (x0, y1)]))


def seg(x1, y1, x2, y2, w=STROKE, cap=0.0):
    """Thick straight stroke; cap extends both ends by `cap` (square caps)."""
    dx, dy = x2 - x1, y2 - y1
    length = math.hypot(dx, dy) or 1.0
    ux, uy = dx / length, dy / length
    nx, ny = -uy * w / 2, ux * w / 2
    ax, ay = x1 - ux * cap, y1 - uy * cap
    bx, by = x2 + ux * cap, y2 + uy * cap
    return poly(ensure_cw([(ax + nx, ay + ny), (bx + nx, by + ny), (bx - nx, by - ny), (ax - nx, ay - ny)]))


def arc(cx, cy, r, a0, a1, w=STROKE, steps=14):
    """Thick circular arc band from angle a0 to a1 (degrees, CCW)."""
    outer, inner = [], []
    for i in range(steps + 1):
        a = math.radians(a0 + (a1 - a0) * i / steps)
        outer.append((cx + (r + w / 2) * math.cos(a), cy + (r + w / 2) * math.sin(a)))
        inner.append((cx + (r - w / 2) * math.cos(a), cy + (r - w / 2) * math.sin(a)))
    return poly(ensure_cw(outer + list(reversed(inner))))


def lozenge(cx, cy, s=DOT):
    return poly(ensure_cw([(cx, cy + s), (cx + s * 0.78, cy), (cx, cy - s), (cx - s * 0.78, cy)]))


def union(paths):
    result = pathops.Path()
    for p in paths:
        result = pathops.op(result, p, pathops.PathOp.UNION, fix_winding=True, keep_starting_points=False)
    return result


def difference(a, b):
    return pathops.op(a, b, pathops.PathOp.DIFFERENCE, fix_winding=True, keep_starting_points=False)


# ---------------------------------------------------------------------------------------------------- the script


class Glyph:
    """
    A Sardani letter: a kinked spine (a vertical stroke that steps sideways once through a 45 degree jog) with a top
    terminal, a bottom terminal and an optional side tab. Terminals: 'armL'/'armR' (horizontal bar), 'stepL'/'stepR'
    (a stair step that rises above the cap line or drops below the baseline), 'hookL'/'hookR' (a quarter-round turn
    ending in a short horizontal tail). Tabs are small solid blocks on the upper half. Everything is connected: no
    detached dots or points (which would echo the i'jam/nuqta marks of the Arabic script), no closed counters.
    """

    X0 = 170           # column of the spine's upper half (kink 'R')
    JOG = 90           # sideways step of the kink
    KNEE = 380         # height of the kink
    ARM = 150
    STEP_RUN = 110
    STEP_RISE = 120
    HOOK_R = 95
    TAB = 86

    def __init__(self, kink="R", height=CAP, x0=None):
        self.parts = []
        self.holes = []
        self.kink = kink
        self.h = height
        self.x0 = self.X0 if x0 is None else x0
        self.cut_top = height
        self.cut_bottom = 0.0
        self.terminals = []

    def top_x(self):
        return self.x0 if self.kink == "R" else self.x0 + self.JOG

    def bottom_x(self):
        return self.x0 + self.JOG if self.kink == "R" else self.x0

    def top(self, kind):
        self.terminals.append(("top", kind))
        return self

    def bottom(self, kind):
        self.terminals.append(("bottom", kind))
        return self

    def tab(self, side):
        self.terminals.append(("tab", side))
        return self

    # geometry ----------------------------------------------------------------------------------------------------
    def _build(self):
        s, h = STROKE, self.h
        knee = self.KNEE * h / CAP
        tx, bx = self.top_x(), self.bottom_x()
        parts = []
        for where, kind in self.terminals:
            if where == "top":
                parts += self._top(kind, tx, h)
            elif where == "bottom":
                parts += self._bottom(kind, bx)
            else:
                y0 = knee + (h - knee) * 0.42
                if kind == "R":
                    parts.append(rect(tx + s - 2, y0, tx + s + self.TAB, y0 + self.TAB))
                else:
                    parts.append(rect(tx - self.TAB, y0, tx + 2, y0 + self.TAB))
        upper_bottom = knee + self.JOG * 0.5
        lower_top = knee - self.JOG * 0.5
        parts.append(rect(tx, upper_bottom, tx + s, self.cut_top))
        parts.append(rect(bx, self.cut_bottom, bx + s, lower_top))
        parts.append(seg(tx + s / 2, upper_bottom + s * 0.3, bx + s / 2, lower_top - s * 0.3, s))
        return parts

    def _top(self, kind, tx, h):
        s, side = STROKE, kind[-1]
        if kind.startswith("arm"):
            return [rect(tx, h - s, tx + s + self.ARM, h) if side == "R" else rect(tx - self.ARM, h - s, tx + s, h)]
        if kind.startswith("step"):
            run, rise = self.STEP_RUN, self.STEP_RISE
            if side == "R":
                return [rect(tx, h - s, tx + s + run, h), rect(tx + run, h - s, tx + run + s, h + rise)]
            return [rect(tx - run, h - s, tx + s, h), rect(tx - run, h - s, tx - run + s, h + rise)]
        # hook: quarter-round from the spine axis into a horizontal tail at the cap line
        r = self.HOOK_R
        cy = h - s / 2 - r
        self.cut_top = cy + 1
        axis = tx + s / 2
        if side == "R":
            cx = axis + r
            return [arc(cx, cy, r, 90, 180), rect(cx - 2, h - s, cx + 70, h)]
        cx = axis - r
        return [arc(cx, cy, r, 0, 90), rect(cx - 70, h - s, cx + 2, h)]

    def _bottom(self, kind, bx):
        s, side = STROKE, kind[-1]
        if kind.startswith("arm"):
            return [rect(bx, 0, bx + s + self.ARM, s) if side == "R" else rect(bx - self.ARM, 0, bx + s, s)]
        if kind.startswith("step"):
            run, rise = self.STEP_RUN, self.STEP_RISE
            if side == "R":
                return [rect(bx, 0, bx + s + run, s), rect(bx + run, -rise, bx + run + s, s)]
            return [rect(bx - run, 0, bx + s, s), rect(bx - run, -rise, bx - run + s, s)]
        r = self.HOOK_R
        cy = s / 2 + r
        self.cut_bottom = cy - 1
        axis = bx + s / 2
        if side == "R":
            cx = axis + r
            return [arc(cx, cy, r, 180, 270), rect(cx - 2, 0, cx + 70, s)]
        cx = axis - r
        return [arc(cx, cy, r, 270, 360), rect(cx - 70, 0, cx + 2, s)]

    def path(self):
        p = union(self._build() + self.parts)
        for hole in self.holes:
            p = difference(p, hole)
        return p


def staircase(x0, steps, run=62, rise=118, s=None):
    """A rising staircase of `steps` treads starting at the baseline (Sardani numerals 1-4)."""
    s = s or STROKE * 0.9
    parts = []
    x, y = x0, 0.0
    parts.append(rect(x, 0, x + run + s, s))
    for i in range(steps):
        parts.append(rect(x + run, y, x + run + s, y + rise + s))
        y += rise
        x += run
        parts.append(rect(x, y, x + run + s, y + s))
    return parts


def numeral(n):
    """
    Sardani numerals count in stair steps: 1-4 are staircases of 1-4 steps, 5 is a kinked spine, 6-9 are the spine
    with 1-4 steps, and 0 is an open lozenge.
    """
    s = STROKE
    if n == 0:
        g = Glyph("R", NUM_CAP)
        g.terminals = []
        cx, cy = 230, NUM_CAP * 0.45
        outer = poly(ensure_cw([(cx, cy + 190), (cx + 130, cy), (cx, cy - 190), (cx - 130, cy)]))
        inner = poly(ensure_cw([(cx, cy + 190 - s * 1.25), (cx + 130 - s * 0.95, cy), (cx, cy - 190 + s * 1.25), (cx - 130 + s * 0.95, cy)]))
        g.parts = [outer]
        g.holes = [inner]
        g._build = lambda: []
        return g
    if n < 5:
        g = Glyph("R", NUM_CAP)
        g._build = lambda: []
        g.parts = staircase(70, n)
        return g
    g = Glyph("R", NUM_CAP, 70)
    g.parts = staircase(70 + g.JOG + s * 0.5, n - 5) if n > 5 else []
    return g


LETTERS = {
    "A": lambda: Glyph("R").top("stepR").bottom("armL"),
    "B": lambda: Glyph("L").top("armL").tab("R").bottom("stepR"),
    "C": lambda: Glyph("R").top("hookR").bottom("stepL"),
    "D": lambda: Glyph("L").top("stepL").tab("L"),
    "E": lambda: Glyph("R").top("armL").tab("R").bottom("armR"),
    "F": lambda: Glyph("L").top("hookL").bottom("armR"),
    "G": lambda: Glyph("R").top("stepR").tab("L").bottom("stepL"),
    "H": lambda: Glyph("L").tab("R").bottom("hookL"),
    "I": lambda: Glyph("R").bottom("stepR"),
    "J": lambda: Glyph("L").top("stepR").bottom("hookR"),
    "K": lambda: Glyph("R").top("armR").tab("L"),
    "L": lambda: Glyph("L").tab("L").bottom("armL"),
    "M": lambda: Glyph("R").top("stepL").tab("R").bottom("stepR"),
    "N": lambda: Glyph("L").top("hookR").tab("L").bottom("armL"),
    "O": lambda: Glyph("R").top("armL").bottom("hookR"),
    "P": lambda: Glyph("L").top("stepL").bottom("stepR"),
    "Q": lambda: Glyph("R").top("hookL").tab("R"),
    "R": lambda: Glyph("L").top("armR").bottom("stepL"),
    "S": lambda: Glyph("R").top("stepR").tab("R").bottom("armL"),
    "T": lambda: Glyph("L").bottom("stepL"),
    "U": lambda: Glyph("R").top("hookR").tab("L").bottom("armR"),
    "V": lambda: Glyph("L").top("stepR").tab("L").bottom("hookL"),
    "W": lambda: Glyph("R").top("armR").bottom("stepR"),
    "X": lambda: Glyph("L").top("hookL").tab("R").bottom("stepR"),
    "Y": lambda: Glyph("R").top("stepL").bottom("hookL"),
    "Z": lambda: Glyph("L").top("armL").tab("R").bottom("armR"),
}


def punctuation():
    s = STROKE
    out = {}
    out["period"] = (union([lozenge(110, DOT)]), 240)
    out["comma"] = (union([lozenge(110, DOT), seg(110, 10, 70, -110, s * 0.8)]), 240)
    out["colon"] = (union([lozenge(110, DOT), lozenge(110, 420)]), 240)
    out["hyphen"] = (union([rect(60, 290, 220, 290 + s), rect(220 - s, 290 - 60, 220, 290 + s)]), 320)
    out["emdash"] = (union([rect(60, 290, 640, 290 + s), rect(640 - s, 290 - 60, 640, 290 + s)]), 740)
    out["exclam"] = (union([Glyph("R", CAP, 60).top("stepR").path(), lozenge(150, -150)]), 420)
    out["question"] = (union([Glyph("L", CAP, 60).top("hookR").path(), lozenge(110, -150)]), 420)
    out["quotesingle"] = (union([seg(110, 700, 90, 520, s * 0.8)]), 220)
    out["quotedbl"] = (union([seg(90, 700, 70, 520, s * 0.8), seg(200, 700, 180, 520, s * 0.8)]), 300)
    out["slash"] = (union([seg(60, -60, 360, 760, s * 0.9)]), 420)
    out["periodcentered"] = (union([lozenge(110, 330)]), 240)
    return out


PUNCT_CODEPOINTS = {
    "period": [0x2E], "comma": [0x2C], "colon": [0x3A], "hyphen": [0x2D], "emdash": [0x2014, 0x2013],
    "exclam": [0x21], "question": [0x3F], "quotesingle": [0x27, 0x2019, 0x2018], "quotedbl": [0x22, 0x201C, 0x201D],
    "slash": [0x2F], "periodcentered": [0xB7],
}

SIDE = 60  # side bearing


def draw_tt(path, dx=0.0):
    from fontTools.pens.transformPen import TransformPen
    pen = TTGlyphPen(None)
    simplified = pathops.simplify(path, clockwise=True)
    simplified.draw(TransformPen(pen, (1, 0, 0, 1, dx, 0)))
    return pen.glyph()


def notdef():
    outer = rect(60, 0, 460, CAP)
    inner = rect(60 + STROKE, STROKE, 460 - STROKE, CAP - STROKE)
    return difference(outer, inner)


def build_font(out_path: Path):
    glyphs, metrics, cmap, order = {}, {}, {}, [".notdef", "space"]

    def add(name, path, codepoints, advance=None):
        bounds = path.bounds  # (xMin, yMin, xMax, yMax)
        dx = SIDE - bounds[0] if advance is None else 0.0
        glyph = draw_tt(path, dx)
        glyphs[name] = glyph
        width = advance if advance is not None else (bounds[2] - bounds[0]) + 2 * SIDE
        xs = [p[0] for p in glyph.coordinates] if glyph.numberOfContours > 0 else [0]
        metrics[name] = (int(round(width)), int(min(xs)))
        if name not in order:
            order.append(name)
        for cp in codepoints:
            cmap[cp] = name

    pen = TTGlyphPen(None)
    glyphs["space"] = pen.glyph()
    metrics["space"] = (SPACE, 0)
    cmap[0x20] = "space"
    cmap[0xA0] = "space"
    add(".notdef", notdef(), [], ADVANCE)

    for ch, make in LETTERS.items():
        add("sar_" + ch, make().path(), [ord(ch), ord(ch.lower())])
    for n in range(10):
        add("sar_num" + str(n), numeral(n).path(), [0x30 + n])
    for name, (path, advance) in punctuation().items():
        add("sar_" + name, path, PUNCT_CODEPOINTS[name])

    fb = FontBuilder(UPM, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=900, descent=-260)
    fb.setupNameTable({
        "familyName": "Sardani",
        "styleName": "Regular",
        "uniqueFontIdentifier": "LongShadow:Sardani-Regular:2026",
        "fullName": "Sardani Regular",
        "psName": "Sardani-Regular",
        "version": "Version 2.000",
        "copyright": "Invented script for OPERATION LONG SHADOW. CC0 1.0.",
        "licenseDescription": "CC0 1.0 Universal (public domain dedication)",
    })
    fb.setupOS2(sTypoAscender=900, sTypoDescender=-260, sTypoLineGap=120, usWinAscent=1000, usWinDescent=300,
                sxHeight=CAP, sCapHeight=CAP, achVendID="LSHD", fsType=0)
    fb.setupPost()
    fb.setupMaxp()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fb.save(str(out_path))
    return len(order)


# ---------------------------------------------------------------------------------------------------- proof sheet

def proof(font_path: Path, out: Path):
    from PIL import Image, ImageDraw, ImageFont

    W, H = 2200, 1900
    img = Image.new("RGB", (W, H), (233, 226, 208))
    d = ImageDraw.Draw(img)
    label = ImageFont.load_default()
    big = ImageFont.truetype(str(font_path), 120)
    mid = ImageFont.truetype(str(font_path), 64)
    small = ImageFont.truetype(str(font_path), 28)

    d.text((40, 20), "SARDANI (invented script, OPERATION LONG SHADOW) - glyph table, Latin key below each glyph", fill=(40, 30, 20), font=label)
    chars = [chr(c) for c in range(ord("A"), ord("Z") + 1)] + [str(n) for n in range(10)] + list(".,:-!?/—")
    cols = 13
    cell_w, cell_h = 160, 210
    for i, ch in enumerate(chars):
        x = 40 + (i % cols) * cell_w
        y = 60 + (i // cols) * cell_h
        d.rectangle([x, y, x + cell_w - 12, y + cell_h - 30], outline=(180, 170, 150))
        d.text((x + 30, y + 10), ch, fill=(30, 25, 20), font=big)
        d.text((x + 6, y + cell_h - 26), ch, fill=(120, 60, 40), font=label)

    samples = ["ZAROUD", "CINEMA ZENITH", "RELAY STATION 6", "THE LONG MARKET", "CURFEW 1984", "PUMP HOUSE SIX",
               "CLOCK SQUARE", "BAKERY - FRUIT - TEA"]
    rows = (len(chars) + cols - 1) // cols
    y = 60 + rows * cell_h + 20
    for s in samples[:4]:
        d.text((40, y), s, fill=(25, 20, 15), font=mid)
        d.text((1240, y + 20), "(" + s + ")", fill=(120, 60, 40), font=label)
        y += 95
    # sign-like: light glyphs on dark painted boards
    for i, s in enumerate(samples[4:]):
        bx, by = 40 + (i % 2) * 1080, y + 20 + (i // 2) * 130
        d.rectangle([bx, by, bx + 1040, by + 110], fill=(48, 62, 58))
        d.text((bx + 24, by + 18), s, fill=(225, 200, 140), font=mid)
    y += 300
    d.text((40, y), "Small print: " + "THE CURFEW IS FOR YOUR PROTECTION 1984", fill=(25, 20, 15), font=small)
    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out)


# ---------------------------------------------------------------------------------------------------- rows


def write_rows(font_path: Path):
    rows = [
        {
            "manifest_id": "UI_FONT_SARDANI",
            "file": TARGET_REL,
            "target_path": TARGET_REL,
            "type": "font",
            "import_notes": "TrueType font 'Sardani' (invented script, generated by Tools/ui/glyph_font.py; CC0, ours). "
                            "A-Z/a-z map to 26 letters, 0-9 to Sardani numerals, plus . , : - — ! ? ' \" / ·. "
                            "Include Font Data ON. LongShadow/UI/2 Build Theme makes 'Sardani-Regular SDF.asset' "
                            "(512 atlas, static) for the UI and WP10's SignBaker. Proof: RawAssets/wp08/_review/sardani_proof.png.",
            "bytes": font_path.stat().st_size,
        },
        {
            "manifest_id": "UI_FONT_SARDANI",
            "file": LICENSE_REL,
            "target_path": LICENSE_REL,
            "type": "license",
            "import_notes": "Licence text (CC0); keep in the repo, not referenced at runtime.",
        },
    ]
    ROWS.parent.mkdir(parents=True, exist_ok=True)
    ROWS.write_text(json.dumps(rows, indent=1, ensure_ascii=False) + "\n")
    if INDEX.exists():
        data = json.loads(INDEX.read_text())
        container = data if isinstance(data, list) else data.get("rows")
        if isinstance(container, list):
            keep = [r for r in container if r.get("target_path") not in (TARGET_REL, LICENSE_REL)]
            keep.extend(rows)
            if isinstance(data, list):
                data = keep
            else:
                data["rows"] = keep
            INDEX.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n")


LICENSE_TEXT = """Sardani-Regular.ttf - invented glyph set for OPERATION LONG SHADOW (WP08).
Generated procedurally by Tools/ui/glyph_font.py; no third-party font data is used.
Dedicated to the public domain under CC0 1.0 Universal: https://creativecommons.org/publicdomain/zero/1.0/
"""


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--force", action="store_true", help="overwrite existing outputs")
    ap.add_argument("--out", type=Path, default=READY / TARGET_REL)
    args = ap.parse_args(argv)
    if args.out.exists() and not args.force:
        print("exists (use --force):", args.out)
    else:
        n = build_font(args.out)
        (READY / LICENSE_REL).write_text(LICENSE_TEXT)
        print("wrote", args.out, n, "glyphs")
    proof(args.out, REVIEW)
    write_rows(args.out)
    print("proof", REVIEW)
    return 0


if __name__ == "__main__":
    sys.exit(main())
