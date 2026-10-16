"""Procedural (physics-based) WP06_SFX one-shots, built only from synthesis: no generated or recorded material.

- Supersonic bullet cracks (SFX_BULLET_CRACK): the sound of a bullet passing within a few metres is a ballistic
  shock wave, an N-wave whose length follows Whitham's far-field rule
      T = 1.82 * M * d * r^(1/4) / (c * (M^2 - 1)^(3/8) * l^(1/4))
  (M Mach number, d calibre, l projectile length, r miss distance, c 343 m/s), i.e. 120-250 us for rifle bullets at
  0.5-4 m. It is synthesised at 16x oversampling (the N-wave is only 6-12 samples long at 48 kHz), followed by its
  ground reflection, the turbulent wake "zip" of a close pass, discrete wall/ground reflections and a short diffuse
  slap tail, then a 40 Hz high-pass (a microphone does not pass the N-wave's DC) and a light 4x-oversampled soft
  clip like an overloaded field recorder. Mirelo/ElevenLabs could not produce a single clean crack (QA: doubles,
  pre-roll noise), and the physics is simple enough to do exactly.
- UI hit marker ticks (SFX_UI_HITMARKER): a short damped metallic tick (inharmonic modes + click), dry, 40-70 ms.

usage: python proc_sfx.py            # writes RawAssets/audio/sfx/_procedural/*.wav (sfx_process.py publishes them)
"""
from __future__ import annotations

import pathlib
import sys

import numpy as np
from scipy import signal

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import alib as A  # noqa: E402

SR = A.SR
OUT = pathlib.Path("/Users/gencsaracaydin/Documents/fun/fun-unity/RawAssets/audio/sfx/_procedural")
C = 343.0


def n_wave_T(mach, d_m, l_m, r_m):
    return 1.82 * mach * d_m * r_m ** 0.25 / (C * (mach ** 2 - 1) ** 0.375 * l_m ** 0.25)


def n_wave(T, os_=16, rise_us=4.0):
    """Unit N-wave of length T at SR*os_: jump to +1, linear fall to -1, jump back to 0; finite rise time."""
    fs = SR * os_
    n = max(4, int(round(T * fs)))
    w = np.linspace(1.0, -1.0, n)
    pad = int(0.004 * fs)
    y = np.concatenate([np.zeros(pad), w, np.zeros(pad)])
    k = max(1, int(rise_us * 1e-6 * fs))
    y = np.convolve(y, np.ones(k) / k, mode="same")
    return y


def render_crack(seed, mach, d_mm, l_mm, r_m, env="street"):
    rng = np.random.default_rng(seed)
    os_ = 16
    fs = SR * os_
    T = n_wave_T(mach, d_mm / 1000, l_mm / 1000, r_m)
    L = int(0.9 * fs)
    y = np.zeros(L)
    nw = n_wave(T, os_)
    t0 = int(0.003 * fs)
    y[t0:t0 + len(nw)] += nw
    # ground reflection: listener ear ~1.6 m, bullet ~1.5 m above ground -> path difference gives 3-9 ms
    h = 1.6
    dg = np.sqrt((2 * h) ** 2 + r_m ** 2) - r_m
    tg = t0 + int(dg / C * fs)
    gl = rng.uniform(0.45, 0.7)                                   # sand/stone absorbs some
    nwg = n_wave(T * 1.05, os_, rise_us=12.0) * gl
    y[tg:tg + len(nwg)] += nwg[: L - tg]
    y = signal.resample_poly(y, 1, os_)                           # -> 48 kHz (anti-aliased)
    n = len(y)
    # turbulent wake "zip": a few ms of band-passed noise right after the shock for close passes
    if r_m < 2.5:
        zl = int(rng.uniform(0.008, 0.02) * SR)
        z = rng.standard_normal(zl)
        z = A.bp(z[:, None], 1800, 9000, order=2)[:, 0]
        z *= np.exp(-np.linspace(0, rng.uniform(3, 5), zl))
        z *= A.undb(rng.uniform(-20, -14) + (2.5 - r_m) * 2) / (np.abs(z).max() + 1e-9)
        i = int(0.003 * SR) + int(T * SR) + 2
        y[i:i + zl] += z
    # discrete reflections (walls, ground, rooftops): darker, lower copies
    base = y[: int(0.012 * SR)].copy()
    if env == "street":
        refl = [(rng.uniform(0.006, 0.02), rng.uniform(-9, -5)), (rng.uniform(0.018, 0.04), rng.uniform(-12, -7)),
                (rng.uniform(0.035, 0.07), rng.uniform(-15, -10)), (rng.uniform(0.06, 0.11), rng.uniform(-18, -13)),
                (rng.uniform(0.09, 0.16), rng.uniform(-22, -16))]
        rt, tail_db = rng.uniform(0.35, 0.55), rng.uniform(-30, -26)
    else:  # open desert / ridge
        refl = [(rng.uniform(0.04, 0.09), rng.uniform(-22, -16)), (rng.uniform(0.12, 0.25), rng.uniform(-28, -22))]
        rt, tail_db = rng.uniform(0.6, 0.9), rng.uniform(-36, -32)
    for dt, g in refl:
        e = A.lp(base[:, None], rng.uniform(3500, 7000), order=2)[:, 0] * A.undb(g)
        i = int(dt * SR)
        y[i:i + len(e)] += e[: n - i]
    # diffuse slap tail
    tl = n
    tt = np.arange(tl) / SR
    tail = rng.standard_normal(tl) * np.exp(-6.91 * tt / rt)
    tail = A.bp(tail[:, None], 500, 6000, order=2)[:, 0]
    tail *= np.clip((tt - 0.004) / 0.01, 0, 1)
    tail *= A.undb(tail_db) / (np.sqrt(np.mean(tail[: int(0.05 * SR)] ** 2)) + 1e-9) * np.abs(y).max() * 0.25
    y = y + tail
    y = A.hp(y[:, None], 40, order=2)
    y = y / (np.abs(y).max() + 1e-12)
    y = A.clip_os(y, rng.uniform(3, 6), -1.0)                    # mild recorder overload, dense like the real thing
    y = A.fade(y, 0.0002, 0.25, curve=2.5)
    return y, T


def render_hitmarker(seed):
    rng = np.random.default_rng(seed)
    n = int(0.07 * SR)
    t = np.arange(n) / SR
    modes = [(rng.uniform(2900, 3300), 1.0, rng.uniform(0.010, 0.016)),
             (rng.uniform(4700, 5300), 0.6, rng.uniform(0.007, 0.011)),
             (rng.uniform(7400, 8200), 0.35, rng.uniform(0.004, 0.007)),
             (rng.uniform(1500, 1800), 0.25, rng.uniform(0.006, 0.01))]
    y = np.zeros(n)
    for f, a, tau in modes:
        y += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / tau)
    click = rng.standard_normal(int(0.0015 * SR))
    click = A.hp(click[:, None], 2500, order=2)[:, 0] * np.hanning(len(click))
    y[: len(click)] += 0.8 * click / (np.abs(click).max() + 1e-9)
    y = A.fade(y[:, None], 0.0003, 0.02)
    return y / (np.abs(y).max() + 1e-12) * A.undb(-1.5)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    specs = [  # (seed, Mach, calibre mm, length mm, miss m, env) - 7.62x39 ~ M2.1, 5.56 ~ M2.8, 7.62x54 ~ M2.4
        (11, 2.1, 7.9, 26.0, 0.8, "street"), (12, 2.0, 7.9, 26.0, 1.6, "street"), (13, 2.2, 7.9, 26.0, 2.8, "street"),
        (14, 2.4, 7.9, 32.0, 1.2, "desert"), (15, 2.1, 7.9, 26.0, 2.0, "desert"), (16, 2.6, 5.7, 23.0, 1.0, "street"),
    ]
    for k, (seed, m, d, l_, r, env) in enumerate(specs, 1):
        y, T = render_crack(seed, m, d, l_, r, env)
        A.save(OUT / f"SFX_BULLET_CRACK_proc_{k:02d}.wav", y)
        print(f"crack {k}: M{m} d{d} r{r} m {env}: N-wave {T * 1e6:.0f} us")
    for k in range(1, 5):
        A.save(OUT / f"SFX_UI_HITMARKER_proc_{k:02d}.wav", render_hitmarker(40 + k))
    print("written to", OUT)


if __name__ == "__main__":
    main()
