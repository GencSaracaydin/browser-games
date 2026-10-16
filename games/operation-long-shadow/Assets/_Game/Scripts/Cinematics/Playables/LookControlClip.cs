using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// Limited look on/off for the POV shots (TECH_PLAN §7.12 LookControlClip; GDD §8: ±40° yaw, ±20° pitch, spring back
    /// after 2 s; "limited look unlocks at 0:14"; MID-B ±20°), and the forced head turn of intro shot 7 (recentre over
    /// 0.8 s at the clip start, input ignored meanwhile).
    /// </summary>
    [System.Serializable]
    public sealed class LookControlClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private float _yawLimit = LimitedLook.DefaultYaw;
        [SerializeField] private float _pitchUp = LimitedLook.DefaultPitch;
        [SerializeField] private float _pitchDown = LimitedLook.DefaultPitch;
        [SerializeField] private float _springDelay = LimitedLook.DefaultSpringDelay;
        [SerializeField, Tooltip("Ease the view back to centre over this many seconds at the clip start (0 = no).")]
        private float _recentreSeconds;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>Look input accepted.</summary>
        public bool LookEnabled { get => _enabled; set => _enabled = value; }
        /// <summary>Yaw limit (±deg).</summary>
        public float YawLimit { get => _yawLimit; set => _yawLimit = value; }
        /// <summary>Upward limit (deg).</summary>
        public float PitchUp { get => _pitchUp; set => _pitchUp = value; }
        /// <summary>Downward limit (deg).</summary>
        public float PitchDown { get => _pitchDown; set => _pitchDown = value; }
        /// <summary>Idle seconds before spring-back.</summary>
        public float SpringDelay { get => _springDelay; set => _springDelay = value; }
        /// <summary>Forced recentre time (s).</summary>
        public float RecentreSeconds { get => _recentreSeconds; set => _recentreSeconds = value; }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<LookControlBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }
    }

    /// <summary>Applies a look clip when it starts.</summary>
    internal sealed class LookControlBehaviour : PlayableBehaviour
    {
        public LookControlClip Clip;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Clip == null || !PlayableUtil.IsLive(info)) return;
            var cams = CinematicRuntime.Cameras;
            if (cams == null) return;
            var look = cams.Look;
            look.YawLimit = Clip.YawLimit;
            look.PitchUpLimit = Clip.PitchUp;
            look.PitchDownLimit = Clip.PitchDown;
            look.SpringDelay = Clip.SpringDelay;
            look.Enabled = Clip.LookEnabled;
            if (Clip.RecentreSeconds > 0f) look.Recentre(Clip.RecentreSeconds);
            CinematicRuntime.Service?.SetLookInput(Clip.LookEnabled);
        }
    }
}
