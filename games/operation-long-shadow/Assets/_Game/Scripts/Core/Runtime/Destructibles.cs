using System.Collections.Generic;

namespace LongShadow.Core
{
    /// <summary>Registry of <see cref="IDestructible"/> by id (WP03 Destructible registers on enable).</summary>
    public static class Destructibles
    {
        private static readonly Dictionary<string, IDestructible> s_byId = new Dictionary<string, IDestructible>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_byId.Clear();

        /// <summary>Registers by <see cref="IDestructible.Id"/> (logs duplicates).</summary>
        public static void Register(IDestructible d)
        {
            if (d == null || string.IsNullOrEmpty(d.Id)) return;
            if (s_byId.TryGetValue(d.Id, out var existing) && !ReferenceEquals(existing, d) && IsAlive(existing))
            {
                LSLog.ErrorOnce("destructible-dup:" + d.Id, LogCategory.Combat, "Duplicate destructible id " + d.Id);
                return;
            }
            s_byId[d.Id] = d;
        }

        /// <summary>Unregisters if registered under its id.</summary>
        public static void Unregister(IDestructible d)
        {
            if (d == null || string.IsNullOrEmpty(d.Id)) return;
            if (s_byId.TryGetValue(d.Id, out var existing) && ReferenceEquals(existing, d)) s_byId.Remove(d.Id);
        }

        /// <summary>Finds a destructible by id.</summary>
        public static bool TryGet(string id, out IDestructible d)
        {
            if (id != null && s_byId.TryGetValue(id, out d) && IsAlive(d)) return true;
            d = null;
            return false;
        }

        /// <summary>Fills a list (cleared first) with every registered destructible.</summary>
        public static int GetAll(List<IDestructible> results)
        {
            results.Clear();
            foreach (var kv in s_byId)
                if (IsAlive(kv.Value)) results.Add(kv.Value);
            return results.Count;
        }

        private static bool IsAlive(object o) => o is UnityEngine.Object uo ? uo != null : o != null;
    }
}
