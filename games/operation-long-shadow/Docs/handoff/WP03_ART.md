# WP03_ART handoff: weapon art (content package)

Status: **done. All 18 manifest ids are import-ready** (every `WPN_*` entry plus `PROC_SHELL_CASINGS`, P0 and P1). 79 files under `RawAssets/wp03/ready/Assets/_Game/Art/{Models,Textures}/Weapons/`, indexed in `RawAssets/wp03/ready/import_index.json`. Every model is inside its §8.9/manifest triangle budget and every texture inside its size cap (`import_index.py --check` passes). fal spend for WP03: **$29.53** (59 calls; this resumed run added $0.92 for the AKMS retake). No Unity editor access was used; nothing is in `Assets/` yet.

Resumed run (after the network outage): the earlier attempt had generated every reference and 3D take and installed the AK, RPK, Commando and casings. This run inventoried those and kept them. It rebuilt everything built before the back-face fix (AK, AKMS, DShK, RPG-7, rocket, M67). It re-generated the AKMS with Hunyuan and built the four P1 items (SVD, M60D, knife, smoke grenade). It added the high-to-low normal bake and the import index, fixed the generator geometry defects listed in §6, and fixed two review-tool bugs: framing of upright items, and LOD1/LOD2 being rendered on top of LOD0.

Contact sheets for every asset (Cycles, studio or desert HDRI, LOD0 unless the name says LODn): `RawAssets/wp03/_review/<Name>.png` (`AK, Commando, RPK, HushPuppy, AKMS, SVD, RPG7, RPG7_Rocket, DShK, M60D, M67, Knife, SmokeGrenade, Casings`), the world models of the FP guns in `<Name>_W.png`, and LOD checks in `AK_W_LOD1/2.png`, `RPG7_W_LOD2.png`, `DShK_W_LOD2.png`. The `_review` folder also holds earlier comparison sheets (`*_tripo`, `*_hy`, `*_desert`); they record which take was chosen and are not deliverables.

---

## 1. Inventory

Tris are LOD0 (+ split parts) as exported; FP models include the back-face copies (see §6). Textures: BC/N/M size in px.

| Manifest id | Files (`Art/Models/Weapons/<Folder>/`) | Tris | Textures (`Art/Textures/Weapons/<Folder>/`) | Source take |
|---|---|---|---|---|
| WPN_COMMANDO | `Commando/SM_WPN_Commando_FP.fbx`, `_W.fbx` | FP 19 993; W 5 989 / 3 038 / 1 358 | `T_WPN_Commando_{BC,N,M}` 2048/2048/1024 (W shares them) | Hunyuan v3.1 Pro (no-mag + right view), 40 k |
| WPN_COMMANDO_MAG | `Commando/SM_WPN_Commando_Mag.fbx` (+ `Mag` child of the FP model) | 1 488 | `T_WPN_Commando_Mag_{BC,N,M}` 512 | Tripo P2 |
| WPN_HUSHPUPPY | `HushPuppy/SM_WPN_HushPuppy_FP.fbx`, `_W.fbx` | FP 19 991; W 2 983 / 1 516 / 941 | `T_WPN_HushPuppy_{BC,N,M}` 2048/2048/1024 | Tripo P2 (no-mag edit), 20 k |
| WPN_HUSHPUPPY_MAG | `HushPuppy/SM_WPN_HushPuppy_Mag.fbx` (+ `Mag` child) | 793 | `T_WPN_HushPuppy_Mag_*` 512 | Tripo P2 |
| WPN_AK | `AK/SM_WPN_AK_FP.fbx`, `_W.fbx` | FP 19 994; W 5 988 / 3 030 / 1 351 | `T_WPN_AK_*` 2048/2048/1024 (W shares) | Hunyuan v3.1 Pro, reference reused from `RawAssets/fal3d` |
| WPN_AK_MAG | `AK/SM_WPN_AK_Mag.fbx` (+ `Mag` child) | 1 491 | `T_WPN_AK_Mag_*` 512 | Tripo P2 |
| WPN_RPK | `RPK/SM_WPN_RPK_FP.fbx`, `_W.fbx` | FP 19 968; W 6 937 / 3 659 / 1 761 | `T_WPN_RPK_*` 2048/2048/1024 (W shares) | Tripo P2 (no-mag edit), 20 k |
| WPN_RPK_MAG | `RPK/SM_WPN_RPK_Mag.fbx` (+ `Mag` child), 75-round drum | 2 478 | `T_WPN_RPK_Mag_*` 512 | Tripo P2 take 3 (face-on drum, mirrored half, 78 mm thick) |
| WPN_AKMS | `AKMS/SM_WPN_AKMS_W.fbx` | 5 994 / 2 997 / 1 318 | `T_WPN_AKMS_*` 1024, normal map baked high -> low | Hunyuan v3.1 Pro (retake), 40 k -> 6 k |
| WPN_SVD (P1) | `SVD/SM_WPN_SVD_W.fbx` | 6 993 / 3 496 / 1 540 | `T_WPN_SVD_*` 1024, normal map baked | Hunyuan v3.1 Pro, 40 k -> 7 k |
| WPN_RPG7 | `RPG7/SM_WPN_RPG7_W.fbx` (+ child `Warhead`) | 4 666 / 2 608 / 1 179 + Warhead 1 190 | `T_WPN_RPG7_*` 1024, normal map baked | Hunyuan v3.1 Pro, 40 k -> 6 k |
| WPN_RPG7_ROCKET | `RPG7/SM_WPN_RPG7_Rocket_W.fbx` | 1 493 / 749 | `T_WPN_RPG7_Rocket_*` 512 | Tripo P2 (cross-section rescaled to 93 mm) |
| WPN_DSHK | `DShK/SM_WPN_DShK_W.fbx` (+ child `Mount`) | 7 624 / 4 107 / 1 838 + Mount 288 | `T_WPN_DShK_*` 1024, normal map baked | Hunyuan v3.1 Pro, 40 k -> 8 k |
| WPN_M60D (P1) | `M60D/SM_WPN_M60D_FP.fbx`, `_W.fbx` | FP 19 992; W 7 993 / 3 998 / 1 759 | `T_WPN_M60D_*` 2048/2048/1024 (W shares) | Hunyuan v3.1 Pro, 40 k |
| WPN_M67 | `M67/SM_WPN_M67_FP.fbx`, `_W.fbx` | FP 2 991; W 1 189 / 636 | `T_WPN_M67_*` 1024/1024/512 | Tripo P2 (3/4 view) |
| WPN_KNIFE (P1) | `Knife/SM_WPN_Knife_FP.fbx` | 2 998 | `T_WPN_Knife_*` 1024/1024/512 | Tripo P2 |
| WPN_SMOKE_GRENADE (P1) | `Smoke/SM_WPN_SmokeGrenade_W.fbx` | 998 / 497 | `T_WPN_SmokeGrenade_{BC,N,M}` 512 (white band) + `T_WPN_SmokeGrenade_Green_BC` 512 (green band) | Tripo P2 (3/4 view) |
| PROC_SHELL_CASINGS (P1) | `Casings/SM_WPN_Casings.fbx`: `SM_WPN_Casing_556x45`, `_762x39` (lacquered steel), `_9x19`, `_762x51` | 60 each | `T_WPN_Casings_{BC,M}` 256 (no normal map) | procedural (Blender) |
| (shared) | — | — | `Weapons/_Shared/T_WPN_DetailScratches_N.png` 512 tiling detail normal (CC0 ambientCG Scratches004, the WPN_COMMANDO manifest note) | CC0 |

All guns are 1970s–80s patterns with no markings or text. The generators' pseudo-text was blurred out where it appeared (`demark` boxes on the Commando and its magazine). Real lengths: Commando 0.79 m, Hush Puppy 0.33, AK/AKMS 0.88, RPK 1.04, SVD 1.22, RPG-7 1.30, rocket 0.95, DShK 1.60, M60D 1.10, M67 0.09 (body height), knife 0.30, smoke can 0.145 (with fuze), casings at their real dimensions.

## 2. Conventions (all FBX)

- **Units and axes.** 1 unit = 1 m. Transforms are baked: every object imports with identity rotation and scale 1. Forward (barrel, blade, rocket nose) = Unity **+Z**, up = **+Y**, the weapon's right side (ejection port) = **+X**. Upright items (M67, smoke grenade) stand on +Y.
- **Origins (pivots).** Hand-held guns: the pistol grip (= `Socket_Grip`, which is (0,0,0)). M60D: the midpoint between the spade grips (`Grip` = right spade +0.06 m, `LeftHand` = left spade -0.06 m, as in WeaponTable). DShK: the pintle yoke. Knife: the middle of the handle (blade +Z, edge -Y). M67, smoke grenade, rocket: the object centre. Casings: the case centre (spin pivot), mouth +Z.
- **Magazines.** The FP model has a child mesh **`Mag`** whose pivot is the magazine-well entry (= `Socket_MagWell`). The stand-alone `SM_WPN_<Name>_Mag.fbx` is the same mesh with the same pivot, for drops, pickups and reload props. World models keep the magazine inside LOD0-2 as a second material slot. The M60D is belt-fed: its short brass belt is part of the body and there is no magazine.
- **Sockets.** Empties named `Socket_<Name>` are parented to the model root. The values below are model-space metres (x, y, z). The FP models carry all of them; the world models carry the subset the job lists (Muzzle and LeftHand on all; SightRear, Grip, Base and Pivot where noted).

| Weapon | Muzzle | SightFront | SightRear | LeftHand | Eject | MagWell | Extra |
|---|---|---|---|---|---|---|---|
| Commando | 0, .080, .529 | 0, .141, .370 | 0, .136, .029 | 0, .083, .257 | .020, .086, .092 | 0, .015, .117 | |
| Hush Puppy | 0, .065, .280 | 0, .080, .147 | 0, .082, -.003 | 0, -.026, .019 | .010, .072, .058 | 0, -.071, -.005 | |
| AK | 0, .063, .612 | 0, .102, .568 | 0, .109, .201 | 0, .054, .316 | .014, .071, .129 | 0, .034, .144 | |
| RPK | 0, .088, .758 | 0, .141, .722 | 0, .135, .209 | 0, .077, .307 | .022, .083, .063 | 0, .051, .158 | |
| AKMS (W) | 0, .080, .603 | — | 0, .130, .214 | 0, .080, .299 | — | — | |
| SVD (W) | 0, .076, .889 | — | 0, .160, -.070 (scope eyepiece) | 0, .082, .398 | — | — | |
| RPG-7 (W) | 0, .127, .203 (tube mouth) | — | 0, .229, -.141 | 0, .044, -.141 | — | — | Exhaust 0, .127, -.690 |
| DShK (W) | 0, .089, .943 | — | 0, .137, -.421 | -.055, .062, -.609 | — | — | Grip .055, .062, -.609; Base 0, -.317, .004 |
| M60D | 0, .016, 1.082 | 0, .117, .466 (ring sight) | 0, .117, .135 | -.06, 0, 0 | .027, -.004, .349 | — | Grip .06, 0, 0; Feed -.05, .032, .371; Pivot (pintle) 0, -.049, .295; Base 0, -.167, .306; GripCenter = origin |
| Knife | — | — | — | — | — | — | Tip 0, .005, .234; Guard 0, 0, .066 |
| PG-7 rocket | — | — | — | — | — | — | Nose 0, 0, .474; Tail 0, 0, -.474 |

  The M60D has no rear notch, so `SightRear` is a point on the level line through the ring-sight centre (ADS looks through the ring along the bore). The SightFront/SightRear pairs of the AK, RPK and Commando are the real front post and rear notch/aperture.
- **World models and LODs.** The root `SM_WPN_<Name>_W` (an empty) has children `_LOD0`, `_LOD1` and `_LOD2`, at about 50 % and 22 % of LOD0 (grenades, rocket and smoke can have 2 levels). Unity builds the LODGroup from the `_LODn` suffixes. **Split parts** (`Warhead` on the RPG-7: hidden after firing; `Mount` on the DShK: the post, which turns separately) are children outside the LOD chain with their own pivots. Add them to every LOD level's renderer list (or keep them in LOD0 and LOD1 only) so they do not float when the LODs cull.
- **Materials.** One material slot per mesh, named `M_WPN_<Name>` (magazine: `M_WPN_<Name>_Mag`; casings: `M_WPN_Casings`), so ModelMaterialBinder remaps by name. The world models of the Commando, Hush Puppy, AK, RPK and M60D use the FP UVs and the FP texture set (manifest: "world model ... shares the FP textures"). Their magazine part uses `M_WPN_<Name>_Mag`.
- **Textures (TECH_PLAN D4).** `_BC` sRGB RGB base colour. `_N` tangent-space normal, OpenGL (+Y up = Unity's convention), linear. `_M` mask, linear RGBA: **R metallic, G ambient occlusion (baked in Blender), B unused (0), A smoothness**; assign it to both `_MetallicGlossMap` and `_OcclusionMap`. The PNGs are lossless at their in-game size; Unity crunches them (q80, TextureImportRules `Weapons`).

## 3. Import instructions (editor holder)

1. Copy `RawAssets/wp03/ready/Assets/**` into the project `Assets/` with the same paths (models, textures, `Weapons/_Shared`). The WP00 import rules then apply by themselves:
   - Models (ModelImportRules, `Art/Models/Weapons`): Scale Factor 1 with Convert Units, Bake Axis Conversion off, Normals Import, Tangents Calculate Mikktspace, Materials Import via MaterialDescription (slot names kept), Animation None, Read/Write off, no lightmap UVs. `_FP`/`_Mag` files: Mesh Compression Off. `_W` files: Medium; Mesh LOD generation stays off because the LODs are authored.
   - Textures (TextureImportRules, `Art/Textures/Weapons`): `_N` = Normal map; `_M` = Default with sRGB off and alpha as data; caps 2048 for FP BC/N (Commando, Hush Puppy, AK, RPK, M60D), 1024 for FP M, the M67, the knife and the world-only guns, 512 for magazines, the rocket, the smoke can and the casings; `_Shared` = 512 with wrap Repeat. These match the PNG sizes delivered.
   `import_index.json` repeats these per file (`import_notes`), with the tris and sizes.
2. Run **LongShadow/Combat/Build All** (Docs/handoff/WP03.md §3). MaterialFactory builds `M_WPN_*` from the `T_WPN_*` sets, ModelMaterialBinder binds the slots, and WeaponPrefabBuilder picks up the art (`WeaponArt` names match these files: `Art/Models/Weapons/<Folder>/SM_WPN_<Name>_{FP,W,Mag}.fbx`, rocket `RPG7/SM_WPN_RPG7_Rocket_W`, casings `Casings/SM_WPN_Casings` per calibre object).
3. Two material follow-ups are requested in Docs/REQUESTS.md:
   - `M_WPN_SmokeGrenade_Green`: BC swapped to `T_WPN_SmokeGrenade_Green_BC`, for the green-band smoke in B10.
   - The detail normal `T_WPN_DetailScratches_N` on the FP gun materials: scale 0.35, tiling about 8.
4. Optional check: `Logs/combat_budget.txt` (Report Weapon Budgets) should show every FP gun at 20 k tris or fewer.

## 4. Tools (all in `Tools/mesh/weapons/`; Python from `Tools/.venv`, fal calls with `/Users/gencsaracaydin/Documents/fun/.venv/bin/python`; Blender 5.2 headless)

| Tool | What it does |
|---|---|
| `weapons.json` | Spec: prompts (manifest prompts plus era detail), real lengths, budgets, folders, edit prompts (no-mag, mag-only, right view). |
| `wpn_fal.py` | Every fal step through `Tools/fal/falcall.py --wp WP03`: `ref`, `edit <nomag/magonly/right>`, `tripo`, `hunyuan --back right`, `upload`. Idempotent: an existing `result.json` skips the call. |
| `jobs/WPN_*.json` | One job per deliverable (body GLB, reference views, socket pixels, pivot, magazine fit, budgets, LODs, splits, texture grading, geometry fixes). `*_tripo.json` / `*_hy.json` are the losing takes, kept for comparison. |
| `wpn_build.py <job> [--steps prep,blender,tex,project,pack,review,install]` | The per-weapon driver. Steps: `prep` (reference masks), `blender` (`bl_weapon.py`), `tex` (`glb_textures.py`), `project` (`bl_project.py`), `pack` (`tex_pack.py`), `review` (`review.py`), `install` (copies into `RawAssets/wp03/ready` and the report into `_reports/`). |
| `bl_weapon.py` | Import, orient, clean (merge by distance, loose geometry, floaters, winding), real-length scale, optional fixes (`fill_holes`, `recess`, `cap`, `flat_normals`, `smooth_boxes`), magazine fit into the well, sockets from image pixels, pivot. Decimates to budget with custom normals transferred from the source. Visible back faces get reversed copies (Unity culls back faces). AO bake, **high-to-low normal bake** (Cycles selected-to-active) for world-only models decimated by more than 75 %, LODs, split parts, FBX export. |
| `bl_backface.py` | Visible-back-face detection and repair (library). |
| `bl_project.py` | Projects the left/right reference photos onto the mesh UVs (silhouette-IoU fit), bakes the visibility weights and the `demark` masks. |
| `tex_pack.py` | Builds `T_*_{BC,N,M}`: the generator maps, the photo projection blended into BC (wood/bakelite at full weight, metal partly), wood/metal grading, demark blur, AO into M.G, smoothness into M.A, `--normal` (baked map), `--recolor` (colour variants), resize to budget. |
| `review.py` / `bl_render.py` | Contact sheets from the exported FBX re-imported (what ships). Views left/right/front34/rear34/top/iso/fp. Back faces the camera can see render magenta. Only LOD0 is rendered unless `--objects <name>_LODn`. `--hdri desert` uses the goegap golden-hour HDRI. |
| `bl_casings.py`, `bl_casings_review.py` | The procedural casings (60 tris each, real dimensions, 256 px brass/lacquered-steel atlas) and their review render. |
| `import_index.py [--check]` | Writes `ready/import_index.json` from the ready tree and the reports; `--check` fails on any budget overrun. |
| `glb_textures.py`, `img_tools.py`, `bl_inspect.py`, `bl_silhouette.py`, `contact.py` | Map extraction, reference image helpers (padref, endratio, magmask, inpaint), GLB inspection, silhouette overlay, contact sheets. |

Re-run a single weapon end to end (about 1–6 min, one Blender at a time):
```
cd /Users/gencsaracaydin/Documents/fun/fun-unity
Tools/.venv/bin/python Tools/mesh/weapons/wpn_build.py Tools/mesh/weapons/jobs/WPN_AK.json --steps prep,blender,tex,project,pack,review,install
Tools/.venv/bin/python Tools/mesh/weapons/import_index.py --check
```
Casings: `Blender -b --factory-startup -P Tools/mesh/weapons/bl_casings.py -- RawAssets/wp03/ready RawAssets/wp03/PROC_SHELL_CASINGS`. Working files per weapon are in `RawAssets/wp03/<ID>/<build dir from the job>/`: `<ID>_processed.blend`, `blender.log`, `process_report.json`, `proj/`, `tex_out/` (AO and normal bakes).

## 5. Deviations from the manifest / plan

- **FBX instead of OBJ** for every weapon, from Blender instead of `process_glb.sh`/`gltf_to_obj.py`/`pack_mask.py` (ORCHESTRATION §3, D2 refined). The mask packing follows D4.
- **Hunyuan v3.1 Pro instead of Tripo** for the AK, Commando, AKMS, SVD, RPG-7, DShK and M60D (40 k source; better geometry, compared on contact sheets). The RPG7_ROCKET and SMOKE_GRENADE manifest entries name Hunyuan Rapid; Tripo P2 was used (already generated, good result).
- **No top views.** Hunyuan used the right view as its back image, and Tripo is single-view. The manifest's "right-side and top views" for the hero gun became left plus right.
- **Normal maps.** World-only guns get a baked high-to-low map. The shared-texture world models (AK/RPK/Commando/HP/M60D) keep the FP maps over transferred normals. The Hush Puppy world model (20 k -> 3 k) is therefore an 85 % decimation without its own bake, because the manifest asks it to share the FP textures.
- **DShK has no gun shield.** The reference with the shield came out malformed, so the shield-less reference was used (common on technicals).
- **M60D sights.** A ring sight plus a synthetic level SightRear (§2).
- **Knife** is FP-only (the manifest variants are `ref`, `fp`). The **smoke grenade** variants are one mesh with two BC maps.
- **AK reference** reused from research (`RawAssets/fal3d/01_rifle_ref_image/rifle_ref.png`), as the manifest says. The research world model `rifle_tripo_p2_world_6k_1k.glb` was **not** used: the world model is decimated from the new FP mesh so it can share the FP UVs and textures.

## 6. Known issues

- **Generator geometry fixes.** These are applied automatically by the jobs:
  - The RPG-7 venturi was capped by the generator. It is now a shallow dish (`recess`) that reads as a flared nozzle from behind, but it is not hollow.
  - The smoke grenade's lid was painted black by Tripo. Its top faces now take the can colour, with flat normals (`cap`, mode `existing`).
- **Small visible back faces.** A few slivers remain inside the RPK's charging-handle slot and near its rear sight (magenta in the review). In Unity they show as tiny see-through gaps, visible only at FP distance on the right side.
- **The M60D receiver top** (seen from the door-gun eye point) carries the generator's soft texture: no photo projection reaches the top. The shared detail normal (request in REQUESTS.md) helps. A top-view projection (nano-banana top edit plus a `top` side in `bl_project.py`) would be the next quality step.
- **AKMS and SVD steel** is the generator's grey graded darker. The SVD's metallic stays low (the generator called the steel non-metal), so it reads slightly flat under a strong sun. Both are seen at 20–120 m.
- **Projection fit.** The DShK (IoU 0.75) and M60D (0.78) fits are looser than the rifles' (0.86–0.94): those meshes are taller than their references (generator proportions), so the photo detail is slightly offset on the mount and the spade grips. Sockets come from the reference pixels, so `Socket_Base` and `Socket_Pivot` on these two can be off by about 1–2 cm. Check them against the mesh in the editor before mounting.
- **Split parts and LODs.** The RPG-7 `Warhead` and the DShK `Mount` are outside the authored LOD chain (§2). Add them to the LODGroup levels in the prefab builder.
- **Upright items in the reviews.** The FP model of the M67 (and the smoke can) is centred; the hand offset is applied by WeaponPrefabBuilder (`BuildItem`), not by the art.
