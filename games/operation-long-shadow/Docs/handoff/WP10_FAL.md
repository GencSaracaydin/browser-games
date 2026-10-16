# WP10_FAL handoff: fal-generated props, vehicles, vegetation, textures and decals

Status: **done** for every manifest id WP10_FAL owns (31 FAL_ props, 3 VEH_, 3 VEG_, 6 TEX_, 6 DCL_, IMG_CONCEPT_KEYFRAMES = 50 ids, P0 + P1 + P2).
fal spend: **$60.75 of the $200 WP10FAL cap** (131 ledger rows, run 1 + run 2 after the outage). No editor/MCP use.

## 1. Where things are
| What | Path |
|---|---|
| Import-ready files (mirror of the Unity paths) | `RawAssets/wp10/ready/Assets/_Game/Art/...` (shared with WP10_CC0; no file overlaps) |
| Import index (218 rows: manifest_id, file, target_path, type, import_notes, tool) | `RawAssets/wp10/ready/import_index_fal.json` |
| Material specs (59 materials, same schema as `materials_cc0.json` + decal/foliage extras) | `RawAssets/wp10/ready/materials_fal.json` |
| Raw generations (refs, GLB/OBJ, patina maps, gpt sheets), per id | `RawAssets/wp10/fal/<ID>/` (`ref_t*.png`, `tripo_*`, `hyr_*`, `hyp_*`, `work_<tag>/`, `ready_*.json`) |
| Review sheets (look at these first) | `RawAssets/wp10/fal/_review/FINAL_*.png` (props 1-3, P1/P2 props, vehicles, trees); per take `<ID>__<tag>.png`, textures `TEX_*__patina_t*.png`, decals `*__atlas.png` |
| Measured mount points (helicopter hubs, DShK pedestal) | `RawAssets/wp10/fal/_vehicle_extras/vehicle_points.json` + check renders |
| Resume notes | `RawAssets/wp10/fal/_PROGRESS.md`, job lists/logs in `RawAssets/wp10/fal/_jobs/` |

## 2. Inventory (what ships)
Model conventions for every FBX: Blender 5.2 export, forward -Z / up Y, baked space transform, FBX_SCALE_ALL -> **Scale Factor 1, 1 unit = 1 m, identity rotation, front faces Unity +Z**, pivot bottom-centre unless noted (wall items: back face centre). Files hold `SM_<Name>_LOD0..n` meshes (single-LOD files just `SM_<Name>`): build the LODGroup from them and turn the importer's Mesh LOD generation off for those files. Import Materials = None; `materials_fal.json` lists what MaterialFactory should build (URP/Lit, `_M` = R metal, G AO, B 0, A smoothness, used as both Metallic and Occlusion map).

| Id | Mesh (LOD0 tris) | Textures (in game) | Notes |
|---|---|---|---|
| FAL_SANDBAG | SM_Sandbag 600 | 512 | retake t2: Tripo 8k -> 600 + re-bake (burlap weave in N) |
| FAL_CABLE_DRUM | 2276 / 1138 / 569 | 512 (1024 file) | |
| FAL_DIESEL_PUMP | 5000 / 2500 / 1250 | 512 (1024 file) | |
| FAL_MARKET_STALL | 2418 / 1209 / 604 | 512 (1024 file) | canvas shade is single-sided geometry: Render Face Both |
| FAL_FRUIT_CRATE_ORANGE / _POMEGRANATE | 2962 / 2788 (+LOD1-2) | 512 | long side runs along Unity Z |
| FAL_ORANGE / FAL_POMEGRANATE | 300 / 140 | 256 | physics fruit for the market crates (G3) |
| FAL_TELETYPE | 2758 | 1024 (hero) | |
| FAL_TUBE_RACK | 4752 | 1024 + `_E` 512 | emissive valves (orange), HDR 3-6 |
| FAL_WALL_SAFE | 2900 | 1024 | pivot = front face plane, bottom edge (body sits in the wall) |
| FAL_DRILL_RIG | 3000 | 512 (1024 file) | Hunyuan 3D Pro take (has the feed handle) |
| FAL_PA_HORN | 1999 | 512 | |
| FAL_FILM_CAN | SM_FilmCan 1478, SM_FilmCanClosed 1472 | 512 | open (reel visible) + closed variant |
| FAL_CINEMA_SEAT | 1546 | 512 | |
| FAL_LEDGER_STACK / FAL_CODEBOOK | 999 / 600 | 512 | |
| FAL_SATCHEL_CHARGE / FAL_BOLT_CUTTER | 1200 / 1200 | 512 | hand props (Tinker) |
| FAL_FUEL_BOWSER | 4895 | 512 | |
| FAL_PRC77 | 2816 | 1024 | |
| FAL_CAMERA_35MM / FAL_DIVE_WATCH | 1701 / 1500 | 512 | |
| FAL_TYPEWRITER, FAL_MG_TRIPOD, FAL_WOODEN_BENCH | 2776 / 1500 / 1074 | 512 | P1 |
| FAL_ON_AIR_LAMP | 822 | 512 + `_E` | P1, wall pivot, red emissive glass |
| FAL_MEAL_PLATE, FAL_WALL_SPEAKER, FAL_KETTLE, FAL_DRILL_BAG | 1499 / 812 / 1012 / 1200 | 512 | P1/P2; speaker has wall pivot |
| VEH_TECHNICAL | 12359 / 6179 / 3089 / 1483 | BC 2048, N/M 1024 + burnt BC/M | DShK pedestal top at local (-0.136, 1.838, -1.026) m |
| VEH_CARGO_TRUCK | SM_CargoTruck 11592, SM_CargoTruckTarpOff 12151 (4 LODs each) | 1024 + burnt BC/M (tarp version) | 6.7 m long |
| VEH_HELICOPTER | SM_Helicopter 14364 (4 LODs), SM_HeliMainRotor (324 + 32 blur), SM_HeliTailRotor (196 + 32) | BC 2048, N/M 1024; rotor 256; blur 512 RGBA | main hub local (0.004, 4.537, 2.121), tail hub (-0.519, 3.992, -5.592) |
| VEG_OLIVE_TREE | SM_OliveTree_A/B/C: 6056 / 7036 / 6196 LOD0, LOD1 ~4k, LOD2 ~1.9k | bark 512, leaves BC 1024 + N/M 512, impostor 256 each | procedural, 4.5 m, crown ~6 m |
| VEG_POMEGRANATE_TREE | SM_PomegranateTree_A/B/C: ~5.2k / 2.8k / 1.2k | same | procedural multi-stem, 3.0 m, fruit |
| VEG_DRY_GRASS_CARDS | SM_DryGrass (8 tuft objects, 6 tris each) | T_DryGrass_BC 1024 RGBA | alpha-cut cards |
| TEX_SALTCRUST / TEX_STONE_PAVING | tiling 1024, + `_H`; M.B = height (Terrain Layer mask) | tile 2.5 m / 2.0 m | |
| TEX_AWNING_STRIPES / TEX_MUDBRICK / TEX_VELVET / TEX_CINEMA_CARPET | tiling 1024 | tile 1.6 / 1.6 / 0.3 / 0.8 m | awning: Render Face Both |
| DCL_BULLET_HOLES (16), DCL_SCORCH (4), DCL_BLOOD_SMALL (6) | BC RGBA + N | 1024 | layout json with UV rects + real sizes |
| DCL_GRIME (8), DCL_CHALK_DRAWINGS (6) | BC RGBA | 1024 | |
| DCL_POSTER_ART (3 posters) | BC RGBA 1536x768 | | blank title band per poster for SignBaker |
| IMG_CONCEPT_KEYFRAMES | concept_keyframes.png 2048 wide (5 frames) | | **editor-only** look-dev reference |

Download estimate (Crunch rule of thumb, `build_index.py`): ~33 MB for everything above (vehicles ~8.7 MB incl. burnt variants and the tarp-off truck; trees ~2.5 MB; props ~14 MB; env textures ~5.8 MB; decals ~2.9 MB). The burnt variants (~1.2 MB) are optional if `BudgetReport` is tight.

## 3. Import instructions (editor holder)
1. Copy `RawAssets/wp10/ready/Assets/_Game/**` rows listed in `import_index_fal.json` into `Assets/` (same paths). Every row's `import_notes` gives texture type, sRGB, max size, crunch, wrap and model settings; they follow TECH_PLAN §3.10/§3.11 (`TextureImportRules` keyed by `_BC/_N/_M/_E/_H`), with these exceptions to set by hand:
   - vehicles: technical/helicopter `_BC` Max Size 2048, `_N/_M` 1024;
   - `T_*_Leaves_BC`, `T_DryGrass_BC`, `T_*_Impostor_BC`, decals: Alpha Is Transparency ON, Mip Maps Preserve Coverage ON (foliage, cutoff 0.5), wrap Clamp;
   - `T_Decal_PosterArt_BC` is 1536x768 (keep Max Size 2048);
   - `concept_keyframes.png`: no mips, keep out of Addressables.
2. Models: Scale Factor 1, Convert Units on, Normals Import (trees and grass carry authored normals and vertex colours: **do not recalculate**, import vertex colours), tangents Calculate Mikk, Animation None, Materials None. LODGroup screen heights suggested in each row.
3. Materials: build from `materials_fal.json` (URP/Lit metallic workflow, `_SmoothnessTextureChannel` 0 = Metallic Alpha). Trees have two slots (0 bark, 1 leaves); leaves = URP/Lit Alpha Clipping 0.5, Render Face Both, or `SG_FoliageWind` (vertex colour R sway, G phase, B flutter). Decals = `Shader Graphs/Decal` (Affect Normal only where a `_N` exists).
4. Prefab hints: helicopter = SM_Helicopter + SM_HeliMainRotor at the main hub + SM_HeliTailRotor at the tail hub (spin Y / X; blades below ~150 rpm, blur discs above); technical = SM_Technical + WPN_DSHK at the pedestal top; swap `M_TechnicalBurnt` / `M_CargoTruckBurnt` after the fireball (E4-01, E11).

## 4. Tools (all in `Tools/mesh/props/`, Python = `Tools/.venv/bin/python`, fal = `/Users/gencsaracaydin/Documents/fun/.venv/bin/python`)
| Tool | What / rerun |
|---|---|
| `wp10fal.py` | every paid call (through `Tools/fal/falcall.py --wp WP10FAL`): `ref ID`, `edit`, `3d ID --image .. --tag .. --engine tripo|hy_rapid|hy_pro [--faces N]`, `patina ID`, `gptimg ID`, `nb ID`, `batch jobs.json`. Idempotent per tag (result.json). Config `wp10fal_assets.json` (name, size, tris, tex, engine, prompts). |
| `process_prop.py` | GLB/OBJ -> FBX + T_BC/N/M(/E) + review sheet: `process_prop.py ID --tag tripo_t1 [--variant v --vname Name] [--scratch] [--proc key]`. Per-asset overrides (yaw, pivot, colour, cage, remesh...) in `wp10fal_proc.json`. Uses `glb_extract.py`, `blender_prop.py` (align/scale/pivot, decimate, re-bake, AO, LODs, export), `tex_pack.py`, `blender_render.py`. `--scratch` writes under the work folder only. |
| `make_tree.py` + `tree_tex.py` + `blender_tree.py` + `blender_tree_render.py` | trees: `tree_tex.py VEG_OLIVE_TREE` (leaf atlas + bark), then `make_tree.py VEG_OLIVE_TREE --variants A,B,C` (FBX, impostors, renders, rows). |
| `patina_pack.py` | `patina_pack.py TEX_ID --take patina_tN` (calibrated BC/N/M(/H) + sheet); `--compare TEX_ID t1 t2` for review only. |
| `decal_atlas.py` | `decal_atlas.py DCL_ID ...` (haze removal, darken alpha, crater feathering, colour bleed, layout json, normal from luminance). |
| `image_plates.py` | `posters` (DCL_POSTER_ART atlas) and `keyframes` (IMG_CONCEPT_KEYFRAMES sheet). |
| `burnt_variant.py` | `burnt_variant.py Technical` / `CargoTruck`. |
| `rotor_tex.py`, `blender_vehicle_extras.py` | rotor textures; rotors + measured mount points + check renders (`Blender -b --factory-startup -P blender_vehicle_extras.py -- --ready RawAssets/wp10/ready --out RawAssets/wp10/fal/_vehicle_extras`). |
| `blender_grass_cards.py` | dry-grass card meshes from the atlas layout. |
| `build_index.py` | collects `RawAssets/wp10/fal/*/ready_*.json` -> `import_index_fal.json` + `materials_fal.json`, checks files exist, prints the size estimate. Rerun after any reprocess. |
| `review_rows.py`, `contact_sheet.py` | review images. |

## 5. Deviations from the manifest (also in `Docs/DECISIONS.md`)
- Trees are procedural (space colonisation + fal leaf/bark textures), 3 variants each, not Hunyuan meshes (the generated canopies were blobs).
- Hunyuan Rapid items moved to Tripo P2 (service flaky; PBR output lost the base colour); FAL_DRILL_RIG uses Hunyuan 3D Pro; FAL_POMEGRANATE stays Hunyuan Rapid (enable_pbr=false, voxel remesh before decimation, 140 tris).
- Extras beyond the manifest: burnt texture variants (technical, cargo truck), helicopter rotors + blur discs, tree impostors, dry-grass card meshes, closed film can, decal layout jsons.
- DCL_POSTER_ART atlas is 1536x768; hero vehicles BC 2048.
- No date palm (none in manifest/GDD).

## 6. Known issues / for the next pass
- Helicopter: Tripo baked the open cabin doors as dark panels on a closed shell; the cabin interior is the separate VEH_HELI_CABIN kit, so at the LZ the door region needs the kit inside (or a cut-out) for the boarding shot.
- Generated meshes have fragmented UV atlases (Tripo); fine for unique props, but no tiling detail. A detail normal (e.g. CC0 hessian for sandbags, painted metal for vehicles) in the material would add close-up texture.
- FAL_SATCHEL_CHARGE reads as a plain canvas bag (the fuse igniter is tiny); FAL_DRILL_RIG keeps some dark/black grime from the Hunyuan texture.
- Tree LOD0 olive B is 7.0k tris + cards (budget 8k); trees have no collider data beyond the suggested trunk capsule.
- Size: vehicles ~8.7 MB vs 6 MB planned in §8.10 (two truck variants + burnt sets); drop the burnt sets or the tarp-off truck textures to 512 if needed.
- `FAL_ON_AIR_LAMP` glass base colour is a light red; its emission map makes it read deep red when lit.
