using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Core implementation of <see cref="IMarkerRegistry"/>. Created on first use and registered in
    /// <see cref="GameServices"/>; reset on SubsystemRegistration. Duplicate ids are errors (the first one wins).
    /// </summary>
    public sealed class MarkerRegistry : IMarkerRegistry
    {
        private static MarkerRegistry s_instance;

        private readonly Dictionary<string, SceneMarker> _byId = new Dictionary<string, SceneMarker>();
        private readonly List<SceneMarker> _all = new List<SceneMarker>();
        private readonly HashSet<SceneMarker> _set = new HashSet<SceneMarker>();

        /// <summary>The registry (created and registered on first access).</summary>
        public static MarkerRegistry Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new MarkerRegistry();
                    GameServices.Register<IMarkerRegistry>(s_instance);
                }
                return s_instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void EnsureRegistered() => _ = Instance;

        /// <summary>Drops the instance (tests). The next access creates a fresh, registered registry.</summary>
        public static void ResetForTests()
        {
            if (s_instance != null) GameServices.Unregister<IMarkerRegistry>(s_instance);
            s_instance = null;
        }

        /// <summary>Number of registered markers.</summary>
        public int Count => _all.Count;

        /// <summary>Adds a marker (called from <see cref="SceneMarker.OnEnable"/>).</summary>
        public void Add(SceneMarker m)
        {
            if (m == null || !_set.Add(m)) return;
            _all.Add(m);
            var id = m.Id;
            if (string.IsNullOrEmpty(id)) return;
            if (_byId.TryGetValue(id, out var existing) && existing != null && existing != m)
            {
                LSLog.ErrorOnce("marker-dup:" + id, LogCategory.Level,
                    "Duplicate marker id '" + id + "' on " + m.name + " (already on " + existing.name + "); the first one wins.", m);
                return;
            }
            _byId[id] = m;
        }

        /// <summary>Removes a marker (called from <see cref="SceneMarker.OnDisable"/>).</summary>
        public void Remove(SceneMarker m)
        {
            if (m == null || !_set.Remove(m)) return;
            _all.Remove(m);
            var id = m.Id;
            if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var existing) && existing == m) _byId.Remove(id);
        }

        /// <inheritdoc/>
        public Transform Get(string id)
        {
            if (TryGet(id, out SceneMarker m)) return m.transform;
            LSLog.WarnOnce("marker-missing:" + id, LogCategory.Level, "Marker '" + id + "' is not registered (section not loaded or id missing).");
            return null;
        }

        /// <inheritdoc/>
        public bool TryGet(string id, out SceneMarker marker)
        {
            if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out marker) && marker != null) return true;
            marker = null;
            return false;
        }

        /// <inheritdoc/>
        public bool TryGet<T>(string id, out T marker) where T : SceneMarker
        {
            if (TryGet(id, out SceneMarker m) && m is T t)
            {
                marker = t;
                return true;
            }
            marker = null;
            return false;
        }

        /// <inheritdoc/>
        public bool Has(string id) => TryGet(id, out SceneMarker _);

        /// <inheritdoc/>
        public IEnumerable<T> All<T>() where T : SceneMarker
        {
            for (var i = 0; i < _all.Count; i++)
                if (_all[i] is T t && t != null) yield return t;
        }

        /// <inheritdoc/>
        public int GetAll<T>(List<T> results) where T : SceneMarker
        {
            results.Clear();
            for (var i = 0; i < _all.Count; i++)
                if (_all[i] is T t && t != null) results.Add(t);
            return results.Count;
        }

        /// <summary>Every registered id (diagnostics, MarkupTools validation in play mode).</summary>
        public IEnumerable<string> Ids => _byId.Keys;
    }
}
