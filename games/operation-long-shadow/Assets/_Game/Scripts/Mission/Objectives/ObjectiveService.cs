using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Objectives
{
    /// <summary>
    /// <see cref="IObjectiveService"/> (TECH_PLAN §7.11, GDD §7.12): objectives in display order, their HUD markers,
    /// intel counts. Objectives come from <see cref="ObjectiveTable"/> (<see cref="Show"/>): exact ids, texts, marker
    /// kinds and targets; markers follow their target (marker, squad member, enemy, destructible) and fall back to the
    /// Appendix A position until it resolves; enemy and destructible markers drop when the target dies. Optional intel
    /// (O1/O2) is not listed (its text is "—"): only its intel icon shows, within 6 m. Completed objectives never come back
    /// (checkpoint resume calls <see cref="Show"/> again safely). Saved with the snapshot ("Objectives"): completed ids,
    /// collected intel, active objectives and their texts.
    /// <para>Plain C#: the MissionDirector owns it, ticks it and registers it in GameServices.</para>
    /// </summary>
    public sealed class ObjectiveService : IObjectiveService, ISaveable
    {
        /// <summary>Save key.</summary>
        public const string Key = "Objectives";
        /// <summary>Seconds between marker re-resolves.</summary>
        public const float ResolveInterval = 0.5f;

        private sealed class MarkerEntry
        {
            public MarkerDef Def;
            public ObjectiveTarget Target;
            public bool HasTarget;
            public string ObjectiveId;
            public float ProximityOnly;
            public bool Visible = true;
        }

        private readonly List<ObjectiveDef> _active = new List<ObjectiveDef>(8);
        private readonly Dictionary<string, ObjectiveDef> _optional = new Dictionary<string, ObjectiveDef>(StringComparer.Ordinal);
        private readonly HashSet<string> _completed = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _intel = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<MarkerEntry> _entries = new List<MarkerEntry>(16);
        private readonly List<MarkerDef> _markers = new List<MarkerDef>(16);
        private readonly List<string> _scratch = new List<string>(16);
        private readonly MarkerResolver _resolver = new MarkerResolver();
        private readonly Func<Vector3> _playerPosition;
        private readonly Func<string, bool> _killed;
        private float _nextResolve;
        private bool _markersDirty;

        /// <summary>Creates the service. <paramref name="killed"/> answers "is this enemy id dead" (from the event log).</summary>
        public ObjectiveService(Func<Vector3> playerPosition, Func<string, bool> killed)
        {
            _playerPosition = playerPosition ?? (() => Vector3.zero);
            _killed = killed;
        }

        /// <summary>Raised when an intel item is collected (id, main count, optional count).</summary>
        public event Action<string> IntelAdded;

        // ------------------------------------------------------------------ IObjectiveService

        /// <inheritdoc/>
        public IReadOnlyList<ObjectiveDef> Active => _active;
        /// <inheritdoc/>
        public IReadOnlyList<MarkerDef> Markers => _markers;
        /// <inheritdoc/>
        public int IntelCount { get; private set; }
        /// <inheritdoc/>
        public int OptionalCount { get; private set; }

        /// <summary>Completed objective ids.</summary>
        public IReadOnlyCollection<string> Completed => _completed;
        /// <summary>Collected intel ids.</summary>
        public IReadOnlyCollection<string> CollectedIntel => _intel;

        /// <inheritdoc/>
        public void Set(ObjectiveDef objective)
        {
            if (string.IsNullOrEmpty(objective.Id) || _completed.Contains(objective.Id)) return;
            if (string.IsNullOrEmpty(objective.FlavourGroup)) objective.FlavourGroup = ObjectiveFormat.Group(objective.Id);
            if (objective.Total <= 0) objective.Total = BeatTable.PlayableBeats;
            if (objective.Optional)
            {
                _optional[objective.Id] = objective;
                return;
            }
            var i = IndexOf(objective.Id);
            if (i >= 0)
            {
                if (_active[i].Text == objective.Text && _active[i].Index == objective.Index) return;
                _active[i] = objective;
            }
            else _active.Add(objective);
            GameEvents.RaiseObjectivesChanged();
        }

        /// <inheritdoc/>
        public void Complete(string id)
        {
            if (string.IsNullOrEmpty(id) || !_completed.Add(id)) return;
            var i = IndexOf(id);
            if (i >= 0) _active.RemoveAt(i);
            _optional.Remove(id);
            RemoveMarkersOf(id);
            GameEvents.RaiseObjectiveCompleted(id);
            GameEvents.RaiseObjectivesChanged();
        }

        /// <inheritdoc/>
        public bool IsComplete(string id) => id != null && _completed.Contains(id);

        /// <inheritdoc/>
        public void AddMarker(MarkerDef marker)
        {
            if (string.IsNullOrEmpty(marker.Id)) return;
            var e = FindEntry(marker.Id) ?? NewEntry(marker.Id);
            e.Def = marker;
            e.HasTarget = false;
            e.ObjectiveId = null;
            e.ProximityOnly = 0f;
            e.Visible = true;
            _markersDirty = true;
            Rebuild();
        }

        /// <inheritdoc/>
        public void RemoveMarker(string id)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Def.Id != id) continue;
                _entries.RemoveAt(i);
                _markersDirty = true;
            }
            Rebuild();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            var had = _active.Count > 0 || _entries.Count > 0 || _optional.Count > 0;
            _active.Clear();
            _optional.Clear();
            _entries.Clear();
            _markersDirty = true;
            Rebuild();
            if (had) GameEvents.RaiseObjectivesChanged();
        }

        // ------------------------------------------------------------------ mission API

        /// <summary>
        /// Shows an objective of <see cref="ObjectiveTable"/> (optionally with replaced text, e.g. a counter) and, unless the
        /// row is script-managed or <paramref name="markers"/> is false, its markers. Completed objectives are ignored.
        /// Returns false for unknown or completed ids.
        /// </summary>
        public bool Show(string objectiveId, string textOverride = null, bool markers = true)
        {
            var spec = ObjectiveTable.Get(objectiveId);
            if (spec == null)
            {
                LSLog.ErrorOnce("obj-unknown:" + objectiveId, LogCategory.Mission, "Unknown objective id " + objectiveId);
                return false;
            }
            if (IsComplete(objectiveId)) return false;
            Set(spec.ToDef(textOverride));
            if (markers && spec.AutoMarkers && spec.Kind.HasValue)
                for (var i = 0; i < spec.Targets.Length; i++) ShowTarget(spec, i);
            return true;
        }

        /// <summary>Replaces the text of an active objective (counters).</summary>
        public void SetText(string objectiveId, string text)
        {
            var i = IndexOf(objectiveId);
            if (i < 0 || _active[i].Text == text) return;
            var d = _active[i];
            d.Text = text;
            _active[i] = d;
            GameEvents.RaiseObjectivesChanged();
        }

        /// <summary>Current text of an active objective, or null.</summary>
        public string TextOf(string objectiveId)
        {
            var i = IndexOf(objectiveId);
            return i >= 0 ? _active[i].Text : null;
        }

        /// <summary>True while the objective is listed (or, for optional intel, tracked).</summary>
        public bool IsActive(string objectiveId) => IndexOf(objectiveId) >= 0 || (objectiveId != null && _optional.ContainsKey(objectiveId));

        /// <summary>Adds the marker of target <paramref name="index"/> of an objective row (script-managed markers).</summary>
        public void ShowTarget(ObjectiveSpec spec, int index)
        {
            if (spec == null || !spec.Kind.HasValue || index < 0 || index >= spec.Targets.Length) return;
            ShowMarker(spec.MarkerId(index), spec.Targets[index], spec.Kind.Value, spec.Label, spec.ShowDistance, spec.ThroughWalls,
                spec.Id, spec.ProximityOnly);
        }

        /// <summary>Removes the marker of target <paramref name="index"/> of an objective row.</summary>
        public void HideTarget(ObjectiveSpec spec, int index)
        {
            if (spec == null || index < 0 || index >= spec.Targets.Length) return;
            RemoveMarker(spec.MarkerId(index));
        }

        /// <summary>Adds a marker that follows a target (resolved now and re-resolved every 0.5 s).</summary>
        public void ShowMarker(string markerId, in ObjectiveTarget target, MarkerKind kind, string label, bool showDistance,
                               bool throughWalls, string objectiveId = null, float proximityOnly = 0f)
        {
            if (string.IsNullOrEmpty(markerId) || !target.IsValid) return;
            if (_resolver.IsGone(target, _killed)) return;
            var e = FindEntry(markerId) ?? NewEntry(markerId);
            e.Def = _resolver.Build(markerId, target, kind, label, showDistance, throughWalls);
            e.Target = target;
            e.HasTarget = true;
            e.ObjectiveId = objectiveId;
            e.ProximityOnly = proximityOnly;
            e.Visible = proximityOnly <= 0f || (_playerPosition() - e.Def.WorldPosition).sqrMagnitude <= proximityOnly * proximityOnly;
            _markersDirty = true;
            Rebuild();
        }

        /// <summary>True if a marker with this id exists.</summary>
        public bool HasMarker(string markerId) => FindEntry(markerId) != null;

        /// <summary>Forgets completed objectives and collected intel (mission start, cold jumps).</summary>
        public void ResetProgress()
        {
            _completed.Clear();
            _intel.Clear();
            Recount();
        }

        /// <summary>Marks an objective complete without events or markers (cold jumps fill the earlier beats).</summary>
        public void MarkComplete(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _completed.Add(id);
            var i = IndexOf(id);
            if (i >= 0) _active.RemoveAt(i);
            _optional.Remove(id);
            RemoveMarkersOf(id);
        }

        /// <summary>
        /// Collects an intel item (intel pickups, Sparrow's codebook): counts it, completes its objective, raises
        /// <see cref="GameEvents.IntelCollected"/>. Returns false if it was already collected or the id is unknown.
        /// </summary>
        public bool CollectIntel(string intelId)
        {
            var def = IntelTable.Get(intelId);
            if (def == null || !_intel.Add(intelId)) return false;
            Recount();
            Complete(def.ObjectiveId);
            GameEvents.RaiseIntelCollected(intelId);
            IntelAdded?.Invoke(intelId);
            return true;
        }

        /// <summary>Adds an intel item silently (cold jumps).</summary>
        public void MarkIntel(string intelId)
        {
            if (IntelTable.Get(intelId) == null || !_intel.Add(intelId)) return;
            Recount();
        }

        /// <summary>True once the intel item is collected.</summary>
        public bool HasIntel(string intelId) => intelId != null && _intel.Contains(intelId);

        /// <summary>Completed required objectives of a beat / its required count (GDD §9.1 "objective completion").</summary>
        public float CompletionOfBeat(int beat)
        {
            var total = ObjectiveTable.RequiredCount(beat);
            if (total <= 0) return 0f;
            var done = 0;
            var all = ObjectiveTable.All;
            for (var i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s.Beat == beat && !s.Optional && _completed.Contains(s.Id)) done++;
            }
            return (float)done / total;
        }

        /// <summary>
        /// Beat start: drops objectives and markers of earlier beats that are still listed (they cannot be finished any more),
        /// and optional intel whose section is gone (<see cref="IntelDef.ExpiresAtBeat"/>).
        /// </summary>
        public void DropBefore(int beat)
        {
            var changed = false;
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var spec = ObjectiveTable.Get(_active[i].Id);
                if (spec == null || spec.Beat >= beat) continue;
                RemoveMarkersOf(_active[i].Id);
                _active.RemoveAt(i);
                changed = true;
            }
            _scratch.Clear();
            foreach (var kv in _optional)
            {
                var intel = IntelTable.ForObjective(kv.Key);
                if (intel != null && beat >= intel.ExpiresAtBeat) _scratch.Add(kv.Key);
            }
            for (var i = 0; i < _scratch.Count; i++)
            {
                _optional.Remove(_scratch[i]);
                RemoveMarkersOf(_scratch[i]);
                changed = true;
            }
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var oid = _entries[i].ObjectiveId;
                var spec = oid != null ? ObjectiveTable.Get(oid) : null;
                if (spec == null || spec.Beat >= beat || _optional.ContainsKey(oid)) continue;
                _entries.RemoveAt(i);
                _markersDirty = true;
            }
            Rebuild();
            if (changed) GameEvents.RaiseObjectivesChanged();
        }

        /// <summary>Re-resolves markers, drops markers of dead targets, applies the optional-intel 6 m rule.</summary>
        public void Tick(float now)
        {
            var pos = _playerPosition();
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.ProximityOnly > 0f)
                {
                    var vis = (pos - e.Def.WorldPosition).sqrMagnitude <= e.ProximityOnly * e.ProximityOnly;
                    if (vis != e.Visible)
                    {
                        e.Visible = vis;
                        _markersDirty = true;
                    }
                }
            }
            if (now >= _nextResolve)
            {
                _nextResolve = now + ResolveInterval;
                for (var i = _entries.Count - 1; i >= 0; i--)
                {
                    var e = _entries[i];
                    if (!e.HasTarget) continue;
                    if (_resolver.IsGone(e.Target, _killed))
                    {
                        _entries.RemoveAt(i);
                        _markersDirty = true;
                        continue;
                    }
                    var hadTarget = e.Def.Target != null;
                    if (hadTarget && e.Target.Kind != ObjectiveTargetKind.Actor) continue;
                    var t = _resolver.Resolve(e.Target);
                    if (t == e.Def.Target) continue;
                    if (t == null && hadTarget && e.Target.Kind == ObjectiveTargetKind.Actor)
                    {
                        // The enemy despawned (checkpoint reload) or died: fall back until it spawns again.
                        e.Def.Target = null;
                        e.Def.Offset = e.Target.Fallback;
                    }
                    else if (t != null)
                    {
                        e.Def.Target = t;
                        e.Def.Offset = e.Target.Offset;
                    }
                    _markersDirty = true;
                }
            }
            Rebuild();
        }

        // ------------------------------------------------------------------ ISaveable

        /// <inheritdoc/>
        public string SaveKey => Key;

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            w.Write("completed", SaveCodec.Join(_completed));
            w.Write("intel", SaveCodec.Join(_intel));
            _scratch.Clear();
            for (var i = 0; i < _active.Count; i++) _scratch.Add(_active[i].Id);
            w.Write("active", SaveCodec.Join(_scratch));
            for (var i = 0; i < _active.Count; i++) w.Write("text." + _active[i].Id, _active[i].Text);
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            _active.Clear();
            _optional.Clear();
            _entries.Clear();
            _completed.Clear();
            _intel.Clear();
            SaveCodec.Split(r.ReadString("completed", string.Empty), _scratch);
            for (var i = 0; i < _scratch.Count; i++) _completed.Add(_scratch[i]);
            SaveCodec.Split(r.ReadString("intel", string.Empty), _scratch);
            for (var i = 0; i < _scratch.Count; i++) _intel.Add(_scratch[i]);
            Recount();
            SaveCodec.Split(r.ReadString("active", string.Empty), _scratch);
            for (var i = 0; i < _scratch.Count; i++)
            {
                var spec = ObjectiveTable.Get(_scratch[i]);
                if (spec == null) continue;
                Set(spec.ToDef(r.ReadString("text." + spec.Id, spec.Text)));
            }
            _markersDirty = true;
            Rebuild();
            GameEvents.RaiseObjectivesChanged();
        }

        // ------------------------------------------------------------------ internals

        private int IndexOf(string id)
        {
            for (var i = 0; i < _active.Count; i++)
                if (_active[i].Id == id) return i;
            return -1;
        }

        private MarkerEntry FindEntry(string id)
        {
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Def.Id == id) return _entries[i];
            return null;
        }

        private MarkerEntry NewEntry(string id)
        {
            var e = new MarkerEntry();
            e.Def.Id = id;
            _entries.Add(e);
            return e;
        }

        private void RemoveMarkersOf(string objectiveId)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.ObjectiveId != objectiveId && e.Def.Id != objectiveId) continue;
                _entries.RemoveAt(i);
                _markersDirty = true;
            }
            Rebuild();
        }

        private void Rebuild()
        {
            if (!_markersDirty) return;
            _markersDirty = false;
            _markers.Clear();
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Visible) _markers.Add(_entries[i].Def);
            GameEvents.RaiseMarkersChanged();
        }

        private void Recount()
        {
            var main = 0;
            var optional = 0;
            foreach (var id in _intel)
            {
                var d = IntelTable.Get(id);
                if (d == null) continue;
                if (d.Optional) optional++;
                else main++;
            }
            IntelCount = main;
            OptionalCount = optional;
        }
    }
}
