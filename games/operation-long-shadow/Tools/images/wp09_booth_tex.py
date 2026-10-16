#!/usr/bin/env python3
"""WP09 booth surface textures (TEX_ACG_PAINTEDPLASTER012, TEX_ACOUSTIC_TILE, PROC_BOOTH_SHELL decals, wall-clock crack).

  Tools/.venv/bin/python Tools/images/wp09_booth_tex.py [plaster tile drain crack mirror] (default: all)
plaster : ambientCG PaintedPlaster012 2K -> T_PaintedPlaster012_{BC,N,M} 2048 (hero set; M = R metal 0, G AO, B 0, A 1-rough)
tile    : procedural 1970s perforated mineral-fibre acoustic tile (2 x 2 tiles of 12 in, 24 x 24 holes of 4.8 mm each),
          fully periodic (FFT noise) -> T_AcousticTile_{BC,N,M} 1024 (+ _H height for parallax if wanted). The fal Patina
          takes (RawAssets/wp09/TEX_ACOUSTIC_TILE/patina_t*) were rejected: no real perforation grid.
drain   : fal top-down cast-iron drain photo -> round decal T_BoothDrain_{BC,N,M} 512 with a feathered rust/damp halo alpha
crack   : procedural impact crack for the clock glass -> T_WallClockGlassCrack_A_BC (RGBA, white lines on alpha) 512
mirror  : T_OneWayMirror_{BC,M} 256: near-black glass with faint wipe streaks for the one-way mirror (smooth, reflection probe does the rest)
Writes RawAssets/wp09/ready/... and ready/rows/tex_booth.json (rows) + ready/rows/tex_booth_materials.json (material specs).
"""
import json, math, pathlib, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = pathlib.Path(__file__).resolve().parents[2]
RAW = ROOT / "RawAssets/wp09"
READY = RAW / "ready"
ROWS, MATS = [], []


def save(a, rel, mode=None):
    p = READY / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    a = np.clip(a, 0, 1)
    if a.ndim == 2:
        mode = mode or "L"
    Image.fromarray((a * 255 + 0.5).astype(np.uint8), mode).save(p, optimize=True)
    return p


def row(mid, p, notes):
    rel = str(p.relative_to(READY))
    ROWS.append({"manifest_id": mid, "file": rel, "target_path": rel, "type": "texture", "import_notes": notes})


def mat(mid, name, folder, tiling=None, extra=None, has_n=True):
    t = f"Assets/_Game/Art/Textures/{folder}"
    m = {"manifest_id": mid, "material": f"M_{name}", "target_path": f"Assets/_Game/Art/Materials/{'Env' if folder.startswith('Env') else 'Props'}/M_{name}.mat",
         "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic",
         "textures": {"_BaseMap": f"{t}/T_{name}_BC.png", "_MetallicGlossMap": f"{t}/T_{name}_M.png", "_OcclusionMap": f"{t}/T_{name}_M.png"},
         "floats": {"_Smoothness": 1.0, "_SmoothnessTextureChannel": 0, "_Metallic": 1.0, "_OcclusionStrength": 1.0},
         "colors": {"_BaseColor": [1, 1, 1, 1]}}
    if has_n:
        m["textures"]["_BumpMap"] = f"{t}/T_{name}_N.png"
    if tiling:
        m["tiling_m"] = tiling
    if extra:
        m.update(extra)
    MATS.append(m)


def srgb2lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin2srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def normal_from_height(h, strength):
    gy, gx = np.gradient(h)
    n = np.stack([-gx * strength, gy * strength, np.ones_like(h)], -1)   # OpenGL +Y (green up)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def periodic_noise(rng, n, beta, lo=1.0):
    """Tileable fractal noise via random-phase FFT with a 1/f^beta spectrum."""
    fx = np.fft.fftfreq(n)[None, :]; fy = np.fft.fftfreq(n)[:, None]
    f = np.sqrt(fx ** 2 + fy ** 2); f[0, 0] = 1
    amp = 1 / f ** beta; amp[0, 0] = 0; amp[f < lo / n] = 0
    ph = np.exp(2j * np.pi * rng.random((n, n)))
    a = np.real(np.fft.ifft2(amp * ph))
    return (a - a.mean()) / (a.std() + 1e-9)


# ---------------------------------------------------------------- PaintedPlaster012
def plaster():
    src = RAW / "TEX_ACG_PAINTEDPLASTER012"
    load = lambda n, m="RGB": np.asarray(Image.open(next(src.glob(f"*_{n}.jpg"))).convert(m).resize((2048, 2048), Image.LANCZOS), np.float32) / 255
    bc = load("Color"); n = load("NormalGL"); rough = load("Roughness", "L"); ao = load("AmbientOcclusion", "L")
    m = np.stack([np.zeros_like(rough), ao, np.zeros_like(rough), 1 - rough], -1)
    d = "Assets/_Game/Art/Textures/Env/PaintedPlaster012"
    p1 = save(bc, f"{d}/T_PaintedPlaster012_BC.png"); p2 = save(n, f"{d}/T_PaintedPlaster012_N.png"); p3 = save(m, f"{d}/T_PaintedPlaster012_M.png", "RGBA")
    row("TEX_ACG_PAINTEDPLASTER012", p1, "Base colour (olive painted plaster, ambientCG PaintedPlaster012, CC0). HERO set: Default, sRGB ON, Max Size 2048, DXT1 Crunched q55, mips on, aniso 4. Tiling ~2 m per repeat on the booth walls.")
    row("TEX_ACG_PAINTEDPLASTER012", p2, "Normal map (OpenGL +Y, ambientCG NormalGL), sRGB OFF, Max Size 2048, DXT5nm Crunched q60.")
    row("TEX_ACG_PAINTEDPLASTER012", p3, "Mask: R metallic 0, G AO, B 0, A smoothness (1 - roughness). sRGB OFF, Max Size 2048, DXT5 Crunched q60; Metallic Map + Occlusion Map.")
    mat("TEX_ACG_PAINTEDPLASTER012", "PaintedPlaster012", "Env/PaintedPlaster012", tiling=2.0,
        extra={"note": "Booth walls below the acoustic tile (GDD 8.0). Tiling = 1 / 2 m on world-UV kit meshes. ambientCG PaintedPlaster012 (CC0)."})


# ---------------------------------------------------------------- acoustic tile
def tile():
    rng = np.random.default_rng(1970)
    N = 1024; SS = 2                      # supersample for the holes
    M = N * SS
    tile_px = M // 2                      # 2 x 2 tiles of 12 in (0.3048 m) -> texture = 0.61 m
    holes = 24; pitch = tile_px / holes; r_hole = 0.0024 / 0.3048 * tile_px     # 4.8 mm holes
    y, x = np.mgrid[0:M, 0:M].astype(np.float32)
    lx = (x % tile_px); ly = (y % tile_px)
    # holes: grid with a half-pitch inset from each tile edge; slight random position jitter per hole (punching tolerance)
    hx = (lx // pitch) * pitch + pitch / 2; hy = (ly // pitch) * pitch + pitch / 2
    jit = rng.normal(0, 0.012 * pitch, (2, holes * 2, holes * 2)).astype(np.float32)
    ix = np.clip((x // pitch).astype(int), 0, holes * 2 - 1); iy = np.clip((y // pitch).astype(int), 0, holes * 2 - 1)
    dx = lx - hx - jit[0][iy, ix]; dy = ly - hy - jit[1][iy, ix]
    dist = np.sqrt(dx * dx + dy * dy)
    hole = np.clip((r_hole + 0.8 * SS - dist) / (1.6 * SS), 0, 1)          # 1 inside the hole, AA edge
    # seams: shallow V kerf at tile edges (~3 mm)
    e = np.minimum(np.minimum(lx, tile_px - lx), np.minimum(ly, tile_px - ly))
    kerf_w = 0.003 / 0.3048 * tile_px
    seam = np.clip(1 - e / kerf_w, 0, 1) ** 1.5
    # fibre surface: fine periodic noise + fissures, tile-to-tile tone shift
    def up(a):
        return np.asarray(Image.fromarray(a.astype(np.float32), "F").resize((M, M), Image.BICUBIC))
    fine = up(periodic_noise(rng, N, 0.6, 40)); mid = up(periodic_noise(rng, N, 1.2, 6)); low = up(periodic_noise(rng, N, 2.0, 1))
    tid = (x // tile_px).astype(int) * 2 + (y // tile_px).astype(int)
    tone = np.array([0.0, 0.035, -0.02, 0.015], np.float32)[tid]
    height = 1.0 - 0.9 * hole - 0.45 * seam + 0.012 * fine + 0.02 * mid
    # colour: aged off-white, nicotine yellowing toward the top of each tile pair, grime in kerfs, one faint water stain
    base = np.array([0.86, 0.835, 0.765], np.float32)
    yellow = np.clip(0.5 + 0.35 * low + tone * 4, 0, 1)[..., None] * np.array([0.0, -0.025, -0.08], np.float32)
    col = base[None, None] + yellow + (0.02 * fine + 0.03 * mid)[..., None] * np.array([1, 1, 0.95], np.float32)
    col = col * (1 + tone[..., None])
    sx, sy, sr = 0.68 * M, 0.3 * M, 0.11 * M                     # water stain with a darker tide line
    sd = np.sqrt(((x - sx + M / 2) % M - M / 2) ** 2 + ((y - sy + M / 2) % M - M / 2) ** 2) / sr
    sd = sd * (1 + 0.18 * mid)
    stain = np.clip(1 - sd, 0, 1) ** 0.6 * 0.06 + np.exp(-((sd - 1.0) / 0.05) ** 2) * 0.09
    col = col - stain[..., None] * np.array([0.6, 0.75, 1.0], np.float32)
    col = col * (1 - 0.35 * seam)[..., None]
    col = col * (1 - 0.88 * hole)[..., None] + hole[..., None] * np.array([0.12, 0.11, 0.1], np.float32) * 0.0
    ao = np.clip(1 - 0.75 * hole - 0.35 * seam, 0, 1)
    rough = np.clip(0.9 + 0.04 * fine - 0.05 * stain, 0, 1)
    def down(a):
        if a.ndim == 2:
            return np.asarray(Image.fromarray(a.astype(np.float32), "F").resize((N, N), Image.LANCZOS))
        return np.stack([down(a[..., c]) for c in range(a.shape[-1])], -1)
    col = down(col); height = down(height); ao = down(ao); rough = down(rough)
    nrm = normal_from_height(height, 6.0)
    d = "Assets/_Game/Art/Textures/Env/acoustic_tile"
    p1 = save(col, f"{d}/T_AcousticTile_BC.png"); p2 = save(nrm, f"{d}/T_AcousticTile_N.png")
    p3 = save(np.stack([np.zeros_like(ao), ao, np.zeros_like(ao), 1 - rough], -1), f"{d}/T_AcousticTile_M.png", "RGBA")
    p4 = save((height - height.min()) / (np.ptp(height) + 1e-6), f"{d}/T_AcousticTile_H.png")
    row("TEX_ACOUSTIC_TILE", p1, "Procedural perforated acoustic tile (2 x 2 tiles of 12 in per repeat = 0.6096 m; tiling = 1/0.6096 on world-UV booth walls/ceiling; align the UV origin to a wall corner so seams meet the corners). Default, sRGB ON, Max Size 1024, DXT1 Crunched q55, mips on, aniso 4.")
    row("TEX_ACOUSTIC_TILE", p2, "Normal map (OpenGL +Y, from the procedural height), sRGB OFF, Max Size 1024, DXT5nm Crunched q60.")
    row("TEX_ACOUSTIC_TILE", p3, "Mask: R metallic 0, G AO (holes/kerfs), B 0, A smoothness (~0.1). sRGB OFF, Max Size 1024, DXT5 Crunched q60.")
    row("TEX_ACOUSTIC_TILE", p4, "Optional height (_H) for URP Lit parallax (0.005-0.008) on the C_POV-facing walls only; otherwise do not import (not referenced by M_AcousticTile). sRGB OFF, Max Size 1024, single channel.")
    mat("TEX_ACOUSTIC_TILE", "AcousticTile", "Env/acoustic_tile", tiling=0.6096,
        extra={"note": "Booth walls (upper band) and ceiling (GDD 8.0). Procedural (Tools/images/wp09_booth_tex.py)."})


# ---------------------------------------------------------------- drain decal
def drain():
    S = 512
    im = Image.open(RAW / "PROC_BOOTH_SHELL/drain_t1.png").convert("RGB")
    w, h = im.size
    c = (int(w * 0.5), int(h * 0.5)); R = int(w * 0.36)          # grate + rust halo
    im = im.crop((c[0] - R, c[1] - R, c[0] + R, c[1] + R)).resize((S, S), Image.LANCZOS)
    a = np.asarray(im, np.float32) / 255
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    r = np.hypot(x - S / 2, y - S / 2) / (S / 2)
    lum = a.mean(2)
    conc = np.median(lum[r > 0.9])
    # alpha: solid over the grate (r < 0.62), then only where the image differs from bare concrete (rust / damp halo)
    diff = np.clip(np.abs(lum - conc) / 0.12, 0, 1)
    rust = np.clip((a[..., 0] - a[..., 2]) / 0.08, 0, 1)
    alpha = np.where(r < 0.62, 1.0, np.clip(np.maximum(diff, rust) * np.clip((0.98 - r) / 0.3, 0, 1), 0, 1))
    alpha = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2)), np.float32) / 255
    alpha = np.where(r < 0.6, 1.0, alpha)
    h_ = np.asarray(Image.fromarray((lum * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)), np.float32) / 255
    grate = (r < 0.6).astype(np.float32)
    nrm = normal_from_height(h_ * (0.3 + 0.7 * grate), 5.0)
    ao = np.clip(0.4 + 0.6 * h_ / max(h_[r < 0.6].max(), 1e-3), 0, 1) * grate + (1 - grate)
    smooth = 0.18 + 0.25 * (1 - np.clip(diff, 0, 1)) * (r > 0.6)            # damp ring a bit glossier
    bc = np.concatenate([a, alpha[..., None]], -1)
    d = "Assets/_Game/Art/Textures/Env/Booth"
    p1 = save(bc, f"{d}/T_BoothDrain_BC.png", "RGBA"); p2 = save(nrm, f"{d}/T_BoothDrain_N.png")
    p3 = save(np.stack([np.where(grate > 0, 0.6, 0.0), ao, np.zeros_like(ao), smooth], -1), f"{d}/T_BoothDrain_M.png", "RGBA")
    row("PROC_BOOTH_SHELL", p1, "Floor drain decal (cast-iron grate + rust/damp halo) for the booth floor at local (0, 0, 0.8); real size 0.42 m across the decal (grate 0.15 m). URP Decal Projector (Base Map + alpha), or bake into the floor. Default, sRGB ON, Alpha Is Transparency ON, Max Size 512, DXT5 Crunched q60.")
    row("PROC_BOOTH_SHELL", p2, "Drain normal (OpenGL +Y, from luminance), sRGB OFF, Max Size 512.")
    row("PROC_BOOTH_SHELL", p3, "Drain mask (R metal 0.6 on the grate, G AO, A smoothness), sRGB OFF, Max Size 512. For a Decal Projector use 'Affect MAOS'.")
    MATS.append({"manifest_id": "PROC_BOOTH_SHELL", "material": "M_BoothDrain_Decal", "target_path": "Assets/_Game/Art/Materials/Env/M_BoothDrain_Decal.mat",
                 "shader": "Shader Graphs/Decal", "textures": {"Base_Map": f"Assets/_Game/{d[len('Assets/_Game/'):]}/T_BoothDrain_BC.png",
                 "Normal_Map": f"{d}/T_BoothDrain_N.png", "Mask_Map": f"{d}/T_BoothDrain_M.png"},
                 "note": "URP Decal (needs the Decal renderer feature, DBuffer). Projector size 0.42 x 0.42, depth 0.1, at booth floor (0, 0, 0.8)."})


# ---------------------------------------------------------------- clock glass crack
def crack():
    rng = np.random.default_rng(640)
    S = 1024; im = Image.new("L", (S, S), 0); d = ImageDraw.Draw(im)
    cx, cy = S * 0.63, S * 0.36                    # impact upper-right of the dial centre (dial hands stay readable at 6:40)
    for k in range(13):                            # radial cracks with kinks
        ang = rng.uniform(0, 2 * math.pi); L = rng.uniform(0.25, 0.75) * S
        pts = [(cx, cy)]; x, y, a = cx, cy, ang; seg = L / 7
        for s in range(7):
            a += rng.normal(0, 0.18); x += seg * math.cos(a); y += seg * math.sin(a); pts.append((x, y))
        d.line(pts, fill=int(rng.uniform(150, 235)), width=int(rng.choice([2, 2, 3])))
        if rng.random() < 0.6:                     # side branch
            j = rng.integers(2, 6); bx, by = pts[j]; ba = a + rng.choice([-1, 1]) * rng.uniform(0.5, 1.0)
            d.line([(bx, by), (bx + seg * 1.6 * math.cos(ba), by + seg * 1.6 * math.sin(ba))], fill=170, width=2)
    for rad in (40, 85, 140):                      # concentric ring fractures
        a0 = rng.uniform(0, 360)
        for _ in range(4):
            st = a0 + rng.uniform(0, 360); d.arc([cx - rad, cy - rad, cx + rad, cy + rad], st, st + rng.uniform(25, 70), fill=190, width=2)
    d.ellipse([cx - 9, cy - 9, cx + 9, cy + 9], fill=245)       # crushed impact point
    a = np.asarray(im.filter(ImageFilter.GaussianBlur(0.9)), np.float32) / 255
    a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).resize((512, 512), Image.LANCZOS), np.float32) / 255
    rgba = np.stack([np.full_like(a, 0.93), np.full_like(a, 0.94), np.full_like(a, 0.95), np.clip(a * 1.2, 0, 1)], -1)
    p = save(rgba, "Assets/_Game/Art/Textures/Props/wall_clock/T_WallClockGlassCrack_A_BC.png", "RGBA")
    row("PH_WALL_CLOCK", p, "Cracked-glass overlay for the booth clock (white crack lines on alpha, impact upper right so 6:40 stays readable). Base Map of M_WallClock_Glass_Cracked (URP Lit Transparent, alpha from map, smoothness 0.95). Default, sRGB ON, Alpha Is Transparency ON, Max Size 512, DXT5 Crunched q60. The glass UVs are planar 0..1 across the face (verify once; rotate the texture 90 deg if the impact lands elsewhere).")
    MATS.append({"manifest_id": "PH_WALL_CLOCK", "material": "M_WallClock_Glass_Cracked", "target_path": "Assets/_Game/Art/Materials/Props/M_WallClock_Glass_Cracked.mat",
                 "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic", "surface": "Transparent", "blend": "Alpha",
                 "textures": {"_BaseMap": "Assets/_Game/Art/Textures/Props/wall_clock/T_WallClockGlassCrack_A_BC.png"},
                 "floats": {"_Smoothness": 0.95, "_Metallic": 0.0}, "colors": {"_BaseColor": [1, 1, 1, 1]},
                 "note": "Booth instance of the wall clock only (assign to the M_WallClock_Glass slot). The Relay Hall clocks keep the clean glass."})


# ---------------------------------------------------------------- one-way mirror
def mirror():
    rng = np.random.default_rng(3)
    S = 256
    st = periodic_noise(rng, S, 1.6, 2)
    streak = np.repeat(periodic_noise(rng, S, 1.0, 4)[:1], S, 0) * 0.5 + st * 0.5
    bc = np.clip(0.035 + 0.006 * streak, 0, 1)
    sm = np.clip(0.93 - 0.06 * np.clip(streak, 0, 3), 0, 1)
    d = "Assets/_Game/Art/Textures/Env/Booth"
    p1 = save(np.repeat(bc[..., None], 3, 2), f"{d}/T_OneWayMirror_BC.png")
    p2 = save(np.stack([np.full_like(bc, 0.0), np.ones_like(bc), np.zeros_like(bc), sm], -1), f"{d}/T_OneWayMirror_M.png", "RGBA")
    row("PROC_BOOTH_SHELL", p1, "One-way mirror glass base (near-black with faint wipe streaks). Default, sRGB ON, Max Size 256. Pair with the baked planar-ish reflection probe (GDD 8.0) so it shows Calder's silhouette.")
    row("PROC_BOOTH_SHELL", p2, "Mirror mask (A smoothness 0.87-0.93 with streaks), sRGB OFF, Max Size 256.")
    mat("PROC_BOOTH_SHELL", "OneWayMirror", "Env/Booth", has_n=False, extra={"note": "x -1.6..1.6, y 0.9..2.1 on the z 2.55 wall. Metallic 0, smoothness from mask; the reflection probe (box projection) carries the image."})


if __name__ == "__main__":
    which = sys.argv[1:] or ["plaster", "tile", "drain", "crack", "mirror"]
    for w in which:
        {"plaster": plaster, "tile": tile, "drain": drain, "crack": crack, "mirror": mirror}[w]()
    (READY / "rows").mkdir(parents=True, exist_ok=True)
    old = []
    pr = READY / "rows/tex_booth.json"
    if pr.exists() and len(which) < 5:
        old = [r for r in json.loads(pr.read_text()) if r["file"] not in {x["file"] for x in ROWS}]
    pr.write_text(json.dumps(old + ROWS, indent=1))
    pm = READY / "rows/tex_booth_materials.json"
    oldm = [m for m in json.loads(pm.read_text()) if m["material"] not in {x["material"] for x in MATS}] if pm.exists() and len(which) < 5 else []
    pm.write_text(json.dumps(oldm + MATS, indent=1))
    print("rows", len(old + ROWS), "mats", len(oldm + MATS))
