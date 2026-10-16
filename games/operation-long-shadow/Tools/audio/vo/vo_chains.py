#!/usr/bin/env python3
"""Offline VO processing chains (GDD 10.1, TECH_PLAN D9): Unity Web has no audio filters, so every chain is baked.

Library use (also for WP06_SFX, e.g. INTERVAL_SIGNAL variants):
    from vo_chains import CHAINS, finalize
    y, info = CHAINS["HF_AM"](x, sr=48000, seed=123)      # x: mono float, 48 kHz
    y, meters = finalize(y)                                  # -18 LUFS, <= -1 dBTP, fades
CLI (one file):
    python vo_chains.py in.wav out.wav --chain RADIO [--seed 1] [--rotor]

Every chain returns (y, info) where info["offset_s"] is how far the speech moved later in the file relative to the
input (TAPE adds a motor start before the words, the rest keep the speech onset where it was, so DRY and RADIO of the
same line stay sample-synchronous for the VOManager's distance crossfade).
Chains: DRY, BOOTH, CLEAN, RADIO, INTERCOM, SCRAMBLER, TAPE, PA_HORN, HF_AM, ROOM_SPEAKER, GREYCOAT_SQUELCH, FAR.
"""
import argparse, math
import numpy as np
from scipy import signal
from common import SR, lufs, true_peak_db, read_audio, to_sr, write_wav16

TARGET_LUFS = -18.0
TP_CEIL = -1.0


# ------------------------------------------------------------------ building blocks
def bp(x, lo, hi, sr=SR, order=4):
    sos = signal.butter(order, [lo / (sr / 2), hi / (sr / 2)], "bandpass", output="sos")
    return signal.sosfilt(sos, x)


def hp(x, f, sr=SR, order=2):
    return signal.sosfilt(signal.butter(order, f / (sr / 2), "highpass", output="sos"), x)


def lp(x, f, sr=SR, order=2):
    return signal.sosfilt(signal.butter(order, f / (sr / 2), "lowpass", output="sos"), x)


def peq(x, f0, gain_db, q, sr=SR):
    """RBJ peaking EQ."""
    A = 10 ** (gain_db / 40)
    w0 = 2 * math.pi * f0 / sr
    al = math.sin(w0) / (2 * q)
    b = np.array([1 + al * A, -2 * math.cos(w0), 1 - al * A])
    a = np.array([1 + al / A, -2 * math.cos(w0), 1 - al / A])
    return signal.lfilter(b / a[0], a / a[0], x)


def shelf(x, f0, gain_db, sr=SR, high=True):
    A = 10 ** (gain_db / 40)
    w0 = 2 * math.pi * f0 / sr
    al = math.sin(w0) / 2 * math.sqrt(2)
    c = math.cos(w0)
    if high:
        b = [A * ((A + 1) + (A - 1) * c + 2 * math.sqrt(A) * al), -2 * A * ((A - 1) + (A + 1) * c),
             A * ((A + 1) + (A - 1) * c - 2 * math.sqrt(A) * al)]
        a = [(A + 1) - (A - 1) * c + 2 * math.sqrt(A) * al, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - 2 * math.sqrt(A) * al]
    else:
        b = [A * ((A + 1) - (A - 1) * c + 2 * math.sqrt(A) * al), 2 * A * ((A - 1) - (A + 1) * c),
             A * ((A + 1) - (A - 1) * c - 2 * math.sqrt(A) * al)]
        a = [(A + 1) + (A - 1) * c + 2 * math.sqrt(A) * al, -2 * ((A - 1) + (A + 1) * c), (A + 1) + (A - 1) * c - 2 * math.sqrt(A) * al]
    b, a = np.array(b), np.array(a)
    return signal.lfilter(b / a[0], a / a[0], x)


def rms(x):
    return float(np.sqrt(np.mean(x ** 2)) + 1e-12)


def speech_rms(x, sr=SR):
    """RMS over the active (speech) frames only: frames within 30 dB of the loudest 20 ms frame."""
    w = int(sr * 0.02)
    n = len(x) // w
    if n < 2:
        return rms(x)
    f = np.sqrt((x[: n * w].reshape(n, w) ** 2).mean(axis=1)) + 1e-12
    act = f[f > f.max() * 10 ** (-30 / 20)]
    return float(np.sqrt(np.mean(act ** 2)))


def soft_clip(x, drive):
    return np.tanh(x * drive) / np.tanh(drive)


def compress(x, sr=SR, thresh_db=-20, ratio=3.0, attack_ms=3, release_ms=90, makeup=True):
    """Feed-forward RMS-ish compressor (vectorised one-pole envelope via lfilter on both branches)."""
    lvl = np.abs(x)
    a = math.exp(-1 / (sr * attack_ms / 1000)); r = math.exp(-1 / (sr * release_ms / 1000))
    # peak follower: fast attack / slow release, approximated with a max of two smoothers
    env_r = signal.lfilter([1 - r], [1, -r], lvl)
    env_a = signal.lfilter([1 - a], [1, -a], lvl)
    env = np.maximum(env_r, env_a)
    edb = 20 * np.log10(env + 1e-9)
    over = np.maximum(edb - thresh_db, 0)
    g = 10 ** (-over * (1 - 1 / ratio) / 20)
    y = x * g
    if makeup:
        y *= rms(x) / max(rms(y), 1e-9)
    return y


def noise(n, seed, color="white"):
    rng = np.random.default_rng(seed)
    w = rng.standard_normal(n)
    if color == "pink":
        b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
        a = [1, -2.494956002, 2.017265875, -0.522189400]
        w = signal.lfilter(b, a, w)
    return w / (rms(w) + 1e-12)


def env_burst(n, attack, decay_pow=1.5):
    t = np.linspace(0, 1, n)
    e = np.minimum(1, t / max(attack, 1e-4)) * (1 - t) ** decay_pow
    return e


def synth_ir(sr, rt60, early, wet_tail_db, lp_hz, seed, length=None, predelay_ms=0):
    """Synthetic room IR: unit direct + early taps [(ms, dB)] + exponentially decaying filtered noise tail."""
    L = int(sr * (length or rt60 * 1.2))
    ir = np.zeros(L)
    ir[0] = 1.0
    rng = np.random.default_rng(seed)
    for ms, db in early:
        i = int(sr * ms / 1000)
        if i < L:
            ir[i] += 10 ** (db / 20) * (1 if rng.random() > 0.3 else -1)
    t = np.arange(L) / sr
    tail = rng.standard_normal(L) * np.exp(-6.91 * t / rt60)
    pd = int(sr * (predelay_ms or (early[0][0] if early else 5)) / 1000)
    tail[:pd] = 0
    fade = np.minimum(1, (np.arange(L) - pd).clip(0) / (sr * 0.004))
    tail *= fade
    tail = lp(tail, lp_hz, sr, 2)
    tail *= 10 ** (wet_tail_db / 20) / (np.sqrt(np.sum(tail ** 2)) + 1e-12)
    return ir + tail


def convolve(x, ir, keep_len_extra_s=None, sr=SR):
    y = signal.fftconvolve(x, ir)
    if keep_len_extra_s is not None:
        y = y[: len(x) + int(sr * keep_len_extra_s)]
    return y


def trim_tail(y, sr=SR, floor_db=-62, min_extra_s=0.0, keep_s=0.04):
    """Cut trailing near-silence (reverb tails below floor), keep a short fade."""
    w = int(sr * 0.005)
    n = len(y) // w
    f = 20 * np.log10(np.sqrt((y[: n * w].reshape(n, w) ** 2).mean(axis=1)) + 1e-12)
    pk = f.max()
    alive = np.where(f > max(pk - 60, floor_db))[0]
    end = (alive[-1] + 1) * w if len(alive) else len(y)
    end = min(len(y), max(end + int(sr * keep_s), int(min_extra_s * sr)))
    y = y[:end].copy()
    fo = min(len(y), int(sr * 0.03))
    y[-fo:] *= np.linspace(1, 0, fo) ** 2
    return y


def varispeed(x, sr, wow_hz, wow_pct, flut_hz, flut_pct, seed):
    """Wow/flutter as a time-varying read position (pitch deviation = derivative of the delay)."""
    rng = np.random.default_rng(seed)
    n = len(x)
    t = np.arange(n) / sr
    ph1, ph2 = rng.uniform(0, 2 * np.pi, 2)
    aw = wow_pct / 100 / (2 * np.pi * wow_hz)
    af = flut_pct / 100 / (2 * np.pi * flut_hz)
    # slightly irregular wow: add a slower drift
    d = aw * np.sin(2 * np.pi * wow_hz * t + ph1) + af * np.sin(2 * np.pi * flut_hz * t + ph2) \
        + 0.4 * aw * np.sin(2 * np.pi * wow_hz * 0.37 * t + ph2)
    pos = (t + d - d[0]) * sr
    pos = np.clip(pos, 0, n - 1)
    return np.interp(pos, np.arange(n), x)


def rotor_bed(n, sr, seed, rpm=324, blades=2):
    """Procedural twin-blade utility helicopter cabin noise: blade slap pulses, gearbox/turbine tones, cabin rumble."""
    rng = np.random.default_rng(seed)
    t = np.arange(n) / sr
    rate = rpm / 60 * blades  # 10.8 Hz blade-pass
    y = np.zeros(n)
    pos = rng.uniform(0, 1 / rate)
    pulse_len = int(sr * 0.06)
    slap_src = bp(noise(pulse_len * 4, seed + 1), 120, 1800, sr, 2)
    while pos < n / sr:
        i = int(pos * sr)
        amp = 1.0 + 0.15 * rng.standard_normal()
        seg = slap_src[rng.integers(0, len(slap_src) - pulse_len):][:pulse_len] * env_burst(pulse_len, 0.05, 3)
        thump = np.sin(2 * np.pi * rng.uniform(38, 46) * np.arange(pulse_len) / sr) * env_burst(pulse_len, 0.08, 2) * 1.6
        e = min(n, i + pulse_len)
        y[i:e] += amp * (seg + thump)[: e - i]
        pos += (1 / rate) * (1 + 0.01 * rng.standard_normal())
    y /= rms(y) + 1e-12
    rumble = lp(noise(n, seed + 2, "pink"), 700, sr, 2)
    rumble /= rms(rumble) + 1e-12
    gear = sum(np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * g for f, g in ((97, 0.25), (194, 0.12), (1180, 0.05), (2360, 0.03)))
    bed = 0.75 * y + 0.55 * rumble + gear
    return bed / (rms(bed) + 1e-12)


def fades(y, sr=SR, fin_ms=2, fout_ms=12):
    y = y.copy()
    fi = min(len(y), int(sr * fin_ms / 1000))
    if fi:
        y[:fi] *= np.linspace(0, 1, fi)
    fo = min(len(y), int(sr * fout_ms / 1000))
    if fo:
        y[-fo:] *= np.linspace(1, 0, fo)
    return y


# ------------------------------------------------------------------ loudness / true peak
def tp_limit(x, sr=SR, ceil_db=-1.3, lookahead_ms=1.5, release_ms=60, os=4):
    ceil = 10 ** (ceil_db / 20)
    up = signal.resample_poly(x, os, 1)
    pk = np.abs(up[: len(x) * os]).reshape(len(x), os).max(axis=1)
    from scipy.ndimage import maximum_filter1d, minimum_filter1d
    la = max(1, int(sr * lookahead_ms / 1000))
    need = np.minimum(1.0, ceil / np.maximum(pk, 1e-12))
    need = minimum_filter1d(need, size=2 * la + 1)
    r = math.exp(-1 / (sr * release_ms / 1000))
    # release smoothing (gain may only rise slowly): one-pole on (1 - g) with instant attack via running min
    g = np.empty_like(need)
    cur = 1.0
    for i, v in enumerate(need):
        cur = v if v < cur else r * cur + (1 - r) * v
        g[i] = cur
    g = signal.lfilter([0.5, 0.5], [1], g)  # tiny smoothing against zipper
    return x * g


def finalize(y, sr=SR, target=TARGET_LUFS, tp_ceil=TP_CEIL):
    """Normalise to target LUFS, keep true peak <= tp_ceil (look-ahead limiter on 4x oversampled peaks)."""
    y = fades(y, sr)
    for it in range(6):
        L = lufs(y, sr)
        y = y * 10 ** ((target - L) / 20)
        tp = true_peak_db(y, sr)
        if tp <= tp_ceil - 0.15:
            break
        y = tp_limit(y, sr, ceil_db=tp_ceil - 0.35 - 0.1 * it)
    L, tp = lufs(y, sr), true_peak_db(y, sr)
    if tp > tp_ceil:  # last resort: trade a little loudness for the TP ceiling
        y = y * 10 ** ((tp_ceil - 0.05 - tp) / 20)
        L, tp = lufs(y, sr), true_peak_db(y, sr)
    return y, {"lufs": round(L, 2), "true_peak_db": round(tp, 2), "sample_peak_db": round(20 * np.log10(np.abs(y).max() + 1e-12), 2)}


# ------------------------------------------------------------------ chains
def chain_dry(x, sr=SR, seed=0, rotor=False, rotor_db=-12.0, **k):
    """Trim/-18 LUFS happen outside; optional rotor bed for lines shouted inside the helicopter cabin/LZ (D201, D204)."""
    y = hp(x, 70, sr, 2)
    if rotor:
        bed = lp(rotor_bed(len(y), sr, seed + 11), 6000, sr, 2)
        g = np.ones(len(y)); f = int(sr * 0.03)
        g[:f] = np.linspace(0, 1, f); g[-f:] = np.linspace(1, 0, f)
        y = y + bed * g * speech_rms(y, sr) * 10 ** (rotor_db / 20)
    return y, {"offset_s": 0.0}


def _room(x, sr, seed, rt60, wet_db, early, lp_hz, length):
    ir = synth_ir(sr, rt60, early, wet_db, lp_hz, seed, length=length)
    y = convolve(hp(x, 70, sr, 2), ir)
    return trim_tail(y, sr)


def chain_booth(x, sr=SR, seed=0, **k):
    """Dry plus a 0.18 s concrete slap (small bare interrogation room)."""
    early = [(4.1, -9), (6.8, -11), (9.7, -12), (13.3, -14), (17.9, -16), (23.0, -19)]
    return _room(x, sr, seed, rt60=0.18, wet_db=-13, early=early, lp_hz=5500, length=0.3), {"offset_s": 0.0}


def chain_clean(x, sr=SR, seed=0, **k):
    """Trim, -18 LUFS and a small concrete-room slap (synthetic 0.25 s IR)."""
    early = [(5.3, -11), (8.9, -13), (12.4, -14), (17.1, -17), (24.6, -20)]
    return _room(x, sr, seed, rt60=0.25, wet_db=-15, early=early, lp_hz=6000, length=0.35), {"offset_s": 0.0}


def _key_up(sr, seed, ms=34):
    n = int(sr * ms / 1000)
    rng = np.random.default_rng(seed)
    nz = bp(rng.standard_normal(n), 700, 5500, sr, 2)
    nz /= np.abs(nz).max() + 1e-9
    e = env_burst(n, 0.02, 1.2)
    click = np.zeros(n)
    click[:int(sr * 0.0015)] = np.hanning(int(sr * 0.003))[: int(sr * 0.0015)] * rng.choice([-1, 1])
    return 0.25 * nz * e + 0.6 * click


def _squelch_tail(sr, seed, ms=170):
    """FM receiver squelch tail after the transmitter un-keys: a broadband 'kssht' that closes abruptly."""
    rng = np.random.default_rng(seed)
    n = int(sr * ms / 1000)
    nz = bp(rng.standard_normal(n), 450, 6000, sr, 2)
    nz /= np.abs(nz).max() + 1e-9
    t = np.linspace(0, 1, n)
    e = np.minimum(1, t / 0.04) * (1 - t ** 3) * (0.8 + 0.2 * np.sin(2 * np.pi * 37 * t * ms / 1000))
    return nz * e


def _radio_core(v, sr, seed, lo, hi, drive, honk_hz, hiss_db, post_lo=250, post_hi=4200):
    v = bp(v, lo, hi, sr, 4)
    v = v / (np.abs(v).max() + 1e-9)
    v = soft_clip(v, drive)
    v = compress(v, sr, thresh_db=-16, ratio=3, attack_ms=4, release_ms=70)
    v = peq(v, honk_hz, 5.0, 1.6, sr)
    sr_ = speech_rms(v, sr)
    hiss = bp(noise(len(v), seed + 7), lo, hi, sr, 2)
    v = v + hiss / (rms(hiss) + 1e-12) * sr_ * 10 ** (hiss_db / 20)
    return v


def chain_radio(x, sr=SR, seed=0, rotor=False, rotor_db=-18.0, **k):
    """Field radio (PRC-77-style FM): 320-3200 Hz, tanh drive, compression, 1.6 kHz honk, hiss bed, key-up
    squelch in the 40 ms lead (speech onset unchanged) and a squelch tail after release."""
    rng = np.random.default_rng(seed)
    v = x.copy()
    if rotor:  # pilot's mic hears the cabin: rotor bed 18 dB under the speech, gated with the transmission
        bed = rotor_bed(len(v), sr, seed + 11)
        v = v + bed * speech_rms(v, sr) * 10 ** (rotor_db / 20)
    v = _radio_core(v, sr, seed, 320, 3200, rng.uniform(2.6, 3.4), 1600, -42)
    ku = _key_up(sr, seed + 3, ms=int(rng.uniform(30, 38)))
    pk = np.abs(v).max()
    v[: len(ku)] += ku * pk * 0.55
    tail = _squelch_tail(sr, seed + 5, ms=int(rng.uniform(150, 210))) * pk * rng.uniform(0.45, 0.6)
    # release: the transmission ends ~60 ms after the last word (inside the take's 140 ms tail)
    cut = max(len(ku), len(v) - int(sr * 0.08))
    y = np.concatenate([v[:cut], tail])
    y = bp(y, 250, 4200, sr, 4)
    return y, {"offset_s": 0.0}


def chain_intercom(x, sr=SR, seed=0, **k):
    """Helicopter intercom: 250-4500 Hz, light drive, no squelch, rotor bed 18 dB under (gated with the mic),
    5.2 kHz low-pass after the drive (earpiece bandwidth)."""
    rng = np.random.default_rng(seed)
    bed = rotor_bed(len(x), sr, seed + 11)
    v = x + bed * speech_rms(x, sr) * 10 ** (-18 / 20)
    v = bp(v, 250, 4500, sr, 4)
    v = v / (np.abs(v).max() + 1e-9)
    v = soft_clip(v, rng.uniform(1.6, 2.0))
    v = compress(v, sr, thresh_db=-18, ratio=2.5, attack_ms=5, release_ms=90)
    v = peq(v, 2200, 3.0, 1.2, sr)
    v = lp(v, 5200, sr, 4)  # headset earpiece/amp bandwidth: the drive's harmonics above 5 kHz never reach the ear
    g = np.ones(len(v))
    f = int(sr * 0.02)
    g[:f] = np.linspace(0, 1, f); g[-f:] = np.linspace(1, 0, f)
    return v * g, {"offset_s": 0.0}


def chain_scrambler(x, sr=SR, seed=0, **k):
    """Booth voice scrambler: -3 st phase vocoder, 300-3400 Hz, 30 Hz ring mod 15 % wet, 10-bit crush, 50 Hz hum -40 dB."""
    import librosa
    y = librosa.effects.pitch_shift(x.astype(np.float32), sr=sr, n_steps=-3, res_type="soxr_hq").astype(np.float64)
    y = bp(y, 300, 3400, sr, 4)
    t = np.arange(len(y)) / sr
    y = 0.85 * y + 0.15 * y * np.sin(2 * np.pi * 30 * t)
    y = y / (np.abs(y).max() + 1e-9) * 0.9
    y = np.round(y * 512) / 512  # 10-bit
    # pre-level the speech to the final loudness so the hum lands at -40 dBFS in the shipped file
    y = y * 10 ** ((TARGET_LUFS - lufs(y, sr)) / 20)
    hum = 10 ** (-40 / 20) * math.sqrt(2) * (np.sin(2 * np.pi * 50 * t) + 0.5 * np.sin(2 * np.pi * 100 * t + 0.4)
                                              + 0.35 * np.sin(2 * np.pi * 150 * t + 1.1) + 0.12 * np.sin(2 * np.pi * 250 * t))
    hum /= 1.25
    return y + hum, {"offset_s": 0.0}


def chain_tape(x, sr=SR, seed=0, **k):
    """Reel-to-reel briefing tape: wow 0.5 Hz +-0.3 %, flutter 7 Hz +-0.1 %, 80-7500 Hz, saturation, hiss -42 dB,
    motor thump at the start (speech moves 0.22 s later)."""
    rng = np.random.default_rng(seed)
    pre = int(sr * 0.22)
    post = int(sr * 0.18)
    v = np.concatenate([np.zeros(pre), x, np.zeros(post)])
    v = varispeed(v, sr, 0.5, 0.3, 7.0, 0.1, seed)
    v = peq(v, 110, 2.0, 0.9, sr)  # head bump
    v = bp(v, 80, 7500, sr, 3)
    v = v / (np.abs(v).max() + 1e-9)
    v = soft_clip(v + 0.03 * v ** 2, 1.6)
    v = v * 10 ** ((TARGET_LUFS - lufs(v, sr)) / 20)
    hiss = hp(noise(len(v), seed + 9), 1500, sr, 1) * 0.7 + noise(len(v), seed + 10, "pink") * 0.3
    hiss = lp(hiss, 9000, sr, 2)
    hiss *= 10 ** (-42 / 20) / (rms(hiss) + 1e-12)
    n = int(sr * 0.16)
    tt = np.arange(n) / sr
    thump = np.sin(2 * np.pi * 52 * tt * (1 - 0.3 * tt / tt[-1])) * np.exp(-tt / 0.035) * 0.35
    clk = bp(noise(int(sr * 0.012), seed + 12), 1500, 7000, sr, 2) * env_burst(int(sr * 0.012), 0.05, 2) * 0.05
    thump[: len(clk)] += clk
    v[:n] += thump
    hiss_env = np.ones(len(v))
    ramp = int(sr * 0.09)
    hiss_env[:ramp] = np.linspace(0, 1, ramp)
    return v + hiss * hiss_env, {"offset_s": pre / sr}


def chain_pa_horn(x, sr=SR, seed=0, **k):
    """Town PA horns: 400-4000 Hz, +6 dB at 1.2 kHz, driver saturation, multi-tap slapback 180/420/900 ms
    (-6/-10/-16 dB), each echo darker and smeared (reflections off buildings)."""
    rng = np.random.default_rng(seed)
    v = bp(x, 400, 4000, sr, 4)
    v = peq(v, 1200, 6.0, 1.1, sr)
    v = v / (np.abs(v).max() + 1e-9)
    v = soft_clip(v, 2.2)
    v = compress(v, sr, thresh_db=-14, ratio=3, attack_ms=3, release_ms=120)
    v = bp(v, 380, 4300, sr, 2)
    tail_s = 1.6
    y = np.concatenate([v, np.zeros(int(sr * tail_s))])
    for ms, db, lpf in ((180, -6, 3400), (420, -10, 2600), (900, -16, 1900)):
        d = int(sr * ms / 1000 * rng.uniform(0.98, 1.02))
        smear = env_burst(int(sr * 0.025), 0.1, 2) * np.abs(noise(int(sr * 0.025), seed + ms))
        smear /= smear.sum()
        e = lp(np.convolve(v, smear)[: len(v)], lpf, sr, 2) * 10 ** (db / 20)
        y[d: d + len(e)] += e[: len(y) - d]
    # light diffuse outdoor tail (whole town)
    ir = synth_ir(sr, 1.4, [], -16, 2500, seed + 4, length=1.5, predelay_ms=40)
    ir[0] = 0
    y = y + convolve(y, ir)[: len(y)]
    return trim_tail(y, sr), {"offset_s": 0.0}


def chain_hf_am(x, sr=SR, seed=0, **k):
    """Shortwave AM numbers broadcast: 200-2800 Hz, AM fading 0.2 Hz 30 %, 1.1 kHz heterodyne at -30 dB,
    wow/flutter, detector grit and atmospheric static."""
    rng = np.random.default_rng(seed)
    v = varispeed(x, sr, 0.35, 0.25, 6.0, 0.12, seed)
    v = bp(v, 200, 2800, sr, 4)
    v = v / (np.abs(v).max() + 1e-9)
    v = soft_clip(v, 1.5)
    t = np.arange(len(v)) / sr
    fade = 1 - 0.3 * (0.5 + 0.5 * np.sin(2 * np.pi * 0.2 * t + rng.uniform(0, 6.28)))
    fade *= 1 - 0.06 * (0.5 + 0.5 * np.sin(2 * np.pi * 0.071 * t + rng.uniform(0, 6.28)))
    v = v * fade
    s = speech_rms(v, sr)
    f_het = 1100 + 3 * np.sin(2 * np.pi * 0.05 * t)
    het = np.sin(2 * np.pi * np.cumsum(f_het) / sr) * s * math.sqrt(2) * 10 ** (-30 / 20)
    stat = bp(noise(len(v), seed + 3, "pink"), 200, 3000, sr, 2)
    stat *= s * 10 ** (-27 / 20) / (rms(stat) + 1e-12)
    stat *= 0.75 + 0.25 * (0.5 + 0.5 * np.sin(2 * np.pi * 0.2 * t + 1.3))  # static rises when the signal fades
    crk = np.zeros(len(v))
    for _ in range(int(len(v) / sr * 3)):
        i = rng.integers(0, len(v) - 200)
        crk[i:i + 120] += rng.standard_normal(120) * np.exp(-np.arange(120) / 25) * s * rng.uniform(0.3, 1.2)
    crk = bp(crk, 300, 3000, sr, 2)
    y = bp(v + het + stat + crk, 180, 3000, sr, 4)  # receiver IF bandwidth limits everything, static included
    g = np.ones(len(y)); f = int(sr * 0.015)
    g[:f] = np.linspace(0, 1, f)
    return y * g, {"offset_s": 0.0}


def chain_room_speaker(x, sr=SR, seed=0, **k):
    """Small loudspeaker in the concrete basement: 250-5000 Hz, cone resonances, slight distortion (harmonics rolled
    off above 6 kHz by the cone), concrete IR."""
    v = bp(x, 250, 5000, sr, 4)
    v = peq(v, 820, 5.0, 1.5, sr)
    v = peq(v, 3200, 3.0, 2.0, sr)
    v = v / (np.abs(v).max() + 1e-9)
    v = soft_clip(v, 1.5)
    v = lp(v, 6000, sr, 4)  # small cone rolls off the distortion harmonics
    early = [(6.2, -6), (11.5, -8), (17.8, -9), (24.1, -11), (33.7, -13), (46.0, -16)]
    ir = synth_ir(sr, 0.75, early, -7, 4000, seed, length=0.9)
    y = convolve(v, ir)
    return trim_tail(y, sr), {"offset_s": 0.0}


def chain_greycoat(x, sr=SR, seed=0, **k):
    """Greycoat squad radios: harder, narrower radio (350-2900 Hz, 1.2 kHz honk) with the alien two-tone squelch
    tail (1.4 kHz then 2.1 kHz, 60 ms each) instead of a noise tail."""
    rng = np.random.default_rng(seed)
    v = _radio_core(x.copy(), sr, seed, 350, 2900, rng.uniform(3.4, 4.0), 1200, -38)
    pk = np.abs(v).max()
    ku = _key_up(sr, seed + 3, ms=30)
    v[: len(ku)] += ku * pk * 0.4
    cut = max(len(ku), len(v) - int(sr * 0.09))
    n = int(sr * 0.06)
    tt = np.arange(n) / sr
    ramp = np.minimum(1, np.minimum(tt, tt[::-1]) / 0.005)
    tone = np.concatenate([np.sin(2 * np.pi * 1400 * tt) * ramp, np.sin(2 * np.pi * 2100 * tt) * ramp])
    chk = _squelch_tail(sr, seed + 5, ms=45) * 0.5
    y = np.concatenate([v[:cut], np.zeros(int(sr * 0.02)), tone * pk * 0.2, chk * pk * 0.6])
    y = bp(y, 300, 3600, sr, 4)
    return y, {"offset_s": 0.0}


def chain_far(x, sr=SR, seed=0, **k):
    """Distant bark (~60-100 m across town): air absorption + diffraction low-pass, thinner low end,
    street slapbacks and a reverberant tail that dominates the direct sound."""
    v = hp(x, 160, sr, 2)
    v = lp(v, 2400, sr, 2)
    v = shelf(v, 1500, -6, sr, high=True)
    v = compress(v, sr, thresh_db=-22, ratio=2, attack_ms=10, release_ms=150)
    early = [(38, -5), (71, -7), (118, -9), (164, -12), (240, -15)]
    ir = synth_ir(sr, 1.3, early, -4, 1800, seed, length=1.5, predelay_ms=30)
    y = convolve(v, ir)
    return trim_tail(y, sr), {"offset_s": 0.0}


CHAINS = {"DRY": chain_dry, "BOOTH": chain_booth, "CLEAN": chain_clean, "RADIO": chain_radio, "INTERCOM": chain_intercom,
          "SCRAMBLER": chain_scrambler, "TAPE": chain_tape, "PA_HORN": chain_pa_horn, "HF_AM": chain_hf_am,
          "ROOM_SPEAKER": chain_room_speaker, "GREYCOAT_SQUELCH": chain_greycoat, "FAR": chain_far}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src"); ap.add_argument("dst")
    ap.add_argument("--chain", required=True, choices=sorted(CHAINS))
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--rotor", action="store_true", help="RADIO: mix the procedural rotor bed into the mic")
    a = ap.parse_args()
    x, sr = read_audio(a.src)
    x = to_sr(x, sr, SR)
    y, info = CHAINS[a.chain](x, SR, seed=a.seed, rotor=a.rotor)
    y, m = finalize(y)
    write_wav16(a.dst, y, SR, seed=a.seed)
    print(a.chain, m, info)


if __name__ == "__main__":
    main()
