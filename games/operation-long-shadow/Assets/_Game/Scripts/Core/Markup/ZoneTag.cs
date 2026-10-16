using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Acoustic/space zone volume (weapon tails, VO flavour, default footsteps). Nested zones resolve by
    /// <see cref="Priority"/> (an Interior inside a Square wins). Use <see cref="Resolve"/> for any position.
    /// </summary>
    public class ZoneTag : TriggerVolume
    {
        [SerializeField] private ZoneType _zone = ZoneType.Street;
        [SerializeField, Tooltip("Higher wins where zones overlap.")]
        private int _priority;

        private static readonly List<ZoneTag> s_scratch = new List<ZoneTag>();

        /// <summary>Zone type.</summary>
        public ZoneType Zone => _zone;
        /// <summary>Overlap priority.</summary>
        public int Priority => _priority;

        /// <summary>Configures the zone (builders).</summary>
        public void SetZone(ZoneType zone, int priority)
        {
            _zone = zone;
            _priority = priority;
        }

        /// <summary>The highest-priority enabled zone containing <paramref name="position"/>, else <paramref name="fallback"/>.</summary>
        public static ZoneType Resolve(Vector3 position, ZoneType fallback = ZoneType.OpenDesert)
        {
            MarkerRegistry.Instance.GetAll(s_scratch);
            var best = fallback;
            var bestPriority = int.MinValue;
            for (var i = 0; i < s_scratch.Count; i++)
            {
                var z = s_scratch[i];
                if (z._priority > bestPriority && z.Contains(position))
                {
                    best = z._zone;
                    bestPriority = z._priority;
                }
            }
            s_scratch.Clear();
            return best;
        }

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(0.4f, 0.6f, 1f, 0.5f);
#endif
    }
}
