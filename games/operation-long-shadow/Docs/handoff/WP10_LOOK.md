# WP10_LOOK handoff (World look: LightStage, sky, film grade, practicals, bakes, LookCapture)

Status: **code complete, compile gate green** (`python3 Tools/build/compile_check.py --system World.Look`: GREEN, 6 assemblies, 0 errors, 0 warnings; `--all` and `--dev` green too). Not landed (the LAND agent lands every package). Code agent: no editor use, so **the HLSL shaders, the builders and the EditMode tests have not run in Unity yet** — the first editor pass must check the console (see §6).

Read with: TECH_PLAN §3.7–3.8, §7.14, D6, D7, D16, D21; GDD §9; `Docs/DECISIONS.md` `[WP10_LOOK]` rows (14 decisions); `Docs/REQUESTS.md` entries "from WP10_LOOK" (Level, WP07, WP09, WP01, WP11, FYI WP06/WP03/WP04); `Docs/CORE_CHANGES.md` "WP10_LOOK look mission-event ids".

---

## 1. What exists and where

### Runtime — `Staging/World/Look/` → `Assets/_Game/Scripts/World/Look/` (assembly `LongShadow.World.Look`, 44 files)

| Folder / type | What it does |
|---|---|
| `Stage/LightStageTable` (+ `LightStageKey`, `LightStageSample`, `StageBlend`) | GDD §9.2a keyframes verbatim (15 rows, linear between rows), stage boundaries, ±0.2 stage ramps, post exposure per stage (0/+0.4/+1.0/+1.5), AI sight (70/60/45/35), HUD amber `#E3A33B` / green `#A6E3B4` from s 2.0, story clock (17:58 → 19:12), sun at azimuth 258° (`LightRotation(el)` = Euler(el, 78, 0)), per-stage shadow bias, sun-disc visibility (fades 1.97 → 2.00). Pure, tested. |
| `Stage/LightStageController` | **`ILightStage`** + `ISaveable` "LightStage". Drives SUN (rotation, intensity, Kelvin colour, shadow strength/bias, soft → none), SKY_KEY (0 → 0.15 over 1.97–2.00, then GDD values), `RenderSettings.sun`, URP shadow distance (80/60 m; 50 m cap in Performance Mode; restored on destroy), the `Sky/PanoBlend` skybox (runtime copy of `M_Sky_PanoBlend`), the sun disc and lens flare, exp² fog (density x `FogDensityScale`), ambient SH (4 stage probes, rescaled by the live sky exposure; gradient fallback), reflection intensity, the four stage volume weights, shader globals `_LS_SunColor/_LS_SunDirection/_LS_StageParams`. Raises `StageChanged`, `LightStageChanged` (once per crossed hundredth + every immediate jump) and `PracticalsStrike` when s crosses 1.97 upward with `SetTarget` (never with `SetImmediate`). `SetTarget(s, smoothing)`: smoothing = settle time (≈95 %), 0 = jump now. Overrides are reference-counted owners. `PreviewApply(s)` / `EditorReleasePreview()` for edit-mode tools. `static Active`. |
| `Stage/LightStageTuning` (asset `Data/Lighting/LST_Default.asset`) | Calibration only: sky base exposures A 0.29 / B 0.075, glow re-expansion, horizon haze, sun/sky-key scales, bias scales, sky-key rendering layers, fog density scale 0.8, ambient scale, sun disc size/distance/luminance, flare intensity, AI sight step time. |
| `Stage/AmbientSHSet` (+ `AmbientSHStage`, `AmbientSHMath`) | Four captured stage probes (27 floats each) + capture exposure; SH projection helpers (Unity `AddDirectionalLight`, self-normalised; Panoramic equirect convention). |
| `Sky/PanoBlendSky`, `Sky/SunDisc` | Sky material parameters; HDR disc billboard placed 2.9 km out per camera in `beginCameraRendering`, depth-tested (ridge hides it), never fogged. |
| `Atmosphere/WindController` (+ `WindMath`) | West → east 3–5 m/s with gusts; globals `_LS_WindDirection` (xyz toward, w m/s), `_LS_WindParams` (gust, turbulence, phase, flutter phase); drives a WindZone for particles; `WindController.Velocity`. |
| `Atmosphere/LightShaft`, `Atmosphere/DustMotes` | Sun-shaft card that follows the sun, takes its colour, fades when its aperture is in shade (raycast to the sun every 0.5 s), cylindrical billboard; projector-beam mode (fixed direction/colour). Dust-mote volumes thinning after sunset. |
| `Exposure/EyeAdaptationDriver` | G10: one runtime volume per `EyeAdaptationZone` (priority 20), 1.5 s in / 1.0 s out (unscaled), post exposure = stage exposure + zone EV every frame (deep-copied profile). |
| `Exposure/DofDirector` | Gameplay DoF (priority 30): Gaussian 40 → 160 m, weight = max(AdsBlend, 0.6 while a squad/Calder line plays out of combat). |
| `Exposure/ExposureFlicker`, `VolumeProfileUtil` | Booth tube flicker ±0.15 EV on a deep-copied profile; runtime profile helpers. |
| `Practicals/PracticalLight` (+ `PracticalSwitch`, `FlickerMode`, `FlickerNoise`) | Emissive renderers (MPB `_EmissionColor`) + optional unshadowed realtime light; rules AlwaysOn / AfterStrike / AfterSunset / MissionEvent / Manual; flicker Fire / Fluorescent / Faulty / Flare / 1 Hz; dies with its destructible id (checkpoint-safe via `Destructibles`); saves on/off when event/script-driven; id defaults to the parent marker's id; `PreviewLevel` for edit-mode previews. |
| `Practicals/PracticalBudget` (+ `PracticalBudgetMath`) | 4 Hz: grants realtime lights to the best-scored budgeted practicals within range: min(6, 12 − 4 muzzle reserve − fixed lights on); 0.25 s fades. `RegisterFixed(Light)` for floods/flares. |
| `Practicals/SodiumLamp` (+ `SodiumWarmup`), `LampRipple` | Sodium heads: sputter, pink-red → orange over 8 s; ripple from the generator (372, 0, 305) at 15 m/s on `PracticalsStrike`, late-loading lamps join the ripple, checkpoints snap; `SFX_SODIUM_STRIKE` at ≤ 6 lamps near the listener. |
| `Practicals/AviationLights` | Mast lights top → bottom 0.3 s apart, then 1 Hz; lenses hidden while dark. |
| `Practicals/FloodLight`, `FloodGlare` | B6 floods: dark until `B6.FloodsOn`, arc slam (staggered), IlluminationZone owner B6.Fx active only while lit and alive, stop on `DestructibleDestroyed`; saves on state. Glare → `IScreenFX.Glare` from view angle, beam position, distance, LOS (10 Hz). |
| `Practicals/ParachuteFlare`, `FlareDirector` | B10 flares on `B10.FL1/2/3` at the markers: shadowed 120° spot (newest flare only) + point fill, 1.3 m/s descent, ±4°/3 s pendulum, wind drift, 30 s burn with sputter and 3 s burn-out, IlluminationZone; checkpoint reload puts them out; greybox fallback when the prefab is missing. |
| `Practicals/FireProp` | Fire lit on a mission event (`B4.TechnicalIgnited`) or at start; particles + fire-flicker practical; saved. |
| `Practicals/PracticalBinder` (+ `PracticalBindRule`) | Attaches Look prefabs under markers that have none: `LAMP.` → sodium head, `LAMPFAR.` → sprite, `B6.F1..F4` → flood variant aimed at (345, 0, 325), `B4/B11.FireProp`, `B9.Emergency1/2`, `B7.Fill1/2`. Runs at start and after every `SectionLoaded`. |
| `Lightmaps/LightmapSet`, `LightmapSwapper` (+ `LightmapSwapRule`) | Swap by **texture** (never index) inside `LightmapSettings.lightmaps`, re-applied after every section load/unload; stage thresholds (S9_TownFar GOLDEN → SUNSET 1.35 → DUSK 1.97) or mission event (S6 EMERGENCY on `B9.TransmitterDead`, saved). |
| `FX/RotorDownwash`, `FX/RotorShadowCaster` | Insertion/LZ downwash (ground ring, streaks, sun-lit sheet; surface-tinted; strength by height and `RotorPower`); shadows-only blades at 324 rpm (cabin strobe). |
| `Common/LookIds`, `Common/LookShaderIds` | Event/marker/cue ids (local adapter of the CORE_CHANGES request) and shader property ids. |

### Editor — `Staging/World/Look/Editor/` (assembly `LongShadow.World.Look.Editor`, 20 files)

| Type | Menu (all under **LongShadow/Look/**) |
|---|---|
| `LookBuildAll` | **Build All (profiles, materials, prefabs, rig, SH)** — `LongShadow.World.Look.Editor.LookBuildAll.Run()`; log `Logs/look_build.txt` |
| `VolumeProfileBuilder` | Build Volume Profiles → `Settings/Volumes/VP_Base, VP_Golden, VP_Sunset, VP_Afterglow, VP_BlueHour, VP_Interior_{Cinema,Hall,Screening,Basement}, VP_Booth, VP_Cinematic` |
| `LookMaterialBuilder` (+ `SkyCalibration`, `FoliageMaterialUpgrader`) | Build Materials and Data → `Art/Materials/Sky/M_Sky_PanoBlend, M_SunDisc, M_Backdrop_Ridge, M_Backdrop_FarHills`, `Art/Materials/FX/Look/M_FX_*` (shaft, beam, motes, lamp glow, sodium glass, aviation/flood/emergency lenses, flare, tungsten bulb, housing, dust/smoke/fire particles), `Art/Textures/FX/T_FX_ShaftNoise_BC.png`, `Data/Lighting/LST_Default.asset`, `Data/Lighting/AmbientSHSet.asset`; Convert Foliage Materials To Wind Shader |
| `LensFlareBuilder` | Build Sun Lens Flare → `Art/VFX/LF_Sun.asset` (glow, halo ring, thin streak, hex ghosts) |
| `FXPrefabBuilder` | Build FX and Practical Prefabs → `Prefabs/Props/Lamps/PF_SodiumLamp_Head, PF_SodiumLamp_Sprite, PF_AviationLights, PF_Floodlight_F1..F4, PF_EmergencyLight, PF_TungstenFill`; `Prefabs/FX/PF_FX_ParachuteFlare, PF_FX_FireTechnical[_Lit], PF_FX_LightShaft, PF_FX_ProjectorBeam, PF_FX_DustMotes, PF_FX_RotorDownwash, PF_FX_RotorShadow` |
| `LookRigBuilder` | Build Look Rig Prefab → `Prefabs/FX/Look/PF_LookRig.prefab`; **Install Look Rig In Active Scene** (S_Persistent) |
| `AmbientSHCapture` | Capture Ambient SH (4 stages) |
| `SectionBaker` (+ `SectionBakeSpec`, `SectionBakeTable`, `ProbePlacer`) | Bake/S0 … S9, Bake/All Sections; Place Light Probes On Critical Path (open scene); log `Logs/look_bake.txt` |
| `LookCapture` (+ `SunGeometryChecks`) | Capture Look Review Sheets (open scenes); Run Sun Geometry Checks (open scenes); `LookCapture.Run(new[]{"S4_Roofs","S9_TownFar"}, null)` → `Builds/captures/look_<ts>/` (PNG per shot and s, `_sheet.png`, `index.html`, `checks.txt`) |
| `LightStageScrubberWindow` | **LightStage Scrubber** (play mode: drives ILightStage with optional override and strike; edit mode: previews the open scene) |
| `LookValidator` | Validate Look → `Logs/look_validate.txt` (EditorOnly HDR dependencies, profiles, URP §3.4, SSAO §3.5 (warnings), bake suns, one sun, ≤ 1 shadowed local, `_EMISSION` on practical glass) |
| `LookPaths`, `LookBuildUtil` | Paths and builder helpers |

### Shaders — `Staging/Assets/_Game/Art/Shaders/Look/` → `Assets/_Game/Art/Shaders/Look/` (land with `land.py --assets`)

`Sky_PanoBlend.shader` (**Sky/PanoBlend**), `SunDisc.shader` (LongShadow/World/SunDisc), `Backdrop.shader` (LongShadow/World/Backdrop, forward + DepthOnly), `FoliageWind.shader` + `FoliageWindInput.hlsl` + `FoliageWindPasses.hlsl` (Lit, alpha clip, two-sided, wind in forward/shadow/depth/depth-normals/meta, back-light translucency, Forward+ `_CLUSTER_LIGHT_LOOP`), `LightShaft.shader`, `DustMotes.shader`, `LampGlow.shader`, `LSWind.hlsl` (shared wind function for Level's cloth), `Editor/CubeToEquirect.shader` (Hidden, editor only).

### Tests — `Staging/Tests/EditMode/World/Look/` (assembly `LongShadow.Tests.EditMode.World.Look`, 6 files, ~40 tests)

`LightStageTableTests` (every keyframe row, interpolation, stages, sunset switch, disc fade, AI sight/HUD/clock, weights sum to 1, exposure blend, azimuth 258°, fog monotonic, bias), `LightStageControllerTests` (strike once on the scripted drive, no strike on checkpoint jumps, hundredth-step events incl. 2.00, overrides, fog, save round trip, clock), `PracticalRulesTests` (budget slots, nearest selection, sodium warm-up, ripple 15 m/s, flicker ranges, flare envelope/pendulum, flood slam, glare, bind rules, downwash), `AtmosphereMathTests` (wind 3–5 m/s eastward, uniform sky SH = radiance, gradient, rotation, round trip, Panoramic convention at u 0.9667 = 258°), `LightmapSwapperTests` (texture remap with a foreign section's entry in between, thresholds), `LookDataTests` (VP_Base grade values, stage override sets, Purkinje, booth, D16 27-eq budget, knots).

## 2. Public API summary

- **`ILightStage`** (Core) = `LightStageController`: `S`, `Stage`, `SetTarget(s, smoothing=2)`, `SetImmediate(s)`, `Override(owner, on)`, `IsOverridden`, `AISightRange` (stepped, 2 s ramp), `HearingScale` (1), `AfterSunset`, `HudColor`, `StoryClockMinutes`. Extras in-assembly: `Active`, `CurrentSample`, `StagePostExposure`, `SunLight`, `SkyKeyLight`, `KeyLight`, `SunColorLinear`, `Applied` event, `PreviewApply/EditorReleasePreview`.
- Events raised: `StageChanged`, `LightStageChanged`, `PracticalsStrike`. Events consumed: `MissionEvent` (`B6.FloodsOn`, `B9.TransmitterDead`, `B10.FL1..3`, `B4.TechnicalIgnited`, practical on/off events), `DestructibleDestroyed`, `CheckpointLoaded`, `SectionLoaded/Unloaded`, `SettingsChanged`, `VOLineStarted/Finished`, `CombatStateChanged`.
- Services used (all optional, null-safe): `ICheckpointService` (registers saveables), `ISettingsService` (Performance Mode), `IPlayerRig` (eye/camera, AdsBlend), `IAudioService` (cues), `IScreenFX.Glare`, `IMarkerRegistry`/`MarkerRegistry` (markers), `Destructibles`.
- Shader globals for any package: `_LS_WindDirection`, `_LS_WindParams`, `_LS_SunColor`, `_LS_SunDirection`, `_LS_StageParams` (see `LookShaderIds`).

## 3. Editor steps for the integrator (exact order)

1. LAND: `python3 Tools/build/land.py World.Look --wp WP10` and `python3 Tools/build/land.py --assets --wp WP10` (the shaders). Then `AssetDatabase.Refresh()`, wait for compilation, **read the console: shader compile errors from `Art/Shaders/Look/*` would show here** (none expected; fix in Staging and re-land if any).
2. Content first (if not done): copy WP10_CC0's ready files (`Tools/build/copy_ready.py`, at least `Art/HDRI/Runtime/T_Sky_*`, `*_sky.json`, `Art/HDRI/EditorOnly/*_nosun_2k.exr` imported as **Cube / Lat-Long**, `Art/Textures/FX/T_LensDirt_BC.png`, `T_VFX_*`), then MaterialFactory over the ready specs (WP00-B, LongShadow/Materials/...).
3. **LongShadow/Look/Build All (profiles, materials, prefabs, rig, SH)** (or `Unity_RunCommand`: `LongShadow.World.Look.Editor.LookBuildAll.Run()`); read `Logs/look_build.txt` (it lists anything that degraded: procedural sky, missing lens dirt, greybox rotor, WP03 Destructible not compiled yet).
4. Open `Assets/_Game/Scenes/S_Persistent.unity` (WP11) → **LongShadow/Look/Install Look Rig In Active Scene** → delete any other directional light → save.
5. EditMode tests: `LongShadow.Core.Editor.TestRunnerBridge.RunEditMode("LongShadow.Tests.EditMode.World.Look")` → expect all green; clear the console afterwards.
6. Play S_Persistent (or a dev scene with the rig) → **LongShadow/Look/LightStage Scrubber**: scrub 0 → 4, press 1.94 → 1.97 → 2.00 with "Continuous" on to see the strike; check one sun, disc, fog, volumes.
7. After WP10 Level builds sections (and its REQUESTS items): **LongShadow/Look/Bake/<section>** (or **All Sections**); read `Logs/look_bake.txt` (eq per set vs cap; auto re-bakes at lower density when over). S9_TownFar and S6 install their `LightmapSwapper` automatically. Re-run after geometry changes.
8. When the EditorOnly EXRs are imported: **Capture Ambient SH (4 stages)** again (it prefers the EXR; it logs the measured glow azimuth and the rotation applied).
9. Review: open S_Persistent + the section(s) (+ S9_TownFar for the ridge) → **Capture Look Review Sheets** or `LookCapture.Run(new[]{"S1_Approach","S9_TownFar"}, null)`; look at `Builds/captures/look_<ts>/index.html` and `checks.txt` (R4 sun at 1.80, disc visible at 1.80 / hidden at 2.00, Sunset Lane at 0.6, shafts in shade, fog transmittance). Tune `Data/Lighting/LST_Default.asset` (fog scale, sky B exposure, bias scales) and re-capture.
10. **LongShadow/Look/Validate Look** before a build: 0 errors expected (`Logs/look_validate.txt`).

## 4. What other packages must provide (and what I assumed)

- **WP10 Level** (REQUESTS "from WP10_LOOK -> WP10_LEVEL"): markers `LAMP.nn` (heads), `LAMPFAR.nn`, `B6.F1..F4`, `B4/B11.FireProp`, `B9.Emergency1/2`, `B7.Fill1/2`, `CAM.Look.*`; one `EyeAdaptationZone` per interior with the VP_Interior_* profile and EV; ridge/far-hill meshes on layer FarProxy with the Backdrop materials, readable, named "Ridge"/"FarHills"; static flags, reflection probes, S6 light naming (`BasementTungsten` group, `Emergency*` lights); FarProxyGroup; mast lamp housings at 90/72/54/36/18 m. Assumed: section scenes at `Sections.ScenePath(id)`, Level's LampLayout ids are SceneMarkers.
- **WP07**: drives s (`SetTarget(s, 2)`), holds overrides for the cinema swap and MID-A (per-frame `SetTarget(s, 0)` so the strike fires at 1.97), raises `B6.FloodsOn`, `B9.TransmitterDead`, `B10.FL1..3`.
- **WP09**: implements `IScreenFX.Glare`; uses `VP_Booth`, `VP_Cinematic`, `ExposureFlicker`, `PF_FX_RotorDownwash`, `PF_FX_RotorShadow`; cinematic DoF volumes above priority 30; `SetImmediate(0)` for the insertion.
- **WP11**: installs PF_LookRig in S_Persistent (active scene), dev console `s <value>`.
- **WP01**: link.xml entries (lens flare, volumes, WindZone, LightProbeGroup) — request filed. Assumed QualityApplier keeps its 50 m performance cap (Look also caps).
- **WP03**: `Destructible` field names (`_id, _kind, _hitRadius, _core, _lights, _emissive, _dieOffSeconds`) — Look's builder adds it by type name; ≤ 4 pooled muzzle lights.
- **WP06**: cues `SFX_SODIUM_STRIKE`, `SFX_FLOOD_SLAM_ON`, `SFX_FLARE` (missing cues are skipped); ambience can listen to `PracticalsStrike`.
- **WP04**: reads `AISightRange`, `HearingScale`, IlluminationZones (owners B6.Fx, B10.FLx) — already implemented on their side.
- **Core** (CORE_CHANGES): mission-event ids in `SequenceIds` (local adapter `LookIds`).

## 5. Known gaps and risks

- **Not run in Unity.** HLSL was written against the URP 17.6 sources in `Library/PackageCache` (same macros as Lit/Unlit), but has never been compiled; editor-only flows (SectionBaker, AmbientSHCapture read-back, LookCapture `SubmitRenderRequest`, particle keyword setup through `BaseShaderGUI/ParticleGUI`) have never executed. First editor slot: watch the console after landing and after Build All.
- **Look calibration is starting values**: sky B exposure (0.075), fog scale (0.8), bias per stage, flare/flood/lamp intensities (URP inverse-square units: flood 900 at ~50 m, flare spot 2400 at 70 m, sodium point 8 at 6 m) need one LookCapture review pass; all live in `LST_Default` or the prefabs.
- **Cube orientation**: Unity's lat-long cube import convention is measured, not assumed (glow azimuth); if an EXR has no clear glow (dusk), the rotation may be off by tens of degrees — `Logs/look_bake.txt`/the SH log prints the measured azimuth; set the Skybox/Cubemap `_Rotation` of `HDRI/EditorOnly/M_Sky_BakeEnvironment` by hand if needed.
- **Lightmap sets of S9/S6** rely on identical atlas layouts between bakes of the same scene (same objects, same density); the baker warns when map counts differ. Untested until sections exist.
- **Edit-mode previews** (scrubber, LookCapture, Install) change scene objects/RenderSettings in memory; the URP shadow distance is restored on release/close.
- Not built: sand blowing off dune crests (CINEMATIC_DIRECTION §2 VFX list), heat-haze distortion (GDD §9.7 cuts it), the Beat 1 dust devil card (WP10 Level, PROC_SALT_PAN), SignBaker-driven decals (WP10_CC0 deferred them; not Look). DoF Bokeh for cinematics is WP09's.
- `IlluminationZone.IsLit` (Core) allocates nothing but scans every marker; fine at WP04's 5 Hz.
- No PlayMode tests.
