using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Checkpoints
{
    /// <summary>
    /// <see cref="ICheckpointService"/> (TECH_PLAN §5.5, D11; GDD §17): in-memory snapshots of every registered
    /// <see cref="ISaveable"/> plus every <see cref="InteractionPoint"/> in the scene, keyed by checkpoint id; restore in
    /// place, no scene reload. <see cref="Save"/> defers by the GDD §17 rules (<see cref="CheckpointDeferral"/>); the
    /// highest checkpoint reached goes to localStorage (<see cref="StorageKeys.HighestCheckpoint"/>). <see cref="Reload"/>
    /// hands over to the MissionDirector's reload flow (death insert, black, restore, teleport, resume).
    /// <para>Plain C#: the MissionDirector owns it, ticks it and registers it in GameServices.</para>
    /// </summary>
    public sealed class CheckpointService : ICheckpointService
    {
        private readonly List<ISaveable> _saveables = new List<ISaveable>(32);
        private readonly List<ISaveable> _captureSet = new List<ISaveable>(64);
        private readonly List<InteractionPoint> _points = new List<InteractionPoint>(16);
        private readonly Dictionary<string, CheckpointSnapshot> _snapshots = new Dictionary<string, CheckpointSnapshot>(StringComparer.Ordinal);
        private readonly Func<float> _clock;
        private readonly Func<CheckpointDeferral.Reason> _deferral;
        private readonly Action<bool> _reloadHandler;
        private ServiceRef<ISettingsService> _settings;
        private ServiceRef<IMarkerRegistry> _markers;
        private string _pending;
        private float _pendingSince;
        private CheckpointDeferral.Reason _lastReason;

        /// <summary>
        /// Creates the service. <paramref name="clock"/> is gameplay time; <paramref name="deferral"/> evaluates the GDD §17
        /// rule now; <paramref name="reloadHandler"/> runs the reload flow (argument: play the death insert).
        /// </summary>
        public CheckpointService(Func<float> clock, Func<CheckpointDeferral.Reason> deferral, Action<bool> reloadHandler)
        {
            _clock = clock ?? (() => 0f);
            _deferral = deferral ?? (() => CheckpointDeferral.Reason.None);
            _reloadHandler = reloadHandler;
        }

        /// <summary>Raised after a snapshot was committed (also GameEvents.CheckpointSaved).</summary>
        public event Action<string> Committed;

        /// <inheritdoc/>
        public string Last { get; private set; }
        /// <inheritdoc/>
        public bool SavePending => _pending != null;
        /// <summary>The pending checkpoint id, or null.</summary>
        public string Pending => _pending;
        /// <summary>Why the pending save waits (diagnostics).</summary>
        public CheckpointDeferral.Reason PendingReason => _lastReason;
        /// <summary>Registered saveables (tests, smoke test).</summary>
        public IReadOnlyList<ISaveable> Saveables => _saveables;
        /// <summary>When true, Save requests are ignored (during a reload).</summary>
        public bool Suspended { get; set; }

        /// <inheritdoc/>
        public void Register(ISaveable saveable)
        {
            if (saveable == null || _saveables.Contains(saveable)) return;
            _saveables.Add(saveable);
        }

        /// <inheritdoc/>
        public void Unregister(ISaveable saveable) => _saveables.Remove(saveable);

        /// <inheritdoc/>
        public void Save(string cpId)
        {
            if (string.IsNullOrEmpty(cpId) || Suspended) return;
            if (_pending != null && _pending != cpId)
            {
                // A newer checkpoint replaces an older pending one; commit the older first so it is not lost.
                Commit(_pending);
            }
            _pending = cpId;
            _pendingSince = _clock();
            Tick();
        }

        /// <summary>Commits a pending save when the deferral rule allows it (called every frame by the director).</summary>
        public void Tick()
        {
            if (_pending == null || Suspended) return;
            _lastReason = _deferral();
            if (!CheckpointDeferral.ShouldCommit(_lastReason, _clock() - _pendingSince))
            {
                LSLog.Verbose(LogCategory.Save, "Checkpoint " + _pending + " deferred: " + _lastReason);
                return;
            }
            Commit(_pending);
        }

        /// <summary>Commits a pending save now, whatever the rule says (before the next beat's first spawn, GDD §17).</summary>
        public void CommitPending()
        {
            if (_pending != null) Commit(_pending);
        }

        /// <summary>Drops a pending save (mission start, quit).</summary>
        public void CancelPending() => _pending = null;

        /// <inheritdoc/>
        public void Reload(bool deathInsert)
        {
            if (_reloadHandler == null)
            {
                LSLog.Warn(LogCategory.Save, "Checkpoint reload requested but no handler is set.");
                return;
            }
            _reloadHandler(deathInsert);
        }

        /// <summary>True if a snapshot exists for the checkpoint.</summary>
        public bool HasSnapshot(string cpId) => cpId != null && _snapshots.ContainsKey(cpId);

        /// <summary>The snapshot of a checkpoint, or null.</summary>
        public CheckpointSnapshot Snapshot(string cpId) => cpId != null && _snapshots.TryGetValue(cpId, out var s) ? s : null;

        /// <summary>Restores a snapshot into every saveable. Returns the number restored (0 when no snapshot).</summary>
        public int Restore(string cpId)
        {
            var snap = Snapshot(cpId);
            if (snap == null) return 0;
            BuildCaptureSet();
            var n = snap.RestoreInto(_captureSet);
            LSLog.Info(LogCategory.Save, "Restored " + cpId + " (" + n + " saveables)");
            return n;
        }

        /// <summary>Forgets every snapshot (mission start).</summary>
        public void ClearSnapshots()
        {
            _snapshots.Clear();
            _pending = null;
            Last = null;
        }

        private void Commit(string cpId)
        {
            _pending = null;
            BuildCaptureSet();
            var snap = Capture(cpId, _captureSet, _clock());
            _snapshots[cpId] = snap;
            Last = cpId;
            StoreHighest(cpId);
            LSLog.Info(LogCategory.Save, "Checkpoint " + cpId + " saved (" + snap.Records.Count + " records)");
            Committed?.Invoke(cpId);
            GameEvents.RaiseCheckpointSaved(cpId);
        }

        /// <summary>
        /// Same contract as <see cref="CheckpointSnapshot.Capture"/> (duplicate or empty keys and throwing saveables are
        /// logged and skipped) with the capture time taken from the mission clock.
        /// </summary>
        public static CheckpointSnapshot Capture(string checkpointId, IReadOnlyList<ISaveable> saveables, float time)
        {
            var snap = new CheckpointSnapshot { CheckpointId = checkpointId, CapturedAt = time };
            for (var i = 0; i < saveables.Count; i++)
            {
                var s = saveables[i];
                if (s == null || (s is UnityEngine.Object o && o == null)) continue;
                var key = s.SaveKey;
                if (string.IsNullOrEmpty(key))
                {
                    LSLog.Error(LogCategory.Save, "ISaveable with an empty SaveKey: " + s);
                    continue;
                }
                if (snap.Find(key) != null)
                {
                    LSLog.Error(LogCategory.Save, "Duplicate SaveKey '" + key + "' (" + s + ")");
                    continue;
                }
                var rec = snap.GetOrAdd(key);
                try
                {
                    s.Capture(new SaveWriter(rec));
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Save, e);
                    snap.Records.Remove(rec);
                }
            }
            return snap;
        }

        private void BuildCaptureSet()
        {
            _captureSet.Clear();
            for (var i = _saveables.Count - 1; i >= 0; i--)
            {
                var s = _saveables[i];
                if (s == null || (s is UnityEngine.Object o && o == null)) _saveables.RemoveAt(i);
            }
            _captureSet.AddRange(_saveables);
            var reg = _markers.Value;
            if (reg == null) return;
            reg.GetAll(_points);
            for (var i = 0; i < _points.Count; i++)
            {
                var p = _points[i];
                if (p != null && !_captureSet.Contains(p)) _captureSet.Add(p);
            }
        }

        private void StoreHighest(string cpId)
        {
            var settings = _settings.Value;
            if (settings == null) return;
            var current = settings.GetValue(StorageKeys.HighestCheckpoint);
            if (SequenceIds.CheckpointOrder(cpId) <= SequenceIds.CheckpointOrder(current)) return;
            settings.SetValue(StorageKeys.HighestCheckpoint, cpId);
        }
    }
}
