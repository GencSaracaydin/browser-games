#!/usr/bin/env python3
"""Extract the PBR images of a GLB (Tripo / Hunyuan) into files, named by their material slot.

  glb_textures.py model.glb out_dir
Writes out_dir/{basecolor,mr,normal,occlusion,emissive}.<png|jpg> and out_dir/textures.json with stats.
glTF packing: metallicRoughness G = roughness, B = metallic; occlusion R (Tripo packs O/R/M in one image).
"""
import json, pathlib, struct, sys, io
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None


def read_glb(path):
    b = pathlib.Path(path).read_bytes()
    magic, ver, length = struct.unpack_from("<III", b, 0)
    assert magic == 0x46546C67, "not a GLB"
    off, js, binc = 12, None, None
    while off < length:
        clen, ctype = struct.unpack_from("<II", b, off)
        data = b[off + 8: off + 8 + clen]
        if ctype == 0x4E4F534A:
            js = json.loads(data)
        elif ctype == 0x004E4942:
            binc = data
        off += 8 + clen
    return js, binc


def main(path, out):
    out = pathlib.Path(out)
    out.mkdir(parents=True, exist_ok=True)
    js, binc = read_glb(path)
    mats = js.get("materials", [])
    slots = {}
    for m in mats:
        pbr = m.get("pbrMetallicRoughness", {})
        if "baseColorTexture" in pbr: slots.setdefault("basecolor", pbr["baseColorTexture"]["index"])
        if "metallicRoughnessTexture" in pbr: slots.setdefault("mr", pbr["metallicRoughnessTexture"]["index"])
        if "normalTexture" in m: slots.setdefault("normal", m["normalTexture"]["index"])
        if "occlusionTexture" in m: slots.setdefault("occlusion", m["occlusionTexture"]["index"])
        if "emissiveTexture" in m: slots.setdefault("emissive", m["emissiveTexture"]["index"])
    info = {"materials": len(mats), "factors": [m.get("pbrMetallicRoughness", {}) for m in mats]}
    for name, ti in slots.items():
        src = js["textures"][ti]["source"]
        img = js["images"][src]
        bv = js["bufferViews"][img["bufferView"]]
        data = binc[bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]]
        ext = "jpg" if img.get("mimeType") == "image/jpeg" else "png"
        p = out / f"{name}.{ext}"
        p.write_bytes(data)
        im = Image.open(io.BytesIO(data))
        a = np.asarray(im.convert("RGB")).astype(np.float32) / 255
        info[name] = {"file": p.name, "size": im.size, "mode": im.mode,
                      "mean_rgb": [round(float(v), 3) for v in a.reshape(-1, 3).mean(0)],
                      "std_rgb": [round(float(v), 3) for v in a.reshape(-1, 3).std(0)]}
    (out / "textures.json").write_text(json.dumps(info, indent=1))
    print(json.dumps(info))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
