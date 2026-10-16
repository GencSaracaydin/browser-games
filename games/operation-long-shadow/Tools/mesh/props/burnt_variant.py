#!/usr/bin/env python3
"""Burnt-out texture variant of a vehicle (same mesh and UVs, material swap after the fireball). WP10_FAL extra.

  Tools/.venv/bin/python Tools/mesh/props/burnt_variant.py Technical [--kind Vehicles --folder technical] [--seed 3]

From T_<Name>_BC / _M writes T_<Name>Burnt_BC / _M next to them: paint cooked off to dark heat-blued / rust-brown steel,
soot that is heaviest in occluded areas (baked AO in M.G), ash-grey patches, black glass and rubber, very low smoothness.
Noise is evaluated per texel in UV space with a low-frequency field plus AO, so island seams stay unobtrusive under soot.
Normal map is shared (T_<Name>_N).
"""
import argparse, json, pathlib
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
READY = ROOT / "RawAssets/wp10/ready"
RAW = ROOT / "RawAssets/wp10/fal"


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("name"); ap.add_argument("--kind", default="Vehicles")
    ap.add_argument("--folder"); ap.add_argument("--seed", type=int, default=3); ap.add_argument("--id")
    a = ap.parse_args()
    folder = a.folder or {"Technical": "technical", "CargoTruck": "cargo_truck", "CargoTruckTarpOff": "cargo_truck"}[a.name]
    tdir = READY / f"Assets/_Game/Art/Textures/{a.kind}/{folder}"
    bc = np.asarray(Image.open(tdir / f"T_{a.name}_BC.png").convert("RGB")).astype(np.float32) / 255
    m = np.asarray(Image.open(tdir / f"T_{a.name}_M.png").convert("RGBA")).astype(np.float32) / 255
    if bc.shape[:2] != m.shape[:2]:   # hero BC is 2048, mask 1024: the burnt set is made at the mask size
        bc = np.asarray(Image.fromarray((bc * 255).astype(np.uint8)).resize(m.shape[1::-1], Image.LANCZOS)).astype(np.float32) / 255
    H, W = bc.shape[:2]
    rng = np.random.default_rng(a.seed)
    lum = bc @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    ao = m[..., 1]
    n1 = gaussian_filter(rng.standard_normal((H, W)).astype(np.float32), 40); n1 /= n1.std() + 1e-6
    n2 = gaussian_filter(rng.standard_normal((H, W)).astype(np.float32), 6); n2 /= n2.std() + 1e-6
    n3 = gaussian_filter(rng.standard_normal((H, W)).astype(np.float32), 1.2); n3 /= n3.std() + 1e-6
    # base: heat-cooked steel, dark brown-grey to rust, keeps a hint of the original value pattern (panel detail)
    rust = np.array([0.30, 0.17, 0.10], np.float32)
    scorched = np.array([0.075, 0.065, 0.06], np.float32)
    ash = np.array([0.36, 0.34, 0.32], np.float32)
    t_rust = np.clip(0.45 + 0.25 * n1 + 0.15 * n2, 0, 1)[..., None]
    base = scorched * (1 - t_rust) + rust * t_rust
    base = base * (0.75 + 0.5 * lum[..., None])                      # keep panel/edge detail from the original
    soot = np.clip(1.15 - ao + 0.25 * n2 + 0.1 * n3, 0, 1)[..., None]  # occluded -> sootier
    out = base * (1 - 0.8 * soot) + np.array([0.03, 0.03, 0.03]) * 0.8 * soot
    ashm = np.clip((n1 * 0.6 + n2 * 0.5 - 1.1) * 1.5, 0, 1)[..., None] * (ao[..., None] > 0.6)
    out = out * (1 - ashm * 0.7) + ash * ashm * 0.7
    # originally very dark or very bright-neutral texels (glass, tyres, chrome) -> black char
    sat = (bc.max(-1) - bc.min(-1)) / np.maximum(bc.max(-1), 1e-4)
    glassy = ((lum < 0.12) | ((sat < 0.08) & (lum > 0.7)))[..., None]
    out = np.where(glassy, np.array([0.025, 0.024, 0.023]) * (0.8 + 0.4 * np.clip(n3[..., None] * 0.3 + 0.5, 0, 1)), out)
    out = np.clip(out + 0.012 * n3[..., None], 0, 1)
    # mask: metal partially (bare cooked steel reads metallic in rust-free spots), AO kept, smoothness very low
    metal = np.clip(0.35 * (1 - t_rust[..., 0]) * (1 - soot[..., 0]), 0, 1)
    smooth = np.clip(0.12 + 0.08 * (1 - soot[..., 0]) + 0.03 * n2, 0.04, 0.3)
    mo = np.dstack([metal, ao, np.zeros_like(ao), smooth])
    pb = tdir / f"T_{a.name}Burnt_BC.png"; pm = tdir / f"T_{a.name}Burnt_M.png"
    Image.fromarray((np.clip(out, 0, 1) ** (1 / 1.0) * 255 + 0.5).astype(np.uint8)).save(pb)
    Image.fromarray((mo * 255 + 0.5).astype(np.uint8), "RGBA").save(pm)
    rel = lambda p: str(p.relative_to(READY))
    aid = a.id or {"Technical": "VEH_TECHNICAL", "CargoTruck": "VEH_CARGO_TRUCK", "CargoTruckTarpOff": "VEH_CARGO_TRUCK"}[a.name]
    rows = [{"manifest_id": aid, "file": rel(pb), "target_path": rel(pb), "type": "texture",
             "import_notes": f"Burnt-out variant base colour for SM_{a.name} (same UVs): sRGB ON, Max Size 1024, DXT1 Crunched. Material "
             f"M_{a.name}Burnt = URP/Lit with T_{a.name}Burnt_BC, T_{a.name}Burnt_M (metallic+occlusion, smoothness from alpha) and the "
             f"shared T_{a.name}_N. Swap to it 1-2 s into the fuel fire (burning technical E4-01; E11 trucks after their fireball).",
             "tool": "burnt_variant"},
            {"manifest_id": aid, "file": rel(pm), "target_path": rel(pm), "type": "texture",
             "import_notes": "Burnt variant mask: R metallic, G AO, B 0, A smoothness (0.05-0.3). sRGB OFF, Max Size 1024, DXT5 Crunched.",
             "tool": "burnt_variant"}]
    (RAW / aid / f"ready_{a.name}Burnt.json").write_text(json.dumps(rows, indent=1))
    print("burnt ok", pb)


if __name__ == "__main__":
    main()
