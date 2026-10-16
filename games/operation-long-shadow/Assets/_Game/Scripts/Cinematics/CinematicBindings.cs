using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Runtime registry of the objects that timeline tracks and inserts drive, keyed by <see cref="BindingIds"/>.
    /// Timelines are assets in S_Persistent's library while the things they animate live in other scenes (S0_Booth,
    /// S1_Approach, S6_StationInt), so tracks are bound at play time by id instead of by serialized scene reference
    /// (track name "Label @Id", see <c>TrackBindingResolver</c>). Lookups fall back to markers (IMarkerRegistry) and to
    /// squad members ("Squad.HOL"). Statics reset on SubsystemRegistration (R6).
    /// </summary>
    public static class CinematicBindings
    {
        private static readonly Dictionary<string, Object> s_objects = new Dictionary<string, Object>();
        private static ServiceRef<IMarkerRegistry> s_markers;
        private static ServiceRef<ISquadService> s_squad;
        private static ServiceRef<IPlayerRig> s_rig;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_objects.Clear();
            s_markers = default;
            s_squad = default;
            s_rig = default;
        }

        /// <summary>Number of registered ids.</summary>
        public static int Count => s_objects.Count;

        /// <summary>Registers (or replaces) an object under an id.</summary>
        public static void Register(string id, Object obj)
        {
            if (string.IsNullOrEmpty(id) || obj == null) return;
            if (s_objects.TryGetValue(id, out var existing) && existing != null && existing != obj)
                LSLog.Verbose(LogCategory.Cinematics, "Binding '" + id + "' replaced (" + existing.name + " -> " + obj.name + ")");
            s_objects[id] = obj;
        }

        /// <summary>Removes an id if it still points at <paramref name="obj"/>.</summary>
        public static void Unregister(string id, Object obj)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (s_objects.TryGetValue(id, out var existing) && (existing == obj || existing == null)) s_objects.Remove(id);
        }

        /// <summary>True if the id is registered (no marker or squad fallback).</summary>
        public static bool IsRegistered(string id) =>
            !string.IsNullOrEmpty(id) && s_objects.TryGetValue(id, out var o) && o != null;

        /// <summary>Resolves an id to a transform: registered object, squad member ("Squad.HOL") or marker.</summary>
        public static Transform ResolveTransform(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (s_objects.TryGetValue(id, out var o) && o != null)
            {
                if (o is Component c) return c.transform;
                if (o is GameObject g) return g.transform;
            }
            if (id == BindingIds.PlayerEye || id == BindingIds.PlayerRoot)
            {
                var rig = s_rig.Value;
                if (rig == null) return null;
                return id == BindingIds.PlayerEye ? rig.Eye : rig.Root;
            }
            if (id.StartsWith(BindingIds.SquadPrefix, System.StringComparison.Ordinal))
            {
                var squad = s_squad.Value;
                return squad?.Member(id.Substring(BindingIds.SquadPrefix.Length));
            }
            var markerId = id.StartsWith(BindingIds.MarkerPrefix, System.StringComparison.Ordinal)
                ? id.Substring(BindingIds.MarkerPrefix.Length)
                : id;
            var reg = s_markers.Value;
            if (reg != null && reg.TryGet(markerId, out SceneMarker m)) return m.transform;
            return null;
        }

        /// <summary>
        /// Resolves an id to an object of type <typeparamref name="T"/>: the registered object itself, a component on it
        /// (or its children), or a component on the resolved transform. Null when nothing matches.
        /// </summary>
        public static T Resolve<T>(string id) where T : class
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (s_objects.TryGetValue(id, out var o) && o != null)
            {
                if (o is T direct) return direct;
                var go = o is Component c ? c.gameObject : o as GameObject;
                if (go != null)
                {
                    var comp = go.GetComponent<T>();
                    if (comp != null) return comp;
                    comp = go.GetComponentInChildren<T>(true);
                    if (comp != null) return comp;
                }
            }
            var t = ResolveTransform(id);
            if (t == null) return null;
            var r = t.GetComponent<T>();
            return r ?? t.GetComponentInChildren<T>(true);
        }

        /// <summary>
        /// Resolves an id to an object assignable to <paramref name="type"/> (track binding types). GameObject and
        /// Transform requests are served from the resolved transform.
        /// </summary>
        public static Object ResolveAs(string id, System.Type type)
        {
            if (string.IsNullOrEmpty(id) || type == null) return null;
            if (s_objects.TryGetValue(id, out var o) && o != null && type.IsInstanceOfType(o)) return o;
            var t = ResolveTransform(id);
            if (t == null) return null;
            if (type == typeof(GameObject)) return t.gameObject;
            if (type == typeof(Transform)) return t;
            if (typeof(Component).IsAssignableFrom(type))
            {
                var c = t.GetComponent(type);
                if (c != null) return c;
                return t.GetComponentInChildren(type, true);
            }
            return null;
        }

        /// <summary>Copies every registered id into <paramref name="results"/> (diagnostics).</summary>
        public static void GetIds(List<string> results)
        {
            results.Clear();
            foreach (var kv in s_objects)
                if (kv.Value != null) results.Add(kv.Key);
        }

        /// <summary>Removes every binding (tests).</summary>
        public static void ClearForTests() => s_objects.Clear();
    }
}
