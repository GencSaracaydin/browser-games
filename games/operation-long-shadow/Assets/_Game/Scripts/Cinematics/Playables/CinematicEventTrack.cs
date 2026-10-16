using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Track of <see cref="CinematicEventClip"/> cues (no binding).</summary>
    [TrackColor(0.95f, 0.3f, 0.3f)]
    [TrackClipType(typeof(CinematicEventClip))]
    public sealed class CinematicEventTrack : TrackAsset
    {
    }
}
