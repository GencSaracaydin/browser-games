# OPERATION LONG SHADOW: Technical Plan

**Version 1.1** · 2026-10-16 · Lead engineer / technical director · completeness/feasibility review applied (see Review log at the end)
**Inputs:** `Docs/GDD.md` (spec), `Docs/DIALOGUE.json` (157 lines), `Docs/research/{webgl,fal_3d,fal_media,cc0}.md`, `Docs/research/cc0_manifest.json`, test outputs in `RawAssets/`.
**Companion file:** `Docs/ASSET_MANIFEST.json` (every asset the GDD needs, one VO entry per dialogue line, voice casting per `voice_id`).

This document turns the GDD into an implementation plan that separate agents can build in parallel. Section 1 records the technical decisions (D1–D22), including the places where this plan refines the GDD. Sections 2–10 are the engineering spec. Section 11 splits the work into 12 packages with owners, dependencies and definitions of done. Appendix A is the marker-ID contract between the level builders and the mission scripters; Appendix B mirrors the bark subtitles. The Review log at the end lists what the 2026-10-16 review changed across all four documents.

Conventions: "must" is a hard rule. Paths are relative to `/Users/gencsaracaydin/Documents/fun/fun-unity/` unless absolute. GDD section references look like `GDD §7.3`.

---

## 0. Current project state (inspected read-only, 2026-10-16)

| Item | State |
|---|---|
| Editor | Unity **6000.6.6f1**, URP 17.6.0, Input System 1.20.0, uGUI 2.6.0 (TextMeshPro included) |
| Other packages | `com.unity.ai.assistant` 2.20.0-pre.2 (hosts the Unity MCP bridge: **keep it**), collab-proxy, IDE packages, all built-in modules |
| Build target | **WebGL** (switched by the webgl research). Brotli + **Decompression Fallback on**, data caching on, name-files-as-hashes off, template `APPLICATION:Default`, memory 32 → 2048 MB geometric, IL2CPP OptimizeSpeed, managed stripping **Minimal** for WebGL, splash screen on, linear color space, Input System only |
| Quality | Levels "Mobile" (excluded for WebGL) and "PC" (WebGL default). PC uses `Assets/Settings/PC_RPAsset.asset` + `PC_Renderer.asset` |
| PC_RPAsset | HDR on, **color grading LDR**, MSAA off, opaque texture **on**, depth texture on, shadows 2048 / 50 m / 4 cascades, soft shadows **High**, light layers on, mixed lighting on, adaptive performance on, SRP Batcher on |
| PC_Renderer | **Forward+**, SSAO active (downsample off, intensity 0.4, radius 0.3, depth-normals source), default PostProcessData (all 11 grain textures) |
| Graphics settings | No lightmap/fog/instancing stripping (everything kept) |
| Layers / tags | Unity defaults only |
| Input | Template `Assets/InputSystem_Actions.inputactions` is the project-wide actions asset |
| Scenes | `SampleScene` in build settings; research test scene `Assets/Scenes/WebGLTest.unity` |
| Research scaffolding in `Assets/` | `Scenes/WebGLTest*`, `Scripts/WebGLTest/`, `WebGLTest/BundleContent/`, `Editor/WebGL{Test,Bundle}Build.cs`, `TutorialInfo/`, `Readme.asset`. **Leave untouched until WP11 removes them.** |
| Version control | **None** (not a git repository). See open decision O7. |
| Local tools | node/npm, python 3.13, uv, git. **No Blender, no ffmpeg** (a static ffmpeg 7.1 exists inside `/Users/gencsaracaydin/Documents/fun/.venv/.../imageio_ffmpeg/binaries/`). Unity ships a **.NET 8 SDK with Roslyn** at `/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/` (used by the offline compile check, §10.1; verified in a scratch test: compiles against UnityEngine modules + package DLLs in 0.3 s and reports CS errors). |

Test outputs that are reusable (see ASSET_MANIFEST `notes`):
- `RawAssets/fal3d/02_rifle_tripo_p2/rifle_tripo_p2.glb` (AKM, 19,944 tris, 4K PBR) and `processed/rifle_tripo_p2_world_6k_1k.glb`: AK world model now; the FP AK still needs a magazine-free remake.
- `RawAssets/fal3d/05_soldier_meshy_rig_multianim/anim_{0,170,183,654,690}_*.fbx`: 5 Meshy rifle clips already bought (Idle, Standing_Reload, Shot_and_Fall_Backward, Rifle_Charge_inplace, Walk_Forward_While_Shooting_inplace). Humanoid-retargetable. The mesh in them (modern operator) is **not** usable.
- `RawAssets/falmedia/processed/*`: AK shot, explosions, radio VO, a briefing loop: placeholders for the vertical slice.
- `RawAssets/cc0/`: `sand_01` and `cracked_concrete_wall` 2k sets, `CorrugatedSteel005`, `TireTracks001`, `Barrel_01` glTF, Quaternius UAL1 + UAL2, Rocketbox `Military_Male_01` (full) + `Male_Adult_05` + 6 animations + `FixRocketboxMaxImport.cs`, Unity SAC subset (DefaultMale + 8 clips).

---

## 1. Key technical decisions

Each decision names the GDD section it refines, where it does.

| # | Decision | Why |
|---|---|---|
| D1 | **`S_Boot` is the only scene in Build Settings** (goes into the main `.data`). `S_Persistent` (GDD §6.7) becomes an **Addressables scene** that is loaded during the click gate and never unloaded. | Keeps the `.data` at ≤ 8 MB. Anything referenced by a build-settings scene lands in `.data`, and the player rig, weapons, VO and music together would break the 12 MB target (webgl.md §6). |
| D2 | **No glTFast.** Every static mesh (fal GLB and Poly Haven glTF) is converted offline to **OBJ + external textures** (`Tools/mesh/gltf_to_obj.py`) and imported by Unity's native ModelImporter. Characters and animation stay FBX. | One import path for all static meshes. The native importer generates lightmap UVs, **Mesh LOD** (not available for glTFast imports, fal_3d.md §3), mesh compression and per-folder presets. We build our own URP/Lit materials anyway (D4). Fallback if the converter shows normal/handedness problems: install `com.unity.cloud.gltfast@6.20.0` (WP03 spike decides in its first hour). |
| D3 | **No Cinemachine.** Timeline (`com.unity.timeline` 6.6.0) for authored sequences; our own camera rigs (booth C_POV with limited look, dolly, heli rail, shake). | Our camera needs are first-person and simple. Avoids an extra runtime assembly and wasm size. GDD §18.1 lists it as optional. |
| D4 | **Standard URP/Lit everywhere** (plus a few purpose shaders). Poly Haven ARM / ambientCG maps are **repacked offline** into one mask texture (R = metal, G = AO, A = smoothness) that is assigned to both `_MetallicGlossMap` and `_OcclusionMap`. No `Lit_ARM` Shader Graph (refines GDD §18.2). | One shader for every surface: fewest variants, full SRP Batcher compatibility. The double-assignment was verified on WebGL (webgl.md §4.4). |
| D5 | **First-person view model on a URP overlay camera** (layer `Viewmodel`, own FOV 55° vertical, near 0.01 m, clears depth, receives but never casts shadows). The FP body is the Calder Rocketbox soldier with head/neck bones scaled to 0 (GDD §12), rendered by that camera. | Independent view-model FOV (CoD convention); no gun clipping into walls; works with player FOV 75–105°. Fallback if the stack costs too much on WebGL: same camera with the view model scaled ×0.3 toward the eye. |
| D6 | **Per-section lightmap bakes use an EditorOnly "BAKE_Sun"** (Mixed, Baked Indirect) inside each section scene. At runtime **one Realtime sun** in `S_Persistent` (driven by LightStage) supplies direct light and shadows to everything. Section bakes are made with only that section open. | Sections bake independently, and each section's lightmaps live in its own bundle (GDD §9.4). It avoids multi-scene lighting-data coupling and avoids relying on shadowmask, which would require the baking light to exist at runtime. |
| D7 | **Far proxies (`S9_TownFar`) use URP *Baked Lit* with fully baked lighting** (sun direct + sky) in **three lightmap sets**: GOLDEN (sun 7°), SUNSET (1.5°) and DUSK (sky only), swapped by `LightmapSwapper` at `s` = 1.35 (inside the cinema, unseen) and 1.97 (hidden by the sodium-lamp ripple). A proxy block is hidden only while its section is loaded **and** the player is within 90 m (10 m LOD cross-fade), not merely because its section is loaded. | The long golden-hour shadows across the town seen from the ridge (Beat 1) exist beyond the 80 m realtime shadow distance, and so do the shadows of loaded sections seen from far away. Baked Lit ignores realtime lights, so there is no double sun. A GOLDEN-only proxy would show sunlit roofs from R4 while the real sun is at 1.5°, hence the SUNSET set. The swapper is the same component as the basement emergency swap (GDD G11). |
| D8 | **Small and medium props get GI from light probes** (Receive GI = Light Probes, Contribute GI on). Only architecture, terrain and big props are lightmapped. | Saves lightmap budget (§8.10) and removes the need for lightmap UVs on generated props. |
| D9 | **No AudioMixer asset.** `AudioService` applies category gains (Master, Music, Effects, Voice, UI, Insert) and ducking directly on pooled `AudioSource.volume`. All filters, reverb and distance muffling are **baked offline** (radio, PA, scrambler, tape, `_far` variants, zone tails). | Unity Web's mixer only does volume anyway, and AudioLowPass/HighPass/Distortion/Echo filters and AudioReverbZones **do not run in Web builds** (webgl.md §4.2, fal_media.md §6.1). This overrides the brief's "radio filter via AudioLowPass/HighPass/Distortion": the radio sound is a baked `_RADIO` file and the runtime only crossfades volume. No editor-created mixer asset means the audio package stays code-only. |
| D10 | **Amplitude envelopes are precomputed offline** for every VO line (30 Hz RMS) and shipped as data. They drive the scrambler's VU needle (G1), the jaw flap (D082, MID-B lips) and the voice-activity look-at. | `AudioSource.GetOutputData/GetSpectrumData` are not available on Web. |
| D11 | **Checkpoint reload restores state in place** (no scene reload): systems implement `ISaveable`; beat scripts are resumable from a checkpoint id. | Reloading additive scenes on the web costs seconds and re-decompresses bundles. GDD §17 snapshot contents map 1:1 to `ISaveable` providers. |
| D12 | **Game tables are code, assets are libraries.** GDD numbers (weapons §13, archetypes §14.1, difficulty §16, encounters §7, beats §7.0, LightStage §9.2) live in C# data classes (`WeaponTable`, `ArchetypeTable`, ...) that compile offline. Asset references (meshes, clips, prefabs) live in a few library ScriptableObjects/prefabs, looked up by id. | Code packages can be built and tested without the editor; tables are diffable; one agent at a time on the editor (§11.2). |
| D13 | **Markup components are data-only and live in Core** (`SceneMarker`, `TriggerVolume`, `SquadNodeSet`, `CoverNode`, `PerchNode`, `SpawnCloset`, `FlankRoute`, `IlluminationZone`, `ZoneTag`, `OutOfBoundsVolume`, `CheckpointMarker`, `GlassingPOI`, `EyeAdaptationZone`). Systems find them by **string id** (Appendix A). | The level builder can greybox and mark up before any system code exists; the mission scripter never needs scene references. |
| D14 | The Harrou 8 mm CRT clip (intro shot 5c) is **pre-rendered in the editor with `com.unity.recorder`** into `StreamingAssets/video/*.mp4` and played with `VideoPlayer.url`. Session 11 (MID-B) and the gun-camera clip (outro) stay **live render textures** (their scenes are loaded at that time), with a pre-render fallback. | `S3_Square` is not loaded during the intro (GDD §6.7), and loading it early costs memory. |
| D15 | The Clockmaker **interval signal and the three-note music-box motif are synthesised procedurally** (python, additive music-box timbre), then used as `audio_reference` for every EL Music cue. | The motif is shared by station, score and outro (GDD §10.3). Only synthesis guarantees the exact same three notes everywhere. |
| D16 | **Lightmap budget cap** of **27 × 1024²-equivalents** plus reflection probes, ≈ 33 MB download (§8.10). A Normal Quality (RGBM, DXT5) 1024² map is ≈ 1.4 MB in VRAM with mips and ≈ 1.05 MB as LZ4 download; a 2048² map counts as 4 eq (≈ 4.2 MB). Per-section caps are in GDD §9.4 (S0 1, S1 2, S2 4, S3 4, S4 2, S5 3, S6 3, S7 1, S8 0, S9 7). Reflection probes are 128 px except 4 hero probes at 256 px (≈ 4.5 MB in total) and use HDR Cubemap Encoding = Normal Quality (RGBM). Texel density is lowered per section until its cap holds; measured in the vertical slice. | The earlier figure of "11 maps at 2048²" assumed ~3 MB per 2k map; measured DXT5 data is ~4.2 MB LZ4 per 2k map, and the original GDD counts (16 × 2048² plus far proxies) would be ~75 MB. |
| D17 | **Excluded CC0 items:** Poly Haven `russian_food_cans_01` (Cyrillic would code the unnamed bloc through a real language, GDD §2.3-1), `cement_bag` (English print), `cigarette_pack` (brand art), `rollershutter_*` graffiti variants, `quiver_tree_01` (reads as southern Africa), ambientCG `PaintedMetal017/018`, `GraffitiSet001`. | Guardrails. |
| D18 | **Code-only packages work in `Staging/` and land through a compile gate** (`Tools/build/land.py`). Only the editor-slot holder refreshes the AssetDatabase. | A half-written script in `Assets/` breaks play mode and domain-reloads everyone's MCP session (webgl.md §7-17). |
| D19 | **Two Calder bodies on the player rig** (refines GDD §12 and D5): `PFB_Calder_FP` on layer `Viewmodel` with head, neck and both thighs scaled to 0 (arms, hands, upper chest), and `PFB_Calder_Shadow`, the full body with `ShadowCastingMode.ShadowsOnly` on layer `Player`, driven by a 5-state locomotion animator (idle, walk, run, crouch, aim) from `IPlayerRig` velocity and stance. | The view model never casts shadows (D5), yet the MID-A "LONG SHADOW" image needs the player's sun shadow. Legs on an overlay camera with its own FOV draw over the world and look wrong, so the FP body has none. |
| D20 | **Section loads only at named quiet points** (GDD §6.7 table) and the **click gate accepts the click once `S_Persistent` + `S0_Booth` are loaded**; `S9_TownFar` and `S1_Approach` keep loading behind the intro, which holds at shot 8 if they are late. | Bundle LZ4 decompression and Crunch transcoding run on the main thread on WebGL; a 12 MB load mid-firefight stalls frames. Waiting for 85 MB before the first click is a long black screen. |
| D21 | **Default render scale from the canvas size**: at startup `QualityApplier` picks the largest render scale in {1.0, 0.9, 0.8, 0.75} with canvas pixels × scale² ≤ 2.1 MP, unless the player has saved a value. | The 60 fps target (GDD §9.8) is defined at ≤ 2.1 MP internal resolution; a Retina canvas at the 1.5 DPR cap is ≈ 3.3 MP. |
| D22 | **Every publish removes stale files.** `publish_plan.py` keeps `Builds/Web_published_files.json` (the file list of the last approved publish) and adds a `null` entry for every path that is no longer in the build. | The artifact host keeps files a publish leaves out. Hashed release names and Addressables bundle names change on every content build, so without removals old files pile up toward the 511-file / 256 MB per-version cap. |

---

## 2. Packages

### 2.1 Add (one manifest edit by WP00, through `Unity_PackageManager_ExecuteAction`; versions are the ones bundled with 6000.6.6f1)

| Package id | Version | Why |
|---|---|---|
| `com.unity.ai.navigation` | 2.0.15 | `NavMeshSurface`/`NavMeshLink`/`NavMeshModifierVolume` per section scene; enemy and squad agents (GDD §14.3, §15) |
| `com.unity.timeline` | 6.6.0 | Intro, title card, MID-B, climax and outro sequences (GDD §8); custom VO/CRT/screen-FX playables |
| `com.unity.splines` | 2.9.1 | Critical-path spline (LightStage progress, squad follow), exfil spline N0–N6, truck and heli approach splines |
| `com.unity.animation.rigging` | 6.6.0 | Multi-aim (spine/head), two-bone IK (left hand on weapon, FP arms), look-at, watch check, pistol rise |
| `com.unity.addressables` | 2.11.2 | Section scenes and content as LZ4 bundles ≤ 12 MB from `StreamingAssets` (webgl.md §6). Pulls `com.unity.scriptablebuildpipeline` 3.0.3 |
| `com.unity.recorder` | 5.1.7 | **Editor-only**: pre-render CRT clips (D14), record review videos of beats. No runtime footprint |
| `com.unity.test-framework` | 1.8.0 | Already resolved as a dependency; add explicitly so test asmdefs are stable |

### 2.2 Keep
`com.unity.render-pipelines.universal` 17.6.0, `com.unity.inputsystem` 1.20.0, `com.unity.ugui` 2.6.0 (TextMeshPro), `com.unity.ai.assistant` (MCP bridge), IDE packages, collab-proxy, all modules.

### 2.3 Do not add (and why)
- `com.unity.cloud.gltfast`: D2 (fallback only).
- `com.unity.cinemachine`: D3.
- `com.unity.visualeffectgraph`, `com.unity.entities`, GPU Resident Drawer: need compute, not on WebGL2.
- `com.unity.probuilder`, `com.unity.terrain-tools`: our kit and terrain are generated by editor scripts (§7.15).
- `com.unity.postprocessing`: Built-in RP only.
- Optional, decided later by WP11: `com.unity.web.stripping-tool` 1.3.0 (engine submodule stripping) and `com.unity.memoryprofiler` 1.1.12 (editor tool).

### 2.4 Offline tool dependencies (outside Unity)
- `Tools/.venv` via `uv venv` + `Tools/requirements.txt` (pinned): `numpy scipy soundfile pyloudnorm pillow pygltflib fal-client httpx certifi librosa fonttools matplotlib`.
- `Tools/mesh/package.json`: `@gltf-transform/cli@4.5.1`, `gltfpack@1.3.x` (the versions used in research).
- FAL_KEY is loaded with `set -a; source /Users/gencsaracaydin/Documents/fun/.env; set +a` and is **never printed, logged or written**. Every tool reads it from the environment only.

---

## 3. Project settings recipe

All of 3.1–3.8 is applied by **one idempotent editor script**, `LongShadow/Setup/Apply Project Settings` (`Assets/_Game/Scripts/Core/Editor/ProjectSetup.cs`, WP00). It logs every value before → after to `Logs/project_setup.txt`. Nobody edits these by hand; changes go through that script (owned by WP00, later WP11).

### 3.1 Player settings (Web)

```csharp
var nt = NamedBuildTarget.WebGL;
PlayerSettings.SetScriptingBackend(nt, ScriptingImplementation.IL2CPP);
PlayerSettings.SetApiCompatibilityLevel(nt, ApiCompatibilityLevel.NET_Standard);
PlayerSettings.SetManagedStrippingLevel(nt, ManagedStrippingLevel.High);           // + Assets/_Game/link.xml (WP01)
PlayerSettings.SetIl2CppCodeGeneration(nt, Il2CppCodeGeneration.OptimizeSize);     // compare OptimizeSpeed in WP11 profiling
PlayerSettings.SetIl2CppCompilerConfiguration(nt, Il2CppCompilerConfiguration.Release); // Master for the final build
PlayerSettings.stripEngineCode = true;
PlayerSettings.stripUnusedMeshComponents = true;
PlayerSettings.colorSpace = ColorSpace.Linear;
PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, true);                 // WebGL2 only for v1 (webgl.md §4.1)
PlayerSettings.runInBackground = false;
PlayerSettings.gcIncremental = true;
PlayerSettings.SplashScreen.show = false;
PlayerSettings.companyName = "LongShadow"; PlayerSettings.productName = "Operation Long Shadow";
PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
PlayerSettings.WebGL.decompressionFallback = true;     // REQUIRED: the host cannot send Content-Encoding
PlayerSettings.WebGL.dataCaching = true;
PlayerSettings.WebGL.nameFilesAsHashes = false;        // true only for release publishes (WebBuild sets it)
PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
PlayerSettings.WebGL.wasm2023 = true;
PlayerSettings.WebGL.threadsSupport = false;           // needs COOP/COEP headers; impossible on this host
PlayerSettings.WebGL.initialMemorySize = 256;
PlayerSettings.WebGL.maximumMemorySize = 4096;         // Safari pass tests 2048
PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;  // External for QA builds
PlayerSettings.WebGL.showDiagnostics = false;          // true in the QA build variant
PlayerSettings.WebGL.template = "PROJECT:CovertOps";   // Assets/WebGLTemplates/CovertOps (WP01)
EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;
// Lightmap Encoding (WebGL) = Normal Quality; set via SerializedObject on ProjectSettings if the API is internal.
// HDR Cubemap Encoding (WebGL) = Normal Quality (baked reflection probes ship as RGBM DXT5, never RGBAHalf/BC6H; D16).
// Progressive Asset Loading: OFF (incompatible with Decompression Fallback, webgl.md §4.7).
// Active Input Handling: Input System only (already). Enter Play Mode Options: Disable Domain Reload (see §4.4 rule R6).
```

### 3.2 Graphics settings
- Lightmap modes: **Custom**, keep *Baked Non-Directional* only (plus plain/dynamic plain); strip directional, shadowmask, subtractive.
- Fog modes: **Custom**, keep **Exponential Squared** only.
- Instancing variants: Strip Unused. BatchRendererGroup variants: **Strip All**.
- URP Global Settings: strip debug variants, strip unused post-processing variants, strip unused variants on; shader variant log level "Only SRP shaders" during WP11 audits.
- Always-included shaders: trim to what URP requires.

### 3.3 Quality
- One level for WebGL: rename "PC" → **`Web_High`**. "Mobile" stays excluded for WebGL (WP11 may delete it; never add a second RP asset, it doubles variants).
- Texture quality full res; anisotropic **Per Texture**; LOD bias 1.25, maximum LOD 0; LOD cross-fade on; skin weights 4; realtime reflection probes off; billboards face camera on; VSync 0 (the browser paces frames; `Application.targetFrameRate = -1`); async upload time slice 4 ms, buffer 64 MB (crunched 2k uploads).
- **Performance mode** is applied at runtime by `QualityApplier` (WP01), not by a second level: SSAO feature off, 2 cascades, shadow distance 50 m, render scale 0.8, motion blur off (GDD §9.8). It is also the Safari default until the Safari pass says otherwise.

### 3.4 URP asset `Assets/Settings/PC_RPAsset.asset` (keep path; only properties change)

| Setting | Now | Set to |
|---|---|---|
| HDR / precision | on / 32-bit | on / 32-bit (R11G11B10) |
| Color grading mode / LUT | LDR / 32 | **HDR** / 32 |
| MSAA | off | off (SMAA High on the camera) |
| Render scale / upscaling | 1.0 / Auto | 1.0 (options 0.75–1.0) / **Bilinear** (FSR is unsupported on WebGL2) |
| Depth texture / opaque texture | on / on | on / **off** (no heat haze; saves a full-screen copy) |
| Main light shadows | 2048, 50 m, 4 cascades | 2048, **80 m** (LightStage sets 80 m in GOLDEN and 60 m in SUNSET; main-light shadows are off from AFTERGLOW, GDD §9.2a), 4 cascades, splits 6.25 % / 18.75 % / 43.75 %, border 0.1. Depth/normal bias for the 0.5°–1.5° sun on R4 is tuned in the vertical slice and stored per stage in `LightStageTable`. |
| Soft shadows | High | **Medium** |
| Additional lights | per pixel, shadows on, atlas 2048 | same; at most 1 shadowed local light visible (flares, GDD §9.5) |
| Light layers | on | **on** (booth isolation, §3.8) |
| Light cookies | on | **off** (no cookies planned; fewer variants) |
| Mixed lighting | on | on (needed by bake proxies) |
| Reflection probes | blending on, box projection on, atlas on | same |
| Light probe system | Light Probe Groups | same (no APV on WebGL2) |
| LOD cross-fade | on | on |
| SRP Batcher / dynamic batching / GPU Resident Drawer | on / off / off | same |
| Adaptive Performance | on | **off** |
| Volume update mode | every frame | every frame (interior volumes blend while moving) |

### 3.5 Renderers
- **`PC_Renderer`** (index 0, world + booth + view-model cameras): Forward+; depth priming off.
  - **SSAO**: Source **Depth** (avoids a depth-normals prepass), Normal quality Medium, **Downsample on**, Samples Medium, Blur Medium, Intensity 0.6, Radius 0.35, Direct lighting strength 0.25.
  - **Decal**: technique **Screen Space**, normal blend Low, max draw distance 60 m.
  - **FullScreenPassRendererFeature "Glitch"**: injection After Post Processing, requirement Color; inactive by default. WP09 assigns its material **at runtime** (`passMaterial`) and toggles it with `SetActive`, so nobody edits the renderer asset again.
  - `postProcessData` → **`Assets/_Game/Settings/URP/PostProcessData_Web.asset`**, a copy with only the `Medium1` and `Large01` grain textures (saves ~2.6 MB raw from the boot data).
- **`PC_Renderer_RT`** (index 1, new): Forward+, no SSAO, no decals, no full-screen passes. Used by render-texture cameras (CRT live feeds, film camera, gun camera).

### 3.6 Cameras (owned by WP02 in the player rig; values set by code)
| Camera | Type | Settings |
|---|---|---|
| `Cam_World` | Base | renderer 0, post-processing on, **SMAA High**, render shadows, depth texture on, near 0.05, far 3000 (ridge silhouette at 2.5 km), volume mask `Default`, culling excludes `Viewmodel`, `Booth`, `Trigger` |
| `Cam_Viewmodel` | Overlay on `Cam_World` | culling `Viewmodel` only, FOV 55° vertical (ADS per weapon), near 0.01, far 5, clear depth, no shadow rendering |
| `Cam_Booth` (in `S0_Booth`) | Base, enabled only during booth shots/inserts | renderer 0, post on, SMAA, volume mask `Booth`, culling `Booth` |
| `Cam_Feed_*` (`Cam_Feed_Ridge` for intro CRT2, `Cam_Feed_Deck` for intro CRT3 in `S0_Booth`, `Cam_Feed_Film` at `BOOTH.FilmCam` for MID-B, `Cam_Feed_GunCam` on the heli skid strut) | Base → RenderTexture 512×384 | renderer 1, no post, enabled only while its CRT or screen is visible |

### 3.7 Post-processing (1980s film look)
Profiles in `Assets/_Game/Settings/Volumes/` (WP10 Look). Global volumes live in `S_Persistent` on layer `Default` and are weighted by LightStage; interior volumes are local boxes whose weight is tweened by `EyeAdaptationZone` (+EV over 1.5 s in, 1.0 s out, GDD G10); the booth volume is a local box on layer `Booth`.

| Profile | Contents (values from GDD §9.6) |
|---|---|
| `VP_Base` (weight 1) | Tonemapping ACES; Bloom 0.6 / threshold 1.0 / scatter 0.65 / dirt `T_LensDirt` (ambientCG SurfaceImperfections003) 0.8; Color Adjustments contrast +12, saturation −8; Split Toning shadows #2E5C66, highlights #D9A066, balance −20; Lift Gamma Gain lift (0.98, 0.99, 1.02, −0.02), gain (1.03, 1.00, 0.96, 0); Film Grain Medium1 0.25 / response 0.8; Vignette 0.25 / 0.4; Chromatic Aberration 0.10; Motion Blur camera 0.15 Low (option); DoF Gaussian far-only from 40 m (enabled during ADS/dialogue by `DofDirector`) |
| `VP_Golden` / `VP_Sunset` / `VP_Afterglow` / `VP_BlueHour` | White balance, exposure, saturation and extras per GDD §9.6 stage table; weights from LightStage |
| `VP_Interior_Cinema` (+1.2 EV), `VP_Interior_Hall` (+0.8), `VP_Interior_Screening` (+1.0), `VP_Interior_Basement` (+1.0) | Color Adjustments post exposure only |
| `VP_Booth` | Color filter #C8D6C0, saturation −35, grain Large01 0.55, lens distortion −0.12, vignette 0.35, CA 0.2; tube flicker drives exposure ±0.15 (script) |
| FX volumes (owned by WP09 `ScreenFX`): `VP_Death` (saturation −100 over 0.6 s), `VP_LowHealth` (desaturation below 50 % HP), `VP_Suppression` (vignette pulse), `VP_Flash` (+6 EV intro shot 9, white-outs), `VP_Purkinje` lives inside `VP_BlueHour` | |

### 3.8 Lighting setup
- **Runtime lights in `S_Persistent`:** `SUN` (Directional, **Realtime**, soft shadows, rotation `(elevation, 78°, 0)` = azimuth 258°, rendering layer `World`), `SKY_KEY` (Directional, Realtime, no shadows, az 258°, el 12°, used from AFTERGLOW on). LightStage enables one at a time.
- **Per-section bake** (`SectionBaker`, WP10): opens the section alone; enables its EditorOnly `BAKE_Sun` (Mixed / Baked Indirect, elevation/colour/indirect multiplier of the section's bake stage, multiplier 0 for `S4_Roofs` "sun bounce off"); sets the environment to the stage's **sun-free HDR cubemap** (EditorOnly) rotated to az 258°; uses the section's LightingSettings asset; bakes synchronously; bakes reflection probes; captures the stage ambient SH; then **restores the scene skybox reference to `M_Sky_PanoBlend`** so no HDR cubemap is pulled into a bundle (the validator checks, §9.3).
- **LightingSettings** (`Assets/_Game/Settings/Lighting/LS_<Section>.lighting`): Progressive GPU, **Non-Directional**, max lightmap 2048, padding 4, compression Normal Quality, baked AO on (max distance 1 m, indirect 1.0, direct 0), bounces 2, indirect resolution per GDD §9.4 densities (16 texels/m hero, 6–8 houses, 4 terrain; cut if over the D16 budget), mixed lighting mode Baked Indirect.
- **Probes:** Light Probe Groups along the path every 2–4 m; after every additive load/unload `SectionStreamer` calls `LightProbes.TetrahedralizeAsync()`. Reflection probes 128–256 px, box-projected indoors, baked.
- **Light layers (Rendering Layers):** 0 `World` (sun, sky key, world lights), 1 `Booth` (booth lights; booth renderers are on `Booth` only, so the sun never lights the underground room), 2 `Cabin` (heli cabin red lamp), 3 `Viewmodel` (view model receives `World` + `Cabin`).
- **Fog:** built-in exponential squared; colour/density from LightStage.

### 3.9 Layers, tags, physics

| # | Layer | Used for | Collides with |
|---|---|---|---|
| 0 | Default | static world geometry | everything except Viewmodel, Trigger, FarProxy, Booth |
| 6 | Player | CharacterController | Default, Character, Vehicle, PlayerClip |
| 7 | Viewmodel | FP weapons, FP body | nothing |
| 8 | Hitbox | enemy bone colliders while alive (kinematic) | nothing (queries only) |
| 9 | SquadHitbox | squad bone colliders (enemy fire VFX only; player shots ignore) | nothing |
| 10 | Character | AI capsule bodies | Default, Player, Character, Vehicle |
| 11 | Ragdoll | dead bodies | Default, Vehicle, Ragdoll, Debris |
| 12 | Projectile | grenades, rockets | Default, Vehicle, Character, Ragdoll |
| 13 | Debris | fruit, small physics props | Default, Ragdoll, Debris, Projectile |
| 14 | Interactable | interaction raycasts | nothing |
| 15 | Trigger | `TriggerVolume`, zones | Player, Character (trigger only) |
| 16 | SmokeLOS | smoke volumes (block AI sight only) | nothing |
| 17 | FarProxy | `S9_TownFar` meshes (no colliders) | nothing |
| 18 | Booth | booth set and booth volumes | nothing |
| 19 | PlayerClip | invisible player-only walls | Player |
| 20 | Vehicle | trucks, technical, helicopter | Default, Player, Character, Ragdoll, Projectile |

Masks (as constants in `LongShadow.Core.LayerIds`): `PlayerShot = Default|Hitbox|Vehicle|Debris`, `EnemyShot = Default|SquadHitbox|Vehicle|Debris` (hits on the player are decided by hit chance, not by a ray; the ray only places the impact/miss), `AISight = Default|Vehicle|SmokeLOS`, `Interact = Interactable|Default`, `Ground = Default|Vehicle`.
Physics: queries hit triggers **off**, queries hit backfaces off, auto-sync transforms off, default contact offset 0.01, fixed timestep 0.02, max allowed timestep 0.1. Tags: only `Player` and `MainCamera` (components identify everything else).

### 3.10 Texture import defaults (`TextureImportRules` AssetPostprocessor, WP00, keyed by folder + suffix)

| Folder / suffix | Type | sRGB | Max size | WebGL format | Crunch q | Mips | Aniso |
|---|---|---|---|---|---|---|---|
| `Art/Textures/Env/*/T_*_BC` | Default | on | 1024 (hero list: 2048) | DXT1 Crunched (DXT5 Crunched if alpha) | 55 | on | 4 (ground 8) |
| `*_N` | Normal map | off | = BC | DXT5nm Crunched | 60 | on | = BC |
| `*_M` (R metal, G AO, A smooth) | Default | **off** | = BC | DXT5 Crunched | 60 | on | = BC |
| `*_E` emissive | Default | on | 512 | DXT1 Crunched | 60 | on | 1 |
| `Art/Textures/Weapons/**` | as above | | 2048 FP / 1024 world | | **80** | on | 4 |
| `Art/Textures/Characters/**` | as above | | 1024 bodies, 2048 squad heads | | 70 (heads 80) | on | 2 |
| `Art/Textures/Props/**` | as above | | 512 small / 1024 large (list in manifest budget) | | 55 | on | 2 |
| `Art/Textures/Decals/**` | Default | on (BC), off (N/M) | 1024 atlas | DXT5 Crunched | 60 | on | 2 |
| `Art/Textures/Images/**` (CRT, intel, documents) | Default | on | 1024 (2048 for full-screen) | DXT1 Crunched | 60 | off for UI-only | 1 |
| `UI/Sprites/**` | Sprite | on | 512 | DXT5 Crunched (≤ 128 px: RGBA32) | 70 | off | — |
| `Art/HDRI/Runtime/T_Sky_*` | Default 2D, wrap U repeat / V clamp | on | 2048×1024 | DXT1 Crunched | 70 | on | 1 |
| `Art/HDRI/EditorOnly/**` | Cube, Lat-Long, RGBAHalf | — | 2048 | **must never be referenced by shipped content** | — | — | — |
| Lightmaps | Unity-managed | | 2048 | Normal Quality (RGBM DXT5) | — | | |

All: Read/Write off, streaming mipmaps off, alpha-is-transparency only on UI/decals.

### 3.11 Model import defaults (`ModelImportRules`, WP00)
| Folder | Animation type | Materials | Normals / tangents | Mesh compression | Lightmap UVs | Mesh LOD |
|---|---|---|---|---|---|---|
| `Art/Models/Kit/**` (generated meshes are `.asset`, not imported) | — | — | — | — | generated by the kit tool | — |
| `Art/Models/Props/**`, `Vehicles/**`, `Vegetation/**` (.obj) | None | **None** (MaterialFactory assigns) | Import / Calculate Mikk | Medium | on only for `*_LM` (large) props | on, 3 levels, P1 verify on WebGL |
| `Art/Models/Weapons/**` (.obj) | None | None | Import / Mikk | **Off** (FP), Medium (world) | off | world models only |
| `Art/Models/Characters/**` (.fbx) | **Humanoid**, Create From This Model | None (RocketboxImporter builds URP Lit Specular) | Import / Mikk | Off | off | off |
| `Art/Animation/Clips/**` (.fbx) | Humanoid (own avatar per source rig), anim compression Optimal, keyframe reduction 0.5° / 0.5 % | None, no meshes used | — | — | — | — |

### 3.12 Audio import defaults (`AudioImportRules`, WP00) and Web rules
| Folder | Force mono | Load type | Preload | Quality | Sample-rate override |
|---|---|---|---|---|---|
| `Audio/SFX/{Weapons,Weapons/Far,Impacts,Foley,Footsteps,Explosions,Whizz,Synth}` | on | Decompress On Load | on | 70 | none |
| `Audio/SFX/{SetPieces,Vehicles}` (loops) | on | Compressed In Memory | on | 60 | none |
| `Audio/SFX/UI` | on | Decompress On Load | on | 60 | 22050 |
| `Audio/Ambience` | off (2D stereo) | Compressed In Memory | **off** | 50 | none |
| `Audio/VO/{RADIO,INTERCOM,HF_AM,GREYCOAT_SQUELCH,PA_HORN}` | on | Compressed In Memory | **off** | 60 | **22050** |
| `Audio/VO/{DRY,FAR,SCRAMBLER,TAPE,CLEAN,BOOTH,ROOM_SPEAKER}` | on | Compressed In Memory | **off** | 70 | 24000 |
| `Audio/Music` | off | Compressed In Memory | off | 60 | none |

Folder names under `Audio/VO/` are exactly the chain names used in `ASSET_MANIFEST.json` `variants` (Legion barks use `DRY`/`FAR` with an `_A`/`_B` voice suffix in the file name). Clockmaker tokens never enter `Assets/` (they stay in `RawAssets/audio/vo/tokens/`); an import-rule folder that is missing is a validator error.

Web rules: audio is re-encoded to AAC; no streaming; looping AAC can click, so loops carry ≥ 1024 samples of lead-in and are crossfaded with two sources (`PlayScheduled`); a clip in a bundle must be `LoadAudioData()`-loaded before that bundle is unloaded (webgl.md §3.6). VO is decoded per beat window (§7.10).

### 3.13 Other settings
- Time: fixed 0.02, maximum 0.1.
- Audio: real voices 32 (GDD budget 24 audible), virtual 64, speaker mode stereo, no spatializer.
- Editor: Enter Play Mode Options on, **Disable Domain Reload** (rule R6), asset serialization Force Text, visible meta files.
- Physics 2D: unused.

---

## 4. Project layout, naming, assemblies

### 4.1 Folders
```
Assets/_Game/
  Scripts/
    Core/        (+Editor/: ProjectSetup, ImportRules, MaterialFactory, Markup gizmos)
    Input/
    Platform/    (+Editor/: WebBuild, PostBuildValidator, AddressablesGroupRules)
    Player/      (+Editor/: PlayerPrefabBuilder)
    Combat/      (+Editor/: WeaponPrefabBuilder)
    AI/          Common/ Enemy/ Squad/ Encounters/ (+Editor/: EnemyPrefabBuilder, AIDevArena)
    Characters/  (+Editor/: RocketboxImporter, ClipImporter, BodyPrefabBuilder, RagdollBuilder, FingerPoseAuthoring)
    Audio/       Core/ VO/ Music/ Ambience/ (+Editor/: DialogueImporter, AudioLibraryBuilder)
    Mission/     Beats/ Objectives/ Checkpoints/ (+Editor/: BeatJumpWindow)
    UI/          Hud/ Menus/ Screens/
    Cinematics/  Playables/ Inserts/ Booth/ (+Editor/)
    World/       Look/ Level/ (+Editor/: SectionBaker, KitGenerator, BuildingGenerator, TerrainBuilder, FarProxyBuilder, MarkupTools)
    Integration/ (+Editor/: SmokeTestMenu, BudgetReport)
  Tests/EditMode/  Tests/PlayMode/
  Art/
    Materials/{Env,Props,Characters,Weapons,Vehicles,FX,Sky,UI}
    Textures/{Env/<set>,Props/<id>,Characters/<id>,Weapons/<id>,Vehicles/<id>,Decals,Images,FX}
    Models/{Kit,Props,Weapons,Vehicles,Vegetation,Characters,Terrain,Backdrop}
    Animation/{Clips/<Source>,Controllers,Masks,Poses}
    HDRI/{Runtime,EditorOnly}
    Shaders/  VFX/  Fonts/
  Audio/{SFX/<Group>,VO/<CHAIN>,Music,Ambience,Envelopes}
  Prefabs/{Systems,Player,Squad,Bodies,Enemies,Weapons,Props,Kit,Vehicles,FX,Booth}
  Scenes/{S_Boot.unity, S_Persistent.unity, Sections/S0_Booth..S9_TownFar, Dev/}
  Timelines/  UI/{Sprites,Themes}  Data/{Libraries,Encounters,Lighting}
  Settings/{URP,Volumes,Lighting,Input}
  Plugins/WebGL/LongShadowWeb.jslib
  link.xml
Assets/WebGLTemplates/CovertOps/        (Unity-required location)
Assets/StreamingAssets/video/           (Unity-required location; VideoPlayer.url)
Assets/AddressableAssetsData/           (Addressables default location)
Staging/<System>/                        (outside Assets: code-only packages, D18)
Tools/{build,fal,mesh,tex,audio}/        (outside Assets)
RawAssets/<area>/<asset_id>/             (downloads and generations, outside Assets)
Builds/Web/                              (release build output)
```

### 4.2 Naming
| Kind | Pattern | Example |
|---|---|---|
| Texture (suffix BC, N, M, E, H or A) | `T_<Name>_<Suffix>` | `T_SandstoneBlocks05_M` |
| Material | `M_<Name>` | `M_ConcreteWall007` |
| Static mesh / skinned | `SM_<Name>`, `SK_<Name>` | `SM_WPN_Commando_FP` |
| Prefab | `PF_<Name>`; body-only prefabs `PFB_<Name>` | `PF_Legion_Rifleman_A`, `PFB_Legion_02` |
| Animation clip / controller / override / mask | `A_<Source>_<Name>`, `AC_`, `AOC_`, `AM_` | `A_Meshy_StandingReload`, `AC_Soldier` |
| SFX / VO / music / ambience / stinger | `SFX_<Group>_<Name>_<nn>`, `VO_<LineId>_<CHAIN>`, `MUS_<Id>_<Layer>`, `AMB_<Name>`, `STG_<Name>` | `VO_D015_RADIO` |
| Shader Graph / shader | `SG_<Name>`, `Sky/PanoBlend` | `SG_CRTScreen` |
| Volume profile / lighting / timeline / render texture | `VP_`, `LS_`, `TL_`, `RT_` | `TL_Intro` |
| Library ScriptableObjects | `LIB_<Domain>` | `LIB_WeaponVisuals` |
| Scenes | `S_Boot`, `S_Persistent`, `S0_Booth` … `S9_TownFar` | |
| Marker ids | `<Beat>.<Kind>.<Name>` (Appendix A) | `B3.NodeSet.M2` |

### 4.3 Assemblies and namespaces
Root namespace **`LongShadow`**; one assembly per system folder, named `LongShadow.<System>` (+ `LongShadow.<System>.Editor`, editor-only). Namespaces follow folders (`LongShadow.AI.Squad`).

| Assembly | References (besides UnityEngine) |
|---|---|
| `LongShadow.Core` | Unity.Addressables, Unity.ResourceManager (for `AssetReference` in a few contracts) |
| `LongShadow.Input` | Core, Unity.InputSystem |
| `LongShadow.Platform` | Core, Unity.Addressables, Unity.ResourceManager, Unity.RenderPipelines.Universal.Runtime |
| `LongShadow.Player`, `LongShadow.Combat`, `LongShadow.AI`, `LongShadow.Mission` | Core (Combat also URP runtime for decals) |
| `LongShadow.Characters` | Core, Unity.Animation.Rigging |
| `LongShadow.Audio` | Core |
| `LongShadow.UI` | Core, Unity.TextMeshPro, UnityEngine.UI |
| `LongShadow.Cinematics` | Core, Unity.Timeline, Unity.TextMeshPro, URP runtime |
| `LongShadow.World` | Core, Unity.Splines, Unity.AI.Navigation, URP runtime |
| `LongShadow.Integration` | everything (composition root, dev console, smoke test) |
| `LongShadow.Tests.EditMode/PlayMode` | Core + the systems under test, test framework |

**Rule:** a system assembly references **only Core** (+ Unity packages). Systems talk through Core interfaces registered in `GameServices` and through `GameEvents`. Only `Integration` references systems directly.

### 4.4 Coding rules
- R1. One public type per file, file name = type name. `[SerializeField] private` fields, `_camelCase`. No `public` mutable fields on MonoBehaviours.
- R2. No per-frame allocations in gameplay code: `*NonAlloc` physics, pooled lists, `StringBuilder`, `TMP_Text.SetText(fmt, value)`, no LINQ in `Update`, no closures in hot paths. GC runs at frame end on Web.
- R3. `Time.deltaTime` for gameplay; `Time.unscaledDeltaTime` for UI, cinematics, inserts and the booth (`Animator.updateMode = UnscaledTime` on booth characters).
- R4. Every `UnityWebRequest`/Addressables call is async and resilient to failure (log + fallback).
- R5. No `System.Reflection` in runtime code (High stripping; also rejected by `Unity_RunCommand`). Anything instantiated only from bundles is listed in `link.xml`.
- R6. Domain reload is disabled: every static (services, events, caches) resets in `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
- R7. `#if UNITY_WEBGL && !UNITY_EDITOR` around every `DllImport`; editor fallbacks exist.
- R8. Debug code goes through `LSLog` (categories; `[Conditional("LS_DEV")]` for chatty logs). Errors are never swallowed silently.
- R9. Every GDD number used in code cites its section in a comment (`// GDD §13.1`).
- R10. Never call `Unity_AssetGeneration_*` MCP tools (no Unity AI credits). Do not use `Unity_AudioClip_Edit` (untested, may consume credits).

---

## 5. Runtime architecture

### 5.1 Scenes and streaming
| Scene | Where | Loaded | Unloaded | Contents |
|---|---|---|---|---|
| `S_Boot` | Build Settings index 0 (`.data`) | startup | never | `BootRoot` (WebBridge, SettingsService, QualityApplier, InputReader, TimeScaleService, InputLockService, PauseService, SectionStreamer, PoolService host), `ClickGateView`, `BootProgress` |
| `S_Persistent` | Addressable group `Core_Persistent` | click gate | never | Player rig, squad ×3, `SUN`/`SKY_KEY`, global volumes + `M_Sky_PanoBlend` skybox (**active scene**), MissionDirector, LightStage, Audio/VO/Music/Ambience, CombatService, TargetRegistry, Encounter/Squad services, Checkpoint, Objectives, Cinematic/Insert/ScreenFX services, HUD + menus canvas, library objects (`LIB_*`) |
| `S0_Booth` … `S9_TownFar` | one Addressable group per section | per GDD §6.7 table | per GDD §6.7 table | section geometry, lightmaps, probes, NavMesh data, markup, beat-local props |

`SectionStreamer` (WP01) loads with `Addressables.LoadSceneAsync(key, LoadSceneMode.Additive)`, then `LightProbes.TetrahedralizeAsync()`, raises `GameEvents.SectionLoaded/Unloaded` (the far-proxy blocks of loaded sections hide themselves). The MissionDirector requests loads from `BeatTable` at the quiet points named in GDD §6.7 (D20) and unloads at beat starts; a beat script that reaches a trigger whose section is not loaded holds it (`yield return streamer.WaitLoaded(id)`).

### 5.2 Boot flow
1. `S_Boot` starts: services register; the HTML loader (template) has already shown wasm/data progress as number groups.
2. `Addressables.InitializeAsync` → load `S_Persistent` → `SetActiveScene(S_Persistent)` → load `S0_Booth` → `VOManager.PrepareWindow(beat 0..1)`, `Music.Preload(MUS_01_SESSION)`. Progress feeds the click gate's number groups.
3. Click gate ready (GDD §8.1) as soon as step 2 is done; `S9_TownFar` and `S1_Approach` start loading now and continue behind the gate and the intro (D20). On pointer-down: `WebBridge.RequestPointerLock(unadjusted:true)`, audio resume, optional fullscreen, then `MissionDirector.StartMission()` → `ICinematicService.Play("Intro")`. `TL_Intro` holds at the start of shot 8 (tube-hum loop, CRT2 static) until both sections report loaded.
4. Dev/QA shortcuts: URL `?cp=CP5`, `?dev=1` (dev console), `?autotest=1` (smoke test in the web build); editor `DevStart` component on `S_Boot`.

### 5.3 Services, events, execution order
- `GameServices.Register<T>(T)` / `Get<T>()` / `TryGet<T>(out T)`; interfaces only. Registration in `Awake`, lookup in `Start` or lazily. Missing services log once and degrade (systems must run alone in test scenes).
- `GameEvents` = static C# events with `Raise*` methods (Core §6.3). High-frequency data (aim spread, health) is read by polling the interface, not via events.
- Execution order constants (`ExecOrder`): Platform −1000, Input −900, LightStage −500, Player −200, Weapons −100, AI 0, Squad 10, Mission 100, Audio 200, UI 300, Cinematics 400.

### 5.4 Time, pause, input locks
- `ITimeScaleService.Push(scale, priority, reason)` returns a handle; effective `Time.timeScale` = the request with the highest priority. Priorities: Pause 100, Insert 90 (scale 0), Cinematic 50, Death 40 (0.3 over 0.6 s), SlowMo 30 (breach 0.35, blast 0.3), default 1.
- Pause (WP01): pointer-lock loss or Esc → `PauseChanged(true)` → TimeScale pause, `AudioListener.pause = true` (UI and Insert sources set `ignoreListenerPause`), InputLock All, pause menu. Resume needs a click (Chrome blocks re-lock for ~1 s).
- `IInputLock.Lock(flags, reason)`; `IInputReader` returns neutral values for locked channels.

### 5.5 Checkpoints (D11)
`ICheckpointService.Save(cpId)` captures every registered `ISaveable` into a `CheckpointSnapshot` (in memory; the highest CP id also goes to localStorage). Deferral rules from GDD §17 (enemy within 10 m from `ITargetRegistry`, live grenade from `GameEvents`, player < 50 HP), max 10 s, always commits before the next beat's first spawn. Reload: fade/insert → `EncounterSpawner` despawns all and restores killed ids → player/squad teleport to the CP markers → loadout, health (full), LightStage `s`, objectives, intel, music cue, destructible states → `MissionDirector.ResumeFrom(cpId)` restarts the beat script at that checkpoint's entry point. Lines already played are skipped unless flagged `objectiveCritical`.

---

## 6. Core contracts (WP00 builds these first; additive changes only afterwards)

Everything below lives in `Assets/_Game/Scripts/Core/` (namespace `LongShadow.Core`). It is small on purpose: types, interfaces, events, markup data and three generic components. Changes after WP00 are **additive only** and are requested in `Docs/CORE_CHANGES.md`; WP00 (or WP11 later) applies them.

### 6.1 Types
```csharp
public enum Team { None, Player, Squad, Legion, Greycoat }
public enum HitZone { Torso, Head, Limb }
public enum DamageType { Bullet, Explosion, Melee, Fall, Fire, Script }
public enum SurfaceType { Default, Sand, Gravel, Stone, Plaster, Concrete, Tile, Wood, Metal, SaltCrust, Fabric, Glass, Flesh, Fruit }
public enum Stance { Stand, Crouch, Sprint, Slide, Airborne, Mantle, Ladder, Mounted }
public enum PerchTag { Ground, Balcony, Rooftop, Window, Vehicle }
public enum SectionId { Booth = 0, Approach = 1, Market = 2, Square = 3, Roofs = 4, StationExt = 5, StationInt = 6, Pitch = 7, Heli = 8, TownFar = 9 }
public enum VOPriority { Bark = 0, Callout = 1, Objective = 2, Scripted = 3 }
public enum NoiseKind { Gunshot, SuppressedShot, Explosion, Footstep, Voice, Impact }
public enum ZoneType { OpenDesert, Street, Square, Interior, Basement, Roof, Cabin }
public enum StageId { Golden, Sunset, Afterglow, BlueHour }
public enum MarkerKind { Diamond, DiamondHollow, HighPriority, TargetX, Follow, Intel, Ring }
public enum HudOverlay { None, Binoculars, DoorGun }
[Flags] public enum InputLockFlags { None = 0, Move = 1, Look = 2, Fire = 4, Weapons = 8, Interact = 16, Jump = 32, All = ~0 }
public enum Difficulty { Recruit, Regular, Hardened, Veteran }

public struct DamageInfo { public float Amount; public DamageType Type; public HitZone Zone; public Vector3 Point, Direction;
  public GameObject Instigator; public Team InstigatorTeam; public string WeaponId; public bool Suppressive; }
public struct KillInfo { public string VictimId; public Team VictimTeam; public DamageInfo Damage; public bool Headshot; public bool ByPlayer; }
public struct ShotEvent { public Vector3 Origin, Direction; public Team Team; public string WeaponId; public float NoiseRadius; public bool Hit; }
public struct NearMissEvent { public Vector3 Point, Direction; public float Distance; public Team ShooterTeam; }
public struct NoiseEvent { public Vector3 Position; public float Radius; public Team Source; public NoiseKind Kind; }
public struct AIShotRequest { public ITargetable Shooter; public Vector3 Muzzle; public IDamageable Target; public float HitChance;
  public float Damage; public string WeaponId; public Team Team; public int TracerEvery; public bool SuppressOnly; }
public struct SubtitleData { public int Handle; public string LineId, Speaker, Callsign, Text; public Color Color; public bool Radio, Italic, Monospace, IsBark; public float Duration; }
public struct PromptData { public string Id, Text; public bool Hold; public float Progress01; }
public struct ObjectiveDef { public string Id, Text; public int Index, Total; public string FlavourGroup; public bool Optional; }
public struct MarkerDef { public string Id; public Transform Target; public Vector3 Offset; public MarkerKind Kind; public string Label; public bool ShowDistance, ThroughWalls; }
public struct DifficultyProfile { /* every row of GDD §16 */ }
[Serializable] public class GameSettings { /* every option of GDD §11 Options row; defaults per GDD */ }
```

### 6.2 Interfaces (implementer → package)
| Interface | Key members | Implemented by |
|---|---|---|
| `IDamageable` | `Team Team`, `bool IsAlive`, `Transform AimPoint`, `void ApplyDamage(in DamageInfo d)` | `Health` (Core), `PlayerHealth` (WP02) |
| `IHitbox` | `IDamageable Owner`, `HitZone Zone`, `float Multiplier`, `SurfaceType Surface` | `Hitbox` (Core) |
| `ITargetable : IDamageable` | `string ActorId`, `Transform Root`, `Vector3 Velocity`, `PerchTag Perch`, `bool SquadTarget {get;set;}`, `float LastEngagedByPlayer`, `void NotifyEngagedByPlayer()` | enemies (WP04), squad (WP04), player (WP02) |
| `ISuppressible` | `void Suppress(float seconds, Team by)` | enemy brains, player |
| `ITargetRegistry` | `Register/Unregister(ITargetable)`, `GetHostilesOf(Team, List<ITargetable>)`, `CountWithin(Team, Vector3, float)` | WP04 |
| `IPlayerRig` | `Transform Root, Eye`, `Camera WorldCamera`, `Vector3 Velocity`, `Stance Stance`, `bool IsADS`, `float AdsBlend`, `float SpeedMultiplier {set;}`, `float TimeSinceSprintEnd`, `Ray ViewRay`, `bool IsGlassing`, `void AddAimDelta(float pitch, float yaw)`, `void SetViewPunch(Quaternion)`, `void RequestFov(object owner, float vFov, float blendTime)`, `void ReleaseFov(object owner)`, `void Shake(float trauma)`, `void Teleport(Vector3 pos, float yaw)`, `void SetForcedWalk(float? speed)`, `void SetLookLimits(Quaternion center, float yaw, float up, float down)`, `void ClearLookLimits()`, `void SetDamageModifiers(float scale, float floorHp)`, `void Mount(Transform seat)`, `void Dismount()` | WP02 |
| `IInputReader` | `Vector2 Move, Look` (DPR-normalised, spike-filtered), `bool Fire, FireDown, Aim, ReloadDown, Sprint, JumpDown, CrouchDown, Frag, FragUp, MeleeDown, Interact, InteractDown, ObjectivesHeld, PauseDown`, `int WeaponSlotDown`, `float Scroll`, `float SkipHeld01` | WP00 (`LongShadow.Input`) |
| `IInputLock` | `int Lock(InputLockFlags, string)`, `void Unlock(int)`, `bool IsLocked(InputLockFlags)` | WP01 |
| `ITimeScaleService` | `int Push(float scale, int priority, string reason)`, `void Pop(int)` | WP01 |
| `IDisplayInfo` | `float DevicePixelRatio`, `bool IsPointerLocked`, `bool InIframe` | WP01 |
| `ISettingsService` | `GameSettings Current`, `DifficultyProfile Difficulty`, `event Action Changed`, `Apply(GameSettings)`, `Save()`, `string GetValue(key)`, `SetValue(key, v)` | WP01 |
| `ISectionStreamer` | `void Request(SectionId, bool load)`, `bool IsLoaded(SectionId)`, `float Progress`, `IEnumerator WaitLoaded(SectionId)` | WP01 |
| `IPoolService` | `GameObject Spawn(GameObject prefab, Vector3, Quaternion, float autoDespawn = 0)`, `void Despawn(GameObject)`, `void Prewarm(GameObject, int)` | WP01 |
| `ICombatService` | `void FireAI(in AIShotRequest r)`, `void ThrowGrenadeAI(Vector3 from, Vector3 to, Team team)`, `void FireRocket(Vector3 from, Vector3 to, Team team, Action<Vector3> onImpact)`, `void Explode(Vector3 pos, float damage, float lethalR, float maxR, GameObject instigator, Team team)`, `void SpawnSmoke(Vector3 pos, Color c, float seconds)`, `void SpawnPickup(string weaponId, Vector3 pos)` | WP03 |
| `IWeaponHudState` | `float CrosshairSpread01`, `bool CrosshairVisible`, `float Heat01`, `float CookTime01` | WP03 |
| `IWeaponGrips` | `Transform RightGrip, LeftGrip, SupportHint`, `float LeftHandWeight` | WP03 (current view model) |
| `ICharacterBody` | `UnityEngine.AI.NavMeshAgent Agent`, `void MoveTo(Vector3, float speed)`, `void Stop()`, `bool HasArrived`, `void SetStance(Stance)`, `void SetAim(Vector3? worldTarget)`, `void SetLookAt(Transform t, float weight)`, `void SetCoverPose(int pose)`, `void PlayAction(string actionId, Action onDone = null)`, `bool IsActionPlaying`, `Transform Muzzle, RightHand, Head`, `void SetWeaponVisual(string weaponId)`, `void Die(in DamageInfo d)`, `void SetAnimatorLod(int lod)`, `event Action<string> AnimEvent` | WP05 (`HumanoidBody`); WP04 ships `DebugCapsuleBody` for tests |
| `IVOService` | `int Play(string lineId, VOOptions o = default)`, `bool IsPlaying(string lineId)`, `void Stop(int handle)`, `void PlayBark(string lineId, Transform speaker, Team team)`, `void PlayClicks(int count, Transform speaker)`, `float Amplitude(string speakerId)`, `void PrepareWindow(int fromBeat, int toBeat)`, `bool HasPlayed(string lineId)` | WP06 |
| `IAudioService` | `void Play(string cueId, Vector3 pos, float vol = 1)`, `void Play2D(string cueId, float vol = 1)`, `int PlayLoop(string cueId, Transform follow)`, `void StopLoop(int, float fade)`, `void PlayWeaponShot(string weaponId, Vector3 pos, bool isPlayer)`, `ZoneType CurrentZone`, `void Tinnitus(float seconds)`, `void SetCategoryGain(string category, float linear)`, `void Duck(string category, float db, float seconds)` | WP06 |
| `IMusicService` | `void PlayCue(string cueId, int layer = 0, float fade = 1)`, `void SetLayer(int layer, float fade)`, `void Stinger(string id)`, `void Stop(float fade)`, `void HardCut()` | WP06 |
| `IObjectiveService` | `void Set(ObjectiveDef)`, `void Complete(string id)`, `void AddMarker(MarkerDef)`, `void RemoveMarker(string id)`, `IReadOnlyList<ObjectiveDef> Active`, `int IntelCount, OptionalCount` | WP07 |
| `ICheckpointService` | `void Save(string cpId)`, `void Reload(bool deathInsert)`, `void Register(ISaveable)`, `string Last` | WP07 |
| `ISaveable` | `string SaveKey`, `void Capture(SaveWriter w)`, `void Restore(SaveReader r)` | many |
| `IMissionDirector` | `int Beat`, `void StartMission()`, `void ResumeFrom(string cpId)`, `void Raise(string eventId)`, `bool Flag(string)`, `void SetFlag(string, bool)`, `MissionStats Stats`, `float StoryClockMinutes` | WP07 |
| `ISquadService` | `void SetNodeSet(string setId)`, `void Follow()`, `void TeleportToNodeSet(string setId)`, `bool AllArrived(string setId)`, `void PlayScripted(string member, string actionId, string markerId, Action done)`, `void ScriptedKill(string member, ITargetable target)`, `void SetChemlights(bool)`, `void SetLookTarget(Transform t, float weight)` (null clears; overrides speaker/player/threat look-at), `Transform Member(string id)` | WP04 |
| `IEncounterService` | `void Activate(string encounterId)`, `void TriggerWave(string encounterId, string wave)`, `int Alive(string encounterId, string wave = null)`, `int Killed(string encounterId)`, `bool Cleared(string encounterId)`, `ITargetable Actor(string enemyId)` | WP04 |
| `ILightStage` | `float S`, `StageId Stage`, `void SetTarget(float s, float smoothing = 2f)`, `void SetImmediate(float s)`, `void Override(object owner, bool on)` (while any owner holds it, the MissionDirector's progress driver is suspended and only the owner calls `SetTarget`), `float AISightRange`, `float HearingScale`, `bool AfterSunset`, `Color HudColor`, `float StoryClockMinutes` (GDD §9.2a curve) | WP10 |
| `ICriticalPath` | `float Progress01(Vector3 pos, int beat)` | WP10 |
| `ICinematicService` | `void Play(string id, Action onDone)`, `bool IsPlaying`, `void Skip()` | WP09 |
| `IInsertService` | `void Play(string insertId, string lineId = null)`, `void PlayDeath(Action onDone)`, `bool IsPlaying` | WP09 |
| `IScreenFX` | `SetLowHealth(float)`, `PulseSuppression(float)`, `Death(float t01)`, `Flash(float ev, float seconds)`, `Glitch(float intensity, float seconds)`, `Glare(float amount01)` (flood glare overlay, Beat 6), `FadeToBlack(float s)`, `FadeIn(float s)` | WP09 |
| `IHud` | `ShowPrompt(PromptData)`, `HidePrompt(string)`, `Tutorial(string key, string text)`, `IntelCard(string title, Sprite photo, string counter)`, `FailCard(string text)`, `Banner(string text, float seconds)` (OOB countdown `RETURN TO THE MISSION AREA n…`), `SetOverlay(HudOverlay)`, `SetRing(string id, Transform t, float v01, string label)`, `ClearRing(string)`, `Letterbox(bool, float)`, `SetVisible(bool)` | WP08 |
| `IInteractable` | `string PromptText`, `bool Hold`, `float HoldSeconds`, `bool CanInteract(IPlayerRig)`, `void Interact(IPlayerRig)`, `Transform Anchor` | WP03, WP07, WP10 props |
| `IMarkerRegistry` | `Transform Get(string id)`, `bool TryGet(string id, out SceneMarker m)`, `IEnumerable<T> All<T>()` | Core (`MarkerRegistry`) |

### 6.3 Events (`GameEvents`)
`PlayerDamaged(DamageInfo)`, `PlayerDied(DamageInfo)`, `PlayerRespawned()`, `ActorDamaged(IDamageable, DamageInfo)`, `ActorKilled(KillInfo)`, `ShotFired(ShotEvent)`, `NearMiss(NearMissEvent)`, `Noise(NoiseEvent)`, `TracerPath(Vector3 from, Vector3 to, Team)`, `GrenadeLanded(Vector3 pos, Team)`, `GrenadeExploded(Vector3 pos, float radius)`, `HitConfirmed(bool kill, bool headshot)`, `WeaponChanged(string weaponId)`, `AmmoChanged(int mag, int reserve, int frags)`, `TriggerEntered(string id, Collider c)`, `TriggerExited(string id, Collider c)`, `MissionEvent(string id)`, `BeatStarted(int)`, `BeatCompleted(int)`, `ObjectivesChanged()`, `IntelCollected(string id)`, `CheckpointSaved(string)`, `CheckpointLoaded(string)`, `LightStageChanged(float s)` (≥ 0.01 steps), `StageChanged(StageId)`, `PracticalsStrike()`, `VOLineStarted(string lineId, string speakerId)`, `VOLineFinished(string lineId)`, `SubtitleShow(SubtitleData)`, `SubtitleHide(int handle)`, `PauseChanged(bool)`, `PointerLockChanged(bool)`, `InsertStarted(string)`, `InsertEnded(string)`, `CinematicStarted(string)`, `CinematicEnded(string)`, `SectionLoaded(SectionId)`, `SectionUnloaded(SectionId)`, `SettingsChanged()`.

### 6.4 Generic components and markup (data only, with gizmos)
- `Health` (implements `IDamageable`, `ISaveable`): max HP, per-zone multipliers (head/limb), torso armour factor, `OverrideMax(float)` (sentries at 30 HP while unaware), `Invulnerable` (squad), events `Damaged`, `Killed`; raises `GameEvents.ActorDamaged/ActorKilled`.
- `Hitbox` (implements `IHitbox`; finds its `Health` in parents). The same bone colliders are hitboxes while alive (kinematic, layer `Hitbox`) and ragdoll bodies after death (layer `Ragdoll`).
- `SurfaceTag` (overrides the `SurfaceType` of a collider; otherwise the physic-material name maps to a type).
- Markup: `SceneMarker {string id}`, `TriggerVolume {string id; bool playerOnly; bool once}`, `SquadNodeSet {string id; Transform hollis, sparrow, tinker; pose hints}`, `CoverNode {stand/crouch, leanL, leanR, facing arc, string group}`, `PerchNode {PerchTag tag}`, `SpawnCloset {string id; Transform door}`, `FlankRoute {string id; Vector3[] points}`, `IlluminationZone {cone or sphere; string ownerId}` (flood cones, flare light, headlights), `ZoneTag {ZoneType}` (weapon tails, VO flavour, footstep default), `OutOfBoundsVolume`, `CheckpointMarker {string cpId}`, `GlassingPOI {string id; float radiusDeg = 4}`, `EyeAdaptationZone {VolumeProfile ref, ev, in 1.5 s, out 1.0 s}`.
- Utilities: `ShuffleBag<T>` (DI_01–06 no-repeat, bark variants), `SpringFloat/SpringVector3/SpringQuaternion`, `CountdownTimer`, `MathX` (bearing, angle wrap, FOV h↔v), `LSLog`, `LayerIds`, `ExecOrder`, `SaveWriter/SaveReader/CheckpointSnapshot` (JsonUtility-based).

---

## 7. Systems

Each subsection lists the scripts (under `Assets/_Game/Scripts/<System>/`), what they do, their public surface and dependencies. "Builder" editor scripts create prefabs reproducibly, so code packages never hand-author prefabs.

### 7.1 Platform / Bootstrap / WebGL (WP01) — `LongShadow.Platform`
| Script | Responsibility |
|---|---|
| `Bootstrap` | Boot sequence of §5.2; progress aggregation; handles the URL flags `cp`, `dev`, `autotest` |
| `WebBridge` + `Plugins/WebGL/LongShadowWeb.jslib` | `LS_GetDevicePixelRatio`, `LS_RequestPointerLock(unadjusted)`, `LS_ExitPointerLock`, `LS_IsPointerLocked`, `LS_RequestFullscreen`, `LS_ResumeAudio`, `LS_StorageGet/Set` (localStorage, try/catch), `LS_IsInIframe`, `LS_GetQuery`, `LS_SetSmokeResult`; lock changes come back via `SendMessage("WebBridge", "OnPointerLockChanged", "1")` (or `"0"`) |
| `DisplayInfo` (`IDisplayInfo`) | DPR (capped at 1.5 by the template), lock state, iframe flag |
| `PointerLockController` | `WebGLInput.stickyCursorLock = false`; detects lock loss → `PauseService`; resume overlay with the 1 s re-lock cooldown |
| `PauseService` | §5.4 |
| `TimeScaleService` (`ITimeScaleService`), `InputLockService` (`IInputLock`) | §5.4 |
| `SettingsService` (`ISettingsService`) | `GameSettings` JSON in localStorage (PlayerPrefs in the editor), defaults per GDD §11; mouse formula `deg = cssPx × sens × 0.03`; difficulty applies at the next checkpoint |
| `QualityApplier` | render scale (`UniversalRenderPipelineAsset.renderScale`; default from the canvas size per D21), performance mode (§3.3), motion blur, grain amount, brightness (−0.5 … +0.5 EV on a dedicated `VP_Brightness` global volume in `S_Persistent`) |
| `SectionStreamer` (`ISectionStreamer`) | §5.1; keeps handles; serialises loads; memory log per load |
| `PoolService` (`IPoolService`) | `UnityEngine.Pool.ObjectPool` per prefab; prewarm per section |
| `Assets/WebGLTemplates/CovertOps/` | `index.html` + `TemplateData/style.css` only: full-window canvas; `config.devicePixelRatio = Math.min(devicePixelRatio, 1.5)`; HTML loading screen in the click-gate style (green CRT cursor, number groups); `config.errorHandler` that returns `true` for `WrongDocumentError`, `NotAllowedError`, `SecurityError` from pointer lock/fullscreen and **never calls `alert()`**; `HTMLCanvasElement.prototype.requestPointerLock` wrapped to try `{unadjustedMovement:true}` then plain, rejections caught; crouch on C (Ctrl never bound); all scripts in `<body>` so the artifact host's page wrapping cannot reorder them (§12 R2) |
| Editor: `WebBuild` | menus `LongShadow/Build/Web Release` and `.../Web QA (diagnostics)`; §9 |
| Editor: `PostBuildValidator`, `AddressablesGroupRules` | §9.2–9.3; folder → group mapping (§8.10), LZ4, `.bundle` names, binary catalog, "Use UnityWebRequest for local bundles" on |
| `link.xml` | Addressables providers, our ScriptableObject library types, Timeline playables |

### 7.2 Input (WP00) — `LongShadow.Input`
- `Assets/_Game/Settings/Input/LongShadow.inputactions` set as **project-wide actions** (replaces the template asset). Maps:
  - `Player`: Move (WASD), Look (Mouse delta), Fire (LMB), Aim (RMB), Reload (R), Sprint (LShift), Jump (Space), Crouch (C), Frag (G, press/release), Melee (V), Interact (E, hold), Weapon1 (1), Weapon2 (2), WeaponScroll (scroll Y), Objectives (Tab), Pause (Esc, P as backup), Skip (Space, Hold 1.0 s).
  - `UI`: Navigate, Submit, Cancel, Point, Click, ScrollWheel.
- `InputIds` (string constants), `InputReader` (`IInputReader`): resolves actions by name once, applies `IInputLock`, divides mouse delta by `IDisplayInfo.DevicePixelRatio`, filters one-frame spikes (> 8× the running median), exposes frame-edge booleans.

### 7.3 Player (WP02) — `LongShadow.Player`
| Script | Responsibility |
|---|---|
| `PlayerRig` (`IPlayerRig`, `ITargetable`) | composition root; registers services; hierarchy `Root → Yaw → Pitch → EyeOffset → Bob → Shake → Cam_World (+ Cam_Viewmodel)` |
| `PlayerMotor` | CharacterController, capsule 1.8 m (crouch 1.1), eye 1.65/1.05; walk 4.4, ADS walk 2.6, crouch 2.4, sprint 6.9 m/s (GDD §12); slide (≥ 5.5 m/s + C, 0.75 s, 8 → 4 m/s, 0.6 m dip, hip fire allowed); jump apex 1.0 m; **auto-vault** 0.6–1.4 m in 0.45 s when moving into a ledge, **climb** 1.4–2.1 m in 0.8 s on Space (capsule casts + top probe), **air mantle** (airborne, ledge top 0–1.4 m above the feet and ≤ 0.8 m ahead → 0.5 s climb); fall damage 30 HP/m above 4.5 m; ladders (`Ladder` volumes, vertical speed 2.2 m/s); forced walk (MID-A 1.2 m/s); un-crouch clearance check; ground snapping; step 0.35, slope 45° |
| `PlayerLook` | yaw/pitch (±85°) from `IInputReader.Look × sens × 0.03`; ADS multiplier × FOV ratio; invert Y; `AddAimDelta` (recoil that moves aim), `SetViewPunch` (visual only); look limits for door gun and cinematics |
| `PlayerCameraRig` | FOV stack (settings horizontal FOV → vertical; sprint +6°, slide +4°, ADS from weapon, binoculars 12°; owner-based requests blended), head bob 1.2 cm vertical / 0.8 cm lateral at walk scaling with speed, landing dip, trauma shake |
| `PlayerHealth` (`IDamageable`, `ISaveable`) | 100 HP; regen delay/rate and last-stand immunity from `ISettingsService.Difficulty` (GDD §16); damage modifiers for Beat 11 (×0.25, floor 1); raises `PlayerDamaged/PlayerDied`; heartbeat/muffle below 25 % through `IAudioService`; low-health amount to `IScreenFX` |
| `PlayerInteractor` | sphere cast 1.8 m + LOS on `Interact` mask; hold timer; prompts through `IHud` |
| `Binoculars` | Beat 1 only (enabled by Mission): RMB raises, FOV 12°, overlay through `IHud.SetOverlay(Binoculars)`, `IsGlassing` + `ViewRay` for `GlassingPOI` checks |
| `FootstepEmitter` | stride-timed steps by speed; surface from `SurfaceTag`/physic material/terrain layer; `IAudioService` footsteps; `Noise` radii sprint 12 / walk 4 / crouch 1.5 m (GDD §14.2). Surface → footstep set: Sand/Default → SAND, Gravel → GRAVEL, Stone/Concrete → STONE, Plaster → ROOF, Tile → TILE, Wood → WOOD, Metal → METALSTAIR, SaltCrust → SALTCRUST (falls back to SAND while that P1 set is missing), Fabric/Glass/Flesh/Fruit → nearest of the above |
| `OutOfBoundsWatcher` | enters `OutOfBoundsVolume` → 6 s countdown (`IHud.Banner`), `D093`, then `IHud.FailCard("YOU ABANDONED YOUR TEAM")` + checkpoint reload; leaving the volume cancels the countdown |
| `PlayerShadowProxy` | drives `PFB_Calder_Shadow` (D19): follows the rig root and yaw, 5-state locomotion from velocity/stance, aim pose while ADS, disabled in the booth, the cabin and cinematics that hide the player |
| `PlayerMount` | seat parenting for the helicopter door gun; disables motor; hands control to `DoorGun` |
| Editor: `PlayerPrefabBuilder` | builds `PF_Player` (rig, cameras, components; FP body slot and shadow-proxy slot filled by WP11 from `PFB_Calder_FP` and `PFB_Calder_Shadow`) |

Depends on Core only. Weapons read `IPlayerRig`; the player never references weapons.

### 7.4 Health and damage (Core components + WP02 + WP03)
- Generic `Health`/`Hitbox` are in Core (§6.4). Player health is `PlayerHealth` (WP02).
- Damage resolution (WP03 `DamageResolver`): weapon falloff (close → far over the falloff range), zone multipliers (`Hitbox.Multiplier` or the weapon's override: Hush Puppy head ×3.0), armour (Greycoat torso ×0.8), explosions with a cover raycast to head and chest, knife 150.
- Damage direction for the HUD comes from `DamageInfo.Direction`/`Instigator` in `PlayerDamaged`. Grenade indicator from `GrenadeLanded`.
- Death and checkpoint flow: `PlayerDied` → MissionDirector → `ITimeScaleService` death slow-down 0.6 s + `IScreenFX.Death` → `IInsertService.PlayDeath` (booth 2.0 s, random `DI_01`–`DI_06` via `ShuffleBag`) → black `SESSION 12 — RESUMING` → `ICheckpointService.Reload(true)`.

### 7.5 Weapons and combat (WP03) — `LongShadow.Combat`
| Script | Responsibility |
|---|---|
| `WeaponTable` (code data) | every number in GDD §13.1–13.4: damage/falloff, multipliers, fire mode/RPM, mag/reserve, tactical/empty reload + keyed phase times (tilt, mag out, mag in, charge), ADS time and FOV, hip/ADS spread, fixed recoil sequences (first N shots) + random ranges + first-shot multiplier + recovery delay/rate, camera share 0.3, model kick 2.2 cm, move multiplier, sprint-out time, noise radius, tracer interval and colour; enemy weapons (damage per hit to the player, bursts, RPM) |
| `LIB_WeaponVisuals` (ScriptableObject) | per weapon id: view-model prefab, world prefab, magazine prefab, audio cue ids, impact profile |
| `WeaponInventory` (`ISaveable`) | primary/secondary slots, pickups (AK/RPK swap), switching on 1/2/wheel with raise/lower, ammo, frags (3 start, 4 max), resupply; raises `WeaponChanged/AmmoChanged` |
| `PlayerWeapon` | fire state (semi/auto, RPM, sprint-out lock), hitscan `RaycastNonAlloc` on `PlayerShot` (500 m), spread model, recoil pattern → `IPlayerRig.AddAimDelta` + view punch, ADS state (blend over ADS time; `RequestFov`; `SpeedMultiplier`), reload state machine (tactical vs empty, cancel on sprint/switch), `ShotFired`, `Noise`, `HitConfirmed`, `TracerPath` |
| `ViewmodelController` (`IWeaponGrips`) | procedural motion with springs: mouse-lag sway, bob synced to head bob, sprint pose, ADS alignment of the `SightAnchor` to screen centre, kick (−Z 2.2 cm + pitch, spring return), reload motion from keyed transforms (tilt, magazine object detaches and drops, new magazine in, charge), crouch/slide tilt, landing dip, raise/lower; exposes grips (left grip follows the magazine during reload) |
| `MuzzleFlashPool`, `TracerPool`, `ImpactService` | pooled flash particles + 50 ms point lights (≤ 4 live, range 4 m), tracer quads (400 m/s visual; red for Player/Squad, green for Legion/Greycoat; MG 1 in 3, rifles 1 in 5), impacts per `SurfaceType` (screen-space `DecalProjector` pool of 64 with fade, particles, sounds) |
| `CombatService` (`ICombatService`) | AI shots: roll `HitChance`; hit → damage (squad hits are VFX only; `SuppressOnly` → `ISuppressible.Suppress`); **miss vs player** aims 0.5–3 m off the player's head (GDD §14.3) and raises `NearMiss` when it passes within 3 m (crack/whizz audio); grenades, rockets (RPG projectile, 4 m splash with falloff), explosions, smoke (spawns a `SmokeLOS` volume for 20 s), pickups |
| `Grenade`, `GrenadeThrower` | M67: 4.0 s fuse from pin pull, cook on hold G with fuse ticks (`IWeaponHudState.CookTime01`), 18 m throw, 220 damage, lethal ≤ 4 m, linear falloff to 8 m, cover raycast, ragdoll/debris impulse, shake, tinnitus < 6 m, noise 80 m, `GrenadeLanded/Exploded` |
| `Knife` | V: 150 damage, 1.6 m reach + 1.0 m lunge, 0.6 s |
| `DoorGun` | Beat 11 mount: yaw ±70° around the left-door normal, pitch +15/−60 (GDD §13.3), 550 RPM, 60 damage, 0.6° spread, red tracer every 3rd, heat +1/shot, overheat at 55, recovery 2.0 s after overheat / 1.0 s idle; overlay through `IHud`; uses `IPlayerRig.SetLookLimits` and `SetDamageModifiers(0.25, 1)` |
| `AmmoCrate` (`IInteractable`), `WeaponPickup` (`IInteractable`) | resupply crates at GDD §13.2 positions (spawned at markers by Mission), dropped AK/RPK pickups |
| `Destructible` | generic shootable target: HP, hit radius filter (flood lamp head 0.4 m), swap meshes, spark burst, die-off audio, events, `ISaveable` (used for floods F1–F4, transmitter racks R1–R4 at 60 HP, trucks at 20 hits, rooftop targets) |
| Editor: `WeaponPrefabBuilder` | builds `PF_VM_<Weapon>` (view model: mesh, magazine child with pivot, `SightAnchor`, `Muzzle`, grips, layer `Viewmodel`) and `PF_W_<Weapon>` (world: mesh, muzzle, grip sockets) |

Hit chance and AI firing logic are not here: AI computes `HitChance` (WP04) and calls `ICombatService`.

### 7.6 Enemy AI and encounters (WP04) — `LongShadow.AI` (`.Common`, `.Enemy`, `.Encounters`)
| Script | Responsibility |
|---|---|
| `ArchetypeTable` (code data) | GDD §14.1: HP, weapon, damage, bursts and pauses, accuracy at 20 m + range curve, preferred range, cover re-evaluation (Legion 6–9 s, Greycoat 4–6 s), grenade rules, tells |
| `EncounterTable` (code data) | every encounter E2–E11 from GDD §7: enemy id, archetype, position, facing, placed/closet/perch/vehicle, wave, trigger condition, delay, flank route id, notes flags; max alive per beat; hard cap 12 |
| `EncounterSpawner` (`IEncounterService`, `ISaveable`) | spawns from pools (prewarmed when the section loads), closet rules (out of sight via raycast to the closet door, ≥ 15 m from the player), queueing above max alive, morale break at 60 % of a wave dead (`BK_LEG_09`, fall back one node), straggler despawn two beats behind, killed-id persistence |
| `EnemyBrain` | states Idle/Patrol → Suspicious (Beat 2 sentries only, detection meter) → Combat {TakeCover, Peek/Fire, Suppress, Flank, Fallback, ThrowGrenade, FleeGrenade} → Dead |
| `Perception` | sight FOV 120° (160° within 8 m), range = `ILightStage.AISightRange` (×2 up to 70 m inside an active `IlluminationZone`), detection 0.4–1.2 s by distance (crouch ×1.5 slower, sprint ×0.6), hearing from `Noise` × `HearingScale`; **round-robin LOS budget: ≤ 24 raycasts per frame** on `AISight` |
| `AttackTokenManager` | tokens per difficulty (2/3/4/5), rotation every 2–4 s, the enemy under the player's crosshair gets one first; tokenless enemies shoot squad members or reposition |
| `AIShooter` | burst cadence; hit chance = base × range curve × player motion (still 1.0, walk 0.8, sprint 0.6, slide 0.5) × cover 0.6 × time-on-target ramp 0.5 → 1.0 over 2 s (reset on LOS break) × flood 1.25 × difficulty (GDD §14.3) → `ICombatService.FireAI`; suppression from squad fire halves rate and accuracy for 2 s |
| `CoverSelector`, `CoverNodeRegistry` | nearest unclaimed `CoverNode` that breaks LOS but allows a peek; claims; perch-only archetypes use `PerchNode` |
| Specials | `SentryIdle` (smoke + `BK_LEG_14/15` every 15 s; 30 HP unaware), `TechnicalGunner` (DShK, shield immunity ±60° of 234°, traverse 40°/s, 3-round bursts, scripted fire at the arch that ignores perception; on death the truck ignites after 1.5 s), `MarksmanBehaviour` (scope glint 1.5 s before each shot, accuracy by player motion, own 160 m sight range independent of LightStage, ceases fire on `MissionEvent("MIDA.Start")`, despawns at `s` ≥ 2.0), `RPGBehaviour` (shout + 1.2 s raise telegraph, rocket, 8 s reload; scripted targets: bleacher until t 60, then Magpie; after the first wave-off it becomes `SquadTarget`), `RooftopTarget` (always aware of the helicopter, fires only in its window; T3 variant hidden until t 28 and always fires at t 30), `VehicleDismount` (rides a World `SplineVehicle`, unloads at the tailgate), `GreycoatSquelch` (2 clicks before moving, 3 on spotting), NVG variant flag |
| `EnemyBarker` | bark ids per event with per-id and global cooldowns through `IVOService.PlayBark` |
| Editor: `EnemyPrefabBuilder`, `AIDevArena` | variants of WP05 body prefabs + brain + archetype + world weapon; a generated test arena scene with cover nodes |

### 7.7 Squad AI (WP04) — `LongShadow.AI.Squad`
| Script | Responsibility |
|---|---|
| `SquadService` (`ISquadService`, `ISaveable`) | owns Hollis/Sparrow/Tinker; node-set execution (1.5 m radius, pose pick); chemlights; scripted actions |
| `SquadMember` | identity (callsign, callout prefix `CO_HOL/SPA/TIN`, subtitle colour), invulnerable `Health`, `ITargetable` (team Squad) |
| `SquadFollow` | follow mode along the critical path (`ICriticalPath`): Hollis 6–10 m ahead, others 3–6 m from the player, waits at corners/doors, never > 12 m ahead; yields within 1 m in 0.3 s; never stops in doorway areas; catch-up teleport (> 25 m behind and off-screen for 2 s) |
| `SquadCombat` | engages within 40 m; **kill share**: lethal only on `SquadTarget` actors or Legion not engaged by the player for 8 s, otherwise `SuppressOnly` shots; 1 frag per beat at clusters ≥ 2 away from the player (30 s cooldown) |
| `SquadCallouts` | relative direction to the player's view (front ±45°, left, right, rear), perch tags (rooftop/balcony/window), reload callouts (20 s cooldown), 4 s global cooldown, `VOPriority.Callout` |
| `SquadLookAt` | heads track the current speaker (`VOLineStarted` speaker id + envelope), the player within 3 m, threats in combat |
| Scripted actions (action ids played on `ICharacterBody` + props) | `BoltCut` (B2, 3 s), `ThrowSmokeWhite` (B3, 20 s), `PlantSatchel` (B6, 3 s), `FistHold` (B5), `PopSmokeGreen` (B10), `Photograph` (B1, B8), `DrillLoop` (B9), `CrackChemlight` (B6), `KickDoorStack`, `HaulBag` (B11) |

### 7.8 Characters and animation (WP05) — `LongShadow.Characters`
- **Bodies:** Rocketbox avatars (MIT) per GDD §18.2 and ASSET_MANIFEST (`CHR_*`): Legion `Military_Male_02/05/06`, Greycoat `Military_Male_03/04`, Calder `Military_Male_01` (FP body, booth, reel variant with shaved head texture), Tinker `Male_Adult_05`, Hollis and Sparrow from Rocketbox adults (exact picks in WP05 after review), interrogator and Harrou from business males, Magpie a helmeted military male. **Downloading avatars beyond the two already local needs user approval (O2).**
- **Import (editor):** `RocketboxImporter` = adapted `FixRocketboxMaxImport.cs` with `usingMixamoAnimations = false` (re-parents thighs/clavicles) and `animationType = Human`; URP/Lit **Specular** materials (smoothness 0.2–0.35 cloth, 0.45 skin); textures from the retexture pass (§8.6).
- **Clip libraries (Humanoid):** Quaternius UAL1/UAL2 (`_RM` and in-place), Unity SAC strafes (mirror the Right clips), Rocketbox sitting/gestures, Meshy rifle clips (§8.6). `ClipImporter` sets loop flags, root-motion bakes (rotation + Y into pose), mirror flags.
- **Finger poses** authored by `FingerPoseAuthoring` as Humanoid muscle curves (Rocketbox has finger bones): `A_Pose_RifleGrip`, `A_Pose_PistolGrip`, `A_Pose_Fist`, `A_Pose_Relaxed`, `A_Pose_FlatHands`.
- **`AC_Soldier`** (shared by enemies and squad; role overrides `AOC_Legion`, `AOC_Greycoat`, `AOC_Squad`):
  - L0 Base: 2D freeform directional blend (strafe set + jog/sprint) by local velocity X/Z; crouch blend tree (UAL crouch + Meshy cautious crouch walks); perch idle; full-body action sub-state (deaths, vault, climb, bolt cut, drill, plant, photograph, chemlight, kick).
  - L1 UpperBody (mask `AM_UpperBody`): rifle aim idle pose, reload (Meshy Standing/Kneeling_Reload), throw (UAL2 OverhandThrow), signals; weight from `HumanoidBody`.
  - L2 Additive hits (UAL Hit_Chest/Head, Meshy Gunshot/Hit_Reaction).
  - L3 Hands (mask `AM_Hands`): finger pose per weapon.
  - Parameters (contract with `HumanoidBody`): `VelX`, `VelZ`, `Speed`, `Crouch`, `Aim`, `Fire` (trigger), `Reload` (trigger), `Throw` (trigger), `Hit` (trigger) + `HitDir`, `Death` (int), `Action` (int) + `ActionTrigger`, `Perch` (bool), `Cover` (int).
- **`HumanoidBody`** (`ICharacterBody`): NavMeshAgent with in-place clips and speed matching (`updateRotation` off, custom turn rate), Animation Rigging rig: Multi-Aim on Spine1 (0.3), Spine2 (0.4), Head (0.8) toward an aim target; Two-Bone IK left hand to the weapon's `LeftGrip` (with hint); look-at rig; weapon on a right-hand socket with per-weapon grip offset; **ragdoll** from the same bone colliders (built by `RagdollBuilder`, death clip blends into ragdoll after 0.2 s, impulse from `DamageInfo`); LOD: `CullUpdateTransforms`, rig weights off beyond 40 m, corpse cap 8 (fade after 30 s).
- **Shadow proxy** `PFB_Calder_Shadow` (D19): the full Calder body, `ShadowsOnly`, layer `Player`, its own small animator (`AC_ShadowProxy`: idle, walk, run, crouch, aim pose from SAC/UAL clips), driven by `PlayerShadowProxy` (WP02).
- **FP body** `PFB_Calder_FP` + `FPArmsIK`: Calder on layer `Viewmodel`, head, neck and both thighs scaled to 0 (D19), both arms two-bone IK to `IWeaponGrips`, finger grip pose, Seiko-style watch on the left wrist showing `IMissionDirector.StoryClockMinutes`, `PlayWatchCheck(1.2 s)` (MID-A), `PlayPistolRise(3 s)` (climax), booth fists `Clench/Unclench` (outro).
- Animation events: `Foot_L`, `Foot_R`, `ThrowRelease`, `MagOut`, `MagIn` → `ICharacterBody.AnimEvent`.

### 7.9 Audio (WP06) — `LongShadow.Audio`
| Script | Responsibility |
|---|---|
| `LIB_Audio` (ScriptableObject) | cue id → `AudioCue` (clips, volume and pitch ranges (±3 %), spatial blend, min/max distance, rolloff curve, priority, category, loop, near/far pair) |
| `AudioService` (`IAudioService`) | 32-source pool, 24 audible voice cap with priority stealing, categories and ducking by volume (D9), current `ZoneType` from `ZoneTag` triggers |
| `WeaponShotPlayer` | layers per shot: transient (random of 4) + mech + body + **zone tail** (Street/Square/Interior) + **`_far`** variant crossfaded by distance (near < 40 m, far > 120 m); auto fire re-triggers the one-shot at RPM and plays the tail on release |
| `MusicDirector` (`IMusicService`) | cues and layers (LO/HI, low/full) as synced sources with `PlayScheduled`, bar-cut loops with crossfade, stingers, hard cut, duck under VO |
| `AmbienceDirector` | beds by LightStage (cicadas → crickets at `s` 1.97, generator hum near the station, blue-hour fire crackle and distant shouting, salt-pan wind) and zone |
| `CivilianEmitter` | G5 sounds (kettle, muted TV, shutters, radio) that turn down/latch when shooting starts within 40 m |
| `PAHorn` | Harrou lines from all 3 horn emitters at once (GDD §7.1), baked slapback, logarithmic rolloff min 25 m / max 500 m, subtitle always shown |
| `PositionalRadio` | G9: plays a line in 3D from Tinker's PRC-77, then switches it to 2D radio at a given time (`D039` at "Seven") |
| `NumbersBroadcast` | basement loop (interval signal + digits) from the room speaker, cut to static on the 4th rack (`D066`) |

### 7.10 Radio and dialogue (WP06) — `LongShadow.Audio.VO`
- **`DialogueImporter`** (editor) reads `Docs/DIALOGUE.json` + `Audio/VO/**` + `Audio/Envelopes/*.json` and builds `LIB_Dialogue`: per line `id, speaker, callsign, voiceId, subtitle (the JSON `subtitle` field if present, else `text`), chain → clip map (VO_<id>_<CHAIN>, plus `_A`/`_B` voice variants for Legion barks), duration, envelope (30 Hz), radioFilter, beat window, objectiveCritical` (the list in GDD §17: D014, D029, D034, D048, D053, D059, D065, D067). It fails on any line without a clip in every chain its manifest entry lists.
- **`VOManager`** (`IVOService`): priority queue (Scripted > Objective > Callout > Bark); one "net" channel for scripted/objective lines (queue, interrupt rules, delays); barks may overlap. **Dry/radio crossfade** by speaker distance: two synced sources (dry 3D at the speaker, radio 2D), dry ≤ 8 m, radio ≥ 15 m, linear blend between (GDD §10.1). Lines with `radio_filter: true` are radio only. Ducks music −6 dB and ambience −4 dB. Raises `VOLineStarted/Finished` and `SubtitleShow/Hide` (speaker colours GDD §11; enemy barks subtitled only within 25 m and only when "Translate enemy chatter" is on). Tracks played lines for checkpoints.
- **Beat windows:** VO clips have *Preload Audio Data* off. `PrepareWindow(from, to)` calls `LoadAudioData()` for lines of the current and next beat and the always-needed sets (callouts, barks, DI lines), and `UnloadAudioData()` for passed beats. The VO bundles themselves stay loaded (FMOD rule, webgl.md §3.6).
- `Amplitude(speakerId)` samples the playing line's envelope at its playback time (VU needle, jaw flap, look-at).

### 7.11 Mission director (WP07) — `LongShadow.Mission`
| Script | Responsibility |
|---|---|
| `BeatTable` (code data) | GDD §7.0 per beat: name, `s_start/s_end`, checkpoints, sections to load/unload on enter (GDD §6.7), music cue, objective list, enter/exit trigger ids |
| `MissionDirector` (`IMissionDirector`, `ISaveable`) | beat state machine Intro → B1 … B11 → Outro → Debrief; runs the beat scripts; LightStage progress = `max(previous, 0.5 × objective completion + 0.5 × ICriticalPath.Progress01)`, mapped into the beat's `s` range and sent to `ILightStage.SetTarget` (2 s smoothing) unless a script holds `ILightStage.Override`; stats; flags; story clock (read from `ILightStage.StoryClockMinutes`) |
| `Gates` | the progression gates of GDD §7.13 and the backtrack blockers of GDD §6.7 as named `SceneMarker`s (doors, PlayerClips) opened and closed by the beat scripts; gate states are `ISaveable` |
| MID-A owner | `Beat05LastLight` owns MID-A end to end (GDD §8.5): takes `ILightStage.Override`, keys `s` (1.80 → 1.80 at 0:04 → 1.97 at 0:14 → 2.00 at 0:20), raises `MIDA.Start` (marksman ceases fire), forced walk, `SetLookTarget`, watch check, `PracticalsStrike`, positional radio, I2, the line sequence, then releases everything and opens `B5.T.Stair` |
| `BeatScript` base + `Beat01Shoulder` … `Beat11Magpie`, `IntroFlow`, `OutroFlow` | coroutine scripts that implement GDD §7.1–7.11 exactly, using helpers: `WaitTrigger(id)`, `WaitLine(id)`, `WaitCleared(enc, wave)`, `WaitAlive(enc, wave, ≤n)`, `WaitObjective(id)`, `WaitSeconds`, `Line(id)`, `Nodes(setId)`, `Wave(enc, wave)`, `Objective(...)`, `Marker(...)`, `Checkpoint(cp)`, `Insert(id, line)`, `Cinematic(id)`, `Event(id)`; entry points per checkpoint (CP3b inside Beat 3) |
| `ObjectiveService` (`IObjectiveService`, `ISaveable`) | objectives and markers exactly as the master list in GDD §7.12 (ids, texts, marker kinds, completion rules), counters (`OBJ 4/11 — 47219 — TEXT`, the group is FNV-1a 32-bit of the objective id mod 100000), markers (palette from LightStage), intel counts; optional intel icons show only within 6 m |
| `CheckpointService` (`ICheckpointService`) | §5.5 |
| `IntelPickup` (`IInteractable`) | hold E 1.2 s; ledgers, reels, codebook (auto, Sparrow), optional O1/O2; `IntelCollected`; intel card |
| `GlassingTracker` | Beat 1: POI within 4° of the binocular view centre for 1.0 s ticks it |
| `MissionStats` | kills, headshots, shots fired/hit (accuracy), grenades, deaths, mission time, intel → debrief |
| `MissionSmokeTest` (dev) | §10.3 |
| Interactable spawns | crates, intel and props that the beat scripts spawn at markers |

### 7.12 Cinematics and the booth (WP09) — `LongShadow.Cinematics`
- **Timeline for authored sequences** (`TL_Intro` 1:43, `TL_TitleCard` 8 s, `TL_MidB_Reel` 45 s, `TL_Climax` 12 s, `TL_Outro` 1:50): many tracks, scrubbable for review, reliable on WebGL. **Scripted coroutines** for short gameplay-coupled sequences: inserts I1–I4, death insert, MID-A (driven by the Beat 5 script, player keeps control), slow-mo breaches.
- Custom playables: `VOLineClip` (plays a line through `IVOService`; scrub-safe), `CRTChannelClip` (static / still / live RT / video / clock), `LookControlClip` (limited look on/off, forced head turn), `ScreenFXClip`, `LightFlickerClip`, `TypewriterClip` (drives UI title card and on-screen type), `CharacterActionClip`, Signal track for events (`Intro.ControlToPlayer`).
- `S0_Booth` (root at world (−600, 0, −600), local frame GDD §8.0): room shell (acoustic tile, olive plaster), chair, CRT cart (6 CRTs + `RT_CRT1..6` 512×384), mirror (baked probe), wall clock, fluorescent tube (baked + flicker), scrambler box with toggle and VU needle (`Amplitude`), reel-to-reel, desk phone, interrogator table with pen (four-tap emitter, `PROC_BOOTH_SMALLS`), lighter, smoke emitter, film camera; `Cam_Booth` C_POV (vertical FOV 50°, limited look ±40° yaw / ±20° pitch, spring back after 2 s), C_CLOCK, `Cam_Feed_Deck`, `Cam_Feed_Film`. A `BoothState` component switches the set between "Session 12" (normal) and "Session 11" (Calder's shaved-head variant, the interrogator's lighter hand posed in frame) for MID-B shots 4–6. Insert lengths are unscaled seconds at a 60 fps reference (6 f = 0.1 s, minimum 2 rendered frames).
- `InsertService` (`IInsertService`): push TimeScale 0 (freezes), pause gameplay audio except the Insert category, lock input, switch to `Cam_Booth` preset for N frames, glitch in/out 0.15 s, line, return. Death insert per GDD §8.4.
- `ScreenFXService` (`IScreenFX`): Glitch full-screen material on the renderer feature (runtime `passMaterial`), FX volumes (§3.7), fades.
- Shaders: `SG_CRTScreen` (curvature, scanlines, flicker, phosphor tint, static, degauss wobble), `SG_Film16mm` (mono, scratches, dust, gate weave, 18 fps step, burn-out), `SG_GunCam` (green phosphor), `SG_FilmBurn`, `SG_Glitch` (full-screen: scanline tear, RGB split, one white frame).
- Skip: hold Space 1 s after the first viewing (flag in settings storage).
- `CinematicService` (`ICinematicService`) owns `PlayableDirector`s and the camera hand-off to and from the player rig.

### 7.13 UI / HUD and menus (WP08) — `LongShadow.UI`
- **Built at runtime by code** (`HudBuilder`) from a `UITheme` ScriptableObject (fonts, sprites, colours) so the package is code-only; WP11 creates the theme asset. Canvas 1920×1080, Scale With Screen Size, match 0.5.
- HUD elements (GDD §11): `CompassStrip`, `ObjectiveMarkers` (bearing projection, edge clamp + arrow, distance format 1 m < 100 m / 5 m beyond, 30 % while ADS, hidden < 4 m, amber #E3A33B / pale green #A6E3B4 / red #E04B3B, through-walls for red, FOLLOW chevron), `ObjectiveText` (teletype reveal; Tab list), `AmmoPanel`, `Crosshair` (from `IWeaponHudState`), `HitMarkers`, `DamageIndicator` (160 px ring arcs, 1.5 s), `GrenadeIndicator`, `HealthVignette` (edge dust/blood overlay; desaturation through `IScreenFX`), `SubtitleView` (≤ 1,100 px, 2 lines, `[CALLSIGN] NAME: text`, colours, radio icon, italic booth, monospace Clockmaker, sizes S/M/L), `PromptView` (hold radial), `TutorialPrompts` (once each), `IntelCardView` (polaroid 2.5 s), `SquadNameplates` (25 m), `WorldRings` (drill %, LZ metres), `OverlayBinoculars`, `OverlayDoorGun`, `FailCardView`, `LetterboxBars`, `FadeScreen`.
- Menus: `ClickGateView` (lives in `S_Boot`; only `LiberationSans`/the typewriter font, no heavy art), `PauseMenu` (RESUME with "Click to resume", RESTART CHECKPOINT, OPTIONS, QUIT TO SESSION), `OptionsMenu` (every option of GDD §11, bound to `ISettingsService`), `TitleCardView` (2.35:1 bars, number groups that resolve into text, 18:40 flicker on line 3), `DebriefScreen` (GDD §8.9: typed dossier, live stats, strike-through retype, redaction bars, stamp, REPLAY / SELECT BEAT / END SESSION).
- Fonts: typewriter (Special Elite or Courier Prime), condensed sans for numerals (Barlow Condensed), monospace (Share Tech Mono or IBM Plex Mono), all OFL/Apache from Google Fonts: **needs user approval (O1)**; fallback is TMP's bundled LiberationSans SDF. The invented **Sardani glyph font** is generated procedurally (§8.8).

### 7.14 World: look, lighting and LightStage (WP10, Look track) — `LongShadow.World.Look`
| Script / asset | Responsibility |
|---|---|
| `LightStageController` (`ILightStage`) + `LightStageTable` (code) | `s` ∈ [0, 4], smoothing, `Override`; keyframes exactly as GDD §9.2a (linear between rows), curves per GDD §9.1–9.2: sun elevation/intensity/colour temperature/shadow strength/shadow distance (`UniversalRenderPipelineAsset.shadowDistance`), SUN ↔ SKY_KEY switch, sun disc, sky material (`_BlendAB`, `_Tint`, `_Exposure`, `_Rotation`), ambient SH lerp between 4 captured stage probes (`AmbientSHSet`), fog colour/density, stage volume weights, `PracticalsStrike` at 1.97, AI sight/hearing, HUD colour; debug slider (dev console + `LightStage Scrubber` editor window) |
| `Sky/PanoBlend` (HLSL skybox) | two LDR equirect textures (sun painted out), blend, tint, exposure, rotation, lower hemisphere fades to fog colour |
| `SunDisc` | HDR emissive billboard, 0.53°, 2.9 km out along the sun direction, unaffected by fog; hidden behind the 2.5 km ridge silhouette during MID-A |
| `SG_Backdrop` | backdrop shader for the ridge silhouette and far hills: no built-in fog, aerial-perspective lerp toward the fog/horizon colour capped at 0.8 (exp² fog would flatten anything at 2.5 km to fog colour) |
| `FloodGlare` | Beat 6: angle between view ray and each live flood → `IScreenFX.Glare` |
| `EyeAdaptationZone` behaviour | tweens interior volume weights (G10) |
| `LightmapSwapper` + `LightmapSet` asset | replaces a scene's lightmap entries in `LightmapSettings.lightmaps` by matching textures (G11 basement emergency set; `S9_TownFar` GOLDEN → SUNSET at `s` 1.35 → DUSK at 1.97, D7) |
| `FarProxyGroup` | per block: hidden only while its section is loaded and the player is within 90 m (10 m cross-fade), D7 |
| `PracticalLight`, `PracticalBudget` | at most 6 realtime unshadowed practicals per area near the player, the rest emissive only (G4); destructible-aware |
| `LampRipple` + `SG_SodiumLamp` | sodium strike rippling out from the generator (372, 0, 305) at 15 m/s; warm-up pink-red → orange over 8 s; 38 lamp heads |
| `AviationLights` | mast lights top-down 0.3 s apart, then 1 Hz |
| `ParachuteFlare` | B10 flares FL1–FL3: shadowed 120° spot + unshadowed point fill, 1.3 m/s descent, ±4° 3 s pendulum, 30 s burn; registers an `IlluminationZone` |
| `WindController` | global shader wind vector (west → east, 3–5 m/s, gusts) for `SG_FoliageWind`, cloth, awnings, laundry, particles |
| Shaders | `SG_FoliageWind` (Lit + vertex wind), `SG_LightShaft` (additive cards, depth fade, scrolling dust), `SG_DustMotes`, `SG_SunDisc`, `SG_SodiumLamp`; far proxies use URP **Baked Lit**; decals use URP `Shader Graphs/Decal` |
| Editor: `SectionBaker`, `AmbientSHCapture`, `MaterialFactory` usage, `LookCapture` | §3.8; `LookCapture` renders beauty shots at `CAM.Look.*` markers for review |
| Volumes | §3.7 |

### 7.15 World: level construction (WP10, Level track) — `LongShadow.World.Level`
- **Kit generator** (`KitGenerator`, editor): procedural meshes saved as `.asset` with world-scale UV0 (1 unit = 1 m, tiling = 1 / `tile_m`) and lightmap UV1 (`Unwrapping.GenerateSecondaryUVSet`): wall segments 1/2/3 m × 3 m, corners, colonial round arches (never pointed), windows/doors, shutters, parapets (Art Deco stepped for the Relay Hall), arcades, timber balconies, stairs, flat roofs, corrugated roofs, awning panels (intact + torn), curbs, paving.
- **`BuildingGenerator`**: `BuildingSpec` per GDD §6.6 (BLD-01 … BLD-16) → kit pieces with seeded variation → **combined per building and material** (draw calls) with LOD1 (≈ 50 %) and a far-proxy shell for `S9_TownFar`.
- **Terrain:** `S1_Approach` uses a Unity Terrain (x 0–200, z 0–180) with heights generated by `TerrainBuilder` to GDD §6.5 (ridge +18…+24, wadi path, scree chute, boulder, grove at 0) and 4 layers (sand_01, rocky_trail_02, dry_ground_rocks, aerial_sand) using packed mask maps; elsewhere paving meshes at Y = 0; salt pan plane + haze sheets; far hills and the **western ridge silhouette** at 2.5 km from ambientCG `Terrain003`.
- **Structures:** procedural lattice mast (90 m, guy wires as line renderers, platform at +25), razor wire, catenary street cables, laundry lines (cloth quads with wind), rooftop water tanks and TV antennas, goalposts, bleacher, light-shaft cards and dust motes.
- **Sections:** `S1`–`S9` per GDD §6.5–6.7 (S0 belongs to WP09). Props placed per GDD; civilian emitters; decals.
- **NavMesh:** a `NavMeshSurface` per section (agent radius 0.35, height 1.8, step 0.4, slope 40°; collect from physics colliders under the section root), area `Doorway` (cost 3, squad never stops there); `NavMeshLink`s for the wadi boulder, the roof planks, tank stand, steel stair and R3→R4/R4 stair (squad uses walk/climb clips), the R3/R4 drops and the ladder (squad uses a scripted climb). Data is saved in the section scene.
- **Section seams:** surfaces from different scenes do not join on their own. Each section's surface extends 4 m past every seam, and a `NavMeshLink` the width of the seam is placed in the section that loads later: S1↔S2 Weighbridge arch (x 176–191, z 160–170), S2↔S3 market arch (z 248–254) and service yard / side door (186, 0, 307), S3↔S4 ladder L (scripted climb), S4↔S5 breach hole (x 300, z 319–325) and the alley, S5↔S6 Hall double door (338, 0.3, 330), S6↔S7 postern and ramp (346, 0–−1, 382–391). Squad moves across a seam whose far section is not loaded wait at the seam.
- **Markup:** everything in Appendix A, using the Core markup components; `MarkupTools` editor window places markers from `Assets/_Game/Data/markup_ids.csv` and validates that every required id exists. WP00 generates that CSV from Appendix A in Phase A (so WP04/WP07 tests can use it before any level exists); WP10 owns it afterwards; Appendix A and the CSV change together, through `Docs/REQUESTS.md`.
- **`SignBaker`** (WP10, editor): renders TMP text (Sardani glyph font from WP08, or Latin) to textures for shop signs, the marquee, the company lettering, the film-can labels and the clock faces.
- `SplineVehicle` (trucks, technical drive-in, heli approach and exfil rail with banking), `FarProxyGroup` (hides the far blocks of loaded sections), `SquadTraversalLink` (vault/jump/ladder clip choice on links), `AwningPanel` (G3: listens to `TracerPath`, swaps to the torn mesh, enables its shaft card), `FruitCrate` (physics fruit on hit), `BreachWall` (intact ↔ blown swap).

### 7.16 Pooling, performance and memory rules
- Pools: tracers (64), impact decals (64), impact particles (per surface 16), muzzle lights (4), audio sources (32), enemies per section (prewarmed from `EncounterTable` counts while the section loads), grenades (8), shells/fruit (32).
- Frame budget (GDD §9.8): ≤ 500 k visible tris, ≤ 300 batches, ≤ 12 additional lights and ≤ 1 shadowed local light, ≤ 10 skinned characters animating near the player (+ the shadow proxy), AI ≤ 3 ms, audio ≤ 24 voices, 60 fps on an M-series Mac in Chrome at ≤ 2.1 MP internal resolution (DPR ≤ 1.5, default render scale per D21).
- AI: LOS round-robin (≤ 24 rays/frame), NavMesh obstacle avoidance quality Low (squad Medium), perception at 10 Hz, cover evaluation at its interval only.
- Animation: `CullUpdateTransforms`; rigs off beyond 40 m.
- Loading: every bundle load happens behind a fade, a radio beat or a cinematic; Crunch transcode and LZ4 decompression run on the main thread.
- Memory: heap 256 MB initial, 4096 max; log `Profiler.GetTotalAllocatedMemoryLong` per section load in QA builds; target peak ≤ 1.8 GB (Safari headroom).

### 7.17 Dev tools (WP11 `LongShadow.Integration`)
`DevConsole` (enabled in the editor or with `?dev=1`): god mode, noclip, `cp <id>`, `beat <n>`, `s <value>` (LightStage), `killall`, `ai` (state labels), `fps`, `mem`, `vo <id>`, `insert <id>`. `BeatJumpWindow` (editor). `BudgetReport` (editor: tris, batches, textures, bundle sizes vs §8.9).

---

## 8. Asset pipeline

All generation and download is driven by `Docs/ASSET_MANIFEST.json`. **Tools never print secrets**, write a `provenance.json` next to every raw output (source, endpoint, arguments, seed, request id, wall time, list price, license) and skip work whose output already exists (idempotent, `--force` to redo).

**Manifest schema** (636 entries; every entry has all keys): `id`, `category` (weapon | prop | character | texture | hdri | sfx | vo | music | image | ui | decal; animation clips are `character`, ambience beds are `sfx`, video is `image`), `source`, `model_or_asset_id` (fal endpoint or CC0/library id), `prompt` (fal; `null` otherwise), `url` (downloads and local paths; `null` otherwise), `variants`, `target_path`, `budget`, `priority` (P0 must-have, P1, P2), `notes` (pipeline steps, review flags, and `Est. list cost $x.xx` for paid items).
- `source` uses the four planned values (`fal`, `polyhaven`, `ambientcg`, `procedural`) plus three that the research made necessary: **`local`** (already downloaded into `RawAssets/`, license in the notes), **`github`** (Microsoft Rocketbox MIT avatars and Unity SAC clips, **needs approval O2**) and **`google_fonts`** (OFL fonts, **needs approval O1**).
- VO entries reference the line by id (`prompt: "DIALOGUE.json#D015"`) instead of copying text; `variants` lists the processing chains, and `target_path` uses a `{CHAIN}` placeholder (`Assets/_Game/Audio/VO/{CHAIN}/VO_D015_{CHAIN}.wav`). Casting lives in the ten `VOICE_<voice_id>` entries plus `VO_CLOCKMAKER_TOKENS`.
- Video targets are the only shipped paths outside `Assets/_Game/` (`Assets/StreamingAssets/video/`, required by `VideoPlayer.url`). `VO_CLOCKMAKER_TOKENS` targets `RawAssets/audio/vo/tokens/` because the tokens are only offline splice material; the four Clockmaker lines are `procedural` (spliced, no TTS call).
- One pass of every paid entry is about $111 at list prices (P0 $89); add about $14–20 if O2 is refused and the Meshy character fallback becomes P0; see O6.

### 8.1 Tools (outside `Assets/`, owned by WP00 unless noted)
| Tool | Purpose |
|---|---|
| `Tools/fal/run_manifest.py` | `--ids`, `--category`, `--priority`, `--stage` (ref, 3d, audio or image), `--dry-run` prints a cost estimate and stops; `--budget-usd N` hard cap per invocation; uploads with `fal_client.upload_file`; downloads to `RawAssets/<area>/<id>/`. Built on `RawAssets/fal3d/tools/fal_run.py` + `falmedia/tools/falrun.py` |
| `Tools/cc0/fetch.py` | wraps `RawAssets/cc0/tools/fetch_cc0.py` (manifest-driven Poly Haven/ambientCG downloads, custom User-Agent, md5 check, strips `.blend/.usdc/.mtlx/.tres`) |
| `Tools/mesh/process_glb.sh` | `gltf-transform weld` → `simplify --ratio r --error 0.002` (r ≥ 0.5 unless re-baked) → `resize` (2048/1024/512); `gltfpack -si r -noq -kn` for world LODs |
| `Tools/mesh/gltf_to_obj.py` | GLB/glTF → OBJ (+ MTL) + external textures; applies node transforms, normalises scale to a target real length/height (`--length 0.88`), fixes orientation, keeps normals/UVs, splits by material |
| `Tools/mesh/inspect.py` | tris, verts, maps, bbox (from `RawAssets/fal3d/tools/inspect_glb.py`) |
| `Tools/tex/pack_mask.py` | Poly Haven ARM / ambientCG Roughness+AO+Metalness / glTF MR → `T_*_M` (R metal, G AO, A smoothness); resize; normal GL passthrough |
| `Tools/tex/hdri_prep.py` (WP10) | finds the sun pixel (azimuth/elevation), paints the sun out of the HDR (`*_nosun_2k.exr`, EditorOnly) and the tonemapped JPG (`T_Sky_<id>_LDR.png`, 2048×1024) |
| `Tools/tex/retexture_rocketbox.py` (WP05) | masks the ACU camo (HSV threshold), fills with a period/faction fabric multiplied by the high-passed luminance, paints out flag and name tapes, paints orange armbands (Legion) |
| `Tools/tex/glyph_font.py` (WP08) | generates `Sardani-Regular.ttf` with fontTools (geometric strokes, 24 glyphs + numerals) and a proof sheet for the resemblance review |
| `Tools/tex/decal_alpha.py` | alpha from displacement/AO for ambientCG decals without opacity (TireTracks001) |
| `Tools/audio/vo_generate.py`, `vo_chains.py`, `envelopes.py`, `clockmaker_splice.py` (WP06) | §8.7 |
| `Tools/audio/sfx_generate.py`, `sfx_process.py`, `music_generate.py`, `music_loop.py`, `motif_synth.py` (WP06) | §8.7 (built on `RawAssets/falmedia/tools/fal_post.py`); `sfx_process.py` also builds the procedural voice/music beds (`SFX_TV_MUFFLED`, `SFX_RADIO_TURNED_DOWN`, `AMB_DISTANT_SHOUTING`) from our own takes |
| `Tools/tex/vfx_flipbook.py` (WP10) | numpy-noise flipbooks for `TEX_VFX_*` |
| `Tools/build/compile_check.py`, `land.py`, `editor_lock.py`, `validate_build.py`, `publish_plan.py`, `validate_assets.py` | §9–10 |

### 8.2 fal 3D (weapons, props, vehicles, trees, character props)
1. **Reference image:** `google/nano-banana-2.1` (2K; 16:9 long guns, 1:1 props, 3:4 tall objects) with the template prompt from fal_3d.md §6.1 (prompts are in the manifest). Always "no text, no markings, no logos, no insignia, no hands". Review the PNG (era, fiction, guardrails) before any 3D call.
2. **Moving parts:** for every gun, `google/nano-banana-2.1/edit` makes (a) the weapon **without its magazine** and (b) **the magazine alone**; both go to 3D. Hero guns (Commando) also get right-side and top views for multi-view.
3. **3D:** `tripo3d/p2/image-to-3d` with `face_limit` from the manifest budget (20000 FP, 3000–8000 props/world), `pbr: true`, `texture_quality: "detailed"`, `delight: true`, **`auto_size: false`**. Hero alternative: `fal-ai/hunyuan-3d/v3.1/pro/image-to-3d` multi-view (+$0.15). Cheap clutter: `fal-ai/hunyuan-3d/v3.1/rapid/image-to-3d` or `fal-ai/hyper3d/rodin/v2.5/fast`.
4. **Inspect + render** (`inspect.py`, `glb_viewer`) and review both sides.
5. **Process:** `process_glb.sh` to budget (≤ 50 % cut; beyond that, `tripo3d/tripo/remesh` with `bake: true` or accept as distant-only) → `gltf_to_obj.py --length <real m>` → textures: base → `_BC`, normal → `_N`, MR (+AO) → `pack_mask.py` → `_M`.
6. **Import (editor slot):** files land in `Art/Models/<Kind>/<Id>/` and `Art/Textures/<Kind>/<Id>/`; `ModelImportRules`/`TextureImportRules` apply; `MaterialFactory` creates `M_<Id>` (URP/Lit, mask in metallic + occlusion, smoothness from alpha, detail normal for FP guns); builders make the prefabs.
7. **Validate:** `validate_assets.py` compares tris/texture sizes with the manifest budgets; `BudgetReport` in the editor.

### 8.3 CC0 textures → materials
`fetch.py tex <id> 1k|2k` → `pack_mask.py` (Poly Haven `arm` → mask; `nor_gl` kept; ambientCG Color/NormalGL/Roughness/AO/Metalness) → `Art/Textures/Env/<Set>/T_<Set>_{BC,N,M}` (+ `_H` only for hero ground) → import rules → `MaterialFactory` → `M_<Set>` with tiling `1 / tile_m` for world-UV kit meshes. 2048 only for the 4 hero sets in the manifest (`sand_01`, `painted_plaster_wall`, `PaintedPlaster012`, `concrete_floor_worn_001`); everything else 1024, with one shared tiling detail normal for close-ups.

### 8.4 CC0 models (Poly Haven)
`fetch.py model <id> gltf_1k` → decimate heavy ones (boulders/cliffs to ≤ 8 k at ≤ 50 % per pass, then Unity Mesh LOD; anything that still exceeds budget becomes distant-only) → `gltf_to_obj.py` (real scale is already correct) → same import as 8.2.6. Excluded items: D17.

### 8.5 HDRI and sky
`fetch.py hdri kiara_8_sunset 2k` and `kiara_9_dusk` (+ tonemapped JPGs) → `hdri_prep.py` → `Art/HDRI/EditorOnly/*_nosun_2k.exr` (cube, bake only) and `Art/HDRI/Runtime/T_Sky_*_LDR.png` → `M_Sky_PanoBlend`. Alternates `rogland_sunset`, `kiara_1_dawn` only if the azimuth/horizon check fails. No HDR cubemap ships (WebGL has no BC6H).

### 8.6 Characters and animation
1. Rocketbox avatars: local copies first; others downloaded by `fetch.py rocketbox <name>` **after approval** (O2) into `RawAssets/cc0/characters/rocketbox/<name>/`.
2. `retexture_rocketbox.py` per faction/variant → `Art/Textures/Characters/<Avatar>_<Variant>/` (1024 bodies; squad heads 2048). Fabric tiles from fal (`TEX_FABRIC_*` in the manifest).
3. Import (editor): `RocketboxImporter` → Humanoid avatar (Enforce T-pose; jaw/eyes mapped) → `BodyPrefabBuilder` → `PFB_<Avatar>_<Variant>` (body, materials, `HumanoidBody`, rig, hitboxes + ragdoll from `RagdollBuilder`, sockets).
4. Character props (hats, goggles, NVG, chest rigs, PRC-77, camera) via §8.2 at 1–5 k tris, parented to head/spine bones.
5. Clip libraries: copy the local UAL1/UAL2/SAC/Rocketbox clip FBX files to `Art/Animation/Clips/<Source>/` → `ClipImporter`.
6. **Meshy rifle clips:** upload the CC0 UAL mannequin GLB (`RawAssets/cc0/characters/quaternius_ual/.../Unreal-Godot/UAL1_Standard.glb`) and call `fal-ai/meshy/rigging/multi-animation` with ≤ 10 ids per call. **First call = test set of 10**; check hand placement on Rocketbox before buying the rest. Resolve ids against the live library list first (the research docs disagree for some names; the five already bought were 0, 170, 183, 654, 690). Import as Humanoid; clip FBX meshes are never referenced.

### 8.7 Audio
1. **Listen gate (human, before bulk):** someone listens to `RawAssets/falmedia/processed/*` and one take per voice key, then approves voices and the gunshot recipe (O4). Until then only P0 slice lines are generated. Every generated ambience, music and bark take is also checked against the guardrail list: reject chanting, a call to prayer, bells, hymn-like or regional song, any intelligible real-language word in a bark, and any take where a voice that should be American sounds like a caricature. Results go into `Docs/AUDIO_REVIEW.md`.
2. **VO:** `vo_generate.py` reads `DIALOGUE.json` + the casting entries (`VOICE_*` in the manifest: endpoint, voice, settings, seed, style) → 3 takes per line into `RawAssets/audio/vo/raw/<id>/`. ElevenLabs v4 `pcm_48000` → `pcm2wav`; delivery goes into a leading tag (EL) or `style_instructions` (Gemini), never into the spoken text. Barks use phonetic text, no `language_code`, two voices (A/B). Clockmaker tokens are generated separately into `RawAssets/audio/vo/tokens/` and `clockmaker_splice.py` builds `D039`, `D066`, `D080`, `D092` and `NUM_BASEMENT_LOOP` on a 0.9 s grid (no per-line TTS).
3. **Chains** (`vo_chains.py`, GDD §10.1): DRY, RADIO, INTERCOM, SCRAMBLER (librosa phase-vocoder −3 st, 300–3400 Hz, 30 Hz ring mod 15 %, 10-bit crush, mains hum), TAPE, CLEAN, PA_HORN, HF_AM, ROOM_SPEAKER, GREYCOAT_SQUELCH, BOOTH, FAR (barks). All −18 LUFS, mono. Output `Assets/_Game/Audio/VO/<CHAIN>/VO_<id>_<CHAIN>.wav`. One take family per line feeds every chain (the TALLOW twist).
4. `envelopes.py` writes `Audio/Envelopes/VO_<id>.json` (30 Hz RMS, duration).
5. **SFX:** `sfx_generate.py` (Mirelo SFX 2.0 transients with `num_samples` set explicitly; ElevenLabs SFX v2 bodies, tails and `loop: true` beds) → `sfx_process.py` (`fal_post.py sfx` trim, punch, layer, `_far` bake with 1.5–3 kHz low-pass + longer echo, zone tails, slicing of footstep/impact sequences by onset) → `Audio/SFX/<Group>/`.
6. **Music:** `motif_synth.py` renders the three-note music-box motif and the interval signal → `music_generate.py` (`elevenlabs/music/v2.5`, `force_instrumental`, 60–90 s, `composition_plan`, motif as `audio_reference`) → `music_loop.py` (bar-line cuts before the generated outro, 1024-sample lead-in, equal-power crossfade) → `Audio/Music/`.
7. Import (editor slot) with `AudioImportRules`, then `DialogueImporter` and `AudioLibraryBuilder`.

### 8.8 Images, decals, UI, video
- Photos/plates: Nano Banana 2.1 at 2K (map of Sardahn, recon aerial, airport plate, intel photos, ledger and document plates), "no text"; all overlay text is set live in TMP. Then crop/resize → `Art/Textures/Images/IMG_*`.
- Decals: `openai/gpt-image-2.5/flare/text-to-image` with transparent background (bullet holes, scorch, grime, chalk drawings, posters without text) → atlases (`Tools/tex/atlas.py`) → `Art/Textures/Decals/`. ambientCG decals as listed. Every decal and poster gets a guardrail review (no real script, slogans or symbols).
- Signage, posters, marquee titles and the company name: rendered in-engine with TMP (Sardani glyph font or Latin) and baked to textures by `SignBaker` (editor).
- Video (P1): Unity Recorder → `StreamingAssets/video/VID_*.mp4` (H.264, 512×384 or 720p, ≤ 14 MB each).

### 8.9 Budgets per asset class

These caps are enforced by `TextureImportRules` (Max Size per folder and per hero list) and checked by `validate_assets.py`; the manifest `budget` fields say "N maps in game (M source)" where they differ. Measured basis: a Crunch q50 set of BC + N + mask costs ≈ 3.5 MB at 2048, ≈ 0.87 MB at 1024, ≈ 0.23 MB at 512 (webgl.md §3.7).

| Class | LOD0 tris | Textures (in game) | Download each (target) |
|---|---|---|---|
| FP weapon (+ magazine) | 12–20 k (+1–3 k) | BC 2048, N 2048, M 1024, crunch q80; magazine 512 | ≤ 3.3 MB |
| World/enemy weapon | 3–7 k | AK and RPK world models keep the FP UVs and **share the FP textures**; world-only guns 1024 | ≤ 0.9 MB (0 MB textures when shared) |
| Grenade, knife, small FP items | 1–3 k | 512 | ≤ 0.3 MB |
| Squad character | 8–15 k (Rocketbox) | body, equipment, head 1024; Hollis head 2048 (heli climax) | ≤ 3.0 MB (Hollis ≤ 5.5 MB) |
| Calder (FP arms, shadow proxy, booth, reel variant: one texture set) | 15.4 k | 1024 + one extra 1024 head for the shaved variant | ≤ 4.0 MB |
| Enemy body variant, interrogator, Magpie | 8–15 k | 512 (shared fabrics) | ≤ 1.2 MB |
| Character prop (hats, rigs, NVG, PRC-77 on Tinker) | 1–3 k | 512 (PRC-77 1024) | ≤ 0.3 MB |
| Humanoid clip | — | — | ≤ 0.1 MB average |
| Small prop | 0.3–2 k | 512 | ≤ 0.3 MB |
| Medium/large prop | 2–8 k | **512**, except the 13 hero props at 1024 (CRTs ×2, binoculars, projector, scrambler box, chair, reel-to-reel, tube rack, safe, PRC-77, teletype, iron gate, `rock_face_01`) | ≤ 0.45 MB (hero ≤ 1.1 MB) |
| Vehicle | 8–15 k | technical and helicopter BC 2048 + N/M 1024; trucks 1024 | ≤ 2.4 MB (trucks ≤ 1.3 MB) |
| Tree | 4–8 k + leaf cards | 1024 | ≤ 1.5 MB |
| Building (combined) | 2–15 k (LOD1 50 %) | env materials | — |
| Far proxy (whole town + Shoulder/grove far terrain) | ≤ 150 k total | Baked Lit + 3 lightmap sets (counted under lightmaps) | ≤ 4 MB meshes |
| Env material set | — | 1024 (4 hero at 2048) | 0.87 / 3.5 MB |
| Decal atlas | — | 1024 | ≤ 0.6 MB |
| Lightmap | — | ≤ 2048, Normal Quality | ≤ 4 MB |
| SFX one-shot | — | — | mono ≤ 2 s, ≤ 30 KB |
| VO line (per chain) | — | — | mono 22–24 kHz, ≤ 30 KB average |
| Music cue | — | — | stereo 60–90 s, ≤ 1.3 MB |
| Ambience loop | — | — | stereo 20–30 s, ≤ 0.45 MB |
| CRT/intel image | — | 1024 (2048 full-screen) | ≤ 0.5 MB |
| Video clip | — | 512×384 H.264 ~1 Mbps | ≤ 1.5 MB per 10 s |

### 8.10 Total size and bundle plan
**Target ≤ 220 MB planned, 230 MB hard cap** (host: 256 MB per version, ≤ 15 MB per file). The earlier 215 MB table under-counted props, weapons, characters, meshes and lightmaps by roughly 40 MB against the manifest; this table is re-derived from the P0 manifest with the §8.9 caps. **P1/P2 assets go in only while `BudgetReport` shows headroom under 220 MB.**

| Content | Basis (P0) | Budget (download) |
|---|---|---|
| Engine (wasm + framework + loader) | measured 5.8–7.0 MB | 7 MB |
| Boot `.data` (`S_Boot`, fonts, code metadata) | measured 3.8–4.6 MB + fonts | 6 MB |
| Shared shaders + UI + FX textures | | 6 MB |
| Environment textures | 4 × 3.5 + 33 × 0.87 | 45 MB |
| Kit, combined buildings, terrain, far-proxy meshes | ≈ 450 k unique tris with UV1 + far proxies | 14 MB |
| Props | 13 hero × 1.1 + ≈ 40 × 0.45 (+ meshes) | 28 MB |
| Vehicles | technical, cargo truck, helicopter | 6 MB |
| Weapons | 4 FP × 3.3 + world-only + small items | 19 MB |
| Characters | squad 3 × 3.0 + Hollis head + Calder 4.0 + 6 × 1.2 + props + meshes | 24 MB |
| Animation | ≈ 60 humanoid clips | 6 MB |
| Lightmaps (27 eq) + reflection probes | D16 | 33 MB |
| Audio | §3.12 rules, ≈ 300 VO files, 8 music cues | 18 MB |
| Images and video | ≈ 15 images at 1024 + 2 clips ≤ 2 MB | 8 MB |
| **Total** | | **220 MB** |

Addressables groups (`AddressablesGroupRules`, every bundle ≤ 12 MB, LZ4, `*.bundle`): `Core_Shaders`, `Core_Persistent`, `Player_FP`, `Weapons_A/B`, `Chars_Squad`, `Chars_Legion`, `Chars_Greycoat`, `Chars_Booth`, `Anim_A/B`, `Audio_VO_A/B/C`, `Audio_SFX_A/B`, `Audio_Music_A/B`, `Audio_Amb`, `Env_Tex_A…F` (materials shared by several sections), `Props_A…C`, `Vehicles`, `Images`, and one group per section `S0_Booth` … `S9_TownFar` (split `_A/_B` when a section passes 12 MB). Shared assets are placed explicitly (Addressables "Check Duplicate Bundle Dependencies" must report nothing). `Weapons_A` (Commando, Hush Puppy, M67, knife) is part of the boot set; `Weapons_B` (AK and RPK, FP and world, sharing textures) loads with `S1_Approach` because the Beat 2 riflemen carry AKs. Expected ~40 files. The click gate needs ≈ 55 MB on a first visit (engine, boot, shaders, persistent content, booth, D20); `S9_TownFar`, `S1_Approach` and `Weapons_B` (≈ 30 MB) stream behind the gate and the intro; later sections stream at the GDD §6.7 quiet points. IndexedDB caches bundles for revisits (not in Safari iframes).

---

## 9. Build and publish

### 9.1 Build (`LongShadow/Build/Web Release`, WP01; runs synchronously inside `Unity_RunCommand` because `EditorApplication.delayCall` does not fire while the editor is unfocused)
1. Assert the PlayerSettings recipe (§3.1); set `nameFilesAsHashes = true` for release.
2. `AddressableAssetSettings.CleanPlayerContent()` then `BuildPlayerContent()`; fail on any build error or duplicate-dependency report.
3. Delete `Builds/Web/` (stale files survive rebuilds), build the player (`BuildPipeline.BuildPlayer`, scene `S_Boot` only) to **`/Users/gencsaracaydin/Documents/fun/fun-unity/Builds/Web`**.
4. Post-process: remove `*.manifest`, unused `TemplateData` files and any foreign files; keep `StreamingAssets/aa/**` (catalog `.bin`/`.hash`, `settings.json`, bundles) and `StreamingAssets/video/**`.
5. `PostBuildValidator` (also `Tools/build/validate_build.py`): **fail** if any file > 14.5 MB, file count > 250, total > 230 MB, any bundle > 12 MB, any `EditorOnly` asset or any HDR-format texture (RGBAHalf, RGBAFloat, BC6H) in a bundle, any cubemap other than a baked reflection probe, or any reflection probe larger than 256 px per face (baked probes at ≤ 256 px in RGBM are expected, D16); warn if total > 220 MB.
6. Write `Builds/Web_report.txt` (sizes per file, per group, per asset type from the BuildReport + Addressables build layout) and `Builds/Web_publish_plan.json`.
- QA variant `LongShadow/Build/Web QA (diagnostics)`: same, plus `showDiagnostics = true`, debug symbols External, `?dev=1` enabled. Development builds are never hostable (uncompressed wasm > 35 MB).

### 9.2 How the files map to artifact publishes
- `index.html` is the page (`file_path`). Every other file goes in `files` with its relative path as the key:
  - `Build/*.loader.js` (text/javascript), `Build/*.framework.js.unityweb`, `Build/*.wasm.unityweb`, `Build/*.data.unityweb` with `contentType: "application/octet-stream"`.
  - `StreamingAssets/aa/settings.json`, `StreamingAssets/aa/catalog.bin` and `.hash` (`application/octet-stream`), `StreamingAssets/aa/WebGL/*.bundle` (`application/octet-stream`), `StreamingAssets/video/*.mp4` (`video/mp4`), `TemplateData/style.css`.
- `publish_plan.py` packs files into batches of **≤ 60 MB and ≤ 250 files**; the first call creates the artifact, later calls pass the same `url` and add files (≤ 511 files / 256 MB per version). About 4 calls.
- **Stale files (D22):** the host keeps every file a publish leaves out, and hashed names change on every build. `publish_plan.py` diffs the new build against `Builds/Web_published_files.json` (written after each approved publish) and puts a `null` entry for every path that disappeared into the first batch, so a version never holds two builds. Total version size after the removals is what must stay ≤ 230 MB.
- If the host rejects `.unityweb`/`.bundle`, rename to `.bin` (`x.bundle.bin` still matches the loader's IndexedDB cache regex) and patch the loader URLs in `index.html` (webgl.md §6.2).
- **Every publish needs the user's explicit go-ahead** (the first private smoke test included, GDD §18.3). WP11 asks; nobody publishes on their own initiative.

### 9.3 Content gates before a publish
`validate_assets.py` (manifest budgets), Addressables analyze (no duplicates), `BudgetReport` (tris, batches per section), smoke test green (§10.3), guardrail review list signed off (decals, posters, barks).

---

## 10. Testing and verification

### 10.1 Compile gate (no editor)
`python3 Tools/build/compile_check.py [--system X] [--overlay Staging/] [--editor]` compiles every `LongShadow.*` asmdef in dependency order with Unity's bundled Roslyn (`.../Scripting/DotNetSdk/dotnet .../Roslyn/bincore/csc.dll`) against `Scripting/Managed/UnityEngine/*.dll`, `NetStandard/ref/2.1.0/netstandard.dll`, `UnityEditor.dll` (editor asmdefs), and package DLLs from `Library/ScriptAssemblies/` (InputSystem, URP/Core runtime, TextMeshPro, UnityEngine.UI, Timeline, AI.Navigation, Splines, Animation.Rigging, Addressables, ResourceManager). Defines: `UNITY_6000_6_OR_NEWER` (and the usual `UNITY_20xx_OR_NEWER` chain), `UNITY_WEBGL`, `ENABLE_INPUT_SYSTEM`, plus `UNITY_EDITOR` for the editor pass. Two passes (player and editor) catch `#if UNITY_EDITOR` mistakes. Exit code ≠ 0 on any CS error. `land.py <System>` runs it with the staged files and only then copies `Staging/<System>` into `Assets/_Game/Scripts/<System>` and appends to `Docs/LANDING_LOG.md`.

### 10.2 Editor checks via Unity MCP (editor-slot holder only)
1. **Refresh + compile:** `Unity_RunCommand` → `AssetDatabase.Refresh()`; wait for compilation; `Unity_ReadConsole` / `Unity_GetConsoleLogs` must show **0 errors**.
2. **EditMode tests:** `Unity_RunCommand` starts `TestRunnerApi.Execute(EditMode)` with a callback that writes `Logs/tests/editmode_<ts>.xml`. Required suites: Core utilities, recoil patterns and falloff (GDD §13.1), hit-chance formula (§14.3), regen (§16), LightStage curves (§9.2), VO priority queue and crossfade weights, checkpoint snapshot round-trip, objective/marker logic, encounter caps.
3. **Play mode:** `Unity_ManageEditor` (play/stop) with Addressables "Use Asset Database" play mode; `Unity_Camera_Capture` for screenshots; `Unity_SceneView_CaptureMultiAngleSceneView` for layout checks; `Unity_Profiler_*` tools for CPU/GC hot spots (GC alloc per frame must be ~0 in combat).
4. MCP caveats: domain reloads make MCP unavailable for 20–30 s; builds run synchronously inside `Unity_RunCommand`; the editor log is `Logs/Editor.log`.

### 10.3 Automated smoke test (WP07 `MissionSmokeTest`, driven by WP11)
Started from `Unity_RunCommand` during play mode (`LongShadow.Integration.SmokeTest.Run(options)`) or with `?autotest=1` in a web build. For every checkpoint CP1 … CP10 + CP3b: `ResumeFrom(cp)` → wait for its sections → god mode → assert (objective set, markers resolvable, required marker ids present, squad at the node set, encounter spawns ≤ max alive and ≤ 12, music cue, LightStage within the beat's `s` range) → dwell 8 s with AI fighting → capture screenshot (`Builds/captures/smoke_<ts>/<cp>.png`) → force-complete (dev kill + objective complete) → next. Records exceptions (`Application.logMessageReceived`), frame time p50/p95, GC alloc, memory, load time per section into `Logs/smoke/smoke_<ts>.json` (web build: `window.LS_SMOKE_RESULT` for the browser pane). **Pass = zero exceptions, all asserts true, every beat reached.**

### 10.4 Look review
`LookCapture.Run(sections, sValues = {0.3, 1.2, 1.9, 2.5, 3.5})` renders beauty shots at `CAM.Look.*` markers to `Builds/captures/look_<ts>/`. Reviewed against GDD §9 (light reads, no two suns, shadows, HUD palette).

### 10.5 Web build verification
Release (or QA) build → `python3 -m http.server` on a free port (not 8765) in `Builds/Web` → Claude browser pane in **its own tab** (`tabs_create`) → read console (no errors besides the expected "configure Content-Encoding" notice), screenshot the click gate and first beat, run `?autotest=1`, read `LS_SMOKE_RESULT`. Pointer lock cannot be granted in the automation pane; real Chrome and Safari passes are manual (O5). Then, with the user's approval, the private artifact smoke test (§9.2) checks frame permissions, CSP, content types, IndexedDB and audio.

---

## 11. Work packages

### 11.1 Overview

| WP | Name | Owns (exclusive write) | Depends on | Editor use | Est. effort | fal cap |
|---|---|---|---|---|---|---|
| **WP00** | Foundation: Core, Input, settings, tooling | `Scripts/Core/**`, `Scripts/Input/**`, `Settings/Input/**`, `Settings/URP/**`, `Packages/manifest.json`, `ProjectSettings/**`, `Assets/Settings/*` (until WP10 takes URP tuning), `Tools/{build,fal,mesh,tex,cc0}/**` (except files named for other WPs), `Docs/CORE_CHANGES.md`, folder + asmdef skeleton | — | **Phase A: exclusive, first (~2 h).** Phase B: one short slot | 1–1.5 days | $0 |
| **WP01** | Platform, streaming and build | `Scripts/Platform/**`, `Plugins/WebGL/**`, `Assets/WebGLTemplates/**`, `Scenes/S_Boot.unity`, `AddressableAssetsData/**` (rules only; group contents follow §8.10), `link.xml`, `Tools/build/{validate_build,publish_plan}.py` | WP00-A | code-only; one slot for template + test build (~1 h); build slots later | 2 days | $0 |
| **WP02** | Player controller and health | `Scripts/Player/**` | WP00-A | none (code + builder) | 2 days | $0 |
| **WP03** | Weapons and combat (+ weapon art) | `Scripts/Combat/**`, `Art/{Models,Textures,Materials}/Weapons/**`, `Prefabs/Weapons/**` (weapon sounds are WP06 clips referenced by cue id) | WP00-A; WP00-B tools for art | code-only for systems; slots for weapon import/prefabs (~3 h) | 3–4 days | $30 |
| **WP04** | AI: enemies, squad, encounters | `Scripts/AI/**`, `Prefabs/Enemies/**`, `Prefabs/Squad/**` | WP00-A | code-only; optional slot for `AIDevArena` (~1 h) | 4 days | $0 |
| **WP05** | Characters and animation | `Scripts/Characters/**`, `Art/Models/Characters/**`, `Art/Textures/Characters/**`, `Art/Materials/Characters/**`, `Art/Animation/**`, `Prefabs/Bodies/**`, `Tools/tex/retexture_rocketbox.py` | WP00-A/B; approval O2 (needed before character work starts) | **heavy** (~8–10 h of slots) | 4 days | $20 ($40 if O2 is refused and the Meshy fallback is built) |
| **WP06** | Audio, radio and VO (+ all audio content) | `Scripts/Audio/**`, `Audio/**`, `Tools/audio/**`, `RawAssets/audio/**` | WP00-A; listen gate O4 | code-only; slot for import + libraries (~1.5 h) | 3–4 days | $45 |
| **WP07** | Mission director, objectives, checkpoints | `Scripts/Mission/**` (encounter data is WP04's `EncounterTable`) | WP00-A | code-only | 4 days | $0 |
| **WP08** | UI: HUD and menus | `Scripts/UI/**`, `UI/**`, `Art/Fonts/**`, `Tools/tex/glyph_font.py` | WP00-A; approval O1 for fonts | code-only (theme asset by WP11) | 3 days | $3 |
| **WP09** | Cinematics and the booth | `Scripts/Cinematics/**`, `Scenes/Sections/S0_Booth.unity`, `Timelines/**`, `Prefabs/Booth/**`, `Art/Shaders/{SG_CRT*,SG_Film*,SG_Gun*,SG_Glitch*}`, `Art/Textures/Images/**`, `StreamingAssets/video/**` | WP00-A/B; WP05 booth bodies; WP06 VO; WP10 MaterialFactory outputs | **heavy** (~8 h) | 4 days | $15 |
| **WP10** | World: look, lighting and levels | `Scripts/World/**`, `Scenes/Sections/S1_*`…`S9_*`, `Art/{Models,Textures,Materials}/{Env,Kit,Props,Vehicles,Vegetation,Terrain,Backdrop,Decals}/**`, `Art/HDRI/**`, `Art/Shaders/` (all others), `Art/VFX/**`, `Settings/{Volumes,Lighting}/**`, `Prefabs/{Kit,Props,Vehicles,FX}/**`, `Data/Lighting/**`, URP asset tuning after WP00, `Tools/tex/hdri_prep.py` | WP00-A/B | **heaviest** (~25–40 h); may run as two agents: **Look** (materials, sky, volumes, LightStage, bakes) and **Level** (kit, sections, props, NavMesh, markup) | 8–12 days | $60 |
| **WP11** | Integration and QA | `Scenes/S_Persistent.unity`, `Scripts/Integration/**`, `Prefabs/Systems/**`, `Data/Libraries/**`, `UI/Themes/**`, removal of research scaffolding, final say on any shared file | all | editor (long slots) | continuous from the slice on | $5 |

**Manifest ownership** (who generates, imports and may write the folders of each `ASSET_MANIFEST` id; this overrides folder ownership where they overlap): WP03 `WPN_*`, `PROC_SHELL_CASINGS`; WP05 `CHR_*`, `CHP_*`, `ANIM_*`, `TEX_FABRIC_*`; WP06 every `sfx`, `vo` and `music` entry; WP08 `UI_*`; WP09 the booth set (`FAL_SCRAMBLER_BOX`, `FAL_INTERROGATION_CHAIR`, `FAL_ROTARY_PHONE`, `FAL_ASHTRAY`, `FAL_REEL_TO_REEL`, `FAL_AV_CART`, `FAL_FILM_CAMERA`, `PH_TELEVISION_01`, `PH_TELEVISION_02`, `PH_WALL_CLOCK`, `PH_VINTAGE_LIGHTER`, `PH_MOUNTED_FLUORESCENT_LIGHTS`, `PROC_BOOTH_SHELL`, `PROC_BOOTH_SMALLS`, `TEX_ACOUSTIC_TILE`, `TEX_ACG_PAINTEDPLASTER012`) plus every `IMG_*` (except `IMG_CONCEPT_KEYFRAMES`, which WP10 needs first) and `VID_*`; WP10 everything else. Shared props (e.g. `PH_WOODEN_TABLE_02`, `FAL_REEL_TO_REEL` also used in the basement) are produced once by their owner and reused. Raw downloads and generations for a manifest id go to `RawAssets/<owner WP, lower case>/<id>/` (e.g. `RawAssets/wp03/WPN_AK/`), except audio (`RawAssets/audio/**`, WP06) and the existing research folders, which stay where they are.

**Other ownership not covered by folders:** `Assets/_Game/Data/markup_ids.csv`: generated by WP00 from Appendix A in Phase A, owned by WP10 afterwards. `SignBaker`, `Tools/tex/vfx_flipbook.py`: WP10. `Tools/audio/clockmaker_splice.py`: WP06. MID-A orchestration (`Beat05LastLight`): WP07, using hooks owned by WP10 (LightStage, lamps), WP06 (positional radio, numbers), WP09 (I2), WP04 (squad look target) and WP05 (watch check). `S8_Heli` scene: WP10 (cabin kit, exterior, splines), with the door gun from WP03 and the targets and trucks from WP04's `EncounterTable` (E11).

### 11.2 Editor protocol
- **One driver at a time.** The lock is `/Users/gencsaracaydin/Documents/fun/fun-unity/.locks/editor.json` (`owner`, `wp`, `purpose`, `since`, `expected_minutes`), managed by `Tools/build/editor_lock.py acquire|release|status|queue`. Slots are ≤ 60 min; the next agent in the queue takes over. A stale lock (> 90 min without heartbeat) may be broken by WP11 only.
- At slot start the holder runs `land.py --pending` (merges green staged code), refresh, console check; at slot end: save scenes/assets, console check, release.
- Code-only packages never touch the editor; they compile with §10.1 and land through `land.py`.
- Package installs and ProjectSettings changes: WP00 (then WP11) only.
- File ownership is exclusive per the table. A package that needs something in another package's folder files a request in `Docs/CORE_CHANGES.md` (Core) or `Docs/REQUESTS.md` (others).

### 11.3 Dependency graph and schedule
```
WP00-A ──┬─> WP01 ─┐
         ├─> WP02 ─┤
         ├─> WP04 ─┤
         ├─> WP07 ─┤
         ├─> WP08 ─┤
         ├─> WP06 ─┼──> WP11 vertical slice (Beat 3 + Beat 5) ──> smoke publish (approval) ──> WP11 full mission
         └─> WP00-B ┬─> WP03 ─┤
                    ├─> WP05 ─┤
                    ├─> WP10 ─┤
                    └─> WP09 ─┘
```
Suggested editor queue after WP00-A: WP00-B verification → WP10 Look spike (4 hero materials, sky, volumes, LightStage in a dev scene) → WP05 spike (one Rocketbox body + UAL + the 5 Meshy clips) → WP03 Commando import → WP10 Level greybox of `S2_Market` + `S4_Roofs` with markup → WP06 slice audio import → **WP11 vertical slice** (GDD §18.3: Beat 3 + Beat 5 at full look, squad, radio, sunset) → web build + smoke publish (with approval) → round-robin content passes → full integration.

### 11.4 Packages in detail

#### WP00 Foundation
- **Phase A (blocking):**
  1. Install the §2.1 packages in one manifest change; wait for compilation; console clean.
  2. `ProjectSetup.cs`: §3.1–3.9, §3.13 (+ `PC_Renderer_RT`, `PostProcessData_Web`, layers, rendering layers, physics matrix, project-wide input actions); run it; log.
  3. Folder skeleton (§4.1) and **all 13 runtime asmdefs + editor asmdefs + test asmdefs** with the reference table of §4.3 (each with a placeholder `AssemblyInfo.cs` so they compile empty).
  4. Core (§6) with XML docs; Input (§7.2).
  5. `compile_check.py`, `land.py`, `editor_lock.py`, `Staging/` layout, `Docs/CORE_CHANGES.md`, `Docs/REQUESTS.md`, `Docs/LANDING_LOG.md`, and `Assets/_Game/Data/markup_ids.csv` generated from Appendix A (one row per id: id, kind, beat, x, y, z, extents or radius, heading, notes).
  6. EditMode tests for Core utilities.
- **Phase B (non-blocking):** `TextureImportRules`, `ModelImportRules`, `AudioImportRules` (§3.10–3.12), `MaterialFactory`, `Tools/fal/run_manifest.py` (with `--dry-run` cost and `--budget-usd`), `Tools/cc0/fetch.py`, `Tools/mesh/*`, `Tools/tex/{pack_mask,decal_alpha,atlas}.py`, `Tools/requirements.txt` + `Tools/.venv`, `Tools/build/validate_assets.py`.
- **Definition of done:** manifest has exactly the §2 packages at those versions; console 0 errors/0 warnings from our code; `ProjectSetup` is idempotent (second run logs no changes) and every §3 value is visible in its log; `compile_check.py --all` passes in < 30 s and fails with a CS error when a broken file is staged; every asmdef compiles empty; Core tests green; a sample texture/model/audio dropped into each rule folder imports with the documented settings; `run_manifest.py --dry-run --priority P0` prints a cost table without calling fal; the research scaffolding still builds (`Tools/Web/Build WebGL Test` untouched).

#### WP01 Platform, streaming and build
- **Builds:** §7.1 scripts, template, jslib, `S_Boot` scene (via a builder), Addressables settings + group rules, `link.xml`, `WebBuild`, `PostBuildValidator`, `validate_build.py`, `publish_plan.py`.
- **DoD:** `S_Boot` + a placeholder addressable `S_Persistent` + one dummy section build with `Web Release` into `Builds/Web` in one menu action; the validator passes and fails correctly on a synthetic 15 MB file; served locally, the page shows the HTML loader, then the click gate; a click locks the pointer in real Chrome (manual note) and resumes audio; a rejected pointer lock produces **no alert dialog**; Esc opens pause and resume needs a click; settings persist across reloads (localStorage) and fall back to defaults in a private window; `?cp=`, `?dev=1`, `?autotest=1` parse; section load/unload calls tetrahedralize and logs memory; `publish_plan.json` batches are ≤ 60 MB / ≤ 250 files with content types.

#### WP02 Player controller and health
- **Builds:** §7.3, `PlayerPrefabBuilder`, EditMode tests for movement math (slide curve, vault ranges, fall damage, regen, sensitivity formula).
- **DoD:** compiles offline; in a builder-generated test course (ramps, 0.6/1.0/1.4/1.8/2.2 m ledges, 2 m gaps including a 2 m gap with a 2 m rise for the air mantle, ladder, 6 m drop) the player walks/sprints/crouches/slides/vaults/climbs/jumps with GDD §12 numbers (logged speeds within ±5 %); the shadow proxy casts a body shadow that matches stance and never renders in the view; fall damage starts above 4.5 m; regen matches GDD §16 for each difficulty; damage direction and death events fire; FOV requests blend; binocular glassing reports `IsGlassing`; OOB countdown → fail card → reload hook; zero GC alloc per frame while moving.

#### WP03 Weapons and combat
- **Builds:** §7.5; art for Commando, Hush Puppy, AK, RPK (FP + world + magazines), AKMS, SVD, RPG-7 (+ rocket), DShK, M60D, M67, knife (manifest `WPN_*`); builders; EditMode tests for falloff, recoil sequences, spread, reload timings, heat.
- **DoD:** each player weapon's numbers match GDD §13 (table-driven tests); in the arena, hits/kills, headshot multipliers and body-hits-to-kill match §13.1 rows; recoil first-N patterns reproduce exactly; ADS, sprint-out, reload (tactical vs empty with the separate magazine moving), switching and pickups work; grenades cook, bounce, explode with cover occlusion; AI shots via `ICombatService` miss 0.5–3 m from the player and raise `NearMiss`; tracers and impacts are pooled (no instantiate after warm-up); door gun heat/overheat per §13.3; FP weapon ≤ 20 k tris and ≤ 3 MB each in `BudgetReport`; no text/markings on any weapon texture.

#### WP04 AI: enemies, squad, encounters
- **Builds:** §7.6–7.7, `EncounterTable` for E2–E11 (59 enemies) and `ArchetypeTable`, `DebugCapsuleBody`, `EnemyPrefabBuilder`, `AIDevArena`, EditMode tests (hit chance, tokens, morale, caps, kill-share rule, callout direction).
- **DoD:** compiles offline; in `AIDevArena` with capsule bodies: enemies detect by sight/hearing per §14.2 (stage-dependent range), take cover, peek, suppress, flank pairs with squelch clicks, throw/flee grenades, respect tokens (never more than N firing at the player), fall back at 60 %; encounter caps hold (max alive per beat, 12 hard); closets never spawn in view or within 15 m; squad follows (6–10 m lead, ≤ 12 m), yields within 1 m, teleports on catch-up, executes node sets, never kills non-`SquadTarget` enemies the player is engaging, calls out with correct relative directions; AI cost ≤ 3 ms for 7 enemies + 3 squad in the editor profiler; an EditMode test samples the `B11.Exfil` spline and asserts every E11 target and truck is inside the left door-gun arc (yaw ±70°, pitch +15°/−60°) for its whole active window.

#### WP05 Characters and animation
- **Builds:** §7.8 and §8.6: bodies for every role in the manifest (`CHR_*`), retextured to 1984 kit with no flags/name tapes, orange Legion armbands; character props; clip libraries; Meshy rifle clip set (test 10 first); `AC_Soldier` + overrides; finger poses; `HumanoidBody`; ragdolls; `PFB_Calder_FP` + `FPArmsIK`; booth bodies (Calder seated, interrogator).
- **DoD:** every body prefab is a valid Humanoid (no avatar errors), stands at the right height (±2 cm), plays idle/walk/strafe/sprint/crouch/aim/reload/throw/death with hands on the weapon (left-hand IK error < 2 cm at the foregrip) and rifle grip fingers; ragdoll blends in 0.2 s after a death clip; `ICharacterBody` contract tested with WP04's brain in the arena; FP arms hold the Commando and follow its reload magazine (no legs or head visible in the view model at any pitch); `PFB_Calder_Shadow` plays its 5 states; watch check and pistol rise play; budgets: ≤ 15 k tris enemy bodies, ≤ 2 MB per variant; no ACU pattern, US flag or real insignia visible (screenshot sheet reviewed).

#### WP06 Audio, radio and VO
- **Builds:** §7.9–7.10 code; `Tools/audio/*`; all audio in the manifest (`VO_*`, `SFX_*`, `MUS_*`, `AMB_*`, `STG_*`); envelopes; `DialogueImporter`, `AudioLibraryBuilder`.
- **DoD:** listen gate O4 passed and recorded in `Docs/AUDIO_REVIEW.md` (including the guardrail rejections of §8.7-1); all 157 lines exist in every chain the manifest lists, at −18 LUFS ±1, trimmed (≤ 60 ms lead), mono, with envelopes; the four Clockmaker lines are spliced from tokens and `D039` is 13.5 s ± 0.2; the interval signal is 6.0 s; the three procedural beds (`SFX_TV_MUFFLED`, `SFX_RADIO_TURNED_DOWN`, `AMB_DISTANT_SHOUTING`) contain only our own material; barks pass the invented-word web check (GDD §2.3-5) and the "sounds like real speech" review; every SFX cue has its variants, `_far` and zone tails where listed; music loops are seamless in a web build test (AAC lead-in); VOManager tests: priority order, dry/radio weights at 6/10/12/20 m, no repeat after checkpoint, bark subtitle rule; total audio download ≤ 18 MB.

#### WP07 Mission director, objectives, checkpoints
- **Builds:** §7.11 including all 11 beat scripts + intro/outro flows exactly per GDD §7–8 timings and triggers; `MissionSmokeTest`; EditMode tests with fake services (every beat script runs start-to-end against a scripted fake world; checkpoint resume points).
- **DoD:** compiles offline; every line id used by a beat script exists in `DIALOGUE.json` (test); every marker id used exists in Appendix A (test against the CSV); every objective of GDD §7.12 exists with its exact id, text, marker kind and completion rule (table-driven test); every gate of GDD §7.13 and every backtrack blocker of GDD §6.7 opens and closes at its rule and survives a checkpoint round-trip; a test tries to skip each beat (reach its exit trigger before its objectives) and must fail to; each beat script can resume from each of its checkpoints; MID-A runs to the §8.5 timeline (±0.3 s per event) in a fake-world test; LightStage progress is monotonic and within the beat's range; deferred checkpoint rules tested; stats feed the debrief model; smoke test passes in the integrated build (with WP11).

#### WP08 UI: HUD and menus
- **Builds:** §7.13; `HudBuilder`; `glyph_font.py` + proof sheet; HUD icons via manifest `UI_*`.
- **DoD:** compiles offline; a builder-made test scene (with fake services) shows every HUD element per GDD §11 at 1920×1080 and 1280×720 with no overlap; markers clamp to edges with arrows and switch palette at `s` 2.0; subtitles format and colours per speaker; options change `ISettingsService` and persist; pause/click-to-resume flow; title card prints number groups that resolve; debrief types the dossier with live stats; zero GC alloc per frame from HUD updates; Sardani glyphs reviewed and resemble no real alphabet (O3).

#### WP09 Cinematics and the booth
- **Builds:** §7.12; `S0_Booth` (the WP09 booth set listed under manifest ownership in §11.1, baked lighting); `TL_Intro`, `TL_TitleCard`, `TL_MidB_Reel`, `TL_Climax`, `TL_Outro`; insert and death-insert systems; CRT/film/gun-cam/glitch shaders; CRT images; Harrou 8 mm clip (P1, D14).
- **DoD:** intro plays shot-for-shot to GDD §8.2 timings (±0.2 s) with limited look (±40°/±20° spring back), CRT montage, the live deck and ridge feeds, and the shot-8 hold when `S1_Approach` is late; MID-B switches the booth to its Session 11 state and back; `IMG_SURVEIL_SPARROW` and `PROC_BOOTH_SMALLS` exist; title card per §8.3; I1–I4 and the death insert freeze/unfreeze cleanly (no gameplay audio leak, input restored); MID-B and the climax hand control back correctly; outro ends on the debrief; skip works after first viewing; booth lighting baked; booth never lit by the sun; `S0_Booth` bundle ≤ 12 MB.

#### WP10 World: look, lighting and levels
- **Builds:** §7.14–7.15 and §8.3–8.5: all CC0 materials and HDRIs in the manifest; sky; volumes; LightStage with debug slider; kit and building generators; terrain; sections S1–S9 with props, decals, practicals, markup (Appendix A), NavMesh, bakes (incl. the basement emergency set and the TownFar GOLDEN/DUSK sets); fal props, vehicles and trees in the manifest.
- **DoD:** every section loads additively without errors and passes `MarkupTools` validation (all Appendix A ids, NavMesh connected along the critical path and across every seam of §7.15); LookCapture sheets at the 5 `s` values approved against GDD §9 (one sun, long shadows in Beat 1, sodium ripple, blue hour readable); **sun-geometry checks:** at `s` 1.80 the whole of R4's floor is in direct sun and the disc is visible from R4 eye height, at `s` 2.00 the disc is fully behind the ridge silhouette, at `s` 0.6 the Sunset Lane band lands on the market floor at z ≈ 236, and no shaft card or "sun" emissive sits where the shadow map says shade; POI1 reads through the binoculars from the ridge; frame budget (§7.16) met at each section's worst view in the editor profiler; each section bundle ≤ 12 MB and lightmaps within D16; no HDR cubemap in bundles; guardrail review of every poster/sign/decal.

#### WP11 Integration and QA
- **Builds:** `S_Persistent` (systems, player, squad, lights, volumes, HUD, libraries), prefab assembly with builders, `UITheme`, `LIB_*` assets, dev console, smoke-test menu, research-scaffolding removal, profiling passes, Safari checklist, publish (with approval).
- **DoD (vertical slice):** Beat 3 and Beat 5 playable start to end in a release web build served locally: squad leapfrogs, radio and subtitles, awnings tear, the sunset and the numbers play with the player's and squad's long shadows across R4 (no acne or peter-panning at 0.5°–1.5°), 60 fps target measured at ≤ 2.1 MP, no console errors, smoke test green on CP3–CP5, slice size extrapolated against §8.10.
- **DoD (final):** the full mission (intro → debrief) plays in a release web build; smoke test green for all checkpoints; total ≤ 230 MB, every file ≤ 14.5 MB, ≤ 250 files; Chrome performance target met (or performance mode documented); Safari pass notes recorded; credits include Rocketbox (MIT), Quaternius (CC0, optional credit), Unity SAC (UCL), Poly Haven, ambientCG; the private artifact is published only after the user approves.

---

## 12. Risks and open decisions

### 12.1 Decisions that need the user (asked by WP11 or the orchestrator, never assumed)
| # | Decision | Default if not approved |
|---|---|---|
| O1 | Download 3–4 OFL/Apache fonts from Google Fonts (typewriter, condensed sans, monospace). Not an approved source yet. | TMP LiberationSans + our procedural glyph font; weaker "typewriter" feel |
| O2 | Download more Microsoft Rocketbox avatars (MIT) and the rest of the Unity SAC strafe set (UCL) from raw.githubusercontent.com. Not an approved source yet; the GDD relies on Rocketbox for every human, but only Military_Male_01 (Calder) is complete on disk (Male_Adult_05 has no textures). **Needed before WP05 starts its character work.** | `CHR_FALLBACK_MESHY` for every role except Calder (≈ $14–20): fal `meshy/v7.1/multi-image-to-3d` characters from controlled reference images, rigged by Meshy (24 bones, **no fingers, no jaw**): hands stay open, Hollis's climax jaw flap and the interrogator's gloved-hand grip are dropped; Calder stays Rocketbox |
| O3 | Sign-off on the invented Sardani glyphs and the bark lexicon web check | Latin signage only; barks stay walla-only |
| O4 | A human listening pass on the test audio and one take per voice before bulk generation (~$45) | Only vertical-slice lines are generated |
| O5 | Approval for the first private artifact smoke-test publish, and for each later publish | Local serving only |
| O6 | fal spend: one pass of everything in the manifest is about **$111** at 2026-10-16 list prices (P0 $89, P1 $21, P2 $1; per-item costs are in each entry's `notes`); with typical retakes expect **$160–170**; add ≈ $14–20 if O2 is refused; Mirelo SFX rises 50 % after 2026-12-13 | P0 only (about $89 for one pass, $130 with retakes) |
| O7 | A local git repository (no remote) for rollback and diffing across parallel agents | Timestamped snapshots of `Assets/` + `ProjectSettings/` by WP11 before risky steps |
| O8 | Optional: install Blender (free, `brew install --cask blender`) for FP weapon bakes, finger bones on fal characters and better LODs | Not needed for v1 |

### 12.2 Technical risks
| # | Risk | Mitigation |
|---|---|---|
| R1 | **Artifact frame permissions**: pointer lock, fullscreen, autoplay may be blocked in a sandboxed iframe; Safari has no IndexedDB in iframes | Smoke publish right after the slice (O5); "click to look" fallback; performance-mode default on Safari |
| R2 | **Artifact host CSP / page wrapping**: WebAssembly compilation may need `wasm-unsafe-eval`; the decompression fallback runs `framework.js` from a **Blob URL**; the host wraps the main page in a document skeleton | The template keeps all scripts in `<body>` and works as a fragment. A CSP that blocks wasm or Blob scripts cannot be fixed from our side (an uncompressed wasm is over the 15 MB file cap), so this is the **#1 go/no-go check of the first smoke publish**, done right after the vertical slice and escalated to the user immediately if it fails |
| R3 | Content types `.unityweb`/`.bundle` rejected | Rename to `.bin` and patch loader URLs (§9.2) |
| R4 | Size over 230 MB | Crunch everything, D7/D8/D16, bundle validator, cut list GDD §18.4 |
| R5 | WebGL performance with the full post stack + 10 skinned characters + Forward+ lights | Vertical-slice profiling gate; performance mode; overlay camera fallback (D5); SSAO from depth only |
| R6 | Safari S3TC availability on Apple Silicon | Safari pass early; if missing, Safari gets a documented "unsupported or slow" notice (ASTC dual build is out of scope) |
| R7 | OBJ conversion artefacts (handedness, normals) | WP03 spike on the AK in hour 1; fallback glTFast 6.20.0 |
| R8 | Meshy rifle clip hand placement; id lists disagree between docs | Test 10 clips first; IK fix-ups; resolve ids against the live library |
| R9 | Rocketbox retexture quality / approval | Review sheet; fallback O2 path |
| R10 | Era/guardrail drift in generated images and meshes | Human review checklist per asset batch; prompts always forbid text/insignia |
| R11 | VO memory when decoded on Web | Beat windows (§7.10); measure in the slice |
| R12 | Single editor bottleneck | Code-only packages + offline compile; strict slot length; builders instead of hand-authored prefabs |
| R13 | Lightmap swap across additive scenes (index offsets) | `LightmapSwapper` matches by texture, not index; tested in the WP10 spike |
| R14 | Mirelo pricing changes after 2026-12-13 | Generate SFX early; re-check `llms.txt` prices before bulk runs |
| R15 | Shadow acne / peter-panning with the sun at 0.5°–1.5° (MID-A, the signature shot) | R4 is the slice's look test; per-stage bias values in `LightStageTable`; shadow strength fades out from 0.6°; fallback: hold the sun at 2.0° until MID-A 0:04 |
| R16 | Build size: the earlier 215 MB budget under-counted ≈ 40 MB | Per-class caps (§8.9), P1/P2 only within headroom, `BudgetReport` after every content landing, validator warns at 220 MB |
| R17 | O2 refused: Meshy characters have no fingers or jaw and faceted cloth normals | Gloves and relaxed-curl reference poses, helmets/goggles/scarves on enemies, climax framed so Hollis's mouth is in shadow; decision needed before WP05 starts |
| R18 | A section still loading when its first trigger is reached (slow connection) | Triggers wait on `ISectionStreamer.IsLoaded`; quiet-point loads leave ≥ 30 s of margin at 25 Mbps |

---

## Appendix A — Marker and trigger id contract (WP10 places, WP04/WP07 consume)

Coordinates are world metres from GDD §6–7 (booth and heli ids use their local frames, GDD §8.0). Kinds: `M` SceneMarker, `T` TriggerVolume, `N` SquadNodeSet (HOL/SPA/TIN), `C` SpawnCloset, `R` FlankRoute, `CP` CheckpointMarker, `P` PerchNode, `I` IlluminationZone, `G` GlassingPOI, `D` Destructible art + marker, `K` PlayerClip/door used as a gate or blocker. Triggers are axis-aligned boxes written `x a–b, z c–d, y e–f`, or spheres written `centre r n`; where no `y` is given a box spans from 1 m below to 4 m above the local floor. `MarkupTools` keeps a CSV copy (`Assets/_Game/Data/markup_ids.csv`, generated by WP00 from this appendix) that both sides test against.

| Beat | Ids |
|---|---|
| Global | `PA.Horn1` M (185, 8, 200) · `PA.Horn2` M (220, 20, 275) · `PA.Horn3` M (346, 6, 294) · `CAM.IntroRidge` M (42, 23.2, 30) h45 · `CAM.Look.*` M (beauty shots, ≥ 2 per section) · `CritPath.B1`…`B10` polylines (knots below) · `CRATE.Weighbridge` M (182, 0, 150) · `CRATE.MarketArch` M (184, 0, 247) · `CRATE.R4` M (288, 12, 330) · `CRATE.Basement` M (322, −4.2, 355) · `CRATE.LZ` M (338, −1, 389) |
| OOB (soft, each 4 m in front of a hard K PlayerClip) | `OOB.RidgeWest` T x 0–6, z 0–60 · `OOB.RidgeSouth` T x 0–85, z 0–6 · `OOB.GroveWest` T x 94–100, z 96–150 · `OOB.GroveSouth` T x 112–176, z 84–90 · `OOB.SaltPanNorth` T x 300–420, z 424–430 · `OOB.SaltPanEast` T x 388–394, z 380–424 · `OOB.RoofsNorth` T x 232–292, z 336–344, y 6–16 · `OOB.RoofsSouth` T x 246–292, z 296–304, y 6–16 · `OOB.MarketRoofs` T x 170–200, z 160–252, y 6–14 (outside the balcony closets) · one `OOB.Block.*` T in front of each blocker below |
| Gates and blockers (GDD §6.7, §7.13) | `Gate.Weighbridge` K (183, 0, 163) · `Gate.CinemaSideDoor` K (186, 0, 307) · `Gate.LadderL` K (236, 0, 301) · `Gate.R4Stair` K (291, 12, 330) · `Gate.HallDoor` K (338, 0.3, 330) · `Gate.ScreeningDoor` K (330, 4.8, 353.5) · `Gate.WestStairDoor` K (319, 0.3, 337) · `Gate.TunnelDoor` K (344, −3, 359.8) · `Gate.HeliDoor` K (350, 0.2, 404) · `Block.MarketSouth` K x 181–189, z 165 (Beat 4 start, with `Gate.Weighbridge` closing) · `Block.MarketArch` K x 181–189, z 250 and `Block.SideAlley` K x 174–180, z 246 (Beat 5 start) · `Block.R4Stair` K (291, 12, 330) (Beat 6 start) · `Block.Breach` K x 300, z 319–325 (Beat 7 start) · `Block.Postern` K (346, 0, 382) (Beat 10 start) |
| B1 | `CP1` (42, 22, 30) h45 · `B1.PlayerStart` M (42, 22, 30) · `B1.Squad.Start` N HOL (36, 22, 28) SPA (46, 22, 26) TIN (39, 22, 24) · `B1.POI1` G (378, 45, 362) · `B1.POI2` G (220, 18, 275) · `B1.POI3` G (183, 4, 162) · `B1.WadiHead` M (70, 18, 50) · `B1.T.Scree` T 4 m-wide box along (82, 13, 62)–(90, 8, 72), 3 m above the ground · `B1.T.Boulder` T centre (98, 4, 82) r 2.5 · `B1.T.WadiMid` T centre (88, 10, 70) r 6 · `B1.T.GroveEdge` T x 100–112, z 82–88 · `B1.GroveTarget` M (108, 0, 96) · `B1.T.GroveEnter` T x 100–120, z 88–104 |
| B2 | `CP2` (108, 0, 96) · `B2.FiringPos` M (136, 0, 112) + `B2.T.FiringPos` T centre r 3 · `B2.HollisMark` M (139, 0, 113) · `B2.Hold` M (130, 0, 108) · `B2.IrrigationWall` M x 140–170, z 109–110, 1.2 m (gaps at x 148 and 162) · `B2.NodeSet.A` N (152, 0, 108) / (158, 0, 108) / (164, 0, 108) · `B2.NodeSet.B` N (170, 0, 146) / (176, 0, 144) / (180, 0, 140) · `B2.T.GateFight` T x 100–200, z 136–137 · `B2.AlertCover` CoverNodes along z 140 · `B2.Gate` M (183, 0, 163) (chained gate = `Gate.Weighbridge`) · `B2.TruckTarget` M (190, 3, 150) · `B2.ArchTarget` M (183, 2, 166) · `B2.T.Exit` T x 176–191, z 168–170 · E2 spawn markers `E2-01`…`E2-06` M at GDD §7.2 positions · `B2.Perch.TruckBed` P (190, 2.2, 150) |
| B3 | `CP3` (185, 0, 170) · `CP3b` (185, 0, 216) · `B3.NodeSet.M1` N (182.5, 0, 180) / (188, 0, 184) / (182.5, 0, 186) · `.M2` (182.5, 0, 201) / (188, 0, 204) / (183, 0, 206) · `.M3` (182.5, 0, 224) / (188, 0, 228) / (183, 0, 232) · `.M4` (183, 0, 246) / (187, 0, 247) / (185, 0, 244) · `B3.T.Z185` T x 174–197, z 185–187, y −1–8 · `B3.T.Z200` T x 174–197, z 200–202, y −1–8 · `B3.T.Crossing` T x 174–197, z 216–218, y −1–8 · `B3.ArchTarget` M (185, 4, 252) · `B3.T.Exit` T centre (185, 0, 252) r 8 · `B3.Smoke` M (185, 0, 212) · `B3.Mantle` M (184, 0, 226) · `B3.AW01`…`AW05` D east-side balcony awnings, z 214–230 at +4.5 · `B3.AW06`…`AW08` D street-spanning awnings, z 230–240 · `B3.SunsetLane` M (181, 0, 238) h258 (lane axis to (120, 0, 225); keep clear of awnings and cables) · `B3.Fruit1`…`6` D (183, 0, 190) (187.5, 0, 198) (182.5, 0, 205) (187.5, 0, 222) (182.5, 0, 230) (187, 0, 240) · `SC_B3_BAL` C (190.5, 4.2, 222) · `SC_B3_ROOF` C (178.5, 8, 214) · `SC_B3_N` C (168, 0, 240) · `B3.Route.WaveB` R (168, 0, 240) → (168, 0, 199) → (175, 0, 197) → (182, 0, 197) · `B3.Perch.Balcony` P (190.5, 4.2, 222) · `B3.Perch.WestRoof` P (178.5, 8, 214) · `E3-01`…`E3-04` M |
| B4 | `CP4` (185, 0, 248) · `B4.NodeSet.Q1` N (183, 0, 250) / (187, 0, 250) / (185, 0, 247) · `.Q2` (205, 0, 265) / (216, 0, 270) / (198, 0, 262) · `B4.ArcadeGap` M (181, 0, 244) · `B4.SideDoor` M (186, 1.2, 307) · `B4.T.SideDoor` T x 186–189, z 305–309 (fires when the door is opened) · `B4.T.Lobby` T x 186–198, z 302–312, y 0–4 (sky swap) · `B4.T.Auditorium` T x 186–231, z 312–330, y 0–9 · `B4.T.Marquee` T x 200–226, z 299–301, y 4.5–7 · `B4.MarqueeDoor` M (214, 4.5, 301) · `B4.MarqueeDrop` M (227, 0, 298) (crate stack, top +2.2) · `B4.Technical` M (232, 1.6, 286) h234 · `B4.Ladder` M (236, 0, 301) · `B4.T.Ladder` T centre (236, 0, 301) r 10 · `SC_B4_E` C (252, 0, 276) · `B4.Route.North` R (252, 0, 276) → (246, 0, 290) → (240, 0, 292) · `B4.SuppressPos` M (242, 0, 262) · `E4-02`…`E4-06` M · `B4.FireProp` M (232, 1.0, 286) (truck bed) |
| B5 | `CP5` (236, 9, 304) · `B5.R4Parapet` M (291, 13, 328) · `B5.BreachPoint` M (300, 1.5, 322) · `B5.NodeSet.R1` N HOL (240, 9, 318) SPA (238, 9, 320) TIN (238, 9, 316) · `.R2` HOL (254, 8, 316) SPA (252, 8, 318) TIN (252, 8, 314) · `.R3` HOL (266, 10, 318) SPA (264, 10, 320) TIN (264, 10, 316) · `B5.NodeSet.MIDA` N HOL (289, 12, 326) SPA (289, 12, 330) TIN (286, 12, 328) · `B5.Plank12` M x 246–248, z 318 · `B5.TankStand` M (260, 8, 324), top +9 · `B5.Plank23` M x 261–263, z 324 · `B5.Stair34` M x 275–277, z 320 · `B5.WT1`…`WT4` CoverNodes (240, 9, 312) (254, 8, 328) (266, 10, 312) (284, 12, 330) · `B5.T.R4` T x 277–292, z 312–332, y 11–15 · `B5.T.Stair` T + `Gate.R4Stair` (291, 12, 330) r 1.5 · `B5.Perch.Mast` P (378, 25, 362) · `E5-02`, `E5-03` M · `B5.SunLook` M 500 m from R4 along az 258° at 1.5° (look target for `SetLookTarget`) · `B5.Ridge` M (sun disc occluder reference) |
| B6 | `CP6` (295, 0, 334) · `B6.T.Alley` T x 293–299, z 300–340 · `B6.StandClear` M (296, 0, 310) + `B6.T.StandClear` T centre r 2.5 · `B6.Charge` M (300, 1, 322) · `B6.BreachWall` D (x 300, z 319–325) · `B6.F1`…`F4` D + I (306, 8, 296) (386, 8, 296) (306, 8, 376) (386, 8, 376), aim (345, 0, 325) · `B6.NodeSet.Y1` N (303, 0, 318) / (304, 0, 326) / (302, 0, 322) · `.Y2` (330, 0, 314) / (334, 0, 320) / (326, 0, 310) · `.Y3` (334, 0, 328.5) / (337, 0, 328.5) / (341, 0, 328.5) · `B6.HallDoor` M (338, 1.2, 330) · `B6.T.Kick` T centre (338, 0.3, 328.5) r 1.8 · `SC_B6_GATE` C (346, 0, 298) · `B6.Route.SouthWall` R (346, 0, 298) → (330, 0, 296) → (312, 0, 300) → (306, 0, 310) · `B6.Perch.Gatehouse` P (346, 4, 294) · `E6-01`…`E6-05` M |
| B7 | `CP7` (338, 0, 327) · `B7.Ledgers` M (351, 1.1, 355) · `B7.O1` M (341, 1.2, 351) · `B7.Spiral` M (334.5, 0.3, 358) · `B7.HOL.A` M (320, 0.3, 333) → `B7.HOL.B` (330, 0.3, 342) · `B7.TIN.A` (344, 0.3, 333) → `B7.TIN.B` (340, 0.3, 348) · `B7.SPA.Office` (348, 0.3, 352) · `B7.T.OfficeDoor` T centre (346, 0.3, 350) r 6 · `B7.T.Landing` T x 330–340, z 350–362, y 4.8–8 (armed by the INTEL 1/3 gate) · `SC_B7_UP` C (334.5, 4.8, 358) · `E7-01`…`E7-03` M · `B7.Fill1`, `B7.Fill2` M (realtime fills over teletype rows 1 and 3) |
| B8 | `CP8` (335, 4.8, 355) · `B8.T.Enter` T x 318–330, z 350–360, y 4.8–8 · `B8.Projector` M (323, 5.6, 352) · `B8.CamReel` M (323, 6.45, 351.4) · `B8.CanRack` M (324, 5.6, 350.5) · `B8.O2` M (319.5, 5.8, 350.5) · `B8.WestStair` M (319, 0.3, 337) · `B8.NodeSet.Reel` N SPA (321, 4.8, 353.5) TIN (327, 4.8, 352.5) HOL (331, 4.8, 353.5) h90, flag `allowDoorway` · `B8.T.BasementFoot` T centre (320.5, −4.2, 343) r 1.5 (armed by the INTEL 2/3 gate) |
| B9 | `CP9` (320.5, −4.2, 343) · `B9.Safe` M (345.8, −3.4, 342) · `B9.NodeSet.Defend` N HOL (325, −4.2, 342) SPA (344.8, −4.2, 342) TIN (339, −4.2, 344) · `B9.R1`…`R4` D (z 356–358) · `B9.OnAir` M (332, −1.2, 359.6) · `B9.Speaker` M (331.5, −3.4, 357) · `B9.TunnelDoor` M (344, −3, 359.8) · `B9.Hatch` M (332, −4.2, 334.3) · `SC_B9_W` C (318, 0.3, 336) · `SC_B9_E` C (351, 0.3, 336) · `SC_B9_H` C (332, −6.0, 331) (cable trench) · `B9.EastStairFoot` M (343.5, −4.2, 338.5) · `B9.T.Tunnel` T x 342–345, z 361–364, y −4.2–0 · `B9.Postern` M (346, 0, 382) · `B9.Emergency1` M (320.5, −2.5, 343) · `B9.Emergency2` M (343.5, −2.5, 338.5) |
| B10 | `CP10` (346, 0, 383) · `B10.T.Postern` T x 343–349, z 383–387, y −2–3 · `B10.LZLine` M (340, −1, 390.5) · `B10.T.LZ` T x 326–352, z 386–398, y −2–3 · `B10.Pad` M (350, −1, 406) · `B10.NodeSet.P1` N (332, −1, 390.5) / (340, −1, 390.5) / (346, −1, 390.5) · `B10.GreenSmoke` M (350, −1, 404) · `B10.Truck1` spline (430, −1, 395) → (392, −1, 396) · `B10.Truck2` spline (362, −1, 470) → (362, −1, 424) · `B10.FL1` M (346, 70, 404) · `B10.FL2` M (356, 70, 410) · `B10.FL3` M (340, 70, 400) (wave-off only) · `SC_B10_W` C (300, −1, 388) · `B10.Route.West` R (300, −1, 388) → (310, −1, 391) → (318, −1, 394) · `B10.HeliApproach` spline (900, 120, 450) → (600, 60, 420) → (420, 20, 408) → (350, −1, 406), the last 15 s from (600, 60, 420) · `B10.HeliWaveOff` spline (420, 20, 408) → (400, 45, 470) → (520, 80, 520) → (640, 70, 440) → rejoins at (600, 60, 420), 20 s · `B10.HeliDoor` M (350, 0.2, 404) |
| B11 | `B11.Exfil` spline N0 (350, 0, 406) · N1 (346, 30, 372) · N2 (300, 35, 330) · N3 (225, 40, 280) · N4 (186, 35, 215) · N5 (120, 55, 120) · N6 (40, 80, 40), segment times 8/8/10/10/8/6 s, auto (Catmull-Rom) tangents, cabin yaw = tangent · `B11.T1` M (346, 6, 294) window t 8–16 · `B11.T2` M (285, 9, 268) t 15–23 · `B11.T3` M (224, 10, 226) hidden until t 28, fires the RPG at t 30 · `B11.T4` M (236, 9, 238) t 26–33 · `B11.TruckSquare` spline (300, 0, 276) → (246, 0, 276), start t 16, 4.5 m/s, 2 trucks 12 m apart · `B11.FireProp` M (technical copy in `S8_Heli`) |
| Booth (local) | `BOOTH.C_POV` (0, 1.18, 0.05) · `BOOTH.C_CLOCK` (0.9, 2.33, 2.20) · `BOOTH.CRT1`…`CRT6` (GDD §8.0) · `BOOTH.Mirror` · `BOOTH.Clock` (0.9, 2.35, 2.52) · `BOOTH.Tube` (0, 2.95, 0.6) · `BOOTH.Scrambler` (−0.75, 0.92, 0.95) · `BOOTH.Reel` (−0.78, 0.92, 1.30) · `BOOTH.FeedDeck` (−0.55, 1.20, 1.05) looking at `BOOTH.Reel` · `BOOTH.Phone` (0.95, 0.88, 1.20) · `BOOTH.Table` (−0.8, 0, −0.7) · `BOOTH.Pen` (−0.7, 0.76, −0.65) · `BOOTH.Smoke` (−0.6, 1.7, −0.6) · `BOOTH.FilmCam` (−1.6, 1.9, 2.2) |
| Heli (local) | `HELI.DoorGunPivot` (−1.05, 1.10, 0.30) · `HELI.PlayerEye` (−0.85, 1.25, 0.30) · `HELI.Hollis` (0.10, 1.55, −0.60) · `HELI.Sparrow` (0.60, 1.00, 0.90) · `HELI.Tinker` (1.05, 1.10, 0.30) · `HELI.RedLamp` (0, 1.90, 0) · `HELI.SkidCam` (−1.4, −0.4, 1.2) looking forward and up at the left door (`VID_GunCam`) |

**Critical-path knots** (`ICriticalPath.Progress01` = normalised arc length of the player's projection onto the beat's polyline, never decreasing):

| Beat | Knots (x, y, z) |
|---|---|
| B1 | (42, 22, 30) (70, 18, 50) (82, 13, 62) (90, 8, 72) (98, 4, 82) (108, 0, 96) |
| B2 | (108, 0, 96) (136, 0, 112) (158, 0, 106) (172, 0, 146) (183, 0, 163) (185, 0, 170) |
| B3 | (185, 0, 170) (185, 0, 200) (185, 0, 216) (185, 0, 246) (185, 0, 252) |
| B4 | (185, 0, 248) (181, 0, 244) (177, 0, 270) (177, 0, 300) (186, 0, 307) (192, 0, 307) (210, 0, 320) (226, 0, 314) (226, 4.5, 315) (214, 4.5, 306) (214, 4.5, 301) (227, 4.5, 300) (227, 0, 297) (236, 0, 301) |
| B5 | (236, 9, 304) (240, 9, 318) (247, 8.5, 318) (254, 8, 316) (260, 9, 324) (266, 10, 320) (276, 11, 320) (284, 12, 322) (291, 12, 328) |
| B6 | (291, 12, 330) (295, 0, 334) (296, 0, 310) (302, 0, 322) (316, 0, 316) (330, 0, 314) (338, 0, 328.5) |
| B7 | (338, 0.3, 331) (330, 0.3, 342) (346, 0.3, 350) (351, 0.3, 355) (334.5, 0.3, 358) (334.5, 4.8, 358) (335, 4.8, 355) |
| B8 | (335, 4.8, 355) (323, 4.8, 352) (334.5, 4.8, 358) (334.5, 0.3, 358) (322, 0.3, 340) (319, 0.3, 337) (320.5, −4.2, 343) |
| B9 | (320.5, −4.2, 343) (332, −4.2, 348) (344, −4.2, 358) (344, −4.2, 362) (344, −4.2, 380) (346, 0, 382) |
| B10 | (346, 0, 383) (346, −1, 391) (340, −1, 390.5) (350, −1, 404) |
| B11 | progress = exfil spline time / 50 s |

## Appendix B — Bark and PA subtitle translations (mirror of `DIALOGUE.json` `subtitle`, which is authoritative; GDD §14.4)

Used when "Translate enemy chatter" is on (barks, within 25 m). `D012` (Harrou's Sardani PA line) is always subtitled: "People of Zaroud, stay inside. The curfew is for your protection."

| Line | Subtitle | Line | Subtitle |
|---|---|---|---|
| BK_LEG_01 | (shouting) There! Over there! | BK_LEG_09 | (shouting) Fall back! |
| BK_LEG_02 | (shouting) Push! Push! | BK_LEG_10 | (shouting) For Zaroud! |
| BK_LEG_03 | (shouting) Go around! Flank them! | BK_LEG_11 | (shouting) Americans! The Americans! |
| BK_LEG_04 | (shouting) Grenade! | BK_LEG_12 | (shouting) Hurry! Hurry! |
| BK_LEG_05 | (shouting) Reloading! | BK_LEG_13 | (pain) |
| BK_LEG_06 | (shouting) Cover me! | BK_LEG_14 | Got a light, friend? |
| BK_LEG_07 | (shouting) Man down! | BK_LEG_15 | Sure, sure. |
| BK_LEG_08 | (shouting) Lights! Lights! | | |
| BK_GRY_01 | (radio) Contact! | BK_GRY_06 | (radio) Clear. |
| BK_GRY_02 | (radio) Suppress, move! | BK_GRY_07 | (radio) Eyes on. |
| BK_GRY_03 | (radio) Hold! Second team, hold! | BK_GRY_08 | (radio) Fall back! |
| BK_GRY_04 | (radio) Grenade! | BK_GRY_09 | (radio) It is time. |
| BK_GRY_05 | (radio) Marksman in position. | | |

---

## Review log (completeness and feasibility review, 2026-10-16)

Scope: `GDD.md`, `DIALOGUE.json`, `TECH_PLAN.md`, `ASSET_MANIFEST.json` and `research/*.md`, read in full. Both JSON files were re-validated after editing (157 dialogue entries, 636 manifest entries, unique ids, identical key sets). Every line id referenced in the GDD exists in `DIALOGUE.json`, and every Appendix A coordinate matches a GDD coordinate.

### Contradictions fixed
1. **Beat 11 targets were on the wrong side of the helicopter.** With the cabin yaw following the N0–N6 spline, the left door faces south-east, but T1 (R4 roof), T2 (R2 roof), T3 (cinema roof) and T4 (market roof) were all 90°–160° off the left-door normal, behind or to the right (checked numerically with a Catmull-Rom spline). New positions T1 gatehouse roof, T2 and T4 housing roofs east and south-east of the square, T3 housing roof south-east of the square, each inside the arc for its whole window; trucks timed from t 16; door-gun depression −50° → −60°. GDD §7.11, §8.7, §13.3; DIALOGUE `D078`, `D081`; Appendix A B11.
2. **MID-A timeline did not fit its own lines.** `D039` is 15 spliced tokens on a 0.9 s grid (13.5 s) plus a 6 s interval signal, but the table gave it 10 s; `D037` ("Sun's down") was placed at 0:16 while its trigger was sun 0°, reached at 0:20. Re-timed to ≈ 47 s with every event and line on one clock; Beat 5 1:00 → 1:20; total play ≈ 13:55; LightStage keyed explicitly through MID-A. GDD §1, §7.0, §8.5; DIALOGUE `D037`–`D042`.
3. Intro and outro line triggers disagreed with the shot tables (`D005` fired at ≈ 1:03 instead of 1:08, `D006` at 1:09 instead of 1:19, `D085` at ≈ 0:11 instead of 0:15, `D087` at ≈ 0:35 instead of 0:40, `D092` at 1:36 vs 1:38). Triggers now point at shot times; outro shots 10–11 re-timed around the 6.0 s interval signal. Intro + title card is 1:43 (the card overlaps shot 9), not 1:51.
4. Bark subtitles differed between `DIALOGUE.json` contexts and Appendix B (`BK_LEG_01/03/11/12`, `BK_GRY_05`). Added a `subtitle` field to `D012` and all 24 barks as the single source; Appendix B now mirrors it; GDD §19 documents the field.
5. GDD vs TECH_PLAN drift: `S_Persistent` "main .data" (D1), glTFast and Cinemachine in GDD §18.1 (D2, D3), the `Lit_ARM` Shader Graph (D4), "Sun: Mixed" (D6), AudioMixer volumes in Options (D9), the Harrou 8 mm clip as a live render (D14), Meshy clip ids that disagree with the manifest. GDD updated to the decisions.
6. Audio import folders did not match manifest chain names (`GREYCOAT` vs `GREYCOAT_SQUELCH`, Legion `A_DRY`/`B_FAR` folders, `TOKENS`, missing `Synth` and `Weapons/Far`). §3.12 and the manifest now agree.
7. Manifest self-contradictions: `MUS_01_SESSION` used itself as `audio_reference`; `AMB_CASSETTE_TUNE` said "no reference" and listed one; Mirelo negative prompts contained "whoosh"/"music"/"voices" for sounds that ask for exactly that (`SFX_RPG_LAUNCH`, `SFX_RPG_FLYBY`, `SFX_KNIFE_SWING`, `SFX_RADIO_TURNED_DOWN`, `SFX_TV_MUFFLED`); Clockmaker lines were listed as TTS calls with "3 takes" although they are spliced. All fixed.
8. Smaller ones: shadow distance "60–100 m" vs GDD 80/60 m; WP09 referenced non-existent `BOOTH_*` ids; marksman sight range (stage 60 m) vs his 130 m engagement; marksman still firing during the forced-walk MID-A; E2-06's RPK vs "first RPK in Beat 3"; the "6 hero materials" count; "S8_Heli loaded in Beat 8" while MID-B (a Timeline) runs there; Beat 6 clock 18:45 vs the story clock.

### Infeasible or physically wrong, replaced
9. **LONG SHADOW image impossible as written.** At 1.5° sun, a 70 m player shadow "across the station yard" cannot come from a +7 m roof (the shadow leaves the roof and lands ≈ 270 m away), and R4 at +7 sat in the shadow of R3 (+10) and the 12 m cinema, so R4 had neither sun nor a view of the disc. R4 is now the highest roof (+12) with no west parapet; the image is the player's and squad's shadows running the width of R4; the ridge silhouette crest is set so the disc vanishes exactly at 0°. GDD §6.5 A9/A10, §7.5, §8.5, BLD-08; Appendix A B5.
10. **The player had no shadow** (view model never casts). Added D19: arms-only FP body plus a full-body shadows-only proxy.
11. Beat 3 lighting promised sun bands on the street floor and sun shafts through torn awnings at 4.5°–6° sun, which the west-side houses make impossible. Added Sunset Lane (a lane aligned with the sun azimuth) for one real band, re-placed awnings AW-01–08 where sun actually reaches, and a "no fake sun where the shadow map says shade" rule.
12. A GOLDEN-only far proxy would show sunlit town roofs from R4 at 1.5°; and hiding proxies whenever a section is loaded would lose baked shadows on distant loaded buildings. D7 now has three proxy lightmap sets and distance-based proxy switching.
13. Exponential-squared fog flattens anything at 2.5 km to fog colour, which would erase the ridge silhouette and the sun disc. Backdrop shader without built-in fog; sun disc unfogged at 2.9 km.
14. **Size budget under-counted by ≈ 40 MB** against the manifest (props, weapons, characters, meshes, lightmaps). Re-derived §8.9/§8.10 with per-class caps (props 512 except 13 heroes, enemies 512, world weapons share FP textures, 4 hero env sets instead of 6), P0-only baseline 220 MB, P1/P2 only within headroom; manifest budgets updated.
15. **Lightmap math** (D16): 2048² RGBM is ≈ 4.2 MB LZ4, not ≈ 3 MB; the GDD's 16 × 2048² plus proxies was ≈ 75 MB. Budget now 27 × 1024²-equivalents with per-section caps (GDD §9.4) and 128 px reflection probes.
16. The post-build validator would have failed every build: it rejected all cubemaps, but baked reflection probes are cubemaps. Rule narrowed (HDR formats and > 256 px probes fail); HDR Cubemap Encoding = Normal Quality added to §3.1.
17. **Hosting:** the artifact host keeps files a publish omits, and hashed build/bundle names change per build, so repeated publishes would pile up toward the 511-file/256 MB cap. D22: `publish_plan.py` removes stale paths with `null` entries.
18. Main-thread Crunch/LZ4 stalls: loads moved to named quiet points (D20, GDD §6.7 table), click gate enabled after ≈ 55 MB instead of ≈ 85 MB, intro holds at shot 8 if late; trigger holds while a section is loading.
19. 60 fps "at 1080p with DPR 1.5" is really ≈ 3.3 MP on a Retina laptop; target restated at ≤ 2.1 MP with an automatic default render scale (D21).
20. A 160° shadowed flare spot wastes its shadow map; now 120° (covers the pitch from 70 m), FL3 added for the wave-off gap.
21. NavMesh surfaces in separate additive scenes do not connect; seam links listed in §7.15.
22. Generated "town" voices and music (muffled TV, radio, distant shouting) could produce a real language or religious song; replaced with procedural beds built from our own takes.
23. Clockmaker lines cannot be generated whole and stay on the 0.9 s grid; now `procedural` splices from the token takes, which stay outside `Assets/`.
24. Character plan relied on GitHub downloads (not an approved source) for every human except Calder (Male_Adult_05 has no textures on disk). The O2 fallback is now concrete per role with its visible costs; the booth no longer depends on extra Rocketbox clips (`ANIM_ROCKETBOX_BOOTH` → P1).

### Underspecified, now specified
25. Objective master list with ids, texts, marker kinds and completion rules (GDD §7.12); flavour-group rule; optional-intel marker rule.
26. Progression gates (GDD §7.13) and backtrack blockers (GDD §6.7) so no beat can be skipped or soft-locked (ledgers before upstairs, film before the basement, racks invulnerable until `D065`, tunnel door until `D066`, ladder until the square is clear, R4 stair until MID-A ends).
27. Appendix A: coordinates or extents for every trigger that had none (`B1.T.GroveEdge`, `B3.T.Z185/Z200/Crossing`, `B4.T.SideDoor/Lobby/Auditorium/Marquee`, `B8.T.Enter`, `B9.T.Tunnel`, `B10.T.Postern/LZ`, `SC_B9_H`, flank routes, fruit crates, OOB volumes, gates, blockers), per-member Beat 5 node offsets, heli approach and wave-off splines, critical-path knots per beat, new booth/heli feed cameras.
28. LightStage keyframe table (sun elevation, intensity, colour, shadows, sky blend/tint/EV, fog, story clock) and the progress formula (GDD §9.1, §9.2a); `ILightStage.Override` for the cinema swap and MID-A.
29. Beat rules a builder would have guessed: Beat 2 "clean" definition and bolt-cut ordering, MID-A trigger when the nest is alive, technical always ignites, RPG wave-off at most once, drill wave timers are wall-clock, weapon drop/pickup and slot rules, limb multipliers, crate use, air mantle, roof links (planks, tank stand, steel stair), the marquee descent, side-alley service yard, cinema upper foyer, irrigation-wall position, PA rolloff and subtitles, played-line snapshot rule and the objective-critical line list, glitch-insert lengths in seconds.
30. Core contract additions (additive, before WP00 builds them): `MarkerKind.DiamondHollow/TargetX`, `ISquadService.SetLookTarget`, `ILightStage.Override` and `StoryClockMinutes`, `IHud.Banner`, `IScreenFX.Glare`.
31. Ownership gaps: `markup_ids.csv`, `SignBaker`, `vfx_flipbook.py`, `clockmaker_splice.py`, MID-A orchestration, `S8_Heli` composition, `RawAssets/<wp>/<id>/` layout; DoDs extended for WP02, WP05, WP06, WP07, WP09, WP10 and the slice.
32. Missing assets added: `IMG_SURVEIL_SPARROW` (outro shot 9 composite), `PROC_BOOTH_SMALLS` (pen, electrodes, cigarette); Calder variants (FP arms, shadow proxy, booth, reel head). Priorities raised to P0 where a P0 feature needs them (`TEX_VFX_GLINT`, `SFX_WPN_SVD_SHOT`, `SFX_EXP_FIREBALL`, `SFX_IMPACT_FRUIT`, `SFX_FRUIT_SPILL`, `SFX_DOOR_CREAK`); fallbacks noted for `WPN_SVD` and `WPN_KNIFE`.

### Content rules
33. Every town/crowd image prompt now also excludes domes, minarets and headscarves; the Harrou still asks for varied ages and builds; the "desert caravan" poster (an orientalist cliché) became an expedition truck on salt flats; the map take is rejected if its borders resemble a real country.
34. Every generated ambience prompt says "no voices, no singing, no music, no bells", and the listen gate (O4, §8.7) now rejects chanting, a call to prayer, bells, hymn-like or regional song, intelligible real-language words in barks, and caricatured accents.
35. Meshy fallback references must vary age, build and complexion across both sides, keep enemy faces mostly covered, and never use keffiyehs or religious dress.
36. No other rule problems found: every country, town, faction and company is invented, the bloc is never named, the locals are never enemies and never on screen, and there is no religious architecture, audio or imagery in the spec. The invented-word web check (GDD §2.3-5) is still a required step before recording.

### Still open (user decisions; see §12.1)
O1 fonts from Google Fonts, **O2 Rocketbox/SAC downloads from GitHub (now blocking WP05)**, O3 human sign-off on glyphs, barks and decals, O4 the human listening pass, O5 each publish, O6 fal spend, O7 a local git repository, O8 optional Blender.
