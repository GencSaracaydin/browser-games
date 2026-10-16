using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// Sodium street-lamp strike and warm-up (GDD §8.5 row 4: "each one starts a dim pink-red and warms to orange over
    /// 8 s, the real sodium warm-up, compressed"): an arc-strike sputter in the first 0.45 s, then brightness rising from
    /// 8 % to 100 % while the hue moves from the neon-start pink-red to high-pressure-sodium orange (~2000 K).
    /// </summary>
    public static class SodiumWarmup
    {
        /// <summary>Warm-up length in seconds (GDD §8.5).</summary>
        public const float Duration = 8f;
        /// <summary>Brightness right after the strike.</summary>
        public const float StartLevel = 0.08f;

        /// <summary>Colour at strike (sRGB factor): dim pink-red.</summary>
        public static readonly Color StartColor = new Color(1f, 0.30f, 0.34f, 1f);
        /// <summary>Colour when warm (sRGB factor): sodium orange.</summary>
        public static readonly Color WarmColor = new Color(1f, 0.56f, 0.19f, 1f);

        /// <summary>Warm fraction 0..1 at <paramref name="sinceStrike"/> seconds.</summary>
        public static float Progress(float sinceStrike)
        {
            if (sinceStrike <= 0f) return 0f;
            var x = Mathf.Clamp01(sinceStrike / Duration);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Brightness 0..1 (0 before the strike).</summary>
        public static float Level(float sinceStrike, float seed = 0f)
        {
            if (sinceStrike < 0f) return 0f;
            var p = Progress(sinceStrike);
            var level = Mathf.Lerp(StartLevel, 1f, Mathf.Pow(p, 1.3f));
            return level * FlickerNoise.StrikeSputter(sinceStrike, seed);
        }

        /// <summary>Hue factor (sRGB) at <paramref name="sinceStrike"/> seconds.</summary>
        public static Color Hue(float sinceStrike)
        {
            var p = Progress(sinceStrike);
            return Color.Lerp(StartColor, WarmColor, p);
        }
    }
}
