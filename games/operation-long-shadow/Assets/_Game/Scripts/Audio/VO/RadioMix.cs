using LongShadow.Audio.Core;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// Dry/radio crossfade by speaker distance (GDD §10.1, TECH_PLAN §7.10): dry only within 8 m, radio only beyond
    /// 15 m, a linear blend in between. DRY and RADIO takes are sample-synchronous, so a linear amplitude crossfade
    /// keeps the level constant. Pure (unit-tested: 6 m → 0, 10 m → 2/7, 12 m → 4/7, 20 m → 1).
    /// </summary>
    public static class RadioMix
    {
        /// <summary>Radio share 0..1 at <paramref name="distance"/> with the GDD thresholds.</summary>
        public static float RadioShare(float distance) =>
            RadioShare(distance, AudioConstants.VODryMaxDistance, AudioConstants.VORadioMinDistance);

        /// <summary>Radio share 0..1 between <paramref name="dryMax"/> and <paramref name="radioMin"/>.</summary>
        public static float RadioShare(float distance, float dryMax, float radioMin)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance)) return 1f;
            if (radioMin <= dryMax) return distance > dryMax ? 1f : 0f;
            return Mathf.Clamp01((distance - dryMax) / (radioMin - dryMax));
        }

        /// <summary>Dry and radio gains (sum 1) at a distance.</summary>
        public static void Weights(float distance, out float dry, out float radio)
        {
            radio = RadioShare(distance);
            dry = 1f - radio;
        }
    }
}
