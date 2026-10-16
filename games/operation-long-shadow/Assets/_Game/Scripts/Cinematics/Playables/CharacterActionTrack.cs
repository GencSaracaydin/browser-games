using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Body actions (no binding; each clip names its body).</summary>
    [TrackColor(0.6f, 0.85f, 0.3f)]
    [TrackClipType(typeof(CharacterActionClip))]
    public sealed class CharacterActionTrack : TrackAsset
    {
    }
}
