#!/usr/bin/env python3
"""1984 retexture of Microsoft Rocketbox avatars (WP05).

  Tools/.venv/bin/python Tools/characters/retexture_rocketbox.py <Variant> [<Variant> ...] [--size 2048]
  Tools/.venv/bin/python Tools/characters/retexture_rocketbox.py --list

Per variant (see rb_configs.py) and per material:
  * rasterises the mesh faces into the UV atlas (rb_dump.py output) -> island, class, 3D-position maps
  * paints out flags / name tapes / rank / unit patches / modern buckles (cv2 inpaint on colour + normal)
  * removes the ACU camo: cloth classes are refilled with a period fabric tile multiplied by a shading
    term rebuilt from the cloth normal map (slope + cavity) and the heavily blurred original luminance
    (large-scale AO), so folds, seams and pockets survive but the digital pattern does not
  * recolours boots/gloves/hair/suits (keeps the original luminance detail), keeps skin
  * adds height-driven dust (hems, knees, boots) and sweat darkening (armpits, back)
  * flattens modern MOLLE webbing in the normal map where asked
  * writes BC (sRGB), N (OpenGL normal), M (R metal, G AO, A smoothness) masters at 2048 with edge padding
Outputs: RawAssets/wp05/_work/<Variant>/master/T_<Variant>_<Part>_{BC,N,M}.png (+ preview PNG)
The export step (rb_export.py / make_ready.py) resizes masters to the per-asset budget.
"""
import argparse, json, pathlib, sys
import numpy as np
import cv2
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import rb_configs  # noqa: E402
from rb_islands import load_dump, part_of  # noqa: E402

ROOT = HERE.parents[1]
WORK = ROOT / "RawAssets/wp05/_work"
FAB = WORK / "fabrics"


def tex_dir(avatar):
    for base in (ROOT / "RawAssets/wp05/rocketbox" / avatar / "Textures", ROOT / "RawAssets/cc0/characters/rocketbox" / avatar / "Textures"):
        if base.exists():
            return base
    raise FileNotFoundError(avatar)


def find_tex(avatar, mat, kind):
    """kind: color|normal|specular. Rocketbox names: <prefix>_<part>_<kind>[_acu].tga"""
    d = tex_dir(avatar)
    c = sorted(d.glob(f"{mat}_{kind}*.tga"))
    return c[0] if c else None


def load_rgb(p, size):
    im = Image.open(p)
    a = np.asarray(im.convert("RGBA" if im.mode in ("RGBA", "LA") else "RGB").resize((size, size), Image.LANCZOS)).astype(np.float32) / 255
    return a


def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def lum(c):
    return c[..., 0] * 0.2126 + c[..., 1] * 0.7152 + c[..., 2] * 0.0722


def nblur(a, mask, sigma):
    """normalised gaussian blur restricted to mask"""
    m = mask.astype(np.float32)
    if a.ndim == 3:
        num = np.stack([ndi.gaussian_filter(a[..., i] * m, sigma) for i in range(a.shape[2])], -1)
        den = ndi.gaussian_filter(m, sigma)[..., None]
    else:
        num = ndi.gaussian_filter(a * m, sigma)
        den = ndi.gaussian_filter(m, sigma)
    return num / np.maximum(den, 1e-4)


def raster_faces(dump, mat, size):
    W = H = size
    img = Image.new("I", (W, H), -1)
    dr = ImageDraw.Draw(img)
    mi = dump["materials"].index(mat)
    idx = []
    for k, f in enumerate(dump["faces"]):
        if f["m"] != mi:
            continue
        poly = [(u * W - 0.5, (1 - v) * H - 0.5) for u, v in f["uv"]]
        dr.polygon(poly, fill=k, outline=k)
        idx.append(k)
    return np.asarray(img).astype(np.int32)


def tile_sample(tile, size, tile_px, rot=0.0, offset=(0, 0)):
    """tile a seamless texture over a size x size atlas; tile_px atlas pixels per tile repeat"""
    th, tw = tile.shape[:2]
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    if rot:
        c, s = np.cos(np.radians(rot)), np.sin(np.radians(rot))
        xx, yy = c * xx - s * yy, s * xx + c * yy
    u = ((xx + offset[0]) / tile_px * tw) % tw
    v = ((yy + offset[1]) / tile_px * th) % th
    return cv2.remap(tile, u, v, cv2.INTER_LINEAR, borderMode=cv2.BORDER_WRAP)


def value_noise(size, scale, seed, octaves=3):
    rng = np.random.default_rng(seed)
    out = np.zeros((size, size), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        n = max(2, int(size / scale * 2 ** o))
        g = rng.random((n, n)).astype(np.float32)
        out += amp * cv2.resize(g, (size, size), interpolation=cv2.INTER_CUBIC)
        tot += amp
        amp *= 0.5
    return out / tot


def shading_from_normal(n, mask, slope_pow=1.6, cav_k=6.0, cav_w=0.55, ridge_w=0.12, blur=1.2):
    nz = np.clip(n[..., 2], 0.05, 1)
    s1 = nz ** slope_pow
    nx = ndi.gaussian_filter(n[..., 0], blur)
    ny = ndi.gaussian_filter(n[..., 1], blur)
    curv = np.gradient(nx, axis=1) - np.gradient(ny, axis=0)  # OpenGL normal: +green = up = -image y
    curv = ndi.gaussian_filter(curv, 1.0) * n.shape[0] / 1024.0
    cav = np.clip(-curv * cav_k, 0, 1)
    ridge = np.clip(curv * cav_k, 0, 1)
    sh = s1 * (1 - cav_w * cav) * (1 + ridge_w * ridge)
    return np.where(mask, sh, 1.0)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0 + 1e-9), 0, 1)
    return t * t * (3 - 2 * t)


def pad_edges(a, valid, iters=None):
    """fill invalid pixels with the nearest valid pixel (avoids seams at mip levels)"""
    if valid.all():
        return a
    _, (iy, ix) = ndi.distance_transform_edt(~valid, return_indices=True)
    return a[iy, ix]


def build_maps(cfg_mat, dump, mat, size, faces_map, src_col=None):
    faces = dump["faces"]
    valid = faces_map >= 0
    fidx = np.where(valid, faces_map, 0)
    isl = np.array([f["isl"] for f in faces], np.int32)[fidx]
    isl[~valid] = -1
    parts = np.array([part_of(f["b"]) for f in faces])[fidx]
    pos = np.array([f["c"] for f in faces], np.float32)[fidx]
    nrm = np.array([f["n"] for f in faces], np.float32)[fidx]
    # class per pixel
    cls = np.full(faces_map.shape, "", dtype=object)
    default = cfg_mat.get("default_by_part", {})
    for p, c in default.items():
        if p == "*":
            cls[valid] = c
    for p, c in default.items():
        if p != "*":
            cls[(parts == p) & valid] = c
    if cfg_mat.get("island_vote", True):
        # one class per UV island (majority of its pixels): removes the per-triangle "teeth" where an island's faces are
        # weighted to two body parts (shirt tails over the hips, boot tops over the calves)
        ids_v = isl[valid]
        cls_v = cls[valid]
        out_v = cls_v.copy()
        order = np.argsort(ids_v, kind="stable")
        ids_s, cls_s = ids_v[order], cls_v[order]
        starts = np.flatnonzero(np.r_[True, ids_s[1:] != ids_s[:-1]])
        ends = np.r_[starts[1:], len(ids_s)]
        for a0, a1 in zip(starts, ends):
            vals, cnt = np.unique(cls_s[a0:a1].astype(str), return_counts=True)
            if len(vals) > 1:
                out_v[order[a0:a1]] = vals[np.argmax(cnt)]
        cls[valid] = out_v
    for c, ids in cfg_mat.get("classes", {}).items():
        cls[np.isin(isl, ids) & valid] = c
    for rule in cfg_mat.get("rect_classes", []):  # [cls, x0,y0,x1,y1] at 2048 px
        c, x0, y0, x1, y1 = rule
        s = size / 2048
        sub = np.zeros_like(valid)
        sub[int(y0 * s):int(y1 * s), int(x0 * s):int(x1 * s)] = True
        cls[sub & valid] = c
    # colour rules on the (blurred) source colour, restricted to a part/class: [{"class", "within": [cls..], hue:[a,b], sat:[a,b], val:[a,b]}]
    if cfg_mat.get("color_rules") and src_col is not None:
        hsv = cv2.cvtColor((cv2.GaussianBlur(src_col, (0, 0), 3 * size / 2048) * 255).astype(np.uint8), cv2.COLOR_RGB2HSV).astype(np.float32)
        H, S, V = hsv[..., 0] * 2, hsv[..., 1] / 255, hsv[..., 2] / 255
        for r in cfg_mat["color_rules"]:
            m = valid.copy()
            if r.get("within"):
                m &= np.isin(cls, r["within"])
            if "hue" in r:
                a, b = r["hue"]
                m &= ((H >= a) & (H <= b)) if a <= b else ((H >= a) | (H <= b))
            if "sat" in r:
                m &= (S >= r["sat"][0]) & (S <= r["sat"][1])
            if "val" in r:
                m &= (V >= r["val"][0]) & (V <= r["val"][1])
            m = ndi.binary_opening(m, iterations=2) if r.get("open", True) else m
            cls[m] = r["class"]
    # smooth the per-face 3D position inside islands
    posb = np.stack([nblur(pos[..., i], valid, 3.0 * size / 2048) for i in range(3)], -1)
    return valid, isl, cls, posb, nrm


def process_material(variant, avatar, mat, cfg_mat, size, dump, outdir, part_name, seed):
    faces_map = raster_faces(dump, mat, size)
    src_c = find_tex(avatar, mat, "color")
    col_full = load_rgb(src_c, size)
    valid, isl, cls, pos, fn = build_maps(cfg_mat, dump, mat, size, faces_map, col_full[..., :3])
    alpha = col_full[..., 3] if col_full.shape[2] == 4 else None
    src_n = find_tex(avatar, mat, "normal")
    src_s = find_tex(avatar, mat, "specular")
    col = col_full[..., :3].copy()
    nrm = load_rgb(src_n, size)[..., :3] if src_n else np.dstack([np.full((size, size), .5), np.full((size, size), .5), np.ones((size, size))]).astype(np.float32)
    spec = lum(load_rgb(src_s, size)[..., :3]) if src_s else np.full((size, size), 0.2, np.float32)
    s = size / 2048.0
    # ---- paint-outs (flags, tapes, patches, buckles): inpaint colour and normal
    po = np.zeros((size, size), np.uint8)
    for (x0, y0, x1, y1) in cfg_mat.get("paintout", []):
        po[int(y0 * s):int(y1 * s), int(x0 * s):int(x1 * s)] = 255
    for poly in cfg_mat.get("paintout_poly", []):
        cv2.fillPoly(po, [np.array([[int(x * s), int(y * s)] for x, y in poly], np.int32)], 255)
    if po.any():
        po = cv2.dilate(po, np.ones((5, 5), np.uint8))
        c8 = (col * 255).astype(np.uint8)
        n8 = (nrm * 255).astype(np.uint8)
        col = cv2.inpaint(c8, po, 9, cv2.INPAINT_TELEA).astype(np.float32) / 255
        nrm = cv2.inpaint(n8, po, 9, cv2.INPAINT_TELEA).astype(np.float32) / 255
        # flatten the normal inside the patch so the patch outline disappears
        nb = cv2.GaussianBlur(nrm, (0, 0), 12 * s)
        w = cv2.GaussianBlur(po.astype(np.float32) / 255, (0, 0), 3)[..., None]
        nrm = nrm * (1 - w) + nb * w
    nv = nrm * 2 - 1
    nv /= np.linalg.norm(nv, axis=2, keepdims=True) + 1e-6
    lin = srgb_to_lin(col)
    L = lum(lin)
    out_lin = lin.copy()
    out_n = nv.copy()
    smooth = np.full((size, size), 0.3, np.float32)
    metal = np.zeros((size, size), np.float32)
    ao = np.ones((size, size), np.float32)
    fills = cfg_mat.get("fills", {})
    rng_seed = seed
    for cname, f in fills.items():
        m = (cls == cname) & valid
        if not m.any():
            continue
        rng_seed += 7
        mode = f.get("mode", "fabric")
        # ---- normal flatten (MOLLE webbing etc.)
        if f.get("flatten", 0) > 0:
            fb = cv2.GaussianBlur(nv, (0, 0), f.get("flatten_sigma", 7) * s)
            fb /= np.linalg.norm(fb, axis=2, keepdims=True) + 1e-6
            t = f["flatten"]
            out_n[m] = (nv[m] * (1 - t) + fb[m] * t)
        nn = out_n / (np.linalg.norm(out_n, axis=2, keepdims=True) + 1e-6)
        if mode == "keep":
            pass
        elif mode in ("fabric", "skin_tile"):
            if mode == "fabric":
                tile = np.asarray(Image.open(FAB / f"{f['fabric']}_bc.png").convert("RGB")).astype(np.float32) / 255
            else:  # mirrored 2x2 tile from a crop of another atlas (e.g. forehead skin for bare hands)
                smat, x0, y0, x1, y1 = f["src"]
                crop = load_rgb(find_tex(avatar, smat, "color"), 2048)[y0:y1, x0:x1, :3]
                top = np.concatenate([crop, crop[:, ::-1]], 1)
                tile = np.concatenate([top, top[::-1]], 0).astype(np.float32)
                # remove the crop's own large-scale shading so the tile is flat
                lo = cv2.GaussianBlur(tile, (0, 0), 25)
                tile = np.clip(tile / (lo + 1e-3) * lo.reshape(-1, 3).mean(0), 0, 1)
            tl = srgb_to_lin(tile)
            tex = tile_sample(tl, size, f.get("tile_px", 512) * s, f.get("rot", 0), (rng_seed * 37 % 500, rng_seed * 53 % 500))
            sh = shading_from_normal(nn, m, cav_w=f.get("cav_w", 0.55), slope_pow=f.get("slope_pow", 1.6))
            Lb = nblur(L, m, f.get("ao_sigma", 40) * s)
            Lm = np.median(L[m]) + 1e-4
            aol = np.clip(Lb / Lm, 0.55, 1.25) ** f.get("ao_w", 0.6)
            # optional: keep a little of the original fine luminance (only for non-camo sources)
            fine = 1.0
            if f.get("keep_fine", 0) > 0:
                Lf = nblur(L, m, 1.5 * s)
                fine = np.clip(L / (Lf + 1e-4), 0.6, 1.6) ** f["keep_fine"]
            D = sh * aol * fine
            D = D / (np.mean(D[m]) + 1e-6)
            base = tex * f.get("value", 1.0)
            if "tint" in f:
                base = base * np.array(f["tint"], np.float32)
            res = base * D[..., None]
            out_lin[m] = res[m]
            ao[m] = np.clip(aol[m], 0.6, 1.0)
        elif mode == "recolor":
            tgt = srgb_to_lin(np.array(f["color"], np.float32))
            Lm = np.median(L[m]) + 1e-4
            rel = np.clip(L / Lm, 0.05, 3.0) ** f.get("contrast", 1.0)
            keep = f.get("keep_chroma", 0.0)
            chroma = lin / (L[..., None] + 1e-4)
            res = tgt[None, None, :] * rel[..., None]
            if keep > 0:
                res = res * (1 - keep) + (tgt.mean() * chroma * rel[..., None]) * keep
            out_lin[m] = res[m]
        elif mode == "tint":  # multiply the original
            out_lin[m] = (lin * np.array(f["mul"], np.float32))[m]
        # ---- smoothness / metal
        sb = f.get("smooth", 0.28)
        sv = f.get("smooth_var", 0.25)
        smean = np.mean(spec[m]) + 1e-4
        smooth[m] = np.clip(sb + sv * (spec[m] - smean) / max(smean, 0.05) * 0.25, 0.04, 0.85)
        metal[m] = f.get("metal", 0.0)
    # ---- bands (armbands): paint a fabric band around a limb segment between two bones
    for bd in cfg_mat.get("bands", []):
        A = np.array(dump["bones"][bd["bone_a"]], np.float32)
        Bp = np.array(dump["bones"][bd["bone_b"]], np.float32)
        ax = Bp - A
        L2 = float((ax ** 2).sum())
        P = pos
        if bd.get("pos_blur"):  # low-poly sleeves: per-face positions are coarse -> blur them inside each island
            P = pos.copy()
            cand = valid & np.isin(cls, bd["within"])
            for iid in np.unique(isl[cand]):
                mi = isl == iid
                for c in range(3):
                    P[..., c] = np.where(mi, nblur(pos[..., c], mi, bd["pos_blur"] * size / 2048), P[..., c])
        t = ((P - A) * ax).sum(-1) / L2
        perp = np.sqrt(np.maximum(((P - A) ** 2).sum(-1) - (t ** 2) * L2, 0))
        m = valid & np.isin(cls, bd["within"]) & (t >= bd["t"][0]) & (t <= bd["t"][1]) & (perp < bd.get("radius", 0.10))
        m = ndi.binary_closing(m, iterations=2) & valid
        if not m.any():
            continue
        f = bd["fill"]
        tile = np.asarray(Image.open(FAB / f"{f['fabric']}_bc.png").convert("RGB")).astype(np.float32) / 255
        tex = tile_sample(srgb_to_lin(tile), size, f.get("tile_px", 300) * s)
        nn = out_n / (np.linalg.norm(out_n, axis=2, keepdims=True) + 1e-6)
        sh = shading_from_normal(nn, m)
        D = sh / (np.mean(sh[m]) + 1e-6)
        # edge darkening (folded band edges)
        edge = np.clip(1 - np.minimum(t - bd["t"][0], bd["t"][1] - t) / 0.02, 0, 1)
        res = tex * f.get("value", 1.0) * (D * (1 - 0.35 * edge))[..., None]
        out_lin[m] = res[m]
        smooth[m] = f.get("smooth", 0.22)
        cls[m] = "band"
    # ---- grime: dust by height + sweat
    g = cfg_mat.get("grime", {})
    if g:
        z = pos[..., 2]
        noise = value_noise(size, 180 * s, seed + 3)
        noise2 = value_noise(size, 40 * s, seed + 5)
        dust_col = srgb_to_lin(np.array(g.get("dust_color", [0.66, 0.58, 0.47]), np.float32))
        dust = np.zeros((size, size), np.float32)
        for cname, amt in g.get("dust", {}).items():
            m = (cls == cname) & valid
            if not m.any():
                continue
            a0, a1, z0, z1 = amt  # amount at z0 .. amount at z1 (linear in height)
            t = np.clip((z - z0) / (z1 - z0 + 1e-6), 0, 1)
            d = (a0 + (a1 - a0) * t) * (0.55 + 0.9 * noise) * (0.7 + 0.6 * noise2)
            # crevice dust: more in cavities
            sh = shading_from_normal(out_n / (np.linalg.norm(out_n, axis=2, keepdims=True) + 1e-6), m)
            d *= 1.0 + 0.6 * (1 - sh)
            dust[m] = np.clip(d[m], 0, 0.85)
        out_lin = out_lin * (1 - dust[..., None]) + dust_col * dust[..., None] * (0.55 + 0.45 * lum(out_lin)[..., None] / (lum(out_lin)[..., None] + 0.05))
        smooth = smooth * (1 - 0.6 * dust)
        for spot in g.get("sweat", []):  # [cls, x,y,z, radius m, strength]
            cname, x, y, zz, r, st = spot
            m = (cls == cname) & valid
            d = np.sqrt(((pos - np.array([x, y, zz], np.float32)) ** 2).sum(-1))
            w = np.clip(1 - d / r, 0, 1) ** 1.5 * st * (0.6 + 0.8 * noise2)
            w = np.where(m, w, 0)
            out_lin *= (1 - w[..., None] * 0.45)
            smooth += w * 0.08
        # knee / seat wear (lighter, abraded)
        for spot in g.get("wear", []):
            cname, x, y, zz, r, st = spot
            m = (cls == cname) & valid
            d = np.sqrt(((pos - np.array([x, y, zz], np.float32)) ** 2).sum(-1))
            w = np.clip(1 - d / r, 0, 1) * st * (0.4 + 1.2 * noise2)
            w = np.where(m, w, 0)
            out_lin = out_lin * (1 - w[..., None]) + (out_lin * 1.35 + 0.02) * w[..., None]
    # ---- special head ops
    for op in cfg_mat.get("head_ops", []):
        out_lin, out_n, smooth = head_op(op, out_lin, out_n, smooth, pos, fn, valid, dump, size, seed)
    # ---- write
    bc = lin_to_srgb(out_lin)
    bc = pad_edges(bc, valid)
    nout = out_n / (np.linalg.norm(out_n, axis=2, keepdims=True) + 1e-6)
    nout = pad_edges((nout + 1) * 0.5, valid)
    msk = np.zeros((size, size, 4), np.float32)
    msk[..., 0] = metal
    msk[..., 1] = ao
    msk[..., 3] = smooth
    msk = pad_edges(msk, valid)
    outdir.mkdir(parents=True, exist_ok=True)
    stem = f"T_{variant}_{part_name}"
    if alpha is not None:
        rgba = np.dstack([bc, alpha])
        Image.fromarray((rgba * 255 + 0.5).clip(0, 255).astype(np.uint8), "RGBA").save(outdir / f"{stem}_BC.png")
    else:
        Image.fromarray((bc * 255 + 0.5).clip(0, 255).astype(np.uint8)).save(outdir / f"{stem}_BC.png")
    Image.fromarray((nout * 255 + 0.5).clip(0, 255).astype(np.uint8)).save(outdir / f"{stem}_N.png")
    Image.fromarray((msk * 255 + 0.5).clip(0, 255).astype(np.uint8), "RGBA").save(outdir / f"{stem}_M.png")
    # class debug
    dbg = (bc * 0.6 * 255).astype(np.uint8)
    pal = {}
    for i, c in enumerate(sorted(set(cls[valid].tolist()))):
        pal[c] = np.array([(i * 97) % 255, (i * 57 + 80) % 255, (i * 31 + 160) % 255], np.uint8)
        dbg[(cls == c) & valid] = (dbg[(cls == c) & valid] * 0.5 + pal[c] * 0.5).astype(np.uint8)
    Image.fromarray(dbg).resize((1024, 1024)).save(outdir / f"_classes_{part_name}.png")
    return {"part": part_name, "classes": {c: int(((cls == c) & valid).sum()) for c in pal}}


def head_op(op, out_lin, out_n, smooth, pos, fn, valid, dump, size, seed):
    kind = op["op"]
    bones = dump["bones"]
    if kind == "shave":
        # scalp = faces above brow line, plus back/sides above the ears; skin colour sampled from the forehead
        head = np.array(bones["Bip01 Head"], np.float32)
        z = pos[..., 2]
        y = pos[..., 1]
        brow = op.get("brow_z", head[2] + 0.11)
        front_y = head[1] - 0.08
        scalp = valid & (z > brow - 0.005) | (valid & (z > head[2] + op.get("ear_z", 0.06)) & (y > front_y + 0.04))
        scalp &= valid
        scalp = ndi.binary_opening(scalp, iterations=2)
        w = ndi.gaussian_filter(scalp.astype(np.float32), 6 * size / 2048)
        sample = valid & (z > brow - 0.05) & (z < brow - 0.015) & (y < front_y)
        skin = np.median(out_lin[sample], axis=0) if sample.any() else np.array([0.2, 0.12, 0.08], np.float32)
        skin = skin * op.get("scalp_mul", 0.92)
        rng = np.random.default_rng(seed)
        stub = rng.random((size, size)).astype(np.float32)
        stub = (stub > 0.72).astype(np.float32) * 0.35
        stub = ndi.gaussian_filter(stub, 0.6)
        shade = value_noise(size, 60, seed + 9) * 0.25 + 0.85
        scalp_col = skin[None, None, :] * (shade - stub * op.get("stubble", 0.8))[..., None]
        out_lin = out_lin * (1 - w[..., None]) + scalp_col * w[..., None]
        nb = cv2.GaussianBlur(out_n, (0, 0), 4)
        out_n = out_n * (1 - w[..., None]) + nb * w[..., None]
        smooth = smooth * (1 - w) + 0.42 * w
    elif kind == "gaunt":
        for b, r, st in (("Bip01 REye", 0.035, 0.30), ("Bip01 LEye", 0.035, 0.30), ("Bip01 RCheek", 0.03, 0.22), ("Bip01 LCheek", 0.03, 0.22)):
            if b not in bones:
                continue
            c = np.array(bones[b], np.float32)
            if "Cheek" in b:
                c = c + np.array([0, 0.01, -0.025], np.float32)
            else:
                c = c + np.array([0, 0.0, -0.012], np.float32)
            d = np.sqrt(((pos - c) ** 2).sum(-1))
            w = np.clip(1 - d / r, 0, 1) ** 2 * st * valid
            out_lin *= (1 - w[..., None] * np.array([0.55, 0.7, 0.6], np.float32))
        # sallow, slightly desaturated
        L = lum(out_lin)[..., None]
        out_lin = out_lin * 0.85 + L * 0.15
    elif kind == "grey_hair":
        head = np.array(bones["Bip01 Head"], np.float32)
        z = pos[..., 2]
        m = valid & (z > head[2] + op.get("above", 0.08))
        L = lum(out_lin)
        if op.get("otsu"):  # region includes skin: split dark hair from skin by an Otsu threshold on log luminance
            v = np.log(L[m] + 1e-4)
            hist, edges = np.histogram(v, 128)
            p = hist / hist.sum()
            om = np.cumsum(p)
            mu = np.cumsum(p * edges[:-1])
            sb = (mu[-1] * om - mu) ** 2 / (om * (1 - om) + 1e-9)
            thr = np.exp(edges[np.argmax(sb)])
            hair = m & (L < thr * op.get("otsu_mul", 1.0))
            hair = ndi.binary_opening(hair, iterations=1)
        else:
            hair = m & (L < np.percentile(L[m], op.get("dark_pct", 70)))
        w = ndi.gaussian_filter(hair.astype(np.float32), 2) * op.get("amount", 0.75)
        grey = (L ** 0.7 * 1.6 + 0.04)[..., None] * np.array([1, 1, 1.02], np.float32)
        out_lin = out_lin * (1 - w[..., None]) + grey * w[..., None]
    return out_lin, out_n, smooth


def run_variant(name, size):
    cfg = rb_configs.VARIANTS[name]
    avatar = cfg["avatar"]
    dump = load_dump(avatar)
    outdir = WORK / name / "master"
    report = []
    for mat, cm in cfg["materials"].items():
        if cm.get("skip"):
            continue
        part = cm.get("part", mat.split("_", 1)[1].capitalize())
        report.append(process_material(name, avatar, mat, cm, size, dump, outdir, part, cfg.get("seed", 1)))
        print(name, mat, "->", part, report[-1]["classes"])
    (WORK / name / "retexture_report.json").write_text(json.dumps(report, indent=1))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("variants", nargs="*")
    ap.add_argument("--size", type=int, default=2048)
    ap.add_argument("--list", action="store_true")
    a = ap.parse_args()
    if a.list:
        print("\n".join(rb_configs.VARIANTS))
        return
    for v in a.variants:
        run_variant(v, a.size)


if __name__ == "__main__":
    main()
