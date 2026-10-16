using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// What happened during the mission, for the beat scripts' waits: every relevant <see cref="GameEvents"/> by channel
    /// and id with a sequence number and a timestamp (runner time), the player's trigger occupancy, and shot bookkeeping
    /// for Beat 2's silent opener. Subscribed by the MissionDirector (<see cref="Attach"/>/<see cref="Detach"/>); the
    /// tests feed it through the same GameEvents. Allocation: only on events (never per frame).
    /// </summary>
    public sealed class MissionEventLog
    {
        private struct Entry
        {
            public int Seq;
            public float Time;
            public int Count;
        }

        /// <summary>Id recorded on <see cref="EventChannel.Mission"/> when LightStage raises <see cref="GameEvents.PracticalsStrike"/>.</summary>
        public const string PracticalsStrikeId = "LightStage.PracticalsStrike";

        private readonly Dictionary<string, Entry>[] _channels;
        private readonly Dictionary<string, int> _inside = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _lastEnemyShot = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Func<float> _clock;
        private bool _attached;

        /// <summary>Global sequence number (increases with every recorded event).</summary>
        public int Seq { get; private set; }
        /// <summary>Time of the player's last shot (any weapon), or -1.</summary>
        public float LastPlayerShotTime { get; private set; } = -1f;
        /// <summary>Sequence of the player's last shot, or 0.</summary>
        public int LastPlayerShotSeq { get; private set; }
        /// <summary>Time of the player's last unsuppressed shot, or -1.</summary>
        public float LastPlayerLoudShotTime { get; private set; } = -1f;
        /// <summary>Sequence of the player's last unsuppressed shot, or 0.</summary>
        public int LastPlayerLoudShotSeq { get; private set; }
        /// <summary>Time of the last unsuppressed shot by anyone (music cue "first loud shot", GDD §7.2), or -1.</summary>
        public float LastLoudShotTime { get; private set; } = -1f;
        /// <summary>Sequence of the last unsuppressed shot by anyone, or 0.</summary>
        public int LastLoudShotSeq { get; private set; }
        /// <summary>Sequence of the last hostile (Legion/Greycoat) shot, or 0.</summary>
        public int LastEnemyShotSeq { get; private set; }
        /// <summary>Live grenades (thrown and not yet exploded) for the checkpoint deferral rule (GDD §17).</summary>
        public int LiveGrenades { get; private set; }
        /// <summary>Time the last grenade was thrown (live grenades older than 8 s are assumed gone).</summary>
        public float LastGrenadeThrownTime { get; private set; } = -1f;

        /// <summary>Creates a log reading time from <paramref name="clock"/> (the runner's gameplay time).</summary>
        public MissionEventLog(Func<float> clock)
        {
            _clock = clock ?? (() => 0f);
            var n = Enum.GetValues(typeof(EventChannel)).Length;
            _channels = new Dictionary<string, Entry>[n];
            for (var i = 0; i < n; i++) _channels[i] = new Dictionary<string, Entry>(StringComparer.Ordinal);
        }

        /// <summary>Subscribes to GameEvents.</summary>
        public void Attach()
        {
            if (_attached) return;
            _attached = true;
            GameEvents.TriggerEntered += OnTriggerEntered;
            GameEvents.TriggerExited += OnTriggerExited;
            GameEvents.MissionEvent += OnMissionEvent;
            GameEvents.Interacted += OnInteracted;
            GameEvents.DestructibleDestroyed += OnDestroyed;
            GameEvents.VOLineStarted += OnLineStarted;
            GameEvents.VOLineFinished += OnLineFinished;
            GameEvents.ActorKilled += OnActorKilled;
            GameEvents.WaveSpawned += OnWaveSpawned;
            GameEvents.EncounterCleared += OnEncounterCleared;
            GameEvents.CinematicEnded += OnCinematicEnded;
            GameEvents.InsertEnded += OnInsertEnded;
            GameEvents.IntelCollected += OnIntel;
            GameEvents.ObjectiveCompleted += OnObjective;
            GameEvents.SectionLoaded += OnSectionLoaded;
            GameEvents.ShotFired += OnShotFired;
            GameEvents.GrenadeThrown += OnGrenadeThrown;
            GameEvents.GrenadeExploded += OnGrenadeExploded;
            GameEvents.PracticalsStrike += OnPracticalsStrike;
        }

        /// <summary>Unsubscribes from GameEvents.</summary>
        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            GameEvents.TriggerEntered -= OnTriggerEntered;
            GameEvents.TriggerExited -= OnTriggerExited;
            GameEvents.MissionEvent -= OnMissionEvent;
            GameEvents.Interacted -= OnInteracted;
            GameEvents.DestructibleDestroyed -= OnDestroyed;
            GameEvents.VOLineStarted -= OnLineStarted;
            GameEvents.VOLineFinished -= OnLineFinished;
            GameEvents.ActorKilled -= OnActorKilled;
            GameEvents.WaveSpawned -= OnWaveSpawned;
            GameEvents.EncounterCleared -= OnEncounterCleared;
            GameEvents.CinematicEnded -= OnCinematicEnded;
            GameEvents.InsertEnded -= OnInsertEnded;
            GameEvents.IntelCollected -= OnIntel;
            GameEvents.ObjectiveCompleted -= OnObjective;
            GameEvents.SectionLoaded -= OnSectionLoaded;
            GameEvents.ShotFired -= OnShotFired;
            GameEvents.GrenadeThrown -= OnGrenadeThrown;
            GameEvents.GrenadeExploded -= OnGrenadeExploded;
            GameEvents.PracticalsStrike -= OnPracticalsStrike;
        }

        /// <summary>Forgets everything (mission start, checkpoint reload).</summary>
        public void Clear()
        {
            for (var i = 0; i < _channels.Length; i++) _channels[i].Clear();
            _inside.Clear();
            _lastEnemyShot.Clear();
            LastPlayerShotTime = LastPlayerLoudShotTime = LastLoudShotTime = -1f;
            LastPlayerShotSeq = LastPlayerLoudShotSeq = LastLoudShotSeq = LastEnemyShotSeq = 0;
            LiveGrenades = 0;
            LastGrenadeThrownTime = -1f;
        }

        /// <summary>Records an event (the GameEvents handlers and the trigger fallback call this).</summary>
        public void Record(EventChannel channel, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var d = _channels[(int)channel];
            d.TryGetValue(id, out var e);
            e.Seq = ++Seq;
            e.Time = _clock();
            e.Count++;
            d[id] = e;
        }

        /// <summary>True if the event happened after sequence <paramref name="sinceSeq"/>.</summary>
        public bool Happened(EventChannel channel, string id, int sinceSeq = 0) =>
            id != null && _channels[(int)channel].TryGetValue(id, out var e) && e.Seq > sinceSeq;

        /// <summary>Time of the last occurrence (runner time), or -1.</summary>
        public float LastTime(EventChannel channel, string id) =>
            id != null && _channels[(int)channel].TryGetValue(id, out var e) ? e.Time : -1f;

        /// <summary>Sequence of the last occurrence, or 0.</summary>
        public int LastSeq(EventChannel channel, string id) =>
            id != null && _channels[(int)channel].TryGetValue(id, out var e) ? e.Seq : 0;

        /// <summary>How often the event happened since the last <see cref="Clear"/>.</summary>
        public int Count(EventChannel channel, string id) =>
            id != null && _channels[(int)channel].TryGetValue(id, out var e) ? e.Count : 0;

        /// <summary>Every id recorded on a channel (tests and diagnostics; allocates).</summary>
        public List<string> Ids(EventChannel channel) => new List<string>(_channels[(int)channel].Keys);

        /// <summary>True while the player is inside the trigger (enter/exit bookkeeping).</summary>
        public bool IsInside(string triggerId) => triggerId != null && _inside.TryGetValue(triggerId, out var n) && n > 0;

        /// <summary>Marks the player inside or outside a trigger without an event (checkpoint teleports, fallbacks).</summary>
        public void SetInside(string triggerId, bool inside)
        {
            if (string.IsNullOrEmpty(triggerId)) return;
            if (inside)
            {
                if (IsInside(triggerId)) return;
                _inside[triggerId] = 1;
                Record(EventChannel.TriggerEnter, triggerId);
            }
            else if (_inside.Remove(triggerId))
            {
                Record(EventChannel.TriggerExit, triggerId);
            }
        }

        /// <summary>Forgets the trigger occupancy (teleports: the player left every trigger without exit events).</summary>
        public void ResetOccupancy() => _inside.Clear();

        /// <summary>Time of the last shot of a hostile weapon id (e.g. the RPK on the truck), or -1.</summary>
        public float LastEnemyShot(string weaponId) =>
            weaponId != null && _lastEnemyShot.TryGetValue(weaponId, out var t) ? t : -1f;

        /// <summary>True if a grenade is live (GDD §17 deferral); grenades older than 8 s are assumed gone.</summary>
        public bool GrenadeLive => LiveGrenades > 0 && _clock() - LastGrenadeThrownTime < 8f;

        private void OnTriggerEntered(string id, Collider c)
        {
            if (c != null && !TriggerVolume.IsPlayer(c)) return;
            _inside.TryGetValue(id ?? string.Empty, out var n);
            if (id != null) _inside[id] = n + 1;
            Record(EventChannel.TriggerEnter, id);
        }

        private void OnTriggerExited(string id, Collider c)
        {
            if (c != null && !TriggerVolume.IsPlayer(c)) return;
            if (id != null && _inside.TryGetValue(id, out var n))
            {
                if (n <= 1) _inside.Remove(id);
                else _inside[id] = n - 1;
            }
            Record(EventChannel.TriggerExit, id);
        }

        private void OnMissionEvent(string id) => Record(EventChannel.Mission, id);
        private void OnInteracted(string id) => Record(EventChannel.Interacted, id);
        private void OnDestroyed(string id) => Record(EventChannel.Destroyed, id);
        private void OnLineStarted(string lineId, string speaker) => Record(EventChannel.LineStarted, lineId);
        private void OnLineFinished(string lineId) => Record(EventChannel.LineFinished, lineId);
        private void OnActorKilled(KillInfo k) => Record(EventChannel.Killed, k.VictimId);
        private void OnWaveSpawned(string encounter, string wave) => Record(EventChannel.WaveSpawned, WaveKey(encounter, wave));
        private void OnEncounterCleared(string id) => Record(EventChannel.EncounterCleared, id);
        private void OnCinematicEnded(string id) => Record(EventChannel.CinematicEnded, id);
        private void OnInsertEnded(string id) => Record(EventChannel.InsertEnded, id);
        private void OnIntel(string id) => Record(EventChannel.Intel, id);
        private void OnObjective(string id) => Record(EventChannel.Objective, id);
        private void OnSectionLoaded(SectionId s) => Record(EventChannel.SectionLoaded, Sections.SceneName(s));

        private void OnShotFired(ShotEvent s)
        {
            var now = _clock();
            var suppressed = s.WeaponId == WeaponIds.HushPuppy;
            if (s.IsPlayer)
            {
                LastPlayerShotTime = now;
                LastPlayerShotSeq = ++Seq;
                if (!suppressed)
                {
                    LastPlayerLoudShotTime = now;
                    LastPlayerLoudShotSeq = Seq;
                }
            }
            else if (Teams.IsEnemySide(s.Team))
            {
                LastEnemyShotSeq = ++Seq;
                if (s.WeaponId != null) _lastEnemyShot[s.WeaponId] = now;
            }
            if (!suppressed)
            {
                LastLoudShotTime = now;
                LastLoudShotSeq = s.IsPlayer ? LastPlayerShotSeq : ++Seq;
            }
        }

        private void OnPracticalsStrike() => Record(EventChannel.Mission, PracticalsStrikeId);

        private void OnGrenadeThrown(Vector3 from, Team team)
        {
            LiveGrenades++;
            LastGrenadeThrownTime = _clock();
        }

        private void OnGrenadeExploded(Vector3 pos, float radius)
        {
            if (LiveGrenades > 0) LiveGrenades--;
        }

        /// <summary>Key of a wave on <see cref="EventChannel.WaveSpawned"/> ("E3/B").</summary>
        public static string WaveKey(string encounter, string wave) => encounter + "/" + wave;
    }
}
