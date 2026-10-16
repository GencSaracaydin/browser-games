using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// The light budget of GDD §9.5 / TECH_PLAN §7.16 as pure functions: at most 12 visible additional lights, of which
    /// pooled muzzle flashes may take 4 (WP03), fixed lights (floods, flares, fixed fills, the projector) take what they
    /// use, and near practicals get the rest up to 6 per area.
    /// </summary>
    public static class PracticalBudgetMath
    {
        /// <summary>Maximum visible additional lights (GDD §9.5).</summary>
        public const int MaxAdditionalLights = 12;
        /// <summary>Maximum realtime near practicals per area (GDD G4, §9.5).</summary>
        public const int MaxPracticals = 6;
        /// <summary>Slots kept for pooled muzzle-flash lights (WP03, TECH_PLAN §7.16 "muzzle lights (4)").</summary>
        public const int MuzzleReserve = 4;

        /// <summary>One candidate.</summary>
        public struct Candidate
        {
            /// <summary>Index in the caller's list.</summary>
            public int Index;
            /// <summary>Lower is better.</summary>
            public float Score;
        }

        /// <summary>Practical slots left after fixed lights and the muzzle reserve.</summary>
        public static int Slots(int fixedLightsOn, int muzzleReserve = MuzzleReserve, int maxPracticals = MaxPracticals,
            int maxAdditional = MaxAdditionalLights)
        {
            var left = maxAdditional - Mathf.Max(0, muzzleReserve) - Mathf.Max(0, fixedLightsOn);
            return Mathf.Clamp(left, 0, maxPracticals);
        }

        /// <summary>
        /// Score of a practical (lower wins): distance divided by priority, with lights behind the camera penalised
        /// (×1.6) and the currently granted ones favoured (×0.85, hysteresis against flip-flopping).
        /// </summary>
        public static float Score(float distance, float priority, bool inFront, bool currentlyGranted)
        {
            var s = distance / Mathf.Max(0.05f, priority);
            if (!inFront) s *= 1.6f;
            if (currentlyGranted) s *= 0.85f;
            return s;
        }

        /// <summary>Sorts candidates by score (insertion sort: N is small, no allocation) and returns how many win.</summary>
        public static int SelectBest(List<Candidate> candidates, int slots)
        {
            for (var i = 1; i < candidates.Count; i++)
            {
                var c = candidates[i];
                var j = i - 1;
                while (j >= 0 && candidates[j].Score > c.Score)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }
                candidates[j + 1] = c;
            }
            return Mathf.Min(slots, candidates.Count);
        }
    }
}
