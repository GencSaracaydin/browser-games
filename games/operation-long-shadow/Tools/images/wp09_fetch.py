#!/usr/bin/env python3
"""WP09 CC0 downloader for the booth set (Poly Haven models, ambientCG PaintedPlaster012) and the Google Fonts
entries owned by WP08 (O1 approved).

  Tools/.venv/bin/python -I Tools/images/wp09_fetch.py [--ids PH_TELEVISION_01,...] [--fonts] [--force]

Reuses the download helpers of Tools/cc0/wp10_fetch.py (md5/size checks, whitelist zip extraction, research
cache reuse) with the raw folder pointed at RawAssets/wp09/<ID>/. Fonts go to RawAssets/wp08/<ID>/ from
github.com/google/fonts (OFL/Apache licence files kept next to the font files). Writes provenance.json per id.
"""
import argparse, importlib.util, json, os, pathlib, subprocess, sys, time

ROOT = pathlib.Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("wp10_fetch", ROOT / "Tools/cc0/wp10_fetch.py")
F = importlib.util.module_from_spec(spec)
spec.loader.exec_module(F)
F.RAW = str(ROOT / "RawAssets/wp09")
F.UA = "fun-unity-wp09-fetch"

BOOTH = ["PH_TELEVISION_01", "PH_TELEVISION_02", "PH_WALL_CLOCK", "PH_VINTAGE_LIGHTER",
         "PH_MOUNTED_FLUORESCENT_LIGHTS", "TEX_ACG_PAINTEDPLASTER012"]
GH = "https://raw.githubusercontent.com/google/fonts/main/"
FONTS = {  # id -> list of (repo path, licence path)
    "UI_FONT_TYPEWRITER": [("apache/specialelite/SpecialElite-Regular.ttf", "apache/specialelite/LICENSE.txt"),
                           ("ofl/courierprime/CourierPrime-Regular.ttf", "ofl/courierprime/OFL.txt"),
                           ("ofl/courierprime/CourierPrime-Bold.ttf", None)],
    "UI_FONT_CONDENSED": [("ofl/barlowcondensed/BarlowCondensed-Medium.ttf", "ofl/barlowcondensed/OFL.txt"),
                          ("ofl/barlowcondensed/BarlowCondensed-SemiBold.ttf", None),
                          ("ofl/barlowcondensed/BarlowCondensed-Regular.ttf", None)],
    "UI_FONT_MONO": [("ofl/sharetechmono/ShareTechMono-Regular.ttf", "ofl/sharetechmono/OFL.txt"),
                     ("ofl/ibmplexmono/IBMPlexMono-Regular.ttf", "ofl/ibmplexmono/OFL.txt")],
}


def fetch_fonts(force):
    for fid, files in FONTS.items():
        d = ROOT / "RawAssets/wp08" / fid
        d.mkdir(parents=True, exist_ok=True)
        got = []
        for f, lic in files:
            for path in [f] + ([lic] if lic else []):
                name = os.path.basename(path)
                if path == lic:
                    name = path.split("/")[1] + "_" + name
                dest = str(d / name)
                st = F.curl(GH + path, dest, None, None, force)
                got.append({"url": GH + path, "file": name, "status": st})
        prov = {"manifest_id": fid, "source": "google_fonts (github.com/google/fonts)", "fetched": time.strftime("%Y-%m-%dT%H:%M:%S"),
                "license": "SIL OFL 1.1 / Apache 2.0 (licence files kept alongside)", "approval": "O1 approved (Docs/ORCHESTRATION.md)",
                "files": got, "tool": "Tools/images/wp09_fetch.py"}
        (d / "provenance.json").write_text(json.dumps(prov, indent=1))
        print(fid, [g["file"] + ":" + g["status"] for g in got])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids"); ap.add_argument("--fonts", action="store_true"); ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    if a.fonts:
        fetch_fonts(a.force)
        if not a.ids:
            return
    man = {e["id"]: e for e in json.load(open(ROOT / "Docs/ASSET_MANIFEST.json"))}
    for mid in (a.ids.split(",") if a.ids else BOOTH):
        e = man[mid]
        t0 = time.time()
        got = F.fetch_acg(e, a.force) if e["source"] == "ambientcg" else F.fetch_ph_model(e, a.force)
        prov = {"manifest_id": mid, "source": e["source"], "asset_id": e["model_or_asset_id"], "license": "CC0 1.0",
                "fetched": time.strftime("%Y-%m-%dT%H:%M:%S"), "wall_s": round(time.time() - t0, 1), "list_price_usd": 0.0,
                "files": got, "tool": "Tools/images/wp09_fetch.py (helpers from Tools/cc0/wp10_fetch.py)"}
        os.makedirs(os.path.join(F.RAW, mid), exist_ok=True)
        json.dump(prov, open(os.path.join(F.RAW, mid, "provenance.json"), "w"), indent=1)
        print(mid, len(got), "files", round(time.time() - t0, 1), "s", flush=True)


if __name__ == "__main__":
    main()
