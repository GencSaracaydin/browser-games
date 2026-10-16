#!/usr/bin/env python3
"""WP10 hdri_prep (TECH_PLAN 8.5, GDD 9.3): Poly Haven HDRI -> sun-free runtime sky + EditorOnly bake HDR.

  Tools/.venv/bin/python -I Tools/tex/hdri_prep.py [--ids HDRI_KIARA_8_SUNSET,...]

Per HDRI (raw: RawAssets/wp10/cc0/<ID>/<id>_2k.hdr):
 1. Measures the sun: brightest pixel -> centroid of the disc, azimuth/elevation, disc irradiance E_sun
    (RGB, HDR units) and its colour. Maps without a disc (kiara_9_dusk) use the brightest horizon glow, or
    the rotation of a sibling map shot at the same spot (--align-to, default for kiara_9_dusk: kiara_8_sunset).
 2. Paints the sun disc + hot core out (log-space inpainting) -> energy now comes only from the realtime SUN.
 3. Rotates the panorama so the sun sits at compass azimuth 258 deg (GDD 9.1: light Euler (el, 78, 0)) under
    Unity's Skybox/Panoramic mapping (u = 0.5 - atan2(dir.z, dir.x) / 2pi, v = 1 - acos(dir.y) / pi), so
    _Rotation = 0 on both sky textures and they blend without any offset.
 4. Writes
    Art/HDRI/Runtime/T_Sky_<id>_LDR.png   2048x1024 sRGB, scene-linear (NOT filmic-tonemapped) with a soft
                                          shoulder above 0.7: in-game ACES tonemapping applies once.
    Art/HDRI/EditorOnly/<id>_nosun_2k.exr 2048x1024 half RGB, same rotation, for bakes / SH / probes only.
    Art/HDRI/Runtime/<id>_sky.json        sun data, LDR->radiance scale, Unity-units exposure, SH L2 ambient.
"""
import argparse, json, math, os, sys
import numpy as np
import cv2

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wp10_texlib as T  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')
READY = os.path.join(ROOT, 'RawAssets', 'wp10', 'ready')
TARGET_AZ = 258.0
LUMA = np.array([0.2126, 0.7152, 0.0722], np.float32)
IDS = {'HDRI_KIARA_8_SUNSET': 'kiara_8_sunset', 'HDRI_KIARA_9_DUSK': 'kiara_9_dusk',
       'HDRI_ROGLAND_SUNSET': 'rogland_sunset', 'HDRI_KIARA_1_DAWN': 'kiara_1_dawn'}
ALIGN = {'HDRI_KIARA_9_DUSK': 'HDRI_KIARA_8_SUNSET'}   # same tripod spot 21 min later (checked by terrain correlation)
# land mask source: the dusk frame (darkest land vs sky) also masks the sunset frame shot from the same spot
MASK_FROM = {'HDRI_KIARA_8_SUNSET': 'HDRI_KIARA_9_DUSK'}
BLUE_THR = {}
MASK_FROM['HDRI_KIARA_1_DAWN'] = 'HDRI_KIARA_9_DUSK'   # same spot, other heading: mask rolled by the terrain shift
# skyline grow (px at 2k): 9 clears the dark haze rim that traced the removed mountains in kiara_9_dusk
GROW_PX = 9
# azimuth smoothing (median px, gauss px) of the horizon colour the extrapolation starts from: kiara_1_dawn has
# clouds sitting on the skyline, which the default smoothing turned into vertical stripes
BND_SMOOTH = {'HDRI_KIARA_1_DAWN': (151, 40)}
KEEP_LAND = {'HDRI_ROGLAND_SUNSET'}  # P2 fallback: sunlit rock defeats the land detector, ship with its land


def load_hdr(p):
    im = cv2.imread(p, cv2.IMREAD_UNCHANGED)
    if im is None:
        raise RuntimeError(p)
    return np.ascontiguousarray(im[..., ::-1].astype(np.float32))


def dirs(h, w):
    """Unity world directions for each texel centre under the Skybox/Panoramic convention."""
    u = (np.arange(w) + 0.5) / w
    v = 1.0 - (np.arange(h) + 0.5) / h          # Unity v (row 0 of the image = top = v ~ 1)
    lon = (0.5 - u) * 2 * math.pi                # = atan2(z, x)
    lat = (1.0 - v) * math.pi                    # = acos(y)
    LON, LAT = np.meshgrid(lon, lat)
    x = np.sin(LAT) * np.cos(LON)
    z = np.sin(LAT) * np.sin(LON)
    y = np.cos(LAT)
    return x, y, z, LAT


def solid_angle(h, w):
    lat = (np.arange(h) + 0.5) / h * math.pi
    return (np.sin(lat) * (math.pi / h) * (2 * math.pi / w))[:, None] * np.ones((1, w))


def u_for_azimuth(az_deg):
    x, z = math.sin(math.radians(az_deg)), math.cos(math.radians(az_deg))
    return (0.5 - math.atan2(z, x) / (2 * math.pi)) % 1.0


def azimuth_for_u(u):
    lon = (0.5 - u) * 2 * math.pi
    x, z = math.cos(lon), math.sin(lon)
    return math.degrees(math.atan2(x, z)) % 360


def find_sun(img):
    h, w, _ = img.shape
    lum = img @ LUMA
    y0, x0 = np.unravel_index(np.argmax(lum), lum.shape)
    peak = float(lum[y0, x0])
    # local background: median in an annulus 6-12 deg around the peak (horizontal wrap)
    px_deg = w / 360.0
    yy, xx = np.mgrid[0:h, 0:w]
    dx = (xx - x0 + w / 2) % w - w / 2
    dy = yy - y0
    r = np.sqrt(dx ** 2 + dy ** 2) / px_deg
    bg = float(np.median(lum[(r > 6) & (r < 12)]))
    return x0, y0, peak, bg, r, lum


def pushpull(vals, valid, wrap=True):
    """Fill invalid pixels from valid ones with a mask-weighted image pyramid (smooth, artefact-free)."""
    v = vals.astype(np.float64)
    m = valid.astype(np.float64)
    pad = 0
    if wrap:
        pad = v.shape[1] // 8
        v = np.concatenate([v[:, -pad:], v, v[:, :pad]], 1)
        m = np.concatenate([m[:, -pad:], m, m[:, :pad]], 1)
    pyr = [(v * m[..., None], m)]
    while min(pyr[-1][1].shape) > 2 and (pyr[-1][1] < 1e-6).any():
        vw, ww = pyr[-1]
        h2, w2 = (vw.shape[0] + 1) // 2 * 2, (vw.shape[1] + 1) // 2 * 2
        vw = np.pad(vw, ((0, h2 - vw.shape[0]), (0, w2 - vw.shape[1]), (0, 0)), mode='edge')
        ww = np.pad(ww, ((0, h2 - ww.shape[0]), (0, w2 - ww.shape[1])), mode='edge')
        vw = vw.reshape(h2 // 2, 2, w2 // 2, 2, -1).sum((1, 3))
        ww = ww.reshape(h2 // 2, 2, w2 // 2, 2).sum((1, 3))
        pyr.append((vw, ww))
    vw, ww = pyr[-1]
    cur = vw / np.maximum(ww, 1e-9)[..., None]
    for vw, ww in reversed(pyr[:-1]):
        up = cv2.resize(cur.astype(np.float32), (vw.shape[1], vw.shape[0]), interpolation=cv2.INTER_LINEAR).astype(np.float64)
        a = np.clip(ww, 0, 1)[..., None]
        here = vw / np.maximum(ww, 1e-9)[..., None]
        cur = here * a + up * (1 - a)
    if wrap:
        cur = cur[:, pad:-pad]
    return cur.astype(np.float32)


def paint_out(img, r, lum, bg, radius_deg=3.5, k=6.0):
    mask = ((r < radius_deg) & (lum > bg * k)).astype(np.uint8)
    mask = cv2.dilate(mask, np.ones((5, 5), np.uint8), iterations=2)
    logi = np.log(np.maximum(img, 1e-5)).astype(np.float32)
    filled = np.exp(pushpull(logi, mask == 0))
    soft = cv2.GaussianBlur(mask.astype(np.float32), (0, 0), 1.5)
    soft = np.maximum(soft, mask)[..., None]
    res = img * (1 - soft) + filled * soft
    return res.astype(np.float32), mask


def skyline_mask(img, ref_el=15.0, thr=0.35, blue_thr=None, grow_px=3):
    """Terrain mask (True = terrain): land is much darker than the sky at sunset/dusk (and, for daylight maps,
    less blue). The mask is column-monotonic and grown upward by grow_px to remove the dark rim."""
    h, w, _ = img.shape
    lum = img @ LUMA
    ry = int((0.5 - ref_el / 180) * h)
    ref = cv2.GaussianBlur(lum[ry - 3:ry + 4].mean(0)[None, :], (0, 0), 6)[0]
    ratio = lum / np.maximum(ref[None, :], 1e-6)
    terr = ratio < thr
    if blue_thr is not None:
        blue = img[..., 2] / np.maximum(img.sum(2), 1e-6)
        terr |= blue < blue_thr
    top = int((0.5 - 14.0 / 180) * h)
    sky_line = np.full(w, h // 2, np.int32)
    for x in range(w):
        col = terr[top:, x]
        run = np.convolve(col.astype(np.int32), np.ones(3, np.int32), 'valid') >= 3
        sky_line[x] = top + int(np.argmax(run)) if run.any() else h // 2
    sky_line = np.minimum(sky_line, h // 2) - grow_px
    yy = np.arange(h)[:, None]
    return yy >= sky_line[None, :], sky_line


def _smooth_az(a, med_px, sig_px):
    """Robust smoothing of a per-column signal across azimuth (wraps)."""
    from scipy.ndimage import median_filter, gaussian_filter1d
    return gaussian_filter1d(median_filter(a, size=(med_px,) + (1,) * (a.ndim - 1), mode='wrap'), sig_px, axis=0, mode='wrap')


def smooth_skyline(line, win_px=121, sig_px=30):
    """Replace the mountain-shaped skyline by a smooth envelope (the highest land within +-win/2 px, Gaussian
    smoothed). The extrapolated sky then meets the photographed sky along a gentle curve instead of tracing
    the removed ridges (a traced scallop showed as a dark line in kiara_9_dusk)."""
    from scipy.ndimage import minimum_filter1d, gaussian_filter1d
    env = minimum_filter1d(line.astype(np.float32), win_px, mode='wrap')
    env = gaussian_filter1d(env, sig_px, mode='wrap')
    return np.minimum(np.floor(env).astype(np.int32), line)


def sky_only(img, terr, sky_line, fit_deg=10.0, bnd_smooth=(31, 6)):
    """Replace the land with sky extrapolated to the horizon: per column, the sky value at the skyline (robustly
    smoothed across azimuth) continued downward with the column's vertical log-gradient (median-smoothed, only
    allowed to brighten toward the horizon, capped at 3x); below 0 deg the horizon value continues (the skybox
    fades the lower hemisphere to fog)."""
    h, w, _ = img.shape
    el = (0.5 - (np.arange(h) + 0.5) / h) * 180.0
    logi = np.log(np.maximum(img, 1e-5))
    bnd = np.zeros((w, 3)); slope = np.zeros((w, 3)); elb = np.zeros(w)
    nfit = int(fit_deg / 180 * h)
    for x in range(w):
        y1 = max(sky_line[x], 8)
        bnd[x] = logi[y1 - 6:y1 - 2, x].mean(0)
        elb[x] = el[y1 - 4]
        y0 = max(0, y1 - nfit)
        e = el[y0:y1]
        X = np.stack([np.ones_like(e), e], 1)
        coef, *_ = np.linalg.lstsq(X, logi[y0:y1, x], rcond=None)
        slope[x] = coef[1]
    bnd = _smooth_az(bnd, *bnd_smooth)
    slope = np.clip(_smooth_az(slope, 61, 12), -0.25, 0.05)   # may keep darkening gently toward the horizon
    out = img.copy()
    for x in range(w):
        y0 = sky_line[x]
        ys = np.arange(max(0, y0 - 2), h)
        e = np.maximum(el[ys], 0.0)
        lv = bnd[x][None, :] + slope[x][None, :] * (e[:, None] - elb[x])
        lv = np.minimum(lv, bnd[x][None, :] + math.log(3.0))
        val = np.exp(lv)
        t = np.clip((ys - (y0 - 2)) / 8.0, 0, 1)[:, None]
        out[ys, x] = img[ys, x] * (1 - t) + val * t
    # below the horizon: the horizon colour heavily smoothed across azimuth, falling off toward the nadir
    # (Sky/PanoBlend fades this region to the fog colour anyway; this keeps it stripe-free if the fade is soft)
    from scipy.ndimage import gaussian_filter1d
    y_h = h // 2
    hz = np.log(np.maximum(out[y_h - 1], 1e-5))
    hz = np.exp(gaussian_filter1d(hz, 40, axis=0, mode='wrap'))
    ys = np.arange(y_h - 6, h)
    e = el[ys]
    fall = np.where(e > 0, 1.0, 0.3 + 0.7 * np.exp(np.minimum(e, 0) / 10.0))[:, None, None]
    blend = np.clip((y_h - ys) / -6.0 + 1.0, 0, 1)[:, None, None]
    blend = np.clip(1.0 - (ys - (y_h - 6)) / 6.0, 0, 1)[:, None, None]
    out[ys] = out[ys] * blend + (hz[None] * fall) * (1 - blend)
    return out


def sh9(img, sa):
    """Radiance -> L2 SH (real basis, Unity coefficient order L00, L1-1(y), L10(z), L11(x), L2-2(xy), L2-1(yz),
    L20(3y^2-1 w.r.t. Unity y? no: standard z), L21(xz), L22(x^2-y^2)) evaluated with Unity's x, y, z.
    Returns the cosine-convolved coefficients / pi (what a white Lambert surface shows), matching the scale of
    RenderSettings.ambientProbe when Unity builds it from a skybox."""
    h, w, _ = img.shape
    x, y, z, _ = dirs(h, w)
    Y = [0.282095 * np.ones_like(x), 0.488603 * y, 0.488603 * z, 0.488603 * x,
         1.092548 * x * y, 1.092548 * y * z, 0.315392 * (3 * z * z - 1), 1.092548 * x * z, 0.546274 * (x * x - y * y)]
    A = [1.0, 2 / 3, 2 / 3, 2 / 3, 0.25, 0.25, 0.25, 0.25, 0.25]  # A_l / pi
    co = []
    for b, a in zip(Y, A):
        co.append([float((img[..., c] * b * sa).sum() * a) for c in range(3)])
    return co


def eval_sh(co, d):
    x, y, z = d
    Y = [0.282095, 0.488603 * y, 0.488603 * z, 0.488603 * x, 1.092548 * x * y, 1.092548 * y * z,
         0.315392 * (3 * z * z - 1), 1.092548 * x * z, 0.546274 * (x * x - y * y)]
    return [sum(co[i][c] * Y[i] for i in range(9)) for c in range(3)]


def log_shoulder(v, knee, top, c=0.3):
    out = v.copy()
    hi = v > knee
    out[hi] = knee + (1 - knee) * np.log1p((v[hi] - knee) / c) / np.log1p(max(top - knee, 1e-3) / c)
    return np.minimum(out, 1.0)


def soft_shoulder(v, knee=0.7):
    out = v.copy()
    hi = v > knee
    t = v[hi] - knee
    out[hi] = knee + (1 - knee) * (1 - np.exp(-t / (1 - knee)))
    return out


def terrain_shift(a, b):
    """Horizontal offset (px) that best aligns the lower hemispheres of two panoramas (log-lum gradients)."""
    def sig(img):
        h = img.shape[0]
        band = np.log(np.maximum(img[h // 2 + 4:h // 2 + 120] @ LUMA, 1e-4))
        return np.abs(np.diff(band, axis=1)).mean(0)
    sa, sb = sig(a), sig(b)
    sa = (sa - sa.mean()) / (sa.std() + 1e-6)
    sb = (sb - sb.mean()) / (sb.std() + 1e-6)
    cc = np.fft.ifft(np.fft.fft(sa) * np.conj(np.fft.fft(sb))).real
    s = int(np.argmax(cc))
    return s if s < len(sa) // 2 else s - len(sa)


def write_exr(path, img):
    import OpenEXR
    h, w, _ = img.shape
    hdr = {'compression': OpenEXR.ZIP_COMPRESSION, 'type': OpenEXR.scanlineimage}
    ch = {'RGB': img.astype(np.float16)}
    with OpenEXR.File(hdr, ch) as f:
        f.write(path)


def process(mid, cache):
    pid = IDS[mid]
    src = os.path.join(RAW, mid, f'{pid}_2k.hdr')
    img = load_hdr(src)
    h, w, _ = img.shape
    sa = solid_angle(h, w)
    x0, y0, peak, bg, r, lum = find_sun(img)
    has_disc = peak > bg * 200
    info = {'manifest_id': mid, 'source': f'Poly Haven {pid} 2k .hdr (CC0)', 'size': [w, h]}
    if has_disc:
        cleaned, mask = paint_out(img, r, lum, bg)
        disc = mask > 0
        excess = np.clip(img - cleaned, 0, None)
        E = [float((excess[..., c] * sa).sum()) for c in range(3)]
        wgt = (excess @ LUMA) * disc
        cx = float(np.angle((wgt * np.exp(2j * math.pi * (np.arange(w)[None, :] + 0.5) / w)).sum()) / (2 * math.pi) * w) % w
        cy = float((wgt * (np.arange(h)[:, None] + 0.5)).sum() / wgt.sum())
        sun_u, sun_el = cx / w, (0.5 - cy / h) * 180.0
        info['sun'] = {'found': 'disc', 'u_src': round(sun_u, 5), 'elevation_deg': round(sun_el, 2),
                       'E_sun_rgb_hdr_units': [round(e, 3) for e in E],
                       'E_sun_lum': round(float(np.dot(E, LUMA)), 3),
                       'sun_rgb_normalised': [round(e / max(E), 4) for e in E],
                       'painted_out_px': int(disc.sum()), 'peak_radiance': round(peak, 1)}
    else:
        cleaned = img
        band = lum[int(h * 0.44):h // 2]
        prof = cv2.GaussianBlur(np.tile(band.mean(0), 3)[None, :].astype(np.float32), (0, 0), w / 72)[0, w:2 * w]
        sun_u = (int(np.argmax(prof)) + 0.5) / w
        info['sun'] = {'found': 'horizon_glow', 'u_src': round(sun_u, 5), 'elevation_deg': None}
    rot_from = None
    if mid in ALIGN and ALIGN[mid] in cache:
        ref = cache[ALIGN[mid]]
        shift_check = terrain_shift(ref['img_raw'], img)
        rot_from = ALIGN[mid]
        sun_u_used = (ref['sun_u'] - shift_check / w) % 1.0
        info['sun']['aligned_to'] = ALIGN[mid]
        info['sun']['terrain_shift_px_vs_ref'] = shift_check
        info['sun']['glow_vs_ref_sun_deg'] = round(((sun_u - sun_u_used + 0.5) % 1.0 - 0.5) * 360, 2)
    else:
        sun_u_used = sun_u
    tgt_u = u_for_azimuth(TARGET_AZ)
    shift = int(round((tgt_u - sun_u_used) * w))
    rot = np.roll(cleaned, shift, axis=1)          # EXR: keeps the land (ground bounce for bakes / SH)
    mref = MASK_FROM.get(mid, mid)
    own = skyline_mask(img, blue_thr=BLUE_THR.get(mid), grow_px=GROW_PX)
    if mref != mid:
        ref_img = load_hdr(os.path.join(RAW, mref, f'{IDS[mref]}_2k.hdr'))
        rterr, rline = skyline_mask(ref_img, blue_thr=BLUE_THR.get(mref), grow_px=GROW_PX)
        tsh = terrain_shift(ref_img, img)          # ref = roll(img, tsh)  ->  img = roll(ref, -tsh)
        rline = np.roll(rline, -tsh)
        sky_line = np.minimum(rline, own[1])       # union of both land masks (higher skyline wins)
        info['mask_shift_px'] = int(tsh)
    else:
        sky_line = own[1]
    if mid not in KEEP_LAND:
        sky_line = smooth_skyline(sky_line)
    terr = np.arange(h)[:, None] >= sky_line[None, :]
    sky = sky_only(cleaned, terr, sky_line, bnd_smooth=BND_SMOOTH.get(mid, (31, 6))) if mid not in KEEP_LAND else cleaned
    sky_rot = np.roll(sky, shift, axis=1)
    info['skyline'] = {'mask_from': mref, 'land_removed': mid not in KEEP_LAND, 'max_land_elevation_deg': round(float((0.5 - sky_line.min() / h) * 180), 2),
                       'median_land_elevation_deg': round(float((0.5 - np.median(sky_line) / h) * 180), 2),
                       'note': 'LDR sky has the photographed land removed and the sky extrapolated to the horizon, so the in-game '
                               'SunDisc never sits in front of painted mountains; far hills come from the game geometry.'}
    info['rotation'] = {'target_azimuth_deg': TARGET_AZ, 'target_u': round(tgt_u, 5), 'roll_px': shift,
                        'equivalent_skybox_rotation_deg_if_unrotated': round(-(tgt_u - sun_u_used) * 360.0, 2),
                        'convention': 'Unity Skybox/Panoramic: u = 0.5 - atan2(dir.z, dir.x)/2pi, v = 1 - acos(dir.y)/pi; '
                                      'compass az from +Z toward +X. Shipped textures are pre-rotated: use _Rotation = 0.',
                        'rotation_source': rot_from or 'own sun/glow'}
    # LDR: scene-linear exposure from the upper-hemisphere sky (sun removed)
    up = sky_rot[: h // 2] @ LUMA
    p50 = float(np.median(up))
    p99 = float(np.percentile(up, 99.0))
    scale = 0.2 / p50
    lin = sky_rot * scale
    mx = lin.max(axis=2, keepdims=True)
    top = float(np.percentile(mx, 99.99))
    sh_mx = log_shoulder(mx, 0.6, top)
    lin = lin * (sh_mx / np.maximum(mx, 1e-6))   # hue-preserving shoulder
    ldr = T.lin_to_srgb(np.clip(lin, 0, 1))
    if ldr.shape[1] != 2048:
        ldr = T.resize_srgb(ldr, (2048, 1024))
    rt = 'Assets/_Game/Art/HDRI/Runtime'
    eo = 'Assets/_Game/Art/HDRI/EditorOnly'
    os.makedirs(os.path.join(READY, rt), exist_ok=True)
    os.makedirs(os.path.join(READY, eo), exist_ok=True)
    p_ldr = f'{rt}/T_Sky_{pid}_LDR.png'
    rng = np.random.default_rng(258)          # 1-LSB triangular dither: breaks 8-bit/DXT1 banding in smooth gradients
    ldr = ldr + (rng.random(ldr.shape) - rng.random(ldr.shape)).astype(np.float32) / 255.0
    T.save8(ldr, os.path.join(READY, p_ldr))
    p_exr = f'{eo}/{pid}_nosun_2k.exr'
    write_exr(os.path.join(READY, p_exr), rot)
    co = sh9(rot, sa)
    ref_E = info['sun'].get('E_sun_lum') or (cache.get('HDRI_KIARA_8_SUNSET', {}).get('E_lum'))
    info['ldr'] = {'file': p_ldr, 'linear_scale': scale, 'radiance_from_texture': f'radiance_hdr = linear(texel) / {scale:.6g} below linear 0.6; above 0.6 a hue-preserving log shoulder compresses the glow (SunDisc + bloom restore the core)',
                   'p99_sky_radiance': round(p99, 4)}
    if ref_E:
        # URP: a directional light of intensity I lights a white Lambert surface to radiance I (no 1/pi);
        # physically that surface shows E/pi. Matching sky to sun: L_unity = L_hdr * pi * I_sun / E_sun.
        k = math.pi / ref_E / scale
        info['ldr']['exposure_multiplier_per_unit_sun_intensity'] = round(k, 4)
        info['ldr']['exposure_ev_for_sun_intensity_1'] = round(math.log2(k), 3)
        info['ldr']['exposure_note'] = ('Sky/PanoBlend output = linear(texel) * exposure; physically consistent with the realtime sun when '
                                        'exposure = exposure_multiplier_per_unit_sun_intensity * I_sun(s). With I_sun 2.0 at s=0 that is '
                                        f'{2 * k:.3f}x (EV {math.log2(2 * k):.2f}). Reference sun: ' + ('own disc' if info['sun'].get('E_sun_lum') else 'kiara_8_sunset disc'))
        info['sh_ambient_unity_sun1'] = [[round(c * math.pi / ref_E, 5) for c in coef] for coef in co]
    info['sh_ambient_hdr_units'] = [[round(c, 5) for c in coef] for coef in co]
    info['sh_note'] = ('L2 SH, Unity SphericalHarmonicsL2 order and axes (Y1-1~y, Y10~z, Y11~x, Y20~3z^2-1, Y22~x^2-y^2, '
                       'real basis constants included at evaluation), cosine-convolved and divided by pi (= radiance of a '
                       'white Lambert surface). *_unity_sun1 is scaled to URP units for a sun of intensity 1; multiply by '
                       'I_sun(s). Cross-check against AmbientSHCapture in the editor; prefer the editor capture if they differ.')
    for name, d in (('up', (0, 1, 0)), ('horizon_sun_side', (math.sin(math.radians(TARGET_AZ)), 0, math.cos(math.radians(TARGET_AZ)))),
                    ('horizon_away', (-math.sin(math.radians(TARGET_AZ)), 0, -math.cos(math.radians(TARGET_AZ)))), ('down', (0, -1, 0))):
        info.setdefault('sh_eval_hdr_units', {})[name] = [round(v, 4) for v in eval_sh(co, d)]
    # horizon / zenith colours for fog matching (sRGB of the LDR)
    hb = ldr[int(1024 * 0.47):int(1024 * 0.5)].reshape(-1, 3).mean(0)
    zb = ldr[:60].reshape(-1, 3).mean(0)
    info['ldr']['horizon_mean_srgb_hex'] = '#%02X%02X%02X' % tuple(int(c * 255 + .5) for c in hb)
    info['ldr']['zenith_mean_srgb_hex'] = '#%02X%02X%02X' % tuple(int(c * 255 + .5) for c in zb)
    info['editor_only_exr'] = {'file': p_exr, 'note': 'sun painted out, same rotation; bake / AmbientSHCapture / reflection probes only. Never referenced by shipped content.'}
    p_json = f'{rt}/{pid}_sky.json'
    json.dump(info, open(os.path.join(READY, p_json), 'w'), indent=1)
    cache[mid] = {'img_raw': img, 'sun_u': sun_u_used, 'E_lum': info['sun'].get('E_sun_lum') or ref_E}
    # preview: rotated LDR with the azimuth grid and the target sun position
    prev = (ldr * 255).astype(np.uint8)[..., ::-1].copy()
    prev = cv2.resize(prev, (1600, 800), interpolation=cv2.INTER_AREA)
    for az in range(0, 360, 30):
        xu = int(u_for_azimuth(az) * 1600)
        cv2.line(prev, (xu, 395), (xu, 405), (0, 0, 255), 1)
        cv2.putText(prev, str(az), (xu + 2, 420), cv2.FONT_HERSHEY_SIMPLEX, 0.35, (0, 0, 255), 1)
    xu = int(tgt_u * 1600)
    el = info['sun'].get('elevation_deg') or 0
    cv2.circle(prev, (xu, int(400 - el / 180 * 800)), 9, (0, 255, 255), 1)
    cv2.putText(prev, f'{mid} rotated: sun target az 258, el {el}', (8, 20), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (0, 255, 255), 1)
    os.makedirs(os.path.join(RAW, '_contact'), exist_ok=True)
    cv2.imwrite(os.path.join(RAW, '_contact', f'{mid}.jpg'), prev, [cv2.IMWRITE_JPEG_QUALITY, 88])
    rows = [
        {'manifest_id': mid, 'file': p_ldr, 'target_path': p_ldr, 'type': 'texture_sky_ldr', 'tool': 'hdri_prep',
         'import_notes': 'Sky/PanoBlend texture A/B: Texture Shape 2D, sRGB ON, Max Size 2048, wrap U Repeat / V Clamp, DXT1 Crunched q70, '
                         'mips ON (generate with Kaiser), aniso 1, no Read/Write. Pre-rotated: _Rotation = 0 puts the sun/glow at az 258. '
                         'Scene-linear (not filmic): set _Exposure from the json (exposure_multiplier_per_unit_sun_intensity x I_sun).'},
        {'manifest_id': mid, 'file': p_exr, 'target_path': p_exr, 'type': 'hdri_editor_only', 'tool': 'hdri_prep',
         'import_notes': 'EditorOnly: Texture Shape Cube, Mapping Latitude-Longitude Layout, RGBA Half / BC6H, Max Size 2048. Bake environment '
                         '(Lighting > Environment), AmbientSHCapture, reflection probe bakes only. Must never be referenced by shipped content '
                         '(no HDR cubemap ships). Same rotation as the LDR.'},
        {'manifest_id': mid, 'file': p_json, 'target_path': p_json, 'type': 'data_json', 'tool': 'hdri_prep',
         'import_notes': 'TextAsset with the sun measurement, rotation convention, LDR scale/exposure and SH L2 ambient (for LightStage / AmbientSHSet cross-check).'}]
    return info, rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ids', default='HDRI_KIARA_8_SUNSET,HDRI_KIARA_9_DUSK,HDRI_ROGLAND_SUNSET,HDRI_KIARA_1_DAWN')
    a = ap.parse_args()
    cache, rows = {}, []
    for mid in a.ids.split(','):
        info, rr = process(mid, cache)
        rows += rr
        s = info['sun']
        print(mid, 'sun', s.get('found'), 'u', s.get('u_src'), 'el', s.get('elevation_deg'), 'E', s.get('E_sun_lum'),
              'roll', info['rotation']['roll_px'], 'ev', info['ldr'].get('exposure_ev_for_sun_intensity_1'),
              'hz', info['ldr']['horizon_mean_srgb_hex'], 'zen', info['ldr']['zenith_mean_srgb_hex'], s.get('terrain_shift_px_vs_ref'), s.get('glow_vs_ref_sun_deg'))
    idx = os.path.join(READY, 'import_index_cc0.json')
    cur = json.load(open(idx)) if os.path.exists(idx) else []
    files = {r['file'] for r in rows}
    cur = [c for c in cur if c['file'] not in files] + rows
    cur.sort(key=lambda r: (r['manifest_id'], r['file']))
    json.dump(cur, open(idx, 'w'), indent=1)


if __name__ == '__main__':
    main()
