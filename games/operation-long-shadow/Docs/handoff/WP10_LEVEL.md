# WP10_LEVEL handoff (World level construction: kit, buildings, terrain, sections S1–S9, markup, NavMesh, far proxies)

Status: **code complete, compile gate green** (`python3 Tools/build/compile_check.py --system World.Level`: GREEN, 6 assemblies, 0 errors, 0 warnings; also green with `--dependents` (32 assemblies), `--warnaserror`, `--dev` and `--all`). Not landed (the LAND agent lands every package). Code agent without editor access: **no builder, scene or EditMode test has run in Unity yet** — the first editor pass must run the menus below and read the logs (§6).

Read with: TECH_PLAN §3.9, §7.15, D7, D11–D13, D16, Appendix A; GDD §6 (maps, named areas, buildings, streaming), §7 (beats), §8.5, §9.3–9.4; `Docs/DECISIONS.md` rows `[WP10_LEVEL]` (23); `Docs/REQUESTS.md` entries "from WP10_LEVEL" (WP00/WP11 NavMesh area, LAND/WP11 CSV rows, replies to WP10_LOOK, WP04, WP07, WP06, WP09, WP03); `Docs/CORE_CHANGES.md` "WP10_LEVEL awning mission-event id".

---

## 1. What exists and where

### Runtime — `Staging/World/Level/` → `Assets/_Game/Scripts/World/Level/` (assembly `LongShadow.World.Level`, namespace `LongShadow.World.Level[.Data|.Geometry]`)

| Type | What it does |
|---|---|
| `CriticalPath` | **`ICriticalPath`** + ISaveable "CriticalPath": Appendix A knots per beat, `Progress01(pos, beat)` = normalised arc length of the closest point (monotonic per beat, 40 m lateral cap), Beat 11 = `B11.Exfil` Elapsed / 50 s; `Configure(dict)`, `PointAt`, `Length`, `Best`, `ResetProgress`. In S9. |
| `SplineVehicle` | **`ISplineVehicle`** + ISaveable "SplineVehicle.&lt;id&gt;": constant speed or per-segment seconds, Hermite arrival ease, final heading hold, bank/pitch for aircraft, shared roots (Magpie), followers at fixed spacing (B11 trucks), `Play/Stop/SetProgress/KnotProgress/Elapsed/GetSeat`, `DistanceAtTime/TimeAtDistance/Evaluate`, optional play-on-event / hide-until-play / pose-on-awake. Lives on the Appendix A spline marker. |
| `AwningPanel` | B3.AW01–08: tears on a tracer through its cloth bounds (`GameEvents.TracerPath`), swaps intact → torn, raises `B3.AwningTorn` once, `SFX_AWNING_TEAR`, saved. `SegmentHitsBounds` is public and tested. |
| `FruitCrate` | B3.Fruit1–6: its own `IDamageable` (team None); spills pooled physics fruit (`Pools`, layer Debris), `SFX_FRUIT_SPILL`/`SFX_IMPACT_FRUIT`, saved; also reacts to `DestructibleDamaged` with its id. |
| `BreachWall` | B6.BreachWall: on `B6.Blast` (or WP03's ForceDestroy) swaps intact → blown and enables the breach NavMeshLink; saved. |
| `GateDoorAnimator` | Swings hinged leaves when its `GateMarker` opens/closes (`GateChanged`), kick mode, creak cues, snaps after `CheckpointLoaded`. |
| `GateLinkedVolume` | Arms an OOB volume only while its blocker is closed (`OOB.Block.*`). |
| `MissionEventSwitch` | Show/hide objects on mission events (S8 burnt technical), optional loop cue, saved. |
| `SectionBlock`, `FarProxyBlock`, `ProxyBlockRegistry`, `ProxyBlockPair`, `ProxyBlockDirector` | Far-proxy swap (D7): real block renderers vs. S9 proxy per block id; director at 5 Hz with 90 m ± 5 m hysteresis (viewer = `IPlayerRig.Eye` or `Camera.main`). |
| `SectionRoot` | Stamp on the generated root (`LEVEL_Generated`): section, generator version, build time, report. |
| `RotorSpin`, `SaveRegistration` | Rotor spin with spool/blur swap; ISaveable registration helper. |
| `Data/*` (pure, tested) | `WorldConst`, `SectionLayout`, `MarkupSections` (id → section), `MarkupDefaults` (node-set poses, checkpoint node sets/headings, perch tags), `MarkupOverrides`, `DerivedMarkers` (OOB.Block.*, B2.AlertCover, B5.Ridge, B7.Fill1/2, B9.R1–R4, B11.FireProp, B8.Screen, CAM.Look.*, MAST.AvLight1–5, LAMP.01–38), `ClosetLayout`, `GateBlockers`, `ZoneVolumes`, `NavSeams` (8 section seams), `NavLinks` (8 in-section links), `ProxyBlocks` (11), `BuildingCatalog` (66 buildings), `DenseHousing` (119 far-only houses incl. B11 targets T2–T4), `LampLayout` (38), `PropCatalog` (~80 keys → manifest ids), `VehicleRoutes` (B10/B11/INS routes), `CriticalPathTable`, `ApproachHeightField` (S1 terrain function), `SunGeometry` (az 258°, lane band, shade line, ridge crest), `LevelEvents` (local adapter of the SequenceIds request), `DetRandom`, `ValueNoise`, `AreaRect`. |
| `Geometry/*` | `MeshBuilder` (slots, LOD masks, colliders, world UVs), walls/openings/fills/trims (`WallFrame`, `WallBuilder`, `WallOpening`, `KitFills`, `KitTrims`, `FacadeComposer`), `BuildingGeometry` (full + proxy), kit structures/street/roof/industrial pieces, `Landmarks` (Weighbridge arch, market arch, civic clock tower — square, flat-topped —, free and compound walls), `CinemaGeometry`, `RelayHallGeometry` (shell for S5, interiors for S6), `Interiors`, `BoxCarve`, `StructureAnchors`. |

### Editor — `Staging/World/Level/Editor/` (assembly `LongShadow.World.Level.Editor`)

| Folder / type | Role |
|---|---|
| `Builders/SectionBuilders` | Pipeline per section: open/create `Assets/_Game/Scenes/Sections/<S#>.unity` → reset `LEVEL_Generated` → builder `Geometry` → decals → reflection probes → flush merged batches → `MarkupPlacer` → `VolumePlacer` → builder `Wire` → flush → `SectionNavBaker` → `AfterNav` → `MarkupValidator` → stamp, log `Logs/level_build_<scene>.txt`, save. Everything outside `LEVEL_Generated` in the scene survives a rebuild. |
| `Builders/S1ApproachBuilder` … `S9TownFarBuilder` | The nine section builders (class docs list contents). `TownKit` (ground, lamps fixtures, props, sandbags, ladders + LadderVolume, PlayerClips, walls, cables, awnings, fruit crates, baked lights), `DecalPlacer`, `ReflectionProbePlacer`. |
| `Buildings/BuildingGenerator` | `Build(spec, parent, folder)`, `BuildProxy`, `AppendProxy`, review scene. |
| `Kit/KitGenerator` | 55 kit pieces → `Art/Models/Kit/Pieces/*.asset` + `Prefabs/Kit/KIT_*.prefab`; review scene `Scenes/Dev/Dev_Kit.unity`. |
| `Terrain/TerrainBuilder` | S1 Terrain (513² heights, 4 layers TL_Sand01/RockyTrail02/DryGroundRocks/AerialSand or flat fallbacks, URP Terrain/Lit), `FarMesh` for S9. |
| `Proxy/FarProxyBuilder`, `Proxy/BackdropBuilder` | S9 proxies per block + dense housing + lamp sprites; ridge silhouette, far hills, desert, salt pans. |
| `Vehicles/VehicleBuilder` | Magpie (+ cabin, HELI.* markers, Cabin ZoneTag, rotors), trucks with seats, B11 square trucks, `Attach(route)` SplineVehicle wiring. |
| `Markup/MarkupPlacer`, `GateLeaves`, `VolumePlacer`, `MarkupValidator`, `MarkupReport`, `MarkupToolsWindow` | Appendix A placement, gate leaves, zones, validation (`Logs/markup_validate_<scene>.txt`), the Markup Tools window. |
| `Nav/SectionNavBaker` | NavMesh per section (asset `Scenes/Sections/<S#>/NavMesh_<S#>.asset`), seam and in-section NavMeshLinks, Doorway modifier boxes. |
| `Common/*` | `SectionBuildContext`, `LevelObjects` (+ `LevelObjectOptions`), `LevelMaterials`, `LevelProps`, `MeshAssets`, `SectionScenes`, `SoftComponents` (WP03 Destructible by reflection), `LevelPaths`, `LevelBuildInfo`. |
| `Menu/LevelBuildMenu` | The menu (§3). |

### Tests — `Staging/Tests/EditMode/World/Level/` (assembly `LongShadow.Tests.EditMode.World.Level`, 3 files, 26 tests)

`LevelMarkupDataTests` (every Appendix A id has a section; gates/blockers start states; OOB per blocker; closets incl. the SC_B7_UP 15 m rule; checkpoint node sets; interaction ids; derived ids unique and 38 lamps; mast light heights; eye-adaptation EVs; seams vs links; overrides), `LevelGeometryDataTests` (Sunset Lane band z 238.85 at x 185; shade line 6.14 m at 6°; ridge crest 24.56 m held ±3° of 258°; B1 knots on the terrain; vehicle timings 50 s / last 15 s / 20 s / ≈4 s; no catalogue overlaps; dense housing off Sunset Lane, 3 targets; closet doors inside their sections), `LevelGeometryKitTests` (quad winding faces up, LOD1 drops detail, BoxCarve volume, DetRandom determinism, awning hit test, every catalogue building generates with colliders, cinema/Relay Hall generate with their anchors).

## 2. Public APIs other packages use

- Markers (Core types) by Appendix A id through `MarkerRegistry`: all CSV ids (except BOOTH.*, INS.Cam.*), plus `B8.Screen`, `B11.Truck1/2`, `LAMP.01–38`, `LAMPFAR.nn`, `MAST.AvLight1–5`, `CAM.Look.S#.nn`. Spline markers carry `ISplineVehicle` (`B10.Truck1/2`, `B10.HeliApproach`, `B10.HeliWaveOff`, `B11.Exfil`, `B11.TruckSquare`); INS.* carry a `SplineContainer` only.
- `ICriticalPath` registered by `CriticalPath` (S9). `ISaveable` keys: `CriticalPath`, `SplineVehicle.<id>`, `Awning.<id>`, `FruitCrate.<id>`, `BreachWall`, `LevelSwitch.<key>`.
- Mission events raised: `B3.AwningTorn`. Listened: `B6.Blast` (BreachWall), `B11.ExfilStart` (S8 switch); gates via `GateChanged`.
- Editor: `SectionBuilders.Build(SectionId)`, `SectionBuilders.BuildAll()`, `KitGenerator.BuildAll()`, `MarkupValidator.Validate(scene, section, table)`, `SectionNavBaker.Bake(ctx)`, `BuildingGenerator.Build(spec, parent, folder)`.

## 3. Editor steps (integrator, in order)

1. Prerequisites (other packages): land Core, World.Level (this package), World.Look; import WP10_CC0/FAL content and run MaterialFactory (materials, `TL_*.terrainlayer`, decal materials); run **LongShadow/Look/Build All** (volume profiles, backdrop materials) — optional but avoids fallbacks; Combat landed if Destructibles are wanted on the first pass (else re-run step 2 later).
2. **LongShadow/Level/Build All (Kit + Sections S1-S9)** — or **Build Sections S1-S9**, or one at a time under **LongShadow/Level/Sections/** (S1 Approach … S9 Town Far). Writes `Assets/_Game/Scenes/Sections/S1_Approach.unity` … `S9_TownFar.unity`, meshes under `Assets/_Game/Art/Models/Kit/Sections/<S#>/`, terrain under `Art/Models/Terrain`, backdrop under `Art/Models/Backdrop`, NavMesh assets under `Scenes/Sections/<S#>/`. Logs: `Logs/level_build_<S#>.txt`, `Logs/markup_validate_<S#>.txt`.
3. **LongShadow/Level/Markup/Validate All Section Scenes** (or the **Markup Tools** window) — must report 0 errors per section.
4. **LongShadow/Look/Bake/All Sections** (WP10 Look) — lightmaps, emergency set, S9 sets; Look's binder adds lamps/floods/fires at runtime.
5. WP06: add Loop/Civilian emitters with its menus on the built scenes (outside `LEVEL_Generated`).
6. Optional: **LongShadow/Level/Kit/Build Kit Review Scene**, **Build Building Review Scene**, **Report Missing Props**.
7. Run EditMode tests (`LongShadow.Tests.EditMode.World.Level`).

Re-running any section builder is safe and deterministic; hand-placed objects must live outside `LEVEL_Generated`.

## 4. What Level assumes from other packages

- Core as landed (markup components, `LayerIds`, `Sections`, `AssetManifest`, `BuilderUtil`, `MarkupIds`, `MarkerRegistry`, `GameEvents`, `Pools`, `SurfaceTag`).
- WP00/WP11: NavMesh area "Doorway" (index 3, cost 3) named in ProjectSettings (fallback works without), layers per TECH_PLAN §3.9 (Trigger, PlayerClip, FarProxy, Vehicle, Debris, Interactable).
- WP10_CC0/FAL: models by manifest id (`PropCatalog`), env materials by name (`LevelMaterials` table), `TL_*` terrain layers, `M_Decal_*`, Terrain003 bytes (optional). Missing pieces: greybox props of the right size, placeholder materials, flat terrain layers, no decal — all logged once.
- WP10 Look: `PracticalBinder` (lamp heads, floods, fires, emergency/tungsten fills), volume profiles `VP_Interior_*`, backdrop materials, SectionBaker (light probes, lightmaps), `LightmapSwapper` in S9.
- WP03: `LongShadow.Combat.Targets.Destructible` with fields `_id, _kind, _maxHp, _hitsToDestroy, _hitRadius, _core, _invulnerable, _intact, _destroyed, _lights, _emissive` (set by name; a renamed field logs once).
- WP07: opens/closes gates, arms B7.T.Landing/B8.T.BasementFoot, enables the InteractionPoints, plays the spline vehicles, raises the events.
- WP04/WP02: NavMeshLink traversal by rise; LadderVolume use; Interactable raycasts.

## 5. Open items / known gaps

- Nothing has run in Unity: expect a first-pass round of fixes (§6). Geometry generators were checked by reading and by offline math probes only.
- `markup_ids.csv` not edited (landed file): B11.Truck1/2, B8.Screen and the LAMP/LAMPFAR/MAST/CAM.Look patterns are requested (Docs/REQUESTS.md); the Level validator already knows them.
- Doorway area: index 3 until ProjectSettings names it.
- No SignBaker (Sardani signage, marquee titles): sign boards are blank geometry; the WP08 font is ready for a later pass (`DCL_SARDANI_SIGNS`).
- Cloth awnings and laundry use Lit cloth materials (no wind) until Look's foliage-wind converter is run on them.
- The tunnel shaft hatch in the S5 yard ground and the S6 shaft ladder must line up (x 343.6–345.0, z 379.6–381.2): check in the first pass.
- Market/square props are placed at fixed coordinates; after the real models land, re-run the sections (collider sizes follow the models' bounds).
- No PlayerClip ceilings over hero interiors: rely on building colliders; review roof-jump exploits in playtest.

## 6. First editor pass checklist

1. Build S1 and open it: terrain heights at the spawn (≈ +22), wadi head (+18), boulder (+4), grove (0); trigger boxes on the ridge follow the slope; INS splines above ground.
2. Build S2–S4 and check the Beat 3/5 routes on the NavMesh (Window ▸ AI ▸ Navigation shows the baked data): node sets within 1 m of the mesh (validator warns otherwise), links on planks/tank stand/stair.
3. S5/S6: breach link inactive until the blast; Hall door, screening, west-stair and tunnel leaves swing the right way (flip the sign in `GateBlockers` if not); SC_B9_H trench link reaches the hatch.
4. S8: Magpie poses at the approach start; HELI.* markers in the cabin; trucks hidden until Play.
5. S9: proxies hide when standing in a loaded section; the ridge hides the sun disc at MID-A (Look's **Run Sun Geometry Checks**).
6. Console: missing assets are expected until content lands; any exception in a builder is a bug in this package.
