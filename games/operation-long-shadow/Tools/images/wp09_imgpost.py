#!/usr/bin/env python3
"""WP09 image post: picks a take per IMG_* id, crops/resizes it, applies the period photo treatment and writes the
import-ready PNG plus index rows.

  Tools/.venv/bin/python Tools/images/wp09_imgpost.py [--ids IMG_A,IMG_B] [--sheet]
Reads RawAssets/wp09/<ID>/<take>.png (fal outputs), writes
  RawAssets/wp09/ready/Assets/_Game/Art/Textures/Images/<file>
  RawAssets/wp09/ready/rows/img_<ID>.json
  RawAssets/wp09/_review/img_final.png (contact sheet, with --sheet)
Treatments (all deterministic, seeded per id):
  crt43     centre-crop to 4:3, 2048x1536 (CRT feeds; SG_CRTScreen adds curvature/scanlines in game)
  bw_recon  crt43 + greyscale, toe/shoulder contrast, Tri-X grain, light vignette, fiducial marks at the edge midpoints
  colneg    crt43 + 400 ASA colour-negative look: lifted blacks, warm shift, soft 0.7 px, colour grain, halation
  polaroid  crop the SX-70 image area out of the generated print (frame comes from UI_POLAROID_FRAME), 1024x1024
  doc       portrait document plate, 1536x2048 (3:4)
"""
import argparse, json, pathlib
import numpy as np
from PIL import Image, ImageFilter

ROOT = pathlib.Path(__file__).resolve().parents[2]
RAW = ROOT / "RawAssets/wp09"
READY = RAW / "ready"
IMG_DIR = "Assets/_Game/Art/Textures/Images"

PICKS = {
    "IMG_MAP_SARDAHN": {"take": "take_b", "file": "T_MapSardahn_BC.png", "treat": "crt43",
                        "why": "take_b: cleaner period sketch-map read, one circled town with arrows; take_a has two compass roses"},
    "IMG_RECON_STATION": {"take": "take_c", "file": "T_ReconStation_BC.png", "treat": "bw_recon",
                          "why": "take_c (round 2): true oblique long-lens recon look; takes a/b read as tilt-shift miniatures"},
    "IMG_HARROU_STILL": {"take": "take_a", "file": "T_HarrouStill_BC.png", "treat": "crt43",
                         "why": "take_a: Harrou larger in frame, face scratched out, orange armbands, square clock tower"},
    "IMG_AIRPORT_PLATE": {"take": "take_a", "file": "T_AirportPlate_BC.png", "treat": "colneg",
                          "why": "take_a: open floor in the middle distance for Sparrow; take_b is busier"},
    "IMG_SURVEIL_SPARROW": {"take": "take_a", "file": "T_SurveilSparrow_BC.png", "treat": "colneg", "crop": [300, 470, 1660, 1490],
                            "why": "fal edit of the airport plate with the WP05 Sparrow renders as identity reference"},
    "IMG_INTEL_LEDGERS": {"take": "take_a", "file": "T_IntelLedgers_BC.png", "treat": "polaroid",
                          "why": "take_a: two ledgers, stamp and pen, numerals only"},
    "IMG_INTEL_REELS": {"take": "take_a", "file": "T_IntelReels_BC.png", "treat": "polaroid",
                        "why": "take_a: blank labels, loose reel with leader"},
    "IMG_INTEL_CODEBOOK": {"take": "take_a", "file": "T_IntelCodebook_BC.png", "treat": "polaroid",
                           "why": "take_a: gloved hand, safe, illegible number groups"},
    "IMG_INTEL_SCHEDULE": {"take": "take_a", "file": "T_IntelSchedule_BC.png", "treat": "polaroid",
                           "why": "take_a: typed lines fully illegible; take_b has pseudo-text that reads almost like words"},
    "IMG_INTEL_FILMLABEL": {"take": "take_a", "file": "T_IntelFilmLabel_BC.png", "treat": "polaroid",
                            "why": "take_a: blank label with one typed underline for the S-3 overlay"},
    "IMG_LEDGER_PAGE": {"take": "take_a", "file": "T_LedgerPage_BC.png", "treat": "doc",
                        "why": "take_a: most of the page is blank ruled columns for the TMP overlay; take_b has a white scan margin"},
    "IMG_CODEBOOK_PAGE": {"take": "take_b", "file": "T_CodebookPage_BC.png", "treat": "doc",
                          "why": "take_b: real typewriter strike, onion-skin, punched holes; take_a uses a modern monospace"},
}


def rng_for(key):
    return np.random.default_rng(sum(map(ord, key)) * 7919)


def crop_aspect(im, aspect):
    w, h = im.size
    if w / h > aspect:
        nw = int(round(h * aspect)); x0 = (w - nw) // 2
        return im.crop((x0, 0, x0 + nw, h))
    nh = int(round(w / aspect)); y0 = (h - nh) // 2
    return im.crop((0, y0, w, y0 + nh))


def grain(a, rng, amount, size=1.0, mono=True):
    """a: float32 HxWx3 in 0..1. Luminance-weighted gaussian grain, softened to 'size' px."""
    h, w, _ = a.shape
    n = rng.standard_normal((h, w, 1 if mono else 3)).astype(np.float32)
    if size > 1.01 or size < 0.99:
        sm = Image.fromarray(np.clip((n[..., 0] if mono else n) * 40 + 128, 0, 255).astype(np.uint8))
        sm = sm.filter(ImageFilter.GaussianBlur(size * 0.6))
        n = (np.asarray(sm, np.float32) - 128) / 40 * (1.0 + 0.6 * (size - 1))
        n = n[..., None] if n.ndim == 2 else n
    lum = a.mean(axis=2, keepdims=True)
    wgt = 0.35 + 2.6 * lum * (1 - lum)          # strongest in the midtones, like silver halide
    return np.clip(a + n * amount * wgt, 0, 1)


def vignette(a, strength):
    h, w, _ = a.shape
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    r = np.sqrt(((x - w / 2) / (w / 2)) ** 2 + ((y - h / 2) / (h / 2)) ** 2) / np.sqrt(2)
    return a * (1 - strength * np.clip(r, 0, 1) ** 2.2)[..., None]


def tone(a, toe=0.03, shoulder=0.97, gamma=1.0):
    a = np.clip((a - toe) / (shoulder - toe), 0, 1)
    return a ** gamma


def fiducials(a):
    """Reconnaissance-camera fiducial marks: small crosses at the four edge midpoints and corner ticks."""
    h, w, _ = a.shape
    s = max(6, w // 160); t = max(2, w // 900)
    for cx, cy in ((w // 2, s * 2), (w // 2, h - s * 2), (s * 2, h // 2), (w - s * 2, h // 2)):
        a[cy - t:cy + t, cx - s:cx + s] = 0.92
        a[cy - s:cy + s, cx - t:cx + t] = 0.92
    for cx, cy, dx, dy in ((s, s, 1, 1), (w - s, s, -1, 1), (s, h - s, 1, -1), (w - s, h - s, -1, -1)):
        x0, x1 = sorted((cx, cx + dx * s * 3)); y0, y1 = sorted((cy, cy + dy * s * 3))
        a[min(cy, cy + dy * t):max(cy, cy + dy * t) + 1, x0:x1] = 0.92
        a[y0:y1, min(cx, cx + dx * t):max(cx, cx + dx * t) + 1] = 0.92
    return a


def halation(a, thresh=0.8, radius=18, tint=(1.0, 0.45, 0.25), amount=0.18):
    lum = a.mean(axis=2)
    hi = np.clip((lum - thresh) / (1 - thresh), 0, 1)
    im = Image.fromarray((hi * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius))
    g = np.asarray(im, np.float32)[..., None] / 255
    return np.clip(a + g * np.array(tint, np.float32) * amount, 0, 1)


def polaroid_inner(im):
    """Find the SX-70 image area: the print border is bright and flat; the image area starts where the border ends."""
    g = np.asarray(im.convert("L"), np.float32)
    h, w = g.shape
    def edge(profile, n):
        # border rows/cols: mean > 185 and low std; image starts at the first index where it fails for 6 in a row
        for i in range(int(n * 0.25)):
            if profile[i:i + 6].max() < 1:
                return i
        return int(n * 0.05)
    bright_row = np.array([1 if (g[i, int(w * .2):int(w * .8)].mean() > 175 and g[i, int(w * .2):int(w * .8)].std() < 26) else 0 for i in range(h)])
    bright_col = np.array([1 if (g[int(h * .2):int(h * .8), j].mean() > 175 and g[int(h * .2):int(h * .8), j].std() < 26) else 0 for j in range(w)])
    top = edge(bright_row, h); bot = h - edge(bright_row[::-1], h)
    lef = edge(bright_col, w); rig = w - edge(bright_col[::-1], w)
    pad = int(0.012 * w)
    box = (lef + pad, top + pad, rig - pad, bot - pad)
    bw, bh = box[2] - box[0], box[3] - box[1]
    side = min(bw, bh)
    cx, cy = (box[0] + box[2]) // 2, (box[1] + box[3]) // 2
    return (cx - side // 2, cy - side // 2, cx + side // 2, cy + side // 2)


def process(iid, P):
    src = RAW / iid / f"{P['take']}.png"
    im = Image.open(src).convert("RGB")
    rng = rng_for(iid)
    t = P["treat"]
    meta = {}
    if P.get("crop"):
        im = im.crop(tuple(P["crop"]))   # tighter long-lens framing
        meta["crop_box"] = P["crop"]
    if t in ("crt43", "bw_recon", "colneg"):
        im = crop_aspect(im, 4 / 3).resize((2048, 1536), Image.LANCZOS)
        a = np.asarray(im, np.float32) / 255
        if t == "bw_recon":
            l = (a * np.array([0.30, 0.59, 0.11], np.float32)).sum(2, keepdims=True)
            a = np.repeat(l, 3, 2)
            a = tone(a, 0.02, 0.985, 1.08)
            a = vignette(a, 0.22)
            a = grain(a, rng, 0.075, 1.25)
            a = fiducials(a)
        elif t == "colneg":
            a = tone(a, -0.035, 1.0, 1.0)             # lifted blacks
            a = a * np.array([1.03, 1.0, 0.95], np.float32) + np.array([0.012, 0.008, 0.0], np.float32)
            a = halation(a)
            im2 = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7))
            a = np.asarray(im2, np.float32) / 255
            a = grain(a, rng, 0.05, 1.4, mono=False)
            a = grain(a, rng, 0.035, 1.0)
            a = vignette(a, 0.18)
        im = Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8))
        meta["size"] = [2048, 1536]
        notes = ("CRT/cinematic plate, 4:3. Texture Type Default, sRGB ON, Non-Power of 2: None (keep 4:3), Max Size 1024 "
                 "(2048 only if shown full-screen), Wrap Clamp, Mip Maps OFF (shown on RT_CRT/UI only), DXT1 Crunched q60 "
                 "(TextureImportRules Art/Textures/Images/**). Raw photo without bezel: SG_CRTScreen adds curvature, scanlines and phosphor.")
    elif t == "polaroid":
        box = polaroid_inner(im)
        meta["crop_box"] = box
        im = im.crop(box).resize((1024, 1024), Image.LANCZOS)
        meta["size"] = [1024, 1024]
        notes = ("Intel polaroid photo area only (square SX-70 image; the white frame is UI_POLAROID_FRAME). Texture Type Default "
                 "(or Sprite if IntelCardView uses Image), sRGB ON, Max Size 512 is enough for the 2.5 s card (1024 for close-ups), "
                 "Wrap Clamp, Mip Maps OFF, DXT1 Crunched q60.")
    elif t == "doc":
        im = crop_aspect(im, 3 / 4).resize((1536, 2048), Image.LANCZOS)
        meta["size"] = [1536, 2048]
        notes = ("Document plate (portrait 3:4) under TMP text. TextureRules: *Page* = world-quad image (Default, mips on, POT scale), "
                 "sRGB ON, Max Size 1024, Wrap Clamp, DXT1 Crunched q60.")
    out = READY / IMG_DIR / P["file"]
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out, optimize=True)
    rel = str(out.relative_to(READY))
    row = {"manifest_id": iid, "file": rel, "target_path": rel, "type": "texture",
           "import_notes": notes + f" Source: RawAssets/wp09/{iid}/{P['take']}.png ({P['why']}); treatment '{t}'.",
           "manifest_target_path": next((e["target_path"] for e in json.loads((ROOT / 'Docs/ASSET_MANIFEST.json').read_text()) if e["id"] == iid), None)}
    (READY / "rows").mkdir(parents=True, exist_ok=True)
    (READY / "rows" / f"img_{iid}.json").write_text(json.dumps([row], indent=1))
    print(iid, "->", rel, meta)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids"); ap.add_argument("--sheet", action="store_true")
    a = ap.parse_args()
    ids = a.ids.split(",") if a.ids else list(PICKS)
    outs = [process(i, PICKS[i]) for i in ids if (RAW / i / f"{PICKS[i]['take']}.png").exists()]
    if a.sheet:
        import subprocess, sys
        subprocess.run([sys.executable, str(ROOT / "Tools/images/contact.py"), str(RAW / "_review/img_final.png")] +
                       [str(o) for o in outs] + ["--cell", "640", "--cols", "3"], check=True)


if __name__ == "__main__":
    main()
