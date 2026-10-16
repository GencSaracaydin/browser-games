# CC0 assets and the humanoid pipeline

Research for the Cold War desert campaign mission (Unity 6000.6.6f1, URP 17.6, WebGL build). Last checked 2026-10-16. Every size and URL below comes from the live Poly Haven, ambientCG, GitHub and itch.io APIs. The machine-readable copy is `Docs/research/cc0_manifest.json`: it lists every map at 1k and 2k with URL, size and md5, every model file and include, and every ambientCG zip.

## TL;DR

- **Environment:** Poly Haven covers almost everything a 1980s desert town needs: 8 HDRIs, 30 core PBR materials plus 34 alternates, and about 100 props. ambientCG fills the dune-sand, damaged-concrete, painted-metal, canvas and decal gaps. Neither library has **sandbags, camo fabric, true mudbrick, 1980s weapons, vehicles, date palms, posters or bullet-hole and blood decals**. Those need fal (section 8).
- **Characters:** use **Microsoft Rocketbox avatars (MIT)**. They are realistic 2010-era humans, 8–15k tris, with a 3ds Max Biped rig that auto-maps to Mecanim Humanoid. The set includes 6 military males, 2 military females and about 60 civilians, and the faces are much more realistic than any CC0 alternative.
- **Animations:** combine sources through **Mecanim Humanoid retargeting**:
  - **Quaternius UAL1 and UAL2 Standard (CC0):** 86 clips including locomotion, crouch, death, hits, overhand throw, slide, 1 m mantle and pistol. Downloaded and verified here.
  - **Unity Standard-Assets-Characters (Unity Companion License):** a mocap strafe and turn locomotion set.
  - **Rocketbox animations (MIT):** sit-at-table, idle and gesture clips for the interrogation and briefing scenes, plus a facial-rig sample.
- **The gap is rifle-specific clips** (rifle idle/aim/fire/reload, cover, rifle deaths). Nothing CC0 has them. Two ways to fill it:
  - **fal `fal-ai/meshy/rigging/multi-animation`:** Meshy's 697-preset library includes `Standing_Reload`, `Walk_Forward_While_Shooting`, `Rifle_Aim_Turn_Right`, `Vault_with_Rifle`, `Shot_and_Fall_Backward` and more (IDs in section 7.5). Run it once on the CC0 UAL mannequin and retarget the results like any other Humanoid clip.
  - **Procedural Animation Rigging:** two-bone IK on the weapon plus multi-aim on the spine. Use this for aiming and recoil anyway.
- **Unity imports `.hdr`, `.exr`, `.tga`, `.fbx` (7.4 / 7.5 / 7.7) natively.** glTF needs the free `com.unity.cloud.gltfast` package. The 2k `goegap.hdr` is a 2048×1024 RLE RGBE file of 5.4 MB.

## 1. Sources and licenses

| Source | License | Login needed? | Download method (tested) |
|---|---|---|---|
| Poly Haven (HDRIs, textures, models) | CC0 1.0 | no | `https://api.polyhaven.com/files/<id>` gives direct `dl.polyhaven.org` URLs. Send a custom User-Agent. |
| ambientCG | CC0 1.0 | no | `https://ambientcg.com/get?file=<Id>_<1K\|2K>-JPG.zip` redirects (302) to `acg-download.struffelproductions.com`. |
| Quaternius UAL1 / UAL2 / Base Characters | CC0 1.0 (in-zip `License.txt` confirms) | no | itch.io "name your own price, $0" flow, scripted with curl (section 7.2). |
| Microsoft Rocketbox | **MIT** (keep `LICENSE.md` and add attribution to credits) | no | raw.githubusercontent.com, file by file. The repo is 24 GB, so do not clone it. Files are not LFS. |
| Unity Standard-Assets-Characters | **Unity Companion License** (Unity-dependent projects only, fine here) | no | raw.githubusercontent.com, file by file |
| fal (Meshy rigging/animation, Hunyuan Motion, 3D gen) | output per fal/provider terms (not CC0) | uses FAL_KEY | `fal_client` |

## 2. Unity import notes (read before importing anything)

- **HDRIs.** Set the `.hdr` or `.exr` to Texture Shape **Cube**, Mapping **Latitude-Longitude**, and use it with `Skybox/Cubemap`, or keep it 2D with `Skybox/Panoramic`. Set Lighting > Environment to Skybox and bake or update reflection probes. A 2k lat-long gives a 512 px cube face: good for lighting, soft as a visible sky. A cheap way to get a sharp sky is to use the HDR only for lighting and probes and show Poly Haven's **tonemapped LDR JPG** as the visible backdrop (for example `https://dl.polyhaven.org/file/ph-assets/HDRIs/extra/Tonemapped%20JPG/goegap.jpg`, 6.0 MB, compresses to DXT1). WebGL does not reliably support BC6H, so HDR cubemaps fall back to RGB9e5 or RGBAHalf. Keep the HDR at ≤ 2k. Poly Haven HDRIs are **unclipped**: the sun is in the map. Pair it with a real-time directional light aligned to the sun, and lower the sky exposure if the ambient light looks doubled.
- **Normals.** Use **`nor_gl`** from Poly Haven and **`NormalGL`** from ambientCG. Unity uses the OpenGL Y+ convention, so skip the DX variants.
- **Channel packing.** Poly Haven **`arm`** is R = AO, G = roughness, B = metal. URP/Lit wants a Metallic map (R = metal, A = smoothness = 1 − rough) plus a separate Occlusion map. There are two options:
  1. **Preferred:** one Shader Graph, "Lit_ARM", that reads `arm` directly: AO = R, smoothness = 1 − G, metal = B. That is one texture per material instead of two.
  2. Repack offline with PIL (system `python3` has Pillow 10.4): `MetallicSmoothness = RGBA(B, B, B, 255 − G)`. For Terrain layers, pack the Unity terrain mask (R = metal, G = AO, B = height from `disp`, A = smoothness).
- **Height.** `disp` maps work as the URP Lit Height Map (parallax). Use them only on hero ground and walls.
- **Rocketbox materials.** Rocketbox uses a specular workflow: TGA color, normal and specular maps, 2048² RGB, no gloss channel. Use URP/Lit **Workflow Mode = Specular**, with smoothness about 0.2–0.35 for cloth and 0.45 for skin. Downscale to 1k for AI.
- **Strip non-texture files before copying into `Assets/`.** ambientCG zips contain `.blend`, `.usdc`, `.mtlx`, `.tres` and a preview `.png`. Unity will try to import `.blend` through Blender, which is not installed, and log errors.
- **glTF vs FBX for Poly Haven models.** With glTFast installed, the `.gltf` (JPG textures) imports with URP materials created automatically: the fastest path. The FBX packs carry PNG textures and need manual materials. Unity re-encodes textures either way, so source file size does not affect build size. Prop variants such as `Barrel_01`'s `oxide_green` and `radiation` skins are separate map sets in `/files/<id>` (keys `<variant>_diff`, `<variant>_nor_gl`, `<variant>_arm`). The glTF include list carries only the default skin.
- **Heavy meshes.** Poly Haven ships only one mesh LOD. Boulders and cliffs run 76k–364k tris and the bushes run to 1.4M. With no Blender available, decimate with `@gltf-transform/cli` or `gltfpack` (meshoptimizer, MIT, via `npx`; needs approval to fetch from npm) or the MIT UnityMeshSimplifier package. Otherwise use those assets only as distant set dressing.
- **WebGL texture budget.** Approximate VRAM with mips:

  | Format | 2048² | 1024² |
  |---|---|---|
  | DXT1 | 2.8 MB | 0.7 MB |
  | DXT5 | 5.6 MB | 1.4 MB |

  A 2k material (base DXT1 + normal DXT5 + ARM DXT1) costs about 11 MB of VRAM. A 1k material costs about 2.8 MB. Crunch compression cuts the download to roughly 25–35 % of that. Recommendation: **1k by default, 2k only for about 6 hero materials** (main ground, two town walls, interrogation-room wall and floor, cliffs). Add a tiling detail-normal for close-ups. The 15 MB per-file host limit also means content must ship in Addressables or AssetBundles of ≤ 15 MB each. A 2k material set compresses to about 3–4 MB.

## 3. HDRIs (Poly Haven, CC0)

The URL pattern is `https://dl.polyhaven.org/file/ph-assets/HDRIs/{hdr|exr}/{1k|2k|4k}/<id>_<res>.{hdr|exr}`. Every candidate was checked visually from Poly Haven thumbnails. The `rogland_*`, `nowhere_road` and `dry_cracked_lake` `.exr` files are about 4× larger than the `.hdr` files, so prefer `.hdr`.

| # | id | use | 1k .hdr | 2k .hdr | 2k .exr | 4k .hdr | 2k .hdr URL |
|---|---|---|---|---|---|---|---|
| 1 | `goegap` | Primary harsh midday: clear desert, hard sun, flat sandy horizon | 1.4 MB | 5.4 MB | 5.1 MB | 22.4 MB | https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/goegap_2k.hdr |
| 2 | `quarry_02` | Clear arid daylight, sandy quarry, scattered cloud (town outskirts) | 1.6 MB | 6.1 MB | 4.8 MB | 24.5 MB | https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/quarry_02_2k.hdr |
| 3 | `kiara_5_noon` | High-noon arid rocky valley with mountain horizon (alt midday) | 1.5 MB | 6.0 MB | 5.1 MB | 25.2 MB | https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/kiara_5_noon_2k.hdr |
| 4 | `kiara_8_sunset` | Golden hour over dry ridges (exfil / set-piece) | 1.5 MB | 6.3 MB | 5.2 MB | 25.9 MB | https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/kiara_8_sunset_2k.hdr |
| 5 | `kiara_9_dusk` | Dusk / blue hour, low contrast (insertion) | 1.5 MB | 6.3 MB | 5.1 MB | 25.9 MB | https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/kiara_9_dusk_2k.hdr |
| 6 | `rogland_moonlit_night` | Moonlit desert night, bright moon (needs exposure -3..-5 EV) | 1.7 MB | 6.8 MB | 24.3 MB | 26.5 MB | https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/rogland_moonlit_night_2k.hdr |
| 7 | `rogland_clear_night` | Dark starry night with Milky Way (alt night) | 1.7 MB | 6.8 MB | 24.3 MB | 27.9 MB | https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/rogland_clear_night_2k.hdr |
| 8 | `missile_launch_facility_01` | Concrete tunnel interior (bunker/hangar reflection probe) | 1.7 MB | 6.6 MB | 6.0 MB | 26.1 MB | https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/missile_launch_facility_01_2k.hdr |

Alternates: `the_lost_city`, `dry_cracked_lake`, `kiara_1_dawn`, `abandoned_tank_farm_05`, `nowhere_road`, `rogland_sunset`.

**Downloaded sample:** `RawAssets/cc0/hdri/goegap_2k.hdr`, 5,445,362 bytes, `#?RADIANCE`, `FORMAT=32-bit_rle_rgbe`, `-Y 1024 +X 2048`.

## 4. PBR textures, Poly Haven core set (30, CC0)

The URL pattern, verified for all 64 textures here with no exceptions, is `https://dl.polyhaven.org/file/ph-assets/Textures/jpg/<res>/<id>/<id>_<map>_<res>.jpg`. `<res>` is `1k`, `2k`, `4k` or `8k`. `<map>` is one of `diff`, `nor_gl`, `arm`, `disp`, `rough`, `ao`, plus `metal` or `mask` on the few textures that have them. PNG and EXR versions also exist under `/png/` and `/exr/`. "Set" sizes are diff + nor_gl + arm + disp as JPG. The tile size is the real-world width one texture repeat covers (Poly Haven `dimensions`); use it for UV and tiling scale. Every one of the 64 has diff, nor_gl, rough, ao, disp and arm.

Example, `sand_01` at 2k, downloaded:

- `https://dl.polyhaven.org/file/ph-assets/Textures/jpg/2k/sand_01/sand_01_diff_2k.jpg` (1,984,816 B)
- `.../sand_01_nor_gl_2k.jpg` (2,503,853 B)
- `.../sand_01_arm_2k.jpg` (477,093 B)
- `.../sand_01_disp_2k.jpg` (462,489 B)
- `.../sand_01_rough_2k.jpg` (757,435 B)
- `.../sand_01_ao_2k.jpg` (1,021,179 B)

**Ground & terrain**

| # | id | look / use | tile size | 1k set | 2k set | extra maps |
|---|---|---|---|---|---|---|
| 1 | `sand_01` | tan trampled sand-clay; main town ground | 1.5 m | 1.7 MB | 5.4 MB | - |
| 2 | `aerial_sand` | compacted sand with tyre/foot ruts; dirt roads (15 m tile) | 15.0 m | 1.6 MB | 5.8 MB | - |
| 3 | `mud_cracked_dry_03` | deep cracked mud; dry riverbed | 1.5 m | 3.6 MB | 13.6 MB | - |
| 4 | `dry_ground_01` | subtle cracked baked earth (4 m tile) | 4.0 m | 1.2 MB | 4.9 MB | - |
| 5 | `brown_mud_dry` | crumbly dirt with small stones | 1.3 m | 2.9 MB | 10.3 MB | - |
| 6 | `gravelly_sand` | coarse gravelly sand; compound yards | 2.5 m | 3.7 MB | 15.0 MB | - |
| 7 | `rocky_trail_02` | dusty rocky trail; hill paths | 2.0 m | 3.9 MB | 14.8 MB | - |
| 8 | `dry_ground_rocks` | stony parched ground; terrain blend layer | 4.0 m | 2.6 MB | 10.8 MB | - |

**Roads**

| # | id | look / use | tile size | 1k set | 2k set | extra maps |
|---|---|---|---|---|---|---|
| 9 | `road_damaged` | potholed patched asphalt | 2.2 m | 2.9 MB | 11.7 MB | - |
| 10 | `asphalt_02` | weathered grey asphalt | 3.0 m | 2.6 MB | 10.5 MB | - |

**Plaster / stucco / earth walls**

| # | id | look / use | tile size | 1k set | 2k set | extra maps |
|---|---|---|---|---|---|---|
| 11 | `beige_wall_001` | clean beige painted plaster | 3.0 m | 0.4 MB | 2.1 MB | - |
| 12 | `painted_plaster_wall` | worn exterior plaster | 2.0 m | 2.6 MB | 10.9 MB | - |
| 13 | `cracked_concrete_wall` | beige cracked/chipped plaster (damaged) | 1.0 m | 3.0 MB | 12.0 MB | - |
| 14 | `white_rough_plaster` | flaking whitewash (damaged) | 1.0 m | 1.9 MB | 6.5 MB | - |
| 15 | `clay_plaster` | brown clay/earth plaster; adobe walls | 2.0 m | 1.9 MB | 7.5 MB | - |

**Stone & concrete**

| # | id | look / use | tile size | 1k set | 2k set | extra maps |
|---|---|---|---|---|---|---|
| 16 | `sandstone_blocks_05` | rough-hewn sandstone blockwork; old quarter | 3.0 m | 2.2 MB | 7.5 MB | - |
| 17 | `concrete_floor_worn_001` | worn slab floor; compound interiors | 3.0 m | 0.5 MB | 2.7 MB | - |
| 18 | `concrete_wall_007` | weathered concrete w/ plaster patches | 2.2 m | 2.2 MB | 9.1 MB | - |
| 19 | `rebar_reinforced_concrete` | painted concrete with exposed rebar (battle damage) | 2.0 m | 2.6 MB | 9.9 MB | Mask |
| 20 | `painted_concrete` | green painted concrete; interrogation/bunker walls | 2.0 m | 2.7 MB | 12.0 MB | - |

**Metal**

| # | id | look / use | tile size | 1k set | 2k set | extra maps |
|---|---|---|---|---|---|---|
| 21 | `corrugated_iron` | galvanised corrugated roofing (metal) | 1.1 m | 2.2 MB | 8.7 MB | Metal |
| 22 | `rusty_corrugated_iron` | heavily rusted corrugated sheet | 2.0 m | 2.0 MB | 8.2 MB | - |
| 23 | `green_metal_rust` | OD-green painted metal w/ rust streaks (vehicles, lockers) | 1.0 m | 0.8 MB | 2.6 MB | - |
| 24 | `metal_plate` | green diamond-plate (truck beds, floors) | 0.5 m | 2.8 MB | 10.2 MB | Metal |

**Wood & fabric**

| # | id | look / use | tile size | 1k set | 2k set | extra maps |
|---|---|---|---|---|---|---|
| 25 | `weathered_planks` | dark weathered planks (doors, shacks) | 2.0 m | 2.0 MB | 8.5 MB | - |
| 26 | `wood_peeling_paint_weathered` | peeling dark paint on planks (shutters) | 0.8 m | 2.4 MB | 9.3 MB | Mask |
| 27 | `hessian_230` | burlap/jute: sandbags, sacks | 0.3 m | 4.0 MB | 14.3 MB | Metal, Mask |

**Rubble, rock, interior floor**

| # | id | look / use | tile size | 1k set | 2k set | extra maps |
|---|---|---|---|---|---|---|
| 28 | `concrete_debris` | crumbled concrete + brick debris ground | 2.0 m | 3.8 MB | 13.8 MB | - |
| 29 | `rock_face_03` | rugged rock face; cliffs | 2.7 m | 3.1 MB | 12.3 MB | - |
| 30 | `old_linoleum_flooring_01` | 1980s checker linoleum; office/interrogation | 2.0 m | 2.0 MB | 8.0 MB | - |

Alternates: `dense_sand` (1.8 m, 2k set 10.3 MB); `aerial_beach_01` (30.0 m, 2k set 2.6 MB); `dirt_aerial_03` (25.0 m, 2k set 3.1 MB); `sandy_gravel` (2.1 m, 2k set 14.3 MB); `preconcrete_wall_001` (4.0 m, 2k set 2.6 MB); `damaged_plaster` (1.9 m, 2k set 9.9 MB); `yellow_plaster` (2.0 m, 2k set 4.3 MB); `clay_floor_001` (2.0 m, 2k set 1.5 MB); `patterned_clay_wall` (1.2 m, 2k set 10.9 MB); `sandstone_blocks_08` (3.0 m, 2k set 2.7 MB); `worn_rock_natural_01` (2.0 m, 2k set 10.2 MB); `concrete_wall_008` (2.7 m, 2k set 8.4 MB); `concrete_floor_damaged_01` (5.0 m, 2k set 8.9 MB); `hangar_concrete_floor` (2.0 m, 2k set 9.6 MB); `garage_floor` (1.9 m, 2k set 9.3 MB); `concrete_layers_02` (2.0 m, 2k set 9.0 MB); `peeling_painted_wall` (1.8 m, 2k set 10.1 MB); `worn_corrugated_iron` (1.8 m, 2k set 11.3 MB); `rusty_metal_04` (2.0 m, 2k set 11.1 MB); `container_side` (1.9 m, 2k set 7.0 MB); `rusty_metal_02` (1.0 m, 2k set 4.7 MB); `brown_planks_03` (1.0 m, 2k set 5.8 MB); `raw_plank_wall` (1.9 m, 2k set 9.0 MB); `roof_09` (4.0 m, 2k set 4.5 MB); `clay_roof_tiles_02` (2.5 m, 2k set 10.0 MB); `rubble` (2.0 m, 2k set 11.4 MB); `cliff_side` (1.8 m, 2k set 9.7 MB); `rock_06` (1.5 m, 2k set 6.7 MB); `dry_riverbed_rock` (2.0 m, 2k set 12.2 MB); `rock_boulder_dry` (1.8 m, 2k set 10.7 MB); `floor_tiles_08` (1.5 m, 2k set 4.2 MB); `dirty_tiles` (2.3 m, 2k set 8.3 MB); `terrazzo_tiles` (2.0 m, 2k set 7.4 MB); `old_mosaic_floor` (2.0 m, 2k set 9.9 MB).

**Downloaded samples (2k, all maps):**

- `RawAssets/cc0/textures/polyhaven/sand_01/`: 7.2 MB total.
- `RawAssets/cc0/textures/polyhaven/cracked_concrete_wall/`: diff 4.36 MB, nor_gl 3.85, arm 2.93, rough 2.53, ao 2.43, disp 0.82. 16.9 MB total.

All are 2048² JPG. Diff, nor_gl and arm are RGB. AO and disp are 8-bit grayscale. `rough` is grayscale for `sand_01` and RGB for `cracked_concrete_wall`.

## 5. ambientCG gap-fill (CC0)

There is no ambientCG asset for **sandbag, camouflage, tarp, canvas, adobe or bullet** (all searched). These fill everything else. Download with `https://ambientcg.com/get?file=<Id>_1K-JPG.zip` or `_2K-JPG.zip`, no login. Zips contain `Color`, `NormalGL`, `NormalDX`, `Roughness`, `AmbientOcclusion`, `Displacement`, plus `Metalness` or `Opacity` where listed, plus `.blend`, `.usdc`, `.mtlx`, `.tres` and a preview `.png`; strip those extras. 2K zips are 2–4× the Poly Haven 2k JPG sets because they bundle both normal conventions. Copy only Color, NormalGL, Roughness, AO and Metalness. Thumbnails: `https://acg-media.struffelproductions.com/file/ambientCG-Web/media/thumbnail/256-JPG-242424/<Id>.jpg`.

**Ground / rock gap-fill**

| id | type | look / use | maps | 1K-JPG zip | 2K-JPG zip |
|---|---|---|---|---|---|
| `Ground097` | Material | wind-rippled tan dune sand (open desert) | color, displacement, normal, roughness, AO | 5.3 MB | 17.7 MB |
| `Ground093B` | Material | pale desert sand with dune accumulation | color, displacement, normal, roughness, AO | 5.1 MB | 18.3 MB |
| `Ground107` | Material | dirt with brick fragments (bombed street) | color, displacement, normal, roughness, AO | 9.8 MB | 34.7 MB |
| `Ground111` | Material | construction debris ground | color, displacement, normal, roughness, AO | 9.8 MB | 34.4 MB |
| `Rocks017` | Material | desert pavement: pebbles in sand | color, displacement, normal, roughness | 8.4 MB | 27.2 MB |
| `Rock061` | Material | brown layered cliff rock | color, displacement, normal, roughness, AO | 7.4 MB | 23.6 MB |

**Walls / concrete**

| id | type | look / use | maps | 1K-JPG zip | 2K-JPG zip |
|---|---|---|---|---|---|
| `Concrete042B` | Material | damaged concrete w/ exposed rusty mesh | color, displacement, normal, roughness, AO, metalness | 6.9 MB | 25.4 MB |
| `Concrete041B` | Material | damaged concrete variant | color, displacement, normal, roughness, AO, metalness | 7.1 MB | 25.8 MB |
| `PaintedPlaster006` | Material | broken painted plaster facade | color, displacement, normal, roughness, AO | 9.0 MB | 29.6 MB |
| `PaintedPlaster012` | Material | olive old painted plaster (interrogation room) | color, displacement, normal, roughness, AO | 8.0 MB | 27.9 MB |
| `PaintedPlaster014` | Material | white plaster broken over brick | color, displacement, normal, roughness, AO | 8.8 MB | 30.8 MB |
| `Plaster001` | Material | clean matte stucco | color, displacement, normal, roughness | 7.4 MB | 26.1 MB |
| `Bricks043` | Material | beige sloppy stone-brick (old walls) | color, displacement, normal, roughness, AO | 8.7 MB | 32.3 MB |

**Metal**

| id | type | look / use | maps | 1K-JPG zip | 2K-JPG zip |
|---|---|---|---|---|---|
| `CorrugatedSteel005` | Material | bare weathered corrugated steel | color, displacement, normal, roughness, AO, metalness | 5.9 MB | 19.5 MB |
| `CorrugatedSteel006A` | Material | green painted corrugated w/ rust | color, displacement, normal, roughness, AO, metalness | 5.4 MB | 17.7 MB |
| `CorrugatedSteel009` | Material | dark ribbed industrial steel | color, displacement, normal, roughness, AO, metalness | 3.9 MB | 10.4 MB |
| `MetalPlates013` | Material | rusty riveted plates (hulls, tanks) | color, displacement, normal, roughness, AO, metalness | 7.0 MB | 23.1 MB |
| `PaintedMetal006` | Material | teal chipped paint over rust (doors, 80s vehicles) | color, displacement, normal, roughness, AO, metalness | 9.7 MB | 35.1 MB |
| `PaintedMetal014` | Material | pale blue chipped paint over rust | color, displacement, normal, roughness, AO, metalness | 9.5 MB | 33.4 MB |
| `SheetMetal001` | Material | perforated sheet (opacity map) | color, displacement, normal, roughness, opacity, metalness | 6.5 MB | 15.7 MB |

**Fabric / misc**

| id | type | look / use | maps | 1K-JPG zip | 2K-JPG zip |
|---|---|---|---|---|---|
| `Fabric066` | Material | olive plain-weave canvas: tarps, tents, webbing | color, displacement, normal, roughness, AO | 9.7 MB | 35.8 MB |
| `Fabric044` | Material | grey rough felt/blanket (opacity) | color, displacement, normal, roughness, opacity | 8.8 MB | 27.4 MB |
| `Rope001` | Material | rope | color, displacement, normal, roughness, metalness | 4.2 MB | 12.2 MB |
| `Wicker010B` | Material | old wicker baskets | color, displacement, normal, roughness, opacity, AO | 7.4 MB | 24.2 MB |
| `Cardboard004` | Material | cardboard | color, displacement, normal, roughness | 6.0 MB | 23.3 MB |
| `Terrazzo003` | Material | dark terrazzo floor (office) | color, displacement, normal, roughness | 6.6 MB | 21.2 MB |

**Decals / atlases / overlays**

| id | type | look / use | maps | 1K-JPG zip | 2K-JPG zip |
|---|---|---|---|---|---|
| `TireTracks001` | Decal | tyre tracks in sand decal (1.8 x 0.45 m) | color, displacement, normal, roughness, AO | 2.8 MB | 6.8 MB |
| `AsphaltDamageSet001` | Atlas | pothole/crack decal atlas | color, displacement, normal, roughness, opacity | 5.1 MB | 14.3 MB |
| `AsphaltDamage001` | Decal | single asphalt damage decal | color, displacement, normal, roughness, opacity | 7.7 MB | 26.2 MB |
| `Leaking004` | Decal | leak/smear streak decal | color, displacement, normal, roughness, opacity | 3.9 MB | 11.4 MB |
| `Leaking007` | Decal | leak streak decal | color, displacement, normal, roughness, opacity | 1.9 MB | 4.0 MB |
| `Leaking009` | Decal | leak streak decal | color, displacement, normal, roughness, opacity | 1.7 MB | 3.3 MB |
| `Tape006` | Atlas | silver duct-tape atlas (taped mags, sealed windows) | color, displacement, normal, roughness, opacity | 5.3 MB | 16.3 MB |
| `Tape004` | Atlas | brown packaging tape atlas | color, displacement, normal, roughness, opacity | 3.7 MB | 12.8 MB |
| `SurfaceImperfections003` | Material | smudge/dust overlay (glass, CRT, scopes) | color, normal, opacity | 5.4 MB | 18.9 MB |
| `Scratches004` | Material | scratch overlay (weapon metal, glass) | color, normal, opacity | 6.4 MB | 22.1 MB |
| `Fingerprints002` | Material | fingerprint overlay (glass, CRT) | color, normal, roughness, opacity | 5.2 MB | 17.5 MB |

**Heightmap**

| id | type | look / use | maps | 1K-JPG zip | 2K-JPG zip |
|---|---|---|---|---|---|
| `Terrain003` | Terrain | 500 m mountain heightmap (distant ranges) | color, soil | - MB | 17.7 MB |
| `Terrain001` | Terrain | mountain heightmap alt | color, soil | - MB | 17.9 MB |

**Downloaded samples:**

- `RawAssets/cc0/textures/ambientcg/CorrugatedSteel005/CorrugatedSteel005_1K-JPG.zip` (5.9 MB, 12 files, layout as above).
- `RawAssets/cc0/textures/ambientcg/TireTracks001/TireTracks001_1K-JPG.zip` (2.8 MB). The tyre-track "decal" has **no Opacity map**. Derive alpha from Displacement or AO for a URP Decal Projector.

Excluded on purpose: `PaintedMetal017` (English "DANGER DO NOT ENTER" text), `PaintedMetal018` (red-cross emblem), `GraffitiSet001` (Latin-script tags, out of setting). Keep any signage or graffiti in an invented script or plain numerals.

## 6. Poly Haven models (CC0)

"tris" is Poly Haven's `polycount`, which equals actual glTF triangles in spot checks of `Barrel_01`, `Television_01`, `ammo_box` and `wooden_crate_01`. `concrete_road_barrier` is the exception: 61k actual against 80k listed. glTF sizes include `.gltf`, `.bin` and all JPG textures. The URL pattern is `https://dl.polyhaven.org/file/ph-assets/Models/{gltf|fbx}/<res>/<id>/<id>_<res>.{gltf|fbx}`. Includes (bin and textures) are listed in the manifest and in `/files/<id>`. `RawAssets/cc0/tools/fetch_cc0.py model <id> gltf_1k` fetches the whole set; tested on `Barrel_01` (0.7 MB, 5 files).

**Military props & cover**

| id | tris | glTF 1k (all files) | glTF 2k | FBX 1k | note |
|---|---|---|---|---|---|
| `ammo_box` | 4,382 | 1.5 MB / 5 files | 5.0 MB | 1.1 MB |  |
| `old_military_crate` | 10,476 | 2.5 MB / 5 files | 9.3 MB | 3.6 MB |  |
| `wooden_military_crate` | 22,986 | 3.0 MB / 5 files | 10.2 MB | 4.1 MB |  |
| `wooden_crate_01` | 6,576 | 2.3 MB / 5 files | 8.3 MB | 3.0 MB |  |
| `wooden_crate_02` | 5,176 | 2.2 MB / 5 files | 8.2 MB | 3.3 MB |  |
| `Barrel_01` | 2,682 | 0.7 MB / 5 files | 2.4 MB | 4.0 MB | 4 skins: explosive / oxide_green / oxide_grey / radiation |
| `barrel_03` | 1,473 | 0.6 MB / 5 files | 2.1 MB | 2.1 MB |  |
| `metal_jerrycan_green` | 9,636 | 2.7 MB / 5 files | 9.4 MB | 4.4 MB |  |
| `metal_jerrycan` | 20,022 | 3.0 MB / 5 files | 10.6 MB | 4.7 MB |  |
| `propane_tank` | 5,238 | 2.2 MB / 5 files | 7.9 MB | 3.8 MB |  |
| `small_lpg_tank` | 15,042 | 2.8 MB / 5 files | 9.7 MB | 4.5 MB |  |
| `cement_bag` | 844 | 1.4 MB / 5 files | 5.6 MB | 1.9 MB | English "CEMENT" print |
| `old_military_compressor` | 79,042 | 4.4 MB / 5 files | 9.7 MB | 5.5 MB |  |
| `portable_generator` | 26,419 | 3.4 MB / 6 files | 9.8 MB | 5.1 MB |  |
| `portable_searchlight` | 18,802 | 1.9 MB / 5 files | 5.3 MB | 2.6 MB |  |
| `metal_tool_chest` | 14,576 | 2.4 MB / 5 files | 7.7 MB | 3.6 MB |  |
| `concrete_road_barrier_02` | 42,578 | 2.9 MB / 5 files | 9.8 MB | 6.8 MB |  |
| `concrete_road_barrier` | 80,776 | 3.8 MB / 5 files | 11.0 MB | 8.0 MB | meta 80k tris (61k actual) |
| `modular_chainlink_fence` | 89,232 | 7.3 MB / 8 files | 16.2 MB | 16.0 MB |  |
| `covered_car` | 12,592 | 2.3 MB / 5 files | 8.1 MB | 3.5 MB |  |
| `old_tyre` | 2,880 | 2.2 MB / 5 files | 8.7 MB | 3.0 MB |  |
| `rusted_wheel_rim_01` | 16,440 | 2.6 MB / 5 files | 7.7 MB | 3.7 MB |  |

**Town infrastructure**

| id | tris | glTF 1k (all files) | glTF 2k | FBX 1k | note |
|---|---|---|---|---|---|
| `modular_fort_01` | 28,218 | 10.6 MB / 11 files | 37.7 MB | 44.7 MB | old colonial stone fort kit (walls, towers, gate) - heavy |
| `large_iron_gate` | 38,140 | 3.1 MB / 5 files | 8.8 MB | 4.2 MB |  |
| `rollershutter_door` | 1,104 | 2.3 MB / 6 files | 8.7 MB | 3.4 MB | has graffiti variant (Latin script; avoid) |
| `rollershutter_window_01` | 1,104 | 2.2 MB / 6 files | 8.4 MB | 3.3 MB |  |
| `modular_electricity_poles` | 200,610 | 12.4 MB / 8 files | 25.9 MB | 22.2 MB | 200k tris - decimate or use pieces |
| `modular_electric_cables` | 42,078 | 6.0 MB / 8 files | 17.7 MB | 16.3 MB |  |
| `power_box_01` | 21,272 | 2.7 MB / 5 files | 8.4 MB | 5.0 MB |  |
| `utility_box_01` | 4,404 | 2.0 MB / 5 files | 7.1 MB | 3.8 MB |  |
| `utility_box_02` | 6,268 | 2.0 MB / 5 files | 7.1 MB | 3.9 MB |  |
| `street_lamp_02` | 20,338 | 1.9 MB / 5 files | 5.1 MB | 3.1 MB |  |
| `street_lamp_01` | 30,610 | 2.2 MB / 5 files | 5.9 MB | 3.6 MB |  |
| `security_light` | 4,668 | 2.4 MB / 5 files | 8.5 MB | 3.9 MB |  |
| `water_manhole_cover` | 6,301 | 2.3 MB / 5 files | 8.0 MB | 5.0 MB |  |
| `metal_trash_can` | 13,960 | 5.0 MB / 8 files | 17.5 MB | 18.6 MB |  |
| `trashbag` | 4,482 | 1.7 MB / 5 files | 5.3 MB | 2.3 MB |  |
| `modular_pipes` | 47,428 | 5.8 MB / 8 files | 18.5 MB | 29.7 MB |  |
| `wooden_ladder` | 8,492 | 2.6 MB / 5 files | 8.9 MB | 3.4 MB |  |
| `exterior_aircon_unit` | 18,986 | 7.7 MB / 14 files | 25.4 MB | 11.6 MB | modern split unit - use with care |

**Interrogation room / intel / CRT montage**

| id | tris | glTF 1k (all files) | glTF 2k | FBX 1k | note |
|---|---|---|---|---|---|
| `Television_01` | 1,918 | 0.5 MB / 5 files | 1.6 MB | 1.5 MB | CRT TV - intel montage |
| `television_02` | 2,310 | 1.2 MB / 5 files | 3.9 MB | 1.5 MB | CRT TV - intel montage |
| `vintage_radio_transceiver` | 42,350 | 6.3 MB / 8 files | 18.4 MB | 16.6 MB | field radio - numbers-station prop |
| `cassette_player` | 4,830 | 0.9 MB / 8 files | 2.6 MB | 4.5 MB |  |
| `portable_cassette_player` | 13,307 | 2.6 MB / 5 files | 7.8 MB | 3.2 MB |  |
| `filmstrip_projector_8mm` | 32,697 | 3.1 MB / 5 files | 7.6 MB | 3.8 MB |  |
| `projector_screen` | 8,727 | 3.1 MB / 8 files | 12.0 MB | 14.2 MB |  |
| `mounted_fluorescent_lights` | 17,820 | 1.9 MB / 6 files | 5.2 MB | 2.5 MB |  |
| `caged_hanging_light` | 22,893 | 3.1 MB / 6 files | 9.0 MB | 4.7 MB |  |
| `hanging_industrial_lamp` | 9,530 | 3.2 MB / 7 files | 12.0 MB | 9.0 MB |  |
| `desk_lamp_arm_01` | 25,710 | 2.9 MB / 5 files | 8.5 MB | 6.6 MB |  |
| `metal_office_desk` | 6,898 | 1.6 MB / 5 files | 5.2 MB | 1.9 MB |  |
| `SchoolChair_01` | 5,072 | 0.5 MB / 5 files | 1.6 MB | 2.2 MB |  |
| `WoodenChair_01` | 19,992 | 1.1 MB / 5 files | 2.3 MB | 2.5 MB |  |
| `metal_stool_01` | 8,334 | 2.3 MB / 5 files | 8.5 MB | 3.5 MB |  |
| `steel_frame_shelves_01` | 4,348 | 1.6 MB / 5 files | 5.6 MB | 2.3 MB |  |
| `worn_metal_rack` | 6,372 | 2.0 MB / 5 files | 6.8 MB | 3.3 MB |  |
| `drawer_cabinet` | 26,406 | 1.1 MB / 5 files | 2.1 MB | 1.8 MB |  |
| `old_bed_frame` | 49,990 | 4.0 MB / 5 files | 10.2 MB | 4.6 MB |  |
| `painted_wooden_table` | 600 | 1.8 MB / 5 files | 7.4 MB | 2.6 MB |  |
| `wooden_table_02` | 196 | 0.5 MB / 5 files | 1.9 MB | 1.6 MB |  |
| `clipboard` | 6,176 | 1.8 MB / 5 files | 6.8 MB | 2.4 MB |  |
| `binder_notebook` | 18,077 | 1.9 MB / 5 files | 5.1 MB | 2.3 MB |  |
| `office_notepads` | 666 | 1.3 MB / 5 files | 5.7 MB | 1.8 MB |  |
| `cigarette_pack` | 7,582 | 1.4 MB / 5 files | 3.8 MB | 2.0 MB | fictional brand art ("Kentucky Ace") |
| `vintage_lighter` | 3,366 | 1.8 MB / 5 files | 5.7 MB | 2.8 MB |  |
| `ceiling_fan` | 16,356 | 1.9 MB / 7 files | 5.2 MB | 2.5 MB |  |
| `wall_clock` | 3,658 | 1.6 MB / 6 files | 4.9 MB | 2.7 MB |  |

**Field gear & domestic clutter**

| id | tris | glTF 1k (all files) | glTF 2k | FBX 1k | note |
|---|---|---|---|---|---|
| `vintage_binocular` | 19,362 | 2.7 MB / 5 files | 8.0 MB | 4.0 MB |  |
| `binoculars` | 19,248 | 2.0 MB / 5 files | 5.2 MB | 2.5 MB |  |
| `old_gas_mask` | 18,900 | 2.3 MB / 5 files | 7.0 MB | 3.3 MB |  |
| `medical_box` | 3,718 | 2.0 MB / 5 files | 7.0 MB | 2.9 MB |  |
| `vintage_flashlight` | 10,562 | 1.8 MB / 5 files | 5.1 MB | 2.3 MB |  |
| `Lantern_01` | 33,902 | 1.4 MB / 5 files | 3.0 MB | 14.2 MB |  |
| `vintage_oil_lamp` | 7,208 | 2.1 MB / 9 files | 6.6 MB | 2.9 MB |  |
| `russian_food_cans_01` | 1,348 | 1.5 MB / 5 files | 4.8 MB | 2.1 MB | Cyrillic labels - fits Soviet-supplied regime |
| `long_life_food` | 4,712 | 2.0 MB / 5 files | 6.8 MB | 2.4 MB |  |
| `metal_jug` | 6,040 | 1.8 MB / 5 files | 6.3 MB | 2.5 MB |  |
| `tea_set_01` | 40,296 | 2.3 MB / 5 files | 5.2 MB | 3.6 MB |  |
| `wicker_basket_01` | 22,276 | 2.7 MB / 5 files | 8.4 MB | 3.6 MB |  |
| `street_rat` | 14,286 | 3.6 MB / 5 files | 9.7 MB | 5.7 MB |  |

**Placeholder weapons (not period-primary)**

| id | tris | glTF 1k (all files) | glTF 2k | FBX 1k | note |
|---|---|---|---|---|---|
| `service_pistol` | 27,548 | 3.3 MB / 5 files | 9.4 MB | 4.1 MB | WWII-era pistol; placeholder only |
| `bolt_action_rifle_7_62` | 19,985 | 6.0 MB / 8 files | 21.5 MB | 19.5 MB | bolt-action rifle; enemy militia flavour only |

**Rocks & vegetation**

| id | tris | glTF 1k (all files) | glTF 2k | FBX 1k | note |
|---|---|---|---|---|---|
| `namaqualand_boulder_02` | 183,681 | 5.1 MB / 5 files | 13.0 MB | 6.9 MB |  |
| `namaqualand_boulder_04` | 110,746 | 3.7 MB / 5 files | 10.1 MB | 5.3 MB |  |
| `namaqualand_boulder_05` | 117,622 | 4.7 MB / 5 files | 11.9 MB | 6.5 MB |  |
| `namaqualand_boulders_01` | 76,590 | 3.6 MB / 5 files | 10.5 MB | 4.9 MB |  |
| `namaqualand_rocks_01` | 160,716 | 4.8 MB / 5 files | 12.1 MB | 7.4 MB |  |
| `namaqualand_stones_01` | 106,602 | 4.2 MB / 5 files | 10.7 MB | 6.3 MB |  |
| `boulder_01` | 123,976 | 5.8 MB / 5 files | 13.5 MB | 10.4 MB |  |
| `rock_07` | 27,830 | 2.2 MB / 5 files | 6.3 MB | 3.8 MB |  |
| `rock_09` | 23,280 | 2.0 MB / 5 files | 5.8 MB | 3.6 MB |  |
| `namaqualand_cliff_01` | 176,938 | 4.5 MB / 5 files | 11.2 MB | 6.2 MB |  |
| `namaqualand_cliff_02` | 363,896 | 8.4 MB / 5 files | 15.5 MB | 9.6 MB | 364k tris - decimate |
| `rock_face_01` | 20,174 | 3.0 MB / 5 files | 10.2 MB | 4.2 MB |  |
| `dry_branches_medium_01` | 16,803 | 3.3 MB / 5 files | 10.8 MB | 4.9 MB |  |
| `dead_tree_trunk` | 101,802 | 4.8 MB / 5 files | 11.3 MB | 11.2 MB |  |
| `dead_quiver_trunk` | 33,706 | 2.3 MB / 5 files | 7.3 MB | 3.1 MB |  |
| `wild_rooibos_bush` | 76,521 | 2.5 MB / 5 files | 6.0 MB | 2.8 MB |  |
| `quiver_tree_01` | 269,522 | 8.9 MB / 8 files | 21.6 MB | 28.5 MB | 270k tris, African aloe tree - use sparingly |
| `didelta_spinosa` | 694,671 | 14.8 MB / 5 files | 17.0 MB | 21.2 MB | 695k tris - too heavy, reference only |
| `searsia_burchellii` | 1,427,042 | 34.6 MB / 5 files | 38.4 MB | 55.1 MB | 1.4M tris - too heavy, reference only |

Notes:

- **Not on Poly Haven, and needed:** 1980s rifles and SMGs (AK-pattern, M16A1/CAR-15, FAL, Uzi, RPG-7, M60), vehicles (technical pickup, APC, helicopter), date palms, sandbags, ammo-can stacks with period markings, field telephones, wall maps.
- Desert flora here is South African (Karoo). Use `dry_branches_medium_01`, `dead_tree_trunk` and `wild_rooibos_bush` as generic scrub. The quiver tree reads as "Africa", so use it sparingly.

## 7. Humanoid characters and animation

### 7.1 Candidates evaluated

| Candidate | License | Realism | Rig / Unity Humanoid | Soldier-relevant content | Verdict |
|---|---|---|---|---|---|
| **Microsoft Rocketbox** (github.com/microsoft/Microsoft-Rocketbox) | MIT | **High** (photo-textured heads, 2k maps) | 3ds Max Biped `Bip01 *`, 81 bones incl. face. Auto-maps (see 7.3). | 6 male and 2 female military (modern ACU), plus about 100 other adults and professionals (police, security, construction, business, medical) and children. 471 animation files (static, xy and xyz root-motion variants): walk, run, crouch, idles, sit at table/chair, gestures, talk. **No weapon clips.** | **Use for all humans** |
| **Quaternius UAL1 Standard** (quaternius.itch.io/universal-animation-library) | CC0 | mannequin | UE5-style names `root/pelvis/spine_01-03/neck_01/Head/clavicle_l/upperarm_l...`, 65 joints. Auto-maps. | 43 clips: locomotion, crouch, death, hits, roll, jump, pistol set, sitting, driving, interact | **Use** |
| **Quaternius UAL2 Standard** (same author) | CC0 | mannequin + female mannequin | same rig | 43 clips: OverhandThrow, ClimbUp_1m, Slide start/loop/exit, Hit_Knockback, LayToIdle, Walk_Carry, melee | **Use** |
| UAL1 **Pro** ($9.99) | CC0 | n/a | same | 120+: 8-direction locomotion, crouch strafes, crawling. **No rifle set visible on the tier sheet.** | Optional purchase, user's call |
| **Unity Standard-Assets-Characters** (GitHub) | Unity Companion License | mannequin (DefaultMale, 15k tris) | native Unity humanoid names | High-quality mocap: idle, walk/run/sprint forward, **full strafe set with turns**, jumps, falls, landing roll | **Use for squad and AI strafing** |
| Quaternius Universal Base Characters | CC0 | stylized (13k tris) | UAL rig | 6 bodies, 20 hairstyles; 122 MB | Fallback only |
| Kenney animated characters | CC0 | blocky | Mixamo-like | few clips | No |
| OpenGameArt "soldier" CC0 | CC0/mixed | low-poly | mostly `.blend` (needs Blender) | n/a | No |
| CMU mocap (BVH) | CMU terms (free, no resale) | n/a | BVH: Unity cannot import it, and Blender is unavailable | no weapons | No |
| Unity Kinematica_Demo | UCL | n/a | LFS-only (raw URLs return pointers) | parkour | No |
| Bandai-Namco / LAFAN1 | CC BY-NC(-ND) | n/a | n/a | n/a | License excludes |
| Mixamo, Sketchfab, ActorCore, RPM | accounts | n/a | n/a | n/a | Not allowed |

### 7.2 What was downloaded and inspected

All of it is under `RawAssets/cc0/characters/`, 323 MB.

**`quaternius_ual/`** (UAL1 Standard v3.0, 16 Jun 2026; zip 15,904,933 B, extracted):

- Files: `Unity/UAL1_Standard.fbx` (in place), `Unity/UAL1_Standard_RM.fbx` (root motion), plus `.glb` copies.
- FBX 7400, Blender export, Z-up, UnitScale 100, 30 fps. Each clip is a separate take (`Armature|<Name>`). Mesh `Mannequin`, 13,744 tris.
- Hierarchy is clean: `thigh_l <- pelvis <- root`, `clavicle_l <- spine_03`.
- Clips (43): A_TPose, Crouch_Fwd_Loop, Crouch_Idle_Loop, Dance_Loop, Death01, Driving_Loop, Fixing_Kneeling, Hit_Chest, Hit_Head, Idle_Loop, Idle_Talking_Loop, Idle_Torch_Loop, Interact, Jog_Fwd_Loop, Jump_Start/Loop/Land, PickUp_Table, Pistol_Aim_Down/Neutral/Up, Pistol_Idle_Loop, Pistol_Reload, Pistol_Shoot, Punch_Cross, Punch_Jab, Push_Loop, Roll, Sitting_Enter/Idle_Loop/Talking_Loop/Exit, Spell_Simple_*, Sprint_Loop, Swim_Fwd/Idle, Sword_Attack, Sword_Idle, Walk_Formal_Loop, Walk_Loop.

**`quaternius_ual2/`** (UAL2 Standard v2.2; zip 18,735,003 B):

- Clips (43): A_TPose, Chest_Open, **ClimbUp_1m**, Consume, Farm_*, **Hit_Knockback**, Idle_FoldArms_Loop, Idle_Lantern_Loop, Idle_No_Loop, Idle_Rail_Call/Loop, Idle_Shield_*, Idle_TalkingPhone_Loop, **LayToIdle**, Melee_Hook(+Rec), NinjaJump_*, **OverhandThrow**, Shield_*, **Slide_Start/Loop/Exit**, Sword_* combos, TreeChopping_Loop, **Walk_Carry_Loop**, Yes, Zombie_*.
- Also includes `Female Mannequin/Unity/Mannequin_F.fbx` (14,612 tris) and a `.blend`. Do not copy the `.blend` into Assets.

**`rocketbox/`** (MIT):

- `Military_Male_01` complete, 146 MB:
  - `Export/Military_Male_01.fbx`: FBX 7400, Y-up, cm. One mesh `sm002_hipoly_81_bones`, **15,387 tris**. 80 LimbNodes: Biped body, 5 fingers per hand, eyes, jaw and 25 facial bones. There is also a `Bip01 Footsteps` null.
  - `Export/Military_Male_01_facial.fbx`: same mesh plus a 5 s facial animation sample, 246 curves.
  - Textures `sm002_{body,equipment,head,helmet,combat_knife}_{color[_acu],normal,specular}.tga`: 2048² RGB (equipment is 2048×1024).
- `Male_Adult_05/Export/Male_Adult_05.fbx`: 7,836 tris. Field vest and cargo trousers; reads well as a CIA case officer in civilian clothes.
- `Animations/`: `m_walk_neutral_01` (1.2 s), `m_run_neutral_01`, `m_crouch_in`, `m_crouch_idle`, `m_idle_look_around_01`, **`m_sit_table_idle_nervous_01` (34 s, ideal for the interrogation room)**. Each is about 121 bones (Biped + `*Nub` ends + `MotionExtractionHelper`). Folders: `all_animations_max_motextr_static` (in place) and `_xy` (root motion in plane).
- `Editor/FixRocketboxMaxImport.cs`: the official AssetPostprocessor. It renames `Bip02`, disables non-hipoly LODs, flags normal maps, and can re-parent thighs and clavicles (see 7.3). It forces `animationType = Generic`; change that to Humanoid. Its material fixes target the Built-in Standard shader; replace them with URP Specular.
- `LICENSE.md` (MIT) and `README.md`.
- **Important rig quirk, verified from the FBX connections:**
  - `Bip01 L Thigh <- Bip01 Spine <- Bip01 Pelvis <- Bip01`: the thighs are children of the **Spine**, not the Pelvis.
  - `Bip01 L Clavicle <- Bip01 Neck`: the clavicles are children of the **Neck**.
  - Mecanim still accepts this because human bones only need to be descendants of their parent human bone. Bending the spine, though, drags the hips. The script's `usingMixamoAnimations = false` branch re-parents both, which is the cleaner path for retargeting UAL and Unity clips.
- Other military variants:

  | Avatar | Gear | Archive size |
  |---|---|---|
  | `Military_Male_03` | helmet, vest, pistol | 146 MB |
  | `Military_Male_04` | helmet, vest | 136 MB |
  | `Military_Male_02`, `Military_Male_05`, `Military_Male_06` | patrol cap, no vest or helmet; best base for 1980s enemy regulars | about 78 MB each |

  The URL pattern is `https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/master/Assets/Avatars/Professions/<Name>/{Export/<Name>.fbx, Textures/*.tga}`.

**`unity_standard_assets_characters_UCL/`** (Unity Companion License, `LICENSE.md` kept):

- `DefaultMale.fbx`: FBX 7500, 15,016 tris. 68 bones already in Unity humanoid naming (Hips, LeftUpperLeg … Jaw, LeftEye).
- `defaultmale_rig.fbx`.
- Clips: `m@StrafeIdle`, `m@WalkForwards`, `m@RunForwards`, `m@SprintForwards`, `m@StrafeWalkForwards`, `m@StrafeRight`, `m@StrafeRightWalkBackwards`, `m@StrafeRightRunForwards`. The repo has about 100 male clips covering all strafe, turn and jump variants; mirror the "Right" clips for left.

**How the itch.io free download was scripted** (`RawAssets/cc0/tools/itch_free_download.sh <user> <slug> <outdir>`, no account):

1. `GET /<slug>/purchase` to get the CSRF token from `<meta name="csrf_token">`.
2. `POST /<slug>/download_url` with `csrf_token` returns JSON `{url: ".../download/<key>"}`.
3. `GET` that page. It sets a session cookie and lists `data-upload_id`.
4. `POST /<slug>/file/<upload_id>?source=game_download&after_download_lightbox=1&as_props=1` with the page's `csrf_token` (no key param) returns a signed R2 URL.
5. `GET` the R2 URL.

itch rate-limits: HTTP 429 came back both with a bare `Mozilla/5.0` UA and after a burst of page loads. Send a full Chrome UA and space out requests.

### 7.3 Recommended character and animation path

1. **Bodies:**
   - Import Rocketbox FBX plus textures into `Assets/Characters/Rocketbox/<Name>/` with `FixRocketboxMaxImport.cs` in `Assets/Editor/`. Edit the script: set `usingMixamoAnimations = false` so thighs and clavicles are re-parented, and set `importer.animationType = ModelImporterAnimationType.Human`.
   - Avatar: Create From This Model. Hips = `Bip01 Pelvis`, Spine/Chest/UpperChest = `Bip01 Spine/Spine1/Spine2`, Head = `Bip01 Head`, Jaw = `Bip01 MJaw`, eyes = `Bip01 L/REye`. Check Configure and Enforce T-Pose.
   - Materials: URP/Lit Specular, 1k for AI and 2k for squad heads in cutscenes.
2. **Animation libraries:**
   - Import `UAL1_Standard_RM.fbx`, `UAL2_Standard_RM.fbx` (root motion; or the non-RM files for in-place) and the Unity Standard Assets clip FBXs as **Humanoid** with Avatar Create From This Model. Each is its own skeleton; retargeting happens in muscle space.
   - Split takes in the Animation tab, set Loop Time on `*_Loop`, and bake root rotation and Y into pose.
   - The UAL `A_TPose` take is a ready reference pose.
3. **Weapon handling:**
   - Install `com.unity.animation.rigging` (Unity registry, free).
   - Per soldier rig: a weapon socket on `Bip01 R Hand`; a Two Bone IK constraint for the left hand onto the weapon's foregrip transform; Multi-Aim constraints on Spine1, Spine2 and Head toward an aim target; a Multi-Parent rig to switch the weapon between hand and sling.
   - Override layer: an upper-body Avatar Mask layer holding either a rifle pose clip (from Meshy, 7.5) or a static "aim" pose. Recoil and sway are procedural offsets on the aim target and weapon pivot. Reload plays a Meshy reload clip on the upper-body layer, or a scripted magazine-swap tween with left-hand IK following the magazine.
   - Deaths blend into a ragdoll: build it with Unity's Ragdoll Wizard on the Biped bones.
4. **Player (first person):** run the same Rocketbox soldier body in the FP rig. Scale the `Bip01 Head` bone to 0 on the FP render layer, or render only the arms with a separate culling mask. Keep the camera at the head. Drive the arms with the same IK onto viewmodel weapon sockets. This avoids needing a separate FP-arms asset.
5. **Cinematics:**
   - Interrogation: Rocketbox `m_sit_table_*` clips (cough, nervous, look around, scratch head, roll head) on the prisoner, `m_gestic_talk_*` on the interrogator, plus the `*_facial.fbx` sample.
   - Facial bones are non-human transforms: enable them under the clip's Mask > Transform, or play facial clips on a Generic Animator child.
   - Briefing and debrief: `m_gestic_*`, `m_documents_*`, `m_idle_*`.
6. **Squad and AI locomotion:** an 8-direction blend tree from the Standard Assets strafe set, plus UAL jog and sprint, UAL crouch, UAL2 slide and mantle (ClimbUp_1m), and UAL2 OverhandThrow for grenades. Use a NavMeshAgent with root motion or in-place clips plus speed matching.

### 7.4 Clip coverage (game need → source)

| Need | Source clip(s) |
|---|---|
| Idle / alert | UAL `Idle_Loop`; Rocketbox `m_idle_look_around_*`; Meshy 2 `Alert`, 334 `Lower_Weapon_Look_Raise` |
| Walk / jog / sprint | Unity SAC `m@WalkForwards`, `m@RunForwards`, `m@SprintForwards`; UAL `Walk_Loop`, `Jog_Fwd_Loop`, `Sprint_Loop` |
| Strafe 8-dir (aiming) | Unity SAC `m@Strafe*` (+mirror); Meshy 233/234 walk back/forward while shooting, 528 `Walk_Left_with_Gun`, 529 `Walk_Backward_with_Gun` |
| Crouch idle / move | UAL `Crouch_Idle_Loop`, `Crouch_Fwd_Loop`; Meshy 523–526 `Cautious_Crouch_Walk_*`, 527 `Crouch_Walk_Left_with_Gun` |
| Rifle aim / fire | **gap**: Meshy 98 `Run_and_Shoot`, 234 `Walk_Forward_While_Shooting`, 585 `Rifle_Aim_Turn_Right`, 573 `Rifle_Turn_Left`, 511 `Rifle_Charge`; plus procedural IK aim and recoil |
| Reload | **gap**: Meshy 170 `Standing_Reload`, 165 `Kneeling_Reload`, 166 `Running_Reload`, 167 `Slow_Walk_Reload`, 168 `Prone_Reload`; UAL `Pistol_Reload` (sidearm) |
| Grenade | UAL2 `OverhandThrow`; Meshy 239 `Crouch_Pull_and_Throw`, 542 `Walk_Backward_with_Grenade` |
| Hit reactions | UAL `Hit_Chest`, `Hit_Head`; UAL2 `Hit_Knockback`; Meshy 177 `Gunshot_Reaction`, 178/179 `Hit_Reaction`, 171 `Hit_Reaction_to_Waist` |
| Deaths | UAL `Death01`; Meshy 183 `Shot_and_Fall_Backward`, 184 `Shot_and_Fall_Forward`, 182 `Shot_and_Blown_Back`, 180 `Shot_in_the_Back_and_Fall`, 188 `Fall_Dead_from_Abdominal_Injury`; then ragdoll |
| Cover / vault / mantle | UAL2 `ClimbUp_1m`; Meshy 425 `Vault_with_Rifle`, 426 `Roll_Behind_Cover`, 427 `Vault_and_Land` |
| Slide | UAL2 `Slide_Start/Loop/Exit` |
| Prone / crawl | Meshy 168 `Prone_Reload`, 549 `Crawl_Backward`, 340 `Crawl_and_Look_Back` (UAL1 Pro has crawl, paid) |
| Interrogation / sitting | Rocketbox `m_sit_table_*`, `m_sit_chair_*`; UAL `Sitting_*`; Meshy 307 `Sitting_Answering_Questions`, 33 `Chair_Sit_Idle_M` |
| Briefing gestures / radio | Rocketbox `m_gestic_talk_*`, `m_cell_phone_talk_01` (works as a radio handset pose); UAL2 `Idle_TalkingPhone_Loop`; Meshy 312 `Phone_Conversation` |
| Door breach / interact | UAL `Interact`, `Push_Loop`; Meshy 285–289 `open_door*`, 103 `Simple_Kick` |
| **Squad hand signals** (halt, rally, move up) | **missing everywhere**: generate with fal `fal-ai/hunyuan-motion` text prompts, or as an IK-posed upper-body clip |

### 7.5 fal for rifle clips (recommended next test)

- **`fal-ai/meshy/rigging/multi-animation`.** Input: a public GLB humanoid (`model_url`) and up to 10 `animation_action_ids` (0–696) per call. Output: rigged FBX/GLB plus one FBX/GLB per clip. The library is listed at https://docs.meshy.ai/en/api/animation-library and the IDs above come from that page.
- **Plan:** upload the CC0 UAL mannequin (`Unreal-Godot/UAL1_Standard.glb` or `Female Mannequin/.../Mannequin_F.glb`) with `fal_client.upload_file` and request about 30 clips in 3–4 calls. Import each FBX as Humanoid and retarget onto Rocketbox. The preset names suggest a rifle-hold pose; confirm the hand placement before committing. Expect left-hand IK fix-ups whatever the source.
- **`fal-ai/meshy/rigging`** (single preset, plus walk and run) can also auto-rig fal-generated 3D characters (Meshy 7 or Tripo image-to-3D) if bespoke 1980s outfits are wanted later.
- **`fal-ai/hunyuan-motion`** (and the 0.46B `/fast` variant) does text-to-motion, 0.5–12 s, `output_format: "fbx"`. Use it for hand signals, "soldier crouch-runs to cover and raises rifle", or bound-prisoner struggling. The skeleton is SMPL-style, so map the Humanoid avatar manually once.

### 7.6 Making Rocketbox soldiers read as 1980s, fictional and fair

- **The ACU digital camo, US-flag patch and MOLLE vest date from about 2004+.** The texture atlases are clean (see `sm002_body_color_acu.tga`): large shirt, trouser and vest islands. Use a Python/PIL pass that:
  1. masks the ACU pixels (low-saturation grey-green, HSV threshold);
  2. fills them with a **seamless period fabric** (solid OD/khaki, 1980s-style six-color desert, or an invented regime pattern) from fal, multiplied by the high-passed original luminance so folds and seams survive;
  3. paints out the flag and name tapes.
- Enemy regulars: `Military_Male_02/05/06` (patrol cap, no vest) recolored to the fictional army's uniform, with Poly Haven `old_gas_mask` / helmet props.
- Squad: Military_01/03/04 recolored, or civilian Rocketbox adults (`Male_Adult_05`'s vest look suits CIA/SOG "sterile" kit).
- Spread Rocketbox's varied faces across **both** sides and add helmets, masks and balaclavas, so no real ethnic group reads as "the enemy".

## 8. Gaps fal should fill (nothing CC0 found)

| Gap | Why | Suggested fal route |
|---|---|---|
| 1980s weapons: AK-pattern, M16A1/CAR-15, FAL, Uzi, M1911, RPG-7, M60, frag grenade | none on Poly Haven or ambientCG; `service_pistol` and `bolt_action_rifle_7_62` are WWII-era placeholders | `meshy/v7.1/image-to-3d` or `tripo3d/p2/image-to-3d` from fal-generated orthographic concept images. Generate the magazine as a separate mesh for reloads. Use no real manufacturer markings. |
| Vehicles: technical pickup, APC, helicopter, sedan | only `covered_car` | same image-to-3D; static meshes with separate wheels or rotors |
| Sandbags (single bag mesh) | absent in both libraries | image-to-3D or hand-modelled pillow mesh with `hessian_230`; instance into walls |
| Camo / uniform fabrics (period desert, OD, fictional patterns) | none in either library | fal image model with a "seamless tileable fabric texture" prompt, plus normal/roughness derived in PIL or a material-maps model |
| True mudbrick / adobe brick wall | only `clay_plaster`, `patterned_clay_wall` | tileable texture generation |
| Date palms, oleander, desert grasses | Poly Haven flora is Karoo | image-to-3D for trunks; alpha-card fronds from generated images |
| Decals: bullet holes (plaster, metal, concrete), blood, scorch, propaganda posters, invented-script signage, graffiti | none suitable | fal image generation with transparent backgrounds, or a matte-removal step |
| Interrogation and intel props: wall map, dossier folders, photos, reel-to-reel recorder, 1980s field telephone, car battery | partly covered (`Television_01/02`, `vintage_radio_transceiver`, `cassette_player`, `filmstrip_projector_8mm`, `desk_lamp_arm_01`, `metal_office_desk`) | image-to-3D; textures for documents |
| Rifle-specific and signal animations | see 7.4 / 7.5 | `fal-ai/meshy/rigging/multi-animation`, `fal-ai/hunyuan-motion` |
| Audio (weapons, ambience, radio, VO) | neither library has audio | fal audio/TTS models (outside this doc) |

## 9. On disk

```
RawAssets/cc0/ (362 MB)
  hdri/goegap_2k.hdr                                     5.4 MB
  textures/polyhaven/sand_01/*_2k.jpg                    6 maps, 7.2 MB
  textures/polyhaven/cracked_concrete_wall/*_2k.jpg      6 maps, 16.9 MB
  textures/ambientcg/CorrugatedSteel005/..._1K-JPG.zip   5.9 MB
  textures/ambientcg/TireTracks001/..._1K-JPG.zip        2.8 MB
  models/polyhaven/Barrel_01/gltf_1k/                    0.7 MB (fetch-script test)
  characters/quaternius_ual/   (zip + extracted)          76 MB
  characters/quaternius_ual2/  (zip + extracted)          88 MB
  characters/rocketbox/        (Military_Male_01 full, Male_Adult_05 fbx, 6 anims, Editor script, LICENSE)  150 MB
  characters/unity_standard_assets_characters_UCL/  (DefaultMale + 8 clips + LICENSE)  9 MB
  tools/fetch_cc0.py              manifest-driven downloader (hdri | tex | tex-core | model | acg)
  tools/itch_free_download.sh     Quaternius itch.io $0 download flow
Docs/research/cc0_manifest.json  every URL, size and md5 above
```

## 10. Risks and open questions

- **itch.io flow:** it is undocumented and could change, and it rate-limits with 429. The packs are CC0, so once downloaded they can be vendored.
- **Rocketbox:** the Biped quirks (thigh and clavicle parenting), 2010-era topology (hands and fingers are fine, no blendshapes), modern uniforms that need retexturing, and the MIT attribution requirement.
- **Meshy and Hunyuan outputs:** quality and rifle-hand alignment are unverified, each call costs money, and usage terms follow fal/provider ToS rather than CC0. Test 10 clips first.
- **Heavy Poly Haven meshes:** decimating them needs an approved tool (`@gltf-transform/cli` or `gltfpack` via npx, or UnityMeshSimplifier). Without one, restrict them to distant use or skip them.
- **Prop text:** `cement_bag` prints English, `cigarette_pack` uses the fictional brand "Kentucky Ace", `russian_food_cans_01` carries Cyrillic labels (useful for a Soviet-supplied regime), and the `rollershutter_*` graffiti variants use Latin script. Pick deliberately.
- **WebGL limits:** the 15 MB per-file host limit forces Addressables chunking. HDR cubemaps cannot rely on BC6H.
- **UAL1 Pro and UAL2 Source** ($9.99 / $14.99) add 8-direction locomotion and crawling but no rifle set visible on the tier sheet. Buying them is the user's decision.
