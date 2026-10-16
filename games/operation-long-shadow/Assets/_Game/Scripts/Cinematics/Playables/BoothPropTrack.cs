using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Booth actions (no binding; uses the loaded BoothRoot).</summary>
    [TrackColor(0.75f, 0.65f, 0.45f)]
    [TrackClipType(typeof(BoothPropClip))]
    public sealed class BoothPropTrack : TrackAsset
    {
    }
}
