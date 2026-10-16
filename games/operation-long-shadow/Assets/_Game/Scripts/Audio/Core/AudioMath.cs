using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>Pure audio math shared by the players (unit-tested).</summary>
    public static class AudioMath
    {
        /// <summary>
        /// Near/far crossfade by distance (TECH_PLAN §7.9: near &lt; 40 m, far &gt; 120 m): returns the far share t in
        /// 0..1 (linear in distance) and the equal-power gains near = cos(t·π/2), far = sin(t·π/2), so the summed power
        /// stays constant through the blend.
        /// </summary>
        public static float FarShare(float distance, float nearEnd, float farStart, out float nearGain, out float farGain)
        {
            float t;
            if (farStart <= nearEnd) t = distance >= farStart ? 1f : 0f;
            else t = Mathf.Clamp01((distance - nearEnd) / (farStart - nearEnd));
            nearGain = Mathf.Cos(t * Mathf.PI * 0.5f);
            farGain = Mathf.Sin(t * Mathf.PI * 0.5f);
            if (nearGain < 1e-4f) nearGain = 0f;
            if (farGain < 1e-4f) farGain = 0f;
            return t;
        }

        /// <summary>
        /// Propagation delay of a sound <paramref name="distance"/> metres away: distance / speed of sound, 0 below
        /// <see cref="AudioConstants.SpeedOfSoundMinDistance"/>, capped at <see cref="AudioConstants.SpeedOfSoundMaxDelay"/>.
        /// </summary>
        public static float PropagationDelay(float distance)
        {
            if (float.IsNaN(distance) || distance < AudioConstants.SpeedOfSoundMinDistance) return 0f;
            return Mathf.Min(distance / AudioConstants.SpeedOfSound, AudioConstants.SpeedOfSoundMaxDelay);
        }

        /// <summary>
        /// Pitch multiplier for slow motion (CINEMATIC_DIRECTION §1 "audio pitch dip"): 1 at time scale ≥ 1, falling
        /// linearly to <paramref name="floor"/> at time scale 0.25 and below. Time scale 0 (pause, inserts) returns 1:
        /// those freeze audio by pausing, not by bending it.
        /// </summary>
        public static float SlowMotionPitch(float timeScale, float floor)
        {
            if (timeScale <= 0f || timeScale >= 0.999f || float.IsNaN(timeScale)) return 1f;
            var k = Mathf.InverseLerp(0.25f, 1f, timeScale);
            return Mathf.Lerp(Mathf.Clamp(floor, 0.3f, 1f), 1f, k);
        }

        /// <summary>
        /// Rough audibility of a 3D source (Unity's logarithmic rolloff: min / distance, 1 inside min, 0 beyond max) used
        /// to rank voices for stealing and to cull inaudible plays.
        /// </summary>
        public static float RolloffGain(float distance, float minDistance, float maxDistance, AudioRolloffMode mode)
        {
            if (distance <= minDistance) return 1f;
            if (distance >= maxDistance) return mode == AudioRolloffMode.Logarithmic ? minDistance / maxDistance * 0.5f : 0f;
            if (mode == AudioRolloffMode.Linear)
                return 1f - (distance - minDistance) / Mathf.Max(0.01f, maxDistance - minDistance);
            return minDistance / Mathf.Max(0.01f, distance);
        }

        /// <summary>Random linear gain within ±<paramref name="jitterDb"/>.</summary>
        public static float JitterGain(float jitterDb, float random01) =>
            jitterDb <= 0f ? 1f : Mathf.Pow(10f, Mathf.Lerp(-jitterDb, jitterDb, random01) / 20f);

        /// <summary>Random pitch within ±<paramref name="jitter"/> (fraction).</summary>
        public static float JitterPitch(float jitter, float random01) =>
            jitter <= 0f ? 1f : 1f + Mathf.Lerp(-jitter, jitter, random01);
    }
}
