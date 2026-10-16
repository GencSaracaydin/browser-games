#!/usr/bin/env python3
"""Nano-banana plates -> poster decal atlas (DCL_POSTER_ART) and the look-dev keyframe sheet (IMG_CONCEPT_KEYFRAMES). WP10_FAL.

  Tools/.venv/bin/python Tools/mesh/props/image_plates.py posters
  Tools/.venv/bin/python Tools/mesh/props/image_plates.py keyframes

posters: poster_a/b/c (2:3) -> T_Decal_PosterArt_BC.png 1536 x 768 (three 512 x 768 cells), alpha = paper with slightly
ragged edges, a torn corner and a few pin holes (aged paper pasted on walls); layout json with UV rects; the empty cream
title band at the bottom of each poster is where SignBaker / DCL_POSTERS_COMPOSED sets the Sardani titles.
keyframes: kf1..kf5 (16:9) -> concept_keyframes.png 2048 x 1728 sheet (2 per row, 5th centred) + each frame at 1024 wide.
"""
import json, pathlib, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import gaussian_filter

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
RAW = ROOT / "RawAssets/wp10/fal"
READY = ROOT / "RawAssets/wp10/ready"
rel = lambda p: str(p.relative_to(READY))


def ragged_alpha(w, h, seed, torn=None):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.minimum.reduce([x, w - 1 - x, y, h - 1 - y])            # distance to the border in px
    n = gaussian_filter(rng.standard_normal((h, w)).astype(np.float32), 3) * 6 + \
        gaussian_filter(rng.standard_normal((h, w)).astype(np.float32), 1) * 2
    a = np.clip((d - 4 + n) / 2.0, 0, 1)
    if torn:   # torn-off corner: a jagged diagonal cut
        cx, cy, r = torn
        cut = (np.abs(x - cx) + np.abs(y - cy)) < r + gaussian_filter(rng.standard_normal((h, w)).astype(np.float32), 2) * 14
        a[cut] = 0
    for _ in range(2):   # pin / nail holes near the top corners
        px, py = rng.integers(14, 40), rng.integers(12, 34)
        for qx in (px, w - px):
            a[(x - qx) ** 2 + (y - py) ** 2 < 9] = 0
    return a


def posters():
    names = ["poster_a", "poster_b", "poster_c"]
    labels = ["salt_flats_expedition", "detective_streetlamp", "balcony_romance"]
    cw, ch = 512, 768
    atlas = np.zeros((ch, cw * 3, 4), np.float32)
    layout = {"texture": "T_Decal_PosterArt_BC", "size": [cw * 3, ch], "elements": []}
    for i, nm in enumerate(names):
        im = Image.open(RAW / "DCL_POSTER_ART" / f"{nm}.png").convert("RGB").resize((cw, ch), Image.LANCZOS)
        rgb = np.asarray(im).astype(np.float32) / 255.0
        a = ragged_alpha(cw, ch, 100 + i, torn=(cw - 1, ch - 1, 70) if i == 1 else None)
        atlas[:, i * cw:(i + 1) * cw, :3] = rgb
        atlas[:, i * cw:(i + 1) * cw, 3] = a
        layout["elements"].append({"name": labels[i], "px": [i * cw, 0, (i + 1) * cw, ch],
                                   "uv": [round(i / 3, 5), 0.0, round((i + 1) / 3, 5), 1.0],
                                   "title_band_uv_local": [0.04, 0.03, 0.96, 0.19]})
    layout["note"] = ("Real size ~0.70 x 1.05 m (one-sheet). URP Decal Projector UV Offset/Scale per element, or a quad with these UVs. "
                      "Titles: SignBaker composes Sardani glyph titles into the empty band (title_band_uv_local, v up) -> DCL_POSTERS_COMPOSED.")
    tdir = READY / "Assets/_Game/Art/Textures/Decals/poster_art"; tdir.mkdir(parents=True, exist_ok=True)
    bc = tdir / "T_Decal_PosterArt_BC.png"; lay = tdir / "T_Decal_PosterArt_layout.json"
    Image.fromarray(np.clip(atlas * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA").save(bc)
    lay.write_text(json.dumps(layout, indent=1))
    rows = [{"manifest_id": "DCL_POSTER_ART", "file": rel(bc), "target_path": rel(bc), "type": "texture_decal_basecolor",
             "import_notes": "Poster plates atlas 1536 x 768 (3 x 512 x 768): sRGB ON, Alpha Is Transparency ON, Max Size 2048 (keeps 1536), "
             "DXT5 Crunched q60, mips on, wrap Clamp. Alpha = paper with ragged edges, torn corner (poster 2) and pin holes. URP Decal "
             "Projector or alpha-clipped quad on walls. Generated with google/nano-banana-2.1 (no text; guardrail-reviewed: invented films, "
             "no real script, no religious buildings). Titles are added in-engine (Sardani glyphs) by SignBaker.", "tool": "image_plates"},
            {"manifest_id": "DCL_POSTER_ART", "file": rel(lay), "target_path": rel(lay), "type": "data_json",
             "import_notes": "Atlas layout (TextAsset): element rects + the blank title band per poster.", "tool": "image_plates"}]
    (RAW / "DCL_POSTER_ART" / "ready_PosterArt.json").write_text(json.dumps(rows, indent=1))
    chk = np.dstack([np.full((ch, cw * 3), 0.55)] * 3)
    comp = atlas[..., :3] * atlas[..., 3:] + chk * (1 - atlas[..., 3:])
    Image.fromarray((comp * 255).astype(np.uint8)).save(RAW / "_review" / "DCL_POSTER_ART__atlas.png")
    print("posters ok", bc)


def keyframes():
    names = ["kf1_market", "kf2_roofs", "kf3_relay", "kf4_lz", "kf5_booth"]
    caps = ["Long Market, golden hour (Beat 3)", "Laundry Roofs, sunset (Beat 5)", "Relay Station yard, afterglow (Beat 6)",
            "Pitch LZ, blue hour (Beat 10)", "Booth, underground (intro/MID-B)"]
    fw, fh = 1024, 576
    sheet = Image.new("RGB", (fw * 2, (fh + 40) * 3), (18, 18, 18))
    d = ImageDraw.Draw(sheet); f = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 22)
    for i, nm in enumerate(names):
        im = Image.open(RAW / "IMG_CONCEPT_KEYFRAMES" / f"{nm}.png").convert("RGB")
        w, h = im.size
        m = int(0.015 * w)          # trim the faint film border some frames carry
        im = im.crop((m, m, w - m, h - m)).resize((fw, fh), Image.LANCZOS)
        x = (i % 2) * fw if i < 4 else fw // 2
        y = (i // 2) * (fh + 40)
        sheet.paste(im, (x, y + 40)); d.text((x + 10, y + 8), caps[i], fill=(230, 220, 180), font=f)
    tdir = READY / "Assets/_Game/Art/Textures/Images"; tdir.mkdir(parents=True, exist_ok=True)
    out = tdir / "concept_keyframes.png"
    sheet.save(out)
    rows = [{"manifest_id": "IMG_CONCEPT_KEYFRAMES", "file": rel(out), "target_path": rel(out), "type": "texture_reference",
             "import_notes": "Look-development target sheet (5 frames) for WP10 LookCapture reviews: Texture Type Default, sRGB ON, "
             "Max Size 2048, no mips. EDITOR-ONLY reference: do not reference from shipped content (keep out of Addressables). "
             "Sources (2K each): RawAssets/wp10/fal/IMG_CONCEPT_KEYFRAMES/kf*.png.", "tool": "image_plates"}]
    (RAW / "IMG_CONCEPT_KEYFRAMES" / "ready_ConceptKeyframes.json").write_text(json.dumps(rows, indent=1))
    print("keyframes ok", out)


if __name__ == "__main__":
    {"posters": posters, "keyframes": keyframes}[sys.argv[1]]()
