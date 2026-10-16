using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Poses a scene body with one clip at a given time in edit mode through a manually evaluated PlayableGraph (works
    /// for Humanoid clips: the retargeting runs as in play mode). The pose stays on the transforms after
    /// <see cref="Dispose"/>. Used by <see cref="LocomotionSpeedProbe"/> and <see cref="BodyValidator"/>.
    /// </summary>
    public sealed class ClipSampler : IDisposable
    {
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;

        /// <summary>True when the graph could be built (Animator with an avatar, a clip).</summary>
        public bool IsValid => _graph.IsValid();

        public ClipSampler(Animator animator, AnimationClip clip)
        {
            if (animator == null || clip == null) return;
            animator.applyRootMotion = false;
            _graph = PlayableGraph.Create("WP05.ClipSampler");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Pose", animator);
            _playable = AnimationClipPlayable.Create(_graph, clip);
            _playable.SetApplyFootIK(false);
            output.SetSourcePlayable(_playable);
        }

        /// <summary>Evaluates the clip at <paramref name="time"/> seconds.</summary>
        public void Sample(float time)
        {
            if (!_graph.IsValid()) return;
            _playable.SetTime(time);
            _graph.Evaluate();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
