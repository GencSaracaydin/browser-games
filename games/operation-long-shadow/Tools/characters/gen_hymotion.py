#!/usr/bin/env python3
"""HY-Motion (fal-ai/hunyuan-motion) bespoke clips through Tools/fal/falcall.py (WP05, ANIM_HYMOTION_SIGNALS + insertion signals).

  /Users/gencsaracaydin/Documents/fun/.venv/bin/python Tools/characters/gen_hymotion.py [names...] [--take N]
Output: RawAssets/wp05/ANIM_HYMOTION_SIGNALS/<name>_t<N>/ (SMPL-H skeleton FBX, 30 fps). Baked onto the Rocketbox
skeleton by Tools/characters/build_clips.py hymotion.
"""
import argparse, json, pathlib, subprocess, sys
ROOT = pathlib.Path(__file__).resolve().parents[2]
PY = "/Users/gencsaracaydin/Documents/fun/.venv/bin/python"
PROMPTS = {
    "fist_hold": ("A soldier stands holding a rifle low in his right hand, raises his left fist to head height to signal halt, holds the fist "
                  "still for two seconds, then lowers his arm.", 5.0),
    "move_up": ("A soldier holding a rifle in his right hand at his side swings his left arm forward from behind his body, pointing ahead with "
                "an open hand to signal his team to move up, then lowers the arm.", 4.0),
    "crouch_run_cover": ("A soldier crouch-runs forward quickly with a rifle held low in both hands, drops to one knee behind cover and raises "
                         "the rifle to his shoulder to aim.", 5.0),
    "plant_charge": ("A soldier kneels on one knee, places a small box on the ground in front of him with both hands, presses it down, then "
                     "stands up and steps back.", 6.0),
    "check_watch": ("A man standing still lifts his left forearm in front of his chest, turns his wrist and looks down at his wristwatch for "
                    "two seconds, then lowers his arm.", 4.5),
    "haul_bag": ("A man grabs a heavy bag from the ground with both hands, lifts it to chest height and heaves it forward into a vehicle, then "
                 "straightens up.", 5.0),
    "one_finger": ("A man sitting on a bench turns his head to the side and raises his right hand above his shoulder with the index finger "
                   "extended, holding up one finger, then lowers it.", 4.0),
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*")
    ap.add_argument("--take", type=int, default=1)
    ap.add_argument("--seed", type=int, default=1984)
    a = ap.parse_args()
    procs = []
    for n in a.names or list(PROMPTS):
        out = ROOT / "RawAssets/wp05/ANIM_HYMOTION_SIGNALS" / f"{n}_t{a.take}"
        if (out / "result.json").exists():
            print("skip", n)
            continue
        prompt, dur = PROMPTS[n]
        args = {"prompt": prompt, "duration": dur, "seed": a.seed, "output_format": "fbx"}
        procs.append((n, subprocess.Popen([PY, str(ROOT / "Tools/fal/falcall.py"), "--wp", "WP05", "--id", "ANIM_HYMOTION_SIGNALS",
                                           "--endpoint", "fal-ai/hunyuan-motion", "--args-json", json.dumps(args), "--est-usd", "0.08",
                                           "--out", str(out)], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)))
    for n, p in procs:
        o, _ = p.communicate()
        print(n, "exit", p.returncode, o[-200:].replace("\n", " "))


if __name__ == "__main__":
    main()
