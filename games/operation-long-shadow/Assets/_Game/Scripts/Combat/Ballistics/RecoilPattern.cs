using LongShadow.Combat.Data;
using UnityEngine;

namespace LongShadow.Combat.Ballistics
{
    /// <summary>
    /// Per-shot aim kick (GDD §13.1): the first <see cref="RecoilSpec.PatternLength"/> shots of a burst replay the fixed
    /// pattern exactly; later shots are <see cref="RecoilSpec.SustainPitch"/> ± <see cref="RecoilSpec.PitchRandom"/> up and
    /// <see cref="RecoilSpec.YawBias"/> ± <see cref="RecoilSpec.YawRandom"/> right. Returns degrees (x pitch up, y yaw right).
    /// </summary>
    public static class RecoilPattern
    {
        /// <summary>Kick of shot <paramref name="shotIndex"/> (0 = first shot of the burst); u1, u2 uniform in [0, 1).</summary>
        public static Vector2 Kick(RecoilSpec r, int shotIndex, float u1, float u2)
        {
            if (r == null) return Vector2.zero;
            var n = r.PatternLength;
            if (shotIndex >= 0 && shotIndex < n) return r.Pattern[shotIndex];
            var basePitch = shotIndex <= 0 && n == 0 ? r.FirstShotPitch : (r.SustainPitch > 0f ? r.SustainPitch : r.Pitch);
            var pitch = basePitch + (u1 * 2f - 1f) * r.PitchRandom;
            var yaw = r.YawBias + (u2 * 2f - 1f) * r.YawRandom;
            return new Vector2(Mathf.Max(0f, pitch), yaw);
        }

        /// <summary>Kick scaled for hip fire vs ADS (GDD values are ADS; hip uses <see cref="RecoilSpec.HipScale"/>).</summary>
        public static Vector2 Scaled(RecoilSpec r, Vector2 kick, float adsBlend) =>
            r == null ? kick : kick * Mathf.Lerp(r.HipScale, 1f, Mathf.Clamp01(adsBlend));
    }
}
