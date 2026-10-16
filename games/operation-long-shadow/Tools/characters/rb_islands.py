#!/usr/bin/env python3
"""Island viewer + mask rasteriser for Rocketbox UV atlases (reads rb_dump.py output).

  Tools/.venv/bin/python Tools/characters/rb_islands.py view <Avatar>   -> RawAssets/wp05/_work/<Avatar>/islands_<mat>.png
Library use: load_dump(), part_of(bone), raster_islands(dump, mat, size) -> int32 island-id map (-1 empty)
"""
import json, pathlib, sys
import numpy as np
from PIL import Image, ImageDraw

ROOT = pathlib.Path(__file__).resolve().parents[2]
WORK = ROOT / "RawAssets/wp05/_work"


def load_dump(avatar):
    return json.loads((WORK / avatar / "dump.json").read_text())


def part_of(bone):
    b = bone.lower()
    if any(k in b for k in ("finger", " hand")):
        return "hand"
    if any(k in b for k in ("forearm", "upperarm")):
        return "arm"
    if any(k in b for k in ("toe", " foot")):
        return "foot"
    if any(k in b for k in ("calf", "thigh")):
        return "leg"
    if any(k in b for k in ("spine", "pelvis", "clavicle")) or b.strip() == "bip01":
        return "torso"
    if b:
        return "head"
    return "none"


PART_COL = {"hand": (255, 80, 80), "arm": (255, 200, 0), "foot": (0, 160, 255), "leg": (0, 220, 120),
            "torso": (200, 120, 255), "head": (255, 255, 255), "none": (90, 90, 90)}


def raster_islands(dump, mat, size, supersample=1):
    W = H = size * supersample
    img = Image.new("I", (W, H), -1)
    dr = ImageDraw.Draw(img)
    mi = dump["materials"].index(mat)
    for f in dump["faces"]:
        if f["m"] != mi:
            continue
        poly = [((u % 1.0001) * W, (1 - v) * H) for u, v in f["uv"]]
        dr.polygon(poly, fill=f["isl"], outline=f["isl"])
    a = np.asarray(img).astype(np.int32)
    if supersample > 1:
        a = a[::supersample, ::supersample]
    return a


def island_info(dump):
    info = {}
    for f in dump["faces"]:
        i = info.setdefault(f["isl"], {"m": f["m"], "n": 0, "c": np.zeros(3), "parts": {}, "uv": []})
        i["n"] += 1
        i["c"] += np.array(f["c"])
        p = part_of(f["b"])
        i["parts"][p] = i["parts"].get(p, 0) + 1
        i["uv"] += f["uv"]
    for k, i in info.items():
        i["c"] = (i["c"] / i["n"]).round(3).tolist()
        i["part"] = max(i["parts"], key=i["parts"].get)
        uv = np.array(i["uv"])
        i["uvc"] = uv.mean(0).tolist()
        i["uvbb"] = [float(uv[:, 0].min()), float(uv[:, 1].min()), float(uv[:, 0].max()), float(uv[:, 1].max())]
        del i["uv"]
    return info


def view(avatar, tex_lookup=None):
    d = load_dump(avatar)
    info = island_info(d)
    texdir = None
    for base in (ROOT / "RawAssets/wp05/rocketbox" / avatar / "Textures", ROOT / "RawAssets/cc0/characters/rocketbox" / avatar / "Textures"):
        if base.exists():
            texdir = base
            break
    out = {}
    for m in d["materials"]:
        size = 1024
        bg = Image.new("RGB", (size, size), (0, 0, 0))
        if texdir:
            cands = sorted(texdir.glob(m.split("_")[0] + "_" + m.split("_", 1)[1] + "_color*.tga"))
            if cands:
                bg = Image.open(cands[0]).convert("RGB").resize((size, size))
        ids = raster_islands(d, m, size)
        a = np.asarray(bg).astype(float) * 0.55
        for k, i in info.items():
            if d["materials"][i["m"]] != m:
                continue
            mask = ids == k
            col = np.array(PART_COL[i["part"]])
            a[mask] = a[mask] * 0.6 + col * 0.4 * 0.8
        # island outlines
        edge = np.zeros_like(ids, bool)
        edge[1:, :] |= ids[1:, :] != ids[:-1, :]
        edge[:, 1:] |= ids[:, 1:] != ids[:, :-1]
        a[edge] = (255, 255, 255)
        im = Image.fromarray(a.clip(0, 255).astype(np.uint8))
        dr = ImageDraw.Draw(im)
        for k, i in info.items():
            if d["materials"][i["m"]] != m:
                continue
            u, v = i["uvc"]
            dr.text((u * size - 6, (1 - v) * size - 5), str(k), fill=(255, 255, 0))
        p = WORK / avatar / f"islands_{m}.png"
        im.save(p)
        out[m] = str(p)
    (WORK / avatar / "islands.json").write_text(json.dumps(info, indent=0))
    return out


if __name__ == "__main__":
    if sys.argv[1] == "view":
        for av in sys.argv[2:]:
            print(view(av))
