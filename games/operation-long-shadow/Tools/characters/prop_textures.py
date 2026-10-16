#!/usr/bin/env python3
"""Extract a Tripo GLB's PBR textures and convert them to game maps (WP05 character props).

  Tools/.venv/bin/python Tools/characters/prop_textures.py <in.glb> <out_dir> <Name> [--size 512] [--tint r g b] [--value v]
Writes <out_dir>/T_<Name>_{BC,N,M}.png : BC sRGB, N OpenGL normal, M = R metal, G AO(1), A smoothness (1 - roughness).
Optional --tint/--value recolour the base colour (multiply, linear) for faction variants.
"""
import argparse, io, json, pathlib, struct
import numpy as np
from PIL import Image


def glb_images(path):
    data = pathlib.Path(path).read_bytes()
    magic, ver, length = struct.unpack_from("<III", data, 0)
    off = 12
    js, binc = None, None
    while off < length:
        clen, ctype = struct.unpack_from("<II", data, off)
        chunk = data[off + 8: off + 8 + clen]
        if ctype == 0x4E4F534A:
            js = json.loads(chunk)
        elif ctype == 0x004E4942:
            binc = chunk
        off += 8 + clen
    imgs = []
    for im in js.get("images", []):
        bv = js["bufferViews"][im["bufferView"]]
        b = binc[bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]]
        imgs.append(Image.open(io.BytesIO(b)))
    mat = js["materials"][0]
    pbr = mat.get("pbrMetallicRoughness", {})
    tex = js.get("textures", [])

    def get(ref):
        if not ref:
            return None
        return imgs[tex[ref["index"]]["source"]]

    return {"bc": get(pbr.get("baseColorTexture")), "mr": get(pbr.get("metallicRoughnessTexture")),
            "n": get(mat.get("normalTexture"))}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("glb")
    ap.add_argument("out")
    ap.add_argument("name")
    ap.add_argument("--size", type=int, default=512)
    ap.add_argument("--tint", type=float, nargs=3)
    ap.add_argument("--value", type=float, default=1.0)
    ap.add_argument("--desat", type=float, default=0.0)
    a = ap.parse_args()
    m = glb_images(a.glb)
    out = pathlib.Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    S = (a.size, a.size)
    bc = np.asarray(m["bc"].convert("RGB").resize(S, Image.LANCZOS)).astype(np.float32) / 255
    if a.tint or a.value != 1.0 or a.desat:
        lin = np.where(bc <= 0.04045, bc / 12.92, ((bc + 0.055) / 1.055) ** 2.4)
        if a.desat:
            L = (lin * [0.2126, 0.7152, 0.0722]).sum(-1, keepdims=True)
            lin = lin * (1 - a.desat) + L * a.desat
        lin = lin * (np.array(a.tint) if a.tint else 1.0) * a.value
        lin = np.clip(lin, 0, 1)
        bc = np.where(lin <= 0.0031308, lin * 12.92, 1.055 * lin ** (1 / 2.4) - 0.055)
    Image.fromarray((bc * 255 + 0.5).astype(np.uint8)).save(out / f"T_{a.name}_BC.png")
    if m["n"] is not None:
        m["n"].convert("RGB").resize(S, Image.LANCZOS).save(out / f"T_{a.name}_N.png")
    mask = np.zeros((a.size, a.size, 4), np.uint8)
    mask[..., 1] = 255
    if m["mr"] is not None:
        mr = np.asarray(m["mr"].convert("RGB").resize(S, Image.LANCZOS))
        mask[..., 0] = mr[..., 2]
        mask[..., 3] = 255 - mr[..., 1]
    else:
        mask[..., 3] = 90
    Image.fromarray(mask, "RGBA").save(out / f"T_{a.name}_M.png")
    print("ok", a.name, {k: (v.size if v is not None else None) for k, v in m.items()})


if __name__ == "__main__":
    main()
