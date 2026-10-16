using LongShadow.Cinematics.Booth;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>The booth tube (bound by "@Booth.Tube").</summary>
    [TrackColor(0.95f, 0.95f, 0.6f)]
    [TrackClipType(typeof(LightFlickerClip))]
    [TrackBindingType(typeof(FlickerLight))]
    public sealed class LightFlickerTrack : TrackAsset
    {
        /// <inheritdoc/>
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) =>
            ScriptPlayable<LightFlickerMixer>.Create(graph, inputCount);
    }

    /// <summary>Switches the tube mode on clip entry.</summary>
    internal sealed class LightFlickerMixer : PlayableBehaviour
    {
        private int _last = -1;

        public override void OnGraphStart(Playable playable) => _last = -1;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var tube = playerData as FlickerLight;
            if (tube == null || !Application.isPlaying) return;
            var i = PlayableUtil.ActiveInput(playable);
            if (i < 0 || i == _last) return;
            _last = i;
            var clip = ((ScriptPlayable<LightFlickerBehaviour>)playable.GetInput(i)).GetBehaviour()?.Clip;
            if (clip != null) tube.SetMode(clip.Mode, clip.Blinks);
        }
    }
}
