# fal.ai for 3D assets: research and test results

Date: 2026-10-16. Unity 6000.6.6f1 / URP 17.6, WebGL target.
Outputs from every test live under `RawAssets/fal3d/`. Section 10 lists the files.

---

## 0. Summary

- **Weapons (image to 3D):** a Nano Banana 2.1 product shot ($0.06) fed to **Tripo P2** (`tripo3d/p2/image-to-3d`, $1.20) gave a first-person-ready AK at **19,944 tris** with 4K PBR maps (base color, metal/rough, normal) in 111 s. No decimation was needed. **Hunyuan 3D v3.1 Pro** ($0.675 with PBR and a custom face count) matched the reference more closely on the visible side. It needed decimation from 40k tris, and its unseen side came out darker. Both are usable. Use Tripo P2 by default and Hunyuan as the fallback for hero weapons.
- **Characters:** the only rigging on fal is **Meshy**. `fal-ai/meshy/rigging/multi-animation` rigged a generated soldier and returned 5 clips plus free walk/run in 172 s for $0.80. The FBX imported into Unity 6 as a **valid Humanoid with 22 bones auto-mapped**, and the clips imported as humanoid motion. So rigged and animated humanoids work end to end without Mixamo, Blender or any account. The main limit is the **24-bone skeleton, which has no finger bones**, so hands stay open.
- **Text-to-3D is not reliable for period accuracy.** Meshy 7.1 text-to-3D ignored "1980s, boonie hat, no weapon" and produced a modern operator with a carbine fused to his chest. **Always go image to 3D from a reference image you control.**
- **Retopology and LODs without Blender:** `gltf-transform simplify` and `gltfpack -si` (meshoptimizer, via npm) work on the outputs. Unity 6.2+ also has built-in **Mesh LOD** generation in the FBX importer. fal also hosts Tripo Remesh (quad or tri at 500–20k faces, bakes textures) and Hunyuan Smart Topology ($0.75).
- **Blender (`brew install --cask blender`)** is not required to ship, but it would noticeably improve FP weapons (high-to-low normal bakes, splitting the magazine and bolt for reload animations) and characters (adding finger bones, fixing weights). Section 8 covers it.
- The 5 paid calls cost about **$3.94** in total, at the documented fal prices.

---

## 1. What fal hosts today (catalog snapshot 2026-10-16)

Source: `https://api.fal.ai/v1/models?category=image-to-3d|text-to-3d|3d-to-3d` (46 / 14 / 15 endpoints). Prices come from each model's `llms.txt`. Latency is **measured** where marked (M). Other latency figures are typical values or unknown.

### 1a. Image to 3D and text to 3D (PBR-capable, game-relevant)

| Endpoint | Price per call | Polycount control | PBR maps | Output formats | Notes |
|---|---|---|---|---|---|
| `tripo3d/p2/image-to-3d` (+ `/text-to-3d`) | $1.00 untextured, $1.10 fast/standard (2K), $1.20 detailed (4K), $1.30 extreme (8K) | `face_limit` 48–50,000 (quad ≤25k); `quad` (no extra cost) | yes (`pbr`): base, MR, normal | GLB (FBX when quad) | Newest (2026-09-20). Has `delight`, `auto_size` (**gave a wrong scale, see section 2**), and `export_orientation`. **111 s (M)**. Returns `task_id`. |
| `tripo3d/h3.1/image-to-3d` (+ multiview, text) | $0.20 geometry, $0.30 standard tex, $0.40 HD tex; +$0.20 detailed geometry; +$0.05 quad | `face_limit` 1k–2M | yes | GLB/FBX | Cheaper Tripo. Has `texture_alignment`. |
| `tripo3d/p1/image-to-3d` | $0.40 / $0.50 | `face_limit` 48–20,000 | basic tex | GLB | Older low-poly line. |
| `meshy/v7.1/image-to-3d` (+ `multi-image-to-3d` with 1–4 views, + `text-to-3d`) | $0.80 geometry, **$1.20 textured**; +$0.20 rigging; +$0.12 animation | `target_polycount` 100–300k (default 30k); `model_type` standard / **lowpoly** / **smart-topology** (≤15k); `topology` quad/tri | `enable_pbr`: base, metallic, roughness, normal (separate PNGs) | GLB, FBX, OBJ, USDZ, STL, plus texture PNGs | Has `pose_mode` a-pose/t-pose, `symmetry_mode`, `geometry_resolution` up to 4k. **Rigging and animation are built in.** Text-to-3D took **320 s (M)**. |
| `fal-ai/hunyuan-3d/v3.1/pro/image-to-3d` (+ text) | $0.375; +$0.15 PBR; +$0.15 multi-view; +$0.15 custom face count | `face_count` **40k–1.5M** (default 500k) | yes: base, MR, normal (4K PNG) + `KHR_materials_specular` | GLB + OBJ/MTL | Up to 8 views (front/back/left/right/top/bottom/two 45°). **160 s (M)**. Lowest-cost high-fidelity option. |
| `fal-ai/hunyuan-3d/v3.1/rapid/image-to-3d` (+ text) | $0.225 (+$0.15 PBR) | none | yes | GLB/OBJ | For bulk background props. |
| `fal-ai/hyper3d/rodin/v2.5` (+ `/text-to-3d`) | $0.40 (+$0.80 HighPack) | `quality_mesh_option` 2K / 20K / 50K / 150K / 500K / 1M / 2M tris or 4K–200K quads; `tier` Minimum…Extreme-High | `material` PBR / Shaded / All | GLB, FBX, OBJ, USDZ, STL | Has `texture_delight`, `TAPose`, `bbox_condition`, up to 5 images. Native FBX output. **Untested**: the next candidate to try. |
| `fal-ai/hyper3d/rodin/v2.5/fast` (+ text) | $0.10 | up to 20K tris | default Shaded (PBR selectable) | same | Cheap prototyping and background clutter. |
| `fal-ai/trellis-2` | $0.25 / $0.30 / $0.35 (512 / 1024 / 1536) | `decimation_target` **vertices** 5k–2M; `remesh` | base + MR in GLB | GLB | Many sampler knobs, `texture_size` 1024/2048/4096. Commercial license on fal. |
| `fal-ai/pixal3d` | $0.30 / $0.42 | `decimation_target` 5k–1.58M verts | yes | GLB | Trellis-2-like. |
| `hitem3d/hi3d/v3.0/image-to-3d` | **$2.10** / $9.10 | `face_count` 100k–5M | yes | GLB/OBJ/STL/FBX/USDZ | Film or print density, so far too heavy for WebGL. The v1.5–2.1 endpoint costs $0.30–0.90. |
| `fal-ai/reconviagen-0.5` | $0.25–0.35 | — | — | GLB | Reconstructs from multiple images. |
| `fal-ai/sam-3/3d-objects` | $0.02 per unit | — | — | GLB | Reconstructs objects from real photos, not studio shots. |
| `fal-ai/bytedance/seed3d/image-to-3d` | about $0.33 ($0.011 per 1k tokens, ~30k tokens) | none | ? | ZIP | **Seed3D exists but does not appear in the category listing**, so it may be hidden or legacy. Untested. |
| `tripo3d/triposplat` | $0.05 | — | — | Gaussian splat | Not a mesh, so not usable here. |

Deprecated: Meshy 6 multi-image, Hunyuan 2.1, Omnipart, PSHuman, Ultrashape.

### 1b. Rigging and animation for humanoids

| Endpoint | Price | Input → output | Notes |
|---|---|---|---|
| `fal-ai/meshy/rigging/multi-animation` | **$0.20 + $0.12 per clip**, at most 10 clips | any **humanoid GLB URL** (from any generator) + `animation_action_ids` + `height_meters` → rigged GLB+FBX, free walk/run (GLB, FBX, armature-only GLB), and one GLB+FBX per clip | **Tested (M: 172 s, 5 clips)**. Meshy library IDs 0–696 ([list](https://docs.meshy.ai/en/api/animation-library)). |
| `fal-ai/meshy/rigging` | $0.20 (+$0.12 with one animation) | GLB → rigged GLB/FBX + walk/run | The pricing API lists `0.8 / generation` for this ID while the docs say $0.20. Check the dashboard after the first use. |
| Meshy 7.1 `enable_rigging` / `enable_animation` | +$0.20 / +$0.12 on top of the 3D call | generate, rig and animate in one call | Same rigger. |
| `fal-ai/hunyuan-motion` ($0.08) / `fal-ai/hunyuan-motion/fast` ($0.06) | per generation | text prompt (0.5–12 s) → **FBX motion** or JSON | HY-Motion 1.0. Reports say it uses an SMPL-H-style skeleton. **Untested.** Useful for bespoke cinematic beats. Retarget it in Unity Humanoid; manual bone mapping may be needed. |
| `fal-ai/sam-3/3d-body` | $0.02 per unit | image → body mesh + MHR pose params + keypoints | Estimates pose and body shape. Not a rigger. |
| Not on fal | — | — | UniRig, Tripo rig (Tripo outputs mention `task_id` for "rig", but every `tripo3d/*/rig*` ID returns 404), and Mixamo-like services. |

Video "motion control" models (Kling, Wan Motion, DreamActor, SCAIL) produce 2D video only, so they are useless for 3D animation.

### 1c. Retopology, parts and retexturing

| Endpoint | Price | What it does |
|---|---|---|
| `tripo3d/tripo/remesh` | $0.01 per Tripo credit. The credit count per call is not documented; expect tens of credits. | GLB/OBJ/FBX/STL (≤150 MB) → clean **tri or quad** at `face_limit` 500–20,000 (quad ≤10k), `bake` textures **on** by default, keeps `part_names` from segmentation. Usable as a Blender-free "bake down" from a 500k Hunyuan mesh. **Untested.** |
| `fal-ai/hunyuan-3d/v3.1/smart-topology` | $0.75 | GLB/OBJ → tri or quad at `face_level` low/medium/high. Texture retention not stated. |
| `tripo3d/tripo/segment` / `fal-ai/hunyuan-3d/v3.1/part` | $0.01 per credit / $0.45 | Split a mesh into semantic parts, for example a separate magazine. |
| `meshy/v7/retexture`, `meshy/v6/retexture` | $0.30 (2K/4K), $0.45 (8K), PBR included | New textures on an existing mesh from a prompt or image. Use it for uniform variants. |
| `fal-ai/trellis-2/retexture`, `hitem3d/hi3d/texture` | $0.20–0.24 / $0.02 per credit | Same idea. |
| `fal-ai/patina/material` (category text-to-image) | — | **Seamless tiling PBR materials** (normal, roughness and more). Use it for rugs, plaster and sand when ambientCG lacks a match. |

---

## 2. Test results (5 paid calls)

| # | Endpoint | Key params | Wall time | Cost (doc price) | Result |
|---|---|---|---|---|---|
| 1 | `google/nano-banana-2.1` | 2K, 16:9, PNG | 28.5 s | ~$0.06 | 2752×1536 AKM side profile on white. Excellent on the first try. |
| 2 | `tripo3d/p2/image-to-3d` | face_limit 20000, pbr, texture_quality detailed, delight, auto_size | 111 s | $1.20 | 19,944 tris / 23,528 verts, 1 material, 4K base (JPG), 4K MR (JPG), 4K normal (PNG). 10.5 MB GLB. |
| 3 | `fal-ai/hunyuan-3d/v3.1/pro/image-to-3d` | enable_pbr, face_count 40000 | 160 s | $0.675 | 40,000 tris / 26,509 verts. Three 4K **PNG** maps + KHR_materials_specular. **34.5 MB GLB.** Also returns OBJ/MTL. |
| 4 | `meshy/v7.1/text-to-3d` | full, target_polycount 20000, pbr, a-pose | 320 s | $1.20 | 20,693 tris. 2K base, MR and normal (metallic empty). GLB 9.6 MB, plus FBX, OBJ, USDZ, STL and separate PNGs. |
| 5 | `fal-ai/meshy/rigging/multi-animation` | GLB from #4, height 1.8, clips [0, 690, 654, 170, 183] | 172 s | $0.80 | Rigged GLB+FBX, walk and run, 5 clips (GLB+FBX each). |

Queue time was about 3.5 s for every call. Renders were made with a three.js viewer (RoomEnvironment IBL, ACES) and are in `RawAssets/fal3d/renders/`.

### 2.1 Rifles: Tripo P2 vs Hunyuan 3.1 Pro
(See `renders/compare_rifle_detail_tripo_top_hunyuan_bottom.png`, `..._wireframe_...png`, `tripo_rifle_views.png`, `hunyuan_rifle_views.png`.)

- **Silhouette and proportions:** both are excellent and faithful to the reference: slant brake, gas tube, rear sight, sling swivel, trigger guard and magazine ribs.
- **Tripo P2:** both sides are textured coherently, because the mirrored side was generated sensibly. The metal reads as weathered blued steel, close to the reference. The wood grain is slightly exaggerated and painterly, and the normal map has some lumpy "melted" noise on the wood. **Topology is game-style adaptive:** large triangles on flat receiver panels and density spent on rivets and curves. Its 20k budget is used very efficiently.
- **Hunyuan 3.1 Pro:** the visible (reference) side is sharper and more faithful (dust-cover ribs, rivets, more natural grain). The metal is a flatter grey. The **hallucinated far side is noticeably darker** (dark walnut, near-black metal), which is a texture-consistency defect. The mesh is uniform and isotropic.
- **Scale and orientation:** Tripo `auto_size=true` produced a **3.016 m** rifle. An AKM is about 0.88 m, so do not trust it. Hunyuan is 1.18 m with a +90° X node rotation. **Always normalize scale in import.**
- **Metal and roughness:** both produce real metal/wood separation in MR maps (Tripo metallic mean 0.59, roughness 0.45).
- **Verdict:** **Tripo P2 for FP and world weapons**, because it lands on budget with consistent sides. Use Hunyuan Pro with multi-view input (add a generated right-side and top image, +$0.15) for hero guns. That fixes the dark-side problem, and you decimate or remesh afterwards.

### 2.2 Decimation and texture tests (local, free)
| Asset | Tool | Result |
|---|---|---|
| Tripo FP | `gltf-transform resize 2048` | 19,944 tris, three 2K maps, **4.78 MB** (from 10.55 MB) |
| Tripo world | `gltf-transform weld` + `simplify --ratio 0.25 --error 0.01` + resize 1024 | Stopped at **7,460 tris** (error cap and UV seams), 1.74 MB |
| Tripo world | `gltfpack -si 0.25 -noq` + resize 1024 | **6,129 tris**, 1.65 MB. Clean at distance. |
| Hunyuan FP | weld + `simplify 0.5 --error 0.001` + resize 2048 | **20,000 tris**, 15.7 MB because the textures stay PNG. Fine as a source; Unity recompresses. |
| Hunyuan world | `simplify 0.125` + resize 1024 | 5,000 tris, 4.6 MB. **Visible shading facets on the receiver**, because the normal map was baked for the dense mesh. |

Takeaways: decimating by ≤50% keeps the generated normal map valid. Aggressive decimation (>75%) breaks it, and the fix is a re-bake (Tripo Remesh `bake:true`, or Blender). gltfpack's npm build only resizes textures together with `-tc/-tw` compression, so use gltf-transform for resizing.

### 2.3 Soldier via Meshy 7.1 text-to-3D
(See `renders/meshy_soldier_views.png`, `04_soldier_meshy71_t23d/preview.png`.)
- It honored the 20k polycount (20,693) and the A-pose, and produced a plausible face, boots, pouches and gloves. Textures are 2K. The look is "decent AA / indie".
- **It ignored the era and the negatives:** it produced a modern high-cut helmet with a comms headset, a plate carrier and an M4-type carbine **fused to the chest**, with no 6-color camo, no boonie hat and no beard.
- The cloth shades with **faceted, crumpled normals** (visible even at full-body framing).
- Conclusion: **never use text-to-3D for characters.** Generate a controlled reference image (era-correct, empty hands, nothing slung) and use image or multi-image to 3D.

### 2.4 Meshy rigging + multi-animation
(See `renders/compare_meshy_rig_clips_walkshoot_reload_charge_death.png`.)
- **Skeleton:** 24 joints in Mixamo-style names without a prefix: `Hips, Spine02, Spine01, Spine, neck, Head, head_end, headfront, Left/RightShoulder, Left/RightArm, Left/RightForeArm, Left/RightHand, Left/RightUpLeg, Left/RightLeg, Left/RightFoot, Left/RightToeBase`. **There are no finger bones, no face bones and no twist bones.**
- **Clips:** Idle (4.03 s), Walk_Forward_While_Shooting_inplace (3.30 s), Rifle_Charge_inplace (0.53 s, a run cycle), Standing_Reload (3.33 s), Shot_and_Fall_Backward (3.53 s), plus walking (1.07 s) and running (0.67 s). Skinning deforms cleanly in the renders, and rifle-hold poses read correctly with an invisible gun. The rifle that was fused to the chest stays baked onto the torso.
- **Files:** each clip ships as a full mesh copy (GLB 10.5 MB, FBX 5.3 MB, binary FBX 7.4). The FBX embeds **only the base-color JPG**, so take the normal and MR maps from the GLB.
- **Unity 6000.6 import (verified in a temporary folder, now moved to the trash):**
  - `rigged_character.fbx` with Animation Type = **Humanoid** gives a valid human Avatar. **22 bones auto-mapped** (Spine→Spine02, Chest→Spine01, UpperChest→Spine, plus neck, shoulders, toes). The file scale of 0.01 gives a correct 1.80 m height. The SkinnedMeshRenderer has 23 bones, 26,703 verts and 20,693 tris.
  - A URP/Lit material is created, **but no texture is bound.** Assign the maps manually or extract them.
  - Clip FBXs imported with *Copy From Other Avatar* give `humanMotion = true`. In-place clips have zero average velocity, which suits a CharacterController-driven locomotion setup. The death clip has root motion (about 0.34 m/s).
  - One harmless warning: the base rig FBX contains a 0-frame `clip0`. Untick Import Animation on that file.

---

## 3. Importing into Unity 6

### FBX (native). Use it for everything skinned.
- Import Meshy `rigged_character.fbx` as Humanoid / Create From This Model. Import each clip FBX as Humanoid / Copy From Other Avatar and turn off Import Materials on the clip files.
- Because the rig is Humanoid, **any** Humanoid clip retargets onto any Meshy-rigged character. Rig every new character with plain `fal-ai/meshy/rigging` ($0.20) and **reuse one shared clip library**, so you pay for clips once.
- Unity 6.2+ **Mesh LOD**: Model importer → LODs → Generate Mesh LODs. It works on FBX imports. For skinned meshes it ignores weights and does not reduce skinning cost, so it only saves raster work. Not available for glTFast imports.
- Tripo outputs FBX only for quad meshes. Rodin supports FBX directly. Hunyuan gives GLB or OBJ only.

### GLB/glTF via glTFast. Use it for static weapons and props.
- Package: `com.unity.cloud.gltfast` **6.20.0** (latest stable, 2026-08-27, requires Unity 6000.0+, pulls burst/collections/mathematics). Optional packages: `com.unity.cloud.ktx` 3.7.1 (KTX2/Basis), `com.unity.cloud.draco`, `com.unity.meshopt.decompress` 0.3.0-exp.1. None are needed if we don't compress inside the GLB.
- Dropping a `.glb` or `.gltf` into Assets creates a prefab with Mesh, Material, Texture and AnimationClip sub-assets. URP is fully supported through glTFast's own shader graphs. Animation imports as Mecanim Generic, with **no Humanoid avatar**, so characters should use FBX.
- **Recommendation:** convert each GLB to `.gltf` + `.bin` + separate JPG/PNG (`gltf-transform copy in.glb out/name.gltf`) before importing. External images become normal Texture assets with a TextureImporter, so you control Max Size, compression and crunch per platform. Embedded GLB images bypass that control. Keep the file names unchanged, because glTFast resolves them by relative URI.
- If you build **URP/Lit materials yourself**, which is better for the SRP Batcher and consistent shading, convert the glTF MR texture (G=roughness, B=metallic) into URP's metallic map (R=metallic, A=smoothness) with `RawAssets/fal3d/tools/gltf_mr_to_urp.py`. Normal maps are OpenGL (+Y) in both glTF and Unity, so no flip is needed. Mark them as Normal Map. glTF base color is sRGB; MR and normal maps are linear.
- Fix scale on import: set the prefab root scale so real-world size is right (AKM 0.88 m, crate about 0.6 m). Don't trust `auto_size`.

---

## 4. Decimating without Blender

In a scratch dir: `npm i @gltf-transform/cli@4 gltfpack` (gltf-transform 4.5.1, gltfpack 1.3). Both use meshoptimizer, which preserves UVs and normals and respects seams. `simplify` also works on skinned GLBs.

```bash
GT=npx gltf-transform
$GT weld in.glb w.glb
$GT simplify w.glb s.glb --ratio 0.5 --error 0.002   # LOD0 trim; --error caps deviation (fraction of bbox)
$GT resize s.glb out.glb --width 2048 --height 2048   # or 1024 for world models / props
npx gltfpack -i in.glb -o lod2.glb -si 0.25 -noq -kn  # stronger, no quantization (safe for glTFast)
$GT copy out.glb unity/name.gltf                      # external textures for Unity's TextureImporter
$GT inspect out.glb                                   # stats
```
Python check: `uv run --with pygltflib --with numpy --with pillow python RawAssets/fal3d/tools/inspect_glb.py file.glb` prints tris, verts, maps and resolutions, skins/joints and clips.
Avoid `fast-simplification` / pyfqmr. They operate on positions only and destroy UVs.
For **skinned** characters, decimate the *static* GLB **before** rigging, then upload it with `fal_client.upload_file` and rig it. Do not try to decimate the FBX.

---

## 5. WebGL budgets (desktop Chrome/Safari, keyboard and mouse)

| Asset | Tris (LOD0) | Textures | Approx. build cost after Unity compression |
|---|---|---|---|
| FP view-model weapon | 10–25k (Tripo P2 gives 20k directly) | 2K base, MR, normal (DXT/BC, crunched base+MR) | ~2–4 MB |
| World or pickup weapon | 3–7k | 1K | ~0.5 MB |
| Character (squad/enemy) | 10–20k (Meshy gives 20k directly; consider 15k), 24 bones | 2K, shared across variants | ~3–4 MB |
| Props | 0.5–5k (barrel 1.5–3k, crate 0.5–2k, truck wreck 8–15k) | 512–1K, ideally atlased or trim sheets | 0.2–0.8 MB |

Frame targets: ≤ ~500k visible tris, ≤ 300 batches (SRP Batcher on, GPU instancing for repeated props), ≤ 8 skinned characters animating near the player, and URP shadows limited to 1–2 cascades.
**Hosting:** each published binary must be ≤15 MB, while a single Unity `.data` file can easily exceed that. Plan on **Addressables / AssetBundle groups under ~12 MB each** (loaded from relative URLs) or a lean main scene, and use Brotli or gzip with **Decompression Fallback**. A rough whole-game asset budget is 80–120 MB compressed, which fits the 256 MB per-version cap. Never ship raw generated GLBs; the Hunyuan GLB alone is 34.5 MB.

---

## 6. Recommended production pipeline

### 6.1 Prompt recipe: image to 3D
1. **Reference image** with `google/nano-banana-2.1` (2K, about $0.06, about 30 s). Use 16:9 for long guns, 1:1 for props and 3:4 for characters. This template worked on the first try:
   > Photorealistic studio product photograph of **[object, era, materials, wear]**, exact **[left side profile / 3/4 elevated view]**, camera perpendicular, no perspective distortion, entire object visible, centered, filling ~90% of frame width. Pure seamless white background, soft even diffuse studio lighting, minimal specular highlights, only a faint contact shadow, everything in sharp focus, museum catalog reference photo. No text, no watermark, no hands, no other objects.
2. For hero assets, make **extra views** with `google/nano-banana-2.1/edit` ("same rifle, right side view / top view, identical design") and feed them to multi-view endpoints: Hunyuan Pro (8 views, +$0.15), Meshy 7.1 multi-image (1–4), Rodin (≤5) or Tripo H3.1 multiview.
3. Keep the lighting flat and turn on delighting (Tripo `delight`, Rodin `texture_delight`, Hi3D `shading`) so baked highlights don't end up in albedo.
4. Never name real militant groups and never use real insignia. Use "no insignia, no text" on fictional-faction gear.

### 6.2 Weapons (FP view models + world models), 3–4 period guns
1. Reference image (above). For FP, the camera sees the **left side and top**, so make the left side the reference view.
2. `tripo3d/p2/image-to-3d` with `face_limit: 20000, pbr: true, texture_quality: "detailed", delight: true`. Optionally run a parallel Hunyuan Pro multi-view and keep the better result.
3. **Moving parts.** Every generator outputs a single welded mesh, but reloads need a separate magazine.
   - **No-Blender trick:** use Nano Banana edit to make (a) the rifle *without* a magazine and (b) the magazine alone. Generate each in 3D and fit them together in Unity under a parent with the correct pivot.
   - The charging handle and bolt can be a small primitive or skipped.
   - Alternatives are `tripo3d/tripo/segment` and `fal-ai/hunyuan-3d/v3.1/part`.
4. FP LOD0: resize the textures to 2K (or keep 4K source files and set Unity Max Size to 2048). World model: `gltfpack -si 0.3` with 1K textures.
5. Convert to `.gltf` with external textures and import with glTFast. Build URP/Lit materials (MR→URP conversion), add a tiling **detail normal** for close-up FP view, and set the scale to real length.
6. Cost is about $1.30–2.00 per weapon including the image and part assets.

### 6.3 Props
**Use CC0 Poly Haven first.** It is already PBR, real scale, with clean UVs, and it's free. It already has: `Barrel_01/02`, `barrel_03`, `wooden_crate_01/02`, `wooden_military_crate`, `old_military_crate`, `ammo_box`, `metal_jerrycan` / `metal_jerrycan_green` / `plastic_jerrycan`, `vintage_radio_transceiver`, `Television_01` / `television_02` (CRTs), `cassette_player`, `metal_office_desk`, `WoodenTable_01–03`, `WoodenChair_01`, `SchoolChair_01`, `folding_wooden_stool`, `concrete_road_barrier(_02)`, `covered_car`, `old_tyre`, `rusted_wheel_rim_01/02`, `propane_tank`, `portable_generator`, `portable_searchlight`, `old_gas_mask`, `stick_grenade`, `binder_notebook`, `clipboard`, `office_notepads`, `metal_toolbox`, `modular_chainlink_fence`, and more. Polycounts run 0.5–20k, so decimate the heavy ones.
**Use fal for the gaps**, with `tripo3d/p2` at face_limit 1.5–5k ($1.10), `fal-ai/hunyuan-3d/v3.1/rapid` ($0.375 with PBR) or `rodin/v2.5/fast` ($0.10) for clutter:
- sandbag stacks, 55-gal oil drums with fictional markings, PRC-77-style field radios and field telephones, AK magazines and ammo cans with fictional stencils, map tables
- **wrecked 1970s–80s trucks and cars**: 8–15k, one object per call; generate the wreck as a single 3/4 reference
- **Rugs, maps and documents:** don't generate meshes. Use quads or decals textured with Nano Banana images, plus ambientCG fabric and paper normals, or `fal-ai/patina/material` for tiling rug patterns.
- **CRT screens:** Poly Haven TV mesh plus an emissive RenderTexture material for the intel montage.

### 6.4 Characters: a concrete path to rigged, animated, realistic-looking humanoids without Mixamo, Blender or accounts
1. **Reference sheet:** Nano Banana 2.1 at 2K, 3:4. Full body, **A-pose (arms about 45° down)**, front view on plain white, flat lighting. Describe era-correct kit explicitly:
   - squad: 6-color desert or woodland BDU, PASGT or M1 helmet / boonie, ALICE web gear
   - enemies: a fictional militia with khaki or olive fatigues, generic chest rig and scarf, **no insignia**
   - always add "**empty gloved hands with fingers relaxed and slightly curled, no weapon, no slung rifle, no holster, no backpack, nothing crossing the torso**"
   Then make a back view (and optionally a side view) with Nano Banana edit.
2. **3D:** `meshy/v7.1/multi-image-to-3d` with `pose_mode: "a-pose"`, `target_polycount: 15000–18000`, `enable_pbr: true`, `symmetry_mode: "on"`, and `enable_rigging: true, rigging_height_meters: 1.78` for $1.40 total. This returns a rigged FBX and GLB plus walk and run.
   - Alternative for better geometry: Tripo P2 (face_limit 18000) or Hunyuan Pro multi-view, then decimate, then `fal-ai/meshy/rigging` ($0.20).
3. **Shared animation library, bought once:** run `fal-ai/meshy/rigging/multi-animation` on the first character with ≤10 IDs per call ($1.40 per 10 clips). Useful IDs:
   - stand and move: Idle 0, Walk_Forward_While_Shooting_inplace 690, Walk_Backward_with_Gun_inplace 685/686, Walk_Left_with_Gun_inplace 694, Rifle_Charge_inplace 654, Lean_Forward_Sprint_inplace 644
   - crouch and reload: Cautious_Crouch_Walk_* 615–618, Crouch_Walk_Left_with_Gun_inplace 623, Standing_Reload 170, Kneeling_Reload 165
   - aim and traversal: Rifle_Aim_Turn_Right 585, Vault_with_Rifle 425
   - hit and death: Gunshot_Reaction 177, Hit_Reaction 178, Shot_and_Fall_Backward 183 / Forward 184 / Slow_Fall_Backward 185, Shot_and_Blown_Back 182
   - talking: Stand_and_Chat 56, Talk_* 308–314
   Every other character gets **rig only** ($0.20) and retargets the library through Unity Humanoid.
4. **Unity setup:**
   - Humanoid import as in section 3. Animator with a locomotion blend tree driven by CharacterController velocity (clips are in place).
   - `com.unity.animation.rigging` 6.6.0: Multi-Aim on the spine and head toward the target, Two-Bone IK putting the left hand on the weapon's handguard socket, and the weapon parented to `RightHand` with a grip offset.
   - Ragdoll on death, or blend in the death clips.
5. **Known limits:** no finger bones, so hands stay open. Prompting relaxed, curled gloved hands helps at gameplay distance. There are no facial bones either. For Black Ops-style interrogation and briefing close-ups, use **pre-rendered video** (fal video models shown on CRTs) rather than real-time facial animation.
6. **Uniform variants:** `meshy/v7/retexture` ($0.30) on the same mesh. Swap textures onto the rigged mesh only if the UVs are unchanged, so check that first.
7. **Bespoke motions** (cutscene beats such as "signals squad to halt and kneels" or "drags wounded comrade"): `fal-ai/hunyuan-motion` ($0.08, ≤12 s FBX), imported as Humanoid. Untested; you may need to map its skeleton manually in the Avatar configurator.
8. **First-person arms** are the hardest piece. Without Blender, the options are:
   - (a) full-body awareness: the rigged soldier is the player body, the camera sits at the head, the head and neck render shadows-only, and arms use IK to weapon grips with procedural sway, recoil and reload;
   - (b) static "hands-on-weapon" meshes generated as part of the FP weapon.
   Either way, fingers won't animate. This is the main reason to consider Blender.
9. Cost is about $1.40–2.00 per character variant plus about $3 once for a 20-clip library.

---

## 7. Cost sketch for the whole mission
4 weapons × about $2 + 30 fal props × about $0.80 (the rest CC0) + 6 character variants × about $1.60 + a $3 clip library + 10 HY-Motion clips × $0.08 comes to **about $45–60**. Iteration typically doubles that.

---

## 8. Would installing Blender via Homebrew materially help?
**Yes for weapons and characters. No for props.** `brew install --cask blender` is free, needs no account and runs headless with `blender -b -P script.py`. Exactly what it unlocks:
1. **High-to-low baking:** generate weapons at 500k–1.5M faces (Hunyuan Pro), then bake normal and AO maps onto a clean 15–20k mesh. This avoids the shading facets we measured after aggressive decimation and is the biggest visual win for FP guns.
2. **Weapon parts and pivots:** cut the magazine, bolt carrier, charging handle, trigger and selector into separate objects with correct pivots, so reload and fire animations become possible. It also fixes UV islands.
3. **Finger and twist bones:** add finger chains to the Meshy skeleton (or rebuild it with Rigify), auto-weight them, and export FBX. Unity Humanoid then maps the fingers, so hands can close on the rifle grip. You can also fix the Meshy auto-weights on vests and pouches.
4. **FP animation authoring:** keyframe reload, inspect, ADS, sprint and melee for arms plus weapon. This is far easier than Unity's Animation window, and it is what gives the "CoD feel".
5. **Mesh surgery:** remove fused gear (such as the chest-strapped carbine), separate helmets and vests for variants, atlas props and merge draw calls.
6. **Batch conversion:** GLB → FBX with PBR materials, apply scale, orientation and transforms, generate LODs with the UV-preserving Decimate modifier, and retarget HY-Motion SMPL clips onto the Meshy rig.
Without Blender, the fal and Unity pipeline above still ships a playable mission. Blender is what lifts weapons and hands from "good indie" to "Black Ops-adjacent". It does not affect props.

---

## 9. Risks and open questions
- Prompt drift on era for characters (anachronistic modern gear). Mitigate with image-driven generation and human review of every character.
- All outputs are single welded meshes, so parts must be generated separately or segmented.
- Scale and orientation are wrong out of the box (Tripo auto_size 3.0 m; Hunyuan rotated).
- Hunyuan's back-side texture drifts darker. Mitigate with multi-view input.
- Meshy cloth shows faceted shading, the skeleton has no fingers, and the FBX embeds only base color.
- The pricing API's unit prices disagree with the docs for Meshy rigging (0.8 vs $0.20). Tripo Remesh and Segment credit counts are undocumented.
- Not yet tested: Rodin 2.5, Tripo Remesh bake-down, Hunyuan Motion FBX retargeting, glTFast import (package not installed, to avoid touching the shared manifest), Mesh LOD on a WebGL build, Seed3D.
- WebGL files are capped at 15 MB each, so Addressables group sizing must be planned early.

---

## 10. Files produced
- `RawAssets/fal3d/01_rifle_ref_image/rifle_ref.png`: reference image, plus request/result/timing JSON in every folder
- `RawAssets/fal3d/02_rifle_tripo_p2/rifle_tripo_p2.glb`, `preview.png`
- `RawAssets/fal3d/03_rifle_hunyuan31_pro/rifle_hunyuan31.glb`, OBJ/MTL, `preview.png`
- `RawAssets/fal3d/04_soldier_meshy71_t23d/soldier_meshy71.{glb,fbx,obj,usdz,stl}`, `texture_0*.png`
- `RawAssets/fal3d/05_soldier_meshy_rig_multianim/rigged_character.{glb,fbx}`, `basic_{walking,running}.*`, `anim_{0,690,654,170,183}_*.{glb,fbx}`
- `RawAssets/fal3d/processed/`: FP/world rifle variants and `rifle_tripo_fp_gltf/` (glTF + external textures + URP metallic map)
- `RawAssets/fal3d/renders/`: comparison renders
- `RawAssets/fal3d/tools/`:
  - `fal_run.py`: submit any fal call, log timing and download every output
  - `inspect_glb.py`
  - `gltf_mr_to_urp.py`
  - `glb_viewer.html` + `glb_viewer_server.js`: three.js multi-view and wireframe renderer; run with `node glb_viewer_server.js <dir> 8765`
