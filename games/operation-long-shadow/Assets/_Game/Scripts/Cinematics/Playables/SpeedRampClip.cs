using LongShadow.Cinematics.Sets;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// A speed ramp (CINEMATIC_DIRECTION §1.1: "time scale 1.0 → 0.45 → 1.0 over about 1.5 s", once per sequence; I-8
    /// boots hitting the sand): scales the timeline itself and the game's time scale (bodies, particles), and the cast
    /// helicopter's rotors. Returns to 1 when no ramp clip is active.
    /// </summary>
    [System.Serializable]
    public sealed class SpeedRampClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private float _from = 1f;
        [SerializeField] private float _to = 0.45f;
        [SerializeField] private ShotEase _ease = ShotEase.Smooth;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>Speed at the start.</summary>
        public float From { get => _from; set => _from = value; }
        /// <summary>Speed at the end.</summary>
        public float To { get => _to; set => _to = value; }
        /// <summary>Ramp easing.</summary>
        public ShotEase Ease { get => _ease; set => _ease = value; }

        /// <summary>Speed at normalised time.</summary>
        public float SpeedAt(float t01) => Mathf.Lerp(_from, _to, Easing.Evaluate(_ease, t01));

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<SpeedRampBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }
    }

    /// <summary>Per-clip behaviour of a speed ramp.</summary>
    internal sealed class SpeedRampBehaviour : PlayableBehaviour
    {
        public SpeedRampClip Clip;
    }

    /// <summary>Applies the active ramp.</summary>
    internal sealed class SpeedRampMixer : PlayableBehaviour
    {
        private bool _active;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying) return;
            var service = CinematicRuntime.Service;
            if (service == null) return;
            var i = PlayableUtil.ActiveInput(playable);
            var speed = 1f;
            if (i >= 0)
            {
                var input = (ScriptPlayable<SpeedRampBehaviour>)playable.GetInput(i);
                var clip = input.GetBehaviour()?.Clip;
                if (clip != null) speed = clip.SpeedAt(PlayableUtil.Normalized(input));
            }
            if (i < 0 && !_active) return;
            _active = i >= 0;
            service.SetRampSpeed(speed);
            var rotors = CinematicBindings.Resolve<HeliRotors>(BindingIds.CastHeli);
            if (rotors != null) rotors.TimeScale = speed;
        }

        public override void OnGraphStop(Playable playable)
        {
            if (!_active) return;
            _active = false;
            CinematicRuntime.Service?.SetRampSpeed(1f);
        }
    }
}
