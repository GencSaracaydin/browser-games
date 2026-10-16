# WP10_CC0 handoff: CC0 textures, HDRI skies, Poly Haven props, decals, procedural FX textures

**Status: done for every owned P0/P1/P2 entry except three SignBaker decals (see "Not delivered").** fal spend: **$0.00** of the $15 WP10CC0 cap. Content agent, no editor use. Last run 2026-10-16 (resume pass after the network outage; all earlier outputs were inventoried, kept, reviewed and fixed in place).

## Where everything is
| What | Path |
|---|---|
| Import-ready files (mirror of the Unity tree) | `RawAssets/wp10/ready/Assets/_Game/Art/...` (shared with WP10_FAL; WP10_CC0 owns `Textures/Env/**`, `Textures/Decals/**` listed below, `Textures/FX/**`, `HDRI/**`, and the `Models/Props` + `Textures/Props` folders of the PH_* ids) |
| Import index (429 rows, 123 manifest ids) | `RawAssets/wp10/ready/import_index_cc0.json` — `{manifest_id, file, target_path, type, import_notes, tool}` (+ `lod_tris`, `size_m` on models) |
| Material specs for MaterialFactory (124) | `RawAssets/wp10/ready/materials_cc0.json` — env sets (URP/Lit + tiling + Terrain Layer), props (per slot), decals (Shader Graphs/Decal + projector size) |
| Raw downloads (+ `provenance.json` per id) | `RawAssets/wp10/cc0/<MANIFEST_ID>/` (fonts: `RawAssets/wp10/cc0/_fonts/` with OFL.txt + provenance) |
| Contact sheets (all reviewed) | `RawAssets/wp10/cc0/_contact/` — `TEX_*.jpg` (BC 2x2 / relit 2x2 / half-offset seam crop / N + M channels), `PROP_*.jpg` (Blender Eevee renders of the shipped FBX + shipped textures: front, back, LOD1, LOD2, BC), `HDRI_*.jpg`, `DCL_*.jpg` (+ `*_preview.jpg` composites), `VFX_*.jpg`, `PAINTOUT_*.jpg` (before/after of every guardrail paint-out), `SHARED_DETAIL.jpg` |
| Reports | `RawAssets/wp10/cc0/_reports/texsets.json` (per set: size, tile, albedo/smoothness/AO stats, seam scores, fixes applied), `_reports/props/<ID>.json` (source tris, LOD tris, sizes, parts, bakes) |

## Import instructions (editor holder)
1. Copy `RawAssets/wp10/ready/Assets/` over the project `Assets/` (only the files listed in `import_index_cc0.json`; WP10_FAL has its own index). Refresh once.
2. **Textures** follow TECH_PLAN §3.10 by suffix; the row's `import_notes` is authoritative where it is more specific:
   - `T_*_BC` sRGB ON, DXT1 Crunched q55 (DXT5 when the PNG has alpha: SheetMetal001, Fabric044, Wicker010B carry opacity → URP/Lit Alpha Clipping 0.5), wrap Repeat, aniso 4 (terrain layers 8).
   - `T_*_N` Texture Type **Normal map**, **OpenGL (+Y)** — never flip green.
   - `T_*_M` sRGB **OFF**: R metallic, G AO, **B height** (only the 4 terrain-layer sets sand_01, rocky_trail_02, dry_ground_rocks, aerial_sand; 0 elsewhere), A smoothness. Assign the same texture to `_MetallicGlossMap` **and** `_OcclusionMap`, Smoothness Source = Metallic Alpha, `_Smoothness` 1, `_Metallic` 1 (D4). For Terrain Layers the same `_M` is the mask map.
   - `T_Sand01_H` (only height map that ships) → `_ParallaxMap`, scale 0.012. `T_*_A` (hessian, rebar concrete, wood peeling paint) are optional Poly Haven paint masks, unused by URP/Lit.
   - Hero 2048 sets: sand_01, painted_plaster_wall, concrete_floor_worn_001 (PaintedPlaster012 is WP09's). Everything else 1024; prop maps 512 (hero props binoculars, filmstrip projector, large iron gate, rock_face_01 at 1024).
   - Shared: `Env/_Shared/T_MacroVariation_BC` (Detail Albedo x2, sRGB 0.5 neutral, world tiling 1/25 m, Max Size 512), `T_DetailPlaster_N` / `T_DetailRock_N` (Detail Normal, tiling 1/0.6 m).
3. **Materials:** run MaterialFactory over `materials_cc0.json` (`M_<Name>` → `Art/Materials/{Env,Props,Decals}/`). Env specs carry `tile_m` / `world_uv_tiling` for world-UV kit meshes and a `terrain_layer` block (`TL_<Name>.terrainlayer`, tile size, height blend) for the 4 terrain sets. Props: slot names in the FBX equal the material names (`M_<Name>[_<Part>]`); Barrel_01 has no default skin — use `M_Barrel01_OxideGreen` or `_OxideGrey` on slot `M_Barrel01`. Glass/film slots are Transparent alpha 0.28, no shadow casting. Lamps (caged/hanging/ceiling fan) have `_E` maps, emission off by default (PracticalLight drives it).
4. **Models (FBX, Blender 5.2):** Scale Factor 1, Convert Units ON, **Bake Axis Conversion OFF** (axes are baked: Y up, root rotation 0), Normals Import, Tangents Mikktspace, Materials None, Mesh Compression Medium, Animation/Rig None. Files with `*_LOD0..2` children create a LODGroup automatically (suggested 50/20/4 %): **turn Unity Mesh LOD generation OFF for them**. LOD0-only files are articulated or tiny (gate leaves, fan blades, projector reels, floodlight glass, binocular strap, notepad items are pivoted children — names and pivots in each row's notes). Generate Lightmap UVs only for PH_COVERED_CAR, PH_ROCK_FACE_01, PH_NAMAQUALAND_BOULDER_04 (D8: the rest use light probes). Multi-file props: old_military_crate `_A` (closed) / `_B` (open), dry_branches `_A/_B/_C`, metal_trash_can `_Clean/_Rust`, wooden_ladder `SM_WoodenStepLadder` (roof dressing) + `SM_WoodenLadderStraight` (3.6 m, ladder L).
5. **HDRI / sky:** `HDRI/Runtime/T_Sky_<id>_LDR.png` (2048x1024, sRGB, wrap U Repeat / V Clamp, DXT1 Crunched q70) → `M_Sky_PanoBlend` A/B; **pre-rotated, `_Rotation` = 0** puts the sun/glow at az 258°. They are scene-linear (not filmic): exposure = `exposure_multiplier_per_unit_sun_intensity` x I_sun from `<id>_sky.json` (also: sun elevation/colour/irradiance, SH L2 ambient, horizon/zenith hex for fog). `HDRI/EditorOnly/<id>_nosun_2k.exr` = Cube / Lat-Long, bakes + AmbientSHCapture + reflection-probe bakes only; **never reference from shipped content**. P0 skies: kiara_8_sunset (GOLDEN/SUNSET), kiara_9_dusk (DUSK); kiara_1_dawn / rogland_sunset are P2 fallbacks.
6. **Decals:** URP Decal Projector + `Shader Graphs/Decal` (`M_Decal_*` specs incl. projector sizes). BC alpha = opacity, wrap Clamp. `_M` only for a custom MAOS decal graph. Atlases (leaks, asphalt damage, tape, company lettering) list their UV rects in the BC row notes.
7. **FX:** `Textures/FX/T_VFX_*` flipbooks/atlases (layouts, blend modes, fps and HDR multipliers in the notes) and `T_LensDirt_BC` (VP_Base Bloom dirt, intensity 0.8).
8. **Terrain003:** `Env/Terrain003/T_Terrain003_H.png` (16-bit) + `Terrain003_height_2048_r16.bytes` + json (500 x 500 m, 200 m range, row 0 = north): **EditorOnly** input for the far-hills / ridge mesh generator; Read/Write ON; keep out of bundles.

## Size (download estimate, §8.9 basis)
Env sets: P0 ≈ 35.7 MB (3 x 2048 hero + 29 x 1024 + extras; budget 45 MB), P1 ≈ 14.8 MB, P2 ≈ 4.4 MB. Prop texture sets: P0 ≈ 8.3 MB, P1 ≈ 4.4 MB, P2 ≈ 2.1 MB (+ meshes). Two sky textures ≈ 2 x 0.7 MB. P1/P2 go in only while BudgetReport shows headroom (§8.10). Source PNGs in `ready/` total ≈ 460 MB uncompressed (Crunch does the shipping compression).

## Tools (all idempotent; run from the repo root; Python in `Tools/.venv`, `-I` because they read downloaded files)
| Step | Command | Does |
|---|---|---|
| 1 fetch | `Tools/.venv/bin/python -I Tools/cc0/wp10_fetch.py [--ids ..] [--priority P0] [--force]` (`--list` for the work list) | Poly Haven / ambientCG downloads → `RawAssets/wp10/cc0/<ID>/` + provenance; reuses `RawAssets/cc0/` research files; skips WP09 ids and D17 items |
| 2 texture sets | `Tools/.venv/bin/python -I Tools/tex/wp10_texset.py [--ids TEX_..]` | BC/N/M(/H/A) packing, resize in linear light, ground flatten, devegetation, sand_01 clone-stamp, chroma flatten, colour grades, AO floor; writes index rows + env material specs + `_reports/texsets.json` |
| 3 extras | `Tools/.venv/bin/python -I Tools/tex/wp10_extras.py [--only decals,lens,terrain,shared]` | ambientCG decals (+ tyre-track alpha from displacement, leak re-tint atlas), lens dirt, Terrain003 heightmap, shared detail/macro maps |
| 4 skies | `Tools/.venv/bin/python -I Tools/tex/hdri_prep.py [--ids HDRI_..]` | sun measure + paint-out, rotation to az 258°, land removal (smooth skyline envelope), scene-linear LDR + EditorOnly EXR + json |
| 5 paint-outs | `Tools/.venv/bin/python -I Tools/tex/wp10_paintout.py` | guardrail inpainting of brands/words on prop BC **and normal** maps → `<ID>/clean/` + `_contact/PAINTOUT_*` |
| 6 props (Blender) | `/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -t 2 -P Tools/cc0/wp10_ph_model.py -- --ids PH_..` | glTF → transforms baked → variants/parts → decimate to budget → LOD1/2 → FBX (Unity axes) → re-import check; Cycles re-bake for the two heavy scans; config `Tools/cc0/wp10_props.json` |
| 6b AO bake (Blender) | `... -P Tools/cc0/wp10_ao_bake.py -- --ids PH_METAL_TRASH_CAN,PH_OFFICE_NOTEPADS` | AO for ARM maps that ship with an empty AO channel → `<ID>/ao_bake/` |
| 7 prop textures | `Tools/.venv/bin/python -I Tools/tex/wp10_props_tex.py [--ids PH_..]` | packs prop maps at budget size (clean BC/N, AO bake fallback, `smooth_scale`, `devegetate` from the config), index rows + prop material specs |
| 8 previews | `... Blender ... -P Tools/cc0/wp10_prop_preview.py -- --ids PH_..` then `Tools/.venv/bin/python -I Tools/tex/wp10_prop_sheet.py --ids PH_..` | Eevee renders of the shipped FBX/materials → `_contact/PROP_*.jpg` |
| 9 VFX | `Tools/.venv/bin/python -I Tools/tex/wp10_vfx_flipbook.py [--only ..]` | seeded numpy flipbooks `T_VFX_*` |
| 10 signs | `Tools/.venv/bin/python -I Tools/tex/wp10_signs.py [--only company,ring]` | company lettering (Limelight OFL) + STAND CLEAR ring |
| 11 decal specs | `Tools/.venv/bin/python -I Tools/tex/wp10_decal_specs.py` | `M_Decal_*` specs into `materials_cc0.json` |
| 12 contact sheets | `Tools/.venv/bin/python -I Tools/tex/wp10_contact.py [--ids ..]` | `_contact/TEX_*.jpg` from the shipped files |
Shared helpers: `Tools/tex/wp10_texlib.py` (colour-space-correct resize, normal encode/decode, mask packing, flatten/grade/devegetate/clone-stamp/AO floor).

## Deviations from the manifest
- `PH_WOODENCHAIR_01` → Poly Haven `painted_wooden_chair_02` (WoodenChair_01 is a 2.27 m Gothic throne with church tracery, GDD §2.3-2). `PH_DRAWER_CABINET` → `vintage_wooden_drawer_01` (drawer_cabinet is a 2010s loft shelf). Same ids, folders named after the new assets.
- `PH_WOODEN_LADDER` is an A-frame step-ladder: shipped as dressing + a procedural 3.6 m straight ladder `SM_WoodenLadderStraight` on the same texture set (ladder L).
- `PH_FILMSTRIP_PROJECTOR_8MM` scaled x1.3 to read as a 16 mm portable projector (`SM_Projector16mm`). `PH_STEEL_FRAME_SHELVES_01` scaled x0.1 (source authored 10x too large).
- `PH_NAMAQUALAND_BOULDER_04` (110k) and `PH_DEAD_TREE_TRUNK` (101k) decimated to 8k with maps re-baked in Cycles onto fresh UVs (no fal remesh needed). `PH_DEAD_TREE_TRUNK` is a lying 3 m log, not a standing snag.
- `PH_BARREL_01`: only the oxide_green / oxide_grey skins ship (default skin = hazard art, radiation skin = trefoil). Rollershutter graffiti variants dropped (D17).
- Height maps dropped everywhere except `T_Sand01_H`; the 4 terrain sets keep height in `_M.B`. Terrain003 colour map dropped (green biome).
- Look/realism edits (logged in DECISIONS.md): AO floor on 5 ground sets, devegetation/clone-stamp on desert grounds, chroma flatten on dry_ground_rocks, aerial_sand palette match, CorrugatedSteel006A sun-fade, floor_tiles_08 flatten, rock_07/09 smoothness x0.4/x0.5, moss re-tint on rock_face_01 / branches / trunk, leak decals re-tinted from green algae to dry grime/rust.
- Guardrail paint-outs (D17 spirit, GDD §2.3-3) on ammo_box, cassette_player (+ tape shell emboss), filmstrip projector, projector screen, old_military_crate, portable_generator (all labels + embossed model name), portable_searchlight (+ emboss), propane_tank, utility_box_01, clipboard (emboss), wooden_crate_02 (company monogram). Generic numerals/codes and operating pictograms stay.
- `DCL_COMPANY_LETTERING`, `DCL_STAND_CLEAR_RING` baked offline instead of by SignBaker (SignBaker may replace them).

## Not delivered (owned, deferred)
- `DCL_SARDANI_SIGNS`, `DCL_MARQUEE_TITLES` (P0) and `DCL_POSTERS_COMPOSED` (P1): need WP08's Sardani glyph font (`Tools/tex/glyph_font.py` output does not exist yet) and the WP10_FAL `DCL_POSTER_ART` plates; they are SignBaker (editor, TMP → RenderTexture) jobs for WP10 Look/Level. `wp10_signs.py` can be extended with the font once it exists (same weathering code).

## Known issues / notes for the next agents
- `PH_ROCK_07` / `PH_ROCK_09` ship at their real scan size (0.32 m / 0.15 m) with 13.9k / 11.6k LOD0 tris (manifest budget). Use them scaled 3-15x as ridge/wadi rocks; for pebble scatter use the LOD2 mesh or Unity Mesh LOD.
- `PH_VINTAGE_RADIO_TRANSCEIVER` (21k), `PH_LARGE_IRON_GATE` (19k, LOD0 only), `PH_FILMSTRIP_PROJECTOR_8MM` (16k, LOD0 only) are at the manifest budgets but heavy; fine as hero/near props.
- rock_face_01 is an open-backed scan shell: embed it in terrain/kit, never show the back.
- Dark olive lichen remains on rock_face_01 (not green enough for the re-tint); reads as desert lichen.
- Sky textures have no land below ~0° (sky extrapolated, lower hemisphere fades to a smooth horizon colour): the far hills/ridge must come from game geometry (Terrain003 mesh); fog should match `horizon_mean_srgb_hex` in the sky json.
- Seam scores > 2 in `texsets.json` (terrazzo_tiles N, CorrugatedSteel006A N, brown_planks_03, floor_tiles_08, beige_wall_001, old_linoleum, SheetMetal001 N) were checked visually: they are tile/plank grout lines on the wrap edge or near-flat normals, not seams.
- `T_MacroVariation_BC` source is 1024 but imports at Max Size 512 (low-frequency map; intentional).

## Inventory (shipped manifest ids; files = rows in the index)

**decal**

| id | P | source | folder (under Art/) | files | size / LOD tris |
|---|---|---|---|---|---|
| DCL_COMPANY_LETTERING | P0 | procedural | Textures/Decals/company_lettering | 3 | 1024x512 |
| DCL_STAND_CLEAR_RING | P0 | procedural | Textures/Decals/stand_clear_ring | 1 | 1024x1024 |
| DCL_LEAKS | P1 | Leaking004 / Leaking007 / Leaking009 | Textures/Decals/leaks | 3 | 1024x1024 |
| DCL_SCRATCHES | P1 | Scratches004 | Textures/Decals/scratches | 3 | 1024x1024 |
| DCL_TIRE_TRACKS | P1 | TireTracks001 | Textures/Decals/tire_tracks | 3 | 1024x256 |
| DCL_ASPHALT_DAMAGE | P2 | AsphaltDamageSet001 | Textures/Decals/asphalt_damage | 3 | 1024x1024 |
| DCL_FINGERPRINTS | P2 | Fingerprints002 | Textures/Decals/fingerprints | 3 | 1024x1024 |
| DCL_TAPE | P2 | Tape006 | Textures/Decals/tape | 3 | 1024x1024 |

**hdri**

| id | P | source | folder (under Art/) | files | size / LOD tris |
|---|---|---|---|---|---|
| HDRI_KIARA_8_SUNSET | P0 | kiara_8_sunset | HDRI/Runtime | 3 | 2048x1024 |
| HDRI_KIARA_9_DUSK | P0 | kiara_9_dusk | HDRI/Runtime | 3 | 2048x1024 |
| HDRI_KIARA_1_DAWN | P2 | kiara_1_dawn | HDRI/Runtime | 3 | 2048x1024 |
| HDRI_ROGLAND_SUNSET | P2 | rogland_sunset | HDRI/Runtime | 3 | 2048x1024 |

**prop**

| id | P | source | folder (under Art/) | files | size / LOD tris |
|---|---|---|---|---|---|
| PH_AMMO_BOX | P0 | ammo_box | Models/Props/ammo_box | 4 | LODs 2190/1094/546 |
| PH_BARREL_01 | P0 | Barrel_01 | Models/Props/Barrel_01 | 7 | LODs 2682/1340/669 |
| PH_BINOCULARS | P0 | binoculars | Models/Props/binoculars | 4 | LODs 9613 |
| PH_CASSETTE_PLAYER | P0 | cassette_player | Models/Props/cassette_player | 7 | LODs 2415/1206/603 |
| PH_FILMSTRIP_PROJECTOR_8MM | P0 | filmstrip_projector_8mm | Models/Props/filmstrip_projector_8mm | 4 | LODs 16319 |
| PH_HANGING_INDUSTRIAL_LAMP | P0 | hanging_industrial_lamp | Models/Props/hanging_industrial_lamp | 5 | LODs 8000/4000/1999 |
| PH_LARGE_IRON_GATE | P0 | large_iron_gate | Models/Props/large_iron_gate | 4 | LODs 19070 |
| PH_METAL_OFFICE_DESK | P0 | metal_office_desk | Models/Props/metal_office_desk | 4 | LODs 6898/3448/1724 |
| PH_OLD_MILITARY_CRATE | P0 | old_military_crate | Models/Props/old_military_crate | 5 | LODs 5237/2617/1308; 5237/2617/1308 |
| PH_PAINTED_WOODEN_TABLE | P0 | painted_wooden_table | Models/Props/painted_wooden_table | 4 | LODs 600 |
| PH_PORTABLE_GENERATOR | P0 | portable_generator | Models/Props/portable_generator | 4 | LODs 13179/6588/3294 |
| PH_PROJECTOR_SCREEN | P0 | projector_screen | Models/Props/projector_screen | 7 | LODs 8727/4363/2180 |
| PH_ROCK_07 | P0 | rock_07 | Models/Props/rock_07 | 4 | LODs 13914/6956/3478 |
| PH_ROCK_09 | P0 | rock_09 | Models/Props/rock_09 | 4 | LODs 11640/5820/2910 |
| PH_ROCK_FACE_01 | P0 | rock_face_01 | Models/Props/rock_face_01 | 4 | LODs 10087/5043/2520 |
| PH_ROLLERSHUTTER_DOOR | P0 | rollershutter_door | Models/Props/rollershutter_door | 4 | LODs 552 |
| PH_ROLLERSHUTTER_WINDOW_01 | P0 | rollershutter_window_01 | Models/Props/rollershutter_window_01 | 4 | LODs 552 |
| PH_SECURITY_LIGHT | P0 | security_light | Models/Props/security_light | 4 | LODs 4668 |
| PH_WOODENCHAIR_01 | P0 | painted_wooden_chair_02 (swap) | Models/Props/painted_wooden_chair_02 | 4 | LODs 1246 |
| PH_WOODEN_CRATE_01 | P0 | wooden_crate_01 | Models/Props/wooden_crate_01 | 4 | LODs 6576/3288/1644 |
| PH_WOODEN_LADDER | P0 | wooden_ladder | Models/Props/wooden_ladder | 5 | LODs 156; 8492/4246/2123 |
| PH_WOODEN_TABLE_02 | P0 | wooden_table_02 | Models/Props/wooden_table_02 | 4 | LODs 196 |
| PH_BARREL_03 | P1 | barrel_03 | Models/Props/barrel_03 | 4 | LODs 1473 |
| PH_CAGED_HANGING_LIGHT | P1 | caged_hanging_light | Models/Props/caged_hanging_light | 5 | LODs 11446/5723/2861 |
| PH_CEILING_FAN | P1 | ceiling_fan | Models/Props/ceiling_fan | 5 | LODs 8178 |
| PH_CONCRETE_ROAD_BARRIER_02 | P1 | concrete_road_barrier_02 | Models/Props/concrete_road_barrier_02 | 4 | LODs 11910/5954/2976 |
| PH_COVERED_CAR | P1 | covered_car | Models/Props/covered_car | 4 | LODs 7997/3998/1999 |
| PH_DESK_LAMP_ARM_01 | P1 | desk_lamp_arm_01 | Models/Props/desk_lamp_arm_01 | 4 | LODs 12050/6024/3012 |
| PH_DRAWER_CABINET | P1 | vintage_wooden_drawer_01 (swap) | Models/Props/vintage_wooden_drawer_01 | 4 | LODs 5184/2592/1295 |
| PH_DRY_BRANCHES_MEDIUM_01 | P1 | dry_branches_medium_01 | Models/Props/dry_branches_medium_01 | 6 | LODs 2799/1399/699; 2800/1400/700; 2800/1400/700 |
| PH_METAL_JERRYCAN_GREEN | P1 | metal_jerrycan_green | Models/Props/metal_jerrycan_green | 4 | LODs 4818/2408/1204 |
| PH_METAL_TRASH_CAN | P1 | metal_trash_can | Models/Props/metal_trash_can | 8 | LODs 6428/3214/1606; 6980/3489/1745 |
| PH_NAMAQUALAND_BOULDER_04 | P1 | namaqualand_boulder_04 | Models/Props/namaqualand_boulder_04 | 4 | LODs 8000/4000/2000 |
| PH_OFFICE_NOTEPADS | P1 | office_notepads | Models/Props/office_notepads | 4 | LODs 666 |
| PH_OLD_TYRE | P1 | old_tyre | Models/Props/old_tyre | 4 | LODs 1998/998/498 |
| PH_STEEL_FRAME_SHELVES_01 | P1 | steel_frame_shelves_01 | Models/Props/steel_frame_shelves_01 | 4 | LODs 4348/2174/1087 |
| PH_UTILITY_BOX_01 | P1 | utility_box_01 | Models/Props/utility_box_01 | 4 | LODs 4404/2202/1101 |
| PH_VINTAGE_RADIO_TRANSCEIVER | P1 | vintage_radio_transceiver | Models/Props/vintage_radio_transceiver | 7 | LODs 21151/10574/5286 |
| PH_WOODEN_CRATE_02 | P1 | wooden_crate_02 | Models/Props/wooden_crate_02 | 4 | LODs 5176/2588/1294 |
| PH_CLIPBOARD | P2 | clipboard | Models/Props/clipboard | 4 | LODs 3088/1544/772 |
| PH_DEAD_TREE_TRUNK | P2 | dead_tree_trunk | Models/Props/dead_tree_trunk | 4 | LODs 8000/4000/2000 |
| PH_METAL_JUG | P2 | metal_jug | Models/Props/metal_jug | 4 | LODs 3020/1510/754 |
| PH_METAL_STOOL_01 | P2 | metal_stool_01 | Models/Props/metal_stool_01 | 4 | LODs 4166/2083/1040 |
| PH_OLD_GAS_MASK | P2 | old_gas_mask | Models/Props/old_gas_mask | 4 | LODs 9450/4725/2362 |
| PH_PORTABLE_SEARCHLIGHT | P2 | portable_searchlight | Models/Props/portable_searchlight | 4 | LODs 9401/4700/2349 |
| PH_PROPANE_TANK | P2 | propane_tank | Models/Props/propane_tank | 4 | LODs 2606/1303/650 |
| PH_TRASHBAG | P2 | trashbag | Models/Props/trashbag | 4 | LODs 2240/1120/560 |
| PH_WICKER_BASKET_01 | P2 | wicker_basket_01 | Models/Props/wicker_basket_01 | 4 | LODs 11138/5568/2784 |

**texture**

| id | P | source | folder (under Art/) | files | size / LOD tris |
|---|---|---|---|---|---|
| TEX_ACG_FABRIC066 | P0 | Fabric066 | Textures/Env/Fabric066 | 3 | 1024x1024 |
| TEX_ACG_METALPLATES013 | P0 | MetalPlates013 | Textures/Env/MetalPlates013 | 3 | 1024x1024 |
| TEX_ACG_PAINTEDMETAL006 | P0 | PaintedMetal006 | Textures/Env/PaintedMetal006 | 3 | 1024x1024 |
| TEX_ACG_PLASTER001 | P0 | Plaster001 | Textures/Env/Plaster001 | 3 | 1024x1024 |
| TEX_ACG_SHEETMETAL001 | P0 | SheetMetal001 | Textures/Env/SheetMetal001 | 3 | 1024x1024 |
| TEX_ACG_TERRAIN003 | P0 | Terrain003 | Textures/Env/Terrain003 | 3 | 2048x2048 |
| TEX_ACG_TERRAZZO003 | P0 | Terrazzo003 | Textures/Env/Terrazzo003 | 3 | 1024x1024 |
| TEX_AERIAL_SAND | P0 | aerial_sand | Textures/Env/aerial_sand | 3 | 1024x1024 |
| TEX_BEIGE_WALL_001 | P0 | beige_wall_001 | Textures/Env/beige_wall_001 | 3 | 1024x1024 |
| TEX_CLAY_PLASTER | P0 | clay_plaster | Textures/Env/clay_plaster | 3 | 1024x1024 |
| TEX_CONCRETE_DEBRIS | P0 | concrete_debris | Textures/Env/concrete_debris | 3 | 1024x1024 |
| TEX_CONCRETE_FLOOR_WORN_001 | P0 | concrete_floor_worn_001 | Textures/Env/concrete_floor_worn_001 | 3 | 2048x2048 |
| TEX_CONCRETE_WALL_007 | P0 | concrete_wall_007 | Textures/Env/concrete_wall_007 | 4 | 1024x1024 (+ shared detail map in Env/_Shared) |
| TEX_CORRUGATED_IRON | P0 | corrugated_iron | Textures/Env/corrugated_iron | 3 | 1024x1024 |
| TEX_CRACKED_CONCRETE_WALL | P0 | cracked_concrete_wall | Textures/Env/cracked_concrete_wall | 3 | 1024x1024 |
| TEX_DRY_GROUND_ROCKS | P0 | dry_ground_rocks | Textures/Env/dry_ground_rocks | 3 | 1024x1024 |
| TEX_GRAVELLY_SAND | P0 | gravelly_sand | Textures/Env/gravelly_sand | 3 | 1024x1024 |
| TEX_GREEN_METAL_RUST | P0 | green_metal_rust | Textures/Env/green_metal_rust | 3 | 1024x1024 |
| TEX_HESSIAN_230 | P0 | hessian_230 | Textures/Env/hessian_230 | 4 | 1024x1024 |
| TEX_LENS_DIRT | P0 | SurfaceImperfections003 | Textures/FX | 1 | 1024x1024 |
| TEX_METAL_PLATE | P0 | metal_plate | Textures/Env/metal_plate | 3 | 1024x1024 |
| TEX_OLD_LINOLEUM_FLOORING_01 | P0 | old_linoleum_flooring_01 | Textures/Env/old_linoleum_flooring_01 | 3 | 1024x1024 |
| TEX_PAINTED_CONCRETE | P0 | painted_concrete | Textures/Env/painted_concrete | 3 | 1024x1024 |
| TEX_PAINTED_PLASTER_WALL | P0 | painted_plaster_wall | Textures/Env/painted_plaster_wall | 3 | 2048x2048 |
| TEX_ROCKY_TRAIL_02 | P0 | rocky_trail_02 | Textures/Env/rocky_trail_02 | 3 | 1024x1024 |
| TEX_ROCK_FACE_03 | P0 | rock_face_03 | Textures/Env/rock_face_03 | 3 | 1024x1024 |
| TEX_ROOF_09 | P0 | roof_09 | Textures/Env/roof_09 | 3 | 1024x1024 |
| TEX_RUSTY_CORRUGATED_IRON | P0 | rusty_corrugated_iron | Textures/Env/rusty_corrugated_iron | 3 | 1024x1024 |
| TEX_SANDSTONE_BLOCKS_05 | P0 | sandstone_blocks_05 | Textures/Env/sandstone_blocks_05 | 4 | 1024x1024 (+ shared detail map in Env/_Shared) |
| TEX_SAND_01 | P0 | sand_01 | Textures/Env/sand_01 | 5 | 2048x2048 (+ shared detail map in Env/_Shared) |
| TEX_TERRAZZO_TILES | P0 | terrazzo_tiles | Textures/Env/terrazzo_tiles | 3 | 1024x1024 |
| TEX_VFX_DUST_IMPACT | P0 | procedural | Textures/FX | 1 | 1024x1024 |
| TEX_VFX_FIRE_FLIPBOOK | P0 | procedural | Textures/FX | 1 | 1024x1024 |
| TEX_VFX_GLINT | P0 | procedural | Textures/FX | 1 | 256x256 |
| TEX_VFX_MUZZLE | P0 | procedural | Textures/FX | 1 | 1024x512 |
| TEX_VFX_SMOKE_FLIPBOOK | P0 | procedural | Textures/FX | 1 | 1024x1024 |
| TEX_VFX_SPARKS | P0 | procedural | Textures/FX | 1 | 512x512 |
| TEX_VFX_TRACER | P0 | procedural | Textures/FX | 1 | 256x64 |
| TEX_WEATHERED_PLANKS | P0 | weathered_planks | Textures/Env/weathered_planks | 3 | 1024x1024 |
| TEX_WHITE_ROUGH_PLASTER | P0 | white_rough_plaster | Textures/Env/white_rough_plaster | 3 | 1024x1024 |
| TEX_WOOD_PEELING_PAINT_WEATHERED | P0 | wood_peeling_paint_weathered | Textures/Env/wood_peeling_paint_weathered | 4 | 1024x1024 |
| TEX_ACG_BRICKS043 | P1 | Bricks043 | Textures/Env/Bricks043 | 3 | 1024x1024 |
| TEX_ACG_CONCRETE042B | P1 | Concrete042B | Textures/Env/Concrete042B | 3 | 1024x1024 |
| TEX_ACG_CORRUGATEDSTEEL005 | P1 | CorrugatedSteel005 | Textures/Env/CorrugatedSteel005 | 3 | 1024x1024 |
| TEX_ACG_CORRUGATEDSTEEL006A | P1 | CorrugatedSteel006A | Textures/Env/CorrugatedSteel006A | 3 | 1024x1024 |
| TEX_ACG_GROUND097 | P1 | Ground097 | Textures/Env/Ground097 | 3 | 1024x1024 |
| TEX_ACG_PAINTEDPLASTER006 | P1 | PaintedPlaster006 | Textures/Env/PaintedPlaster006 | 3 | 1024x1024 |
| TEX_ACG_ROCKS017 | P1 | Rocks017 | Textures/Env/Rocks017 | 3 | 1024x1024 |
| TEX_ACG_ROPE001 | P1 | Rope001 | Textures/Env/Rope001 | 3 | 1024x1024 |
| TEX_BROWN_PLANKS_03 | P1 | brown_planks_03 | Textures/Env/brown_planks_03 | 3 | 1024x1024 |
| TEX_DAMAGED_PLASTER | P1 | damaged_plaster | Textures/Env/damaged_plaster | 3 | 1024x1024 |
| TEX_FLOOR_TILES_08 | P1 | floor_tiles_08 | Textures/Env/floor_tiles_08 | 3 | 1024x1024 |
| TEX_MUD_CRACKED_DRY_03 | P1 | mud_cracked_dry_03 | Textures/Env/mud_cracked_dry_03 | 3 | 1024x1024 |
| TEX_PEELING_PAINTED_WALL | P1 | peeling_painted_wall | Textures/Env/peeling_painted_wall | 3 | 1024x1024 |
| TEX_REBAR_REINFORCED_CONCRETE | P1 | rebar_reinforced_concrete | Textures/Env/rebar_reinforced_concrete | 4 | 1024x1024 |
| TEX_ROAD_DAMAGED | P1 | road_damaged | Textures/Env/road_damaged | 3 | 1024x1024 |
| TEX_RUSTY_METAL_02 | P1 | rusty_metal_02 | Textures/Env/rusty_metal_02 | 3 | 1024x1024 |
| TEX_YELLOW_PLASTER | P1 | yellow_plaster | Textures/Env/yellow_plaster | 3 | 1024x1024 |
| TEX_ACG_CARDBOARD004 | P2 | Cardboard004 | Textures/Env/Cardboard004 | 3 | 1024x1024 |
| TEX_ACG_FABRIC044 | P2 | Fabric044 | Textures/Env/Fabric044 | 3 | 1024x1024 |
| TEX_ACG_PAINTEDMETAL014 | P2 | PaintedMetal014 | Textures/Env/PaintedMetal014 | 3 | 1024x1024 |
| TEX_ACG_WICKER010B | P2 | Wicker010B | Textures/Env/Wicker010B | 3 | 1024x1024 |
| TEX_WORN_CORRUGATED_IRON | P2 | worn_corrugated_iron | Textures/Env/worn_corrugated_iron | 3 | 1024x1024 |
