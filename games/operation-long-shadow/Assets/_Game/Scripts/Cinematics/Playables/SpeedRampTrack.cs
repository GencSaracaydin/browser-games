using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Speed ramps (no binding).</summary>
    [TrackColor(1f, 0.85f, 0.2f)]
    [TrackClipType(typeof(SpeedRampClip))]
    public sealed class SpeedRampTrack : TrackAsset
    {
        /// <inheritdoc/>
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) =>
            ScriptPlayable<SpeedRampMixer>.Create(graph, inputCount);
    }
}
