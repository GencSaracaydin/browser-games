"""Objective checks for generated music takes: tempo vs target, bar grid, motif pitch-class share, dynamic
envelope (where the generated outro starts), loudness, clipping; writes a PNG per take.

usage: python music_analyze.py <wav> --bpm 72 [--png out.png]
"""
from __future__ import annotations

import argparse
import json
import pathlib
import sys

import librosa
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import alib as A  # noqa: E402

PC = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
MOTIF_PC = [2, 5, 1]  # D, F, C#


def analyze(path, bpm, png=None):
    x, sr = A.load(path)
    m = A.mono(x)[:, 0]
    y = librosa.resample(m, orig_sr=sr, target_sr=22050)
    sr2 = 22050
    oenv = librosa.onset.onset_strength(y=y, sr=sr2, hop_length=512)
    tempo, beats = librosa.beat.beat_track(onset_envelope=oenv, sr=sr2, hop_length=512, start_bpm=bpm, tightness=200)
    tempo = float(np.atleast_1d(tempo)[0])
    bt = librosa.frames_to_time(beats, sr=sr2, hop_length=512)
    ibi = np.diff(bt)
    # tempo octave fix toward target
    cand = [tempo * k for k in (0.5, 2 / 3, 1, 1.5, 2)]
    tempo_fixed = min(cand, key=lambda t: abs(t - bpm))
    # global autocorrelation tempo (robust): peak of onset AC near the target beat period
    ac = librosa.autocorrelate(oenv, max_size=len(oenv) // 2)
    hop_t = 512 / sr2
    lag_target = 60 / bpm / hop_t
    lags = np.arange(len(ac))
    win = (lags > lag_target * 0.85) & (lags < lag_target * 1.15)
    lag = lags[win][np.argmax(ac[win])] if win.any() else lag_target
    # parabolic refine
    i = int(lag)
    if 1 <= i < len(ac) - 1:
        a, b, c = ac[i - 1], ac[i], ac[i + 1]
        lag = i + 0.5 * (a - c) / (a - 2 * b + c + 1e-12)
    tempo_ac = 60 / (lag * hop_t)
    # bar-lag autocorrelation (how loop-able the groove is)
    bar_lag = int(round(4 * 60 / bpm / hop_t))
    bar_ac = float(ac[bar_lag] / ac[0]) if bar_lag < len(ac) else float("nan")
    # chroma: motif pitch-class share
    chroma = librosa.feature.chroma_cqt(y=y, sr=sr2, hop_length=2048)
    prof = chroma.mean(axis=1)
    prof = prof / prof.sum()
    motif_share = float(prof[MOTIF_PC].sum())
    key_guess = PC[int(np.argmax(prof))]
    # RMS envelope (1 s) for intro/outro detection
    hop = sr
    rms = np.array([np.sqrt(np.mean(m[i:i + hop] ** 2) + 1e-12) for i in range(0, len(m) - hop // 2, hop // 2)])
    rdb = 20 * np.log10(rms + 1e-12)
    med = np.median(rdb)
    # outro start: first time after 40% of the file where level stays > 6 dB under median until the end
    outro = None
    for k in range(int(len(rdb) * 0.4), len(rdb)):
        if np.all(rdb[k:] < med - 6):
            outro = k * 0.5
            break
    meas = A.measure(x)
    r = dict(file=str(path), dur_s=meas["dur_s"], target_bpm=bpm, tempo_beat_track=round(tempo_fixed, 2),
             tempo_autocorr=round(float(tempo_ac), 2), ibi_cv=round(float(np.std(ibi) / np.mean(ibi)), 3) if len(ibi) > 4 else None,
             bar_autocorr=round(bar_ac, 3), motif_pc_share=round(motif_share, 3), chroma_peak=key_guess,
             chroma_profile={PC[k]: round(float(prof[k]), 3) for k in range(12)},
             outro_starts_s=outro, lufs_i=meas["lufs_i"], true_peak_dbtp=meas["true_peak_dbtp"], clipped=meas["clipped"],
             stereo_corr=meas["stereo_corr"], first_beat_s=round(float(bt[0]), 3) if len(bt) else None)
    if png:
        fig, ax = plt.subplots(3, 1, figsize=(12, 7))
        tt = np.arange(len(rdb)) * 0.5
        ax[0].plot(tt, rdb)
        ax[0].axhline(med - 6, color="r", lw=0.5)
        if outro:
            ax[0].axvline(outro, color="r")
        ax[0].set_title(f"{pathlib.Path(path).parent.name}/{pathlib.Path(path).name[:20]}  tempo {tempo_ac:.1f} (target {bpm})  "
                        f"motif share {motif_share:.2f}  key~{key_guess}  LUFS {meas['lufs_i']}", fontsize=8)
        ax[1].plot(librosa.times_like(oenv, sr=sr2, hop_length=512), oenv, lw=0.4)
        for b in bt[::4]:
            ax[1].axvline(b, color="g", lw=0.3)
        librosa.display.specshow(chroma, sr=sr2, hop_length=2048, x_axis="time", y_axis="chroma", ax=ax[2]) if hasattr(librosa, "display") else ax[2].imshow(chroma, aspect="auto", origin="lower")
        fig.tight_layout()
        fig.savefig(png, dpi=80)
        plt.close(fig)
    return r


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("wav")
    ap.add_argument("--bpm", type=float, required=True)
    ap.add_argument("--png")
    a = ap.parse_args()
    print(json.dumps(analyze(a.wav, a.bpm, a.png), indent=1))
