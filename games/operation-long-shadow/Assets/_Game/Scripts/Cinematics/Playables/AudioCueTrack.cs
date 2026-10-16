using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Sound cues (no binding; IAudioService/IMusicService).</summary>
    [TrackColor(0.25f, 0.85f, 0.85f)]
    [TrackClipType(typeof(AudioCueClip))]
    public sealed class AudioCueTrack : TrackAsset
    {
    }
}
