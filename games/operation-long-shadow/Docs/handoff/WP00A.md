# WP00 Phase A handoff (Foundation: packages, settings, skeleton, Core, Input, tooling)

Status: **done**. Editor console: 0 errors, 0 warnings. `compile_check.py --all`: green, 56 assemblies, about 3 s. EditMode tests: 78/78 passed (`Logs/tests/editmode_20261016_093656.txt`). `ProjectSetup` was applied, and a second run reported 0 changes. WebGL is still the active build target. The research scaffolding (`Assets/Scenes/WebGLTest*`, `Assets/Scripts/WebGLTest`, `Assets/WebGLTest`, `Assets/Editor/WebGL*`) is untouched and compiles; the `Tools/Web/Build WebGL Test` menu is still there.

Read these next: `Docs/CORE_CONTRACTS.md` (index of every Core type), `Tools/build/README.md` (Staging, gate, landing), `Docs/DECISIONS.md` (`[WP00]` rows).

---

## 1. What exists and where

| Item | Where |
|---|---|
| Packages (TECH_PLAN §2.1, exact versions, nothing substituted) | `Packages/manifest.json`: ai.navigation 2.0.15, timeline 6.6.0, splines 2.9.1, animation.rigging 6.6.0, addressables 2.11.2 (+ scriptablebuildpipeline 3.0.3), recorder 5.1.7, test-framework 1.8.0. `com.unity.ai.assistant` is kept. |
| Project settings script | `Assets/_Game/Scripts/Core/Editor/ProjectSetup.cs`. Menus **LongShadow/Setup/Apply Project Settings** and **LongShadow/Setup/Verify Project Settings** (dry run). Log: `Logs/project_setup.txt` (first apply kept as `Logs/project_setup_first_apply.txt`). |
| Applied settings | §3.1 player/WebGL, encodings, §3.2 stripping, §3.3 quality `Web_High`, §3.4 PC_RPAsset, §3.5 renderers (`PC_Renderer` with SSAO + Decal + inactive `Glitch` full-screen pass; new `Assets/_Game/Settings/URP/PC_Renderer_RT.asset` at index 1; `PostProcessData_Web.asset`), §3.8 rendering layers (Default=World, Booth, Cabin, Viewmodel), §3.9 layers 6-20 + collision matrix + query flags, §3.13 time/audio/editor, project-wide input actions. |
| Folder skeleton (§4.1) | `Assets/_Game/**` (Art, Audio/SFX/<group>, Audio/VO/<CHAIN>, Prefabs, Scenes/Sections, Data, Settings, Timelines, UI, Plugins/WebGL, Tests). `.gitkeep` in empty folders. |
| Asmdefs (§4.3 + split World) | One runtime + `.Editor` + `Tests.EditMode` + `Tests.PlayMode` asmdef per system: Core, Input, Platform, Player, Combat, AI, Characters, Audio, Mission, UI, Cinematics, **World.Look** (`Scripts/World/Look`), **World.Level** (`Scripts/World/Level`), Integration. 56 assemblies in total. Each has a placeholder `AssemblyInfo.cs`. The table lives in `Tools/build/ls_layout.py`, and `Tools/build/skeleton.py --check` verifies it. |
| Core (§6) | `Assets/_Game/Scripts/Core/{Types,Interfaces,Runtime,Markup,Components,Save,Editor}`. Namespace `LongShadow.Core` (editor: `LongShadow.Core.Editor`). |
| Input (§7.2) | `Assets/_Game/Scripts/Input/{InputIds,InputReader,SpikeFilter}.cs` and `Assets/_Game/Settings/Input/LongShadow.inputactions` (project-wide). |
| Tests | `Assets/_Game/Tests/EditMode/Core/*.cs` (12 files), `.../EditMode/Input/InputTests.cs`. |
| Markup id CSV | `Assets/_Game/Data/markup_ids.csv`: 292 ids from TECH_PLAN Appendix A plus the CINEMATIC_DIRECTION §2 `INS.*` ids. Generator: `Tools/build/gen_markup_csv.py`. WP10 owns it from now on. |
| Tools | `Tools/build/compile_check.py`, `land.py`, `ls_layout.py`, `skeleton.py`, `gen_markup_csv.py`, `gen_core_contracts.py`, `editor_lock.py`, `README.md`. |
| Docs | `Docs/CORE_CONTRACTS.md` (generated), `Docs/CORE_CHANGES.md` and `Docs/REQUESTS.md` (formats + first entry), `Docs/LANDING_LOG.md`, `Staging/README.md`. |
| Staging | `Staging/<System>/` and `Staging/Tests/{EditMode,PlayMode}/<System>/` for every system, plus `Staging/Assets/` for non-code assets. Staged Core/Input are identical to the landed copies. |

## 2. Public APIs (summary; full list in `Docs/CORE_CONTRACTS.md`)

- **Service locator.** In `Awake`, call `GameServices.Register<IYourService>(this)`. In `OnDestroy`, call `Unregister`. To use a service, keep a `ServiceRef<T>` field and read `.Value`, which can be null: degrade, never throw. `GameServices.Get<T>()` logs once when the service is missing.
- **Events.** Use `GameEvents.<Name>` and raise them only through `GameEvents.Raise<Name>(...)`. There are 53 events, and every one is cleared on `SubsystemRegistration`; a test enforces this for new ones.
- **Interfaces and their implementers** (register them in `GameServices`):

| WP | Implements |
|---|---|
| WP01 Platform | `IDisplayInfo`, `ITimeScaleService` (priorities in `TimePriority`), `IInputLock`, `IPauseService`, `ISettingsService` (keys in `StorageKeys`), `ISectionStreamer` (scene names/keys in `Sections`), `IPoolService`. It also puts `LongShadow.Input.InputReader` and `LibraryRegistry` in `S_Boot`/`S_Persistent`. |
| WP02 Player | `IPlayerRig`, `IPlayerHealth` (both also `ITargetable`/`ISuppressible`), using `LadderVolume`, `OutOfBoundsVolume`, `GlassingPOI`, `IInteractable` and `AudioCues.Footstep(surface)`. **No AudioListener on the rig** (see WP06). |
| WP03 Combat | `ICombatService` (incl. `SpawnAmmoCrate`), `IWeaponService` (loadout, `SetHolstered(owner, bool)`, `Resupply`, `MountDoorGun(pivot)`), `IWeaponHudState`, `IWeaponGrips`, `IDestructible` (register in `Destructibles`), using `WeaponIds`, `AudioCues.Impact/WeaponShot`, `LayerIds.PlayerShot/EnemyShot`. |
| WP04 AI | `ITargetRegistry`, `IEncounterService`, `ISquadService`. It raises `EncounterActivated/WaveSpawned/EncounterCleared/CombatStateChanged`, and drives bodies through `ICharacterBody` using `ActionIds`. |
| WP05 Characters | `ICharacterBody` (`HumanoidBody`), `IFirstPersonBody`. Controllers use `AnimatorParams`, and events use `AnimEventIds`. |
| WP06 Audio | `IAudioService` (owns **the only AudioListener**, follows `IPlayerRig.Eye`, `SetListenerOverride` for cinematics), `IVOService` (speaker ids and subtitle colours in `SpeakerIds`; G9 via `VOOptions.RadioSwitchTime`), `IMusicService`. Cue ids are ASSET_MANIFEST ids. |
| WP07 Mission | `IMissionDirector`, `IObjectiveService`, `ICheckpointService` (ids in `SequenceIds.Checkpoints`). It spawns crates at `CRATE.*` through `ICombatService.SpawnAmmoCrate`, listens to `GameEvents.Interacted` for `InteractionPoint`s, and drives `GateMarker`s. |
| WP08 UI | `IHud`, `IClickGate`. It reads `IWeaponHudState`, `IObjectiveService`, `ILightStage.HudColor` and `SubtitleData`. |
| WP09 Cinematics | `ICinematicService` (ids in `SequenceIds`), `IInsertService`, `IScreenFX`. It uses `IPlayerRig.SetViewActive/SetLookAngles`, `IAudioService.SetListenerOverride`, `IWeaponService.SetHolstered`. |
| WP10 Look | `ILightStage` (raises `LightStageChanged`, `StageChanged`, `PracticalsStrike`). It drives `EyeAdaptationZone`, `IlluminationZone`. |
| WP10 Level | `ICriticalPath`, `ISplineVehicle`. It places every markup component (ids from `markup_ids.csv`), including `InteractionPoint` and `GateMarker`. |
| Core (done) | `IMarkerRegistry` (`MarkerRegistry`, auto-registered), `Health`/`Hitbox`/`SurfaceTag`, markup components, `CheckpointSnapshot`/`SaveWriter`/`SaveReader`, `Libraries`/`AssetLibrary<T>`/`LibraryRegistry`, `Placeholders`, `Pools`, `LSLog`, `MathX`, springs, `ShuffleBag`, `CountdownTimer`. |
| Input (done) | `IInputReader` = `LongShadow.Input.InputReader` (DPR-normalised, spike-filtered, honours `IInputLock`). |

- **Assets that have not landed yet.** At runtime, `Libraries.Get<TLib>()` then `lib.Get(id)`. A missing entry or library returns null and logs one `LSLog.MissingAsset` line; then use `Placeholders.Create/Humanoid/ForMissingAsset/SilentClip`. In editor builders, `AssetManifest.Find<T>(manifestId, nameContains)` resolves `ASSET_MANIFEST.json` target paths and returns null with one warning when the file is missing. Builders should use `BuilderUtil.PlaceholderMaterial` (a saved material asset) and `BuilderUtil.SavePrefab` (which keeps GUIDs) so that re-running a builder after the asset lands just upgrades the prefab.

## 3. How code agents compile and land (exact commands, from the project root)

```
python3 Tools/build/compile_check.py --system Player                 # green/red for Staging/Player + Player.Editor + Player tests
python3 Tools/build/compile_check.py --system World.Look --system World.Level
python3 Tools/build/compile_check.py --system Combat --pass player   # just the WebGL player pass
python3 Tools/build/compile_check.py --all                           # everything staged + landed
python3 Tools/build/land.py --status
python3 Tools/build/land.py Player --wp WP02 --dry-run
python3 Tools/build/land.py Player --wp WP02
python3 Tools/build/land.py --assets --wp WP01                       # Staging/Assets/** (WebGL template etc.)
```

Proof, recorded 2026-10-16. `--system Player` on the empty skeleton: GREEN (6 assemblies, 2.6 s, exit 0). With an injected `Staging/Player/__DemoBroken.cs`, the result was RED, exit 1:
```
Staging/Player/__DemoBroken.cs:5:26: error CS0029: Cannot implicitly convert type 'string' to 'int' [LongShadow.Player; editor+player]
Staging/Player/__DemoBroken.cs:6:33: error CS0234: The type or namespace name 'EditorApplication' does not exist in the namespace 'UnityEditor' (are you missing an assembly reference?) [LongShadow.Player; player]
```
After deleting the file it was green again. `compile_check.py --selftest` also passes (clean = green; an injected CS0029 is caught; an unguarded `UnityEditor` call is caught in the player pass only).

Rules for code agents:
- Only `.cs` files go in Staging (no asmdefs, no metas).
- Namespaces are `LongShadow.<System>[.<Folder>]`.
- Warnings block landing.
- A system may reference only Core.
- **World.Level does not reference World.Look.**
- New package references go into a staged copy of the system's own asmdef.
- New asmdefs need `Docs/REQUESTS.md`.

## 4. Editor steps for the integrator (editor holder)

1. At the start of every slot: `python3 Tools/build/land.py --status`, then `Unity_RunCommand` → `AssetDatabase.Refresh()`, wait for compilation, then `Unity_ReadConsole` (must show 0 errors). Every landing triggers a domain reload, and Unity MCP calls fail for 20-30 s during one.
2. After WP01 lands `Assets/WebGLTemplates/CovertOps/index.html` (`land.py --assets`), run **LongShadow/Setup/Apply Project Settings** once more. It switches `WebGL.template` to `PROJECT:CovertOps`; everything else reports `=`. Use **LongShadow/Setup/Verify Project Settings** at any time to check for drift (a dry run that writes `Logs/project_setup.txt`).
3. EditMode tests: menu **LongShadow/Tests/Run EditMode Tests**, or `Unity_RunCommand` with `LongShadow.Core.Editor.TestRunnerBridge.RunEditMode(null)` (all LongShadow EditMode assemblies; or pass `"LongShadow.Tests.EditMode.Core;LongShadow.Tests.EditMode.Player"`). Results go to `Logs/tests/editmode_<ts>.xml/.txt`. The run logs expected warnings/errors from `LogAssert` tests, so **clear the console afterwards** before the 0-error check.
4. After any Core change (WP11), in this order:
   1. Edit `Staging/Core`.
   2. `land.py Core --wp WP11` (this gates every landed system).
   3. `python3 Tools/build/gen_core_contracts.py`.
   4. Add an entry to `Docs/CORE_CHANGES.md`.
5. After Appendix A changes (WP10): edit `markup_ids.csv` directly, or update TECH_PLAN Appendix A and run `python3 Tools/build/gen_markup_csv.py`. Then rerun the EditMode tests: `MarkupTests` and `IdTablesTests` read the CSV.
6. Optional: `python3 Tools/build/editor_lock.py acquire|release|status` if editor slots are shared outside the workflow.
7. Toggle markup labels in the scene view: **LongShadow/Markup/Toggle Gizmo Labels**.

## 5. What other packages must provide, and what WP00 assumed about them

- **WP01** places `InputReader` (in `S_Boot`) and the `LibraryRegistry` host. It owns `link.xml`, which must preserve our `ScriptableObject` library types, because stripping is now **High**. It calls `Application.targetFrameRate = -1` and sets `WebGLInput.stickyCursorLock = false` at runtime (these are not project settings). It implements the D21 render-scale default with `IDisplayInfo.CanvasPixelSize`. Assumption: section Addressables addresses equal the scene names (`Sections.AddressKey`).
- **WP02** must not add an `AudioListener`. It implements the IPlayerRig members added in Phase A (`Pitch`, `Yaw`, `SetLookAngles`, `SetViewActive`) and raises `PlayerMountChanged` in `Mount`/`Dismount`.
- **WP03** owns crate and door-gun behaviour, behind `ICombatService.SpawnAmmoCrate` and `IWeaponService.MountDoorGun`. Assumption: `IWeaponService` is the inventory's public face, and the inventory saves itself as the `ISaveable` "Weapons".
- **WP04** raises the encounter/combat-state events. It honours `CoverNode`, `PerchNode`, `SpawnCloset`, `FlankRoute`, `SquadNodeSet` and `IlluminationZone` as data.
- **WP05** maps every `ActionIds` id to an animator action index. Unknown ids should log once and complete immediately, so that scripts never hang. It builds `AC_Soldier` and `AC_ShadowProxy` with the `AnimatorParams` names.
- **WP06** builds `LIB_Audio`/`LIB_Dialogue` keyed by manifest ids and DIALOGUE ids. It skips missing cues with one log line. It keeps the only `AudioListener`.
- **WP09** plays the insertion cold open inside `TL_Intro` (`SequenceIds.Intro`). It places `INS.Cam.I01..I12` markers itself, and it uses its own instance of `PFB_Calder_Shadow` for third-person Calder in the insertion (no rig change needed).
- **WP10 Level** places `OOB.InsertionApron` and the `INS.*` splines in `S1_Approach`, and owns `markup_ids.csv` from now on.

## 6. Known gaps and notes

- Phase B items are **not** part of this package: `TextureImportRules`/`ModelImportRules`/`AudioImportRules` (§3.10-3.12), `MaterialFactory`, `Tools/fal/run_manifest.py`, `Tools/cc0/fetch.py`, `Tools/mesh/*`, `Tools/tex/{pack_mask,decal_alpha,atlas}.py`, `Tools/requirements.txt`, `validate_assets.py`. Until they exist, importers use Unity defaults, so an editor holder importing assets before WP00-B must set the import settings by hand (or wait).
- The film-grain textures cannot be trimmed in URP 17.6: they moved to Graphics Settings `FilmGrainResources`. `PostProcessData_Web` exists but saves nothing yet (WP11 size audit).
- The WebGL template is still `APPLICATION:Default` until WP01 lands CovertOps (see step 4.2).
- No PlayMode tests exist yet. The PlayMode asmdefs compile empty.
- `markup_ids.csv` has no coordinates for 18 ids that Appendix A does not locate: `CAM.Look.*`, `INS.Cam.*`, the six `OOB.Block.*` volumes ("in front of each blocker"), `B2.AlertCover` ("along z 140"), `B5.Ridge`, `B7.Fill1/2`, `B9.R1..R4` (only z 356-358), `B11.FireProp` and `CritPath.B11` (time-based). WP10 places them from the notes column; `python3 Tools/build/gen_markup_csv.py` prints the list.
- `Docs/DIALOGUE.json` and `Docs/GDD.md` were modified by content agents during this run. `IdTablesTests.EveryDialogueSpeakerHasAStyle` fails loudly if a new speaker id appears. If it does, add the speaker to `SpeakerIds` (Core change).
- The `.gitignore` has no entry for `/.locks/` (`editor_lock.py` creates it on first use); the orchestrator may want to add one.
