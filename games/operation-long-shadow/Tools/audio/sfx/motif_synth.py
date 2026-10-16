"""Procedural audio for WP06_SFX (TECH_PLAN D15): the Clockmaker three-note music-box motif, the interval signal,
the outro piece MUS_06_SIXFORTY, the EL Music style reference, and the synthesised SFX (radio squelch code,
key-up/release, tinnitus, film leader beep, CRT whine, stuck clock tick).

The motif is fixed here once and reused everywhere:  D6 - F6 - C#6  (rising minor third, then falling to the
leading tone, left unresolved).  MOTIF below is the single source of truth.

Music-box timbre = additive synthesis of a clamped-free steel tine (fundamental, weak 2nd harmonic from the
asymmetric swing, a pair of close upper modes near 5.9x / 6.3x that beat, and short-lived higher modes), a
pin-pluck click, a small wooden soundboard IR and a small-room IR. Clock ticks = Mirelo tick slices when
available (our own takes) layered with a synthetic escapement click.

usage: python motif_synth.py all     (writes RawAssets/audio/music/procedural/*.wav; build.py picks them up)
"""
from __future__ import annotations

import pathlib
import sys

import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import alib as A  # noqa: E402

SR = A.SR
ROOT = pathlib.Path("/Users/gencsaracaydin/Documents/fun/fun-unity")
OUT = ROOT / "RawAssets/audio/music/procedural"

NOTE = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def hz(name):
    n, o = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((NOTE[n] + 12 * (o + 1) - 69) / 12)


MOTIF = ["D6", "F6", "C#6"]          # the Clockmaker chime (D15) - never change after the music cues are generated
MOTIF_HZ = [hz(n) for n in MOTIF]     # 1174.66, 1396.91, 1108.73 Hz


# ----------------------------------------------------------------------------- music box
def tine(f, dur=3.0, vel=1.0, detune_cents=0.0, seed=0):
    rng = np.random.default_rng(seed)
    f = f * 2 ** (detune_cents / 1200)
    n = int(dur * SR)
    t = np.arange(n) / SR
    # pitch settles from slightly sharp after the pluck (tine stiffening at large amplitude)
    glide = 1 + 0.0018 * np.exp(-t / 0.04)
    # T60 shortens for higher notes
    t60 = np.clip(2.3 * (880 / f) ** 0.6, 0.9, 3.5)  # ~1.9 s at 1.1 kHz, ~3.5 s for low tines
    modes = [  # ratio, amp, t60 factor
        (1.0, 1.0, 1.0),
        (2.0, 0.10, 0.45),
        (5.93, 0.22 * vel, 0.16),
        (6.27, 0.15 * vel, 0.14),
        (12.3, 0.05 * vel, 0.06),
        (17.55, 0.05 * vel, 0.035),
    ]
    y = np.zeros(n)
    for r, a, tf in modes:
        fr = f * r * (1 + rng.normal(0, 0.0015))
        if fr > SR * 0.45:
            continue
        ph = 2 * np.pi * np.cumsum(fr * glide) / SR + rng.uniform(0, 2 * np.pi)
        y += a * np.sin(ph) * np.exp(-6.91 * t / (t60 * tf))
    # attack: 0.6 ms rise, pin pluck click
    y *= 1 - np.exp(-t / 0.0006)
    click = rng.standard_normal(int(0.004 * SR)) * np.exp(-np.arange(int(0.004 * SR)) / (0.0007 * SR))
    click = A.bp(click[:, None], 2500, 9000, order=2)[:, 0]
    y[: len(click)] += 0.25 * vel * click / (np.abs(click).max() + 1e-9)
    # damper buzz on note end is not modelled (music box tines ring free)
    return (vel * y)[:, None]


_SB = None
_ROOM = None


def soundboard_ir():
    global _SB
    if _SB is None:
        rng = np.random.default_rng(7)
        n = int(0.12 * SR)
        t = np.arange(n) / SR
        ir = rng.standard_normal(n) * np.exp(-6.91 * t / 0.07)
        ir = ir[:, None]
        y = np.zeros_like(ir)
        for fc, g, q in ((420, 1.0, 5), (930, 0.8, 6), (1750, 0.6, 7), (3100, 0.35, 6)):
            y += g * A.biquad(ir, "peak", fc, 14, q) - g * ir
        ir = y + 0.3 * ir
        ir[0] += 6.0  # direct
        _SB = ir / np.sqrt((ir ** 2).sum())
    return _SB


def room_ir():
    global _ROOM
    if _ROOM is None:
        _ROOM = A.synth_ir(rt60=0.55, predelay=0.006, er=[(0.004, -6, 7000), (0.009, -8, 6000), (0.013, -9, 5000),
                                                         (0.021, -11, 4000)], lp_hz=7000, stereo_ir=True, seed=11)
    return _ROOM


def musicbox(events, total, mech=True, room_mix=0.22, seed=1, stereo=True):
    """events: [(time_s, note_name_or_hz, velocity, detune_cents)] -> stereo render of a music box in a small room."""
    n = int(total * SR)
    dry = np.zeros((n, 1))
    rng = np.random.default_rng(seed)
    for k, ev in enumerate(events):
        t0, nm, vel = ev[0], ev[1], ev[2]
        det = ev[3] if len(ev) > 3 else 0.0
        f = hz(nm) if isinstance(nm, str) else nm
        y = tine(f, dur=min(4.0, total - t0 + 0.01), vel=vel, detune_cents=det, seed=seed * 100 + k)
        i = int(t0 * SR)
        y = y[: n - i]
        dry[i:i + len(y)] += y
    body = A.convolve(dry, soundboard_ir())[:n]
    if mech:  # governor fan whirr and comb-cylinder rumble, very low
        nz = rng.standard_normal((n, 1))
        whirr = A.bp(nz, 180, 1400, order=2) * (1 + 0.3 * np.sin(2 * np.pi * 23 * np.arange(n) / SR))[:, None]
        body += whirr * A.undb(-52) * np.abs(body).max()
    x = A.stereo(body) if stereo else body
    if room_mix > 0:
        wet = A.convolve(body, room_ir())[:n]
        x = x * (1 - room_mix) + (wet if stereo else A.mono(wet)) * room_mix * 2.0
    return x


# ----------------------------------------------------------------------------- clock ticks
def synth_tick(tock=False, vel=1.0, seed=0):
    rng = np.random.default_rng(seed)
    n = int(0.09 * SR)
    t = np.arange(n) / SR
    exc = np.zeros(n)
    exc[0] = 1.0
    exc[: int(0.0012 * SR)] += rng.standard_normal(int(0.0012 * SR)) * 0.6
    s = 0.92 if tock else 1.0
    y = np.zeros(n)
    for fc, g, q, dec in ((2350 * s, 1.0, 14, 0.012), (4150 * s, 0.7, 18, 0.008), (6900 * s, 0.35, 20, 0.005),
                          (880 * s, 0.6, 5, 0.02), (310, 0.3, 3, 0.03)):
        r = A.biquad(exc[:, None], "peak", fc, 30, q)[:, 0] - exc
        y += g * r * np.exp(-t / dec)
    y = A.hp(y[:, None], 150, order=2)[:, 0]
    return vel * (y / (np.abs(y).max() + 1e-9))[:, None]


def mirelo_ticks():
    """Slices of our Mirelo clock-tick takes (SFX_CLOCK_TICK_STUCK/mirelo_tick), best 6 by clarity."""
    d = ROOT / "RawAssets/audio/sfx/SFX_CLOCK_TICK_STUCK/mirelo_tick"
    ticks = []
    for f in sorted(d.glob("*generated_audio*.wav")):
        x, _ = A.load(f, mono=True)
        x = A.remove_dc(x, 60)
        ev = A.detect_events(x, min_gap=0.35, rise_db=15, floor_db=-30, min_len=0.02, max_len=0.25)
        for s, e in ev:
            seg = x[max(0, s - 48): e]
            if len(seg) < 400:
                continue
            m = A.measure(seg)
            ticks.append((m["crest_db"], A.fade(seg, 0.0003, 0.02)))
    ticks.sort(key=lambda z: -z[0])
    return [A.peak_norm(t, -1) for _, t in ticks[:6]]


def tick(i, tock=False, use_real=True):
    real = mirelo_ticks() if use_real else []
    syn = synth_tick(tock, seed=i)
    if real:
        r = real[i % len(real)]
        L = max(len(r), len(syn))
        y = A.pad_to(r, L) * 0.75 + A.pad_to(syn, L) * 0.35
    else:
        y = syn
    return A.peak_norm(y, -1)


# ----------------------------------------------------------------------------- deliverables
def motif_events(t0=0.0, step=0.4, vel=(0.9, 0.85, 1.0), det=(0, 0, 0)):
    return [(t0 + k * step, MOTIF[k], vel[k], det[k]) for k in range(3)]


def render_motif():
    """MOTIF_MUSICBOX.wav: the chime once, ringing out (3.2 s)."""
    x = musicbox(motif_events(0.05), 3.2, seed=3)
    x = A.fade(x, 0.0005, 0.4)
    return A.loudness_to(x, -20.0, tp=-1.0)


def render_interval_signal():
    """INTERVAL_SIGNAL.wav (dry master, mono): 8 clock ticks 0.5 s apart, then the three-note chime; 6.0 s total."""
    total = 6.0
    n = int(total * SR)
    x = np.zeros((n, 1))
    for k in range(8):
        tk = tick(k, tock=bool(k % 2))
        i = int((0.02 + 0.5 * k) * SR)
        seg = tk[: n - i]
        x[i:i + len(seg)] += seg * A.undb(-3 if k % 2 else 0)
    chime = A.mono(musicbox(motif_events(4.02, 0.42), total, seed=5, room_mix=0.12))
    x = x * 0.55 + chime * 1.0
    x = A.fade(x, 0.0005, 0.25)
    return A.loudness_to(x, -18.0, tp=-1.0)


def render_sixforty():
    """MUS_06_SIXFORTY.wav: the music box alone, detuned and winding down, then silence (~24 s)."""
    ev = []
    t = 0.3
    beat = 0.95
    # phrase 1: motif with low dyads
    ev += [(t, "D6", 0.9, -4), (t, "D5", 0.45, 3), (t + beat, "F6", 0.85, 5), (t + 2 * beat, "C#6", 1.0, -8),
           (t + 2 * beat, "A4", 0.4, 6)]
    t += 4 * beat
    # phrase 2: the motif an octave lower, answering
    ev += [(t, "D5", 0.75, 6), (t + beat * 1.05, "F5", 0.7, -7), (t + 2.1 * beat, "C#5", 0.85, 9), (t + 2.1 * beat, "A3", 0.35, -5)]
    t += 4.3 * beat
    # phrase 3: motif over a dissonant Bb under the leading tone
    ev += [(t, "D6", 0.85, -10), (t, "F5", 0.4, 4), (t + beat * 1.12, "F6", 0.8, 12), (t + 2.3 * beat, "C#6", 0.95, -14),
           (t + 2.3 * beat, "A#4", 0.45, 10)]
    t += 4.9 * beat
    # phrase 4: the spring runs down - each note later and flatter, the last one never quite arrives in tune
    ev += [(t, "D6", 0.7, -16), (t + beat * 1.45, "F6", 0.6, -22), (t + beat * 3.4, "C#6", 0.55, -38)]
    end = t + beat * 3.4 + 4.2
    x = musicbox(ev, end + 3.0, seed=9, room_mix=0.25)
    # mechanism stops: a soft governor clunk right after the last note
    clunk = A.lp(synth_tick(True, 0.5, seed=99), 1800, order=2)
    i = int((t + beat * 3.4 + 0.35) * SR)
    x[i:i + len(clunk)] += A.stereo(clunk) * 0.08
    x = A.fade(x, 0.0005, 3.5)
    x[int(end * SR):] *= 0.0  # true silence after the ring-out (the score ends here)
    return A.loudness_to(x, -20.0, tp=-1.0)


def render_style_reference():
    """MOTIF_REF (offline only, never shipped): ~28 s at 72 BPM - the music box motif four times over an analog
    synth drone with tape hiss, so EL Music v2.5 learns the palette and the exact three notes."""
    bpm = 72.0
    beat = 60 / bpm
    total = 16 * beat * 2  # 8 bars of 4/4 at 72 BPM = 26.7 s
    n = int(total * SR)
    tt = np.arange(n) / SR
    # analog drone: detuned saw pair on D2 + A2, slow filter sweep
    def saw(f, det):
        ph = (f * (1 + det) * tt) % 1.0
        return 2 * ph - 1
    drone = (saw(hz("D2"), 0.0) + saw(hz("D2"), 0.004) + 0.6 * saw(hz("A2"), -0.003) + 0.4 * saw(hz("D3"), 0.002))[:, None]
    lfo = 0.5 + 0.5 * np.sin(2 * np.pi * tt / (8 * beat))
    lo = A.lp(drone, 380, order=2)
    hi_ = A.lp(drone, 1400, order=2)
    drone = lo * (1 - lfo[:, None] * 0.5) + hi_ * (lfo[:, None] * 0.5)
    drone = A.stereo(drone) * np.array([[1.0, 0.92]])
    drone = drone / np.abs(drone).max() * A.undb(-14)
    # slow pulse: soft low synth thump on each beat
    pulse = np.zeros((n, 1))
    for b in range(int(total / beat)):
        i = int(b * beat * SR)
        k = np.arange(int(0.25 * SR)) / SR
        th = np.sin(2 * np.pi * (55 + 40 * np.exp(-k / 0.02)) * k) * np.exp(-k / 0.08)
        pulse[i:i + len(th), 0] += th[: n - i] * (0.6 if b % 4 else 1.0)
    pulse = A.stereo(pulse) * A.undb(-16)
    # music box motif at bars 1, 3, 5, 7 (beats 1, 2, 3)
    ev = []
    for bar in (0, 2, 4, 6):
        t0 = bar * 4 * beat + 0.02
        ev += motif_events(t0, beat, det=(0, 0, 0))
    mb = musicbox(ev, total, seed=13)
    hiss = A.stereo(A.hp(np.random.default_rng(3).standard_normal((n, 1)), 3000, order=2)) * A.undb(-46)
    x = drone + pulse + mb * A.undb(4) + hiss
    x = A.fade(x, 0.5, 1.0)
    return A.loudness_to(x, -16.0, tp=-1.0)


# ----------------------------------------------------------------------------- synth SFX
def squelch_burst(ms=70, level_db=-6, seed=0, lo=500, hi=6500):
    rng = np.random.default_rng(seed)
    n = int(SR * ms / 1000)
    nz = A.bp(rng.standard_normal((n, 1)), lo, hi, order=2)
    env = np.minimum(1, np.arange(n) / (0.001 * SR)) * np.exp(-np.arange(n) / (0.6 * n))
    return nz / (np.abs(nz).max() + 1e-9) * env[:, None] * A.undb(level_db)


def ptt_click(seed=0):
    """Mechanical press-to-talk click + the carrier 'chunk' of a squelch break."""
    c = synth_tick(seed=seed)[: int(0.03 * SR)] * 0.5
    b = squelch_burst(55, -4, seed)
    return A.mix((c, 0, 0), (b, 0, 0.004))


def two_tone(f1=1400, f2=2100, ms=60):
    n = int(SR * ms / 1000)
    t = np.arange(n) / SR
    env = np.minimum(1, t / 0.003) * np.minimum(1, (t[::-1]) / 0.004)
    a = np.sin(2 * np.pi * f1 * t) * env
    b = np.sin(2 * np.pi * f2 * t) * env
    return np.r_[a, b][:, None] * 0.6


def radio_band(x, lo=300, hi=3400, drive=2.0):
    y = A.bp(x, lo, hi, order=3)
    y = A.soft_clip(y / (np.abs(y).max() + 1e-9), drive)
    return y


def render_radio_clicks(nclicks, seed):
    rng = np.random.default_rng(seed)
    parts = []
    t = 0.0
    for k in range(nclicks):
        parts.append((ptt_click(seed * 10 + k), 0, t))
        hiss = squelch_burst(int(rng.uniform(70, 110)), -16, seed * 10 + k, 300, 4000)
        parts.append((hiss, 0, t + 0.05))
        t += rng.uniform(0.16, 0.21)
    parts.append((two_tone(), -3, t + 0.02))
    parts.append((squelch_burst(120, -10, seed + 77), 0, t + 0.15))
    x = A.mix(*parts)
    x = radio_band(x, 300, 3400, 1.6)
    x = A.fade(x, 0.0005, 0.04)
    return A.finalize(x, -1.0)


def render_keyup(seed):
    rng = np.random.default_rng(seed)
    x = A.mix((ptt_click(seed), 0, 0), (squelch_burst(int(rng.uniform(90, 140)), -14, seed + 1, 300, 4200), 0, 0.03))
    x = radio_band(x, 280, 3600, 1.4)
    return A.finalize(A.fade(x, 0.0005, 0.03), -1.0)


def render_release(seed):
    rng = np.random.default_rng(seed)
    n = int(rng.uniform(0.18, 0.28) * SR)
    nz = A.bp(rng.standard_normal((n, 1)), 400, 5000, order=2)
    env = (np.minimum(1, np.arange(n) / (0.002 * SR)) * np.exp(-np.arange(n) / (0.35 * n)))[:, None]
    tail = nz / np.abs(nz).max() * env
    x = A.mix((synth_tick(True, 0.4, seed)[: int(0.02 * SR)], -6, 0), (tail, -2, 0.003))
    x = radio_band(x, 280, 4200, 1.8)
    return A.finalize(A.fade(x, 0.0005, 0.05), -1.0)


def render_tinnitus(f, dur=3.0, seed=0):
    n = int(dur * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(seed)
    wob = 1 + 0.0015 * np.sin(2 * np.pi * 0.7 * t + rng.uniform(0, 6))
    ph = 2 * np.pi * np.cumsum(f * wob) / SR
    env = np.minimum(1, t / 0.15) * np.exp(-t / (dur * 0.42))
    y = (np.sin(ph) + 0.08 * np.sin(1.003 * ph + 1.0)) * env
    y = A.fade(y[:, None], 0.0005, 0.6)
    return A.finalize(y, -6.0)  # quieter by design: it sits on top of the muffled mix


def render_leader_beep(seed=0, warble=False):
    n = int(SR / 24)  # one frame at 24 fps
    t = np.arange(n) / SR
    f = 1000.0 * (1 + (0.004 * np.sin(2 * np.pi * 6 * t) if warble else 0 * t))
    y = np.sin(2 * np.pi * np.cumsum(f) / SR)
    y = A.soft_clip(y[:, None], 1.3)
    y = np.vstack([y, np.zeros((int(0.06 * SR), 1))])
    rng = np.random.default_rng(seed)
    hiss = A.bp(rng.standard_normal((len(y), 1)), 200, 7000, order=2) * A.undb(-40)
    y = A.bp(y, 120, 7000, order=2) + hiss
    y = A.fade(y, 0.001, 0.05)
    return A.finalize(y, -1.0)


def render_crt_whine(dur_cycles_s=3.0, mains=50):
    """3 s seamless body: 12 kHz whine (integer cycles) + mains hum harmonics + faint flyback buzz."""
    n = int(dur_cycles_s * SR)
    t = np.arange(n) / SR
    y = 0.25 * np.sin(2 * np.pi * 12000 * t) + 0.03 * np.sin(2 * np.pi * 15734 * t * 0 + 2 * np.pi * 12000 * 2 * t * 0)
    for h, a in ((1, 0.5), (2, 0.35), (3, 0.18), (4, 0.06), (6, 0.04)):
        y = y + a * 0.25 * np.sin(2 * np.pi * mains * h * t)
    rng = np.random.default_rng(5)
    buzz = A.bp(rng.standard_normal((n, 1)), 2000, 8000, order=2)[:, 0] * 0.02 * (1 + np.sin(2 * np.pi * 100 * t))
    y = (y + buzz)[:, None]
    return A.finalize(y, -6.0)


def render_clock_stuck(seed=0):
    """2.0 s loop body: tick, the escapement tries to advance (small rebound tick 70 ms later), stalls; twice."""
    n = int(2.0 * SR)
    x = np.zeros((n, 1))
    for k, t0 in enumerate((0.0, 1.0)):
        a = tick(k + seed, tock=False)
        b = tick(k + seed + 3, tock=True) * A.undb(-9)
        for seg, tt in ((a, t0 + 0.01), (b, t0 + 0.08), (b * 0.5, t0 + 0.115)):
            i = int(tt * SR)
            seg = seg[: n - i]
            x[i:i + len(seg)] += seg
    return A.finalize(x, -1.0)


def write_all():
    OUT.mkdir(parents=True, exist_ok=True)
    out = {}
    out["MOTIF_MUSICBOX"] = A.save(OUT / "MOTIF_MUSICBOX.wav", render_motif())
    out["INTERVAL_SIGNAL"] = A.save(OUT / "INTERVAL_SIGNAL_DRY.wav", render_interval_signal())
    out["MUS_06_SIXFORTY"] = A.save(OUT / "MUS_06_SIXFORTY.wav", render_sixforty())
    out["MOTIF_REF"] = A.save(OUT / "MOTIF_REF_72bpm.wav", render_style_reference())
    for nc in (1, 2, 3):
        for v in (1, 2):
            A.save(OUT / f"SFX_RADIO_CLICKS_{nc}_{v:02d}.wav", render_radio_clicks(nc, seed=nc * 10 + v))
    for v in (1, 2, 3):
        A.save(OUT / f"SFX_RADIO_KEYUP_{v:02d}.wav", render_keyup(v))
        A.save(OUT / f"SFX_RADIO_RELEASE_{v:02d}.wav", render_release(v))
    A.save(OUT / "SFX_TINNITUS_01.wav", render_tinnitus(4300, 3.0, 1))
    A.save(OUT / "SFX_TINNITUS_02.wav", render_tinnitus(5600, 3.0, 2))
    A.save(OUT / "SFX_LEADER_BEEP_01.wav", render_leader_beep(1, False))
    A.save(OUT / "SFX_LEADER_BEEP_02.wav", render_leader_beep(2, True))
    A.save(OUT / "SFX_CRT_WHINE_body.wav", render_crt_whine())
    A.save(OUT / "SFX_CLOCK_TICK_STUCK_body.wav", render_clock_stuck())
    return out


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "all":
        print(write_all())
