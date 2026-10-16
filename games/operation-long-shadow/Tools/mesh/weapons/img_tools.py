#!/usr/bin/env python3
"""Image helpers for the WP03 weapon pipeline (numpy + PIL, Tools/.venv).

  img_tools.py inpaint in.png out.png x0,y0,x1,y1 [...]   diffuse-fill rectangles (removes tiny stamped glyphs)
  img_tools.py magmask full.png nomag.png out.json [debug.png]
        magazine mask = pixels that are weapon in full but background in nomag (largest blob);
        writes bbox, top edge, weapon bbox of both images (to check the edit kept the layout)
  img_tools.py silhouette in.png  -> prints bbox of the non-white object
  img_tools.py cropobj in.png out.png [margin]  crop to the object bbox + margin, pad to square, white bg
"""
import json, sys
import numpy as np
from PIL import Image, ImageFilter


def load(p):
    return np.asarray(Image.open(p).convert("RGB")).astype(np.float32)


def objmask(a, thr=40):
    # background is near white; object = clearly darker or more saturated than the local white
    bg = np.median(np.concatenate([a[:8].reshape(-1, 3), a[-8:].reshape(-1, 3), a[:, :8].reshape(-1, 3), a[:, -8:].reshape(-1, 3)]), axis=0)
    d = np.abs(a - bg).max(2)
    return d > thr


def bbox(m):
    ys, xs = np.nonzero(m)
    return [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())]


def largest_blob(m):
    try:
        from scipy import ndimage
        lab, n = ndimage.label(m)
        if n == 0:
            return m
        sizes = ndimage.sum(m, lab, range(1, n + 1))
        return lab == (int(np.argmax(sizes)) + 1)
    except ImportError:
        pass
    from collections import deque
    h, w = m.shape
    lab = np.zeros((h, w), np.int32)
    best, bestn, cur = 0, 0, 0
    ys, xs = np.nonzero(m)
    for y0, x0 in zip(ys, xs):
        if lab[y0, x0]:
            continue
        cur += 1
        q = deque([(y0, x0)])
        lab[y0, x0] = cur
        n = 0
        while q:
            y, x = q.popleft()
            n += 1
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < h and 0 <= xx < w and m[yy, xx] and not lab[yy, xx]:
                    lab[yy, xx] = cur
                    q.append((yy, xx))
        if n > bestn:
            best, bestn = cur, n
    return lab == best


def inpaint(src, dst, rects):
    a = load(src)
    m = np.zeros(a.shape[:2], bool)
    for r in rects:
        x0, y0, x1, y1 = map(int, r.split(","))
        m[y0:y1, x0:x1] = True
    out = a.copy()
    out[m] = 0
    known = ~m
    # iterative neighbour diffusion from the rectangle borders
    for _ in range(400):
        acc = np.zeros_like(out)
        cnt = np.zeros(a.shape[:2], np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            sh = np.roll(np.roll(out, dy, 0), dx, 1)
            k = np.roll(np.roll(known, dy, 0), dx, 1)
            acc += sh * k[..., None]
            cnt += k
        fill = m & (cnt > 0)
        out[fill] = acc[fill] / cnt[fill][:, None]
        known = known | fill
    # add a little of the surrounding grain back
    rng = np.random.default_rng(1)
    out[m] += rng.normal(0, 2.0, (m.sum(), 3))
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(dst)
    print("inpainted", len(rects), "rects ->", dst)


def magmask(full, nomag, out_json, dbg=None):
    a, b = load(full), load(nomag)
    if a.shape != b.shape:
        b = np.asarray(Image.open(nomag).convert("RGB").resize((a.shape[1], a.shape[0]), Image.LANCZOS)).astype(np.float32)
    ma, mb = objmask(a), objmask(b)
    # magazine = weapon pixels in full that changed strongly in nomag (a soft contact shadow changes little)
    diff = np.abs(a - b).max(2)
    mag = (diff > 40) & objmask(a, 45)
    # open (erode/dilate) to remove edge noise and thin shadow smears, keep the largest blob, fill holes
    im = Image.fromarray((mag * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(9)).filter(ImageFilter.MaxFilter(9))
    mag = largest_blob(np.asarray(im) > 0)
    try:
        from scipy import ndimage
        mag = ndimage.binary_fill_holes(mag)
    except ImportError:
        pass
    Image.fromarray((mag * 255).astype(np.uint8)).save(out_json.replace(".json", "_mask.png"))
    def blob_bbox(m):
        o = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
        return bbox(largest_blob(np.asarray(o) > 0))
    res = {"image_size": [a.shape[1], a.shape[0]], "weapon_bbox_full": blob_bbox(ma), "weapon_bbox_nomag": blob_bbox(mb),
           "mag_bbox": bbox(mag), "mag_area": int(mag.sum())}
    # top edge of the magazine (where it enters the receiver): the row band of the first 3 % of the mag height
    x0, y0, x1, y1 = res["mag_bbox"]
    ys, xs = np.nonzero(mag)
    top = ys < y0 + 0.03 * (y1 - y0)
    res["mag_top_center"] = [float(xs[top].mean()), float(y0)]
    # magazine axis by PCA (for the drop direction)
    c = np.stack([xs, ys], 1).astype(np.float64)
    c -= c.mean(0)
    w, v = np.linalg.eigh(c.T @ c)
    ax = v[:, -1]
    if ax[1] < 0:
        ax = -ax
    res["mag_axis_down_img"] = [float(ax[0]), float(ax[1])]
    res["mag_centroid"] = [float(xs.mean()), float(ys.mean())]
    json.dump(res, open(out_json, "w"), indent=1)
    print(json.dumps(res))
    if dbg:
        d = a.copy()
        d[mag] = d[mag] * 0.4 + np.array([255, 0, 0]) * 0.6
        d[mb & ~ma] = d[mb & ~ma] * 0.4 + np.array([0, 0, 255]) * 0.6
        Image.fromarray(d.astype(np.uint8)).save(dbg)


def cropobj(src, dst, margin=0.08):
    a = load(src)
    m = objmask(a)
    m = largest_blob(np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(9))) > 0)
    x0, y0, x1, y1 = bbox(m)
    w, h = x1 - x0, y1 - y0
    s = int(max(w, h) * (1 + 2 * margin))
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    im = Image.open(src).convert("RGB")
    canvas = Image.new("RGB", (s, s), tuple(int(v) for v in np.median(a[:8].reshape(-1, 3), 0)))
    canvas.paste(im.crop((cx - s // 2, cy - s // 2, cx - s // 2 + s, cy - s // 2 + s)), (0, 0))
    # pixels outside the source image were pasted black by crop(); repaint them white
    arr = np.asarray(canvas).copy()
    ox, oy = cx - s // 2, cy - s // 2
    yy, xx = np.mgrid[0:s, 0:s]
    outside = (xx + ox < 0) | (yy + oy < 0) | (xx + ox >= im.size[0]) | (yy + oy >= im.size[1])
    arr[outside] = 250
    out = Image.fromarray(arr)
    if s < 1024:
        out = out.resize((1024, 1024), Image.LANCZOS)
    out.save(dst)
    print("crop", [x0, y0, x1, y1], "->", dst, out.size)


def shadowless_mask(a, thr=40, core=100, grow=3):
    """Object mask without the soft contact shadow: the shadow under a studio shot differs from the white by at
    most ~90 (max channel), the gun body by >100. core = clearly-object pixels (holes filled), then the anti-aliased
    rim (diff > thr) within `grow` px of the core is added back."""
    from scipy import ndimage
    bg = np.median(np.concatenate([a[:8].reshape(-1, 3), a[-8:].reshape(-1, 3), a[:, :8].reshape(-1, 3), a[:, -8:].reshape(-1, 3)]), axis=0)
    d = np.abs(a - bg).max(2)
    c = largest_blob(ndimage.binary_opening(d > core, iterations=1))
    c = ndimage.binary_fill_holes(ndimage.binary_closing(c, iterations=2))
    m = ndimage.binary_dilation(c, iterations=grow) & (d > thr) | c
    return ndimage.binary_fill_holes(m)


def padref(src, dst_pad, dst_mask, thr=40, core=100):
    """Object mask of a white-background reference (contact shadow excluded) + a copy whose background is filled
    with the nearest object colour (so a slightly misregistered projection never pulls white onto the edges)."""
    from scipy import ndimage
    a = load(src)
    m = shadowless_mask(a, thr, core) if core > 0 else ndimage.binary_fill_holes(largest_blob(objmask(a, thr)))
    inner = ndimage.binary_erosion(m, iterations=3)  # edge pixels are anti-aliased against white
    idx = ndimage.distance_transform_edt(~inner, return_distances=False, return_indices=True)
    pad = a[idx[0], idx[1]]
    Image.fromarray(pad.astype(np.uint8)).save(dst_pad)
    Image.fromarray((m * 255).astype(np.uint8)).save(dst_mask)
    print("padref", bbox(m), "->", dst_pad, dst_mask)


def grid(src, dst, box, step=50, scale=1.0):
    """Crop box (x0,y0,x1,y1) and draw a labelled pixel grid in source-image coordinates."""
    from PIL import ImageDraw
    x0, y0, x1, y1 = map(int, box.split(","))
    im = Image.open(src).convert("RGB").crop((x0, y0, x1, y1))
    if scale != 1.0:
        im = im.resize((int(im.size[0] * scale), int(im.size[1] * scale)), Image.LANCZOS)
    d = ImageDraw.Draw(im)
    for gx in range((x0 // step + 1) * step, x1, step):
        X = (gx - x0) * scale
        d.line([(X, 0), (X, im.size[1])], fill=(255, 0, 0) if gx % (step * 2) == 0 else (255, 160, 160), width=1)
        d.text((X + 2, 2), str(gx), fill=(200, 0, 0))
    for gy in range((y0 // step + 1) * step, y1, step):
        Y = (gy - y0) * scale
        d.line([(0, Y), (im.size[0], Y)], fill=(0, 0, 255) if gy % (step * 2) == 0 else (160, 160, 255), width=1)
        d.text((2, Y + 2), str(gy), fill=(0, 0, 200))
    im.save(dst)


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "padref":
        padref(sys.argv[2], sys.argv[3], sys.argv[4], core=float(sys.argv[5]) if len(sys.argv) > 5 else 100)
    elif cmd == "grid":
        grid(sys.argv[2], sys.argv[3], sys.argv[4], int(sys.argv[5]) if len(sys.argv) > 5 else 50, float(sys.argv[6]) if len(sys.argv) > 6 else 1.0)
    if cmd == "inpaint":
        inpaint(sys.argv[2], sys.argv[3], sys.argv[4:])
    elif cmd == "magmask":
        magmask(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5] if len(sys.argv) > 5 else None)
    elif cmd == "endratio":
        core = float(sys.argv[3]) if len(sys.argv) > 3 else 100
        m = shadowless_mask(load(sys.argv[2]), 40, core)
        x0, y0, x1, y1 = bbox(m)
        L = x1 - x0
        def h(sl):
            ys = np.nonzero(m[:, sl].any(1))[0]
            return ys.max() - ys.min()
        print(round(h(slice(x0, int(x0 + 0.04 * L))) / h(slice(int(x1 - 0.04 * L), x1 + 1)), 3), [x0, y0, x1, y1])
    elif cmd == "silhouette":
        print(bbox(objmask(load(sys.argv[2]))))
    elif cmd == "cropobj":
        cropobj(sys.argv[2], sys.argv[3], float(sys.argv[4]) if len(sys.argv) > 4 else 0.08)
