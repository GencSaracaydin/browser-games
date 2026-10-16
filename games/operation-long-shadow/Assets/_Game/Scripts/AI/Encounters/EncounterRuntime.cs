using System.Collections.Generic;

namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// The running state of one <see cref="EncounterDef"/> (pure C#, driven by <see cref="EncounterSpawner"/> at 5 Hz and
    /// by tests through a fake <see cref="ISpawnHost"/>): wave triggers and alert rules (<see cref="WaveTrigger"/>), the
    /// spawn queue under the encounter's max alive and the hard cap of 12 (extra spawns queue, GDD §7), morale breaks
    /// (<see cref="MoraleRule"/>), kill counts, the cleared edge, and the condition context (events seen since
    /// activation, trigger occupancy, wave and death times). Killed ids restored from a checkpoint start dead.
    /// </summary>
    public sealed class EncounterRuntime : IConditionContext
    {
        private readonly ISpawnHost _host;
        private readonly List<EnemySlot> _slots = new List<EnemySlot>();
        private readonly List<EnemySlot> _queue = new List<EnemySlot>();
        private readonly Dictionary<string, float> _events = new Dictionary<string, float>();
        private readonly Dictionary<string, WaveDef> _extraWaves = new Dictionary<string, WaveDef>();
        private readonly HashSet<string> _started = new HashSet<string>();
        private readonly HashSet<string> _spawnedWaves = new HashSet<string>();
        private readonly HashSet<string> _alertedWaves = new HashSet<string>();
        private readonly HashSet<string> _broken = new HashSet<string>();
        private bool _alertAll;
        private bool _queueDirty;
        private float _now;

        private static readonly System.Comparison<EnemySlot> s_queueOrder = (a, b) =>
        {
            var c = a.DueAt.CompareTo(b.DueAt);
            if (c != 0) return c;
            c = a.Def.QueuePriority.CompareTo(b.Def.QueuePriority);
            return c != 0 ? c : a.Order.CompareTo(b.Order);
        };

        /// <summary>Creates the runtime; <paramref name="killedIds"/> start dead (checkpoint restore).</summary>
        public EncounterRuntime(EncounterDef def, ISpawnHost host, ICollection<string> killedIds = null)
        {
            Def = def;
            _host = host;
            for (var i = 0; i < def.Enemies.Count; i++)
            {
                var e = def.Enemies[i];
                var wave = def.GetWave(e.Wave);
                if (wave == null && !_extraWaves.TryGetValue(e.Wave, out wave))
                {
                    wave = new WaveDef(e.Wave, WaveTrigger.Manual());
                    _extraWaves[e.Wave] = wave;
                }
                var slot = new EnemySlot(this, e, wave, i);
                if (killedIds != null && killedIds.Contains(e.Id)) slot.State = SlotState.Dead;
                _slots.Add(slot);
            }
        }

        /// <summary>Definition.</summary>
        public EncounterDef Def { get; }
        /// <summary>True between <see cref="Activate"/> and <see cref="Deactivate"/>.</summary>
        public bool Active { get; private set; }
        /// <summary>True once the cleared edge fired.</summary>
        public bool ClearedRaised { get; private set; }
        /// <summary>Every slot in table order.</summary>
        public IReadOnlyList<EnemySlot> Slots => _slots;

        // ------------------------------------------------------------------ IConditionContext

        /// <inheritdoc/>
        public float Now => _now;
        /// <inheritdoc/>
        public float ActivatedAt { get; private set; }
        /// <inheritdoc/>
        public bool EventTime(string eventId, out float time)
        {
            if (eventId != null && _events.TryGetValue(eventId, out time)) return true;
            time = 0f;
            return false;
        }
        /// <inheritdoc/>
        public bool PlayerInside(string triggerId) => _host != null && _host.PlayerInside(triggerId);
        /// <inheritdoc/>
        public bool WaveStarted(string wave) => wave != null && _started.Contains(wave);
        /// <inheritdoc/>
        public bool WaveAllSpawned(string wave)
        {
            if (!WaveStarted(wave)) return false;
            for (var i = 0; i < _slots.Count; i++)
                if (_slots[i].Def.Wave == wave && !_slots[i].Resolved) return false;
            return true;
        }
        /// <inheritdoc/>
        public int WaveAlive(string wave) => Alive(wave);
        /// <inheritdoc/>
        public bool WaveDead(string wave)
        {
            if (!WaveStarted(wave)) return false;
            for (var i = 0; i < _slots.Count; i++)
                if (_slots[i].Def.Wave == wave && !_slots[i].Done) return false;
            return true;
        }
        /// <inheritdoc/>
        public int Killed
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _slots.Count; i++)
                    if (_slots[i].State == SlotState.Dead) n++;
                return n;
            }
        }
        /// <inheritdoc/>
        public bool EnemyDeathTime(string enemyId, out float time)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.Def.Id != enemyId || s.State != SlotState.Dead) continue;
                time = s.DiedAt;
                return true;
            }
            time = 0f;
            return false;
        }

        // ------------------------------------------------------------------ counts

        /// <summary>Living members (optionally of one wave).</summary>
        public int Alive(string wave = null)
        {
            var n = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.State == SlotState.Alive && (wave == null || s.Def.Wave == wave)) n++;
            }
            return n;
        }

        /// <summary>Queued members (waiting for delay, caps or a closet).</summary>
        public int QueuedCount => _queue.Count;

        /// <summary>True when every wave has started and every member is dead or despawned.</summary>
        public bool Cleared
        {
            get
            {
                for (var i = 0; i < _slots.Count; i++)
                    if (!_slots[i].Done) return false;
                return true;
            }
        }

        /// <summary>A slot by enemy id, or null.</summary>
        public EnemySlot Find(string enemyId)
        {
            for (var i = 0; i < _slots.Count; i++)
                if (_slots[i].Def.Id == enemyId) return _slots[i];
            return null;
        }

        // ------------------------------------------------------------------ control

        /// <summary>Activates (idempotent): arms the waves and evaluates them at once.</summary>
        public void Activate(float now)
        {
            if (Active) return;
            Active = true;
            ActivatedAt = now;
            _now = now;
            _events.Clear();
            Tick(now);
        }

        /// <summary>Despawns the living, drops the queue and stops (stragglers, skipped beats).</summary>
        public void Deactivate()
        {
            if (!Active) return;
            Active = false;
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.State == SlotState.Alive)
                {
                    _host?.Despawn(s);
                    s.Instance = null;
                    s.State = SlotState.Despawned;
                }
                else if (s.State == SlotState.Queued || s.State == SlotState.Pending)
                {
                    s.State = SlotState.Despawned;
                }
            }
            _queue.Clear();
        }

        /// <summary>Records a named event (mission event, trigger, interaction, destructible, "VO:Dxxx").</summary>
        public void RecordEvent(string id, float now)
        {
            if (!Active || string.IsNullOrEmpty(id)) return;
            _events[id] = now;
        }

        /// <summary>Starts a wave: its pending members queue with their delays. Manual starts ignore the alive gate.</summary>
        public bool StartWave(string waveId, float now, bool manual)
        {
            if (string.IsNullOrEmpty(waveId) || _started.Contains(waveId)) return false;
            var wave = Def.GetWave(waveId);
            if (wave == null && !_extraWaves.TryGetValue(waveId, out wave)) return false;
            if (!manual && wave.GateMaxAlive >= 0 && Alive() > wave.GateMaxAlive) return false;
            _started.Add(waveId);
            var alerted = wave.StartAlerted || _alertAll || _alertedWaves.Contains(waveId);
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.Def.Wave != waveId || s.State != SlotState.Pending) continue;
                s.State = SlotState.Queued;
                s.QueuedAt = now;
                s.DueAt = now + s.Def.Delay;
                s.Alerted = alerted;
                s.WaitingSince = float.PositiveInfinity;
                _queue.Add(s);
                _queueDirty = true;
            }
            return true;
        }

        /// <summary>
        /// Alerts a wave (null = every wave): living members are alerted through the host, the rest spawn alerted.
        /// </summary>
        public void Alert(string waveId)
        {
            if (waveId == null) _alertAll = true;
            else _alertedWaves.Add(waveId);
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (waveId != null && s.Def.Wave != waveId) continue;
                if (s.State == SlotState.Alive)
                {
                    if (!s.Alerted)
                    {
                        s.Alerted = true;
                        _host?.OnAlerted(this, s);
                    }
                }
                else if (s.State == SlotState.Queued || s.State == SlotState.Pending) s.Alerted = true;
            }
        }

        /// <summary>Evaluates triggers, alert rules and the queue (5 Hz).</summary>
        public void Tick(float now)
        {
            _now = now;
            if (!Active) return;
            EvaluateWaves(now);
            EvaluateAlerts();
            ProcessQueue(now);
            if (!ClearedRaised && Cleared && _started.Count > 0)
            {
                ClearedRaised = true;
                _host?.OnCleared(this);
            }
        }

        private void EvaluateWaves(float now)
        {
            var waves = Def.Waves;
            for (var i = 0; i < waves.Count; i++)
            {
                var w = waves[i];
                if (_started.Contains(w.Id) || w.Trigger.IsManualOnly) continue;
                if (w.Trigger.Evaluate(this)) StartWave(w.Id, now, false);
            }
        }

        private void EvaluateAlerts()
        {
            var waves = Def.Waves;
            for (var i = 0; i < waves.Count; i++)
            {
                var w = waves[i];
                if (w.AlertOn == null || _alertedWaves.Contains(w.Id)) continue;
                if (w.AlertOn.Evaluate(this)) Alert(w.Id);
            }
        }

        private void ProcessQueue(float now)
        {
            if (_queue.Count == 0) return;
            if (_queueDirty)
            {
                _queue.Sort(s_queueOrder);
                _queueDirty = false;
            }
            for (var i = 0; i < _queue.Count; i++)
            {
                var s = _queue[i];
                if (s.State != SlotState.Queued)
                {
                    _queue.RemoveAt(i--);
                    continue;
                }
                if (s.DueAt > now) break;                                                       // sorted by due time
                var total = _host != null ? _host.AliveTotal : 0;
                // "Placed means the enemy exists when the beat loads" (GDD §7): placed members of the waves that start
                // with the encounter ignore its max alive (only E2 has more placed than its max); the hard cap of 12 holds.
                var cap = IsPlacedAtLoad(s) ? int.MaxValue : Def.MaxAlive;
                if (!SpawnRules.UnderCaps(Alive(), cap, total, EncounterTable.HardCap))
                {
                    if (total >= EncounterTable.HardCap) break;                                  // GDD §14.3
                    continue;                                                                   // GDD §7: extra spawns queue
                }
                var result = _host != null ? _host.TrySpawn(this, s, now) : SpawnResult.Failed;
                if (result == SpawnResult.Wait)
                {
                    if (float.IsPositiveInfinity(s.WaitingSince)) s.WaitingSince = now;
                    continue;
                }
                _queue.RemoveAt(i--);
                if (result == SpawnResult.Failed)
                {
                    s.State = SlotState.Despawned;
                    continue;
                }
                s.State = SlotState.Alive;
                s.SpawnedAt = now;
                if (_spawnedWaves.Add(s.Def.Wave)) _host?.OnWaveSpawned(this, s.Wave);
            }
        }

        /// <summary>True for a placed (or placed-perch) member queued when the encounter activated.</summary>
        public bool IsPlacedAtLoad(EnemySlot s) =>
            (s.Def.Kind == SpawnKind.Placed || s.Def.Kind == SpawnKind.Perch) && s.QueuedAt <= ActivatedAt + 1e-3f && s.Def.Delay <= 0f;

        /// <summary>Members that exist when the encounter activates (placed members of its starting waves).</summary>
        public int PlacedAtLoadCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _slots.Count; i++)
                {
                    var s = _slots[i];
                    if ((s.Def.Kind == SpawnKind.Placed || s.Def.Kind == SpawnKind.Perch) && s.Def.Delay <= 0f)
                    {
                        var w = Def.GetWave(s.Def.Wave);
                        if (w != null && w.Trigger.AnyOf.Count > 0 && w.Trigger.AnyOf[0].Length > 0 && w.Trigger.AnyOf[0][0].Kind == ConditionKind.Activated) n++;
                    }
                }
                return n;
            }
        }

        /// <summary>A member died: counts, death time and the morale check (GDD §14.1).</summary>
        public void NotifyDeath(EnemySlot slot, float now)
        {
            if (slot == null || slot.State == SlotState.Dead) return;
            slot.State = SlotState.Dead;
            slot.DiedAt = now;
            _now = now;
            var wave = slot.Def.Wave;
            var size = 0;
            var dead = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.Def.Wave != wave) continue;
                size++;
                if (s.State == SlotState.Dead) dead++;
            }
            if (MoraleRule.ShouldBreak(size, dead, _broken.Contains(wave)) && Alive(wave) > 0)
            {
                _broken.Add(wave);
                _host?.OnMoraleBreak(this, slot.Wave);
            }
        }

        /// <summary>A member left without dying (withdrawn marksman, straggler).</summary>
        public void NotifyDespawned(EnemySlot slot)
        {
            if (slot == null || slot.Done) return;
            slot.State = SlotState.Despawned;
            slot.Instance = null;
        }

        /// <summary>True once the wave broke (morale).</summary>
        public bool WaveBroken(string wave) => _broken.Contains(wave);
    }
}
