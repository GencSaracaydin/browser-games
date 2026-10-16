using UnityEngine;

namespace LongShadow.Cinematics.FX
{
    /// <summary>
    /// Time curves of the full-screen effects (pure functions, unscaled seconds): exposure flashes (intro shot 9: +6 EV
    /// falling to 0 over 2 s), suppression vignette pulses, fades, the death desaturation and the glitch envelope
    /// (0.15 s in and out, GDD §5.5).
    /// </summary>
    public static class FxEnvelope
    {
        /// <summary>Suppression pulse length in seconds.</summary>
        public const float SuppressionSeconds = 0.6f;
        /// <summary>Extra vignette at a full suppression pulse.</summary>
        public const float SuppressionVignette = 0.35f;
        /// <summary>Death desaturation time (GDD §8.4: 0.6 s).</summary>
        public const float DeathSeconds = 0.6f;
        /// <summary>Saturation offset at full low health (GDD §11 desaturation below 50 % HP).</summary>
        public const float LowHealthSaturation = -60f;
        /// <summary>Vignette added at full low health.</summary>
        public const float LowHealthVignette = 0.22f;

        /// <summary>
        /// Exposure of a flash <paramref name="t"/> seconds after it started: holds the peak for 10 % of the time, then
        /// falls with an ease-out so the white settles into the image (GDD §8.2 shot 9).
        /// </summary>
        public static float Flash(float peakEv, float seconds, float t)
        {
            if (seconds <= 0f || t >= seconds) return 0f;
            if (t <= 0f) return peakEv;
            var hold = seconds * 0.1f;
            if (t <= hold) return peakEv;
            var k = (t - hold) / (seconds - hold);
            return peakEv * (1f - Easing.Evaluate(ShotEase.EaseOut, k));
        }

        /// <summary>Suppression pulse amount (0..1) <paramref name="t"/> seconds after a pulse of <paramref name="amount"/>.</summary>
        public static float Pulse(float amount, float t)
        {
            if (t < 0f || t >= SuppressionSeconds) return 0f;
            var attack = 0.06f;
            if (t < attack) return amount * (t / attack);
            return amount * Mathf.Exp(-6f * (t - attack));
        }

        /// <summary>
        /// Glitch envelope: ramps up over <see cref="Inserts.InsertTiming.GlitchSeconds"/>, holds, ramps down over the
        /// same time before <paramref name="seconds"/>.
        /// </summary>
        public static float Glitch(float intensity, float seconds, float t)
        {
            if (seconds <= 0f || t < 0f || t > seconds) return 0f;
            var ramp = Mathf.Min(Inserts.InsertTiming.GlitchSeconds, seconds * 0.5f);
            if (t < ramp) return intensity * (t / ramp);
            if (t > seconds - ramp) return intensity * Mathf.Clamp01((seconds - t) / ramp);
            return intensity;
        }

        /// <summary>Linear fade alpha from <paramref name="from"/> to <paramref name="to"/> over seconds.</summary>
        public static float Fade(float from, float to, float seconds, float t)
        {
            if (seconds <= 0f) return to;
            return Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
        }
    }
}
