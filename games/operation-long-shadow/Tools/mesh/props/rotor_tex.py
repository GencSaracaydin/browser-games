#!/usr/bin/env python3
"""Procedural rotor blade + rotor blur textures for SM_HeliMainRotor / SM_HeliTailRotor (WP10_FAL).
  Tools/.venv/bin/python Tools/mesh/props/rotor_tex.py
T_HeliRotor_BC/M (256): weathered dark-grey blades (u = span 0..1, v = around the airfoil), faded yellow tip band, leading-edge
wear strip. T_HeliRotorBlur_BC (512 RGBA, radial UV centred): faint spinning-disc blur with the tip band ring."""
import pathlib
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter
R = pathlib.Path(__file__).resolve().parents[3] / "RawAssets/wp10/ready/Assets/_Game/Art/Textures/Vehicles/helicopter"
R.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(7)
n = 256
u = np.linspace(0, 1, n)[None, :].repeat(n, 0)     # span along x
v = np.linspace(0, 1, n)[:, None].repeat(n, 1)     # around the section along y (0.5 = leading edge)
noise = gaussian_filter(rng.standard_normal((n, n)), (1, 12)) * 0.02 + gaussian_filter(rng.standard_normal((n, n)), 2) * 0.01
bc = np.dstack([np.full((n, n), 0.085)] * 3) + noise[..., None]
tip = (u > 0.93) & (u < 0.985)
bc[tip] = np.array([0.55, 0.45, 0.12]) * (0.85 + noise[tip][:, None] * 4)
le = np.abs(v - 0.5) < 0.05                                   # leading-edge abrasion strip (bare metal)
bc[le & ~tip] = np.array([0.32, 0.32, 0.31]) + noise[le & ~tip][:, None]
Image.fromarray((np.clip(bc, 0, 1) * 255).astype(np.uint8)).save(R / "T_HeliRotor_BC.png")
m = np.dstack([np.where(le, 0.8, 0.15), np.ones((n, n)), np.zeros((n, n)), np.where(le, 0.45, 0.3)])
Image.fromarray((m * 255).astype(np.uint8), "RGBA").save(R / "T_HeliRotor_M.png")
N = 512
y, x = np.mgrid[0:N, 0:N]
r = np.hypot(x - N / 2 + 0.5, y - N / 2 + 0.5) / (N / 2)
ang = np.arctan2(y - N / 2, x - N / 2)
a = np.where(r < 0.065, 0, np.where(r > 0.995, 0, 0.13 + 0.07 * r))
a += np.where((r > 0.93) & (r < 0.985), 0.14, 0)               # tip band ring
a *= 0.85 + 0.15 * np.cos(2 * ang) ** 2                        # 2-blade beat in the blur
a = gaussian_filter(a, 1.0)
col = np.dstack([np.full((N, N), 0.09)] * 3)
ring = (r > 0.93) & (r < 0.985)
col[ring] = [0.5, 0.42, 0.14]
Image.fromarray((np.dstack([col, a]) * 255).clip(0, 255).astype(np.uint8), "RGBA").save(R / "T_HeliRotorBlur_BC.png")
print("rotor textures ok")
