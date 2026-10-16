#!/usr/bin/env python3
"""CONTENT_AUDIT gap fill for VEH_HELI_CABIN (P0, owner WP10; no builder existed in the code or content runs).

  Tools/.venv/bin/python -I Tools/audit/heli_cabin.py [--render]

1. Bakes T_HeliCabin_Gauges_{BC,E,N,M} (1024): instrument panel, overhead console and pedestal radios; round gauges with
   tick marks and needles, toggles, breakers; Latin numerals only (GDD §2.3), no words, no logos.
2. Runs Tools/audit/blender_heli_cabin.py (Blender, headless): builds SM_HeliCabin.fbx (cabin interior kit) and
   SM_Helicopter_DoorsOpen.fbx (WP10_FAL's SM_Helicopter with the baked dark door panels removed on every LOD), and with
   --render the review images in RawAssets/_audit/heli_cabin/.
3. Writes the index rows (RawAssets/wp10/ready/import_index_audit.json) and the material specs
   (RawAssets/wp10/ready/materials_audit.json) for the cabin's own materials; the tiling surfaces bind to the existing
   WP10 env materials by slot name (M_GreenMetalRust, M_MetalPlate, M_MetalPlates013).
"""
import argparse
import json
import math
import os
import subprocess
import sys

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "Tools", "tex"))
sys.path.insert(0, os.path.join(ROOT, "Tools", "audit"))
import wp10_texlib as T  # noqa: E402
from sign_bake import merge, height_to_normal, fbm  # noqa: E402

READY = os.path.join(ROOT, "RawAssets", "wp10", "ready")
WORK = os.path.join(ROOT, "RawAssets", "_audit", "heli_cabin")
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
TEXREL = "Assets/_Game/Art/Textures/Vehicles/heli_cabin"
MID = "VEH_HELI_CABIN"
F_COND = os.path.join(ROOT, "RawAssets/wp08/ready/Assets/_Game/Art/Fonts/BarlowCondensed-SemiBold.ttf")
F_MONO = os.path.join(ROOT, "RawAssets/wp08/ready/Assets/_Game/Art/Fonts/ShareTechMono-Regular.ttf")


def gauges():
    S = 1024
    ss = 2
    im = Image.new("RGB", (S * ss, S * ss), (22, 23, 22))
    em = Image.new("RGB", (S * ss, S * ss), (0, 0, 0))
    hm = Image.new("L", (S * ss, S * ss), 90)
    d, de, dh = ImageDraw.Draw(im), ImageDraw.Draw(em), ImageDraw.Draw(hm)
    rng = np.random.default_rng(84)
    f_small = ImageFont.truetype(F_COND, 15 * ss)
    f_win = ImageFont.truetype(F_MONO, 22 * ss)

    def P(u, v):  # uv (origin bottom-left) -> pixel
        return u * S * ss, (1 - v) * S * ss

    def gauge(cx, cy, r, ticks=12, numerals=True, red_arc=False):
        cx, cy, r = cx * ss, cy * ss, r * ss
        dh.ellipse([cx - r - 6 * ss, cy - r - 6 * ss, cx + r + 6 * ss, cy + r + 6 * ss], fill=170)
        dh.ellipse([cx - r, cy - r, cx + r, cy + r], fill=60)
        d.ellipse([cx - r - 6 * ss, cy - r - 6 * ss, cx + r + 6 * ss, cy + r + 6 * ss], fill=(48, 48, 46))
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(12, 12, 12))
        a0, a1 = math.radians(225), math.radians(-45)
        for i in range(ticks * 2 + 1):
            a = a0 + (a1 - a0) * i / (ticks * 2)
            L = 0.16 if i % 2 == 0 else 0.08
            p0 = (cx + math.cos(a) * r * 0.92, cy - math.sin(a) * r * 0.92)
            p1 = (cx + math.cos(a) * r * (0.92 - L), cy - math.sin(a) * r * (0.92 - L))
            for dd, col in ((d, (225, 222, 210)), (de, (150, 60, 45))):
                dd.line([p0, p1], fill=col, width=max(1, int(r * 0.03)))
            if numerals and i % 2 == 0 and i % 4 == 0:
                t = str(i // 2)
                q = (cx + math.cos(a) * r * 0.58, cy - math.sin(a) * r * 0.58)
                for dd, col in ((d, (210, 208, 196)), (de, (120, 50, 40))):
                    dd.text(q, t, font=f_small, fill=col, anchor="mm")
        if red_arc:
            d.arc([cx - r * 0.95, cy - r * 0.95, cx + r * 0.95, cy + r * 0.95], -45 - 40, -45, fill=(160, 30, 25), width=int(r * 0.06))
            d.arc([cx - r * 0.95, cy - r * 0.95, cx + r * 0.95, cy + r * 0.95], -45 - 110, -45 - 40, fill=(40, 120, 50), width=int(r * 0.06))
        a = a0 + (a1 - a0) * rng.uniform(0.0, 0.25)  # engines spooled down: needles near the stop
        for dd, col in ((d, (235, 232, 220)), (de, (170, 70, 50))):
            dd.line([(cx, cy), (cx + math.cos(a) * r * 0.8, cy - math.sin(a) * r * 0.8)], fill=col, width=max(2, int(r * 0.05)))
        d.ellipse([cx - r * 0.08, cy - r * 0.08, cx + r * 0.08, cy + r * 0.08], fill=(30, 30, 30))
        # glass glint
        d.arc([cx - r * 0.8, cy - r * 0.8, cx + r * 0.8, cy + r * 0.8], 200, 250, fill=(70, 72, 74), width=int(r * 0.05))

    # --- instrument panel: u 0.02..0.98, v 0.52..0.98 (panel 1.68 x 0.53 m)
    x0, y0 = P(0.02, 0.98)
    x1, y1 = P(0.98, 0.52)
    d.rectangle([x0, y0, x1, y1], fill=(34, 35, 34))
    for row, vy in enumerate((0.88, 0.66)):
        n = 9 if row == 0 else 8
        for i in range(n):
            u = 0.07 + i * (0.86 / (n - 1))
            if abs(u - 0.5) < 0.04:
                continue
            cx, cy = P(u, vy)
            gauge(cx / ss, cy / ss, 40 if row == 0 else 36, ticks=10 if i % 3 else 8, red_arc=(i % 4 == 1))
    # centre stack: a few square annunciators
    for j in range(4):
        cx, cy = P(0.5, 0.94 - j * 0.1)
        d.rectangle([cx - 30 * ss, cy - 14 * ss, cx + 30 * ss, cy + 14 * ss], fill=(60, 20, 16) if j % 2 else (60, 52, 18))
        d.rectangle([cx - 30 * ss, cy - 14 * ss, cx + 30 * ss, cy + 14 * ss], outline=(70, 70, 68), width=2 * ss)
    # --- overhead console: u 0.02..0.30, v 0.02..0.48: rows of breakers and toggles
    for r_ in range(7):
        for c_ in range(5):
            u = 0.04 + c_ * 0.05
            v = 0.44 - r_ * 0.06
            cx, cy = P(u, v)
            if r_ % 3 == 0:
                d.rectangle([cx - 8 * ss, cy - 14 * ss, cx + 8 * ss, cy + 14 * ss], fill=(45, 45, 44))
                d.ellipse([cx - 5 * ss, cy - 18 * ss, cx + 5 * ss, cy - 8 * ss], fill=(170, 170, 165))
                dh.ellipse([cx - 5 * ss, cy - 18 * ss, cx + 5 * ss, cy - 8 * ss], fill=230)
            else:
                d.ellipse([cx - 9 * ss, cy - 9 * ss, cx + 9 * ss, cy + 9 * ss], fill=(14, 14, 14))
                d.ellipse([cx - 5 * ss, cy - 5 * ss, cx + 5 * ss, cy + 5 * ss], fill=(205, 200, 190))
                dh.ellipse([cx - 9 * ss, cy - 9 * ss, cx + 9 * ss, cy + 9 * ss], fill=200)
    # --- pedestal radios: u 0.70..0.98, v 0.02..0.48: three radio heads with frequency windows and knobs
    for j in range(3):
        v0, v1 = 0.46 - j * 0.15, 0.33 - j * 0.15
        ax, ay = P(0.71, v0)
        bx, by = P(0.97, v1)
        d.rectangle([ax, ay, bx, by], fill=(30, 31, 30), outline=(62, 62, 60), width=2 * ss)
        wx, wy = P(0.74, v0 - 0.035)
        d.rectangle([wx, wy, wx + 120 * ss, wy + 34 * ss], fill=(6, 6, 6))
        freq = f"{rng.integers(118, 136)}.{rng.integers(0, 9)}{rng.choice([0, 5])}"
        d.text((wx + 8 * ss, wy + 4 * ss), freq, font=f_win, fill=(205, 200, 185))
        de.text((wx + 8 * ss, wy + 4 * ss), freq, font=f_win, fill=(160, 70, 40))
        for k_ in range(3):
            kx, ky = P(0.79 + k_ * 0.06, v1 + 0.035)
            d.ellipse([kx - 16 * ss, ky - 16 * ss, kx + 16 * ss, ky + 16 * ss], fill=(12, 12, 12))
            d.line([kx, ky, kx + 12 * ss, ky - 8 * ss], fill=(200, 200, 190), width=2 * ss)
            dh.ellipse([kx - 16 * ss, ky - 16 * ss, kx + 16 * ss, ky + 16 * ss], fill=240)
    bc = np.asarray(im.resize((S, S), Image.LANCZOS)).astype(np.float32) / 255.0
    e = np.asarray(em.resize((S, S), Image.LANCZOS)).astype(np.float32) / 255.0
    h = np.asarray(hm.resize((S, S), Image.LANCZOS)).astype(np.float32) / 255.0
    # wear: dust in the corners, scuffs
    dust = np.clip((fbm((S, S), 3, 60, 4) - 0.55) * 1.5, 0, 1)[..., None]
    bc = bc * (1 - 0.2 * dust) + np.array([0.32, 0.29, 0.24]) * 0.2 * dust
    n = height_to_normal(cv2.GaussianBlur(h, (0, 0), 1.0), 3.0)
    smooth = np.where(h < 0.3, 0.75, 0.35).astype(np.float32)  # gauge glass glossy, panel paint satin
    m = T.pack_mask(np.zeros((S, S), np.float32), np.ones((S, S), np.float32), smooth)
    os.makedirs(os.path.join(READY, TEXREL), exist_ok=True)
    rows = []
    for suf, arr, typ, note in (
            ("BC", bc, "texture_basecolor", "Instrument faces (panel, overhead, pedestal) for SM_HeliCabin slot M_HeliCabin_Gauges: Default, "
             "sRGB ON, Max Size 1024, DXT1 Crunched q60, wrap Clamp, mips on. Latin numerals only (GDD §2.3-3); no words or logos."),
            ("E", e, "texture_emissive", "Instrument lighting (red-white markings): sRGB ON, Max Size 512, DXT1 Crunched q60, wrap Clamp. "
             "M_HeliCabin_Gauges emission x0.6 at dusk (Beat 11), off in daylight (insertion)."),
            ("N", n, "texture_normal", "Normal map, OpenGL (+Y), sRGB OFF, Max Size 1024, DXT5nm Crunched q60."),
            ("M", m, "texture_mask", "Mask D4, sRGB OFF, Max Size 1024, DXT5 Crunched q60: gauge glass glossy, panel paint satin.")):
        rel = f"{TEXREL}/T_HeliCabin_Gauges_{suf}.png"
        T.save8(arr, os.path.join(READY, rel))
        rows.append({"manifest_id": MID, "file": rel, "target_path": rel, "type": typ, "import_notes": note, "tool": "audit_heli_cabin"})
    return rows


def specs():
    tex = lambda s: f"{TEXREL}/T_HeliCabin_Gauges_{s}.png"  # noqa: E731
    mp = "Assets/_Game/Art/Textures/Env/MetalPlates013/T_MetalPlates013"
    fab = "Assets/_Game/Art/Textures/Env/Fabric066/T_Fabric066"
    return [
        {"manifest_id": MID, "material": "M_HeliCabin_Gauges", "target_path": "Assets/_Game/Art/Materials/Vehicles/M_HeliCabin_Gauges.mat",
         "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic",
         "textures": {"_BaseMap": tex("BC"), "_BumpMap": tex("N"), "_MetallicGlossMap": tex("M"), "_OcclusionMap": tex("M")},
         "floats": {"_Metallic": 1.0, "_Smoothness": 1.0}, "emission": {"_EmissionMap": tex("E"), "_EmissionColor_linear": [0.6, 0.6, 0.6, 1]},
         "assign_to_slot": "M_HeliCabin_Gauges", "note": "Instrument faces (0..1 UVs)."},
        {"manifest_id": MID, "material": "M_HeliCabin_Dark", "target_path": "Assets/_Game/Art/Materials/Vehicles/M_HeliCabin_Dark.mat",
         "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic",
         "textures": {"_BaseMap": mp + "_BC.png", "_BumpMap": mp + "_N.png", "_MetallicGlossMap": mp + "_M.png", "_OcclusionMap": mp + "_M.png"},
         "floats": {"_Metallic": 1.0, "_Smoothness": 0.8}, "colors": {"_BaseColor": [0.17, 0.17, 0.16, 1]}, "tile_m": 2.0,
         "assign_to_slot": "M_HeliCabin_Dark", "note": "Flat-black painted metal (seat shells, consoles, gun posts, trims): MetalPlates013 maps tinted dark; metric UVs."},
        {"manifest_id": MID, "material": "M_HeliCabin_Canvas", "target_path": "Assets/_Game/Art/Materials/Vehicles/M_HeliCabin_Canvas.mat",
         "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic",
         "textures": {"_BaseMap": fab + "_BC.png", "_BumpMap": fab + "_N.png", "_MetallicGlossMap": fab + "_M.png", "_OcclusionMap": fab + "_M.png"},
         "floats": {"_Metallic": 1.0, "_Smoothness": 1.0}, "colors": {"_BaseColor": [0.62, 0.6, 0.42, 1]}, "tile_m": 0.5,
         "assign_to_slot": "M_HeliCabin_Canvas", "note": "Olive-drab canvas (troop bench, gunner seats, cockpit cushions, ceiling blankets): Fabric066 maps tinted; metric UVs."},
        {"manifest_id": MID, "material": "M_HeliCabin_Glass", "target_path": "Assets/_Game/Art/Materials/Vehicles/M_HeliCabin_Glass.mat",
         "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic", "textures": {}, "floats": {"_Metallic": 0.0, "_Smoothness": 0.93},
         "colors": {"_BaseColor": [0.32, 0.36, 0.34, 0.16]}, "surface": "Transparent", "blend": "Alpha", "render_face": "Both",
         "assign_to_slot": "M_HeliCabin_Glass", "note": "Windshield, chin and side windows: transparent, both faces, no shadow casting (set on the renderer)."},
        {"manifest_id": MID, "material": "M_HeliCabin_RedLamp", "target_path": "Assets/_Game/Art/Materials/Vehicles/M_HeliCabin_RedLamp.mat",
         "shader": "Universal Render Pipeline/Lit", "workflow": "Metallic", "textures": {}, "floats": {"_Metallic": 0.0, "_Smoothness": 0.7},
         "colors": {"_BaseColor": [0.35, 0.03, 0.03, 1]}, "emission": {"_EmissionColor_linear": [6.0, 0.25, 0.15, 1]},
         "assign_to_slot": "M_HeliCabin_RedLamp", "note": "Red cabin lamp lens; pair with a red point light at Mark_RedLamp (GDD §8.7 'the cabin goes red')."},
    ]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--render", action="store_true")
    a = ap.parse_args()
    os.makedirs(WORK, exist_ok=True)
    rows = gauges()
    cmd = [BLENDER, "-b", "--factory-startup", "-t", "2", "-P", os.path.join(ROOT, "Tools/audit/blender_heli_cabin.py"), "--",
           "--ready", READY, "--out", WORK] + (["--render"] if a.render else [])
    r = subprocess.run(cmd, capture_output=True, text=True)
    stats_line = [ln for ln in r.stdout.splitlines() if ln.startswith("STATS")]
    if r.returncode != 0 or not stats_line:
        print(r.stdout[-4000:], r.stderr[-4000:])
        sys.exit(1)
    stats = json.loads(stats_line[0][6:])
    print("blender:", stats)
    cab = "Assets/_Game/Art/Models/Vehicles/heli_cabin/SM_HeliCabin.fbx"
    heli = "Assets/_Game/Art/Models/Vehicles/helicopter/SM_Helicopter_DoorsOpen.fbx"
    rows += [
        {"manifest_id": MID, "file": cab, "target_path": cab, "type": "model_fbx", "tool": "audit_heli_cabin", "import_notes": (
            f"Helicopter cabin interior kit (Huey-style, 1984; procedural Blender, {stats['cabin_tris']} tris, budget 8000). FBX = WP10 "
            "convention: Scale Factor 1, Convert Units ON (1 unit = 1 m), Bake Axis Conversion OFF, identity rotation, +Z = nose, +X = right, "
            "pivot = cabin floor centre under the door-gun pivots (GDD §8.0 cabin frame). Parent under SM_Helicopter (or _DoorsOpen) at local "
            "(0, 1.00, 2.375). Normals Import (smooth tubes, hard boxes), Tangents Calculate Mikk, Mesh Compression Medium, Read/Write off, "
            "Generate Lightmap UVs OFF (moving vehicle, light probes), Animation None, Mesh LOD generation OFF (single LOD: only seen up close). "
            "Materials: Import via MaterialDescription so the slot names survive; ModelMaterialBinder binds them by name: M_GreenMetalRust "
            "(skin, ribs, ceiling), M_MetalPlate (floor tread), M_MetalPlates013 (tubes, rails), "
            "M_HeliCabin_Canvas/_Dark/_Glass/_RedLamp/_Gauges (materials_audit.json). UVs are metric (1 UV = 1 m) to match the env materials' "
            "tile_m. Empties: Socket_DoorGunPivot_L/R (-/+1.05, 1.10, 0.30) for the M60D Socket_Pivot (WP03 request), Mark_PlayerEye, "
            "Mark_Hollis, Mark_Sparrow, Mark_Tinker, Mark_RedLamp (lamp hangs at 1.60: the Tripo hull roof is ~1.75 above the floor, "
            "so GDD's 1.90 would poke through), Mark_PilotL/R (Magpie in the right seat). Colliders: box per seat/post + floor plane "
            "(builder). Cargo door openings z -0.38..0.98, y 0.06..1.44 both sides; troop bench on the rear bulkhead; gunner seats "
            "inboard of each post facing outboard (seat pan 0.45 m, eye 1.25 m).")},
        {"manifest_id": MID, "file": heli, "target_path": heli, "type": "model_fbx", "tool": "audit_heli_cabin", "import_notes": (
            "Doors-open copy of WP10_FAL's SM_Helicopter (same UVs, materials, LODs and pivot; WP10 FBX convention, Scale Factor 1, "
            "identity rotation, +Z nose, pivot bottom centre): the baked dark cargo-door panels are deleted on both sides on every LOD "
            f"({stats['door_faces_removed']} faces) so SM_HeliCabin shows through. Use it wherever the cabin is visible (insertion I-1..I-12, "
            "the LZ boarding, Beat 11 exfil); keep SM_Helicopter for distant fly-bys. Material slot M_Helicopter (MaterialFactory). "
            "Contains SM_Helicopter_DoorsOpen_LOD0..3: Unity builds the LODGroup, turn Mesh LOD generation off. Rotors parent as for "
            "SM_Helicopter (main hub local (0.0043, 4.5374, 2.1207), tail hub (-0.5188, 3.9917, -5.5921)). Tris per LOD: "
            + ", ".join(f"{k} {v}" for k, v in stats["heli_tris"].items()) + ".")},
    ]
    merge(os.path.join(READY, "import_index_audit.json"), rows)
    merge(os.path.join(READY, "materials_audit.json"), specs())
    print(len(rows), "rows")


if __name__ == "__main__":
    main()
