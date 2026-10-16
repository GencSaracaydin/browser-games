using UnityEngine;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// The story-clock column of the GDD §9.2a LightStage keyframes (linear between rows): minutes after midnight on
    /// 21 October 1984 as a function of LightStage s. WP10's <c>ILightStage.StoryClockMinutes</c> is authoritative; this
    /// is the fallback while no LightStage is registered (the Seiko on Calder's wrist, GDD §8.5 "reads 18:40").
    /// </summary>
    public static class StoryClock
    {
        // GDD §9.2a: s → clock.
        private static readonly float[] s_s = { 0.00f, 0.15f, 0.45f, 1.00f, 1.35f, 1.50f, 1.80f, 1.94f, 1.97f, 2.00f, 2.40f, 2.80f, 3.20f, 3.70f, 4.00f };
        private static readonly float[] s_minutes =
        {
            M(17, 58), M(18, 8), M(18, 17), M(18, 26), M(18, 30), M(18, 34), M(18, 38), M(18, 40), M(18, 40), M(18, 41),
            M(18, 50), M(18, 58), M(19, 5), M(19, 10), M(19, 12),
        };

        /// <summary>The booth clock of the intro (GDD §5.1: 03:12, 2 November).</summary>
        public static readonly float BoothIntroMinutes = M(3, 12);
        /// <summary>The booth clock of the outro (GDD §7.0: 03:40).</summary>
        public static readonly float BoothOutroMinutes = M(3, 40);

        /// <summary>Story clock in minutes after midnight for a LightStage value.</summary>
        public static float FromS(float s)
        {
            if (s <= s_s[0]) return s_minutes[0];
            for (var i = 1; i < s_s.Length; i++)
            {
                if (s > s_s[i]) continue;
                var t = (s - s_s[i - 1]) / (s_s[i] - s_s[i - 1]);
                return Mathf.Lerp(s_minutes[i - 1], s_minutes[i], t);
            }
            return s_minutes[s_minutes.Length - 1];
        }

        /// <summary>"HH:MM" for minutes after midnight.</summary>
        public static string Format(float minutes)
        {
            var m = Mathf.FloorToInt(Mathf.Repeat(minutes, 1440f) + 0.001f);
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }

        private static float M(int h, int m) => h * 60f + m;
    }
}
