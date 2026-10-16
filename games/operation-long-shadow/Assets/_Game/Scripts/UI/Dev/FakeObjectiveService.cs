using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.UI.Dev
{
    /// <summary>In-memory <see cref="IObjectiveService"/> for the UI test bed and tests (raises the same events as WP07).</summary>
    public sealed class FakeObjectiveService : IObjectiveService
    {
        private readonly List<ObjectiveDef> _active = new List<ObjectiveDef>();
        private readonly List<MarkerDef> _markers = new List<MarkerDef>();
        private readonly HashSet<string> _completed = new HashSet<string>();

        public IReadOnlyList<ObjectiveDef> Active => _active;
        public IReadOnlyList<MarkerDef> Markers => _markers;
        public int IntelCount { get; set; }
        public int OptionalCount { get; set; }

        public void Set(ObjectiveDef objective)
        {
            _completed.Remove(objective.Id);
            for (var i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id != objective.Id) continue;
                _active[i] = objective;
                GameEvents.RaiseObjectivesChanged();
                return;
            }
            _active.Add(objective);
            GameEvents.RaiseObjectivesChanged();
        }

        public void Complete(string id)
        {
            for (var i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id != id) continue;
                _active.RemoveAt(i);
                _completed.Add(id);
                GameEvents.RaiseObjectiveCompleted(id);
                GameEvents.RaiseObjectivesChanged();
                return;
            }
        }

        public bool IsComplete(string id) => _completed.Contains(id);

        public void AddMarker(MarkerDef marker)
        {
            for (var i = 0; i < _markers.Count; i++)
            {
                if (_markers[i].Id != marker.Id) continue;
                _markers[i] = marker;
                GameEvents.RaiseMarkersChanged();
                return;
            }
            _markers.Add(marker);
            GameEvents.RaiseMarkersChanged();
        }

        public void RemoveMarker(string id)
        {
            for (var i = _markers.Count - 1; i >= 0; i--)
                if (_markers[i].Id == id) _markers.RemoveAt(i);
            GameEvents.RaiseMarkersChanged();
        }

        public void Clear()
        {
            _active.Clear();
            _markers.Clear();
            GameEvents.RaiseObjectivesChanged();
            GameEvents.RaiseMarkersChanged();
        }
    }
}
