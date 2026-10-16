using LongShadow.Cinematics.Booth;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>One CRT's channel (bound by "@Booth.CRTn").</summary>
    [TrackColor(0.45f, 0.95f, 0.55f)]
    [TrackClipType(typeof(CRTChannelClip))]
    [TrackBindingType(typeof(CRTScreen))]
    public sealed class CRTChannelTrack : TrackAsset
    {
        /// <inheritdoc/>
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) =>
            ScriptPlayable<CRTChannelMixer>.Create(graph, inputCount);
    }

    /// <summary>Applies the active CRT clip on entry and its continuous parameters every frame.</summary>
    internal sealed class CRTChannelMixer : PlayableBehaviour
    {
        private int _last = -1;

        public override void OnGraphStart(Playable playable) => _last = -1;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var screen = playerData as CRTScreen;
            if (screen == null || !Application.isPlaying) return;
            var i = PlayableUtil.ActiveInput(playable);
            if (i < 0) return;
            var input = (ScriptPlayable<CRTChannelBehaviour>)playable.GetInput(i);
            var clip = input.GetBehaviour()?.Clip;
            if (clip == null) return;
            if (i != _last)
            {
                _last = i;
                clip.Apply(screen);
            }
            clip.Tick(screen, PlayableUtil.Normalized(input));
        }
    }
}
