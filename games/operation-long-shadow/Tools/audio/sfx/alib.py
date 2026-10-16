"""WP06_SFX audio library: IO, filters, dynamics, reverb, slicing, loops and measurements.

No ffmpeg needed (numpy + scipy + soundfile + pyloudnorm). Built on the research tool
RawAssets/falmedia/tools/fal_post.py (trim, limiter, loop) and extended for production:
true-peak safe finalisation, RBJ biquads, synthetic room impulse responses, onset slicing,
loop seam metrics and the per-file measurement record used in Docs/AUDIO_REVIEW.md.

Run with Tools/.venv/bin/python (numpy scipy soundfile pyloudnorm matplotlib librosa).
"""
from __future__ import annotations

import json
import math
import pathlib

import numpy as np
import pyloudnorm as pyln
import soundfile as sf
from scipy import signal
from scipy.ndimage import maximum_filter1d

SR = 48000


# ----------------------------------------------------------------------------- IO
def load(path, sr=SR, mono=False):
    """Load any WAV/FLAC/MP3 (headerless .pcm = EL 48 kHz s16le) as float64 [n, ch], resampled to sr."""
    path = str(path)
    raw = pathlib.Path(path).read_bytes()[:4]
    if path.endswith(".pcm") or raw not in (b"RIFF", b"fLaC", b"OggS") and not raw.startswith(b"ID3") and raw[:2] not in (b"\xff\xfb", b"\xff\xf3", b"\xff\xf2"):
        x = np.frombuffer(pathlib.Path(path).read_bytes(), dtype="<i2").astype(np.float64)[:, None] / 32768.0
        r = 48000
    else:
        x, r = sf.read(path, always_2d=True)
    if r != sr:
        g = math.gcd(int(r), int(sr))
        x = signal.resample_poly(x, sr // g, r // g, axis=0)
    if mono:
        x = x.mean(axis=1, keepdims=True)
    return x, sr


def save(path, x, sr=SR, bits=16):
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    x = np.atleast_2d(np.asarray(x, dtype=np.float64))
    if x.shape[0] < x.shape[1] and x.shape[0] <= 2:
        x = x.T
    if bits == 16:  # TPDF dither at 1 LSB keeps quiet fades and room tones free of truncation buzz
        rng = np.random.default_rng(len(x))
        x = x + (rng.random(x.shape) - rng.random(x.shape)) / 32768.0
    sf.write(str(path), np.clip(x, -1, 1 - 1 / 32768), sr, subtype="PCM_16" if bits == 16 else "PCM_24")
    return path


def mono(x):
    return x.mean(axis=1, keepdims=True) if x.shape[1] > 1 else x


def stereo(x):
    return np.repeat(x, 2, axis=1) if x.shape[1] == 1 else x


def db(v):
    return 20 * np.log10(np.maximum(v, 1e-12))


def undb(d):
    return 10 ** (d / 20)


def silence(sec, ch=1, sr=SR):
    return np.zeros((int(round(sec * sr)), ch))


def pad_to(x, n):
    if len(x) >= n:
        return x[:n]
    return np.vstack([x, np.zeros((n - len(x), x.shape[1]))])


def mix(*parts, n=None):
    """mix((x, gain_db, offset_s), ...) -> sum; channel count = max."""
    ch = max(p[0].shape[1] for p in parts)
    total = n or max(int(round(p[2] * SR)) + len(p[0]) for p in parts)
    out = np.zeros((total, ch))
    for x, g, off in parts:
        o = int(round(off * SR))
        if o >= total:
            continue
        seg = x[: total - o]
        if seg.shape[1] != ch:
            seg = np.repeat(seg, ch, axis=1)
        out[o:o + len(seg)] += seg * undb(g)
    return out


# ----------------------------------------------------------------------------- filters
def _sos(kind, f, sr=SR, order=4):
    f = np.atleast_1d(f) / (sr / 2)
    f = np.clip(f, 1e-5, 0.9999)
    return signal.butter(order, f if len(f) > 1 else f[0], kind, output="sos")


def hp(x, f, order=2, sr=SR):
    return signal.sosfiltfilt(_sos("highpass", f, sr, order), x, axis=0)


def lp(x, f, order=4, sr=SR):
    return signal.sosfiltfilt(_sos("lowpass", f, sr, order), x, axis=0)


def bp(x, lo, hi, order=4, sr=SR):
    return signal.sosfiltfilt(_sos("bandpass", [lo, hi], sr, order), x, axis=0)


def lp_causal(x, f, order=2, sr=SR):
    return signal.sosfilt(_sos("lowpass", f, sr, order), x, axis=0)


def biquad(x, kind, f0, gain_db=0.0, q=0.707, sr=SR):
    """RBJ cookbook peaking / lowshelf / highshelf."""
    A = 10 ** (gain_db / 40)
    w0 = 2 * np.pi * f0 / sr
    alpha = np.sin(w0) / (2 * q)
    c = np.cos(w0)
    if kind == "peak":
        b = [1 + alpha * A, -2 * c, 1 - alpha * A]
        a = [1 + alpha / A, -2 * c, 1 - alpha / A]
    elif kind == "lowshelf":
        sa = 2 * np.sqrt(A) * alpha
        b = [A * ((A + 1) - (A - 1) * c + sa), 2 * A * ((A - 1) - (A + 1) * c), A * ((A + 1) - (A - 1) * c - sa)]
        a = [(A + 1) + (A - 1) * c + sa, -2 * ((A - 1) + (A + 1) * c), (A + 1) + (A - 1) * c - sa]
    elif kind == "highshelf":
        sa = 2 * np.sqrt(A) * alpha
        b = [A * ((A + 1) + (A - 1) * c + sa), -2 * A * ((A - 1) + (A + 1) * c), A * ((A + 1) + (A - 1) * c - sa)]
        a = [(A + 1) - (A - 1) * c + sa, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - sa]
    else:
        raise ValueError(kind)
    b = np.array(b) / a[0]
    a = np.array(a) / a[0]
    return signal.lfilter(b, a, x, axis=0)


def remove_dc(x, f=20):
    return hp(x, f, order=2)


# ----------------------------------------------------------------------------- dynamics
def envelope(x, ms=5, sr=SR):
    m = np.abs(x).max(axis=1)
    w = max(1, int(sr * ms / 1000))
    return np.sqrt(np.convolve(m ** 2, np.ones(w) / w, mode="same"))


def peak_norm(x, d=-1.0):
    return x * (undb(d) / max(np.abs(x).max(), 1e-12))


def soft_clip(x, drive=1.0):
    return np.tanh(x * drive) / np.tanh(drive)


def limit(x, drive_db=0.0, ceiling_db=-1.0, lookahead_ms=1.5, release_ms=40, sr=SR):
    """Look-ahead brickwall limiter (instant attack inside the look-ahead window, exponential release)."""
    y = x * undb(drive_db)
    ceil = undb(ceiling_db)
    la = max(1, int(sr * lookahead_ms / 1000))
    pk = maximum_filter1d(np.abs(y).max(axis=1), size=2 * la + 1)
    target = np.minimum(1.0, ceil / np.maximum(pk, 1e-12))
    r = math.exp(-1 / (sr * release_ms / 1000))
    # vectorised-ish release: process with lfilter on the "release" side, then take min with target
    g = np.empty_like(target)
    cur = 1.0
    for i in range(len(target)):
        t = target[i]
        cur = t if t < cur else r * cur + (1 - r) * t
        g[i] = cur
    g = np.convolve(g, np.ones(la) / la, mode="same")  # smooth the gain to avoid AM grit
    g = np.minimum(g, target)
    return np.clip(y * g[:, None], -ceil, ceil)


def compress(x, thresh_db=-20, ratio=4, attack_ms=2, release_ms=120, knee_db=6, makeup_db=0.0, sr=SR):
    lvl = np.abs(x).max(axis=1)
    a = math.exp(-1 / (sr * attack_ms / 1000))
    r = math.exp(-1 / (sr * release_ms / 1000))
    env = np.empty_like(lvl)
    e = 0.0
    for i in range(len(lvl)):
        v = lvl[i]
        e = a * e + (1 - a) * v if v > e else r * e + (1 - r) * v
        env[i] = e
    edb = db(env)
    over = edb - thresh_db
    gr = np.where(over <= -knee_db / 2, 0,
                  np.where(over >= knee_db / 2, over * (1 - 1 / ratio),
                           (1 - 1 / ratio) * (over + knee_db / 2) ** 2 / (2 * knee_db)))
    return x * undb(makeup_db - gr)[:, None]


def true_peak_db(x, sr=SR):
    up = signal.resample_poly(x, 4, 1, axis=0)
    return float(db(np.abs(up).max()))


def finalize(x, tp=-1.0, peak=None, sr=SR):
    """Normalise so the 4x-oversampled true peak sits at `tp` dBTP (or lower when `peak` asks for less)."""
    target = tp if peak is None else min(tp, peak)
    x = peak_norm(x, target - 0.3)
    for _ in range(6):
        t = true_peak_db(x, sr)
        if t <= tp + 1e-3:
            break
        x = x * undb(tp - t - 0.05)
    return x


def lufs(x, sr=SR):
    try:
        return float(pyln.Meter(sr).integrated_loudness(x if x.shape[1] > 1 else x[:, 0]))
    except Exception:
        return float("nan")


def momentary_max(x, win=0.4, hop=0.05, sr=SR):
    meter = pyln.Meter(sr, block_size=win)
    n = int(win * sr)
    if len(x) < n:
        x = pad_to(x, n)
    best = -120.0
    for i in range(0, len(x) - n + 1, int(hop * sr)):
        seg = x[i:i + n]
        try:
            v = meter.integrated_loudness(seg if seg.shape[1] > 1 else seg[:, 0])
        except Exception:
            continue
        if np.isfinite(v):
            best = max(best, v)
    return float(best)


def loudness_to(x, target_lufs, tp=-1.0, sr=SR, limit_ok=True):
    """Integrated loudness to target; if that would push true peak over tp, limit (if allowed) or settle lower."""
    l = lufs(x, sr)
    if not np.isfinite(l):
        return finalize(x, tp)
    y = x * undb(target_lufs - l)
    if true_peak_db(y, sr) > tp:
        if limit_ok:
            y = limit(y, 0.0, tp - 0.6, lookahead_ms=3, release_ms=80)
            l2 = lufs(y, sr)
            y = y * undb(target_lufs - l2)
            y = limit(y, 0.0, tp - 0.6, lookahead_ms=3, release_ms=80)
        y = finalize(y, tp) if true_peak_db(y, sr) > tp else y
    return y


def mmax_to(x, target, tp=-1.0, sr=SR):
    """Momentary-max loudness (400 ms) to target, limited so true peak <= tp."""
    m = momentary_max(x, sr=sr)
    y = x * undb(target - m)
    if true_peak_db(y, sr) > tp:
        y = limit(y, 0.0, tp - 0.5, lookahead_ms=1.5, release_ms=40)
        y = finalize(y, tp) if true_peak_db(y, sr) > tp else y
    return y


# ----------------------------------------------------------------------------- trimming
def onset_index(x, on_db=-30, back_ms=40, sr=SR):
    env = envelope(x, 2, sr)
    pk = env.max()
    on = np.where(env > pk * undb(on_db))[0]
    start = on[0] if len(on) else 0
    foot = pk * undb(on_db - 20)
    lim = max(0, start - int(sr * back_ms / 1000))
    while start > lim and env[start - 1] > foot and env[start - 1] <= env[start] * 1.05:
        start -= 1
    return int(start)


def trim(x, on_db=-30, floor_db=-60, pre_ms=3, fade_ms=60, back_ms=40, max_len=None, sr=SR):
    """Cut the pre-roll (Mirelo pads 0.03-2.8 s) to pre_ms before the onset; cut the tail where it sinks under
    floor_db (re. peak) and fade; optional max_len (s) with the fade ending at max_len."""
    start = max(0, onset_index(x, on_db, back_ms, sr) - int(sr * pre_ms / 1000))
    env = envelope(x, 10, sr)
    pk = env.max()
    alive = np.where(env > pk * undb(floor_db))[0]
    end = min(len(x), alive[-1] + int(sr * fade_ms / 1000)) if len(alive) else len(x)
    if max_len:
        end = min(end, start + int(max_len * sr))
    y = x[start:end].copy()
    y = fade(y, 0.0005, fade_ms / 1000)
    return y


def fade(x, fin=0.0005, fout=0.06, curve=2.0, sr=SR):
    y = x.copy()
    a = min(len(y), int(fin * sr))
    b = min(len(y), int(fout * sr))
    if a > 0:
        y[:a] *= np.linspace(0, 1, a)[:, None]
    if b > 0:
        y[-b:] *= (np.linspace(1, 0, b) ** curve)[:, None]
    return y


def decay_fade(x, start_s, end_s, sr=SR):
    """Exponential-ish fade from start_s to end_s (cuts the embedded echo of a transient for zone layering)."""
    y = x[: int(end_s * sr)].copy()
    s = int(start_s * sr)
    n = len(y) - s
    if n > 0:
        t = np.linspace(0, 1, n)
        y[s:] *= (np.cos(t * np.pi / 2) ** 3)[:, None]
    return y


# ----------------------------------------------------------------------------- events / slicing
def detect_events(x, min_gap=0.25, rise_db=12, floor_db=-45, min_len=0.06, max_len=1.2, sr=SR, ms=4):
    """Onsets where the short envelope rises rise_db above the running floor and is within |floor_db| of the
    file peak; each event ends at the next onset or where it decays to floor. Returns [(start, end)] in samples."""
    env = envelope(x, ms, sr)
    edb = db(env)
    pk = edb.max()
    # slow floor = running minimum-ish via a long median of the dB envelope
    hop = max(1, int(sr * 0.005))
    e_s = edb[::hop]
    floor = signal.medfilt(e_s, kernel_size=int(0.3 / 0.005) | 1)
    floor = np.minimum.accumulate(np.r_[floor[::-1]])[::-1] * 0 + floor  # keep shape
    cands = []
    last = -1e9
    for i in range(1, len(e_s)):
        if e_s[i] > pk + floor_db and e_s[i] - min(e_s[max(0, i - 8):i].min(), floor[i]) > rise_db:
            t = i * hop
            if t - last > min_gap * sr:
                cands.append(t)
                last = t
    events = []
    for k, t in enumerate(cands):
        # refine back to the foot of the rise
        s = t
        lim = max(0, t - int(0.03 * sr))
        while s > lim and env[s - 1] <= env[s] * 1.02 and edb[s - 1] > edb[t] - 30:
            s -= 1
        nxt = cands[k + 1] if k + 1 < len(cands) else len(x)
        seg_env = edb[s:nxt]
        local_pk = seg_env.max() if len(seg_env) else pk
        dead = np.where(seg_env < local_pk - 40)[0]
        dead = dead[dead > int(min_len * sr)]
        e = s + (dead[0] if len(dead) else len(seg_env))
        e = min(e, s + int(max_len * sr), nxt - int(0.003 * sr))
        if e - s >= int(min_len * sr):
            events.append((int(s), int(e)))
    return events


def slice_events(x, events, pre_ms=2, fade_ms=30, sr=SR):
    out = []
    for s, e in events:
        s2 = max(0, s - int(pre_ms * sr / 1000))
        y = fade(x[s2:e].copy(), 0.0005, fade_ms / 1000)
        out.append(y)
    return out


def count_transients(x, window_s=0.5, rel_db=-14, gap_ms=35, sr=SR):
    """Count separate sharp peaks within window_s of the onset that come within rel_db of the max (double-shot test)."""
    env = envelope(x, 1, sr)
    on = onset_index(x, -30, 40, sr)
    seg = env[on:on + int(window_s * sr)]
    if not len(seg):
        return 0
    pk = seg.max()
    thr = pk * undb(rel_db)
    peaks, _ = signal.find_peaks(seg, height=thr, distance=int(gap_ms * sr / 1000), prominence=pk * undb(rel_db - 4))
    return int(len(peaks))


# ----------------------------------------------------------------------------- reverb
def synth_ir(rt60=1.2, predelay=0.01, length=None, er=(), lp_hz=6000, lp_end_hz=None, density=1.0, stereo_ir=False,
             hp_hz=80, seed=0, sr=SR):
    """Synthetic room IR: discrete early reflections [(t_s, gain_db, lp_hz)], then exponentially decaying
    filtered noise whose high band decays faster (lp sweeps from lp_hz to lp_end_hz)."""
    rng = np.random.default_rng(seed)
    length = length or (predelay + rt60 * 1.1)
    n = int(length * sr)
    ch = 2 if stereo_ir else 1
    ir = np.zeros((n, ch))
    t = np.arange(n) / sr
    p = int(predelay * sr)
    noise = rng.standard_normal((n, ch))
    if density < 1:
        mask = rng.random((n, ch)) < density
        noise = noise * mask / np.sqrt(density)
    dec = np.exp(-6.91 * np.maximum(t - predelay, 0) / rt60)[:, None]
    tail = noise * dec
    tail[:p] = 0
    # frequency-dependent decay: split into 2 bands, high band decays twice as fast
    lo_band = lp(tail, lp_end_hz or max(800, lp_hz / 4), order=2)
    hi_band = tail - lo_band
    hi_band *= np.exp(-6.91 * np.maximum(t - predelay, 0) / (rt60 * 0.45))[:, None]
    tail = lo_band + hi_band
    tail = lp(tail, lp_hz, order=2)
    # soft onset of the diffuse field
    ramp = np.clip((t - predelay) / 0.02, 0, 1)[:, None]
    tail *= ramp
    ir += tail * 0.5
    for ts, g, lph in er:
        i = int(ts * sr)
        if i >= n:
            continue
        imp = np.zeros((min(n - i, 2048), ch))
        imp[0] = 1.0
        if lph:
            imp = lp_causal(imp, lph, order=2)
        if stereo_ir:
            pan = rng.uniform(-0.6, 0.6)
            imp[:, 0] *= np.sqrt(0.5 * (1 - pan))
            imp[:, 1] *= np.sqrt(0.5 * (1 + pan))
        ir[i:i + len(imp)] += imp * undb(g)
    ir = hp(ir, hp_hz, order=2)
    return ir / (np.sqrt((ir ** 2).sum(axis=0)).max() + 1e-12)


def convolve(x, ir):
    ch = max(x.shape[1], ir.shape[1])
    xs = np.repeat(x, ch, axis=1) if x.shape[1] < ch else x
    irs = np.repeat(ir, ch, axis=1) if ir.shape[1] < ch else ir
    return np.stack([signal.fftconvolve(xs[:, c], irs[:, c]) for c in range(ch)], axis=1)


# ----------------------------------------------------------------------------- loops
LEAD = 2048  # samples of lead-in (>= 1024, AAC priming, TECH_PLAN §3.12) and of tail-out


def best_loop_cut(x, approx_end, search_s=0.5, win_s=0.05, sr=SR):
    """Find an end point near approx_end whose following window best matches the file start (max correlation)."""
    m = mono(x)[:, 0]
    w = int(win_s * sr)
    ref = m[:w]
    lo = max(w, int((approx_end - search_s) * sr))
    hi = min(len(m) - w, int((approx_end + search_s) * sr))
    best, bi = -2, int(approx_end * sr)
    for i in range(lo, hi, 16):
        seg = m[i:i + w]
        c = np.dot(seg, ref) / (np.linalg.norm(seg) * np.linalg.norm(ref) + 1e-12)
        if c > best:
            best, bi = c, i
    return bi, best


def make_loop_body(x, start, end, xfade, sr=SR):
    """Seamless loop body for [start, end) samples: the last `xfade` samples are an equal-power blend of the
    audio before `end` and the audio before `start` (needs start >= xfade)."""
    assert start >= xfade, "need xfade samples of audio before start"
    y = x[start:end].copy()
    t = np.linspace(0, np.pi / 2, xfade)[:, None]
    y[-xfade:] = x[end - xfade:end] * np.cos(t) + x[start - xfade:start] * np.sin(t)
    return y


def rotate_seamless(x, xfade_s=1.0, sr=SR):
    """Make a whole generated bed loop: crossfade its tail into its head (equal power, decorrelated content)."""
    f = int(xfade_s * sr)
    head = x[:f]
    body = x[f:].copy()
    t = np.linspace(0, np.pi / 2, f)[:, None]
    body[-f:] = body[-f:] * np.cos(t) + head * np.sin(t)
    return body


def package_loop(body, lead=LEAD):
    """File layout for Web loops: [lead-in = last `lead` samples of body] + body + [tail-out = first `lead` samples].
    Loop region = [lead, lead + len(body)); see import_notes."""
    return np.vstack([body[-lead:], body, body[:lead]]), lead, len(body)


def seam_metrics(body, sr=SR):
    """Wrap discontinuity of a loop body, judged against the body's own interior: the sample jump vs the 99.9th
    percentile sample step (click), and the 50 ms RMS step and spectral distance across the wrap vs the same
    measures at 24 random interior cut points (p90). seam_ok = no click and both within the interior p90 (+1 dB)."""
    m = mono(body)[:, 0]
    w = int(0.05 * sr)
    jump = float(abs(m[0] - m[-1]))
    local = float(np.sqrt(np.mean(np.r_[m[:w], m[-w:]] ** 2)) + 1e-12)
    d = np.abs(np.diff(m))
    click_ratio = float(jump / (np.percentile(d, 99.9) + 1e-12))

    def pair(a, b):
        ra = db(np.sqrt(np.mean(a ** 2)) + 1e-12)
        rb = db(np.sqrt(np.mean(b ** 2)) + 1e-12)
        fa = np.abs(np.fft.rfft(a * np.hanning(len(a))))
        fb = np.abs(np.fft.rfft(b * np.hanning(len(b))))
        return float(abs(ra - rb)), float(np.mean(np.abs(db(fa + 1e-9) - db(fb + 1e-9))))

    step, spec = pair(m[-w:], m[:w])
    rng = np.random.default_rng(1)
    bs, bf = [], []
    for _ in range(24):
        i = int(rng.integers(w, len(m) - w))
        a, b = pair(m[i - w:i], m[i:i + w])
        bs.append(a)
        bf.append(b)
    p90s, p90f = float(np.percentile(bs, 90)), float(np.percentile(bf, 90))
    ok = click_ratio < 2.5 and step <= p90s + 1.0 and spec <= p90f + 1.0
    return dict(seam_jump=round(jump, 5), seam_jump_rel_rms=round(jump / local, 3), click_ratio=round(click_ratio, 2),
                seam_rms_step_db=round(step, 2), interior_rms_step_p90_db=round(p90s, 2),
                seam_spec_dist_db=round(spec, 2), interior_spec_dist_db=round(float(np.median(bf)), 2),
                interior_spec_p90_db=round(p90f, 2), seam_ok=bool(ok))


# ----------------------------------------------------------------------------- measurement
def measure(x, sr=SR):
    m = mono(x)[:, 0]
    n = len(m)
    peak = float(np.abs(x).max())
    rms = float(np.sqrt(np.mean(m ** 2)) + 1e-12)
    env = envelope(x, 5, sr)
    above = np.where(env > peak * undb(-40))[0]
    lead = above[0] / sr if len(above) else n / sr
    above2 = np.where(env > peak * undb(-60))[0]
    tail = (n - above2[-1]) / sr if len(above2) else n / sr
    f, P = signal.welch(m, sr, nperseg=min(8192, max(256, n)))
    P = P + 1e-20
    centroid = float((f * P).sum() / P.sum())
    low = float(P[f < 150].sum() / P.sum())
    hi_ = float(P[f > 8000].sum() / P.sum())
    Pdb = 10 * np.log10(P)
    bw = float(f[np.where(Pdb > Pdb.max() - 60)[0][-1]])
    corr = float(np.corrcoef(x[:, 0], x[:, 1])[0, 1]) if x.shape[1] > 1 else 1.0
    r = dict(sr=sr, ch=int(x.shape[1]), dur_s=round(n / sr, 3), peak_dbfs=round(float(db(peak)), 2),
             true_peak_dbtp=round(true_peak_db(x, sr), 2), rms_dbfs=round(float(db(rms)), 2),
             lufs_i=round(lufs(x, sr), 1), lufs_m_max=round(momentary_max(x, sr=sr), 1),
             crest_db=round(float(db(peak) - db(rms)), 1), clipped=int((np.abs(x) >= 0.9999).sum()),
             lead_ms=round(lead * 1000, 1), tail_ms=round(tail * 1000, 1), centroid_hz=round(centroid),
             low_lt150=round(low, 3), hi_gt8k=round(hi_, 4), bandwidth_hz=round(bw), dc=round(float(m.mean()), 5),
             stereo_corr=round(corr, 3))
    return r


def spectrum_bands(x, sr=SR):
    """Energy share per octave-ish band (for quick spectral sanity)."""
    m = mono(x)[:, 0]
    f, P = signal.welch(m, sr, nperseg=min(8192, max(256, len(m))))
    edges = [20, 60, 150, 400, 1000, 2500, 6000, 12000, 24000]
    tot = P.sum() + 1e-20
    return {f"{a}-{b}": round(float(P[(f >= a) & (f < b)].sum() / tot), 3) for a, b in zip(edges[:-1], edges[1:])}


def write_json(path, obj):
    pathlib.Path(path).parent.mkdir(parents=True, exist_ok=True)
    pathlib.Path(path).write_text(json.dumps(obj, indent=1))


# ----------------------------------------------------------------------------- WP06_SFX resume additions (2026-10-16)
def save_loop(path, x, loop_start, loop_end, sr=SR, bits=16):
    """Write a WAV and append a RIFF 'smpl' chunk with one forward loop [loop_start, loop_end) (end exclusive in our
    metadata; the chunk stores the inclusive last sample). Unity may or may not honour it; the loop region is also
    published in LOOP_POINTS.json and the import notes for the PlayScheduled two-source loop (TECH_PLAN §3.12)."""
    import struct
    p = save(path, x, sr, bits)
    data = bytearray(pathlib.Path(p).read_bytes())
    period_ns = int(round(1e9 / sr))
    smpl = struct.pack("<9I", 0, 0, period_ns, 60, 0, 0, 0, 1, 0)
    smpl += struct.pack("<6I", 0, 0, int(loop_start), int(loop_end) - 1, 0, 0)
    chunk = b"smpl" + struct.pack("<I", len(smpl)) + smpl
    data += chunk
    data[4:8] = struct.pack("<I", len(data) - 8)
    pathlib.Path(p).write_bytes(bytes(data))
    return p


def peaks_after_main(x, window_s=0.6, sr=SR):
    """Secondary transients after the main peak: [(t_s, rel_db, rise_ms, hf_rel_db)]. A second gunshot shows up as a
    peak within ~8 dB of the main one with a sharp (< 3 ms) rise and similar 2-8 kHz content; echoes are lower,
    slower and darker."""
    m = mono(x)[:, 0]
    env = np.sqrt(np.convolve(m ** 2, np.ones(48) / 48, mode="same"))  # 1 ms RMS
    on = onset_index(x, -30, 40, sr)
    seg = env[on:on + int(window_s * sr)]
    if len(seg) < 100:
        return []
    i0 = int(np.argmax(seg[: int(0.05 * sr)]))
    pk = seg[i0]
    hfb = bp(m[:, None], 2000, 8000, order=2)[:, 0]
    hfe = np.sqrt(np.convolve(hfb ** 2, np.ones(240) / 240, mode="same"))
    hf0 = hfe[on + i0]
    peaks, _ = signal.find_peaks(seg, distance=int(0.02 * sr), prominence=pk * 0.12)
    out = []
    for p in peaks:
        if p <= i0 + int(0.012 * sr):
            continue
        rel = float(db(seg[p] / pk))
        if rel < -18:
            continue
        lo = max(0, p - int(0.01 * sr))
        foot = np.where(seg[lo:p] < seg[p] * undb(-12))[0]
        rise = (p - (lo + foot[-1])) / sr * 1000 if len(foot) else 10.0
        hfr = float(db(hfe[on + p] / (hf0 + 1e-12)))
        out.append((round(p / sr, 3), round(rel, 1), round(rise, 2), round(hfr, 1)))
    return out


def double_shot(x, sr=SR):
    """True when a second gunshot-like transient follows the main one (see peaks_after_main)."""
    for t, rel, rise, hfr in peaks_after_main(x, sr=sr):
        if rel > -9 and rise < 3.0 and hfr > -9:
            return True
    return False


def decay_rt(x, sr=SR, lo_db=-5, hi_db=-30):
    """Rough RT60 (s) from the slope of the 20 ms dB envelope between lo_db and hi_db below its peak."""
    env = db(envelope(x, 20, sr))
    i = int(np.argmax(env))
    pk = env[i]
    tail = env[i:]
    a = np.where(tail < pk + lo_db)[0]
    b = np.where(tail < pk + hi_db)[0]
    if not len(a) or not len(b) or b[0] <= a[0]:
        return float("nan")
    return float(60.0 * (b[0] - a[0]) / sr / (lo_db - hi_db))


def gain_env(n, points, sr=SR):
    """Piecewise-linear gain in dB over time: points [(t_s, db), ...] -> [n, 1] linear gain."""
    t = np.arange(n) / sr
    ts = [p[0] for p in points]
    ds = [p[1] for p in points]
    return undb(np.interp(t, ts, ds))[:, None]


def exp_shorten(x, rt_now, rt_target, start_s=0.0, sr=SR):
    """Shorten a decay from rt_now to rt_target by multiplying with exp(-6.91 t (1/rt_target - 1/rt_now))."""
    if not np.isfinite(rt_now) or rt_now <= rt_target:
        return x
    t = np.maximum(np.arange(len(x)) / sr - start_s, 0)
    k = 6.91 * (1 / rt_target - 1 / rt_now)
    return x * np.exp(-k * t)[:, None]


def to_mono_safe(x):
    """Stereo -> mono. If the channels are strongly anti-correlated, take the louder channel instead of the sum."""
    if x.shape[1] == 1:
        return x
    c = np.corrcoef(x[:, 0], x[:, 1])[0, 1] if np.std(x[:, 0]) > 0 and np.std(x[:, 1]) > 0 else 1.0
    if c < -0.3:
        k = int(np.argmax(np.sqrt((x ** 2).mean(axis=0))))
        return x[:, k:k + 1]
    return x.mean(axis=1, keepdims=True)


def clip_os(x, gain_db=0.0, ceil_db=-1.0, os_factor=4):
    """4x-oversampled tanh soft clipper: the classic way to make a gunshot dense without the gain pumping a
    slow-release limiter causes on a 30 ms burst. Output true peak ~ ceil_db."""
    up = signal.resample_poly(x * undb(gain_db), os_factor, 1, axis=0)
    c = undb(ceil_db)
    y = c * np.tanh(up / c)
    return signal.resample_poly(y, 1, os_factor, axis=0)


# ----------------------------------------------------------------------------- WP06_SFX resume pass 2 (2026-10-16)
def focus_main(x, gap_s=0.12, on_db=-30, tail_db=-45, pre_ms=3, max_len=None, fade_ms=40, sr=SR):
    """Keep only the main event cluster of a take: the loudest moment plus everything above on_db (re. peak) that is
    joined to it by gaps shorter than gap_s, then its decay down to tail_db, then a fade. Earlier or later separate
    events (a second footstep, a second swing, pre-roll noise, a second reload click 0.5 s later) are cut away.
    Returns (y, info) with info = dict(start_s, end_s, cut_before, cut_after)."""
    env = envelope(x, 5, sr)
    pk = env.max() + 1e-12
    ipk = int(np.argmax(env))
    above = env > pk * undb(on_db)
    gap = int(gap_s * sr)
    # walk left from the peak across short gaps
    s = ipk
    run = 0
    i = ipk
    while i > 0:
        i -= 1
        if above[i]:
            s = i
            run = 0
        else:
            run += 1
            if run > gap:
                break
    # walk right
    e = ipk
    run = 0
    i = ipk
    while i < len(x) - 1:
        i += 1
        if above[i]:
            e = i
            run = 0
        else:
            run += 1
            if run > gap:
                break
    # decay after the cluster down to tail_db, but never into the next separate event
    nxt = np.where(above[e + gap:])[0]
    hard_end = e + gap + int(nxt[0]) - int(0.004 * sr) if len(nxt) else len(x)
    dec = np.where(env[e:hard_end] < pk * undb(tail_db))[0]
    end = e + int(dec[0]) if len(dec) else hard_end
    end = min(end + int(fade_ms / 1000 * sr), hard_end, len(x))
    start = max(0, onset_index(x[s:end] if end > s else x, on_db, 30, sr) + s - int(pre_ms / 1000 * sr))
    if max_len:
        end = min(end, start + int(max_len * sr))
    y = fade(x[start:end].copy(), 0.0005, min(fade_ms / 1000, (end - start) / sr * 0.3))
    has_before = bool(above[: max(0, s - gap)].any())
    has_after = bool(len(nxt))
    return y, dict(start_s=round(start / sr, 3), end_s=round(end / sr, 3), cut_before=has_before, cut_after=has_after)


def decays(x, sr=SR, frac=0.33, min_drop_db=12):
    """True when the last third of x is at least min_drop_db below the first third (energy)."""
    m = mono(x)[:, 0]
    n = len(m)
    a = np.mean(m[: int(n * frac)] ** 2) + 1e-20
    b = np.mean(m[int(n * (1 - frac)):] ** 2) + 1e-20
    return bool(10 * np.log10(a / b) >= min_drop_db)
