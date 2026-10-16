#!/usr/bin/env python3
"""Build the Unity texture set of one mesh from generator maps (Tools/.venv python).

  tex_pack.py --src <dir from glb_textures.py> --ao <baked ao.png | none> --out <dir> --name T_WPN_AK
              [--bc 2048] [--n 2048] [--m 1024] [--recolor json]
Outputs (PNG):
  <name>_BC.png  base colour, sRGB, RGB
  <name>_N.png   tangent-space normal, OpenGL (+Y up = Unity), linear, RGB, renormalised after resize
  <name>_M.png   mask (TECH_PLAN D4): R metallic, G ambient occlusion, B 0 (unused), A smoothness (1 - roughness), linear, RGBA
Downscaling uses box reduction for exact powers of two (no ringing on the atlas islands), Lanczos otherwise.
--grade-wood "sat=0.75,val=0.8,hue=-0.01,smooth=0.4,nstr=0.6": tone down generated wood (orange, painterly, glossy):
  wood = non-metal pixels with an orange/brown hue; saturation and value scaled, hue shifted, smoothness capped,
  normal-map strength reduced (the generator's wood normals are lumpy). Soft mask, so no seams.
--grade-metal "val=0.9,smooth=0.55,metal=0.4": optional same for metal pixels (metallic > 0.5); metal= scales
  their metallic (coated finishes: anodised, phosphated, painted).
--project <dir> [--project-metal 0.6] [--project-lo 0.25 --project-hi 0.65]: blend the photo projections made by
  bl_project.py (proj_<side>_color.png weighted by proj_<side>_vis.png) into the base colour: full weight on
  non-metal (wood, bakelite, polymer), --project-metal on metal. Applied before --grade-*.
--recolor: optional JSON {"from": [r,g,b], "to": [r,g,b], "tol": 40} to repaint a colour band in BC (smoke variants).
--demark <mask.png>: UV-space mask baked by bl_project.py from model-space boxes (job "demark"): inside it the
  generator's pseudo-text (stamped glyphs, serial-like squiggles) is removed: base colour, normal and smoothness are
  (and metallic) replaced by a Gaussian blur of themselves (soft edge), so only the low-frequency finish and wear remain.
"""
import argparse, json, pathlib
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None


def find(src, stem):
    for ext in ("png", "jpg", "jpeg"):
        p = src / f"{stem}.{ext}"
        if p.exists():
            return p
    return None


def resize(im, size):
    w = im.size[0]
    if w == size:
        return im
    if w % size == 0 and (w // size) in (2, 4, 8):
        return im.reduce(w // size)
    return im.resize((size, size), Image.LANCZOS)


def demark_mask(path, size):
    """Soft 0..1 mask at `size`: the baked box mask, dilated ~3 texels (at 2048) and feathered."""
    from PIL import ImageFilter
    if not path or not pathlib.Path(path).exists():
        return None
    m = Image.open(path).convert("L").resize((size, size), Image.BILINEAR)
    k = max(3, int(round(size / 2048 * 7)) | 1)
    m = m.filter(ImageFilter.MaxFilter(k)).filter(ImageFilter.GaussianBlur(max(1.0, size / 2048 * 3)))
    return np.asarray(m).astype(np.float32) / 255


def blur_rgb(arr, sigma):
    from PIL import ImageFilter
    return np.asarray(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(sigma))).astype(np.float32)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--ao", default="none")
    ap.add_argument("--out", required=True)
    ap.add_argument("--name", required=True)
    ap.add_argument("--bc", type=int, default=2048)
    ap.add_argument("--n", type=int, default=2048)
    ap.add_argument("--m", type=int, default=1024)
    ap.add_argument("--recolor")
    ap.add_argument("--ao-strength", type=float, default=1.0)
    ap.add_argument("--grade-wood")
    ap.add_argument("--project")
    ap.add_argument("--project-metal", type=float, default=0.6)
    ap.add_argument("--project-lo", type=float, default=0.25)
    ap.add_argument("--project-hi", type=float, default=0.65)
    ap.add_argument("--grade-metal")
    ap.add_argument("--match", default="metal", help="comma list of metal,wood,other | all | none: tone the unprojected texels (top, bottom, ends) "
                    "of that class to the colour statistics of the photo-projected texels")
    ap.add_argument("--match-strength", type=float, default=0.85)
    ap.add_argument("--dehighlight", type=float, default=1.0, help="<1 compresses photo highlights above the 85th percentile")
    ap.add_argument("--smooth-max", type=float, default=1.0, help="cap smoothness everywhere (painted/parkerised kit)")
    ap.add_argument("--demark")
    ap.add_argument("--normal", help="tangent-space normal map to use instead of <src>/normal (e.g. bl_weapon's high-to-low bake)")
    ap.add_argument("--demark-sigma", type=float, default=7.0, help="blur sigma in texels at 2048 (scaled with size)")
    a = ap.parse_args()
    src, out = pathlib.Path(a.src), pathlib.Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    stats = {}
    bc = Image.open(find(src, "basecolor")).convert("RGB")
    bc = resize(bc, a.bc)
    if a.recolor:
        rc = json.loads(a.recolor)
        arr = np.asarray(bc).astype(np.float32)
        d = np.abs(arr - np.array(rc["from"], np.float32)).max(2)
        msk = d < rc.get("tol", 40)
        # keep the luminance variation (wear, grain) of the original band
        lum = arr[msk].mean(1, keepdims=True) / max(1.0, np.mean(rc["from"]))
        arr[msk] = np.clip(np.array(rc["to"], np.float32) * lum, 0, 255)
        bc = Image.fromarray(arr.astype(np.uint8))
        stats["recolored_px"] = int(msk.sum())
    mrp = find(src, "mr")

    def metal_at(size):
        if not mrp:
            return np.zeros((size, size), np.float32)
        return np.asarray(resize(Image.open(mrp).convert("RGB"), size)).astype(np.float32)[..., 2] / 255

    def parse(g):
        return {k: float(v) for k, v in (kv.split("=") for kv in g.split(","))} if g else None

    gw, gm = parse(a.grade_wood), parse(a.grade_metal)

    def masks(size, rgb):
        from PIL import ImageFilter
        hsv = np.asarray(Image.fromarray(rgb).convert("HSV")).astype(np.float32) / 255
        met = metal_at(size)
        wood = ((met < 0.35) & (hsv[..., 0] > 0.0) & (hsv[..., 0] < 0.13) & (hsv[..., 1] > 0.28) & (hsv[..., 2] > 0.12)).astype(np.float32)
        metal = (met > 0.5).astype(np.float32)
        blur = lambda m: np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(max(1, size // 1024)))).astype(np.float32) / 255
        return blur(wood), blur(metal), hsv

    bc_orig = bc
    if a.project:
        pdir = pathlib.Path(a.project)
        base = np.asarray(bc).astype(np.float32)
        met = metal_at(a.bc)
        cls = (1 - np.clip((met - 0.35) / 0.3, 0, 1)) + np.clip((met - 0.35) / 0.3, 0, 1) * a.project_metal
        acc = np.zeros_like(base)
        W = np.zeros(base.shape[:2], np.float32)
        Wv = np.zeros(base.shape[:2], np.float32)
        for side in ("left", "right"):
            cp, vp = pdir / f"proj_{side}_color.png", pdir / f"proj_{side}_vis.png"
            if not cp.exists():
                continue
            col = np.asarray(Image.open(cp).convert("RGB").resize((a.bc, a.bc), Image.LANCZOS)).astype(np.float32)
            vis = np.asarray(Image.open(vp).convert("L").resize((a.bc, a.bc), Image.BILINEAR)).astype(np.float32) / 255
            t = np.clip((vis - a.project_lo) / (a.project_hi - a.project_lo), 0, 1)
            t = t * (col.sum(2) > 6)  # texels the colour bake never reached (cleared black gutter) get no weight
            if a.dehighlight < 1.0:
                # studio product shots bake specular highlights into the photo; compress the brightest texels
                # (soft knee above the 85th percentile of the projected texels) so they do not become albedo
                V = col.max(2)
                sel_ = t > 0.5
                if sel_.sum() > 1000:
                    thr = np.percentile(V[sel_], 85)
                    f = np.where(V > thr, (thr + (V - thr) * a.dehighlight) / np.maximum(V, 1e-3), 1.0)
                    col = col * f[..., None]
            w = t * t * (3 - 2 * t) * cls
            acc += col * w[..., None]
            W += w
            Wv += t * t * (3 - 2 * t)
        W = np.clip(W, 0, 1)
        out_bc = base * (1 - W[..., None]) + acc
        if a.match != "none":
            # texels the side photos never see (top, bottom, end faces) keep the generator's flatter colour; move
            # their per-channel mean/std towards the projected texels of the same class so the gun reads as one finish
            Wv = np.clip(Wv, 0, 1)
            aop = pathlib.Path(a.ao) if a.ao != "none" else None
            island = np.ones(Wv.shape, bool)
            if aop and aop.exists():
                island = np.asarray(Image.open(aop).convert("L").resize((a.bc, a.bc), Image.BILINEAR)).astype(np.float32) > 4
            ghsv = np.asarray(bc_orig.convert("HSV")).astype(np.float32) / 255  # generator colours classify the texels
            woodc = (met <= 0.5) & (ghsv[..., 0] < 0.13) & (ghsv[..., 1] > 0.28) & (ghsv[..., 2] > 0.12)
            allc = {"metal": met > 0.5, "wood": woodc, "other": (met <= 0.5) & ~woodc}
            want = ["metal", "wood", "other"] if a.match == "all" else a.match.split(",")
            classes = [(c, allc[c]) for c in want if c in allc]
            for cname, cm in classes:
                Pm, Um = island & cm & (Wv > 0.85), island & cm & (Wv < 0.2)
                if Pm.sum() < 2000 or Um.sum() < 2000:
                    continue
                mp, sp = out_bc[Pm].mean(0), out_bc[Pm].std(0) + 1e-3
                mu, su = out_bc[Um].mean(0), out_bc[Um].std(0) + 1e-3
                k = np.clip(sp / su, 0.5, 2.0)
                mapped = (out_bc - mu) * k + mp
                wgt = ((1 - Wv) * cm * a.match_strength)[..., None]
                out_bc = out_bc * (1 - wgt) + mapped * wgt
                stats[f"match_{cname}"] = {"proj_mean": [round(float(v), 1) for v in mp], "unproj_mean": [round(float(v), 1) for v in mu]}
        bc = Image.fromarray(np.clip(out_bc, 0, 255).astype(np.uint8))
        stats["projected_frac"] = round(float(W.mean()), 3)
    if gw or gm:
        rgb = np.asarray(bc)
        wood, metal, hsv = masks(a.bc, rgb)
        h, sat, val = hsv[..., 0], hsv[..., 1], hsv[..., 2]
        if gw:
            h = h + wood * gw.get("hue", 0.0)
            sat = sat * (1 - wood * (1 - gw.get("sat", 1.0)))
            val = val * (1 - wood * (1 - gw.get("val", 1.0)))
            stats["wood_frac"] = round(float(wood.mean()), 3)
        if gm:
            val = val * (1 - metal * (1 - gm.get("val", 1.0)))
            sat = sat * (1 - metal * (1 - gm.get("sat", 1.0)))
        hsv2 = (np.stack([np.mod(h, 1.0), np.clip(sat, 0, 1), np.clip(val, 0, 1)], 2) * 255 + 0.5).astype(np.uint8)
        bc = Image.fromarray(hsv2, "HSV").convert("RGB")
    if a.demark:
        dm = demark_mask(a.demark, a.bc)
        if dm is not None:
            arr = np.asarray(bc).astype(np.float32)
            bl = blur_rgb(arr, a.demark_sigma * a.bc / 2048)
            arr = arr * (1 - dm[..., None]) + bl * dm[..., None]
            bc = Image.fromarray(np.clip(arr + 0.5, 0, 255).astype(np.uint8))
            stats["demark_frac"] = round(float(dm.mean()), 4)
    bc.save(out / f"{a.name}_BC.png", optimize=True)
    nrm = Image.open(a.normal if a.normal else find(src, "normal")).convert("RGB")
    if a.normal:
        stats["normal_src"] = str(a.normal)
    n = np.asarray(resize(nrm, a.n)).astype(np.float32) / 255 * 2 - 1
    if gw and gw.get("nstr", 1.0) < 1.0:
        wood_n, _, _ = masks(a.n, np.asarray(bc_orig.resize((a.n, a.n), Image.BILINEAR) if bc_orig.size[0] != a.n else bc_orig))
        k = 1 - wood_n * (1 - gw["nstr"])
        n[..., 0] *= k
        n[..., 1] *= k
    n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
    if a.demark:
        dm = demark_mask(a.demark, a.n)
        if dm is not None:
            nb = blur_rgb((n * 0.5 + 0.5) * 255, a.demark_sigma * 1.4 * a.n / 2048) / 255 * 2 - 1
            n = n * (1 - dm[..., None]) + nb * dm[..., None]
            n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
    Image.fromarray(((n * 0.5 + 0.5) * 255 + 0.5).clip(0, 255).astype(np.uint8)).save(out / f"{a.name}_N.png", optimize=True)
    if mrp:
        mr = np.asarray(resize(Image.open(mrp).convert("RGB"), a.m)).astype(np.float32) / 255
        metal, rough = mr[..., 2], mr[..., 1]
        if gw or gm:
            wood_m, metal_m, _ = masks(a.m, np.asarray(bc_orig.resize((a.m, a.m), Image.BILINEAR)))
            smooth = 1 - rough
            if gw and "smooth" in gw:
                smooth = smooth * (1 - wood_m) + np.minimum(smooth, gw["smooth"]) * wood_m
            if gm and "smooth" in gm:
                smooth = smooth * (1 - metal_m) + np.minimum(smooth, gm["smooth"]) * metal_m
            rough = 1 - smooth
        if gm and "metal" in gm:
            # coated finishes (anodised aluminium, phosphate, paint) read darker and less mirror-like than bare steel:
            # scale the metallic of the metal texels (TECH_PLAN D4 mask R)
            metal = metal * (1 - (metal > 0.5) * (1 - gm["metal"]))
    else:
        metal = np.zeros((a.m, a.m), np.float32)
        rough = np.full((a.m, a.m), 0.6, np.float32)
    if a.smooth_max < 1.0:
        rough = np.maximum(rough, 1 - a.smooth_max)
    if a.ao != "none" and pathlib.Path(a.ao).exists():
        ao = np.asarray(resize(Image.open(a.ao).convert("L"), a.m)).astype(np.float32) / 255
        ao = 1 - (1 - ao) * a.ao_strength
    else:
        ao = np.ones_like(metal)
    if a.demark:
        dm = demark_mask(a.demark, a.m)
        if dm is not None:
            from PIL import ImageFilter
            sg = max(1.5, a.demark_sigma * a.m / 2048 * 1.5)
            r8 = Image.fromarray((np.clip(rough, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(sg))
            rough = rough * (1 - dm) + np.asarray(r8).astype(np.float32) / 255 * dm
            m8 = Image.fromarray((np.clip(metal, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(sg))
            metal = metal * (1 - dm) + np.asarray(m8).astype(np.float32) / 255 * dm
    m = np.stack([metal, ao, np.zeros_like(metal), 1 - rough], 2)
    Image.fromarray((m * 255 + 0.5).clip(0, 255).astype(np.uint8), "RGBA").save(out / f"{a.name}_M.png", optimize=True)
    stats.update({"metal_mean": round(float(metal.mean()), 3), "smooth_mean": round(float((1 - rough).mean()), 3),
                  "ao_mean": round(float(ao.mean()), 3), "bc_mean": [round(float(v), 1) for v in np.asarray(bc).reshape(-1, 3).mean(0)]})
    for suf in ("BC", "N", "M"):
        stats[suf + "_kb"] = round((out / f"{a.name}_{suf}.png").stat().st_size / 1024)
    print(json.dumps({a.name: stats}))


if __name__ == "__main__":
    main()
