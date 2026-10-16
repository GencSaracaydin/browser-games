using UnityEngine;

namespace LongShadow.Cinematics.Inserts
{
    /// <summary>
    /// Insert lengths (GDD §8.4, TECH_PLAN §7.12): lengths given in frames are at a 60 fps reference and play as unscaled
    /// time (6 f = 0.100 s) with a minimum of 2 rendered frames, so an insert looks the same at any frame rate.
    /// </summary>
    public static class InsertTiming
    {
        /// <summary>Reference frame rate of frame-counted insert lengths.</summary>
        public const float ReferenceFps = 60f;
        /// <summary>Minimum rendered frames of any insert shot.</summary>
        public const int MinRenderedFrames = 2;
        /// <summary>Glitch in and out (GDD §5.5: 0.15 s in and out).</summary>
        public const float GlitchSeconds = 0.15f;

        /// <summary>Seconds of a frame count at the 60 fps reference.</summary>
        public static float FramesToSeconds(int frames) => Mathf.Max(0, frames) / ReferenceFps;

        /// <summary>True once a shot has lasted its seconds and rendered at least <see cref="MinRenderedFrames"/>.</summary>
        public static bool IsComplete(float elapsedSeconds, int renderedFrames, float shotSeconds) =>
            elapsedSeconds >= shotSeconds - 1e-4f && renderedFrames >= MinRenderedFrames;
    }
}
