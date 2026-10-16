using UnityEngine;
using UnityEngine.Playables;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Helpers shared by the WP09 playables.</summary>
    public static class PlayableUtil
    {
        /// <summary>
        /// True when a clip should run its side effects: play mode and a playback evaluation (not an editor scrub or a
        /// manual Evaluate after a skip).
        /// </summary>
        public static bool IsLive(FrameData info) =>
            Application.isPlaying && info.evaluationType == FrameData.EvaluationType.Playback;

        /// <summary>Index of the input with the largest weight (−1 if none is active).</summary>
        public static int ActiveInput(Playable mixer)
        {
            var best = -1;
            var bestWeight = 0f;
            var n = mixer.GetInputCount();
            for (var i = 0; i < n; i++)
            {
                var w = mixer.GetInputWeight(i);
                if (w > bestWeight)
                {
                    bestWeight = w;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Normalised local time 0..1 of an input playable.</summary>
        public static float Normalized(Playable input)
        {
            var d = input.GetDuration();
            return d > 1e-6 ? Mathf.Clamp01((float)(input.GetTime() / d)) : 0f;
        }
    }
}
