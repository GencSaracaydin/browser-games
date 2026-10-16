using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Player.View
{
    /// <summary>
    /// Yaw/pitch of the player (TECH_PLAN §7.3 PlayerLook): mouse look (scaled by <see cref="LookMath"/> in PlayerRig),
    /// recoil that moves the aim (<c>AddAimDelta</c>), look limits for the door gun and cinematics (the view eases back
    /// inside new limits), suppression aim sway (GDD §14.3) and the seat frame while mounted.
    /// <para>Unmounted, the yaw rotates the rig root (so <c>Root.forward</c> is the body heading) and the Yaw node stays
    /// identity; mounted, the root follows the seat and the yaw is seat-relative on the Yaw node. Pitch always rotates
    /// the Pitch node (the neck pivot). Transforms are written immediately so weapons that run later in the frame
    /// (ExecOrder.Weapons) aim along this frame's view.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform _yawNode;
        [SerializeField] private Transform _pitchNode;
        [SerializeField, Tooltip("How fast the view eases back inside newly set look limits (1/s).")]
        private float _limitReturnLambda = 10f;
        [SerializeField, Tooltip("Aim sway at full suppression in degrees (GDD §14.3: 15 % aim sway for 1 s; DECISIONS WP02).")]
        private float _suppressionSwayDegrees = 0.6f;

        private float _yaw;
        private float _pitch;
        private Transform _seat;
        private bool _limited;
        private bool _limitsSeatRelative;
        private Quaternion _limitCenter = Quaternion.identity;
        private float _limitYaw;
        private float _limitUp;
        private float _limitDown;
        private bool _settling;
        private float _swayTimer;
        private float _sway01;
        private float _swayTime;
        private Vector2 _swayOffset;
        private bool _initialised;

        /// <summary>View pitch in degrees (positive up, ±85).</summary>
        public float Pitch => _pitch;
        /// <summary>World compass heading of the view (0 = +Z).</summary>
        public float Heading => _yawNode != null ? MathX.BearingOf(_yawNode.forward) : MathX.WrapAngle360(_yaw);
        /// <summary>Heading of the body frame WASD moves in (world compass).</summary>
        public float BodyHeading => Heading;
        /// <summary>True while look limits are set.</summary>
        public bool HasLimits => _limited;
        /// <summary>Current suppression sway 0..1.</summary>
        public float Suppression01 => _sway01;
        /// <summary>The Yaw node.</summary>
        public Transform YawNode => _yawNode;
        /// <summary>The Pitch node.</summary>
        public Transform PitchNode => _pitchNode;

        /// <summary>Wires the hierarchy (PlayerRigFactory).</summary>
        internal void Bind(Transform yawNode, Transform pitchNode)
        {
            _yawNode = yawNode;
            _pitchNode = pitchNode;
        }

        private void Awake() => Initialise();

        /// <summary>Takes the starting yaw from the root's rotation (idempotent).</summary>
        public void Initialise()
        {
            if (_initialised) return;
            _initialised = true;
            _yaw = MathX.BearingOf(transform.forward);
            _pitch = 0f;
            Apply();
        }

        /// <summary>Applies a mouse delta in degrees (x = yaw right, y = pitch up) and the per-frame effects.</summary>
        public void Tick(Vector2 deltaDegrees, float dt)
        {
            Initialise();
            _yaw += deltaDegrees.x;
            _pitch = LookMath.ClampPitch(_pitch + deltaDegrees.y);
            ApplyLimits(dt);
            UpdateSway(dt);
            WrapYaw();
            Apply();
        }

        /// <summary>Recoil that moves the aim (degrees, positive pitch = up).</summary>
        public void AddDelta(float pitch, float yaw)
        {
            Initialise();
            _pitch = LookMath.ClampPitch(_pitch + pitch);
            _yaw += yaw;
            if (!_settling) ApplyLimits(0f);
            WrapYaw();
            Apply();
        }

        /// <summary>Sets the view instantly (pitch in degrees, world compass heading).</summary>
        public void SetAngles(float pitch, float worldHeading)
        {
            Initialise();
            _pitch = LookMath.ClampPitch(pitch);
            _yaw = _seat != null ? MathX.WrapAngle180(worldHeading - SeatHeading()) : worldHeading;
            _settling = false;
            WrapYaw();
            Apply();
        }

        /// <summary>Limits the view around a world orientation (yaw ±<paramref name="yaw"/>, pitch +up/−down).</summary>
        public void SetLimits(Quaternion center, float yaw, float up, float down)
        {
            _limited = true;
            _limitsSeatRelative = _seat != null;
            _limitCenter = _seat != null ? Quaternion.Inverse(_seat.rotation) * center : center;
            _limitYaw = Mathf.Abs(yaw);
            _limitUp = Mathf.Abs(up);
            _limitDown = Mathf.Abs(down);
            _settling = true;
        }

        /// <summary>Removes the limits.</summary>
        public void ClearLimits()
        {
            _limited = false;
            _settling = false;
        }

        /// <summary>Switches the yaw to the seat frame (PlayerMount; call before parenting the root).</summary>
        public void Mount(Transform seat)
        {
            Initialise();
            var heading = Heading;
            _seat = seat;
            _yaw = MathX.WrapAngle180(heading - SeatHeading());
            if (_limited && !_limitsSeatRelative && seat != null)
            {
                _limitCenter = Quaternion.Inverse(seat.rotation) * _limitCenter;
                _limitsSeatRelative = true;
            }
        }

        /// <summary>Back to the world frame (PlayerMount; call after unparenting the root).</summary>
        public void Dismount()
        {
            var heading = Heading;
            if (_limited && _limitsSeatRelative && _seat != null)
            {
                _limitCenter = _seat.rotation * _limitCenter;
                _limitsSeatRelative = false;
            }
            _seat = null;
            _yaw = heading;
            WrapYaw();
            Apply();
        }

        /// <summary>Starts or extends suppression aim sway for <paramref name="seconds"/>.</summary>
        public void Suppress(float seconds) => _swayTimer = Mathf.Max(_swayTimer, seconds);

        /// <summary>Clears sway and limits settling (respawn, teleport).</summary>
        public void ResetEffects()
        {
            _swayTimer = 0f;
            _sway01 = 0f;
            _swayOffset = Vector2.zero;
            _settling = false;
            Apply();
        }

        /// <summary>Writes the angles to the transforms.</summary>
        public void Apply()
        {
            if (_seat == null)
            {
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                if (_yawNode != null) _yawNode.localRotation = Quaternion.identity;
            }
            else if (_yawNode != null)
            {
                _yawNode.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            }
            if (_pitchNode != null) _pitchNode.localRotation = Quaternion.Euler(-(_pitch + _swayOffset.y), _swayOffset.x, 0f);
        }

        private float SeatHeading() => _seat != null ? MathX.BearingOf(_seat.forward) : 0f;

        private void WrapYaw() => _yaw = _seat != null ? MathX.WrapAngle180(_yaw) : MathX.WrapAngle360(_yaw);

        private void ApplyLimits(float dt)
        {
            if (!_limited) return;
            var center = _limitCenter;
            if (_limitsSeatRelative && _seat == null) center = _limitCenter; // seat gone: keep the last local frame as world
            var fwd = center * Vector3.forward;
            var centerYaw = MathX.BearingOf(fwd);
            var centerPitch = LookMath.PitchOf(fwd);
            var y = _yaw;
            var p = _pitch;
            LookMath.ClampToLimits(ref y, ref p, centerYaw, centerPitch, _limitYaw, _limitUp, _limitDown);
            if (_settling && dt > 0f)
            {
                var k = 1f - Mathf.Exp(-_limitReturnLambda * dt);
                _yaw += MathX.WrapAngle180(y - _yaw) * k;
                _pitch = Mathf.Lerp(_pitch, p, k);
                if (Mathf.Abs(MathX.WrapAngle180(y - _yaw)) < 0.05f && Mathf.Abs(p - _pitch) < 0.05f) _settling = false;
            }
            else
            {
                _yaw = y;
                _pitch = p;
                _settling = false;
            }
        }

        private void UpdateSway(float dt)
        {
            if (dt <= 0f) return;
            _swayTimer = Mathf.Max(0f, _swayTimer - dt);
            _sway01 = Mathf.MoveTowards(_sway01, _swayTimer > 0f ? 1f : 0f, dt * 3f);
            if (_sway01 <= 0f)
            {
                _swayOffset = Vector2.zero;
                return;
            }
            _swayTime += dt;
            var a = _suppressionSwayDegrees * _sway01;
            _swayOffset = new Vector2(TraumaShake.Noise(_swayTime * 0.9f, 3.1f) * a, TraumaShake.Noise(_swayTime * 0.7f, 9.4f) * a);
        }
    }
}
