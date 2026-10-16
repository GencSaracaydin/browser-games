using LongShadow.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// Drives the player rig in sequences shot on the player's own camera (the climax, GDD §8.7; the title card; the
    /// intro hand-off): eased look turns (to fixed angles or toward a binding: "the camera is drawn across the cabin,
    /// yaw about 160° over 1.5 s, to Hollis"), the lurch (view-punch roll + trauma), leaving the door gun, the FP pistol
    /// rise (WP05 IFirstPersonBody, 3 s), fists, body visibility and holstering.
    /// </summary>
    [System.Serializable]
    public sealed class PlayerRigClip : PlayableAsset, ITimelineClipAsset
    {
        /// <summary>Rig actions.</summary>
        public enum Action
        {
            /// <summary>Eased look from the current angles to <see cref="Pitch"/>/<see cref="Yaw"/> or toward <see cref="Target"/>.</summary>
            LookTurn,
            /// <summary>Roll punch (Value degrees) and shake trauma (Value2).</summary>
            Lurch,
            /// <summary>Leave the door gun.</summary>
            DismountDoorGun,
            /// <summary>FP pistol rise over the clip length.</summary>
            PistolRise,
            /// <summary>View-model and shadow-proxy visibility (Flag, Flag2).</summary>
            BodyVisibility,
            /// <summary>Holster (Flag) or draw.</summary>
            Holster,
            /// <summary>Set look angles instantly (Pitch, Yaw) — hand-off pose.</summary>
            SetLook,
            /// <summary>Lock look input for the clip (input channel Look).</summary>
            LockLook,
        }

        [SerializeField] private Action _action;
        [SerializeField] private float _pitch;
        [SerializeField] private float _yaw;
        [SerializeField] private string _target;
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private ShotEase _ease = ShotEase.EaseInOutCubic;
        [SerializeField] private float _value;
        [SerializeField] private float _value2;
        [SerializeField] private bool _flag;
        [SerializeField] private bool _flag2;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        public Action Kind { get => _action; set => _action = value; }
        public float Pitch { get => _pitch; set => _pitch = value; }
        public float Yaw { get => _yaw; set => _yaw = value; }
        public string Target { get => _target; set => _target = value; }
        public Vector3 TargetOffset { get => _targetOffset; set => _targetOffset = value; }
        public ShotEase Ease { get => _ease; set => _ease = value; }
        public float Value { get => _value; set => _value = value; }
        public float Value2 { get => _value2; set => _value2 = value; }
        public bool Flag { get => _flag; set => _flag = value; }
        public bool Flag2 { get => _flag2; set => _flag2 = value; }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<PlayerRigBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        /// <summary>Target look angles (pitch up positive, compass yaw) seen from the eye.</summary>
        public bool TargetAngles(Vector3 eye, out float pitch, out float yaw)
        {
            pitch = _pitch;
            yaw = _yaw;
            if (string.IsNullOrEmpty(_target)) return true;
            var t = CinematicBindings.ResolveTransform(_target);
            if (t == null) return false;
            var d = t.position + _targetOffset - eye;
            yaw = MathX.BearingOf(d);
            var flat = new Vector2(d.x, d.z).magnitude;
            pitch = Mathf.Atan2(d.y, Mathf.Max(0.001f, flat)) * Mathf.Rad2Deg;
            return true;
        }
    }

    /// <summary>Runs a rig action on entry; eases look turns over the clip.</summary>
    internal sealed class PlayerRigBehaviour : PlayableBehaviour
    {
        public PlayerRigClip Clip;
        private float _fromPitch;
        private float _fromYaw;
        private bool _turning;
        private int _lookLock;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Clip == null || !PlayableUtil.IsLive(info)) return;
            var rig = GameServices.Get<IPlayerRig>();
            switch (Clip.Kind)
            {
                case PlayerRigClip.Action.LookTurn:
                    if (rig == null) return;
                    _fromPitch = rig.Pitch;
                    _fromYaw = rig.Yaw;
                    _turning = true;
                    break;
                case PlayerRigClip.Action.SetLook:
                    rig?.SetLookAngles(Clip.Pitch, Clip.Yaw);
                    break;
                case PlayerRigClip.Action.Lurch:
                    rig?.SetViewPunch(Quaternion.Euler(0f, 0f, Clip.Value));
                    rig?.Shake(Clip.Value2);
                    break;
                case PlayerRigClip.Action.DismountDoorGun:
                    // WP07 dismounts at +3 s too (guarded the same way): whoever comes first does it.
                    var weapons = GameServices.Get<IWeaponService>();
                    if (weapons != null && weapons.IsDoorGunMounted) weapons.DismountDoorGun();
                    break;
                case PlayerRigClip.Action.PistolRise:
                    var fp = GameServices.Get<IFirstPersonBody>();
                    if (fp != null) fp.PlayPistolRise((float)playable.GetDuration(), null);
                    break;
                case PlayerRigClip.Action.BodyVisibility:
                    rig?.SetBodyVisibility(Clip.Flag, Clip.Flag2);
                    break;
                case PlayerRigClip.Action.Holster:
                    GameServices.Get<IWeaponService>()?.SetHolstered(Clip, Clip.Flag);
                    break;
                case PlayerRigClip.Action.LockLook:
                    var locks = GameServices.Get<IInputLock>();
                    if (locks != null) _lookLock = locks.Lock(InputLockFlags.Look, "Cinematic look turn");
                    break;
            }
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!_turning || Clip == null || !Application.isPlaying) return;
            var rig = GameServices.Get<IPlayerRig>();
            if (rig == null || rig.Eye == null) return;
            if (!Clip.TargetAngles(rig.Eye.position, out var pitch, out var yaw)) return;
            var k = Easing.Evaluate(Clip.Ease, PlayableUtil.Normalized(playable));
            var p = Mathf.Lerp(_fromPitch, pitch, k);
            var y = Mathf.LerpAngle(_fromYaw, yaw, k);
            rig.SetLookAngles(p, y);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            _turning = false;
            if (_lookLock == 0) return;
            GameServices.Get<IInputLock>()?.Unlock(_lookLock);
            _lookLock = 0;
        }
    }
}
