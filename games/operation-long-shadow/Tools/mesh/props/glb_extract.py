#!/usr/bin/env python3
"""Extract the PBR images of a GLB (first material that has them) into a folder.

  Tools/.venv/bin/python Tools/mesh/props/glb_extract.py in.glb outdir
Writes outdir/src_basecolor.<ext>, src_mr.<ext> (glTF: G roughness, B metallic), src_normal.<ext>,
src_occlusion/src_emissive if present, and outdir/glb_info.json (materials, image sizes, tris, extras).
"""
import json, pathlib, struct, sys


def read_glb(p):
    b = pathlib.Path(p).read_bytes()
    magic, ver, length = struct.unpack_from("<4sII", b, 0)
    assert magic == b"glTF", "not a GLB"
    off = 12
    js, binc = None, None
    while off < length:
        clen, ctype = struct.unpack_from("<I4s", b, off)
        data = b[off + 8: off + 8 + clen]
        if ctype == b"JSON":
            js = json.loads(data)
        elif ctype == b"BIN\x00":
            binc = data
        off += 8 + clen
    return js, binc


def image_bytes(js, binc, img_idx):
    im = js["images"][img_idx]
    bv = js["bufferViews"][im["bufferView"]]
    s = bv.get("byteOffset", 0)
    return binc[s: s + bv["byteLength"]], im.get("mimeType", "image/png")


def tris(js):
    n = 0
    for m in js.get("meshes", []):
        for pr in m["primitives"]:
            if "indices" in pr:
                n += js["accessors"][pr["indices"]]["count"] // 3
            else:
                n += js["accessors"][pr["attributes"]["POSITION"]]["count"] // 3
    return n


def main():
    src, out = sys.argv[1], pathlib.Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    js, binc = read_glb(src)
    info = {"tris": tris(js), "materials": [], "images": len(js.get("images", []))}
    done = set()
    for mi, mat in enumerate(js.get("materials", [])):
        slots = {}
        pbr = mat.get("pbrMetallicRoughness", {})
        if "baseColorTexture" in pbr:
            slots["basecolor"] = pbr["baseColorTexture"]["index"]
        if "metallicRoughnessTexture" in pbr:
            slots["mr"] = pbr["metallicRoughnessTexture"]["index"]
        if "normalTexture" in mat:
            slots["normal"] = mat["normalTexture"]["index"]
        if "occlusionTexture" in mat:
            slots["occlusion"] = mat["occlusionTexture"]["index"]
        if "emissiveTexture" in mat:
            slots["emissive"] = mat["emissiveTexture"]["index"]
        info["materials"].append({"name": mat.get("name"), "slots": list(slots),
                                  "metallicFactor": pbr.get("metallicFactor", 1.0),
                                  "roughnessFactor": pbr.get("roughnessFactor", 1.0),
                                  "baseColorFactor": pbr.get("baseColorFactor"),
                                  "alphaMode": mat.get("alphaMode", "OPAQUE")})
        for k, ti in slots.items():
            key = f"{k}" if mi == 0 else f"{k}_m{mi}"
            if key in done:
                continue
            img_idx = js["textures"][ti]["source"]
            data, mime = image_bytes(js, binc, img_idx)
            ext = ".jpg" if "jpeg" in mime else ".png"
            (out / f"src_{key}{ext}").write_bytes(data)
            done.add(key)
    (out / "glb_info.json").write_text(json.dumps(info, indent=1))
    print(json.dumps(info))


if __name__ == "__main__":
    main()
