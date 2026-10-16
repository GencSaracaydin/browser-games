#!/usr/bin/env python3
"""Contact sheet of waveform + spectrogram (+ 30 Hz envelope) for a set of WAVs, to eyeball takes and chains.
    python vo_preview.py out.png f1.wav f2.wav ... [--cols 3] [--fmax 12000]
"""
import argparse, pathlib
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from common import read_audio, envelope30, lufs, true_peak_db, lead_silence_ms


def sheet(out, files, cols=3, fmax=12000, title=None):
    n = len(files)
    rows = int(np.ceil(n / cols))
    fig, axes = plt.subplots(rows * 2, cols, figsize=(5.2 * cols, 3.1 * rows), squeeze=False,
                             gridspec_kw={"height_ratios": [1, 1.6] * rows})
    for i, f in enumerate(files):
        r, c = divmod(i, cols)
        x, sr = read_audio(f)
        t = np.arange(len(x)) / sr
        aw, asp = axes[2 * r][c], axes[2 * r + 1][c]
        aw.plot(t, x, lw=0.4, color="#335")
        env = envelope30(x, sr)
        aw.plot((np.arange(len(env)) + 0.5) / 30, env * 2.5, color="#d62", lw=1)
        aw.set_xlim(0, t[-1] if len(t) else 1); aw.set_ylim(-1, 1); aw.set_xticks([]); aw.set_yticks([])
        name = pathlib.Path(f).stem
        aw.set_title(f"{name}  {len(x)/sr:.2f}s  {lufs(x, sr):.1f} LUFS  TP {true_peak_db(x, sr):.1f}  lead {lead_silence_ms(x, sr):.0f}ms",
                     fontsize=7)
        asp.specgram(x + 1e-9, NFFT=1024, Fs=sr, noverlap=768, cmap="magma", vmin=-130, vmax=-20)
        asp.set_ylim(0, min(fmax, sr / 2)); asp.tick_params(labelsize=6)
    for j in range(n, rows * cols):
        r, c = divmod(j, cols)
        axes[2 * r][c].axis("off"); axes[2 * r + 1][c].axis("off")
    if title:
        fig.suptitle(title, fontsize=9)
    fig.tight_layout()
    pathlib.Path(out).parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out, dpi=80)
    plt.close(fig)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("files", nargs="+")
    ap.add_argument("--cols", type=int, default=3); ap.add_argument("--fmax", type=int, default=12000)
    ap.add_argument("--title")
    a = ap.parse_args()
    sheet(a.out, a.files, a.cols, a.fmax, a.title)
