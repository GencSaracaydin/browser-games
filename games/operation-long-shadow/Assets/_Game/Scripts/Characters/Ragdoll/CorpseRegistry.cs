using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Characters.Ragdoll
{
    /// <summary>
    /// Corpse cap (TECH_PLAN §7.8: at most 8 corpses, each fades after 30 s). Registering the ninth corpse evicts the
    /// oldest one. Statics reset on SubsystemRegistration (domain reload is off, R6).
    /// </summary>
    public static class CorpseRegistry
    {
        /// <summary>Maximum corpses kept (TECH_PLAN §7.8).</summary>
        public const int DefaultCap = 8;

        private static readonly List<ICorpse> s_corpses = new List<ICorpse>(16);
        private static int s_cap = DefaultCap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_corpses.Clear();
            s_cap = DefaultCap;
        }

        /// <summary>Live corpse count.</summary>
        public static int Count => s_corpses.Count;

        /// <summary>Current cap.</summary>
        public static int Cap
        {
            get => s_cap;
            set => s_cap = Mathf.Max(0, value);
        }

        /// <summary>Adds a corpse; evicts the oldest ones beyond the cap.</summary>
        public static void Register(ICorpse corpse)
        {
            if (corpse == null || s_corpses.Contains(corpse)) return;
            s_corpses.Add(corpse);
            while (s_corpses.Count > s_cap)
            {
                var oldest = 0;
                for (var i = 1; i < s_corpses.Count; i++)
                    if (s_corpses[i].DiedAt < s_corpses[oldest].DiedAt) oldest = i;
                var c = s_corpses[oldest];
                s_corpses.RemoveAt(oldest);
                c.Evict();
            }
        }

        /// <summary>Forgets a corpse (revived, despawned, destroyed).</summary>
        public static void Unregister(ICorpse corpse) => s_corpses.Remove(corpse);

        /// <summary>True if tracked.</summary>
        public static bool Contains(ICorpse corpse) => s_corpses.Contains(corpse);

        /// <summary>Forgets every corpse (tests, checkpoint reload).</summary>
        public static void Clear() => s_corpses.Clear();
    }
}
