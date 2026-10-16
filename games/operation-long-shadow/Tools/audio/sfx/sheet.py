"""Contact sheets: waveform (dB envelope) + spectrogram for a list of audio files, one row each.

usage: python sheet.py out.png file1.wav file2.wav ... [--max-dur 3]
       (also importable: sheet(out_png, paths, labels=None, max_dur=None))
"""
from __future__ import annotations

import pathlib
import sys

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from scipy import signal

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import alib  # noqa: E402


def sheet(out_png, paths, labels=None, max_dur=None, title=None, cols=1):
    paths = [pathlib.Path(p) for p in paths]
    n = len(paths)
    rows = int(np.ceil(n / cols))
    fig, axes = plt.subplots(rows, 2 * cols, figsize=(7 * cols * 2 / 2 + 2, 1.55 * rows + 0.6), squeeze=False,
                             gridspec_kw={"width_ratios": [1, 1.4] * cols})
    for k, p in enumerate(paths):
        r, c = divmod(k, cols)
        ax_w, ax_s = axes[r][2 * c], axes[r][2 * c + 1]
        try:
            x, sr = alib.load(p)
        except Exception as e:  # noqa: BLE001
            ax_w.set_title(f"{p.name}: {e}", fontsize=6)
            continue
        if max_dur:
            x = x[: int(max_dur * sr)]
        m = alib.mono(x)[:, 0]
        t = np.arange(len(m)) / sr
        env = alib.envelope(x, 3, sr)
        ax_w.plot(t, alib.db(env), lw=0.5, color="k")
        ax_w.set_ylim(-80, 0)
        ax_w.set_xlim(0, max(t[-1] if len(t) else 1, 0.05))
        tp = alib.true_peak_db(x, sr)
        lab = labels[k] if labels else p.name
        ax_w.set_title(f"{lab}  {len(m)/sr:.2f}s  TP {tp:.1f}  Mmax {alib.momentary_max(x):.1f}", fontsize=6, loc="left")
        ax_w.tick_params(labelsize=5)
        nper = 512 if len(m) > 2048 else 128
        f, tt, S = signal.spectrogram(m, sr, nperseg=nper, noverlap=nper * 3 // 4)
        ax_s.pcolormesh(tt, f, 10 * np.log10(S + 1e-14), shading="auto", vmin=-130, vmax=-30, cmap="magma")
        ax_s.set_yscale("symlog", linthresh=500)
        ax_s.set_ylim(30, sr / 2)
        ax_s.tick_params(labelsize=5)
    for k in range(n, rows * cols):
        r, c = divmod(k, cols)
        axes[r][2 * c].axis("off")
        axes[r][2 * c + 1].axis("off")
    if title:
        fig.suptitle(title, fontsize=8)
    fig.tight_layout()
    pathlib.Path(out_png).parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out_png, dpi=90)
    plt.close(fig)
    return out_png


if __name__ == "__main__":
    args = sys.argv[1:]
    md = None
    if "--max-dur" in args:
        i = args.index("--max-dur")
        md = float(args[i + 1])
        del args[i:i + 2]
    sheet(args[0], args[1:], max_dur=md)
