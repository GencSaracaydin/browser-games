# WebGL build and hosting feasibility (Unity 6000.6.6f1, URP 17.6)

Owner: WebGL/hosting agent. Date: 2026-10-16. Machine: macOS arm64 (Apple M3), 8 CPU, 16 GB.

Build folder (main test build): `/Users/gencsaracaydin/Documents/fun/fun-unity/Builds/WebGLTest`
Variant builds: `/Users/gencsaracaydin/Documents/fun/fun-unity/Builds/WebGLTest_WebGPU`, `/Users/gencsaracaydin/Documents/fun/fun-unity/Builds/WebGLTest_SizeRecipe`
Bundle size experiments: `/Users/gencsaracaydin/Documents/fun/fun-unity/Builds/BundleTest/`
Build reports: `/Users/gencsaracaydin/Documents/fun/fun-unity/Builds/WebGLTest_report.txt` (latest), `..._report_build1_baseline.txt` (cold build, includes the packed-asset breakdown), `WebGLTest_WebGPU_report.txt`, `WebGLTest_SizeRecipe_report.txt`, `WebGLTest_build1_files.txt`.

Nothing was published anywhere. The local servers were stopped after testing. WebGL is still the active build target.

---

## 1. Verdict

**Feasible.** A URP Forward+ build with the full realism post stack (ACES, bloom, grading, vignette, film grain, chromatic aberration, DoF, motion blur, SSAO, 4-cascade soft shadows, SMAA) builds and runs in the browser with the Web defaults. The engine-only build is **11.7 MB**, and its largest file is **6.97 MB**. The 15 MB-per-file limit holds if we follow these rules:

1. Keep the build **compressed**: Brotli plus **Decompression Fallback**. The raw wasm is 24 to 35 MB, so an uncompressed build cannot meet the limit.
2. Keep the main `.data` small: boot and menu scene, shaders, core UI. Target 12 MB compressed or less.
3. Ship all level content as **LZ4 AssetBundles/Addressables of 12 MB or less each**. Load them from relative URLs (`StreamingAssets/...`), and give the files a `.bundle` extension. Bundle compression lives inside the file, so the host doesn't need any headers. **Crunch is mandatory** for textures (section 7).
4. **Progressive Asset Loading cannot be used**, because Unity disables it when Decompression Fallback is on.

The largest open risk is outside Unity. We don't yet know how a claude.ai artifact page behaves inside its frame: pointer lock, fullscreen, autoplay, and IndexedDB/Safari. It needs one private smoke-test publish of this build, and that needs the user's go-ahead (section 10).

---

## 2. What was done in the project (exact changes)

| Item | Before | After |
|---|---|---|
| Active build target | StandaloneOSX | **WebGL** (switch took 22.8 s) |
| `PlayerSettings.WebGL.compressionFormat` | Brotli | Brotli (set explicitly, unchanged) |
| `PlayerSettings.WebGL.decompressionFallback` | false | **true** |
| `PlayerSettings.WebGL.dataCaching` | true | true (set explicitly) |
| `PlayerSettings.WebGL.nameFilesAsHashes` | false | false (set explicitly) |
| `PlayerSettings.WebGL.showDiagnostics` | false | false (set explicitly) |
| `PlayerSettings.WebGL.powerPreference` | HighPerformance | HighPerformance (set explicitly) |
| Quality level "PC" (PC_RPAsset/PC_Renderer) | excluded for WebGL | **included for WebGL** |
| Quality level "Mobile" (Mobile_RPAsset) | WebGL's level | **excluded for WebGL** |
| `QualitySettings m_PerPlatformDefaultQuality.WebGL` | 0 (Mobile) | **1 (PC)** |
| Graphics APIs (WebGL) | Auto (=WebGL2/OpenGLES3) | Auto. The WebGPU variant build set `[WebGPU, OpenGLES3]` temporarily and restored Auto afterwards. |
| IL2CPP code gen / managed stripping / splash | OptimizeSpeed / Minimal / on | Same. The SizeRecipe build changed them temporarily and restored them. ProjectSettings.asset now stores explicit `WebGL: 0` / `WebGL: 4` entries, which hold the same values. |
| EditorBuildSettings scenes | SampleScene only | Unchanged. The test scene went in through `BuildPlayerOptions.scenes`. |

The template excluded the PC level from WebGL, so web builds silently used the Mobile renderer, which has no SSAO and weaker shadows. This is now fixed.

Effective WebGL settings that I left at their defaults: exceptionSupport = ExplicitlyThrownExceptionsOnly, linkerTarget = Wasm, wasm2023 = true, threadsSupport = false, initialMemorySize = 32, maximumMemorySize = 2048, memoryGrowthMode = Geometric (0.2 step, 96 MB cap), debugSymbolMode = Off, template = APPLICATION:Default, IL2CPP Release, API level .NET Standard, stripEngineCode = true, Linear color space, texture compression default = DXT (`m_Formats 05000000` for WebGL), `webGLBuildSubtarget` = Generic (uses the Player setting), Run In Background = false, Active Input Handling = Input System only.

Files created:
- `Assets/Scenes/WebGLTest.unity`: camera (SMAA High, post on), soft-shadow sun, primitives, global Volume, a `BundleLoader` object. Also `WebGLTest_Sand.mat`, `WebGLTest_Metal.mat`, `WebGLTest_Volume.asset` (ACES, Bloom, ColorAdjustments, Vignette, FilmGrain, ChromaticAberration, Gaussian DoF, MotionBlur).
- `Assets/Scripts/WebGLTest/WebGLTestRotator.cs`: rotates a cube, logs once, and does click-to-lock plus mouse-delta look with the Input System.
- `Assets/Scripts/WebGLTest/WebGLBundleLoader.cs`: loads `StreamingAssets/bundles/*.bundle` at runtime, applies a PBR material, and plays audio after a click.
- `Assets/Editor/WebGLTestBuild.cs`: menu **Tools/Web/Build WebGL Test** and `WebGLTestBuild.Build(path)`. It applies settings, builds only the test scene, and writes `<path>_report.txt` with the file list, top packed assets, size by type and warnings.
- `Assets/Editor/WebGLBundleTest.cs`: configures DXT or Crunch imports for the test content and builds two bundles.
- `Assets/WebGLTest/BundleContent/`: Poly Haven `beige_wall_001` 2k (CC0), with diffuse, normal (GL) and a packed mask (R = metal, G = AO, A = smoothness), plus `BeigeWall_Lit.mat` and a generated 10 s stereo WAV. Originals are in `RawAssets/webgl/`.

---

## 3. Measured results

### 3.1 Build times (Apple M3, 8 cores)

| Build | Wall clock |
|---|---|
| First (cold) WebGL build, WebGL2, Brotli + fallback | **278.7 s (4 min 39 s)** |
| WebGPU + WebGL2 variant (shader recompile) | 252.4 s |
| Incremental rebuild after a C# change | 121.5 to 174.2 s |
| SizeRecipe (OptimizeSize + High stripping) | 105.7 s |
| AssetBundle builds (2 bundles) | 1 to 11 s; a Crunch reimport of three 2k textures took 11.4 s |

The slowest steps in the cold build were Brotli on the wasm (60 s), Brotli on the data (25 s), the emscripten link (42 s), and one large IL2CPP C++ object (32 s). Development builds are **never compressed**, so a dev build's raw wasm (more than 35 MB) cannot be hosted. Only release builds can be published.

### 3.2 Output files (final `/Users/gencsaracaydin/Documents/fun/fun-unity/Builds/WebGLTest`)

| File | Bytes | Raw (decompressed) |
|---|---:|---:|
| `Build/WebGLTest.wasm.unityweb` | **6,966,155** | 34,976,515 |
| `Build/WebGLTest.data.unityweb` | **4,602,725** | 12,378,206 |
| `Build/WebGLTest.loader.js` | 118,806 | (plain JS, includes the JS Brotli decoder) |
| `Build/WebGLTest.framework.js.unityweb` | 67,185 | ~331 KB |
| `index.html` | 6,412 | |
| `TemplateData/*` (11 files: css, ico, 9 png) | 13,914 total | |
| `StreamingAssets/bundles/wall_material.bundle` | 3,486,714 | (LZ4, Crunch, 2k PBR set) |
| `StreamingAssets/bundles/audio_test.bundle` | 136,001 | (LZ4, 10 s stereo AAC) |
| `StreamingAssets/bundles/{bundles, *.manifest}` | 1,641 + 1,548 | (build manifests, not needed at runtime) |
| `timing.html` (my test page with a console hook) | 6,885 | |

The output folder holds 24 files and 16 MB in total. The build alone is 11,775,397 bytes (`BuildReport.summary.totalSize`). The cold baseline build (before the bundle loader script) was wasm 6,905,828, data 4,600,959, framework 65,955, total 11,712,074.

What is inside the `.data` (the `UnityWebData1.0` container, parsed):

| Entry | Raw | Brotli share (approx.) |
|---|---:|---:|
| `Il2CppData/Metadata/global-metadata.dat` | 7,694,034 | ~2.26 MB |
| `data.unity3d` (all scene and asset data) | 3,044,502 | ~2.65 MB |
| `Resources/unity default resources` | 1,631,680 | ~0.15 MB |
| RuntimeInitializeOnLoads.json, ScriptingAssemblies.json, boot.config | ~7.7 KB | small |

`data.unity3d` is already compressed internally: about 7.5 MB of packed assets became 3.0 MB, and Brotli only removes another ~13% on top. Asset data in the main `.data` therefore costs roughly its **LZ4 size × 0.87**, not its raw size × 0.6.

Assets packed in the engine-only scene (uncompressed): Texture2D 6.18 MB, of which the **splash logo is 2.80 MB** and **11 URP film grain textures are 2.88 MB** (the default PostProcessData pulls in all of them). Shaders are 0.88 MB, with Lit at 183 KB. There is also a 0.52 MB built-in cubemap and 0.15 MB of MonoScripts.

### 3.3 Variants

| Variant | wasm (br) | data (br) | wasm raw | data raw | Notes |
|---|---:|---:|---:|---:|---|
| Baseline WebGL2 (OptimizeSpeed, Minimal stripping) | 6.97 MB | 4.60 MB | 35.0 MB | 12.4 MB | |
| **WebGPU + WebGL2** | 7.24 MB | 5.49 MB | 36.7 MB | 15.1 MB | Shaders 0.88 to **1.68 MB**, compute 67 to 240 KB, default resources 1.6 to 3.0 MB. AssetBundles would also carry both shader sets. |
| **SizeRecipe** (IL2CPP OptimizeSize + Managed Stripping High) | **5.75 MB** (-17.5%) | **3.75 MB** (-18.5%) | 23.8 MB | 9.3 MB | global-metadata 7.7 to 4.6 MB raw. It ran correctly, bundles included. `SplashScreen.show = false` did not remove the logo texture from the data. |

### 3.4 Loader structure (what the page fetches)

`index.html` defines `config = { dataUrl: "Build/WebGLTest.data.unityweb", frameworkUrl: "Build/WebGLTest.framework.js.unityweb", codeUrl: "Build/WebGLTest.wasm.unityweb", streamingAssetsUrl: "StreamingAssets", companyName, productName, productVersion, fullscreenElementID: "unity-fullscreen-container", showBanner }`. It then injects `Build/WebGLTest.loader.js`, which calls `createUnityInstance(canvas, config, onProgress)`.

- **framework.js.unityweb**: fetched, decompressed in JS, then run from a Blob URL. Its served content type is irrelevant.
- **wasm.unityweb**: fetched, decompressed in JS, then `WebAssembly.instantiate(ArrayBuffer)`. With the fallback there is **no streaming compile**, so `application/wasm` is not needed.
- **data.unityweb**: fetched, decompressed, and unpacked into the Emscripten in-memory FS. It is cached in **IndexedDB "UnityCache"** with `must-revalidate`.
- The loader detects compression by the **in-file marker** `UnityWeb Compressed Content (brotli)`, not by headers. Files are named `.unityweb` whenever Decompression Fallback is on.
- Default `cacheControl(url)`: `must-revalidate` for `dataUrl` and for any URL matching `/\.bundle/` (so `x.bundle` and `x.bundle.bin` are both cached in IndexedDB). Everything else gets `no-store`.
- The runtime `UnityWebRequest` fetches `StreamingAssets/...` relative to the page.
- The loader needs WebAssembly 2023: Chrome 91 or later, Firefox 89 or later, **Safari 16.4 or later**.
- The page also loads `TemplateData/style.css`, the favicon and the logo PNGs (from the default template).

Expected content types: `.unityweb`/`.bundle` → `application/octet-stream` (python served `.unityweb` as `application/vnd.unity`), `.js` → `text/javascript`, `.html`/`.css`/`.png`/`.ico` standard. **No `Content-Encoding` header is needed or wanted.**

### 3.5 Local serving (python3 -m http.server, 127.0.0.1)

Every one of the 24 files returned HTTP 200 with **no `Content-Encoding`** and was byte-identical to the file on disk. Served types: `text/html`, `text/javascript`, `application/vnd.unity` (.unityweb), `application/octet-stream` (.bundle), `text/cache-manifest` (.manifest), `text/css`, `image/png`, `image/x-icon`. The only console notices were the expected "You can reduce startup time if you configure your web server to add Content-Encoding: br" lines.

### 3.6 Runtime verification (Claude browser pane = Chromium 152, macOS, Apple M3 via ANGLE Metal)

- **WebGL2**: "Creating WebGL 2.0 context", quality level **PC**, gfx OpenGLES3. The scene rendered with soft cascaded shadows, vignette and grain. No shader errors. The only warnings were that `Hidden/.../PaniniProjection` and `Edge Adaptive Spatial Upsampling` (FSR) are unsupported or stripped on WebGL2. Neither is used.
- **WebGPU build**: "Unity WebGPU: Version WebGPU 1.0, Renderer: WebGPU Device", and the image was identical. `navigator.gpu` and `isSecureContext` were both true on `http://127.0.0.1`.
- Extensions the GPU exposes: `WEBGL_compressed_texture_s3tc(+_srgb)`, `EXT_texture_compression_bptc`, `_rgtc`, `WEBGL_compressed_texture_astc`, `_etc`, `EXT_color_buffer_float`, `EXT_float_blend`, `KHR_parallel_shader_compile`, `WEBGL_multi_draw`. **`OES_draw_buffers_indexed` was not listed**, which matters for DBuffer decals.
- **Startup**: a fresh load over localhost reached `createUnityInstance` resolved at **~1.05 s** (0.77 s for SizeRecipe), including JS Brotli decompression of about 47 MB raw. One run on the IndexedDB-revalidate path in a background tab took 4.5 s.
- **AssetBundles over plain HTTP**: `wall_material.bundle` (Crunch 2k PBR) loaded in 0.24 s and rendered correctly. The bundle carried its own copy of URP Lit. Both bundles were stored in, and later revalidated from, the IndexedDB cache.
- **Audio from a bundle**: when the bundle was unloaded with `Unload(false)` before first playback, playing the `CompressedInMemory` clip failed with **"FMOD error: File not found"**. Calling `clip.LoadAudioData()`, waiting for `loadState == Loaded`, then `Unload(false)` **plays fine**. Keeping the bundle loaded also works. The AudioContext resumed automatically on the first click.
- **Input System on Web**: `Mouse.current.delta` produced values (154, -52) for a 60 CSS-px move. That is about **DPR (2) × page zoom**, so the delta is in device pixels. The automation pane never granted a real pointer lock, so verify this in real Chrome and Safari.
- **Pointer-lock failure is loud**: in a background tab, `requestPointerLock` rejected with `WrongDocumentError`. The default loader treats the unhandled rejection as fatal ("Invoking error handler"), and the default template calls `alert(...)`. The game kept running, but a real user would see an error dialog. The custom template needs an `errorHandler` and a guarded `requestPointerLock` (section 6.5).

### 3.7 Measured content sizes (the basis for estimates)

One 2k PBR set (BC1 albedo + BC3/DXT5nm normal + BC3 mask), built as a WebGL AssetBundle:

| Encoding | Uncompressed bundle | **LZ4 bundle (download)** | Brotli of raw (for reference) |
|---|---:|---:|---:|
| DXT (no crunch) | 14,451,456 | **10,946,758** | 8,863,970 |
| Crunch q50 | 3,671,168 | **3,486,714** | 3,335,435 |

- VRAM per set is the same either way, about 14 MB at 2k and 3.5 MB at 1k (with mips).
- A 1k set costs about a quarter of these sizes: DXT LZ4 ~2.7 MB, Crunch ~0.9 MB.
- In the main `.data` (internally LZ4, then Brotli ×0.87): DXT 2k ≈ 9.5 MB, Crunch 2k ≈ 3.0 MB.
- Audio: a 10 s stereo WAV is encoded as **AAC at about 14.5 KB/s (~116 kbps)**, which made a 144,784-byte bundle. LZ4 and Brotli only save about 7% (136,001 / 134,062).

---

## 4. Platform research summary (Unity 6.6 Web)

### 4.1 WebGL2 vs WebGPU

- In **Unity 6.6 (6000.6), WebGPU is out of experimental and fully supported**, but **WebGL2 remains the default** ("Auto Graphics API").
- To get WebGPU, turn off Auto and list `WebGPU, WebGL2`. The runtime falls back in list order. WebGPU needs a secure context: https works, and so does 127.0.0.1.
- 6.6 adds a **WebGPU Device Filter** asset (allow and deny lists, required features and limits) to force the WebGL2 fallback on bad browsers or GPUs.
- Browser support:
  - WebGPU is on by default in Chrome/Edge 113 and later on desktop, and in **Safari 26** (macOS 26 Tahoe, iOS 26). Earlier Safari has no WebGPU, so it falls back to WebGL2.
  - WebGL2 works in Safari 15 and later. Unity's loader also needs **Safari 16.4 or later** for WebAssembly 2023.
- What WebGPU adds: compute, so the GPU Resident Drawer, GPU occlusion culling, **STP upscaling**, VFX Graph, APV, and compute skinning. It also enables DBuffer decals.
- WebGPU limitations: no async compute, no dynamic resolution, no cubemap arrays, no synchronous GPU readback (use `AsyncGPUReadback`), RWBuffer limits, and storage-format limits.
- Cost measured here: wasm +0.27 MB, data +0.89 MB, and shader data roughly 1.9× (also inside every bundle). It also doubles the QA matrix.
- **Recommendation: ship WebGL2-only for v1.** Revisit `WebGPU, WebGL2` plus a device filter once the Safari pass is done. Every realism feature we need works on WebGL2.

### 4.2 URP feature support on Web (WebGL2)

| Feature | Status | Evidence / note |
|---|---|---|
| Forward+ rendering path | Works | Verified (PC_Renderer uses Forward+). |
| Main light shadows, 4 cascades, soft shadows | Works | Verified visually. Soft quality High is expensive on the web; use Medium. |
| Additional light shadows | Works | Keep it to 2-4 shadowed local lights. |
| SSAO renderer feature | Works | Verified: compiled, ran, no errors. Use Downsample and Medium samples. |
| Bloom, ACES tonemapping, color adjustments/curves/LGG/split toning, vignette, film grain, chromatic aberration | Works | Verified (UberPost) on WebGL2 and WebGPU. |
| Depth of Field (Gaussian), Motion Blur (camera) | Works | Verified Gaussian DoF and motion blur. Use Bokeh only in cutscenes, because it is heavy. |
| SMAA | Works | Verified (camera AA = SMAA High). |
| MSAA | Usually works | Unity says "most (but not all)" browser/GPU combinations support it. Prefer SMAA. |
| **FSR 1 upscaling** | **No** on WebGL2 | Verified: the EASU shader is unsupported. Use Bilinear render-scale. STP needs WebGPU. |
| **Decals** | **Screen Space technique only** | DBuffer needs per-render-target blend (GLES 3.2 / `OES_draw_buffers_indexed`), which was not exposed. Set the Decal feature technique to Screen Space. |
| Realtime GI | **No** | Baked GI only on Web. |
| Lightmaps | **Non-directional only** | Unity Web docs. Directional maps are useless data. |
| APV (Adaptive Probe Volumes) | Avoid on WebGL2 | Listed as compute-dependent (WebGPU). Not verified. Use classic Light Probe Groups. |
| GPU Resident Drawer / GPU occlusion | No on WebGL2 | Needs compute. |
| Native multithreading / parallel Burst jobs | **No** on our host | Needs `SharedArrayBuffer` and therefore COOP/COEP headers, which the host cannot set. Physics, animation and audio stay single-threaded. |
| Video (VideoPlayer) | Use URL mode | Don't import VideoClips. Play `StreamingAssets/*.mp4` via `VideoPlayer.url` (this suits the CRT intel montage). |
| AudioMixer | **Volume only** | No effects, reverb, EQ, low-pass or snapshots-with-effects. Bake the radio band-pass and reverb into the clips. |

### 4.3 Lighting strategy (realism within web limits)

- **Sun**: realtime Directional in **Mixed / Baked Indirect** mode.
  - Shadows: 4 cascades, 2048 shadow map (4096 as a "High" option), distance 70-90 m for FPS, soft shadows at Medium.
- **Static environment**: **baked lightmaps**.
  - Progressive GPU lightmapper, **Non-Directional**, Lightmap Encoding **Normal Quality**, 8-16 texels/m, with baked AO.
  - Bake per additive section scene, so each section's lightmaps fit inside its own bundle (budget 2-4 × 2048² per section).
- **Dynamic objects** (squad, enemies, weapons): **Light Probe Groups**, plus baked **reflection probes** (128-256 px, box projection, blending on).
- **Local lights**: Forward+ handles many unshadowed lights, such as muzzle flashes, flickering bulbs and fire. Keep shadowed spot/point lights to 2-4 visible.
- SSAO (downsampled) for contact shadows on dynamic objects; HDR plus ACES; height/exponential fog plus dust particles for the desert air.
- A heat-haze distortion needs the Opaque Texture. Enable it only if we use it.

### 4.4 Texture compression

- Desktop web: **DXT/BC** (BC1 opaque, BC3 alpha, BC3 DXT5nm normals), which is the project default.
  - If a GPU lacks a format, Unity decompresses in software, which costs memory and speed.
  - ASTC is for mobile only. A dual build plus a data-file switch is possible but out of scope.
  - BC7/BC5 are exposed in Chrome, but Safari support is unverified, so don't rely on them.
- **Crunch** (DXT1Crunched/DXT5Crunched) works on Web (verified) and cuts download by **~3.1×** at q50.
  - The cost is CPU transcoding on load, on the main thread because there are no threads. Hide it behind load screens.
  - Use q50-60 for environments and q75-85 for viewmodel weapons, hands and faces.
- Pack masks: **R = metal, G = AO, A = smoothness**. Assign one BC3 texture to both `_MetallicGlossMap` and `_OcclusionMap` (verified working).

### 4.5 Memory

- The Unity heap is one growable `WebAssembly.Memory`. The defaults are initial 32 MB and max 2048 MB (up to 4096 MB is supported, 16384 with wasm64).
- The unpacked `.data` lives in the JS-side Emscripten FS for the whole session, so keep it small. AssetBundles download straight into the Unity heap, and the **whole bundle file stays in memory until unloaded**.
- The GC only runs at the **end of a frame**, so per-frame allocation loops create quadratic pressure. Use pooling, StringBuilder and NativeArray.
- Recommended: initial **256 MB**, max **4096 MB**, Geometric growth. Test Safari at a 2048 cap.
- VRAM estimate for the content plan: 270-380 MB of textures plus render targets, which is fine on desktop.

### 4.6 Code size: IL2CPP and stripping

- IL2CPP is the only backend for Web.
- Measured: **OptimizeSize + Managed Stripping High** cuts wasm by 17.5% (1.2 MB) and the data by 0.85 MB, and the build ran fine.
- Use High stripping with a `link.xml` for anything reflection-based: serializers, Addressables providers, and classes that are only referenced from bundles. Build bundles first and pass `BuildPlayerOptions.assetBundleManifestPath`, or use Addressables, which handles this.
- For IL2CPP code generation: use OptimizeSize for iteration (faster builds). Compare OptimizeSpeed and OptimizeSize in the profiler before release. The wasm has 8 MB of headroom, so speed can win.
- Keep wasm2023 on. Keep Exceptions = Explicitly Thrown Only; "None" aborts on any exception. Debug Symbols Off; use External for QA builds.

### 4.7 Compression and hosting

- The host cannot set `Content-Encoding`, so **Decompression Fallback is required**. Its costs are a larger loader (119 KB), JS decompression, and no wasm streaming compile. Measured, it is fine: about 1 s for this build.
- Brotli is about 25-30% smaller than gzip here (wasm 6.9 vs 9.7 MB). Prefer it despite slower builds.
- **Data Caching**: IndexedDB caches the `.data` and `*.bundle` files with revalidation. Safari does **not** support IndexedDB for content running inside an iframe, so if the artifact is framed, Safari re-downloads every visit.
- **Progressive Asset Loading** (new per-scene `.data` split) is **incompatible with Decompression Fallback** and Name Files As Hashes. With compression Disabled the wasm is 24-35 MB, which is over the limit. **Not usable on this host.**

### 4.8 Input, pointer lock, fullscreen, audio, Safari

- **Pointer lock and fullscreen** must be requested from a user gesture. Unity defers the request to the next user event, so trigger on **mouse/key down**.
  - Set `WebGLInput.stickyCursorLock = false` so that Esc (which the browser reserves) turns `lockState` to `None`, and open the pause menu on lock loss.
  - Chrome blocks re-locking for about 1 s after the user exits the lock. Have a "click to resume" overlay.
  - Raw mouse input: `requestPointerLock({unadjustedMovement:true})` (Chrome) via template JS, with fallback.
  - Mouse delta is in device pixels, so divide by `devicePixelRatio` (pass it in through a .jslib) for consistent sensitivity.
  - Filter one-frame delta spikes.
- **Keys the browser owns**: Esc, Ctrl/Cmd+W/T/N/Q, F5, Cmd+R. **Do not bind crouch to Ctrl**, because Ctrl+W closes the tab. Use C for crouch and slide.
  - `navigator.keyboard.lock()` can capture some of these, but only in fullscreen Chrome.
- **Audio**: browsers block playback until a click or key. Unity resumes the AudioContext on the first gesture, so add a "CLICK TO BEGIN" title card before the intro.
  - Web audio is **AAC** (Unity encodes it; the Vorbis setting doesn't apply). Use Force To Mono for 3D SFX and VO.
  - Load types: SFX/VO **DecompressOnLoad** (lower latency); music and ambience **CompressedInMemory**.
  - Looping AAC can click at the loop point. Add 1024 samples of lead-in silence and a smpl loop chunk, or crossfade.
  - The decode sample rate follows the browser context, so don't assume 44.1 kHz.
- **Safari (macOS)**: WebGL2 since 15, WebAssembly 2023 since 16.4, WebGPU since 26.
  - Pointer lock and fullscreen are supported (16.4 and later unprefixed), and Safari blocks Esc for fullscreen toggling.
  - No IndexedDB in iframes.
  - Typically stricter on memory, so test a 2 GB heap.
  - BC/S3TC availability on Apple Silicon Safari is unverified and is the **#1 item to check**. Without it, Unity decompresses in software (4-8× the memory).
- **Device pixel ratio**: on Retina screens Unity renders at CSS size × DPR (2×). Cap it in the template (`config.devicePixelRatio = Math.min(window.devicePixelRatio, 1.5)`) and offer a render-scale option.

---

## 5. Recommended recipe

### 5.1 PlayerSettings (Web). Apply as an editor script before the content phase (not yet compiled as a block; the property names were validated individually in this session)

```csharp
using UnityEditor; using UnityEditor.Build; using UnityEngine;
var nt = NamedBuildTarget.WebGL;
PlayerSettings.SetScriptingBackend(nt, ScriptingImplementation.IL2CPP);
PlayerSettings.SetApiCompatibilityLevel(nt, ApiCompatibilityLevel.NET_Standard);
PlayerSettings.SetManagedStrippingLevel(nt, ManagedStrippingLevel.High);          // + Assets/link.xml
PlayerSettings.SetIl2CppCodeGeneration(nt, Il2CppCodeGeneration.OptimizeSize);    // compare OptimizeSpeed before release
PlayerSettings.SetIl2CppCompilerConfiguration(nt, Il2CppCompilerConfiguration.Release); // Master for the final build only
PlayerSettings.stripEngineCode = true;
PlayerSettings.colorSpace = ColorSpace.Linear;
PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, true);               // WebGL2 for v1
PlayerSettings.runInBackground = false;
PlayerSettings.gcIncremental = true;
PlayerSettings.SplashScreen.show = false;                                         // if the license allows; data impact is small
PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;           // gzip is OK for faster dev iteration
PlayerSettings.WebGL.decompressionFallback = true;                               // REQUIRED: host can't send Content-Encoding
PlayerSettings.WebGL.dataCaching = true;
PlayerSettings.WebGL.nameFilesAsHashes = true;                                    // release publishes: cache-safe names (delete stale files!)
PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
PlayerSettings.WebGL.wasm2023 = true;
PlayerSettings.WebGL.threadsSupport = false;                                      // needs COOP/COEP; impossible on this host
PlayerSettings.WebGL.initialMemorySize = 256;
PlayerSettings.WebGL.maximumMemorySize = 4096;                                    // test 2048 on Safari
PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;                  // External for QA builds
PlayerSettings.WebGL.showDiagnostics = false;                                     // true for QA builds (FPS/memory overlay)
PlayerSettings.WebGL.template = "PROJECT:CovertOps";                              // custom template, see 5.5
EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;
// Progressive Asset Loading: OFF (incompatible with decompression fallback)
```

Graphics settings: Lightmap/Fog modes Automatic, Instancing Variants Strip Unused, **BatchRendererGroup variants Strip All**, and trim Always Included Shaders.

### 5.2 QualitySettings

- Use one level for WebGL, **"PC"** (already set), renamed later to "Web_High".
  - Avoid a second RP asset for "Low", because every included RP asset adds shader variants to the data and bundles.
  - Do "performance mode" at runtime instead: SSAO off, 2 cascades, render scale 0.8, shadow distance 50.
- Texture Quality full res; Anisotropic Filtering **Per Texture**, aniso 4-8 on ground and walls.
- LOD bias 1.25, LOD crossfade dither on, Skin Weights 4 bones.
- Realtime Reflection Probes off; VSync 0 (the browser rAF paces frames; `Application.targetFrameRate = -1`).

### 5.3 URP asset (`Assets/Settings/PC_RPAsset.asset`), current → recommended

| Setting | Current | Recommended |
|---|---|---|
| HDR | on | on |
| **Color Grading Mode** | **LDR (0)** | **HDR** (proper ACES; LUT 32) |
| MSAA | Disabled | Disabled (SMAA High on the camera) |
| Render Scale / Upscaling | 1.0 / Auto | 1.0 (option 0.75-0.85) / **Bilinear** (FSR unsupported on WebGL2) |
| Depth Texture / Opaque Texture | on / on (2× down) | on / on only if heat haze/refraction is used |
| Main light shadows | 2048, distance 50, 4 cascades | 2048 (High = 4096), **distance 80**, 4 cascades, re-tune splits for FPS |
| Soft Shadows quality | High (3) | **Medium** |
| Additional lights | per-pixel, 4/object, shadows 2048 atlas | Same; keep ≤4 shadowed local lights |
| Reflection probe blending / box projection | on / on | on / on |
| Light Probe System | Light Probe Groups | Keep (no APV on WebGL2) |
| LOD Cross-Fade | on (Bayer) | on |
| SRP Batcher / GPU Resident Drawer | on / off | on / off |
| Volume Update Mode | Every frame | Via scripting for static volumes |

PC_Renderer (`Assets/Settings/PC_Renderer.asset`):
- Keep **Forward+**.
- SSAO: set **Downsample = on**, Samples Medium, Blur Medium, Intensity 0.5-0.8, Radius 0.35.
- Add the **Decal** feature with **Technique = Screen Space**.
- Point the renderer's `postProcessData` at a project copy of PostProcessData that keeps only 1-2 film grain textures. This saves about 2.6 MB raw from the boot data.

### 5.4 Default "Black Ops" volume (starting values, all verified to render on WebGL2)

| Override | Values |
|---|---|
| Tonemapping | ACES |
| Color Adjustments | post exposure 0-0.3, contrast +12, saturation -10, warm color filter |
| Split Toning | shadows teal, highlights amber; balance about -20 |
| Lift Gamma Gain | slight crush |
| White Balance | +8 for day |
| Bloom | intensity 0.4-0.8, threshold 1.0, scatter 0.6, no HQ filtering |
| Vignette | 0.25 |
| Film Grain | Medium1 0.2-0.3 (heavier in the intro and interrogation) |
| Chromatic Aberration | 0.08-0.15 |
| Lens Distortion | -0.05 in cutscenes |
| Motion Blur | camera, 0.15, Low quality, with a user toggle |
| DoF | Gaussian during ADS and dialogue; Bokeh in cutscenes only |

### 5.5 Custom Web template (`Assets/WebGLTemplates/CovertOps/`)

- Full-window canvas with `config.devicePixelRatio = Math.min(devicePixelRatio, 1.5)`.
- **"CLICK TO BEGIN" overlay**: it unlocks audio, requests pointer lock and optionally fullscreen.
- Loading bar driven by `onProgress`, and later by the bundle loader.
- `config.errorHandler` returns `true` for `WrongDocumentError`, `NotAllowedError` and `SecurityError` pointer-lock or fullscreen errors, and the template does **not** `alert()`.
- Wrap `HTMLCanvasElement.prototype.requestPointerLock` to try `{unadjustedMovement:true}` first and `.catch()` rejections.
- Drop the unused TemplateData PNGs to keep the file count low.

---

## 6. File-splitting strategy that satisfies the host limits

Host limits: 15 MB or less per binary file, 16 MB per text file, 255 files and 64 MB per publish call, 511 files and **256 MB per version**, standard media types, and no custom headers.

### 6.1 Layout

```
index.html, TemplateData/ (2-4 files)
Build/<name>.loader.js                    ~0.12 MB
Build/<name>.framework.js.unityweb        ~0.07 MB
Build/<name>.wasm.unityweb                6-9 MB    (budget ≤ 10 MB; measured 5.7-7.0 MB engine-only)
Build/<name>.data.unityweb                ≤ 12 MB   (boot/menu/intro UI, fonts, core shaders, player rig if small)
StreamingAssets/aa/WebGL/*.bundle         each ≤ 12 MB, LZ4 (ChunkBasedCompression), crunched textures
StreamingAssets/video/*.mp4               each ≤ 14 MB (H.264 720p ~2 Mbps ≈ 50 s per file), VideoPlayer.url
```

### 6.2 Comparison of options

| Option | Works on this host? | Notes |
|---|---|---|
| Everything in one `.data` (≤15 MB br) | Only for the engine and boot scene | A single 2k DXT PBR set costs about 9.5 MB inside the data. The whole game is far over. |
| Progressive Asset Loading | **No** | Incompatible with Decompression Fallback; an uncompressed wasm is over 15 MB. |
| **Addressables (LZ4) from StreamingAssets** | **Yes, recommended** | Relative URLs, in-file compression, IndexedDB caching via `.bundle`, dependency and duplicate analysis, async loading per section. Group "Pack Together" per section with a hard 12 MB cap, and use the binary catalog. Adds about 1 MB wasm. |
| Raw AssetBundles + own manifest | Yes (verified in this session) | Fewer files and no catalog, but we manage dependencies ourselves. Shared shaders get duplicated into every bundle (the test bundle carried its own Lit) unless a shared-shader bundle is built. |
| StreamingAssets raw media (mp4, audio via UnityWebRequestMultimedia) | Yes | Best for the intro video montage and long music beds. |

Rules:
- Name bundles `*.bundle` so the loader caches them.
- Pass `contentType: "application/octet-stream"` for `.unityweb` and `.bundle` in the Artifact `files` map. If the host rejects that, rename to `.bin` (`x.bundle.bin` still matches the cache regex) and patch `dataUrl`, `codeUrl` and `frameworkUrl` in index.html.
- Don't ship `*.manifest` files.
- Before you publish, **clean the output folder**, because Unity leaves foreign and stale files in it (my `timing.html` survived rebuilds).
- Add a **post-build validator** that fails when any file is over 14.5 MB, when the file count is over 250, or when the total is over 230 MB, and that prints the publish batches (64 MB or less each).

### 6.3 Estimate for the planned content (~60 PBR materials, ~40 meshes, ~150 audio clips)

The texture figures are extrapolated from the measured sets in 3.7. The mesh, animation and lightmap figures are assumption-based estimates and still need measuring.

| Content | Assumption | Download (LZ4 bundles) | VRAM / heap |
|---|---|---:|---:|
| Textures, DXT (no crunch), 15×2k + 45×1k sets | measured 10.95 / ~2.74 MB per set | **~288 MB: fails the 256 MB/version cap** | ~370 MB |
| **Textures, Crunch q50**, 15×2k + 45×1k | measured 3.49 / ~0.87 MB per set | **~92 MB** | ~370 MB |
| Textures, Crunch, lean (6×2k + 54×1k) | | ~68 MB | ~275 MB |
| Meshes, 40 unique (8 hero @30k verts, 32 env @10k verts, ~1M tris, 56 B/vert + skin) | estimate | 15-30 MB (mesh compression Medium, vertex compression on) | 35-45 MB |
| Audio, 150 clips (100 SFX mono 1.5 s, 40 VO/radio mono 4 s, 10 music/ambience stereo 90 s) | measured 14.5 KB/s stereo AAC; mono assumed ~½ | ~15-20 MB (incompressible) | PCM for DecompressOnLoad SFX lives in Web Audio memory |
| Animations (~60 humanoid clips) | estimate | 5-9 MB | |
| Lightmaps (5 sections × 2-4 × 2048² non-directional, Normal quality) | estimate ~5.6 MB per 2k map | 20-35 MB | |
| Shaders, UI, fonts, VFX | estimate | 8-15 MB | |
| Main build (wasm + data) | measured engine baseline + boot content | 12-20 MB | |
| **Total (Crunch, hero mix)** | | **~175-220 MB in ~20-30 files** | |

What this means:
- **Crunch on everything except a few hero textures is mandatory**. Without it, textures alone exceed the 256 MB version cap.
- Plan for **≤ 230 MB total**.
- Expect about 4 publish calls (64 MB each) to the same URL.
- Initial download is the wasm, the data and the first section, about 40-50 MB. Stream later sections in during the mission, for example behind radio-briefing beats.

Illustrative bundle set (each ≤12 MB):
- `shared_shaders`
- `player_viewmodel_weapons` (split into _a/_b if needed)
- `squad_characters`, `enemy_characters`
- `section_01..06_env` (one per additive lighting scene, with its lightmaps)
- `vo_radio_a/b`, `music_ambience_a/b/c`
- `intro_montage.mp4` (one or two files)

---

## 7. Gotchas found

1. Development builds are never compressed (raw wasm over 35 MB), so **only release builds can be hosted**. Use a release build plus `showDiagnostics` for QA instead.
2. The URP template's WebGL quality default was the **Mobile** renderer (no SSAO). This is now fixed. Re-check it whenever a new RP asset is added.
3. **Progressive Asset Loading cannot be used** (it is incompatible with Decompression Fallback).
4. The loader's default **`alert()`** fires on pointer-lock rejections. Ship a custom template error handler.
5. **Audio clips from an unloaded bundle fail** with "FMOD File not found". Call `LoadAudioData()`, wait for Loaded, then `Unload(false)`, or keep the bundle loaded (verified).
6. Web AudioMixer supports **volume only**, and audio filter components and reverb zones are unsupported. Bake the radio band-pass, interior reverb and distant gunfire variants into the files. ffmpeg is not installed; use python, numpy or scipy, or pre-processed fal output.
7. **FSR is unavailable** on WebGL2 (EASU shader unsupported) and STP needs WebGPU. Use bilinear render scale plus a DPR cap.
8. **DBuffer decals don't work** on WebGL2; use the Screen Space technique.
9. **Mouse delta is in device pixels.** Normalize by `devicePixelRatio`. Esc is browser-reserved, so pause on lock loss and use `stickyCursorLock = false`. Never bind Ctrl for crouch.
10. **Shader duplication across bundles**: each bundle that references URP Lit carries its own copy. Use Addressables duplicate analysis or a shared shader bundle.
11. Classes that are only referenced in bundles can be stripped with High stripping. Use `link.xml` or `assetBundleManifestPath`.
12. Every **film grain texture** (11 × 256 KB) and the splash logo (2.8 MB raw) are always in the boot data. Use a trimmed PostProcessData. Turning the splash off did not remove the logo texture.
13. Safari has **no IndexedDB in iframes**, so data caching is lost if the artifact is framed. Assume a full re-download on every Safari visit.
14. There are no threads and no SharedArrayBuffer, the GC only runs at frame end, and Crunch transcoding plus bundle decompression run on the main thread. Do all loading behind fades and briefings.
15. Unity names compressed files `.unityweb` when the fallback is on. That is not a standard media type, so the publish call needs an explicit content type.
16. `BuildReport.packedAssets` is **empty for incremental builds** whose assets are unchanged. The per-asset breakdown is in the cold-build report (`WebGLTest_report_build1_baseline.txt`).
17. Unity MCP and editor operations:
    - `EditorApplication.delayCall` **never fired** while the editor was unfocused (dozens of startup callbacks were still queued), so builds must run **synchronously inside `Unity_RunCommand`**. A 279 s build returned normally, with no MCP timeout.
    - Unity_RunCommand rejects `using System.Reflection`.
    - Creating scripts triggers a domain reload, and MCP answers "Unity not detected" for about 20-30 s.
    - The Editor log is **`/Users/gencsaracaydin/Documents/fun/fun-unity/Logs/Editor.log`**, not `~/Library/Logs/Unity/Editor.log`.
18. Environment collisions: port **8765** is taken by another agent's GLB-inspector node server, and the Claude browser pane's first tab is shared with that agent. Use your own ports (I used 18731/18732, now stopped) and your own tabs (`tabs_create`).

---

## 8. Not verified yet / next steps

1. **Artifact smoke test.** It needs the user's approval to publish, even privately. Publish this exact `/Users/gencsaracaydin/Documents/fun/fun-unity/Builds/WebGLTest` folder, about 16 MB in 4 batches, after removing `timing.html` and the `*.manifest` files. Then check:
   - content types accepted for `.unityweb` and `.bundle`
   - whether the page runs inside an iframe, and its sandbox and `allow` flags (pointer lock, fullscreen, autoplay)
   - IndexedDB caching
   - WebGPU availability
2. **Safari 18/26 pass** on Apple Silicon: S3TC exposed?, pointer lock, mouse deltas, audio unlock, memory at 2-4 GB, startup time.
3. **Real Chrome** pointer-lock deltas, `unadjustedMovement`, and DPR normalization.
4. **Performance budget**: build a representative 1-section greybox with 30 AI, the full post stack and 4 cascades, and profile at 1080p with DPR 1. Target 60 fps on an M-series Mac. Use `showDiagnostics`.
5. Measure real mesh, animation, lightmap and mono-AAC sizes once the content agents have assets, and replace the estimates in 6.3.
6. Decide between Addressables and raw AssetBundles. My recommendation is Addressables 2.x.
7. Write the post-build validator and publish-batcher (section 6.2).

## 9. Sources

- Unity 6.6 Web browser compatibility: https://docs.unity3d.com/6000.6/Documentation/Manual/webgl-browsercompatibility.html
- WebGPU in Unity 6.6 (overview, limitations, WebGPU vs WebGL2, device filter):
  - https://docs.unity3d.com/6000.6/Documentation/Manual/WebGPU.html
  - https://docs.unity3d.com/6000.6/Documentation/Manual/WebGPU-limitations.html
  - https://docs.unity3d.com/6000.6/Documentation/Manual/web-graphics-apis-intro.html
  - https://docs.unity3d.com/Manual/webgpu-intro-device-filter-asset.html
- WebGL2 notes (non-directional lightmaps, no realtime GI): https://docs.unity3d.com/6000.6/Documentation/Manual/WebGL2.html
- Web Player settings: https://docs.unity3d.com/6000.6/Documentation/Manual/class-PlayerSettingsWebGL.html
- Recommended Player, Graphics and Quality settings:
  - https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/building-distribution/web-optimization/player.md
  - https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/building-distribution/web-optimization/graphics.md
  - https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/building-distribution/web-optimization/quality.md
- Progressive asset loading (and its incompatibility with decompression fallback): https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/building-distribution/web-optimization/progressive-asset-loading.md
- Deploying compressed builds / decompression fallback: https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/building-distribution/deploying.md
- Cache behavior, memory, audio, texture compression:
  - https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/caching.md
  - https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/memory.md
  - https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/audio.md
  - https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/texture-compression.md
- AssetBundles on Web (LZ4 only, memory, stripping): https://docs.unity3d.com/6000.6/Documentation/Manual/webgl-assetbundles.html
- Input on Web (cursor lock, stickyCursorLock, fullscreen): https://docs.unity3d.com/6000.6/Documentation/Manual/webgl-input.html
- Unity 6.6 WebGPU production status (secondary source): https://app.cinevva.com/news/2026-09-01-unity-6-6-webgpu-production
- Raw mouse input: https://web.dev/articles/disable-mouse-acceleration
- URP decals on GLES3/WebGL (issue tracker):
  - https://issuetracker.unity.com/issues/21967/decal-render-feature-isnt-working-in-urp-when-running-with-opengles-3031
  - https://issuetracker.unity3d.com/issues/urp-decals-are-not-present-in-the-player-when-building-for-webgl
