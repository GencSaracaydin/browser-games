using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// Progression gates (GDD §7.13), backtrack blockers (GDD §6.7) and the gate-driven trigger arms, interactions and
    /// invulnerable racks of <see cref="GateTable"/> (TECH_PLAN §7.11 Gates). The state lives here and is pushed to the
    /// scene components found by id (GateMarker.SetOpen, TriggerVolume.SetArmed, InteractionPoint.SetAvailable,
    /// IDestructible.Invulnerable) whenever it changes and whenever a section loads, so a gate in a section that streams
    /// in later still gets the right state. Saved with the checkpoint snapshot ("Gates").
    /// </summary>
    public sealed class Gates : ISaveable
    {
        /// <summary>Save key.</summary>
        public const string Key = "Gates";

        private readonly Dictionary<string, bool> _open = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly List<string> _scratch = new List<string>(32);
        private ServiceRef<IMarkerRegistry> _markers;

        /// <summary>Raised when a state changes (id, open).</summary>
        public event Action<string, bool> Changed;

        /// <summary>Creates the gate set in its mission-start state.</summary>
        public Gates() => ResetToBeat(BeatTable.Intro, false);

        /// <inheritdoc/>
        public string SaveKey => Key;

        /// <summary>True when the entry is open (passable / armed / available / vulnerable). Unknown ids are open.</summary>
        public bool IsOpen(string id) => id == null || !_open.TryGetValue(id, out var open) || open;

        /// <summary>
        /// Sets every entry to its state at the start of <paramref name="beat"/> in a normal run (mission start, cold jumps:
        /// ?cp=, SELECT BEAT, the smoke test) and applies it.
        /// </summary>
        public void ResetToBeat(int beat, bool apply = true)
        {
            foreach (var g in GateTable.All) _open[g.Id] = g.OpenAtBeatStart(beat);
            if (apply) ApplyAll();
        }

        /// <summary>Opens or closes an entry and pushes it to the scene. Returns true if it changed.</summary>
        public bool Set(string id, bool open)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var had = _open.TryGetValue(id, out var was);
            _open[id] = open;
            Apply(id, open);
            if (had && was == open) return false;
            LSLog.Verbose(LogCategory.Mission, "Gate " + id + (open ? " open" : " closed"));
            Changed?.Invoke(id, open);
            return true;
        }

        /// <summary>Pushes every state to the scene (after a section load or a restore).</summary>
        public void ApplyAll()
        {
            _scratch.Clear();
            _scratch.AddRange(_open.Keys);
            for (var i = 0; i < _scratch.Count; i++) Apply(_scratch[i], _open[_scratch[i]]);
        }

        private void Apply(string id, bool open)
        {
            var def = GateTable.Get(id);
            if (def != null && def.Kind == GateKind.Invulnerable)
            {
                for (var i = 0; i < def.Members.Length; i++)
                    if (Destructibles.TryGet(def.Members[i], out var d)) d.Invulnerable = !open;
                return;
            }
            var reg = _markers.Value;
            if (reg == null || !reg.TryGet(id, out SceneMarker m) || m == null) return;
            switch (m)
            {
                case GateMarker gate:
                    gate.SetOpen(open);
                    break;
                case TriggerVolume trigger:
                    trigger.SetArmed(open);
                    break;
                case InteractionPoint point:
                    point.SetAvailable(open);
                    break;
            }
        }

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            _scratch.Clear();
            foreach (var kv in _open)
                if (kv.Value) _scratch.Add(kv.Key);
            w.Write("open", SaveCodec.Join(_scratch));
            _scratch.Clear();
            foreach (var kv in _open)
                if (!kv.Value) _scratch.Add(kv.Key);
            w.Write("closed", SaveCodec.Join(_scratch));
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            SaveCodec.Split(r.ReadString("open", string.Empty), _scratch);
            for (var i = 0; i < _scratch.Count; i++) _open[_scratch[i]] = true;
            SaveCodec.Split(r.ReadString("closed", string.Empty), _scratch);
            for (var i = 0; i < _scratch.Count; i++) _open[_scratch[i]] = false;
            ApplyAll();
        }
    }
}
