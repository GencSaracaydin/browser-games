#!/usr/bin/env python3
"""WP10 procedural VFX textures (TECH_PLAN 8.1 vfx_flipbook): numpy noise, seeded, deterministic.

  Tools/.venv/bin/python -I Tools/tex/wp10_vfx_flipbook.py [--only smoke,fire,muzzle,sparks,dust,tracer,glint]

Writes RawAssets/wp10/ready/Assets/_Game/Art/Textures/FX/T_VFX_*.png + rows (tool "wp10_vfx") in
ready/import_index_cc0.json and a contact sheet per texture in RawAssets/wp10/cc0/_contact/VFX_*.jpg.
  smoke  8x8 1024: lit grey + alpha (tint per use: white smoke, green smoke, dust)   T_VFX_SmokePuff_8x8_BC
  fire   8x8 1024: emissive flames, seamless 64-frame loop (additive)                T_VFX_Fire_8x8_E
  muzzle 4x2 1024x512: row 0 side flashes, row 1 front flashes (additive)             T_VFX_MuzzleFlash_4x2_E
  sparks 4x4 512: streaks (cells 0-11) and glints (12-15) (additive)                  T_VFX_Sparks_4x4_E
  dust   8x8 1024: impact burst + chips, lit grey + alpha (tint by surface)           T_VFX_DustImpact_8x8_BC
  tracer 256x64: rows red (top) / green (bottom), white core (additive)               T_VFX_Tracer_E
  glint  256: scope glint: core, 6-point star, anamorphic streak (additive)           T_VFX_Glint_E
"""
import argparse, json, math, os, sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wp10_texlib as T  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
READY = os.path.join(ROOT, 'RawAssets', 'wp10', 'ready')
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')
FX = 'Assets/_Game/Art/Textures/FX'


class Noise3:
    """Periodic 3-D value noise (quintic interpolation) on a random lattice."""
    def __init__(self, seed, P=32):
        self.lat = np.random.default_rng(seed).random((P, P, P)).astype(np.float32)
        self.P = P

    def __call__(self, x, y, z):
        P = self.P
        xi, yi, zi = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64), np.floor(z).astype(np.int64)
        xf, yf, zf = x - xi, y - yi, z - zi
        f = lambda t: t * t * t * (t * (t * 6 - 15) + 10)
        u, v, w = f(xf), f(yf), f(zf)
        x0, y0, z0 = xi % P, yi % P, zi % P
        x1, y1, z1 = (x0 + 1) % P, (y0 + 1) % P, (z0 + 1) % P
        L = self.lat
        c00 = L[x0, y0, z0] * (1 - u) + L[x1, y0, z0] * u
        c10 = L[x0, y1, z0] * (1 - u) + L[x1, y1, z0] * u
        c01 = L[x0, y0, z1] * (1 - u) + L[x1, y0, z1] * u
        c11 = L[x0, y1, z1] * (1 - u) + L[x1, y1, z1] * u
        return (c00 * (1 - v) + c10 * v) * (1 - w) + (c01 * (1 - v) + c11 * v) * w

    def fbm(self, x, y, z, octaves=5, gain=0.5):
        a, s, tot = 1.0, 0.0, 0.0
        for o in range(octaves):
            s = s + a * self(x * 2 ** o + 17.3 * o, y * 2 ** o + 5.1 * o, z * 2 ** o + 3.7 * o)
            tot += a
            a *= gain
        return s / tot


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def grid(F):
    c = (np.arange(F) + 0.5) / F * 2 - 1
    return np.meshgrid(c, c)        # u right, v down (image rows)


def atlas(frames, cols, rows):
    F = frames[0].shape[0]
    ch = frames[0].shape[2]
    out = np.zeros((rows * F, cols * frames[0].shape[1], ch), np.float32)
    for i, fr in enumerate(frames):
        r, c = divmod(i, cols)
        out[r * F:(r + 1) * F, c * fr.shape[1]:(c + 1) * fr.shape[1]] = fr
    return out


def lit_density(dens, light=(-0.6, -0.8), k=6.0):
    gy, gx = np.gradient(dens)
    s = -(gx * light[0] + gy * light[1]) * k * dens.shape[0] / 128
    lit = np.clip(0.62 + 0.38 * np.tanh(s * 3), 0, 1) * (1 - 0.30 * np.clip(dens, 0, 1))
    return lit


def smoke(seed=11, F=128, N=64):
    nz = Noise3(seed)
    u, v = grid(F)
    frames = []
    for i in range(N):
        t = i / (N - 1)
        R = 0.28 + 0.62 * (1 - (1 - t) ** 2.2)
        cy = -0.10 * t
        r = np.sqrt(u ** 2 + ((v - cy) * 1.05) ** 2)
        wx = nz.fbm(u * 1.6 + 3, v * 1.6, t * 1.2, 3) - 0.5
        wy = nz.fbm(u * 1.6, v * 1.6 + 7, t * 1.2 + 4, 3) - 0.5
        d = nz.fbm(u * 2.6 + wx * 1.4, v * 2.6 + wy * 1.4 + t * 0.6, t * 1.8 + 11, 6)
        rr = r + (nz.fbm(u * 3, v * 3, t * 2 + 30, 3) - 0.5) * 0.45 * R
        m = smooth(R, R * 0.35, rr)
        dens = np.clip(d * 1.9 - 0.42, 0, 1) * m
        fade = min(1.0, t / 0.04) * (1 - t) ** 1.25
        a = 1 - np.exp(-dens * 3.2 * fade * 1.6)
        lit = lit_density(dens)
        frames.append(np.stack([lit, lit, lit * 0.985, a], 2))
    return atlas(frames, 8, 8)


def fire(seed=23, F=128, N=64):
    P = 32
    nz = Noise3(seed, P)
    u, v = grid(F)
    h = (1 - v) / 2                                         # 0 at bottom, 1 at top
    frames = []
    for i in range(N):
        t = i / N                                           # loops: noise z and y-scroll are periodic
        z = t * P
        n = np.zeros_like(u)
        a, tot = 1.0, 0.0
        for o in range(5):
            f = 2 ** o
            n = n + a * nz(u * 3 * f + 9 * o, (h * 2.2 - t * 8) * f % P, (z * f) % P)
            tot += a
            a *= 0.55
        n /= tot
        warp = (n - 0.5) * 0.35
        w = 0.42 * (1 - np.clip(h + warp * 0.5, 0, 1) ** 1.25) + 0.02
        shape = smooth(1.0, 0.25, np.abs(u + warp * (0.4 + h)) / w)
        base = smooth(0.0, 0.08, h)
        temp = np.clip(shape * base * (0.55 + 0.9 * n) - h * 0.55, 0, 1.2)
        frames.append(np.stack(fire_ramp(temp), 2))
    return atlas(frames, 8, 8)


def fire_ramp(tmp):
    """Blackbody-ish ramp (display-linear emissive): dark red -> orange -> yellow -> warm white."""
    xs = [0.0, 0.22, 0.42, 0.62, 0.82, 1.2]
    cs = np.array([[0, 0, 0], [0.30, 0.03, 0.0], [0.85, 0.22, 0.02], [1.0, 0.52, 0.10], [1.0, 0.80, 0.42], [1.0, 0.97, 0.88]], np.float32)
    out = []
    for c in range(3):
        out.append(np.interp(tmp, xs, cs[:, c]).astype(np.float32))
    return out


def muzzle(seed=31, F=256):
    rng = np.random.default_rng(seed)
    nz = Noise3(seed + 1)
    u, v = grid(F)
    tiles = []
    for k in range(4):      # side views: muzzle at left, flash travels right
        x = (u + 1) / 2
        y = v
        x0 = 0.08
        L = rng.uniform(0.62, 0.85)
        w = 0.035 + 0.20 * np.clip(x - x0, 0, 1) * rng.uniform(0.8, 1.2)
        env = smooth(x0 - 0.02, x0 + 0.06, x) * np.exp(-np.clip(x - x0 - 0.12, 0, None) / (L * 0.45))
        tongue = nz.fbm(x * 6 + k * 3, y * 14, k * 5.0, 4)
        body = np.exp(-(y / w) ** 2) * env * np.clip(tongue * 1.7 - 0.25, 0, 1.2)
        # lateral jets from the brake (slanted, upper side)
        jet = np.exp(-(((x - 0.14) * 9) ** 2 + ((y + 0.22 * (x - 0.1) / 0.3) * 4) ** 2)) * 0.5 * rng.uniform(0.6, 1.0)
        core = np.exp(-(((x - x0 - 0.05) / 0.07) ** 2 + (y / 0.06) ** 2)) * 1.4
        tiles.append(muzzle_color(body + jet + core))
    for k in range(4):      # front views: star from the brake ports
        r = np.sqrt(u ** 2 + v ** 2)
        th = np.arctan2(v, u)
        n = rng.integers(3, 6)
        th0 = rng.uniform(0, 2 * np.pi)
        petals = np.zeros_like(u)
        for j in range(n):
            tj = th0 + 2 * np.pi * j / n + rng.normal(0, 0.15)
            dth = np.angle(np.exp(1j * (th - tj)))
            petals += np.exp(-(dth / (0.16 + 0.10 * r)) ** 2) * np.exp(-r / rng.uniform(0.30, 0.45))
        tex = nz.fbm(r * 8 + k, th * 3, 40 + k * 3.0, 4)
        core = np.exp(-(r / 0.13) ** 2) * 1.6 + np.exp(-(r / 0.32) ** 2) * 0.35
        tiles.append(muzzle_color(core + petals * np.clip(tex * 1.8 - 0.3, 0, 1.3)))
    return atlas(tiles, 4, 2)


def muzzle_color(I):
    I = np.clip(I, 0, 2.0)
    return np.stack(fire_ramp(I * 0.85), 2)


def sparks(seed=41, F=128):
    rng = np.random.default_rng(seed)
    u, v = grid(F)
    tiles = []
    for k in range(12):
        img = np.zeros((F, F), np.float32)
        hot = np.zeros((F, F), np.float32)
        for s in range(rng.integers(1, 4)):
            ang = rng.uniform(-0.5, 0.5) if k < 8 else rng.uniform(0, np.pi)
            L = rng.uniform(0.6, 1.5)
            x0, y0 = rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3)
            dx, dy = math.cos(ang), math.sin(ang)
            px, py = u - x0, v - y0
            along = px * dx + py * dy
            perp = -px * dy + py * dx
            t = np.clip((along + L / 2) / L, 0, 1)
            inside = smooth(0, 0.08, t) * smooth(1.0, 0.85, t)
            width = 0.012 + 0.018 * t
            line = np.exp(-(perp / width) ** 2) * inside * (0.35 + 0.65 * t)
            img += line
            hot += np.exp(-(perp / (width * 0.45)) ** 2) * inside * t ** 2
        r = np.clip(img + hot, 0, 1.5)
        tiles.append(np.stack([np.clip(r * 1.0, 0, 1), np.clip(img * 0.55 + hot * 0.9, 0, 1), np.clip(hot * 0.7, 0, 1)], 2))
    for k in range(4):
        r = np.sqrt(u ** 2 + v ** 2)
        th = np.arctan2(v, u) + rng.uniform(0, np.pi)
        star = (np.abs(np.cos(th * 2)) ** 40) * np.exp(-r / 0.28) + (np.abs(np.cos(th * 2 + np.pi / 4)) ** 60) * np.exp(-r / 0.16) * 0.5
        core = np.exp(-(r / 0.07) ** 2)
        I = np.clip(core + star, 0, 1.2)
        tiles.append(np.stack([np.clip(I, 0, 1), np.clip(I * 0.9, 0, 1), np.clip(I * 0.7, 0, 1)], 2))
    return atlas(tiles, 4, 4)


def dust(seed=53, F=128, N=64):
    nz = Noise3(seed)
    rng = np.random.default_rng(seed)
    u, v = grid(F)
    chips = [(rng.uniform(-0.9, 0.9), rng.uniform(1.0, 2.2), rng.uniform(0.008, 0.02)) for _ in range(14)]
    frames = []
    for i in range(N):
        t = i / (N - 1)
        # burst from the bottom centre, opening upward in a ~70 deg cone, settling and thinning
        x = u
        y = (1 - v) / 2              # 0 bottom .. 1 top
        spread = 0.18 + 0.75 * (1 - (1 - t) ** 2.5)
        rise = 0.15 + 0.55 * (1 - (1 - t) ** 3) - 0.10 * t ** 2
        cone = smooth(spread, spread * 0.25, np.abs(x) / (0.25 + y * 1.2 + 1e-3))
        vert = smooth(rise, rise * 0.2, y) * smooth(0.0, 0.03, y)
        d = nz.fbm(x * 3 + 2, y * 3 - t * 1.2, t * 2.2, 6)
        dens = np.clip(d * 2.0 - 0.45, 0, 1) * cone * vert
        fade = min(1.0, t / 0.03) * (1 - t) ** 1.6
        a = 1 - np.exp(-dens * 3.5 * fade * 1.4)
        lit = lit_density(dens, light=(-0.5, -0.86))
        rgb = np.stack([lit, lit * 0.98, lit * 0.95], 2)
        # chips: ballistic dark specks in the first third
        if t < 0.45:
            for cx, sp, sz in chips:
                tt = t * 2.2
                px = cx * tt * 0.9
                py = sp * tt - 2.2 * tt ** 2
                cy = 1 - 2 * np.clip(py * 0.5, 0, 1)   # back to image v
                m = np.exp(-(((u - px) ** 2 + (v - cy) ** 2) / (sz ** 2)))
                a = np.maximum(a, m * (1 - t / 0.45))
                rgb = rgb * (1 - m[..., None] * 0.7)
        frames.append(np.concatenate([rgb, a[..., None]], 2))
    return atlas(frames, 8, 8)


def tracer():
    W, H = 256, 32
    x = (np.arange(W) + 0.5) / W
    y = (np.arange(H) + 0.5) / H * 2 - 1
    X, Y = np.meshgrid(x, y)
    taper = smooth(0.0, 0.12, X) * smooth(1.0, 0.9, X) * (0.55 + 0.45 * X)
    core = np.exp(-(Y / 0.10) ** 2) * taper
    halo = np.exp(-(Y / 0.45) ** 2) * taper
    rows = []
    for col in ((1.0, 0.16, 0.05), (0.25, 1.0, 0.18)):
        rgb = np.stack([np.clip(core + halo * c, 0, 1) for c in col], 2)
        rows.append(rgb)
    return np.concatenate(rows, 0)


def glint(F=256):
    u, v = grid(F)
    r = np.sqrt(u ** 2 + v ** 2)
    th = np.arctan2(v, u)
    star = (np.abs(np.cos(th * 3)) ** 80) * np.exp(-r / 0.35) * 0.7
    streak = np.exp(-(v / 0.012) ** 2) * np.exp(-np.abs(u) / 0.55) * 0.8
    core = np.exp(-(r / 0.05) ** 2) * 1.2 + np.exp(-(r / 0.18) ** 2) * 0.35
    I = np.clip(core + star + streak, 0, 1.4)
    return np.stack([np.clip(I, 0, 1), np.clip(I * 0.95, 0, 1), np.clip(I * 0.86, 0, 1)], 2)


JOBS = {
    'smoke': ('TEX_VFX_SMOKE_FLIPBOOK', 'T_VFX_SmokePuff_8x8_BC.png', smoke,
              'Flipbook 8x8 (64 frames, 128 px), row-major from top-left. RGB = self-shadowed grey (light from top-left), A = opacity. '
              'Default, sRGB ON, Alpha Is Transparency ON, Max Size 1024, DXT5 Crunched q60, mips on, wrap Clamp. Particle: Texture Sheet Animation 8x8, '
              'Alpha Blended (premultiply-friendly), tint white smoke / green smoke / dust via start colour.'),
    'fire': ('TEX_VFX_FIRE_FLIPBOOK', 'T_VFX_Fire_8x8_E.png', fire,
             'Flipbook 8x8 (64 frames) seamless loop, emissive flames, black = empty. Additive. Default, sRGB ON, Max Size 1024, DXT1 Crunched q60, mips on, wrap Clamp. '
             'Play at 24-30 fps; HDR intensity x2-4 for bloom.'),
    'muzzle': ('TEX_VFX_MUZZLE', 'T_VFX_MuzzleFlash_4x2_E.png', muzzle,
               'Atlas 4x2 (256 px cells): row 0 = side flashes (muzzle at the cell\'s left edge, flash along +U), row 1 = front stars. Additive emissive, '
               'pick a random cell per shot, 1-2 frames. Default, sRGB ON, Max Size 1024, DXT1 Crunched q70, mips on, wrap Clamp.'),
    'sparks': ('TEX_VFX_SPARKS', 'T_VFX_Sparks_4x4_E.png', sparks,
               'Atlas 4x4 (128 px): cells 0-7 near-horizontal streaks (stretched billboards), 8-11 random streaks, 12-15 glints. Additive. '
               'Default, sRGB ON, Max Size 512, DXT1 Crunched q60, wrap Clamp.'),
    'dust': ('TEX_VFX_DUST_IMPACT', 'T_VFX_DustImpact_8x8_BC.png', dust,
             'Flipbook 8x8 (64 frames): bullet-impact dust burst rising from the cell bottom-centre with ballistic chips early; RGB lit grey, A opacity. '
             'Tint per surface (sand C9A87A, plaster D8D0C0, stone A99A86). Default, sRGB ON, Alpha Is Transparency ON, Max Size 1024, DXT5 Crunched q60, wrap Clamp. '
             'Pivot the particle at the bottom-centre.'),
    'tracer': ('TEX_VFX_TRACER', 'T_VFX_Tracer_E.png', tracer,
               '256x64: top half red tracer (Western), bottom half green (Eastern bloc ammo); white core, coloured halo, tapered ends. Additive on a stretched '
               'quad / LineRenderer (UV v 0.5-1 red, 0-0.5 green). Default, sRGB ON, Max Size 256, DXT1 Crunched q70, wrap Clamp.'),
    'glint': ('TEX_VFX_GLINT', 'T_VFX_Glint_E.png', glint,
              'Scope glint (Beat 5 marksman tell): bright core, 6-point star, horizontal anamorphic streak. Additive billboard, HDR x6-10 so it blooms at 300 m. '
              'Default, sRGB ON, Max Size 256, DXT1 Crunched q70, mips on, wrap Clamp.'),
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--only', default=','.join(JOBS))
    a = ap.parse_args()
    rows = []
    from PIL import Image
    for k in a.only.split(','):
        mid, fname, fn, note = JOBS[k]
        img = fn()
        rel = f'{FX}/{fname}'
        p = os.path.join(READY, rel)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        T.save8(img, p)
        rows.append({'manifest_id': mid, 'file': rel, 'target_path': rel, 'type': 'texture_vfx', 'import_notes': note, 'tool': 'wp10_vfx'})
        # contact: over dark and over sky-ish background
        im = img[..., :3]
        if img.shape[2] == 4:
            al = img[..., 3:4]
            bg = np.ones_like(im) * np.array([0.55, 0.6, 0.68], np.float32)
            im = im * al + bg * (1 - al)
        sh = Image.fromarray((np.clip(im, 0, 1) * 255).astype(np.uint8))
        sh.thumbnail((1024, 1024))
        os.makedirs(os.path.join(RAW, '_contact'), exist_ok=True)
        sh.save(os.path.join(RAW, '_contact', f'VFX_{k}.jpg'), quality=88)
        print(k, img.shape, rel)
    idx = os.path.join(READY, 'import_index_cc0.json')
    cur = json.load(open(idx)) if os.path.exists(idx) else []
    files = {r['file'] for r in rows}
    cur = [c for c in cur if c['file'] not in files] + rows
    cur.sort(key=lambda r: (r['manifest_id'], r['file']))
    json.dump(cur, open(idx, 'w'), indent=1)


if __name__ == '__main__':
    main()
