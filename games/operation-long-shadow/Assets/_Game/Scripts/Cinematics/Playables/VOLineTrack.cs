using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Dialogue lines (no binding; IVOService).</summary>
    [TrackColor(0.3f, 0.6f, 1f)]
    [TrackClipType(typeof(VOLineClip))]
    public sealed class VOLineTrack : TrackAsset
    {
    }
}
