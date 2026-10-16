using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Path moves of one transform (bound by "@Cast.Heli", "@Cast.Hollis", "@Booth.Interrogator" ...).</summary>
    [TrackColor(0.9f, 0.5f, 0.6f)]
    [TrackClipType(typeof(TransformPathClip))]
    [TrackBindingType(typeof(Transform))]
    public sealed class TransformPathTrack : TrackAsset
    {
        /// <inheritdoc/>
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) =>
            ScriptPlayable<TransformPathMixer>.Create(graph, inputCount);
    }

    /// <summary>Places the bound transform on the active path.</summary>
    internal sealed class TransformPathMixer : PlayableBehaviour
    {
        private Transform _detached;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as Transform;
            if (target == null || !Application.isPlaying) return;
            var i = PlayableUtil.ActiveInput(playable);
            if (i < 0) return;
            var input = (ScriptPlayable<TransformPathBehaviour>)playable.GetInput(i);
            var clip = input.GetBehaviour()?.Clip;
            if (clip == null || clip.Knots == null || clip.Knots.Length < 2) return;
            // A body seated in the helicopter leaves its seat when it starts moving on the ground.
            if (target.parent != null && _detached != target && !clip.BoothFrame)
            {
                target.SetParent(null, true);
                _detached = target;
            }
            clip.Evaluate(PlayableUtil.Normalized(input), (float)input.GetDuration(), target.rotation, out var p, out var r);
            target.SetPositionAndRotation(p, r);
        }
    }
}
