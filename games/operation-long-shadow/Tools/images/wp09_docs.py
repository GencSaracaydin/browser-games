#!/usr/bin/env python3
"""WP09 document / insert plates baked offline (the GDD's 'TMP + RenderTexture bake' items, done in Python so they do not
wait for the editor; the editor can still re-bake live text over the blank plates that ship alongside).

  Tools/.venv/bin/python Tools/images/wp09_docs.py [teamid roster teletype leader debrief] (default: all)
Inputs: fal paper plates in RawAssets/wp09/<ID>/ (card_plate, plate), ID photos (hollis_a, sparrow_a, tinker_a),
OFL fonts from RawAssets/wp08/UI_FONT_* (Special Elite, Courier Prime, Barlow Condensed).
Outputs: RawAssets/wp09/ready/Assets/_Game/Art/Textures/Images/<sub>/T_*.png, ready/rows/doc_<ID>.json,
StreamingAssets/video/VID_FilmLeader.mp4, review sheets in RawAssets/wp09/_review/.
Typewriter text: per-glyph jitter, ink density, ribbon wear and bleed; stamps: patchy ink, rotation, multiply.
"""
import json, math, pathlib, subprocess, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

ROOT = pathlib.Path(__file__).resolve().parents[2]
RAW = ROOT / "RawAssets/wp09"
READY = RAW / "ready"
IMG = "Assets/_Game/Art/Textures/Images"
F_TYPE = str(ROOT / "RawAssets/wp08/UI_FONT_TYPEWRITER/SpecialElite-Regular.ttf")
F_COUR = str(ROOT / "RawAssets/wp08/UI_FONT_TYPEWRITER/CourierPrime-Bold.ttf")
F_COND = str(ROOT / "RawAssets/wp08/UI_FONT_CONDENSED/BarlowCondensed-SemiBold.ttf")
ROWS = {}


def row(mid, path, notes):
    rel = str(path.relative_to(READY))
    typ = "video" if rel.endswith(".mp4") else "texture"  # CONTENT_AUDIT: the leader clip is a video row
    ROWS.setdefault(mid, []).append({"manifest_id": mid, "file": rel, "target_path": rel, "type": typ, "import_notes": notes})


def flush_rows():
    for mid, rows in ROWS.items():
        (READY / "rows" / f"doc_{mid}.json").write_text(json.dumps(rows, indent=1))


def smooth_noise(rng, h, w, scale):
    n = rng.random((max(2, h // scale + 2), max(2, w // scale + 2))).astype(np.float32)
    im = Image.fromarray((n * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    return np.asarray(im, np.float32) / 255


def type_layer(size, lines, font_path, px, origin, rng, pitch=None, lead=1.55, wear=0.25, jitter=1.2):
    """Alpha mask (float HxW) of typewritten lines: monospace pitch, per-glyph offset/rotation/density."""
    W, H = size
    A = Image.new("L", (W, H), 0)
    font = ImageFont.truetype(font_path, px)
    pitch = pitch or font.getlength("M")
    x0, y0 = origin
    for li, line in enumerate(lines):
        y = y0 + li * px * lead + rng.normal(0, 0.6)
        for ci, ch in enumerate(line):
            if ch == " ":
                continue
            g = Image.new("L", (int(px * 1.6), int(px * 1.8)), 0)
            ImageDraw.Draw(g).text((int(px * 0.3), int(px * 0.2)), ch, font=font, fill=int(255 * rng.uniform(0.72, 1.0)))
            g = g.rotate(rng.normal(0, 0.7), resample=Image.BICUBIC)
            gx = int(x0 + ci * pitch + rng.normal(0, jitter * 0.5) - px * 0.3)
            gy = int(y + rng.normal(0, jitter) - px * 0.2 + (1.5 if rng.random() < 0.06 else 0))   # occasional low strike
            A.paste(ImageChops.lighter(A.crop((gx, gy, gx + g.width, gy + g.height)), g), (gx, gy))
    a = np.asarray(A, np.float32) / 255
    a = a * (1 - wear * smooth_noise(rng, H, W, 7)) * (1 - 0.35 * (rng.random((H, W)) < 0.04))   # ribbon wear, pinholes
    a = np.asarray(Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.55)), np.float32) / 255
    return np.clip(a * 1.15, 0, 1)


def stamp_layer(size, text_lines, font_path, px, centre, angle, rng, box=True, pad=0.35):
    W, H = size
    font = ImageFont.truetype(font_path, px)
    tw = max(font.getlength(t) for t in text_lines); th = px * 1.15 * len(text_lines)
    sw, sh = int(tw + px * pad * 4), int(th + px * pad * 3)
    S = Image.new("L", (sw, sh), 0); d = ImageDraw.Draw(S)
    if box:
        b = max(3, px // 9)
        d.rounded_rectangle([b, b, sw - b, sh - b], radius=px // 5, outline=255, width=b)
    for i, t in enumerate(text_lines):
        d.text(((sw - font.getlength(t)) / 2, px * pad * 1.4 + i * px * 1.15), t, font=font, fill=255)
    a = np.asarray(S, np.float32) / 255
    cover = np.clip(smooth_noise(rng, sh, sw, max(4, px // 4)) * 1.6 - 0.15, 0, 1) * (0.55 + 0.45 * smooth_noise(rng, sh, sw, max(8, px)))
    a = a * np.clip(0.35 + cover, 0, 1) * (1 - 0.6 * (rng.random((sh, sw)) < 0.05))
    S = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)).rotate(angle, expand=True, resample=Image.BICUBIC)
    L = Image.new("L", (W, H), 0)
    L.paste(S, (int(centre[0] - S.width / 2), int(centre[1] - S.height / 2)))
    return np.asarray(L, np.float32) / 255


def ink(paper, alpha, colour, strength=0.92):
    c = np.asarray(colour, np.float32) / 255
    a = (alpha * strength)[..., None]
    return paper * (1 - a) + (paper * c) * a          # multiply-style ink so paper texture shows through


def photo_finish(a, rng, grain=0.03, blur_r=0.6, vig=0.15):
    im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(blur_r))
    a = np.asarray(im, np.float32) / 255
    a = a + rng.normal(0, grain, a.shape[:2] + (1,)).astype(np.float32)
    h, w = a.shape[:2]; y, x = np.mgrid[0:h, 0:w]
    a = a * (1 - vig * (((x - w / 2) / (w / 2)) ** 2 + ((y - h / 2) / (h / 2)) ** 2))[..., None]
    return np.clip(a, 0, 1)


def save(a, rel, mode="RGB"):
    p = READY / IMG / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), mode).save(p, optimize=True)
    return p


def load(p, size):
    return np.asarray(Image.open(p).convert("RGB").resize(size, Image.LANCZOS), np.float32) / 255


# ---------------------------------------------------------------- team ID cards (CRT6)
TEAM = [("Hollis", "SUNDIAL-ACTUAL", "HOLLISTER, F.", "HT 6-1   WT 190", "BLOOD  O POS", -1.6),
        ("Sparrow", "SUNDIAL-2", "CASTELLANOS, M.", "HT 5-6   WT 128", "BLOOD  A NEG", 1.2),
        ("Tinker", "SUNDIAL-4", "DUQUESNE, W.", "HT 5-10  WT 165", "BLOOD  B POS", -0.8)]


def teamid():
    rng = np.random.default_rng(12)
    Wc, Hc = 2048, 1536
    plate = Image.open(RAW / "IMG_TEAM_ID_PHOTOS/card_plate.png").convert("RGB").resize((Wc, Hc), Image.LANCZOS)
    pa = np.asarray(plate, np.float32) / 255
    # the rusty paperclip on the plate (top-left) is re-laid over the photo so the print looks clipped on
    clip_box = (250, 20, 420, 380)
    clip = pa[clip_box[1]:clip_box[3], clip_box[0]:clip_box[2]]
    paper_ref = np.median(pa[400:600, 900:1100].reshape(-1, 3), 0)
    clip_a = np.clip((paper_ref.mean() - clip.mean(2)) / 0.25, 0, 1)
    sheets = []
    for name, cs, nm, ht, bl, rot in TEAM:
        card = pa.copy()
        ph = Image.open(RAW / f"IMG_TEAM_ID_PHOTOS/{name.lower()}_a.png").convert("L")
        w0, h0 = ph.size; ph = ph.crop((int(w0 * 0.06), int(h0 * 0.03), int(w0 * 0.94), int(h0 * 0.03 + w0 * 0.88 * 4 / 3)))
        ph = ph.resize((540, 720), Image.LANCZOS)
        pr = Image.new("RGB", (540 + 36, 720 + 36), (238, 236, 228)); pr.paste(Image.merge("RGB", [ph, ph, ph]), (18, 18))
        sh = Image.new("L", (pr.width + 40, pr.height + 40), 0); ImageDraw.Draw(sh).rectangle([20, 20, 20 + pr.width, 20 + pr.height], fill=120)
        sh = sh.filter(ImageFilter.GaussianBlur(9)).rotate(rot, expand=True, resample=Image.BICUBIC)
        prr = pr.rotate(rot, expand=True, resample=Image.BICUBIC, fillcolor=(0, 0, 0))
        mask = Image.new("L", pr.size, 255).rotate(rot, expand=True, resample=Image.BICUBIC)
        base = Image.fromarray((card * 255).astype(np.uint8))
        px, py = 170, 170
        dark = Image.new("RGB", base.size, (0, 0, 0)); sm = Image.new("L", base.size, 0); sm.paste(sh, (px - 4, py + 10))
        base = Image.composite(dark, base, sm.point(lambda v: int(v * 0.55)))
        base.paste(prr, (px, py), mask)
        card = np.asarray(base, np.float32) / 255
        # clip on top
        reg = card[clip_box[1]:clip_box[3], clip_box[0]:clip_box[2]]
        card[clip_box[1]:clip_box[3], clip_box[0]:clip_box[2]] = reg * (1 - clip_a[..., None]) + clip * clip_a[..., None]
        # typed fields on the ruled lines (right half)
        lines = [cs, nm, ht, bl]
        t = type_layer((Wc, Hc), lines, F_TYPE, 62, (1020, 780), rng, lead=1.62)
        card = ink(card, t, (40, 38, 52), 0.95)
        t2 = type_layer((Wc, Hc), ["SUBJ. PHOTO 10/84"], F_TYPE, 38, (210, 975), rng, lead=1.5)
        card = ink(card, t2, (40, 38, 52), 0.85)
        # red stamp across the photo corner + card
        st = stamp_layer((Wc, Hc), [cs], F_COND, 92, (625, 1080), rng.uniform(-11, -6), rng)
        card = ink(card, st, (196, 34, 30), 0.9)
        card = photo_finish(card, rng, grain=0.022, blur_r=0.7, vig=0.12)
        p = save(card, f"team_id/T_TeamID_{name}_BC.png")
        row("IMG_TEAM_ID_PHOTOS", p, "CRT6 team ID card (4:3, 2048x1536): photoreal ID photo derived from the WP05 head render, typed callsign/name/height/weight/blood, red callsign stamp. Default, sRGB ON, Non-Power of 2 None, Max Size 1024, Mip Maps OFF, DXT1 Crunched q60. CRT6 can cycle the three cards (2-3 s each).")
        photo = save(np.asarray(ph.convert("RGB"), np.float32) / 255, f"team_id/T_TeamID_{name}Photo_BC.png")
        row("IMG_TEAM_ID_PHOTOS", photo, "Bare 540x720 B&W ID photo (for an in-editor re-bake with live TMP). Default, sRGB ON, Max Size 512, Mip Maps OFF.")
        sheets.append(p)
    blank = save(photo_finish(pa, rng, 0.02, 0.6, 0.1), "team_id/T_TeamID_Card_BC.png")
    row("IMG_TEAM_ID_PHOTOS", blank, "Blank manila card plate (2048x1536) for an editor re-bake. Default, sRGB ON, Max Size 1024.")
    return sheets


# ---------------------------------------------------------------- codebook roster (outro push-in)
ROSTER = [("KESTREL-1", "HALVORSEN, J.", "2207"), ("KESTREL-4", "ADEYEMI, T.", "0815"), ("LANTERN-2", "MORROW, D.", "1133"),
          ("LANTERN-5", "VASQUEZ, R.", "0402"), ("HALYARD-1", "BRENNAN, K.", "1750"), ("HALYARD-3", "OKONKWO, E.", "0929"),
          ("WICKET-2", "SOLBERG, A.", "1614"), ("WICKET-6", "TRAN, M.", "0306"), ("COPPICE-1", "DELACROIX, P.", "2041"),
          ("COPPICE-4", "FENWICK, L.", "1218"), ("MERIDIAN-2", "YARDLEY, S.", "0557"), ("MERIDIAN-3", "PRUITT, G.", "1442"),
          ("SUNDIAL-3", "CALDER, N.", "0640"), ("SUNDIAL-2", "CASTELLANOS, M.", "1921")]


def dotted(a, b, c, w1=14, w2=19):
    return f"{a} " + "." * (w1 - len(a)) + f" {b} " + "." * (w2 - len(b)) + f" {c}"


def roster():
    rng = np.random.default_rng(640)
    Wp, Hp = 1536, 2048
    pa = load(RAW / "IMG_ROSTER_PAGE/plate.png", (Wp, Hp))
    blank = save(photo_finish(pa, rng, 0.015, 0.4, 0.05), "roster/T_RosterPage_Blank_BC.png")
    row("IMG_ROSTER_PAGE", blank, "Blank onion-skin plate (1536x2048) for a live TMP re-bake. Default, sRGB ON, Max Size 1024 (2048 for the push-in).")
    lines = ["74410  00640  31907  52288", "", "- 31 -", ""] + [dotted(*r) for r in ROSTER] + ["", "", "21160  09451  00640  74410"]
    t = type_layer((Wp, Hp), lines, F_TYPE, 40, (150, 230), rng, lead=1.9, wear=0.3)
    # carbon-copy softness: a faint offset ghost and slight bleed
    ghost = np.roll(np.roll(t, 2, 0), 1, 1) * 0.25
    page = ink(pa, np.clip(t + ghost, 0, 1), (28, 26, 34), 0.93)
    page = photo_finish(page, rng, 0.018, 0.5, 0.06)
    p = save(page, "roster/T_RosterPage_FS_BC.png")
    y_c3 = 230 + (4 + 12) * 40 * 1.9
    row("IMG_ROSTER_PAGE", p, f"Typed codebook roster (portrait 1536x2048, 14 callsigns; SUNDIAL-3 CALDER, N. 0640 on the 13th line at y ~{int(y_c3)} px from the top, SUNDIAL-2 CASTELLANOS, M. 1921 below it). TextureRules: *Page* = world-quad image (mips on, POT scale) and _FS = Max Size 2048 (the CRT5 push-in lands on the SUNDIAL-3 line); sRGB ON, DXT1 Crunched q60.")
    json.dump({"roster": ROSTER, "line_px": 40 * 1.9, "first_line_y": 230, "sundial3_line_index": 16}, open(RAW / "IMG_ROSTER_PAGE/layout.json", "w"), indent=1)
    return [p]


# ---------------------------------------------------------------- teletype feed (CRT1)
def teletype():
    rng = np.random.default_rng(74410)
    Wt, Ht = 1024, 2048
    pa = load(RAW / "IMG_TELETYPE_FEED/plate.png", (Wt, Ht))
    pa = pa[:, :, :]
    # vertical tiling: cross-fade the top and bottom 12 %
    k = int(Ht * 0.12)
    wgt = np.linspace(0, 1, k, dtype=np.float32)[:, None, None]
    top = pa[:k].copy(); pa[:k] = top * wgt + pa[-k:] * (1 - wgt); pa = pa[:-k]
    Ht2 = pa.shape[0]
    pa = np.asarray(Image.fromarray((pa * 255).astype(np.uint8)).resize((Wt, 2048), Image.LANCZOS), np.float32) / 255
    groups = []
    for i in range(30):
        g = [f"{rng.integers(0, 100000):05d}" for _ in range(5)]
        if i in (3, 14, 25):
            g[1:3] = ["74410", "00640"]
        if i == 21:
            g[0] = "00640"
        groups.append("  ".join(g))
    px = 34; lead = 2.0
    t = type_layer((Wt, 2048), groups, F_COUR, px, (110, 20), rng, lead=lead, wear=0.35, jitter=0.9)
    page = ink(pa, t, (35, 33, 40), 0.9)
    page = photo_finish(page, rng, 0.012, 0.35, 0.0)
    p = save(page, "teletype_feed/T_TeletypeFeed_BC.png")
    row("IMG_TELETYPE_FEED", p, "CRT1 teletype roll (1024x2048, 30 lines of five-digit groups incl. 74410 00640, tiles vertically: Wrap U Clamp / V Repeat). Scroll the V offset upward ~1 line (68 px = 0.033 UV) per carriage return with a short jerk. Default, sRGB ON, Max Size 1024, Mip Maps OFF, DXT1 Crunched q60.")
    blank = save(photo_finish(pa, rng, 0.012, 0.35, 0.0), "teletype_feed/T_TeletypePaper_BC.png")
    row("IMG_TELETYPE_FEED", blank, "Blank canary teletype paper (1024x2048, tiles vertically) for live TMP typing. Default, sRGB ON, Wrap V Repeat, Max Size 1024.")
    return [p]


# ---------------------------------------------------------------- film leader (MID-B 5..4..3)
def leader():
    rng = np.random.default_rng(543)
    W, H = 512, 384
    font = ImageFont.truetype(F_COND, 250)
    frames, plates = [], []
    fps, per = 18, 24                     # 18 fps projector, 1.33 s per number (0:12-0:16)
    for num in (5, 4, 3):
        for k in range(per):
            th = 360.0 * k / per
            im = Image.new("L", (W, H), 150); d = ImageDraw.Draw(im)
            cx, cy, R = W // 2, H // 2, 160
            # sweep: the darker wedge already swept
            d.pieslice([cx - 400, cy - 400, cx + 400, cy + 400], -90, -90 + th, fill=95)
            d.ellipse([cx - R, cy - R, cx + R, cy + R], outline=245, width=5)
            d.ellipse([cx - R + 22, cy - R + 22, cx + R - 22, cy + R - 22], outline=245, width=4)
            d.line([(0, cy), (W, cy)], fill=235, width=3); d.line([(cx, 0), (cx, H)], fill=235, width=3)
            t = str(num); tw = font.getlength(t)
            d.text((cx - tw / 2, cy - 160), t, font=font, fill=20)
            a = np.asarray(im, np.float32) / 255
            if k == 0:
                plates.append(a.copy())
            frames.append(a)
    # film damage + grain + flicker; leader frames are B&W
    out = []
    for i, a in enumerate(frames):
        a = a * (1 + rng.normal(0, 0.03))
        a = a + rng.normal(0, 0.05, a.shape).astype(np.float32)
        im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)); d = ImageDraw.Draw(im)
        for _ in range(rng.integers(2, 7)):
            x, y, r = rng.integers(0, W), rng.integers(0, H), rng.uniform(0.6, 2.4)
            d.ellipse([x - r, y - r, x + r, y + r], fill=int(rng.choice([15, 240])))
        if (i // 7) % 3 == 0:
            x = 90 + (i % 7) * 2
            d.line([(x, 0), (x + 3, H)], fill=230, width=1)
        im = im.filter(ImageFilter.GaussianBlur(0.6)).rotate(0, translate=(rng.normal(0, 0.8), rng.normal(0, 1.2)))
        g = np.asarray(im, np.float32) / 255
        out.append(np.repeat(g[..., None], 3, 2))
    for num, pl in zip((5, 4, 3), plates):
        p = save(np.repeat(pl[..., None], 3, 2), f"leader/T_FilmLeader_{num}_BC.png")
        row("IMG_FILM_LEADER", p, f"Clean countdown leader plate '{num}' (512x384, sweep at 0): for a shader-driven sweep (darken where atan2 angle < t*360) through SG_Film16mm. Default, sRGB ON, Max Size 512, Mip Maps OFF.")
    vid = READY / "Assets/StreamingAssets/video/VID_FilmLeader.mp4"
    cmd = [subprocess.check_output([sys.executable, "-c", "import imageio_ffmpeg;print(imageio_ffmpeg.get_ffmpeg_exe())"], text=True).strip(),
           "-loglevel", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(fps), "-i", "-",
           "-c:v", "libx264", "-profile:v", "main", "-pix_fmt", "yuv420p", "-b:v", "500k", "-preset", "slow", "-movflags", "+faststart", "-an", str(vid)]
    vid.parent.mkdir(parents=True, exist_ok=True)
    pr = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for f in out:
        pr.stdin.write((np.clip(f, 0, 1) * 255).astype(np.uint8).tobytes())
    pr.stdin.close(); pr.wait()
    row("IMG_FILM_LEADER", vid, "MID-B shot 3 countdown 5-4-3 (4.0 s, 18 fps, 512x384 H.264, B&W, with dust/scratches/flicker; one number per 1.33 s, the sweep completes per number: sync the leader beeps to frames 0/24/48). Ships from StreamingAssets (VideoPlayer.url), like the other VID_ clips; alternatively drive the plates above through SG_Film16mm.")
    sheet = Image.new("RGB", (6 * 258, 3 * 194), (20, 20, 20))
    for j, i in enumerate(list(range(0, 72, 12))[:6] + list(range(6, 72, 12))[:6] + list(range(3, 72, 12))[:6]):
        sheet.paste(Image.fromarray((out[i] * 255).astype(np.uint8)).resize((256, 192)), ((j % 6) * 258, (j // 6) * 194))
    sheet.save(RAW / "_review/IMG_FILM_LEADER.png")
    return []


# ---------------------------------------------------------------- debrief dossier
def debrief():
    rng = np.random.default_rng(13)
    Wd, Hd = 2048, 1536
    pa = load(RAW / "IMG_DEBRIEF_DOSSIER/plate.png", (Wd, Hd))
    bg = save(photo_finish(pa, rng, 0.015, 0.4, 0.12), "debrief/T_DebriefBackground_FS_UI_BC.png")
    row("IMG_DEBRIEF_DOSSIER", bg, "Debrief dossier background (2048x1536): blank classified sheet in a manila folder; the AAR text types live in TMP over the white area (inside x 9-91 %, y 7-93 %), then the CRT panel shader. UI sprite by name (_UI; _FS = 2048 cap in TextureRules): Sprite, sRGB ON, Max Size 2048, Mip Maps OFF, DXT1 Crunched q60.")
    # redaction bars: black felt-marker strokes (alpha), three variants
    for v in range(3):
        w, h = 1024, 96
        a = np.zeros((h, w), np.float32)
        y, x = np.mgrid[0:h, 0:w].astype(np.float32)
        x0, x1 = rng.uniform(4, 18), w - rng.uniform(4, 30)
        for s in range(3):                                   # overlapping marker passes
            off = rng.normal(0, 3); tilt = rng.normal(0, 0.006)
            yc = h / 2 + off + (x - w / 2) * tilt
            half = 30 + rng.normal(0, 1.5) + 0.8 * np.sin(x / rng.uniform(60, 140))
            edge = np.clip(half - np.abs(y - yc), 0, 1.4) / 1.4
            xs = np.clip((x - x0 - s * 6) / 10, 0, 1) * np.clip((x1 - s * 9 - x) / 14, 0, 1)
            a = np.maximum(a, edge * xs * rng.uniform(0.96, 1.0))
        streaks = np.repeat(smooth_noise(rng, h, 1, 2), w, 1)          # marker felt streaks run along the stroke
        a *= 0.94 + 0.06 * streaks
        rgba = np.zeros((h, w, 4), np.float32); rgba[..., :3] = 0.035; rgba[..., 3] = a
        p = save(rgba, f"debrief/T_RedactionBar{v + 1}_UI_BC.png", "RGBA")
        row("IMG_DEBRIEF_DOSSIER", p, "Redaction bar (felt marker, 1024x96, alpha). UI: Sprite (Single), sRGB ON, Alpha Is Transparency ON, Max Size 1024, Mip Maps OFF, DXT5 Crunched q60. Wipe it in with an Image fill (Horizontal, origin Left) over a personnel line.")
    # stamp
    Ws, Hs = 1600, 420
    st = stamp_layer((Ws, Hs), ["PROGRAM LONG SHADOW", "ASSETS ACTIVE: 13"], F_COND, 120, (Ws / 2, Hs / 2), 0.0, rng, pad=0.3)
    rgba = np.zeros((Hs, Ws, 4), np.float32); rgba[..., :3] = np.array([0.72, 0.1, 0.09], np.float32); rgba[..., 3] = np.clip(st * 1.05, 0, 1)
    p = save(rgba, "debrief/T_StampAssetsActive_UI_BC.png", "RGBA")
    row("IMG_DEBRIEF_DOSSIER", p, "Final stamp 'PROGRAM LONG SHADOW / ASSETS ACTIVE: 13' (1600x420, red ink, patchy, alpha; rotate -6..-10 deg when it lands, 1.15 -> 1.0 scale in 0.08 s with a thump). UI Sprite, sRGB ON, Alpha Is Transparency ON, Max Size 1024, DXT5 Crunched q60.")
    # strike-through stroke for the 'HOSTILE -> UNDER REVIEW' retype
    w, h = 512, 24
    a = np.zeros((h, w), np.float32); y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    yc = h / 2 + (x - w / 2) * 0.004
    a = np.clip(3.2 - np.abs(y - yc), 0, 1.6) / 1.6 * (0.85 + 0.15 * smooth_noise(rng, h, w, 5))
    rgba = np.zeros((h, w, 4), np.float32); rgba[..., :3] = 0.06; rgba[..., 3] = a
    p = save(rgba, "debrief/T_StrikeThrough_UI_BC.png", "RGBA")
    row("IMG_DEBRIEF_DOSSIER", p, "Typed strike-through line (512x24, alpha) for HOSTILE -> UNDER REVIEW; or strike with TMP <s>. UI Sprite, sRGB ON, Alpha Is Transparency ON, Mip Maps OFF.")
    return [bg]


if __name__ == "__main__":
    which = sys.argv[1:] or ["teamid", "roster", "teletype", "leader", "debrief"]
    outs = []
    for w in which:
        outs += {"teamid": teamid, "roster": roster, "teletype": teletype, "leader": leader, "debrief": debrief}[w]() or []
    flush_rows()
    print("\n".join(str(o) for o in outs))
