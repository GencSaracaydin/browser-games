"""Shared image helpers for the WP10_CC0 texture tools (numpy + Pillow + OpenCV).

Conventions (TECH_PLAN D4, 3.10):
  _BC  base colour, sRGB, RGB (RGBA when it carries opacity)
  _N   tangent-space normal, OpenGL (+Y up), linear
  _M   mask, linear: R metallic, G ambient occlusion, B height (terrain layers only, else 0), A smoothness
  _H   height, linear, 8-bit grey (only where parallax is used)
"""
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None


def load(path, mode=None):
    im = Image.open(path)
    if im.mode in ('I;16', 'I;16B', 'I'):
        a = np.asarray(im).astype(np.float32) / 65535.0
        return a
    if mode:
        im = im.convert(mode)
    elif im.mode not in ('L', 'RGB', 'RGBA'):
        im = im.convert('RGB')
    return np.asarray(im).astype(np.float32) / 255.0


def load_gray(path):
    a = load(path)
    if a.ndim == 3:
        a = a[..., :3].mean(axis=2) if a.shape[2] >= 3 else a[..., 0]
    return a


def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def _resize_ch(ch, size):
    """Lanczos resize of one float channel (PIL 'F' mode keeps float precision)."""
    if ch.shape[0] == size[1] and ch.shape[1] == size[0]:
        return ch
    im = Image.fromarray(ch.astype(np.float32), mode='F')
    return np.asarray(im.resize(size, Image.LANCZOS)).astype(np.float32)


def resize(a, size):
    """size = (w, h). Works on HxW or HxWxC float arrays."""
    if a.ndim == 2:
        return _resize_ch(a, size)
    return np.stack([_resize_ch(a[..., i], size) for i in range(a.shape[2])], axis=2)


def resize_srgb(a, size):
    """Resize colour in linear light (avoids darkened high-contrast detail), alpha linearly."""
    if a.shape[1] == size[0] and a.shape[0] == size[1]:
        return a
    rgb = lin_to_srgb(resize(srgb_to_lin(a[..., :3]), size))
    if a.shape[2] == 4:
        return np.concatenate([rgb, np.clip(resize(a[..., 3:4], size), 0, 1)], axis=2)
    return rgb


def decode_normal(a):
    n = a[..., :3] * 2.0 - 1.0
    l = np.linalg.norm(n, axis=2, keepdims=True)
    return n / np.maximum(l, 1e-6)


def encode_normal(n):
    l = np.linalg.norm(n, axis=2, keepdims=True)
    n = n / np.maximum(l, 1e-6)
    return np.clip(n * 0.5 + 0.5, 0, 1)


def resize_normal(a, size):
    n = decode_normal(a)
    if n.shape[1] != size[0] or n.shape[0] != size[1]:
        n = resize(n, size)
    n[..., 2] = np.maximum(n[..., 2], 0.02)
    return encode_normal(n)


def save8(a, path):
    a = np.clip(a, 0, 1)
    u = (a * 255.0 + 0.5).astype(np.uint8)
    mode = {2: 'L'}.get(u.ndim) or {1: 'L', 3: 'RGB', 4: 'RGBA'}[u.shape[2]]
    if u.ndim == 3 and u.shape[2] == 1:
        u = u[..., 0]
    Image.fromarray(u, mode=mode).save(path, optimize=False, compress_level=6)


def save16_gray(a, path):
    u = (np.clip(a, 0, 1) * 65535.0 + 0.5).astype(np.uint16)
    Image.fromarray(u, mode='I;16').save(path)


def pack_mask(metal, ao, smooth, height=None):
    h = height if height is not None else np.zeros_like(ao)
    return np.stack([metal, ao, h, smooth], axis=2)


def tile_seam_score(a):
    """Mean abs difference across the wrap edge vs. the mean neighbouring-pixel difference inside.
    ~1 means seamless; >2 means a visible seam."""
    g = a[..., :3].mean(axis=2) if a.ndim == 3 else a
    inner_x = np.abs(np.diff(g, axis=1)).mean()
    inner_y = np.abs(np.diff(g, axis=0)).mean()
    edge_x = np.abs(g[:, 0] - g[:, -1]).mean()
    edge_y = np.abs(g[0, :] - g[-1, :]).mean()
    return float(max(edge_x / max(inner_x, 1e-6), edge_y / max(inner_y, 1e-6)))


def highpass_normal(a, sigma_px):
    """Keep only the high-frequency part of a tiling normal map (wrap-around blur keeps it tileable)."""
    import cv2
    n = decode_normal(a)
    low = blur_wrap(n, sigma_px)
    # rotate the detail into the frame of the low-frequency normal -> subtract the slope
    d = n.copy()
    d[..., 0] = n[..., 0] - low[..., 0]
    d[..., 1] = n[..., 1] - low[..., 1]
    d[..., 2] = np.sqrt(np.clip(1 - d[..., 0] ** 2 - d[..., 1] ** 2, 0.0, 1.0))
    return encode_normal(d)


def blur_wrap(a, sigma_px):
    """Gaussian blur with wrap-around borders (OpenCV filters do not support BORDER_WRAP)."""
    import cv2
    p = int(sigma_px * 3) + 1
    k = int(sigma_px * 6) | 1
    if a.ndim == 2:
        pad = np.pad(a, p, mode='wrap')
        return cv2.GaussianBlur(pad, (k, k), sigma_px)[p:-p, p:-p]
    pad = np.pad(a, ((p, p), (p, p), (0, 0)), mode='wrap')
    out = np.stack([cv2.GaussianBlur(np.ascontiguousarray(pad[..., i]), (k, k), sigma_px)
                    for i in range(a.shape[2])], axis=2)
    return out[p:-p, p:-p]


def macro_flatten(bc, strength=0.6, sigma_frac=0.1):
    """Reduce low-frequency luminance blotches that make a tiling ground texture read as a grid.
    Works in linear light, keeps chroma; strength 0 = untouched, 1 = fully flattened above sigma."""
    rgb = srgb_to_lin(bc[..., :3])
    lum = (rgb * np.array([0.2126, 0.7152, 0.0722], np.float32)).sum(axis=2)
    low = blur_wrap(lum, max(2.0, bc.shape[0] * sigma_frac))
    ratio = np.power(np.clip(lum.mean() / np.maximum(low, 1e-4), 0.25, 4.0), strength)
    out = lin_to_srgb(np.clip(rgb * ratio[..., None], 0, 1))
    if bc.shape[2] == 4:
        out = np.concatenate([out, bc[..., 3:4]], axis=2)
    return out.astype(np.float32)


def chroma_flatten(bc, strength=0.5, sigma_frac=0.06):
    """Flatten low-frequency *chroma* blotches (keeps luminance and fine colour detail). Linear light."""
    rgb = srgb_to_lin(bc[..., :3])
    lum = np.maximum((rgb * np.array([0.2126, 0.7152, 0.0722], np.float32)).sum(axis=2), 1e-4)
    chrom = rgb / lum[..., None]
    low = blur_wrap(chrom, max(2.0, bc.shape[0] * sigma_frac))
    mean = chrom.reshape(-1, 3).mean(0)
    ratio = np.power(np.clip(mean[None, None, :] / np.maximum(low, 1e-4), 0.25, 4.0), strength)
    out = lin_to_srgb(np.clip(chrom * ratio * lum[..., None], 0, 1))
    if bc.shape[2] == 4:
        out = np.concatenate([out, bc[..., 3:4]], axis=2)
    return out.astype(np.float32)


def gray_flatten(a, strength=0.6, sigma_frac=0.06):
    """Same low-frequency flatten for a single linear channel (AO / height), keeps the mean."""
    low = blur_wrap(a, max(2.0, a.shape[0] * sigma_frac))
    return np.clip(a * np.power(np.clip(a.mean() / np.maximum(low, 1e-4), 0.25, 4.0), strength), 0, 1).astype(np.float32)


def ao_floor(ao, below=0.65, target=0.8):
    """Photogrammetry AO on large-area tiling sets is often normalised for looks (means of 0.35-0.6), which makes
    shaded ground far darker than real sky occlusion of a near-flat surface. If mean(ao) < below, rescale the
    occlusion depth so the mean becomes `target` (pattern kept): ao' = 1 - (1 - ao) * k."""
    m = float(ao.mean())
    if m >= below:
        return ao, None
    k = (1.0 - target) / max(1.0 - m, 1e-4)
    return np.clip(1.0 - (1.0 - ao) * k, 0, 1).astype(np.float32), round(k, 3)


def devegetate(bc, thresh=0.015, grow_px=3):
    """Remove green plant matter (leaves, grass blades) from a desert ground texture: pixels whose green exceeds
    red/blue are re-tinted to the local non-green colour at their own luminance (soft mask, wraps)."""
    import cv2
    rgb = srgb_to_lin(bc[..., :3])
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    greenness = g - np.maximum(r * 1.0, b * 1.15)
    m = (greenness > thresh).astype(np.float32)
    if m.sum() == 0:
        return bc, 0.0
    m = cv2.dilate(m, np.ones((3, 3), np.uint8), iterations=grow_px)
    m = blur_wrap(m, 1.5)
    m = np.clip(m * 1.5, 0, 1)
    keep = 1.0 - m
    sig = max(4.0, bc.shape[0] / 64)
    num = blur_wrap(rgb * keep[..., None], sig)
    den = np.maximum(blur_wrap(keep, sig), 1e-3)[..., None]
    local = num / den
    lw = np.array([0.2126, 0.7152, 0.0722], np.float32)
    lum = (rgb * lw).sum(axis=2, keepdims=True)
    llum = np.maximum((local * lw).sum(axis=2, keepdims=True), 1e-4)
    # plant matter is darker/greener than sand: lift it halfway toward the local luminance too
    tgt_lum = lum * 0.5 + llum * 0.5
    repl = local * (tgt_lum / llum)
    out = rgb * (1 - m[..., None]) + repl * m[..., None]
    res = lin_to_srgb(np.clip(out, 0, 1))
    if bc.shape[2] == 4:
        res = np.concatenate([res, bc[..., 3:4]], axis=2)
    return res.astype(np.float32), float((greenness > thresh).mean())


def green_mask(bc_srgb, thresh=0.015, grow_px=8):
    """Binary-ish soft mask of green plant matter (see devegetate), grown and feathered."""
    import cv2
    rgb = srgb_to_lin(bc_srgb[..., :3])
    gness = rgb[..., 1] - np.maximum(rgb[..., 0], rgb[..., 2] * 1.15)
    m = (gness > thresh).astype(np.uint8)
    # drop isolated specks, keep leaves / blades
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, connectivity=8)
    keep = np.zeros_like(m)
    for i in range(1, n):
        if st[i, cv2.CC_STAT_AREA] >= 6:
            keep[lab == i] = 1
    keep = cv2.dilate(keep, np.ones((3, 3), np.uint8), iterations=grow_px)
    return np.clip(blur_wrap(keep.astype(np.float32), grow_px * 0.6) * 1.4, 0, 1)


def clone_fix(maps, mask, candidates=None):
    """Clone-stamp repair of a tiling texture set: every map gets the masked region replaced by the same region
    of the texture shifted by one offset (wrap), feathered by `mask`. maps: dict name -> HxW[xC] float arrays at
    any resolution (mask is resized per map). The offset is picked among candidates (fractions of the size)
    so the source area has no masked pixels and the closest mean luminance."""
    ref = maps['bc']
    h, w = ref.shape[:2]
    if candidates is None:
        candidates = [(dy, dx) for dy in (0.13, 0.29, 0.41, 0.57, 0.71, 0.83) for dx in (0.17, 0.37, 0.53, 0.67, 0.89)]
    lum = ref[..., :3].mean(axis=2)
    mm = mask > 0.05
    best = None
    for fy, fx in candidates:
        sy, sx = int(fy * h), int(fx * w)
        src_mask = np.roll(np.roll(mask, sy, 0), sx, 1)
        overlap = float((src_mask * mask).sum())
        dl = abs(float(np.roll(np.roll(lum, sy, 0), sx, 1)[mm].mean()) - float(lum[~mm].mean()))
        score = overlap * 10 + dl
        if best is None or score < best[0]:
            best = (score, fy, fx)
    _, fy, fx = best
    out = {}
    for k, a in maps.items():
        hh, ww = a.shape[:2]
        m = mask if (hh, ww) == (h, w) else _resize_ch(mask, (ww, hh))
        src = np.roll(np.roll(a, int(fy * hh), 0), int(fx * ww), 1)
        mk = m[..., None] if a.ndim == 3 else m
        out[k] = (a * (1 - mk) + src * mk).astype(np.float32)
    return out, (fy, fx), float(mm.mean())


def grade(bc, target_srgb_mean=None, amount=1.0, saturation=1.0, chalk=0.0):
    """Colour grade in linear light that keeps per-pixel luminance:
    target_srgb_mean: shift the mean chromaticity toward this sRGB colour (amount 0..1, palette match);
    saturation: scale chroma around luminance; chalk: blend toward the pixel's own luminance-grey lifted by 15 %
    (sun-faded / chalked paint)."""
    lw = np.array([0.2126, 0.7152, 0.0722], np.float32)
    rgb = srgb_to_lin(bc[..., :3])
    lum = np.maximum((rgb * lw).sum(axis=2, keepdims=True), 1e-5)
    if target_srgb_mean is not None:
        t = srgb_to_lin(np.array(target_srgb_mean, np.float32))
        tc = t / max(float((t * lw).sum()), 1e-5)
        own = rgb.reshape(-1, 3).mean(0)
        oc = own / max(float((own * lw).sum()), 1e-5)
        rgb = rgb * np.power(tc / oc, amount)[None, None, :]
        rgb = rgb * (lum / np.maximum((rgb * lw).sum(axis=2, keepdims=True), 1e-5))
    if saturation != 1.0:
        rgb = lum + (rgb - lum) * saturation
    if chalk > 0:
        rgb = rgb * (1 - chalk) + (lum * 1.15) * chalk
    out = lin_to_srgb(np.clip(rgb, 0, 1))
    if bc.shape[2] == 4:
        out = np.concatenate([out, bc[..., 3:4]], axis=2)
    return out.astype(np.float32)
