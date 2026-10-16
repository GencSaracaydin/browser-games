#!/usr/bin/env python3
"""WP08 UI content (WP09_ART run): HUD icons, damage vignette, procedural HUD shapes/overlays/panels, OFL fonts.

  Tools/.venv/bin/python Tools/images/wp08_ui.py [icons vignette shapes polaroid binoculars doorgun clickgate letterbox pause fonts]
Writes RawAssets/wp08/ready/Assets/_Game/UI/Sprites/<group>/T_UI_*.png, ready/Assets/_Game/Art/Fonts/*.ttf (+ licences),
ready/rows/<group>.json (import rows) and review sheets in RawAssets/wp08/_review/. Shapes are drawn at 4x and
downsampled (clean anti-aliased edges); tintable shapes are pure white with alpha.
"""
import json, math, pathlib, shutil, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = pathlib.Path(__file__).resolve().parents[2]
RAW = ROOT / "RawAssets/wp08"
READY = RAW / "ready"
SPR = "Assets/_Game/UI/Sprites"
ROWS = {}
SS = 4


def row(mid, p, notes, group):
    rel = str(p.relative_to(READY))
    ROWS.setdefault(group, []).append({"manifest_id": mid, "file": rel, "target_path": rel, "type": "sprite" if "/Sprites/" in rel else "font",
                                       "import_notes": notes})


SPRITE = ("Sprite (2D and UI), Single, sRGB ON, Alpha Is Transparency ON, Mip Maps OFF, Wrap Clamp, Filter Bilinear, "
          "Non-Power of 2 None (TextureRules UI/Sprites: DXT5 Crunched q70, RGBA32 when <= 128 px). ")


def canvas(w, h):
    return Image.new("L", (w * SS, h * SS), 0)


def finish(a_img, w, h, rgb=(255, 255, 255)):
    a = a_img.resize((w, h), Image.LANCZOS)
    out = Image.new("RGBA", (w, h), rgb + (0,))
    out.putalpha(a)
    return out


def save(img, group, name):
    p = READY / SPR / group / f"{name}.png"
    p.parent.mkdir(parents=True, exist_ok=True)
    img.save(p, optimize=True)
    return p


# ---------------------------------------------------------------- icons (fal gpt-image sheet -> 8 sprites)
ICONS = ["Frag", "Intel", "Radio", "FireAuto", "FireSemi", "Interact", "Crate", "Binoculars"]


def icons():
    src = Image.open(RAW / "UI_HUD_ICONS/gpt_t1.png").convert("RGBA")
    W, H = src.size; cw, ch = W // 4, H // 2
    outs = []
    for i, name in enumerate(ICONS):
        cell = src.crop(((i % 4) * cw, (i // 4) * ch, (i % 4 + 1) * cw, (i // 4 + 1) * ch))
        a = np.asarray(cell, np.float32) / 255
        alpha = a[..., 3] * np.clip((a[..., :3].mean(2) - 0.35) / 0.4, 0, 1)        # drop grey fringes
        alpha[alpha < 0.05] = 0
        ys, xs = np.nonzero(alpha > 0.1)
        x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
        side = int(max(x1 - x0, y1 - y0) * 1.12)
        cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
        al = Image.fromarray((alpha * 255).astype(np.uint8))
        sq = Image.new("L", (side, side), 0)
        sq.paste(al.crop((cx - side // 2, cy - side // 2, cx + side // 2, cy + side // 2)), (0, 0))
        img = finish(sq, 128, 128)
        p = save(img, "icons", f"T_UI_Icon_{name}")
        variant = {"Frag": "frag", "Intel": "intel", "Radio": "radio", "FireAuto": "firemode_auto", "FireSemi": "firemode_semi",
                   "Interact": "interact", "Crate": "crate", "Binoculars": "(extra) binoculars"}[name]
        row("UI_HUD_ICONS", p, SPRITE + f"128x128 white pictogram ({variant}), tint in UI (amber/white/red). fal openai/gpt-image-2.5 sheet RawAssets/wp08/UI_HUD_ICONS/gpt_t1.png, alpha cleaned.", "icons")
        outs.append(p)
    return outs


# ---------------------------------------------------------------- damage vignette
def vignette():
    src = Image.open(RAW / "UI_DAMAGE_VIGNETTE/gpt_t1.png").convert("RGBA").resize((1024, 512), Image.LANCZOS)
    a = np.asarray(src, np.float32) / 255
    y, x = np.mgrid[0:512, 0:1024].astype(np.float32)
    e = np.sqrt(((x - 512) / 512) ** 2 + ((y - 256) / 256) ** 2)
    keep = np.clip((e - 0.55) / 0.35, 0, 1) ** 1.5                 # guarantee a clean centre (no grit over the crosshair)
    a[..., 3] *= keep
    # gpt-image painted the grit pale (reads as fog in game): keep the blood, turn everything else into dark grime
    red = np.clip((a[..., 0] - a[..., 1] - 0.06) / 0.15, 0, 1)[..., None]
    lum = a[..., :3].mean(2, keepdims=True)
    grime = np.array([0.11, 0.095, 0.08], np.float32) * (0.7 + 0.6 * (1 - lum))
    a[..., :3] = a[..., :3] * red + grime * (1 - red)
    a[..., 3] *= 0.85 + 0.15 * red[..., 0]
    p = save(Image.fromarray((a * 255).astype(np.uint8), "RGBA"), "vignette", "T_UI_DamageVignette")
    row("UI_DAMAGE_VIGNETTE", p, SPRITE + "1024x512 full-screen overlay (stretch to the canvas, Image.preserveAspect off); alpha = 0 in the central ellipse. Drive Image.color.a from 0 (>= 50 % HP) to 1 (0 % HP); desaturation via IScreenFX. Source fal gpt-image-2.5 (gpt_t1), centre cleared.", "vignette")
    return [p]


# ---------------------------------------------------------------- HUD shapes
def shapes():
    outs = []
    def poly(name, w, h, pts, width=None, note=""):
        c = canvas(w, h); d = ImageDraw.Draw(c)
        P = [(x * SS, y * SS) for x, y in pts]
        if width:
            d.line(P + [P[0]], fill=255, width=width * SS, joint="curve")
        else:
            d.polygon(P, fill=255)
        p = save(finish(c, w, h), "shapes", name); row("UI_SHAPES", p, SPRITE + note, "shapes"); outs.append(p)
    poly("T_UI_DiamondFill", 64, 64, [(32, 4), (60, 32), (32, 60), (4, 32)], note="Objective marker diamond (filled), 64 px, drawn at 28 px; tint amber #E3A33B / pale green #A6E3B4 / red #E04B3B.")
    poly("T_UI_DiamondOutline", 64, 64, [(32, 6), (58, 32), (32, 58), (6, 32)], width=5, note="Objective diamond outline (5 px at 64), for the off-screen / ADS 30 % state.")
    poly("T_UI_Chevron", 64, 64, [(6, 18), (32, 44), (58, 18), (58, 30), (32, 56), (6, 30)], note="FOLLOW chevron (points down), 64 px; float over a squadmate.")
    poly("T_UI_EdgeArrow", 64, 64, [(12, 8), (58, 32), (12, 56), (22, 32)], note="Edge-clamp arrow pointing +X (right); rotate to the marker bearing.")
    poly("T_UI_GrenadeArrow", 64, 64, [(32, 4), (56, 40), (40, 36), (40, 60), (24, 60), (24, 36), (8, 40)], note="Grenade indicator arrow pointing up (+Y) for the 160 px ring; rotate to the bearing, frag icon beside it.")
    # line / tick (9-slice)
    c = canvas(16, 64); ImageDraw.Draw(c).rounded_rectangle([3 * SS, 2 * SS, 13 * SS, 62 * SS], radius=4 * SS, fill=255)
    p = save(finish(c, 16, 64), "shapes", "T_UI_Line"); outs.append(p)
    row("UI_SHAPES", p, SPRITE + "Rounded bar 16x64, Sprite Border L4 R4 T6 B6 (9-slice): crosshair lines, compass ticks (major 2x14, minor 2x8 px), hit-marker ticks (rotate 45 deg), heat-bar fill.", "shapes")
    # hit marker (4 diagonal ticks)
    c = canvas(128, 128); d = ImageDraw.Draw(c)
    for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
        d.line([((64 + sx * 18) * SS, (64 + sy * 18) * SS), ((64 + sx * 44) * SS, (64 + sy * 44) * SS)], fill=255, width=7 * SS)
    p = save(finish(c, 128, 128), "shapes", "T_UI_HitMarker"); outs.append(p)
    row("UI_SHAPES", p, SPRITE + "Hit marker (4 diagonal ticks), 128 px drawn at ~40 px; 0.12 s; kill = red #E04B3B and 1.2x for 0.2 s.", "shapes")
    # damage arc: 60 deg arc at the top of a 256 ring, alpha falls off at the ends and outward
    W = 256; c = np.zeros((W * SS, W * SS), np.float32)
    y, x = np.mgrid[0:W * SS, 0:W * SS].astype(np.float32) / SS
    r = np.hypot(x - 128, y - 128); ang = np.degrees(np.arctan2(x - 128, 128 - y))
    band = np.clip(1 - np.abs(r - 104) / 12, 0, 1) ** 0.7
    ends = np.clip((30 - np.abs(ang)) / 14, 0, 1)
    c = band * ends
    p = save(finish(Image.fromarray((c * 255).astype(np.uint8)), W, W), "shapes", "T_UI_DamageArc"); outs.append(p)
    row("UI_SHAPES", p, SPRITE + "Damage direction arc: 60 deg segment at the top of a 256 px ring (radius 104 px = the 160 px ring at 0.77 scale); rotate about the centre to the source bearing, tint red, fade 1.5 s.", "shapes")
    for name, rr, wdt, note in (("T_UI_Ring", 118, 5, "Thin ring 256 px (world rings: drill %, LZ metres)."),
                                ("T_UI_RingFill", 110, 16, "Thick ring 256 px for Image.Filled Radial360 (hold-to-interact radial, drill progress).")):
        c = canvas(256, 256); ImageDraw.Draw(c).ellipse([(128 - rr) * SS, (128 - rr) * SS, (128 + rr) * SS, (128 + rr) * SS], outline=255, width=wdt * SS)
        p = save(finish(c, 256, 256), "shapes", name); outs.append(p); row("UI_SHAPES", p, SPRITE + note, "shapes")
    c = canvas(64, 64); ImageDraw.Draw(c).ellipse([20 * SS, 20 * SS, 44 * SS, 44 * SS], fill=255)
    p = save(finish(c.filter(ImageFilter.GaussianBlur(3 * SS)), 64, 64), "shapes", "T_UI_SoftDot"); outs.append(p)
    row("UI_SHAPES", p, SPRITE + "Soft dot 64 px: squad dots on the compass, centre dot.", "shapes")
    c = canvas(64, 64); ImageDraw.Draw(c).rounded_rectangle([8 * SS, 8 * SS, 56 * SS, 56 * SS], radius=10 * SS, fill=255)
    p = save(finish(c.filter(ImageFilter.GaussianBlur(5 * SS)), 64, 64), "shapes", "T_UI_SoftPanel"); outs.append(p)
    row("UI_SHAPES", p, SPRITE + "Soft rounded panel 64 px, Sprite Border 24 all sides (9-slice): subtitle backing (black at 45 %), prompt backing.", "shapes")
    return outs


# ---------------------------------------------------------------- polaroid frame
def polaroid():
    rng = np.random.default_rng(70)
    s = 512 / 88.0                                       # SX-70 print 88 x 107 mm, image 79 x 79 mm
    fw, fh = 512, int(107 * s); m = 24
    W, H = fw + 2 * m, fh + 2 * m
    win = (m + int(4.5 * s), m + int(6.0 * s), m + int(4.5 * s) + int(79 * s), m + int(6.0 * s) + int(79 * s))
    paper = np.ones((H, W, 3), np.float32) * np.array([0.93, 0.92, 0.885], np.float32)
    n = np.asarray(Image.fromarray((rng.random((H // 4, W // 4)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), np.float32) / 255
    paper = paper * (0.985 + 0.03 * n[..., None])
    yy = np.arange(H)[:, None, None] / H
    paper = paper * (1 - 0.035 * yy)                     # faint ageing toward the thick bottom edge
    alpha = np.zeros((H, W), np.float32)
    frame = Image.new("L", (W * SS, H * SS), 0); d = ImageDraw.Draw(frame)
    d.rounded_rectangle([m * SS, m * SS, (m + fw) * SS, (m + fh) * SS], radius=6 * SS, fill=255)
    d.rectangle([win[0] * SS, win[1] * SS, win[2] * SS, win[3] * SS], fill=0)
    fa = np.asarray(frame.resize((W, H), Image.LANCZOS), np.float32) / 255
    sh = Image.new("L", (W, H), 0); ImageDraw.Draw(sh).rounded_rectangle([m + 4, m + 8, m + fw + 4, m + fh + 8], radius=8, fill=150)
    sha = np.asarray(sh.filter(ImageFilter.GaussianBlur(10)), np.float32) / 255
    inner = np.zeros((H, W), np.float32); inner[win[1]:win[3], win[0]:win[2]] = 1
    sha = sha * (1 - inner)
    # inner edge: the print's emulsion lip (slightly darker 1 px line)
    lip = np.asarray(Image.fromarray((inner * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3)), np.float32) / 255 - inner
    paper = paper * (1 - 0.25 * lip[..., None])
    alpha = np.clip(fa + sha * (1 - fa), 0, 1)
    rgb = paper * fa[..., None] + 0.0 * (1 - fa[..., None])
    rgba = np.concatenate([rgb, alpha[..., None]], -1)
    p = save(Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA"), "polaroid_frame", "T_UI_PolaroidFrame")
    row("UI_POLAROID_FRAME", p, SPRITE + f"SX-70 print frame {W}x{H} incl. a 24 px soft drop shadow; transparent photo window at x {win[0]}-{win[2]}, y {win[1]}-{win[3]} px (from the top-left), i.e. "
        f"{win[2]-win[0]} px square. IntelCardView: put the T_Intel* photo (RawImage/Image) under this frame in that rect, caption/'INTEL 1/3' in the thick bottom border (y {win[3]+10}-{m+fh-12}).", "polaroid")
    json.dump({"size": [W, H], "window": win}, open(RAW / "UI_POLAROID_FRAME_layout.json", "w"))
    return [p]


# ---------------------------------------------------------------- binoculars
def binoculars():
    W, H = 1024, 576
    c = Image.new("L", (W * 2, H * 2), 255); d = ImageDraw.Draw(c)
    R = int(H * 0.47 * 2)
    for cx in (int(W * 0.5 - H * 0.36) * 2, int(W * 0.5 + H * 0.36) * 2):
        d.ellipse([cx - R, H - R, cx + R, H + R], fill=0)
    c = c.filter(ImageFilter.GaussianBlur(18))
    a = np.asarray(c.resize((W, H), Image.LANCZOS), np.float32) / 255
    rgba = np.zeros((H, W, 4), np.float32); rgba[..., 3] = np.clip(a * 1.02, 0, 1)
    p1 = save(Image.fromarray((rgba * 255).astype(np.uint8), "RGBA"), "binocular_reticle", "T_UI_BinocularMask")
    row("UI_BINOCULAR_RETICLE", p1, SPRITE + "Binocular mask 1024x576 (16:9): black outside two overlapping eyepiece circles, soft edge; stretch to the canvas. On ultra-wide screens add black side bars.", "binoculars")
    S = 512; c = canvas(S, S); d = ImageDraw.Draw(c); k = SS; cx = cy = S // 2
    lw = 2 * k
    d.line([(40 * k, cy * k), ((cx - 14) * k, cy * k)], fill=255, width=lw); d.line([((cx + 14) * k, cy * k), ((S - 40) * k, cy * k)], fill=255, width=lw)
    d.line([(cx * k, (cy + 14) * k), (cx * k, (cy + 150) * k)], fill=255, width=lw)
    for i in range(-10, 11):                                  # mil scale: 10 mil ticks, 5 mil minor
        if i == 0:
            continue
        x = cx + i * 19
        L = 16 if i % 2 == 0 else 9
        d.line([(x * k, (cy - L) * k), (x * k, cy * k)], fill=255, width=lw)
    for j in range(1, 8):                                     # range-finding stadia below the centre
        y = cy + 20 * j; L = 12 if j % 2 == 0 else 6
        d.line([((cx - L) * k, y * k), ((cx + L) * k, y * k)], fill=255, width=lw)
    d.ellipse([(cx - 3) * k, (cy - 3) * k, (cx + 3) * k, (cy + 3) * k], fill=255)
    p2 = save(finish(c, S, S), "binocular_reticle", "T_UI_BinocularReticle")
    row("UI_BINOCULAR_RETICLE", p2, SPRITE + "Binocular mil reticle 512 px (white, tint near-black #101010 at 85 %): horizontal mil scale (19 px per 5 mil at 512), range stadia below centre. Centre on screen, ~40 % of screen height. POI tick-off marks: reuse T_UI_Line.", "binoculars")
    return [p1, p2]


# ---------------------------------------------------------------- door gun
def doorgun():
    S = 512; c = canvas(S, S); d = ImageDraw.Draw(c); k = SS; cx = cy = S // 2
    for rr, w in ((236, 5), (150, 4), (70, 3)):
        d.ellipse([(cx - rr) * k, (cy - rr) * k, (cx + rr) * k, (cy + rr) * k], outline=255, width=w * k)
    for a in range(0, 360, 90):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        d.line([((cx + 22 * ca) * k, (cy + 22 * sa) * k), ((cx + 236 * ca) * k, (cy + 236 * sa) * k)], fill=255, width=3 * k)
    for a in range(45, 360, 90):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        d.line([((cx + 140 * ca) * k, (cy + 140 * sa) * k), ((cx + 160 * ca) * k, (cy + 160 * sa) * k)], fill=255, width=3 * k)
    d.ellipse([(cx - 4) * k, (cy - 4) * k, (cx + 4) * k, (cy + 4) * k], fill=255)
    p1 = save(finish(c, S, S), "doorgun_ring", "T_UI_DoorGunRing")
    row("UI_DOORGUN_RING", p1, SPRITE + "Door-gun ring sight 512 px (white; tint #E8E8E8 at 80 %): outer/mid/inner lead rings + cross with a centre gap. Beat 11 overlay.", "doorgun")
    c = canvas(256, 32); d = ImageDraw.Draw(c); d.rounded_rectangle([2 * SS, 2 * SS, 254 * SS, 30 * SS], radius=6 * SS, outline=255, width=3 * SS)
    p2 = save(finish(c, 256, 32), "doorgun_ring", "T_UI_HeatBarFrame")
    row("UI_DOORGUN_RING", p2, SPRITE + "Heat bar frame 256x32, Sprite Border 10 (9-slice). Fill = T_UI_Line (Filled Horizontal) coloured white -> #E04B3B with heat.", "doorgun")
    return [p1, p2]


# ---------------------------------------------------------------- click gate
def clickgate():
    rng = np.random.default_rng(12)
    W, H = 512, 288
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    e = ((x - W / 2) / (W / 2)) ** 2 + ((y - H / 2) / (H / 2)) ** 2
    glow = 0.05 * np.exp(-e * 1.6) + 0.012
    scan = 0.9 + 0.1 * (np.arange(H)[:, None] % 2)
    n = rng.normal(0, 0.006, (H, W))
    g = (glow * scan + n).clip(0, 1)
    rgb = np.stack([g * 0.35, g * 1.0, g * 0.45], -1)
    rgb = rgb * (1 - 0.55 * np.clip(e - 0.6, 0, 1))[..., None]
    p1 = save(Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8)), "click_gate", "T_UI_ClickGateBG")
    row("UI_CLICK_GATE", p1, SPRITE + "Click-gate CRT glass 512x288 (16:9): near-black with a faint green phosphor glow and line structure; stretch full-screen behind the TMP text (#7FE07F). Lives in S_Boot: keep it tiny (no other art).", "clickgate")
    c = Image.new("RGBA", (32, 48), (0, 0, 0, 0)); a = Image.new("L", (32, 48), 0)
    ImageDraw.Draw(a).rectangle([6, 6, 25, 41], fill=255)
    a = Image.blend(a.filter(ImageFilter.GaussianBlur(3)), a, 0.6)
    c = Image.new("RGBA", (32, 48), (127, 224, 127, 0)); c.putalpha(a)
    p2 = save(c, "click_gate", "T_UI_CrtCursor")
    row("UI_CLICK_GATE", p2, SPRITE + "Green block cursor 32x48 with phosphor bloom; blink 0.53 s on / 0.53 s off.", "clickgate")
    return [p1, p2]


# ---------------------------------------------------------------- letterbox
def letterbox():
    a = np.ones((64, 8), np.float32); a[-4:, :] = np.linspace(1, 0, 4)[:, None]
    rgba = np.zeros((64, 8, 4), np.uint8); rgba[..., 3] = (a * 255).astype(np.uint8)
    p = save(Image.fromarray(rgba, "RGBA"), "letterbox", "T_UI_LetterboxBar")
    row("UI_LETTERBOX", p, SPRITE + "Letterbox bar 8x64 black with a 4 px soft inner edge at the bottom (use for the top bar; flip Y for the bottom bar). Sprite Border L2 R2 T2 B6 (9-slice) so it stretches to any width; 2.35:1 (GDD) / 2.39:1 (CINEMATIC_DIRECTION) bar height = (H - W/2.39)/2.", "letterbox")
    return [p]


# ---------------------------------------------------------------- pause / dossier CRT panel
def pause():
    rng = np.random.default_rng(5)
    S = 256; y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    base = np.array([0.030, 0.045, 0.036], np.float32)
    e = np.clip(np.maximum(np.abs(x - S / 2), np.abs(y - S / 2)) / (S / 2), 0, 1)
    rgb = base[None, None] * (1.25 - 0.45 * e[..., None] ** 3) + rng.normal(0, 0.004, (S, S, 1)).astype(np.float32)
    a = Image.new("L", (S * SS, S * SS), 0); ImageDraw.Draw(a).rounded_rectangle([2 * SS, 2 * SS, (S - 2) * SS, (S - 2) * SS], radius=14 * SS, fill=245)
    al = np.asarray(a.resize((S, S), Image.LANCZOS), np.float32) / 255
    rim = Image.new("L", (S * SS, S * SS), 0); ImageDraw.Draw(rim).rounded_rectangle([4 * SS, 4 * SS, (S - 4) * SS, (S - 4) * SS], radius=12 * SS, outline=255, width=2 * SS)
    rim = np.asarray(rim.resize((S, S), Image.LANCZOS), np.float32) / 255
    rgb = rgb + rim[..., None] * np.array([0.10, 0.16, 0.12], np.float32)
    rgba = np.concatenate([np.clip(rgb, 0, 1), al[..., None]], -1)
    p1 = save(Image.fromarray((rgba * 255).astype(np.uint8), "RGBA"), "pause_panel", "T_UI_CrtPanel")
    row("UI_PAUSE_PANEL", p1, SPRITE + "Dark CRT glass panel 256 px, Sprite Border 32 all sides (9-slice), 96 % opaque, faint green rim: pause/options/debrief backing. Add T_UI_Scanlines (Tiled) over it.", "pause")
    t = np.zeros((4, 4, 4), np.uint8); t[2:, :, 3] = 46
    p2 = save(Image.fromarray(t, "RGBA"), "pause_panel", "T_UI_Scanlines")
    row("UI_PAUSE_PANEL", p2, SPRITE + "4x4 scanline tile (2 px clear, 2 px black at 18 %), Wrap Repeat, Image Type Tiled, Pixels Per Unit so one tile = 2 screen px at 1080p (PPU 200 on a 1920 canvas).", "pause")
    return [p1, p2]


# ---------------------------------------------------------------- fonts
FONTS = {"UI_FONT_TYPEWRITER": [("SpecialElite-Regular.ttf", "specialelite_LICENSE.txt"), ("CourierPrime-Regular.ttf", "courierprime_OFL.txt"), ("CourierPrime-Bold.ttf", "courierprime_OFL.txt")],
         "UI_FONT_CONDENSED": [("BarlowCondensed-Medium.ttf", "barlowcondensed_OFL.txt"), ("BarlowCondensed-SemiBold.ttf", "barlowcondensed_OFL.txt"), ("BarlowCondensed-Regular.ttf", "barlowcondensed_OFL.txt")],
         "UI_FONT_MONO": [("ShareTechMono-Regular.ttf", "sharetechmono_OFL.txt"), ("IBMPlexMono-Regular.ttf", "ibmplexmono_OFL.txt")]}
ATLAS = {"UI_FONT_TYPEWRITER": "1024", "UI_FONT_CONDENSED": "512", "UI_FONT_MONO": "512"}
ROLE = {"UI_FONT_TYPEWRITER": "objectives, title card (amber), debrief dossier, click gate", "UI_FONT_CONDENSED": "HUD numerals (ammo 30 / 120, distances, compass)",
        "UI_FONT_MONO": "Clockmaker subtitles (#7FE07F)"}


def fonts():
    from fontTools.ttLib import TTFont
    dst = READY / "Assets/_Game/Art/Fonts"; dst.mkdir(parents=True, exist_ok=True)
    sheet = Image.new("RGB", (1400, 90 * 9), (24, 24, 24)); d = ImageDraw.Draw(sheet); yy = 10
    for mid, files in FONTS.items():
        for f, lic in files:
            srcp = RAW / mid / f
            tt = TTFont(srcp); fam = tt["name"].getDebugName(1); glyphs = len(tt.getGlyphOrder())
            cmap = tt.getBestCmap(); need = "—·°×"
            missing = [ch for ch in need if ord(ch) not in cmap]
            shutil.copy(srcp, dst / f)
            licn = f"{pathlib.Path(f).stem}_LICENSE.txt"
            shutil.copy(RAW / mid / lic, dst / licn)
            row(mid, dst / f, f"TrueType font '{fam}' ({glyphs} glyphs; missing {''.join(missing) or 'none'} of the em dash / middle dot / degree / times signs). "
                f"Licence {lic.split('_')[1].split('.')[0].upper()} kept next to it ({licn}). Create a TMP Font Asset (SDF, Padding 9, atlas {ATLAS[mid]}x{ATLAS[mid]}, "
                f"Dynamic, ASCII + the signs above). Role: {ROLE[mid]}.", "fonts")
            row(mid, dst / licn, "Licence text that must ship with the font (keep in the repo; not referenced at runtime).", "fonts")
            fnt = ImageFont.truetype(str(srcp), 40)
            d.text((10, yy), f"{f}: OPERATION LONG SHADOW 21 OCTOBER 1984 - 17:58  30/120  74410 00640", font=fnt, fill=(230, 220, 180)); yy += 88
    sheet.save(RAW / "_review/fonts_proof.png")
    return []


ALL = ["icons", "vignette", "shapes", "polaroid", "binoculars", "doorgun", "clickgate", "letterbox", "pause", "fonts"]

if __name__ == "__main__":
    which = sys.argv[1:] or ALL
    outs = []
    for w in which:
        outs += globals()[w]() or []
    (READY / "rows").mkdir(parents=True, exist_ok=True)
    for g, rows in ROWS.items():
        (READY / "rows" / f"{g}.json").write_text(json.dumps(rows, indent=1))
    (RAW / "_review").mkdir(exist_ok=True)
    import subprocess
    subprocess.run([sys.executable, str(ROOT / "Tools/images/contact.py"), str(RAW / "_review/ui_all.png")] + [str(o) for o in outs] +
                   ["--cell", "300", "--cols", "6", "--bg", "checker"], check=False)
