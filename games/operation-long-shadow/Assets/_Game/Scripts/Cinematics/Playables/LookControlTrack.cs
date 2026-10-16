using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Track of <see cref="LookControlClip"/>s (no binding; drives CinematicCameras.Look).</summary>
    [TrackColor(0.35f, 0.75f, 0.95f)]
    [TrackClipType(typeof(LookControlClip))]
    public sealed class LookControlTrack : TrackAsset
    {
    }
}
