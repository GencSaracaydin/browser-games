using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Ballistics
{
    /// <summary>
    /// Hitscan ordering and penetration. GDD §13 does not specify wall penetration, so bullets stop at the first solid
    /// surface; they pass through thin cloth (awnings, laundry, G3) and glass at a damage cost, at most
    /// <see cref="MaxPenetrations"/> times. Bodies stop bullets.
    /// </summary>
    public static class HitscanRules
    {
        /// <summary>Most thin surfaces one bullet passes through.</summary>
        public const int MaxPenetrations = 2;
        /// <summary>Damage kept after each penetration.</summary>
        public const float PenetrationDamageScale = 0.85f;

        /// <summary>True for surfaces a bullet passes through.</summary>
        public static bool Penetrates(SurfaceType s) => s == SurfaceType.Fabric || s == SurfaceType.Glass;

        /// <summary>Sorts the first <paramref name="count"/> hits by distance, nearest first (insertion sort; no allocation).</summary>
        public static void SortByDistance(RaycastHit[] hits, int count)
        {
            for (var i = 1; i < count; i++)
            {
                var h = hits[i];
                var j = i - 1;
                while (j >= 0 && hits[j].distance > h.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }
                hits[j + 1] = h;
            }
        }
    }
}
