#!/usr/bin/env python3
"""Generate the TEX_FABRIC_* seamless fabric tiles with fal-ai/patina/material (via Tools/fal/falcall.py).

Usage: /Users/gencsaracaydin/Documents/fun/.venv/bin/python Tools/characters/gen_fabrics.py [--ids TEX_FABRIC_KHAKI_DRILL ...] [--take 2] [--force]
Raw outputs: RawAssets/wp05/<ID>/take<N>/ (request/result/timing json + PNG maps).
Pick and post-process with Tools/characters/process_fabrics.py.
"""
import argparse, json, pathlib, subprocess, sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
PY = "/Users/gencsaracaydin/Documents/fun/.venv/bin/python"

COMMON = (" Flat lay, photographed straight down, evenly lit with soft overcast light, no shadows, no folds, no wrinkles,"
          " no seams, no buttons, no stitching lines, no text, no labels, no logos, no insignia, no flags, fills the frame,"
          " seamless tileable fabric swatch about 40 cm across.")
FABRICS = {
    "TEX_FABRIC_TIGERSTRIPE": "faded 1970s tiger stripe camouflage cotton twill fabric: generic horizontal brush-stroke stripes of dark brown and"
                              " black over faded olive and khaki green, sun-bleached and washed out, worn surplus cloth with visible twill weave." + COMMON,
    "TEX_FABRIC_KHAKI_DRILL": "faded khaki cotton drill uniform fabric, sandy tan-brown, slightly uneven sun fading and faint sweat-darkened mottling,"
                              " fine diagonal drill weave visible, worn surplus military cloth." + COMMON,
    "TEX_FABRIC_OLIVE_SATEEN": "plain olive drab green cotton uniform fabric (army OD green, muted brownish green), tight smooth weave, no pattern,"
                               " washed many times, subtle uneven fading." + COMMON,
    "TEX_FABRIC_GREYGREEN": "plain grey-green military cotton fatigue fabric, muted cool grey with a green cast, tight plain weave, no pattern,"
                            " slightly worn and dusty." + COMMON,
    "TEX_FABRIC_TAN_SHIRT": "tan cotton poplin field shirt fabric, light sand-tan, fine tight poplin weave, slightly faded and dusty,"
                            " subtle uneven colour." + COMMON,
    "TEX_FABRIC_ORANGE_ARMBAND": "bright safety-orange cotton webbing armband fabric, saturated orange, slightly dirty with faint dust and grime,"
                                 " coarse plain weave." + COMMON,
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids", nargs="*", default=list(FABRICS))
    ap.add_argument("--take", type=int, default=1)
    ap.add_argument("--images", type=int, default=2)
    ap.add_argument("--seed", type=int, default=1984)
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    procs = []
    for fid in a.ids:
        out = ROOT / "RawAssets/wp05" / fid / f"take{a.take}"
        if (out / "result.json").exists() and not a.force:
            print("skip", fid)
            continue
        args = {"prompt": FABRICS[fid], "image_size": "square_hd", "num_images": a.images, "seed": a.seed,
                "enable_prompt_expansion": False, "tiling_mode": "both", "maps": ["basecolor", "normal", "roughness"],
                "output_format": "png"}
        est = round(0.01 + a.images * 1.05 * (0.02 + 0.03), 3)
        cmd = [PY, str(ROOT / "Tools/fal/falcall.py"), "--wp", "WP05", "--id", fid, "--endpoint", "fal-ai/patina/material",
               "--args-json", json.dumps(args), "--est-usd", str(est), "--out", str(out)]
        procs.append((fid, subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)))
    for fid, p in procs:
        out, _ = p.communicate()
        print("==", fid, "exit", p.returncode)
        print(out[-600:])


if __name__ == "__main__":
    sys.exit(main())
