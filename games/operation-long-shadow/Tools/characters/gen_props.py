#!/usr/bin/env python3
"""Character props (CHP_*): Nano Banana 2.1 reference -> Tripo P2 image-to-3D, all through Tools/fal/falcall.py (WP05).

  PY=/Users/gencsaracaydin/Documents/fun/.venv/bin/python
  $PY Tools/characters/gen_props.py ref  [--ids CHP_BOONIE ...] [--take 1]     # 2 reference images per prop
  $PY Tools/characters/gen_props.py 3d   --ids CHP_BOONIE --ref RawAssets/wp05/CHP_BOONIE/ref_take1/00_x.png [--take 1]
Raw outputs: RawAssets/wp05/<ID>/ref_take<N>/, RawAssets/wp05/<ID>/tripo_take<N>/
Fit/skin/export happens in Tools/characters/blender/prop_fit.py + rb_export.py.
"""
import argparse, json, pathlib, subprocess, sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
PY = "/Users/gencsaracaydin/Documents/fun/.venv/bin/python"
TAIL = (" Pure seamless white background, soft even diffuse studio lighting, minimal specular highlights, only a faint contact"
        " shadow, everything in sharp focus, museum catalog reference photo. No text, no lettering, no numbers, no markings, no logos,"
        " no serial numbers, no insignia, no badges, no patches, no flags, no hands, no head, no mannequin visible, no other objects.")
VIEW = (" 3/4 elevated view from the front-left, camera perpendicular, no perspective distortion, entire object visible, centered,"
        " filling about 90% of the frame width.")
PROPS = {
    "CHP_BOONIE": ("Photorealistic studio product photograph of a faded tan cotton boonie hat with a stitched floppy brim, sun-bleached and"
                   " sweat-stained, slightly crushed and worn, round crown." + VIEW + TAIL, 1500),
    "CHP_BALLCAP": ("Photorealistic studio product photograph of a plain faded olive drab cotton baseball cap with a curved brim, worn and"
                    " slightly dusty, plain front panel." + VIEW + TAIL, 1000),
    "CHP_BERET": ("Photorealistic studio product photograph of a plain faded khaki-brown wool military beret without any badge, with a leather"
                  " sweatband edge, the crown pulled down to the right side as it would sit on a head, felted wool texture." + VIEW + TAIL, 800),
    "CHP_HELMET_GOGGLES": ("Photorealistic studio product photograph of a plain rounded 1970s steel combat helmet painted matte grey-green,"
                           " canvas chin strap hanging down, a pair of simple dust goggles with an elastic strap around the front of the"
                           " helmet above the brim, chipped worn paint." + VIEW + TAIL, 2500),
    "CHP_CHEST_RIG": ("Photorealistic ghost-mannequin product photograph of a 1980s faded olive canvas chest rig harness worn on an invisible"
                      " hollow torso: a horizontal belly panel with three tall rifle magazine pouches with flaps across the front, two small"
                      " grenade pouches at the sides, narrow canvas shoulder straps crossing in an X on the back, shaped exactly as if worn"
                      " by a person, hollow inside, front 3/4 view from the front-left, slightly elevated, entire rig visible, centered."
                      + TAIL, 3000),
    "CHP_AVIATOR_GOGGLES": ("Photorealistic studio product photograph of a pair of 1970s aviator dust goggles with round tinted lenses in a"
                            " rubber frame and a worn elastic strap forming a loop as if around a head." + VIEW + TAIL, 1200),
    "CHP_NVG": ("Photorealistic studio product photograph of an early-1980s single-tube night vision goggle unit mounted on a webbing head"
                " harness shaped as if worn on a head, olive drab, with a dark green lens." + VIEW + TAIL, 2000),
    "CHP_ARMOR_VEST": ("Photorealistic ghost-mannequin product photograph of a 1980s plain grey-green nylon fabric armour vest with a high collar"
                       " and front closure flap, no pouches, no patches, worn on an invisible hollow torso, shaped exactly as if worn, front 3/4"
                       " view from the front-left, slightly elevated, entire vest visible, centered." + TAIL, 3000),
    # added in the resume pass (not in ASSET_MANIFEST; logged in DECISIONS.md)
    "CHP_FLIGHT_HELMET": ("Photorealistic studio product photograph of a 1980s olive drab helicopter pilot flight helmet in the style of an"
                          " SPH-4, smooth rounded shell, a dark smoked sun visor lowered over the face opening, a boom microphone on the"
                          " left side, chin strap, worn matte paint with scuffs." + VIEW + TAIL, 2500),
    "CHP_SCARF": ("Photorealistic ghost-mannequin product photograph of a plain solid faded olive-drab cotton neck scarf wrapped loosely once"
                  " around an invisible neck, knotted at the front with the two short ends hanging down onto the chest, plain weave, one"
                  " single solid colour, no pattern, no checks, no stripes, no fringe, no tassels, shaped exactly as if worn, hollow inside,"
                  " front 3/4 view from the front-left, slightly elevated, entire scarf visible, centered." + TAIL, 1500),
}


def call(fid, endpoint, args, est, out):
    cmd = [PY, str(ROOT / "Tools/fal/falcall.py"), "--wp", "WP05", "--id", fid, "--endpoint", endpoint,
           "--args-json", json.dumps(args), "--est-usd", str(est), "--out", str(out)]
    return subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)


def upload(path):
    sys.path.insert(0, str(ROOT / "Tools/fal"))
    import os
    import falcall  # noqa: F401  (for load_key)
    falcall.load_key()
    import fal_client
    return fal_client.upload_file(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("stage", choices=["ref", "3d"])
    ap.add_argument("--ids", nargs="*", default=list(PROPS))
    ap.add_argument("--take", type=int, default=1)
    ap.add_argument("--ref", help="chosen reference PNG (3d stage, single id)")
    ap.add_argument("--images", type=int, default=2)
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    procs = []
    for fid in a.ids:
        prompt, faces = PROPS[fid]
        if a.stage == "ref":
            out = ROOT / "RawAssets/wp05" / fid / f"ref_take{a.take}"
            if (out / "result.json").exists() and not a.force:
                print("skip", fid)
                continue
            args = {"prompt": prompt, "aspect_ratio": "1:1", "resolution": "2K", "output_format": "png", "num_images": a.images,
                    "limit_generations": True}
            procs.append((fid, call(fid + "_ref", "google/nano-banana-2.1", args, 0.10 * a.images, out)))
        else:
            ref = a.ref
            out = ROOT / "RawAssets/wp05" / fid / f"tripo_take{a.take}"
            if (out / "result.json").exists() and not a.force:
                print("skip", fid)
                continue
            url = upload(ref)
            (out).mkdir(parents=True, exist_ok=True)
            (out / "ref_used.txt").write_text(str(ref) + "\n" + url + "\n")
            args = {"image_url": url, "face_limit": faces, "texture": True, "pbr": True, "texture_quality": "detailed",
                    "delight": True, "export_uv": True, "auto_size": False, "quad": False}
            procs.append((fid, call(fid, "tripo3d/p2/image-to-3d", args, 1.20, out)))
    for fid, p in procs:
        o, _ = p.communicate()
        print("==", fid, "exit", p.returncode, o[-300:].replace("\n", " "))


if __name__ == "__main__":
    main()
