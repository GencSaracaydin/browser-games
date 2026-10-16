#!/usr/bin/env python3
"""Turn the chosen patina takes into game fabric tiles.

  Tools/.venv/bin/python Tools/characters/process_fabrics.py
Writes RawAssets/wp05/ready/Assets/_Game/Art/Textures/Characters/Fabrics/T_Fabric_<Name>_{BC,N,M}.png (1024, seamless)
and RawAssets/wp05/_work/fabrics/<Name>_bc.png (linear-ish working copy used by retexture_rocketbox.py).
M = URP mask (R metal 0, G AO 1, B 0, A smoothness = 1 - roughness).
"""
import json, pathlib
import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
RAW = ROOT / "RawAssets/wp05"
READY = RAW / "ready/Assets/_Game/Art/Textures/Characters/Fabrics"
WORK = RAW / "_work/fabrics"

# id -> (take, game name)
# TigerStripe: the second raw generation (image1) reads as broken surplus tiger stripe; the first (basecolor take 1)
# was too regular (a striped sweater at body scale). Its normal/roughness come from OliveSateen (plain twill weave),
# and it is faded 25 % toward its mean colour (sun-bleached surplus).
OVERRIDES = {"TEX_FABRIC_TIGERSTRIPE": {"bc": "image1", "maps_from": ("TEX_FABRIC_OLIVE_SATEEN", 3), "fade": 0.25, "lift": 1.06}}
PICKS = {
    "TEX_FABRIC_TIGERSTRIPE": (1, "TigerStripe"),
    "TEX_FABRIC_KHAKI_DRILL": (1, "KhakiDrill"),
    "TEX_FABRIC_OLIVE_SATEEN": (3, "OliveSateen"),
    "TEX_FABRIC_GREYGREEN": (1, "GreyGreen"),
    "TEX_FABRIC_TAN_SHIRT": (1, "TanPoplin"),
    "TEX_FABRIC_ORANGE_ARMBAND": (1, "OrangeArmband"),
}


def maps_of(fid, take):
    d = RAW / fid / f"take{take}"
    res = json.loads((d / "result.json").read_text())
    files = sorted(d.glob("0*.png"))
    out = {}
    for i, img in enumerate(res["images"]):
        mt = img.get("map_type") or f"image{i}"
        out[mt] = files[i]
    return out


def main():
    READY.mkdir(parents=True, exist_ok=True)
    WORK.mkdir(parents=True, exist_ok=True)
    index = []
    for fid, (take, name) in PICKS.items():
        m = maps_of(fid, take)
        ov = OVERRIDES.get(fid, {})
        bcsrc = m[ov.get("bc", "basecolor")]
        if ov.get("maps_from"):
            m = maps_of(*ov["maps_from"])
        bc = Image.open(bcsrc).convert("RGB").resize((1024, 1024), Image.LANCZOS)
        if ov.get("fade"):
            a = np.asarray(bc).astype(np.float32) / 255
            a = (a * (1 - ov["fade"]) + a.reshape(-1, 3).mean(0) * ov["fade"]) * ov.get("lift", 1.0)
            bc = Image.fromarray((a.clip(0, 1) * 255 + 0.5).astype(np.uint8))
        n = Image.open(m["normal"]).convert("RGB").resize((1024, 1024), Image.LANCZOS)
        r = np.asarray(Image.open(m["roughness"]).convert("L").resize((1024, 1024), Image.LANCZOS)).astype(np.float32) / 255
        # fabric is never glossy: clamp smoothness to a cloth range
        sm = np.clip(1 - r, 0.08, 0.45)
        mask = np.zeros((1024, 1024, 4), np.uint8)
        mask[..., 1] = 255
        mask[..., 3] = (sm * 255).astype(np.uint8)
        # soften the generated normal (weave only; folds come from the character maps)
        na = np.asarray(n).astype(np.float32) / 127.5 - 1
        na[..., :2] *= 0.6
        na /= np.linalg.norm(na, axis=2, keepdims=True) + 1e-6
        n = Image.fromarray(((na + 1) * 127.5).clip(0, 255).astype(np.uint8))
        bc.save(READY / f"T_Fabric_{name}_BC.png")
        n.save(READY / f"T_Fabric_{name}_N.png")
        Image.fromarray(mask, "RGBA").save(READY / f"T_Fabric_{name}_M.png")
        bc.save(WORK / f"{name}_bc.png")
        n.save(WORK / f"{name}_n.png")
        for suf, typ, note in (("BC", "texture", "sRGB on, Default, Max 1024, Wrap Repeat, crunch 70"),
                               ("N", "texture", "Normal map (OpenGL +Y), sRGB off, Max 1024, Wrap Repeat"),
                               ("M", "texture", "Default, sRGB OFF, URP mask: R metal 0, G AO 1, A smoothness; Max 1024, Wrap Repeat")):
            index.append({"manifest_id": fid, "file": f"Assets/_Game/Art/Textures/Characters/Fabrics/T_Fabric_{name}_{suf}.png",
                          "target_path": f"Assets/_Game/Art/Textures/Characters/Fabrics/T_Fabric_{name}_{suf}.png", "type": typ,
                          "import_notes": f"Seamless fabric tile (patina take {take}). {note}. Already baked into every character body texture by "
                                          "retexture_rocketbox.py; ship only if a material uses it as a detail/tiling map, otherwise keep as source."})
        print(name, "ok")
    (WORK / "fabric_index.json").write_text(json.dumps(index, indent=1))


if __name__ == "__main__":
    main()
