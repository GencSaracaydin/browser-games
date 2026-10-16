// Core contract (WP00). Changes are additive only and go through Docs/CORE_CHANGES.md (TECH_PLAN §6).
using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Global game events (TECH_PLAN §6.3). Subscribe in OnEnable, unsubscribe in OnDisable. Raise through the
    /// <c>Raise*</c> methods only. Handlers must not throw (an exception propagates to the raiser). High-frequency data
    /// (spread, health) is polled through interfaces, not sent as events. Every event resets on SubsystemRegistration.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>The player took damage (direction for the HUD damage indicator).</summary>
        public static event Action<DamageInfo> PlayerDamaged;
        /// <summary>The player died (MissionDirector starts the death flow).</summary>
        public static event Action<DamageInfo> PlayerDied;
        /// <summary>The player was restored at a checkpoint.</summary>
        public static event Action PlayerRespawned;
        /// <summary>Any damageable other than the player took damage.</summary>
        public static event Action<IDamageable, DamageInfo> ActorDamaged;
        /// <summary>Any actor died (stats, morale, barks, encounters).</summary>
        public static event Action<KillInfo> ActorKilled;
        /// <summary>A weapon fired (AI hearing, stats, squad callouts).</summary>
        public static event Action<ShotEvent> ShotFired;
        /// <summary>A bullet passed within 3 m of the player's head.</summary>
        public static event Action<NearMissEvent> NearMiss;
        /// <summary>A sound AI can hear.</summary>
        public static event Action<NoiseEvent> Noise;
        /// <summary>A tracer was drawn (awnings tear, G3).</summary>
        public static event Action<Vector3, Vector3, Team> TracerPath;
        /// <summary>A grenade left a hand (live grenade for checkpoint deferral).</summary>
        public static event Action<Vector3, Team> GrenadeThrown;
        /// <summary>A live grenade came to rest (HUD grenade indicator within 8 m, AI flee).</summary>
        public static event Action<Vector3, Team> GrenadeLanded;
        /// <summary>A grenade exploded (ends the live-grenade state).</summary>
        public static event Action<Vector3, float> GrenadeExploded;
        /// <summary>Any explosion (grenade, rocket, satchel, vehicle): shake, tinnitus, AI noise.</summary>
        public static event Action<Vector3, float, Team> Explosion;
        /// <summary>A player shot hit a damageable (hit markers).</summary>
        public static event Action<bool, bool> HitConfirmed;
        /// <summary>The player's current weapon changed.</summary>
        public static event Action<string> WeaponChanged;
        /// <summary>The player's ammo or frags changed.</summary>
        public static event Action<int, int, int> AmmoChanged;
        /// <summary>A TriggerVolume was entered (id = its marker id).</summary>
        public static event Action<string, Collider> TriggerEntered;
        /// <summary>A TriggerVolume was exited.</summary>
        public static event Action<string, Collider> TriggerExited;
        /// <summary>A named mission event ("MIDA.Start", ...).</summary>
        public static event Action<string> MissionEvent;
        /// <summary>A beat started.</summary>
        public static event Action<int> BeatStarted;
        /// <summary>A beat completed.</summary>
        public static event Action<int> BeatCompleted;
        /// <summary>The objective list changed.</summary>
        public static event Action ObjectivesChanged;
        /// <summary>An objective completed.</summary>
        public static event Action<string> ObjectiveCompleted;
        /// <summary>The marker list changed.</summary>
        public static event Action MarkersChanged;
        /// <summary>An intel item was collected.</summary>
        public static event Action<string> IntelCollected;
        /// <summary>A checkpoint snapshot was committed.</summary>
        public static event Action<string> CheckpointSaved;
        /// <summary>A checkpoint was restored.</summary>
        public static event Action<string> CheckpointLoaded;
        /// <summary>LightStage s changed by at least 0.01.</summary>
        public static event Action<float> LightStageChanged;
        /// <summary>LightStage entered a new named stage.</summary>
        public static event Action<StageId> StageChanged;
        /// <summary>Sodium practicals strike at s 1.97 (MID-A).</summary>
        public static event Action PracticalsStrike;
        /// <summary>A VO line started (look-at, jaw flap).</summary>
        public static event Action<string, string> VOLineStarted;
        /// <summary>A VO line finished or was stopped.</summary>
        public static event Action<string> VOLineFinished;
        /// <summary>Show a subtitle.</summary>
        public static event Action<SubtitleData> SubtitleShow;
        /// <summary>Hide a subtitle by handle.</summary>
        public static event Action<int> SubtitleHide;
        /// <summary>Pause state changed.</summary>
        public static event Action<bool> PauseChanged;
        /// <summary>Browser pointer lock changed.</summary>
        public static event Action<bool> PointerLockChanged;
        /// <summary>A glitch insert started.</summary>
        public static event Action<string> InsertStarted;
        /// <summary>A glitch insert ended.</summary>
        public static event Action<string> InsertEnded;
        /// <summary>A cinematic started.</summary>
        public static event Action<string> CinematicStarted;
        /// <summary>A cinematic ended.</summary>
        public static event Action<string> CinematicEnded;
        /// <summary>A section scene finished loading (markers registered, probes tetrahedralised).</summary>
        public static event Action<SectionId> SectionLoaded;
        /// <summary>A section scene unloaded.</summary>
        public static event Action<SectionId> SectionUnloaded;
        /// <summary>Options changed.</summary>
        public static event Action SettingsChanged;
        /// <summary>The listener entered a different acoustic zone (ZoneTag).</summary>
        public static event Action<ZoneType> ZoneChanged;
        /// <summary>A Destructible took damage.</summary>
        public static event Action<string, float> DestructibleDamaged;
        /// <summary>A Destructible was destroyed (floods, racks, trucks).</summary>
        public static event Action<string> DestructibleDestroyed;
        /// <summary>A GateMarker opened or closed.</summary>
        public static event Action<string, bool> GateChanged;
        /// <summary>An <see cref="InteractionPoint"/> was used (id = its marker id).</summary>
        public static event Action<string> Interacted;
        /// <summary>An encounter was activated (WP04 EncounterSpawner).</summary>
        public static event Action<string> EncounterActivated;
        /// <summary>A wave of an encounter spawned (encounter id, wave id).</summary>
        public static event Action<string, string> WaveSpawned;
        /// <summary>Every wave of an encounter is spawned and dead.</summary>
        public static event Action<string> EncounterCleared;
        /// <summary>
        /// True when the first enemy enters combat (aware of the player or squad), false when none is left in combat
        /// (WP04). Music layers, ambience ducking and civilian emitters listen.
        /// </summary>
        public static event Action<bool> CombatStateChanged;
        /// <summary>The player mounted (true) or left (false) a seat or the door gun.</summary>
        public static event Action<bool> PlayerMountChanged;

        /// <summary>Raises <see cref="PlayerDamaged"/>.</summary>
        public static void RaisePlayerDamaged(in DamageInfo d) => PlayerDamaged?.Invoke(d);
        /// <summary>Raises <see cref="PlayerDied"/>.</summary>
        public static void RaisePlayerDied(in DamageInfo d) => PlayerDied?.Invoke(d);
        /// <summary>Raises <see cref="PlayerRespawned"/>.</summary>
        public static void RaisePlayerRespawned() => PlayerRespawned?.Invoke();
        /// <summary>Raises <see cref="ActorDamaged"/>.</summary>
        public static void RaiseActorDamaged(IDamageable victim, in DamageInfo d) => ActorDamaged?.Invoke(victim, d);
        /// <summary>Raises <see cref="ActorKilled"/>.</summary>
        public static void RaiseActorKilled(in KillInfo k) => ActorKilled?.Invoke(k);
        /// <summary>Raises <see cref="ShotFired"/>.</summary>
        public static void RaiseShotFired(in ShotEvent s) => ShotFired?.Invoke(s);
        /// <summary>Raises <see cref="NearMiss"/>.</summary>
        public static void RaiseNearMiss(in NearMissEvent e) => NearMiss?.Invoke(e);
        /// <summary>Raises <see cref="Noise"/>.</summary>
        public static void RaiseNoise(in NoiseEvent e) => Noise?.Invoke(e);
        /// <summary>Raises <see cref="TracerPath"/>.</summary>
        public static void RaiseTracerPath(Vector3 from, Vector3 to, Team team) => TracerPath?.Invoke(from, to, team);
        /// <summary>Raises <see cref="GrenadeThrown"/>.</summary>
        public static void RaiseGrenadeThrown(Vector3 from, Team team) => GrenadeThrown?.Invoke(from, team);
        /// <summary>Raises <see cref="GrenadeLanded"/>.</summary>
        public static void RaiseGrenadeLanded(Vector3 position, Team team) => GrenadeLanded?.Invoke(position, team);
        /// <summary>Raises <see cref="GrenadeExploded"/>.</summary>
        public static void RaiseGrenadeExploded(Vector3 position, float radius) => GrenadeExploded?.Invoke(position, radius);
        /// <summary>Raises <see cref="Explosion"/>.</summary>
        public static void RaiseExplosion(Vector3 position, float maxRadius, Team team) => Explosion?.Invoke(position, maxRadius, team);
        /// <summary>Raises <see cref="HitConfirmed"/>.</summary>
        public static void RaiseHitConfirmed(bool kill, bool headshot) => HitConfirmed?.Invoke(kill, headshot);
        /// <summary>Raises <see cref="WeaponChanged"/>.</summary>
        public static void RaiseWeaponChanged(string weaponId) => WeaponChanged?.Invoke(weaponId);
        /// <summary>Raises <see cref="AmmoChanged"/>.</summary>
        public static void RaiseAmmoChanged(int mag, int reserve, int frags) => AmmoChanged?.Invoke(mag, reserve, frags);
        /// <summary>Raises <see cref="TriggerEntered"/>.</summary>
        public static void RaiseTriggerEntered(string id, Collider other) => TriggerEntered?.Invoke(id, other);
        /// <summary>Raises <see cref="TriggerExited"/>.</summary>
        public static void RaiseTriggerExited(string id, Collider other) => TriggerExited?.Invoke(id, other);
        /// <summary>Raises <see cref="MissionEvent"/>.</summary>
        public static void RaiseMissionEvent(string id) => MissionEvent?.Invoke(id);
        /// <summary>Raises <see cref="BeatStarted"/>.</summary>
        public static void RaiseBeatStarted(int beat) => BeatStarted?.Invoke(beat);
        /// <summary>Raises <see cref="BeatCompleted"/>.</summary>
        public static void RaiseBeatCompleted(int beat) => BeatCompleted?.Invoke(beat);
        /// <summary>Raises <see cref="ObjectivesChanged"/>.</summary>
        public static void RaiseObjectivesChanged() => ObjectivesChanged?.Invoke();
        /// <summary>Raises <see cref="ObjectiveCompleted"/>.</summary>
        public static void RaiseObjectiveCompleted(string id) => ObjectiveCompleted?.Invoke(id);
        /// <summary>Raises <see cref="MarkersChanged"/>.</summary>
        public static void RaiseMarkersChanged() => MarkersChanged?.Invoke();
        /// <summary>Raises <see cref="IntelCollected"/>.</summary>
        public static void RaiseIntelCollected(string id) => IntelCollected?.Invoke(id);
        /// <summary>Raises <see cref="CheckpointSaved"/>.</summary>
        public static void RaiseCheckpointSaved(string cpId) => CheckpointSaved?.Invoke(cpId);
        /// <summary>Raises <see cref="CheckpointLoaded"/>.</summary>
        public static void RaiseCheckpointLoaded(string cpId) => CheckpointLoaded?.Invoke(cpId);
        /// <summary>Raises <see cref="LightStageChanged"/>.</summary>
        public static void RaiseLightStageChanged(float s) => LightStageChanged?.Invoke(s);
        /// <summary>Raises <see cref="StageChanged"/>.</summary>
        public static void RaiseStageChanged(StageId stage) => StageChanged?.Invoke(stage);
        /// <summary>Raises <see cref="PracticalsStrike"/>.</summary>
        public static void RaisePracticalsStrike() => PracticalsStrike?.Invoke();
        /// <summary>Raises <see cref="VOLineStarted"/>.</summary>
        public static void RaiseVOLineStarted(string lineId, string speakerId) => VOLineStarted?.Invoke(lineId, speakerId);
        /// <summary>Raises <see cref="VOLineFinished"/>.</summary>
        public static void RaiseVOLineFinished(string lineId) => VOLineFinished?.Invoke(lineId);
        /// <summary>Raises <see cref="SubtitleShow"/>.</summary>
        public static void RaiseSubtitleShow(in SubtitleData data) => SubtitleShow?.Invoke(data);
        /// <summary>Raises <see cref="SubtitleHide"/>.</summary>
        public static void RaiseSubtitleHide(int handle) => SubtitleHide?.Invoke(handle);
        /// <summary>Raises <see cref="PauseChanged"/>.</summary>
        public static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);
        /// <summary>Raises <see cref="PointerLockChanged"/>.</summary>
        public static void RaisePointerLockChanged(bool locked) => PointerLockChanged?.Invoke(locked);
        /// <summary>Raises <see cref="InsertStarted"/>.</summary>
        public static void RaiseInsertStarted(string insertId) => InsertStarted?.Invoke(insertId);
        /// <summary>Raises <see cref="InsertEnded"/>.</summary>
        public static void RaiseInsertEnded(string insertId) => InsertEnded?.Invoke(insertId);
        /// <summary>Raises <see cref="CinematicStarted"/>.</summary>
        public static void RaiseCinematicStarted(string id) => CinematicStarted?.Invoke(id);
        /// <summary>Raises <see cref="CinematicEnded"/>.</summary>
        public static void RaiseCinematicEnded(string id) => CinematicEnded?.Invoke(id);
        /// <summary>Raises <see cref="SectionLoaded"/>.</summary>
        public static void RaiseSectionLoaded(SectionId section) => SectionLoaded?.Invoke(section);
        /// <summary>Raises <see cref="SectionUnloaded"/>.</summary>
        public static void RaiseSectionUnloaded(SectionId section) => SectionUnloaded?.Invoke(section);
        /// <summary>Raises <see cref="SettingsChanged"/>.</summary>
        public static void RaiseSettingsChanged() => SettingsChanged?.Invoke();
        /// <summary>Raises <see cref="ZoneChanged"/>.</summary>
        public static void RaiseZoneChanged(ZoneType zone) => ZoneChanged?.Invoke(zone);
        /// <summary>Raises <see cref="DestructibleDamaged"/>.</summary>
        public static void RaiseDestructibleDamaged(string id, float health01) => DestructibleDamaged?.Invoke(id, health01);
        /// <summary>Raises <see cref="DestructibleDestroyed"/>.</summary>
        public static void RaiseDestructibleDestroyed(string id) => DestructibleDestroyed?.Invoke(id);
        /// <summary>Raises <see cref="GateChanged"/>.</summary>
        public static void RaiseGateChanged(string id, bool open) => GateChanged?.Invoke(id, open);
        /// <summary>Raises <see cref="Interacted"/>.</summary>
        public static void RaiseInteracted(string id) => Interacted?.Invoke(id);
        /// <summary>Raises <see cref="EncounterActivated"/>.</summary>
        public static void RaiseEncounterActivated(string encounterId) => EncounterActivated?.Invoke(encounterId);
        /// <summary>Raises <see cref="WaveSpawned"/>.</summary>
        public static void RaiseWaveSpawned(string encounterId, string wave) => WaveSpawned?.Invoke(encounterId, wave);
        /// <summary>Raises <see cref="EncounterCleared"/>.</summary>
        public static void RaiseEncounterCleared(string encounterId) => EncounterCleared?.Invoke(encounterId);
        /// <summary>Raises <see cref="CombatStateChanged"/>.</summary>
        public static void RaiseCombatStateChanged(bool inCombat) => CombatStateChanged?.Invoke(inCombat);
        /// <summary>Raises <see cref="PlayerMountChanged"/>.</summary>
        public static void RaisePlayerMountChanged(bool mounted) => PlayerMountChanged?.Invoke(mounted);

        /// <summary>Clears every subscriber (domain reload is disabled; also used by tests).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetAll()
        {
            PlayerDamaged = null;
            PlayerDied = null;
            PlayerRespawned = null;
            ActorDamaged = null;
            ActorKilled = null;
            ShotFired = null;
            NearMiss = null;
            Noise = null;
            TracerPath = null;
            GrenadeThrown = null;
            GrenadeLanded = null;
            GrenadeExploded = null;
            Explosion = null;
            HitConfirmed = null;
            WeaponChanged = null;
            AmmoChanged = null;
            TriggerEntered = null;
            TriggerExited = null;
            MissionEvent = null;
            BeatStarted = null;
            BeatCompleted = null;
            ObjectivesChanged = null;
            ObjectiveCompleted = null;
            MarkersChanged = null;
            IntelCollected = null;
            CheckpointSaved = null;
            CheckpointLoaded = null;
            LightStageChanged = null;
            StageChanged = null;
            PracticalsStrike = null;
            VOLineStarted = null;
            VOLineFinished = null;
            SubtitleShow = null;
            SubtitleHide = null;
            PauseChanged = null;
            PointerLockChanged = null;
            InsertStarted = null;
            InsertEnded = null;
            CinematicStarted = null;
            CinematicEnded = null;
            SectionLoaded = null;
            SectionUnloaded = null;
            SettingsChanged = null;
            ZoneChanged = null;
            DestructibleDamaged = null;
            DestructibleDestroyed = null;
            GateChanged = null;
            Interacted = null;
            EncounterActivated = null;
            WaveSpawned = null;
            EncounterCleared = null;
            CombatStateChanged = null;
            PlayerMountChanged = null;
        }
    }
}
