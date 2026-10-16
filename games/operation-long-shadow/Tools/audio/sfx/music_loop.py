"""Bar-line loop cutting for generated music (TECH_PLAN §8.7-6, §3.12).

1. bar_grid(): tempo from onset autocorrelation near the target BPM, downbeat phase by folding the onset envelope,
   beat times snapped to tracked beats (+-40 ms) so the loop length matches the real musical length.
2. choose_loop(): every (start bar, N bars) inside [region] before the generated outro; scored by how alike the
   crossfade windows are (mel spectrum cosine + level step) and by groove stability; best one wins.
3. render(): equal-power crossfade of the body's last X into the audio before the start, then the Web layout
   [lead-in][body][tail-out] (lead-in = the real audio before the loop start, >= 2048 samples; tail-out = body head).

usage (normally driven by build_music.py): python music_loop.py in.wav out.wav --bpm 110 --bars 16 [--region 0 80]
"""
from __future__ import annotations

import argparse
import json
import pathlib
import sys

import librosa
import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import alib as A  # noqa: E402

SR = A.SR


def onset_env(x, sr=SR, hop=256):
    m = A.mono(x)[:, 0]
    y = librosa.resample(m, orig_sr=sr, target_sr=24000)
    o = librosa.onset.onset_strength(y=y, sr=24000, hop_length=hop)
    return o, hop / 24000


def bar_grid(x, bpm, beats_per_bar=4, region=None, sr=SR):
    o, ht = onset_env(x, sr)
    ac = librosa.autocorrelate(o, max_size=len(o) // 2)
    lag_t = 60 / bpm / ht
    lags = np.arange(len(ac))
    w = (lags > lag_t * 0.9) & (lags < lag_t * 1.1)
    i = int(lags[w][np.argmax(ac[w])])
    a, b, c = ac[i - 1], ac[i], ac[i + 1]
    lag = i + 0.5 * (a - c) / (a - 2 * b + c + 1e-12)
    beat = lag * ht
    # refine with longer lags (bar and 4-bar) for precision
    for mult in (beats_per_bar, beats_per_bar * 4):
        L = beat * mult / ht
        lo, hi = int(L * 0.98), int(L * 1.02) + 1
        if hi < len(ac):
            j = lo + int(np.argmax(ac[lo:hi]))
            if 1 <= j < len(ac) - 1:
                a, b, c = ac[j - 1], ac[j], ac[j + 1]
                jj = j + 0.5 * (a - c) / (a - 2 * b + c + 1e-12)
                beat = jj * ht / mult
    bar = beat * beats_per_bar
    t = np.arange(len(o)) * ht
    r0, r1 = region if region else (0, t[-1])
    sel = (t >= r0) & (t <= r1)
    # downbeat phase: fold onset strength (weighted: beat 1 strongest) over candidate phases
    best, phase = -1, 0.0
    for ph in np.arange(0, bar, ht):
        score = 0.0
        for k in range(beats_per_bar):
            idx = np.round((ph + k * beat + np.arange(0, t[-1], bar)) / ht).astype(int)
            idx = idx[(idx < len(o))]
            idx = idx[sel[idx]]
            score += o[idx].sum() * (2.0 if k == 0 else 1.0)
        if score > best:
            best, phase = score, ph
    # tracked beats for snapping
    y24 = librosa.resample(A.mono(x)[:, 0], orig_sr=sr, target_sr=24000)
    _, tb = librosa.beat.beat_track(y=y24, sr=24000, hop_length=256, start_bpm=60 / beat, tightness=400, units="time")
    downbeats = []
    tt = phase
    while tt < len(x) / sr:
        if len(tb):
            k = int(np.argmin(np.abs(tb - tt)))
            snapped = tb[k] if abs(tb[k] - tt) < 0.04 else tt
        else:
            snapped = tt
        downbeats.append(float(snapped))
        tt += bar
    return dict(beat_s=float(beat), bar_s=float(bar), bpm=float(60 / beat), phase_s=float(phase), downbeats=downbeats)


def _mel(x, s, e, sr=SR):
    m = A.mono(x[max(0, s):e])[:, 0]
    if len(m) < 2048:
        m = np.pad(m, (0, 2048 - len(m)))
    S = librosa.feature.melspectrogram(y=m, sr=sr, n_fft=2048, hop_length=512, n_mels=48)
    return np.log(S + 1e-9)


def choose_loop(x, grid, bars, region, xfade_s=0.3, sr=SR, exact_len_s=None):
    db = grid["downbeats"]
    X = int(xfade_s * sr)
    cands = []
    for i in range(len(db)):
        j = i + bars
        if j >= len(db):
            break
        s_t, e_t = db[i], db[j]
        if s_t < region[0] or e_t > region[1] or s_t * sr < max(X, A.LEAD) + 1:
            continue
        s, e = int(round(s_t * sr)), int(round(e_t * sr))
        if exact_len_s:
            e = s + int(round(exact_len_s * sr))
        # the body's last X is blended with the X before s: they should be alike
        ma, mb = _mel(x, e - X, e), _mel(x, s - X, s)
        n = min(ma.shape[1], mb.shape[1])
        va, vb = ma[:, :n].ravel(), mb[:, :n].ravel()
        cos = float(np.dot(va - va.mean(), vb - vb.mean()) / (np.linalg.norm(va - va.mean()) * np.linalg.norm(vb - vb.mean()) + 1e-9))
        # and what follows e (original) should be alike what follows s (the loop start)
        mc, md = _mel(x, e, e + X), _mel(x, s, s + X)
        n2 = min(mc.shape[1], md.shape[1])
        vc, vd = mc[:, :n2].ravel(), md[:, :n2].ravel()
        cos2 = float(np.dot(vc - vc.mean(), vd - vd.mean()) / (np.linalg.norm(vc - vc.mean()) * np.linalg.norm(vd - vd.mean()) + 1e-9))
        la = A.db(np.sqrt(np.mean(x[e - X:e] ** 2)) + 1e-9)
        lb = A.db(np.sqrt(np.mean(x[s - X:s] ** 2)) + 1e-9)
        score = cos + 0.5 * cos2 - 0.08 * abs(la - lb)
        cands.append(dict(start_bar=i, start=s, end=e, score=round(score, 4), cos=round(cos, 3), cos_after=round(cos2, 3),
                          level_step_db=round(float(abs(la - lb)), 2)))
    if not cands:
        raise ValueError("no loop candidate in region")
    cands.sort(key=lambda c: -c["score"])
    return cands[0], cands[:5]


def render(x, s, e, xfade_s=0.3, keep_intro=False, sr=SR):
    X = int(xfade_s * sr)
    body = A.make_loop_body(x, s, e, X)
    if keep_intro:
        lead = x[:s]
        lead_n = s
    else:
        lead = x[s - A.LEAD:s]
        lead_n = A.LEAD
    out = np.vstack([lead, body, body[:A.LEAD]])
    return out, lead_n, len(body), body


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("dst")
    ap.add_argument("--bpm", type=float, required=True)
    ap.add_argument("--bars", type=int, required=True)
    ap.add_argument("--region", type=float, nargs=2)
    a = ap.parse_args()
    x, sr = A.load(a.src)
    g = bar_grid(x, a.bpm)
    best, top = choose_loop(x, g, a.bars, a.region or (0, len(x) / sr))
    out, lead, n, body = render(x, best["start"], best["end"])
    A.save(a.dst, out)
    print(json.dumps(dict(grid={k: v for k, v in g.items() if k != "downbeats"}, best=best, seam=A.seam_metrics(body)), indent=1))


if __name__ == "__main__":
    main()
