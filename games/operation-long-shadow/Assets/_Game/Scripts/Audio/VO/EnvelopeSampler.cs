using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// Precomputed amplitude envelopes (TECH_PLAN D10): 30 Hz RMS per clip, normalised to the clip's peak RMS and stored
    /// as bytes. <see cref="Sample"/> reads one at a playback time with linear interpolation (VU needle, jaw flap,
    /// voice-activity look-at). Pure (unit-tested).
    /// </summary>
    public static class EnvelopeSampler
    {
        /// <summary>Envelope rate of the WP06_VO files (<c>rate_hz</c>).</summary>
        public const float RateHz = 30f;

        /// <summary>Amplitude 0..1 at <paramref name="t"/> seconds; 0 outside the envelope.</summary>
        public static float Sample(byte[] envelope, float t, float rateHz = RateHz)
        {
            if (envelope == null || envelope.Length == 0 || t < 0f || float.IsNaN(t)) return 0f;
            var x = t * rateHz;
            var i = (int)x;
            if (i >= envelope.Length) return 0f;
            var a = envelope[i] / 255f;
            if (i + 1 >= envelope.Length) return a;
            var b = envelope[i + 1] / 255f;
            return Mathf.Lerp(a, b, x - i);
        }

        /// <summary>Encodes linear RMS values as bytes normalised to <paramref name="peak"/> (≤ 0 = the maximum value).</summary>
        public static byte[] Encode(float[] rms, float peak)
        {
            if (rms == null) return new byte[0];
            if (peak <= 0f)
            {
                for (var i = 0; i < rms.Length; i++)
                    if (rms[i] > peak) peak = rms[i];
            }
            var bytes = new byte[rms.Length];
            if (peak <= 0f) return bytes;
            for (var i = 0; i < rms.Length; i++)
                bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(rms[i] / peak * 255f), 0, 255);
            return bytes;
        }
    }
}
