#!/usr/bin/env python3
"""WP09 offline video finishing for the CRT clips (fal Kling v3 sources -> period look -> 512x384 H.264).

  Tools/.venv/bin/python Tools/images/wp09_video.py harrou   # VID_HARROU_8MM: Super 8 look on kling_t1.mp4
  Tools/.venv/bin/python Tools/images/wp09_video.py guncam   # VID_GUNCAM: RPG near-miss + cabin muzzle flash composited
                                                             # onto the clean flight plate kling_t2.mp4, video-tube look
Writes RawAssets/wp09/ready/Assets/StreamingAssets/video/VID_*.mp4 (+ a review contact sheet in RawAssets/wp09/_review/)
and RawAssets/wp09/<ID>/final_preview_*.png frames. ffmpeg comes from the imageio-ffmpeg wheel in Tools/.venv.
Deterministic (seeded). Everything is monochrome/colour in the file itself; the in-game CRT shader adds curvature/scanlines.
"""
import json, math, pathlib, subprocess, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import imageio_ffmpeg

ROOT = pathlib.Path(__file__).resolve().parents[2]
RAW = ROOT / "RawAssets/wp09"
OUTD = RAW / "ready/Assets/StreamingAssets/video"
FF = imageio_ffmpeg.get_ffmpeg_exe()
W, H = 512, 384


def read_frames(path, fps=None):
    vf = f"scale={W}:{H}:flags=lanczos" + (f",fps={fps}" if fps else "")
    p = subprocess.run([FF, "-loglevel", "error", "-i", str(path), "-vf", vf, "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
                       capture_output=True, check=True)
    a = np.frombuffer(p.stdout, np.uint8).reshape(-1, H, W, 3)
    return a.astype(np.float32) / 255.0


def write_mp4(frames, path, fps, kbps):
    path.parent.mkdir(parents=True, exist_ok=True)
    cmd = [FF, "-loglevel", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(fps), "-i", "-",
           "-c:v", "libx264", "-profile:v", "main", "-pix_fmt", "yuv420p", "-b:v", f"{kbps}k", "-maxrate", f"{int(kbps * 1.4)}k",
           "-bufsize", f"{kbps * 2}k", "-preset", "slow", "-g", str(fps * 2), "-movflags", "+faststart", "-an", str(path)]
    p = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for f in frames:
        p.stdin.write((np.clip(f, 0, 1) * 255 + 0.5).astype(np.uint8).tobytes())
    p.stdin.close(); p.wait()
    return path.stat().st_size


def shift(img, dx, dy, rot_deg=0.0):
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    im = im.rotate(rot_deg, resample=Image.BILINEAR, translate=(dx, dy), fillcolor=(0, 0, 0))
    return np.asarray(im, np.float32) / 255


def blur(img, r):
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r)), np.float32) / 255


def sheet(frames, idx, out, cols=6):
    th = [Image.fromarray((np.clip(frames[i], 0, 1) * 255).astype(np.uint8)).resize((256, 192)) for i in idx]
    rows = (len(th) + cols - 1) // cols
    s = Image.new("RGB", (cols * 258, rows * 194), (20, 20, 20))
    for k, t in enumerate(th):
        s.paste(t, ((k % cols) * 258, (k // cols) * 194))
    s.save(out)


# ---------------------------------------------------------------- Harrou 8 mm
def harrou():
    rng = np.random.default_rng(84)
    src = read_frames(RAW / "VID_HARROU_8MM/kling_t1.mp4", fps=18)      # 18 fps step = Super 8 silent speed
    n = len(src)
    weave = np.cumsum(rng.normal(0, 0.35, (n, 2)), 0); weave -= np.linspace(0, 1, n)[:, None] * weave[-1]   # bounded drift
    out = []
    scratches = [(rng.integers(40, W - 40), rng.integers(0, n - 30), rng.integers(10, 40)) for _ in range(5)]
    for i, f in enumerate(src):
        f = f.copy()
        # faded Kodachrome: lifted blacks, magenta-warm cast, slightly crushed greens
        f = 0.05 + 0.92 * f
        f = f * np.array([1.04, 0.95, 0.98], np.float32)
        f = f * (1 + rng.normal(0, 0.025))                                # exposure flicker
        # gate weave (+ the odd frame-line jump)
        dy = weave[i, 1] + (rng.integers(-3, 4) if rng.random() < 0.03 else 0)
        f = shift(f, weave[i, 0] * 0.6, dy)
        # dust (dark specks and pale hairs) for one frame each
        im = Image.fromarray((np.clip(f, 0, 1) * 255).astype(np.uint8)); d = ImageDraw.Draw(im)
        for _ in range(rng.integers(2, 9)):
            x, y = rng.integers(0, W), rng.integers(0, H); r = rng.uniform(0.6, 2.2)
            d.ellipse([x - r, y - r, x + r, y + r], fill=(18, 14, 12))
        if rng.random() < 0.25:
            x, y = rng.integers(0, W), rng.integers(0, H)
            pts = [(x + rng.normal(0, 6) * k, y + rng.normal(0, 6) * k) for k in range(5)]
            d.line(pts, fill=(235, 228, 210), width=1)
        for (sx, s0, sl) in scratches:                                   # persistent vertical emulsion scratches
            if s0 <= i < s0 + sl:
                x = sx + int(3 * math.sin(i * 0.4))
                d.line([(x, 0), (x + rng.integers(-2, 3), H)], fill=(225, 220, 205), width=1)
        f = np.asarray(im, np.float32) / 255
        # grain + vignette + halation
        f = f + rng.normal(0, 0.035, (H, W, 1)).astype(np.float32) * (0.4 + 2.0 * f.mean(2, keepdims=True) * (1 - f.mean(2, keepdims=True)))
        y, x = np.mgrid[0:H, 0:W]
        v = 1 - 0.28 * (((x - W / 2) / (W / 2)) ** 2 + ((y - H / 2) / (H / 2)) ** 2) ** 1.3
        f = f * v[..., None]
        out.append(np.clip(f, 0, 1))
    # exactly 8.0 s at 18 fps
    out = out[:144] + [out[-1]] * max(0, 144 - len(out))
    size = write_mp4(out, OUTD / "VID_Harrou8mm.mp4", 18, 1100)
    sheet(out, list(range(0, 144, 12)), RAW / "_review/VID_HARROU_8MM_final.png")
    print("harrou", len(out), "frames", size, "bytes")
    return size


# ---------------------------------------------------------------- Gun camera
def smoke_blob(acc, x, y, r, a):
    h, w = acc.shape
    x0, x1 = max(0, int(x - 3 * r)), min(w, int(x + 3 * r) + 1)
    y0, y1 = max(0, int(y - 3 * r)), min(h, int(y + 3 * r) + 1)
    if x0 >= x1 or y0 >= y1:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1]
    acc[y0:y1, x0:x1] += a * np.exp(-((xx - x) ** 2 + (yy - y) ** 2) / (2 * r * r))


def guncam():
    rng = np.random.default_rng(1984)
    fps = 24
    src = read_frames(RAW / "VID_GUNCAM/kling_t2.mp4")
    n = min(len(src), 12 * fps)
    src = src[:n]
    lum = [(f * np.array([0.3, 0.59, 0.11], np.float32)).sum(2) for f in src]
    # RPG: launched from the town far below-left, rises and passes just under the skid toward the lower-right edge
    t0, t1 = int(4.2 * fps), int(4.62 * fps)                  # ~0.42 s from first sight to the frame edge
    p0, p1 = np.array([0.30 * W, 0.92 * H]), np.array([1.08 * W, 0.80 * H])
    ctrl = np.array([0.58 * W, 0.70 * H])                     # passes under the skid tube (y ~ 0.62-0.75 H)
    def rpos(u):
        return (1 - u) ** 2 * p0 + 2 * (1 - u) * u * ctrl + u * u * p1
    trail = []                                                  # (frame_born, x, y, radius)
    flash_f = int(9.1 * fps)
    cabin = np.array([0.47 * W, 0.34 * H])
    out = []
    persist = np.zeros((H, W), np.float32)
    for i in range(n):
        L = lum[i].copy()
        # rocket and its trail
        if t0 <= i <= t1:
            u0 = (i - t0) / (t1 - t0)
            for s in np.linspace(max(0, u0 - 1 / (t1 - t0)), u0, 28):
                q = rpos(s); r0 = 2.0 + 8 * s ** 1.3
                trail.append((i, q[0] + rng.normal(0, r0 * 0.25), q[1] + rng.normal(0, r0 * 0.25), r0))
        smoke = np.zeros((H, W), np.float32)
        drift = np.array([3.2, -0.4])                          # the heli flies left: the trail streams right and lifts slightly
        for (b, x, y, r) in trail:
            age = (i - b) / fps
            if age < 0 or age > 2.2:
                continue
            xx, yy = x + drift[0] * (i - b), y + drift[1] * (i - b)
            smoke_blob(smoke, xx, yy, r * (1 + 3.0 * age), 0.16 * math.exp(-age * 1.3) / (1 + 2.0 * age))
        smoke = blur(np.repeat(np.clip(smoke, 0, 1)[..., None], 3, 2), 1.5)[..., 0] if smoke.max() > 0 else smoke
        L = L + (0.92 - L) * np.clip(smoke * 1.3, 0, 0.9)
        if t0 <= i <= t1:                                       # motor flare at the head
            q = rpos((i - t0) / (t1 - t0))
            flare = np.zeros((H, W), np.float32); smoke_blob(flare, q[0], q[1], 3 + 6 * (i - t0) / (t1 - t0), 1.6)
            L = np.maximum(L, np.clip(flare, 0, 1.0))
        # muzzle flash inside the dark cabin (2 frames + a fading phosphor smear)
        if i in (flash_f, flash_f + 1):
            fl = np.zeros((H, W), np.float32)
            smoke_blob(fl, cabin[0], cabin[1], 26 if i == flash_f else 14, 2.5 if i == flash_f else 1.2)
            smoke_blob(fl, cabin[0] - 30, cabin[1] + 4, 60, 0.45 if i == flash_f else 0.15)   # light on the cabin interior
            L = np.clip(L + fl + (0.18 if i == flash_f else 0.05), 0, 1)
        # video-tube phosphor persistence on highlights
        persist = np.maximum(persist * 0.55, np.clip(L - 0.75, 0, 1))
        L = np.clip(L + persist * 0.6, 0, 1)
        f = np.repeat(L[..., None], 3, 2)
        # lurch after the near miss: impulse shake decaying over ~1.2 s, plus constant airframe buzz
        k = i - t1
        amp = 7.0 * math.exp(-k / (0.35 * fps)) if k >= 0 else 0.0
        dx = rng.normal(0, 0.6) + amp * math.sin(k * 1.9); dy = rng.normal(0, 0.6) + amp * 0.8 * math.cos(k * 2.3)
        rot = (2.0 * math.exp(-k / (0.4 * fps)) * math.sin(k * 0.9)) if k >= 0 else 0.0
        f = shift(f, dx, dy, rot)
        # tube look: soft, bloom, noise, faint line structure, rolling hum bar, vignette
        f = 0.7 * f + 0.3 * blur(f, 1.2)
        f = f + 0.25 * blur(np.clip(f - 0.7, 0, 1), 6)
        g = f[..., :1]
        g = g + rng.normal(0, 0.045, (H, W, 1)).astype(np.float32)
        g = g * (0.94 + 0.06 * (np.arange(H)[:, None, None] % 2))
        hum = 0.04 * np.exp(-(((np.arange(H) - (i * 3.1) % (H + 120) + 60) / 40.0) ** 2))[:, None, None]
        g = g + hum
        y, x = np.mgrid[0:H, 0:W]
        g = g * (1 - 0.35 * (((x - W / 2) / (W / 2)) ** 2 + ((y - H / 2) / (H / 2)) ** 2) ** 1.5)[..., None]
        g = np.clip(g, 0, 1) ** 1.1
        out.append(np.repeat(g, 3, 2))
    size = write_mp4(out, OUTD / "VID_GunCam.mp4", fps, 950)
    sheet(out, [0, 48, 96, 100, 103, 106, 108, 111, 115, 130, 150, 200, 217, 218, 219, 222, 250, 287], RAW / "_review/VID_GUNCAM_final.png")
    # green-phosphor reference grade (not shipped; SG_GunCam does this in game)
    gr = [f * np.array([0.32, 1.0, 0.45], np.float32) for f in out[::24]]
    sheet(gr, list(range(len(gr))), RAW / "VID_GUNCAM/green_reference.png")
    print("guncam", len(out), "frames", size, "bytes")
    return size


if __name__ == "__main__":
    {"harrou": harrou, "guncam": guncam}[sys.argv[1]]()
