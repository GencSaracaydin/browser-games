#!/usr/bin/env python3
"""Shared 512 px material atlas for the procedural booth parts (M_BoothParts): reel flange disc, tape-pack disc and
32 flat swatches. Used by Tools/images/wp09_parts.py (moving parts of fal props) and wp09_smalls.py (PROC_BOOTH_SMALLS).

  Tools/.venv/bin/python Tools/images/wp09_parts_atlas.py
Writes RawAssets/wp09/ready/Assets/_Game/Art/Textures/Props/booth_parts/T_BoothParts_{BC,M}.png and
Tools/images/wp09_parts_atlas.json (swatch uv centres, disc centres/radii) that the Blender scripts read.
Layout (pixels, y down): flange disc centre (128,128) r 124; tape disc centre (384,128) r 124; swatches 64 px in the
bottom half, index s -> col s%8, row s//8.
"""
import json, pathlib
import numpy as np
from PIL import Image, ImageFilter

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "RawAssets/wp09/ready/Assets/_Game/Art/Textures/Props/booth_parts"
S = 512
SW = [  # name, sRGB base colour, metallic, smoothness
    ("chrome", (205, 205, 210), 1.0, 0.88), ("bakelite", (16, 16, 16), 0.0, 0.78), ("matte_black", (12, 12, 12), 0.0, 0.30),
    ("brushed_alu", (188, 190, 194), 1.0, 0.55), ("tape_oxide", (58, 36, 24), 0.0, 0.55), ("hub_alu", (110, 112, 116), 1.0, 0.45),
    ("cream", (228, 216, 188), 0.0, 0.40), ("red", (150, 24, 20), 0.0, 0.50), ("copper", (184, 115, 51), 1.0, 0.60),
    ("red_pvc", (135, 20, 18), 0.0, 0.50), ("black_pvc", (22, 22, 22), 0.0, 0.50), ("gel_pad", (205, 200, 188), 0.0, 0.40),
    ("cloth_tape", (214, 206, 186), 0.0, 0.12), ("cig_paper", (236, 233, 226), 0.0, 0.18), ("cork_filter", (196, 138, 72), 0.0, 0.20),
    ("ash", (88, 86, 83), 0.0, 0.05), ("ember", (62, 30, 20), 0.0, 0.10), ("gunmetal", (70, 72, 76), 1.0, 0.50),
    ("brass", (180, 140, 70), 1.0, 0.60), ("rubber", (25, 25, 25), 0.0, 0.20), ("steel", (150, 152, 156), 1.0, 0.65),
    ("needle_red", (170, 30, 22), 0.0, 0.45),
]


def srgb2lin(c):
    c = np.asarray(c, np.float32) / 255
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def main():
    rng = np.random.default_rng(9)
    bc = np.zeros((S, S, 3), np.float32); mm = np.zeros((S, S, 4), np.float32)
    y, x = np.mgrid[0:256, 0:256].astype(np.float32)
    r = np.hypot(x - 128, y - 128) / 124.0
    # flange: circular-brushed aluminium (fine concentric lines, slow tonal drift, darker anodised hub ring)
    lines = rng.standard_normal(2000).astype(np.float32)
    lines = np.convolve(lines, np.ones(3) / 3, mode="same")
    idx = np.clip((r * 1999).astype(int), 0, 1999)
    tone = 0.74 + 0.035 * lines[idx] + 0.02 * np.sin(r * 9.0) + 0.015 * np.cos(np.arctan2(y - 128, x - 128) * 2)
    tone = np.where(r < 0.36, tone * 0.82, tone)          # hub ring
    tone = np.where((r > 0.955) & (r < 1.0), tone * 0.9, tone)  # rolled rim
    bc[0:256, 0:256] = np.stack([tone * 0.985, tone * 0.995, tone * 1.02], -1)
    mm[0:256, 0:256] = np.stack([np.ones_like(r), np.ones_like(r), np.zeros_like(r), 0.58 + 0.06 * lines[idx]], -1)
    # tape pack face: oxide brown with layer rings (winding tension bands) and a glossy back-coat edge
    rings = rng.standard_normal(3000).astype(np.float32)
    rings = np.convolve(rings, np.ones(5) / 5, mode="same")
    idx2 = np.clip((r * 2999).astype(int), 0, 2999)
    t2 = 0.235 + 0.022 * rings[idx2] + 0.006 * np.sin(r * 23)
    bc[0:256, 256:512] = np.stack([t2, t2 * 0.64, t2 * 0.44], -1)
    mm[0:256, 256:512] = np.stack([np.zeros_like(r), np.ones_like(r), np.zeros_like(r), 0.5 + 0.08 * rings[idx2]], -1)
    sw = {}
    for i, (name, col, met, sm) in enumerate(SW):
        cx, cy = (i % 8) * 64, 256 + (i // 8) * 64
        noise = rng.standard_normal((64, 64, 1)).astype(np.float32) * 0.012
        bc[cy:cy + 64, cx:cx + 64] = np.clip(np.asarray(col, np.float32) / 255 + noise, 0, 1)
        mm[cy:cy + 64, cx:cx + 64] = (met, 1.0, 0.0, sm)
        sw[name] = {"index": i, "uv": [(cx + 32) / S, 1 - (cy + 32) / S]}
    OUT.mkdir(parents=True, exist_ok=True)
    Image.fromarray((np.clip(bc, 0, 1) * 255 + 0.5).astype(np.uint8)).save(OUT / "T_BoothParts_BC.png")
    Image.fromarray((np.clip(mm, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(OUT / "T_BoothParts_M.png")
    meta = {"size": S, "swatches": sw, "flange": {"uv_centre": [0.25, 0.75], "uv_radius": 124 / S},
            "tape": {"uv_centre": [0.75, 0.75], "uv_radius": 124 / S}}
    (pathlib.Path(__file__).resolve().parent / "wp09_parts_atlas.json").write_text(json.dumps(meta, indent=1))
    rd = ROOT / "RawAssets/wp09/ready"
    rel = lambda n: str((OUT / n).relative_to(rd))
    rows = [{"manifest_id": mid, "file": rel(n), "target_path": rel(n), "type": "texture", "import_notes": note}
            for mid in ("FAL_SCRAMBLER_BOX",) for n, note in (
                ("T_BoothParts_BC.png", "Shared atlas for every procedural booth part (M_BoothParts): reel flange disc, tape-pack disc, 22 flat swatches "
                 "(chrome, bakelite, brushed alu, tape oxide, PVC, cloth tape, cigarette paper/cork/ash, brass, steel...). Default, sRGB ON, Max Size 512, "
                 "DXT1 Crunched q55 (Props/**), mips on. Also used by FAL_REEL_TO_REEL, FAL_ROTARY_PHONE and PROC_BOOTH_SMALLS."),
                ("T_BoothParts_M.png", "Mask for M_BoothParts: R metallic, G AO (1), B 0, A smoothness per swatch. sRGB OFF, Max Size 512, DXT5 Crunched q60. "
                 "No normal map for this material."))]
    (rd / "rows").mkdir(parents=True, exist_ok=True)
    (rd / "rows/booth_parts.json").write_text(json.dumps(rows, indent=1))
    print("ok", OUT)


if __name__ == "__main__":
    main()
