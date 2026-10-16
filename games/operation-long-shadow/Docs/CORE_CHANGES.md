# Core contract change requests

Core (`Assets/_Game/Scripts/Core`, namespace `LongShadow.Core`) is frozen except for **additive** changes (TECH_PLAN §6). Code agents never edit `Staging/Core/` or the landed Core. To ask for a change, append one entry below; WP00 (later WP11) applies it, lands it and fills in the Status line. Until then, work around it locally (an internal adapter in your own assembly) instead of waiting.

Entry format (append at the bottom, newest last):

```
## <YYYY-MM-DD HH:MM> <WPxx> <short title>
- What: the exact member or type to add (signature + XML doc text), file it belongs to
- Why: who calls it, who implements it, the GDD/TECH_PLAN section
- Breaking? must be "no" (additive only: new types, new interface members nobody implements yet, new events, new optional parameters at the end)
- Status: requested
```

Reference of the current contracts: `Docs/CORE_CONTRACTS.md`.

---

## 2026-10-16 WP00 Phase A additions (applied, landed)
- What: `IWeaponService`; `ICombatService.SpawnAmmoCrate`; `IAudioService.PlayAttached`, `IAudioService.SetListenerOverride`; `IPlayerRig.Pitch`, `Yaw`, `SetLookAngles`, `SetViewActive`; `VOOptions.RadioSwitchTime`; `GameEvents.Interacted`, `EncounterActivated`, `WaveSpawned`, `EncounterCleared`, `CombatStateChanged`, `PlayerMountChanged`; markup `InteractionPoint`; id tables `WeaponIds`, `SpeakerIds` + `SpeakerStyle`, `ActionIds`, `AnimEventIds`, `AnimatorParams`, `AudioCues`, `Sections`, `SequenceIds`.
- Why: cross-package calls that TECH_PLAN §7 implies but §6 did not type (Mission → Combat loadout/door gun/crates, Cinematics → player view/listener, G9 positional radio, shared string ids).
- Breaking? no (no implementations existed yet).
- Status: landed 2026-10-16 (see Docs/LANDING_LOG.md).

## 2026-10-16 10:55 WP01 ITimeScaleService.SetScale (ramps)
- What: `bool SetScale(int handle, float scale);` on `ITimeScaleService` (Interfaces/ITimeScaleService.cs). XML doc: "Changes the scale of a live request (ramps such as Death 0.3 over 0.6 s, TECH_PLAN §5.4). Returns false for unknown handles."
- Why: WP02 (death slow-down 1 -> 0.3 over 0.6 s) and WP09 (insert/blast slow-mo ramps) would otherwise Pop/Push every frame, which reorders equal-priority requests. Implemented already by WP01 `TimeScaleService.SetScale` (public, compiles today); callers can use it through the interface once this lands.
- Breaking? no (new interface member; the only implementer is WP01's TimeScaleService, which already has it).
- Status: requested

## 2026-10-16 10:55 WP01 IPauseService.RelockCooldown
- What: `float RelockCooldown { get; }` on `IPauseService` (Interfaces/IPauseService.cs). XML doc: "Seconds until the browser accepts a new pointer lock (Chrome refuses one for about 1 s after the user pressed Esc); 0 when a click would lock now. WP08's pause menu shows 'Click to resume' while it is above 0 (GDD §11)."
- Why: WP08's pause menu cannot reference Platform; GDD §11 asks for the "Click to resume" overlay because of Chrome's re-lock cooldown. Implemented already by WP01 `PauseService.RelockCooldown`.
- Breaking? no (new interface member; the only implementer is WP01's PauseService, which already has it).
- Status: requested

## 2026-10-16 12:00 WP02 IPlayerRig.SetEyeHeightOverride
- What: `void SetEyeHeightOverride(float? eyeHeight, float blendSeconds);` on `IPlayerRig` (Interfaces/IPlayerRig.cs). XML doc: "Scripted posture: overrides the eye height above the feet (Beat 1 start prone ≈ 0.35 m, then the scripted kneel at 1.05 m, GDD §7.1/§8.3), blended over `blendSeconds`; null returns to the stance height. The capsule is unchanged; the shadow proxy hides while the eye is below 0.7 m (AC_ShadowProxy has no prone state)."
- Why: WP07's Beat 1 script / WP09's intro shot 9 and title card need "prone → look-only → scripted kneel" (GDD §7.1 Player start, §8.3) and cannot reference Player. Implemented already by WP02 `PlayerRig.SetEyeHeightOverride` (same signature), backed by `PlayerCameraRig.SetEyeHeightOverride`. Until it lands, callers have no way to do it except through Integration.
- Breaking? no (new interface member; the only implementer is WP02's PlayerRig, which already has it).
- Status: requested

## 2026-10-16 12:00 WP02 SequenceIds.GlassedPrefix
- What: `public const string GlassedPrefix = "Glassed.";` in Runtime/SequenceIds.cs under "mission events with listeners outside Mission". XML doc: "Raised by WP02 Binoculars through GameEvents.RaiseMissionEvent once per GlassingPOI when the binocular view keeps it within its radius for its dwell time (GDD §7.1): GlassedPrefix + POI id, e.g. \"Glassed.B1.POI1\". The ticked set clears on CheckpointLoaded."
- Why: WP07's Beat 1 objective `OBSERVE ZAROUD 0/3` listens for it (WP07 may also poll `IPlayerRig.IsGlassing`/`ViewRay` itself). WP02 uses the local adapter `LongShadow.Player.PlayerEvents.GlassedPrefix` (same string) until this lands.
- Breaking? no (new constant).
- Status: requested

## 2026-10-16 12:00 WP02 DamageInfo remark: who applies GDD §16 to the player (comment only)
- What: amend the `DamageInfo` summary (Types/DamageInfo.cs) sentence "Receivers only apply their own modifiers (difficulty scaling on the player, ...)" to: "Receivers only apply their own modifiers. Hostile **bullet** damage to the player arrives already difficulty-scaled (WP04 `ArchetypeTable.DamageToPlayer`: Regular × `EnemyDamageMultiplier`, the marksman's absolute `MarksmanDamagePerHit`); the player scales hostile **non-bullet** damage (explosions, fire, melee) by `EnemyDamageMultiplier`; falls, scripted and friendly damage are never scaled. Invulnerability and last-stand immunity stay with the receiver."
- Why: WP04 scales AI shot damage and WP03 `FireAI` passes it through, while WP03 explosions reach the player at Regular values; the earlier WP02 draft scaled every hostile hit, which would have applied GDD §16 twice to bullets (Veteran AK 20 × 2.2 × 2.2 ≈ 97 per hit, "hits to down" 1 instead of 3). WP02 `PlayerDamageMath` now follows the split above; tests `AiBulletsArriveScaledAndAreNotScaledTwice`, `HostileExplosionsAreScaledByThePlayer`, `LegionAkHitsToDownMatchGdd16` (10/5/4/3 through the whole pipeline).
- Breaking? no (comment only).
- Status: requested

## 2026-10-16 12:00 WP02 TriggerVolume occupancy by collider (robustness)
- What: in Markup/TriggerVolume.cs, track the player by collider instead of the `_playerInside` counter (e.g. `if (isPlayer) { if (_lastPlayer == other && _playerInside > 0) return-or-refire-per-rules; ... }`, or a small `HashSet<Collider>`), so a repeated OnTriggerEnter from the same collider does not raise the count twice; keep the existing events and `PlayerInside` semantics.
- Why: Unity may skip OnTriggerExit when a collider is disabled and sends OnTriggerEnter again when it is re-enabled inside the trigger. The player's CharacterController is disabled while mounted (Beat 11 door gun) and for teleports, so `PlayerInside` can stay true after the player left, `SetArmed(true)` can fire for a player who is not inside, and non-once triggers re-raise `TriggerEntered`. WP02 already keeps the controller enabled during vaults, climbs and ladders, and its own consumers (ladder list, OOB countdown) are idempotent.
- Breaking? no (internal bookkeeping).
- Status: requested

## 2026-10-16 WP05 extra action ids
- What: in `Runtime/ActionIds.cs` add string constants (values must equal WP05's `LongShadow.Characters.Animation.ExtraActionIds`):
  `Fire = "Fire"` (one shot: procedural upper-body recoil), `SitIdle = "SitIdle"` (booth: seated nervous idle), `SitTalk = "SitTalk"` (booth: seated, answering), `SitArmsCrossed = "SitArmsCrossed"`, `PhoneTalk = "PhoneTalk"` (handset/phone conversation, arms only), `ReachToggle = "ReachToggle"` (outro row 7: reach to BOOTH.Scrambler), `HeadLift = "HeadLift"` / `GoLimp = "GoLimp"` (MID-B shots 5/6), `LookAround = "LookAround"` (sentry idle variety), `Kneel = "Kneel"`, `CheckWatch = "CheckWatch"` (squad glances at a watch), `OneFinger = "OneFinger"` (INSERTION I-3 Hollis, arms only), `Wave = "Wave"`, `Talk = "Talk"` (standing conversation), `Stop = "Stop"` (ends any looping/timed action, completes immediately).
- Why: WP09's CharacterActionClip, WP07 beat scripts and WP04 brains need typed ids for the booth, MID-B, outro and insertion actions WP05 implements (`ActionTable`); until then they pass the same literals.
- Breaking? no (new constants only).
- Status: requested (WP05 uses `ExtraActionIds` locally meanwhile).

## 2026-10-16 WP05 character models keep material slots
- What: in `Editor/ModelRules.cs`, rule "Characters" (Art/Models/Characters/** except Props): `Materials = ModelImporterMaterialImportMode.ImportViaMaterialDescription` instead of `None` (and `ModelRuleApplier` then sets `materialLocation = InPrefab` as for static meshes).
- Why: with "None" the FBX slot names (M_Calder_Body, M_CHP_ChestRig, ...) are lost and `ModelMaterialBinder` cannot bind the character materials; WP05's `RocketboxImporter` (postprocess order 10) already overrides this locally, so only `ImportRuleReport`'s drift diff disagrees today.
- Breaking? no (WP05 is the only consumer of that folder).
- Status: requested (local override in `LongShadow.Characters.Editor.RocketboxImporter`).

## 2026-10-16 WP04 AI mission-event ids in SequenceIds
- What: in `Runtime/SequenceIds.cs` under "mission events with listeners outside Mission" add (values must equal WP04's local adapter `LongShadow.AI.Common.AIEvents`):
  `AIAlertPrefix = "AI.Alert."` ("AI.Alert.<encounter>[.<wave>]": alerts an encounter or one wave; waves not spawned yet spawn alerted. Beat 2 alert branch: "AI.Alert.E2.Gate"),
  `AIRunPrefix = "AI.Run."` ("AI.Run.<enemyId>": a sentry runs to its wave's alert cover shouting BK_LEG_01; Beat 2 failure branch "AI.Run.E2-01"),
  `B4TechnicalOpen = "B4.TechnicalOpen"` (optional: the technical opens fire now; otherwise 2 s after D028 starts),
  `B6Blast = "B6.Blast"` (the breach detonated: E6 yard alert, gatehouse wave timer),
  `E5FirstGlint = "E5.FirstGlint"`, `E5FirstShot = "E5.FirstShot"` (raised by the marksman: cues for D035/D036),
  `B4TechnicalIgnited = "B4.TechnicalIgnited"` (raised 1.5 s after E4-01 dies: WP10 lights the fire prop at B4.FireProp),
  `B10RpgAtMagpie = "B10.RPGAtMagpie"` (raised when E10-07 fires at Magpie: WP07 starts the wave-off),
  `B11RpgLaunch = "B11.RPGLaunch"` (raised when B11 T3 launches at t 30: D081 and the climax),
  `AIMoralePrefix = "AI.Morale."` ("AI.Morale.<encounter>.<wave>", raised on a morale break; diagnostics/music).
- Why: WP07 beat scripts raise the commands and listen to the AI's events; WP10 (fire prop) and WP09 (climax) listen too. Until this lands everyone uses the same literals.
- Breaking? no (new constants).
- Status: requested (local adapter `LongShadow.AI.Common.AIEvents`).

## 2026-10-16 WP04 ActionIds.SmokeIdle
- What: in `Runtime/ActionIds.cs` add `public const string SmokeIdle = "SmokeIdle";` with doc "Beat 2 sentries: smoking idle loop (cigarette to the mouth every few seconds); ended by `Stop` or any other action (GDD §7.2)". (`Stop` is already in WP05's extra-id request.)
- Why: WP04's SentryBehaviour plays it on E2-01/E2-02 while they are unaware; WP05 maps it in ActionTable (until then the body logs once and completes it, and the sentries just stand).
- Breaking? no (new constant).
- Status: requested (local adapter `LongShadow.AI.Common.AIActionIds.SmokeIdle` / `.Stop`).

## 2026-10-16 WP04 IEncounterService.Awareness01
- What: in `Interfaces/IEncounterService.cs` add `float Awareness01(string enemyId);` with doc "Detection meter 0..1 of a living enemy (the Beat 2 sentry HUD icon, GDD §14.2); 0 when unknown or dead."
- Why: WP08's HUD shows the detection meter over the Beat 2 sentries (GDD §14.2 "Suspicious state (a detection meter on a sentry's HUD icon)"); the AI owns the meter. WP04's `EncounterSpawner.Awareness01` already implements it.
- Breaking? no (new member of an interface only WP04 implements).
- Status: requested (implemented in `LongShadow.AI.Encounters.EncounterSpawner`).

## 2026-10-16 WP06 IAudioService.PlayPitched
- What: in `Interfaces/IAudioService.cs` add `void PlayPitched(string cueId, Vector3 position, float volume, float pitch);` with doc "3D one-shot with a pitch multiplier on top of the cue's own random pitch (sprint footsteps +5 %, GDD §10.2; WP06_SFX notes)."
- Why: WP02's FootstepEmitter needs the sprint pitch (its handoff lists it as a gap because IAudioService has no pitch parameter); WP09 can use it for slow-motion accents. WP06's `AudioService.PlayPitched` already implements exactly this signature.
- Breaking? no (new member of an interface only WP06 implements; the only other implementer, WP06's test fake, already has the member).
- Status: requested (implemented in `LongShadow.Audio.Core.AudioService`).

## 2026-10-16 WP06 audio mission-event ids in SequenceIds
- What: in `Runtime/SequenceIds.cs` under "mission events with listeners outside Mission" add (values must equal WP06's local adapter `LongShadow.Audio.Core.AudioEvents`):
  `AmbienceBoothOn = "Ambience.Booth.On"`, `AmbienceBoothOff = "Ambience.Booth.Off"` (booth room tone instead of the world beds; CinematicStarted(Intro|Outro) does it by default),
  `AmbienceMute = "Ambience.Mute"`, `AmbienceUnmute = "Ambience.Unmute"` (every bed fades out / back),
  `NumbersStart = "B9.Numbers.Start"`, `NumbersStop = "B9.Numbers.Stop"` (basement broadcast; BeatStarted(9) and CP9 start it by default),
  `TransmittersDown = "B9.TransmittersDown"` (raised by WP06 when the fourth rack dies: the basement tube hum stops),
  `PASiren = "PA.Siren"` (curfew siren from the three PA horns), `PAPlayPrefix = "PA.Play."` (+ cue id: any cue from the three horns).
- Why: WP07 beat scripts and WP09 cinematics raise them, WP06 listens; neither can reference LongShadow.Audio. Until this lands they pass the same literals.
- Breaking? no (new constants).
- Status: requested (local adapter `LongShadow.Audio.Core.AudioEvents`).

## 2026-10-16 WP10_LOOK look mission-event ids in SequenceIds
- What: in `Runtime/SequenceIds.cs` under "mission events with listeners outside Mission" add (values equal to WP07's `MissionEvents` and Look's local adapter `LongShadow.World.Look.Common.LookIds`):
  `CinemaSkySwap = "B4.SkySwap"` (Beat 4 cinema sky swap 1.00 → 1.35), `FloodsOn = "B6.FloodsOn"` (floods F1–F4 slam on), `TransmitterDead = "B9.TransmitterDead"` (emergency lightmap set + red stair lights, G11), `Flare1 = "B10.FL1"`, `Flare2 = "B10.FL2"`, `Flare3 = "B10.FL3"` (parachute flares at the markers of the same id).
- Why: WP07 raises them; WP10 Look (floods, LightmapSwapper, emergency practicals, FlareDirector) and WP10 Level (`LevelEvents`) listen; neither can reference Mission.
- Breaking? no (new constants).
- Status: requested (local adapter `LongShadow.World.Look.Common.LookIds`).

## 2026-10-16 WP08 UI mission-event ids in SequenceIds
- What: in `Runtime/SequenceIds.cs` under "mission events with listeners outside Mission" add (values must equal WP08's local adapter `LongShadow.UI.Common.UiEvents`):
  `TitleCardFinished = "UI.TitleCardFinished"` (doc: "Raised by the HUD when the GDD §8.3 title card has retracted its bars; D007 follows (WP07)."),
  `UIGateReady = "UI.Gate.Ready"` ("The click gate became ready while shown (boot, QUIT TO SESSION, END SESSION); WP01 re-arms the browser gesture."),
  `DebriefReady = "UI.Debrief.Ready"`, `DebriefReplay = "UI.Debrief.Replay"`, `DebriefEnd = "UI.Debrief.End"`, `DebriefSelectPrefix = "UI.Debrief.Select."` ("+ checkpoint id"),
  `PauseQuit = "UI.Pause.Quit"`, `PauseRestart = "UI.Pause.Restart"`.
- Why: WP07 (D007 after the title card, debrief flow), WP01 (re-arming the pointer-lock gesture when the gate is shown again) and WP11's smoke test listen to these; they cannot reference the UI assembly.
- Breaking? no (new constants).
- Status: requested (local adapter `LongShadow.UI.Common.UiEvents`).

## 2026-10-16 WP08 consumes two pending members through adapters
- What: no new request. WP08 reads `IPauseService.RelockCooldown` (requested by WP01) and `IEncounterService.Awareness01` (requested by WP04) through `LongShadow.UI.Common.UIAdapters.RelockCooldown` / `.Awareness01` (static delegates WP11 assigns in Integration). When those members land, switch `PauseMenu.RelockCooldown` and `DetectionMeters` to the interfaces and delete the adapters.
- Why: the UI may reference only Core.
- Breaking? no.
- Status: FYI

## 2026-10-16 WP07 mission-event ids in SequenceIds
- What: in `Runtime/SequenceIds.cs` under "mission events with listeners outside Mission" add (values must equal WP07's local table `LongShadow.Mission.Data.MissionEvents`; the ids WP10_LOOK, WP06 and WP08 already requested above are not repeated):
  `WeighbridgeOpen = "B2.GateOpen"` ("Beat 2: the chain is cut and the Weighbridge gate swings open, 3 s after D020 starts; WP10 animates the gate"),
  `HallBreach = "B7.Breach"` ("Beat 7: the Hall door was kicked; 1.5 s slow-mo breach at 0.35 (WP06 pitches the breach mix ×0.7)"),
  `DrillStarted = "B9.DrillStart"` ("Beat 9: D060, Sparrow's drill starts; E9 wave timers count from here"),
  `HeliApproach = "B10.HeliApproach"`, `HeliWaveOff = "B10.HeliWaveOff"`, `HeliWheelsDown = "B10.WheelsDown"` ("Beat 10 Magpie: final approach at hold t 60, wave-off after the RPG shot, wheels down at t 75/95"),
  `ExfilStart = "B11.ExfilStart"`, `ExfilTrucks = "B11.Trucks"`, `ClimaxStart = "B11.Climax"` ("Beat 11: spline t 0, the Clock Square trucks at t 16, the RPG launch at t 30"),
  `Debrief = "Mission.Debrief"`, `QuitToSession = "Mission.QuitToSession"` ("the debrief opened / QUIT TO SESSION"),
  `SmokeDone = "Smoke.Done"` ("MissionSmokeTest finished; its JSON is `MissionSmokeTest.LastResultJson`; WP11 forwards it to `window.LS_SMOKE_RESULT`").
- Why: WP07 raises them; WP10 Level (gate swing, heli/truck splines), WP06 (breach mix, rotor/alarm), WP09 (climax) and WP11 (smoke result) listen; none can reference Mission. Until this lands everyone uses the same literals.
- Breaking? no (new constants).
- Status: requested (local table `LongShadow.Mission.Data.MissionEvents`).

## 2026-10-16 WP07 uses pending members through local adapters (no new request)
- What: (1) `IPlayerRig.SetEyeHeightOverride` (requested by WP02 above) is reached through the local interface `LongShadow.Mission.Runtime.IPlayerPosture` (same signature) that WP11's Integration registers in GameServices with a one-line forwarder to `PlayerRig.SetEyeHeightOverride`; when the Core member lands, `MissionContext.SetPosture` should call `IPlayerRig` directly and the adapter goes. (2) The WP05 action ids `Kneel`, `Stop`, `CheckWatch` (requested above as ExtraActionIds) are used as literals in `LongShadow.Mission.Data.MissionActionIds`. (3) `SequenceIds.GlassedPrefix` (WP02) is mirrored as `MissionEvents.GlassedPrefix`; the AI ids (WP04), audio ids (WP06), look ids (WP10_LOOK) and UI ids (WP08) are mirrored in `MissionEvents` with the same values.
- Why: Mission may reference only Core.
- Breaking? no.
- Status: FYI

## 2026-10-16 WP09 cinematic seen-flag prefix and screen-FX extras
- What: (1) in `Runtime/StorageKeys.cs` add `public const string CinematicSeenPrefix = "ls.cine.";` — doc: "Prefix of the 'seen once' flag of a sequence other than the intro (\"ls.cine.Outro\" = \"1\"); enables hold-to-skip after the first viewing (GDD §8)." (2) optional, in `Interfaces/IScreenFX.cs` add `void FadeToWhite(float seconds);` — doc: "Fades to white over seconds (unscaled): the climax cut to white (GDD §8.7)."
- Why: (1) WP09 SeenFlags uses the literal (local adapter `LongShadow.Cinematics.SeenFlags.Prefix`); WP08's options/debrief may want to reset the flags. (2) WP07's fallback climax (without cinematics) could cut to white; WP09's ScreenFXService already implements `FadeToWhite` publicly.
- Breaking? no (new constant; new interface member implemented only by WP09's ScreenFXService).
- Status: requested (local adapters in place).

## 2026-10-16 18:00 WP10_LEVEL awning mission-event id in SequenceIds
- What: in `Runtime/SequenceIds.cs` under "mission events with listeners outside Mission" add `public const string AwningTorn = "B3.AwningTorn";` — doc: "Beat 3: the first market awning was torn by tracers (raised once by WP10 Level's AwningPanel; WP07 plays D023)". The other ids Level listens to are already requested (WP07: B2.GateOpen, B11.ExfilStart, B11.Trucks; WP04: B4.TechnicalIgnited, B6.Blast; WP10_LOOK: B6.FloodsOn, B9.TransmitterDead); `MIDA.Start` exists.
- Why: Level raises it, Mission listens; neither may reference the other.
- Breaking? no (new constant).
- Status: requested (local adapter `LongShadow.World.Level.Data.LevelEvents`).
