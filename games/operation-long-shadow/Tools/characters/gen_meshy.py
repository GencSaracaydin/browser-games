#!/usr/bin/env python3
"""Meshy rifle clip sets via fal-ai/meshy/rigging/multi-animation (through Tools/fal/falcall.py), WP05.

  PY=/Users/gencsaracaydin/Documents/fun/.venv/bin/python
  $PY Tools/characters/gen_meshy.py ANIM_MESHY_SET_A 585 165 425 685 694 184 182 239 426 171
Input mesh: RawAssets/wp05/ANIM_MESHY_SET_A/calder_rig_input.glb (Calder/Rocketbox Military_Male_01 body, 4k tris,
made by Tools/characters/blender/to_glb.py) so the Meshy rig has Rocketbox proportions (best Humanoid retarget onto
the Rocketbox bodies). Ids are checked against RawAssets/wp05/_downloads/meshy_docs/meshy_animation_library.json.
Output: RawAssets/wp05/<SET>/take1/ (rigged character, basic walk/run, one FBX+GLB per clip)
"""
import json, pathlib, subprocess, sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
PY = "/Users/gencsaracaydin/Documents/fun/.venv/bin/python"
LIB = json.loads((ROOT / "RawAssets/wp05/_downloads/meshy_docs/meshy_animation_library.json").read_text())
INPUT = ROOT / "RawAssets/wp05/ANIM_MESHY_SET_A/calder_rig_input.glb"
URLCACHE = ROOT / "RawAssets/wp05/ANIM_MESHY_SET_A/calder_rig_input.url"


def main():
    set_id = sys.argv[1]
    ids = [int(x) for x in sys.argv[2:]]
    take = 1
    out = ROOT / "RawAssets/wp05" / set_id / f"take{take}"
    if (out / "result.json").exists():
        print("exists", out)
        return
    for i in ids:
        if str(i) not in LIB:
            sys.exit(f"id {i} not in the Meshy library listing")
        print(i, LIB[str(i)]["name"])
    if URLCACHE.exists():
        url = URLCACHE.read_text().strip()
    else:
        sys.path.insert(0, str(ROOT / "Tools/fal"))
        import falcall
        falcall.load_key()
        import fal_client
        url = fal_client.upload_file(str(INPUT))
        URLCACHE.write_text(url)
    args = {"model_url": url, "height_meters": 1.80, "animation_action_ids": ids, "enable_safety_checker": True}
    est = round(0.20 + 0.12 * len(ids), 2)
    out.mkdir(parents=True, exist_ok=True)
    (out / "clip_names.json").write_text(json.dumps({str(i): LIB[str(i)]["name"] for i in ids}, indent=1))
    r = subprocess.run([PY, str(ROOT / "Tools/fal/falcall.py"), "--wp", "WP05", "--id", set_id, "--endpoint",
                        "fal-ai/meshy/rigging/multi-animation", "--args-json", json.dumps(args), "--est-usd", str(est),
                        "--out", str(out), "--timeout", "2400"], capture_output=True, text=True)
    print(r.stdout[-1500:], r.stderr[-1500:])
    sys.exit(r.returncode)


if __name__ == "__main__":
    main()
