using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Body
{
    /// <summary>
    /// Locomotion math for in-place clips driven by a NavMeshAgent (TECH_PLAN §7.8 "in-place clips and speed matching
    /// (updateRotation off, custom turn rate)"): local velocity parameters with damping, body yaw that faces the aim
    /// target while aiming (strafing) and the travel direction otherwise, an in-place turn parameter with hysteresis,
    /// and a playback-rate scale above the fastest clip. Plain C#, allocation-free, unit-tested.
    /// </summary>
    public sealed class LocomotionDriver
    {
        /// <summary>Yaw rate while moving (deg/s).</summary>
        public float MoveTurnRate = 400f;
        /// <summary>Yaw rate while standing and aiming (deg/s).</summary>
        public float AimTurnRate = 260f;
        /// <summary>Yaw rate when idle and not aiming (deg/s).</summary>
        public float IdleTurnRate = 160f;
        /// <summary>A standing body starts turning in place once the facing error exceeds this (deg).</summary>
        public float TurnStartAngle = 55f;
        /// <summary>... and stops when the error is below this (deg).</summary>
        public float TurnStopAngle = 4f;
        /// <summary>Below this planar speed (m/s) the body counts as standing.</summary>
        public float StandSpeed = 0.2f;
        /// <summary>Damping rate of the VelX/VelZ parameters (1/s).</summary>
        public float VelocityDamping = 10f;
        /// <summary>Fastest native clip speed in the blend tree (m/s); above it playback speeds up.</summary>
        public float MaxNativeSpeed = 6.0f;
        /// <summary>Cap of the playback-rate scale.</summary>
        public float MaxPlaybackScale = 1.35f;

        private Vector2 _localVel;
        private float _turnParam;
        private bool _turningInPlace;

        /// <summary>Smoothed local velocity (x = right, y = forward) in m/s.</summary>
        public Vector2 LocalVelocity => _localVel;
        /// <summary>Planar speed of the smoothed local velocity (m/s).</summary>
        public float Speed => _localVel.magnitude;
        /// <summary>In-place turn parameter -1..1 (left .. right).</summary>
        public float TurnParam => _turnParam;
        /// <summary>True while the body is pivoting on the spot.</summary>
        public bool TurningInPlace => _turningInPlace;

        /// <summary>Resets the smoothed state (spawn, teleport, pool reuse).</summary>
        public void Reset()
        {
            _localVel = Vector2.zero;
            _turnParam = 0f;
            _turningInPlace = false;
        }

        /// <summary>
        /// Advances one frame. Returns the new body yaw (degrees, world). <paramref name="aimDirection"/> is null when the
        /// body is not aiming; <paramref name="faceDirection"/> is an optional explicit facing (scripted moves).
        /// </summary>
        public float Step(float dt, float currentYaw, Vector3 worldVelocity, Vector3? aimDirection, Vector3? faceDirection)
        {
            if (dt <= 0f) return currentYaw;
            var planar = new Vector3(worldVelocity.x, 0f, worldVelocity.z);
            var speed = planar.magnitude;
            var moving = speed > StandSpeed;

            // Target yaw: aim > explicit facing > travel direction > hold.
            var targetYaw = currentYaw;
            var hasTarget = false;
            if (aimDirection.HasValue && Flat(aimDirection.Value, out var aim))
            {
                targetYaw = MathX.BearingOf(aim);
                hasTarget = true;
            }
            else if (faceDirection.HasValue && Flat(faceDirection.Value, out var face))
            {
                targetYaw = MathX.BearingOf(face);
                hasTarget = true;
            }
            else if (moving)
            {
                targetYaw = MathX.BearingOf(planar);
                hasTarget = true;
            }

            var error = MathX.WrapAngle180(targetYaw - currentYaw);
            var newYaw = currentYaw;
            var yawRate = 0f;
            if (hasTarget)
            {
                float rate;
                if (moving)
                {
                    rate = MoveTurnRate;
                    _turningInPlace = false;
                }
                else
                {
                    // Standing: hysteresis so small errors are left to the spine aim instead of shuffling the feet.
                    if (!_turningInPlace && Mathf.Abs(error) > TurnStartAngle) _turningInPlace = true;
                    else if (_turningInPlace && Mathf.Abs(error) < TurnStopAngle) _turningInPlace = false;
                    rate = _turningInPlace ? (aimDirection.HasValue ? AimTurnRate : IdleTurnRate) : 0f;
                }
                var step = Mathf.Clamp(error, -rate * dt, rate * dt);
                newYaw = MathX.WrapAngle360(currentYaw + step);
                yawRate = step / dt;
            }
            else
            {
                _turningInPlace = false;
            }

            // Local velocity relative to the new facing.
            var local = LocalOf(planar, newYaw);
            _localVel = MathX.Damp(_localVel, local, VelocityDamping, dt);
            if (_localVel.sqrMagnitude < 1e-6f) _localVel = Vector2.zero;
            var targetTurn = _turningInPlace ? Mathf.Clamp(yawRate / 180f, -1f, 1f) : 0f;
            _turnParam = MathX.Damp(_turnParam, targetTurn, 12f, dt);
            return newYaw;
        }

        /// <summary>Playback-rate multiplier so feet keep up beyond the fastest clip (1 inside the blend range).</summary>
        public float PlaybackScale(float speed)
        {
            if (MaxNativeSpeed <= 0f || speed <= MaxNativeSpeed) return 1f;
            return Mathf.Min(MaxPlaybackScale, speed / MaxNativeSpeed);
        }

        /// <summary>World planar velocity expressed in a body frame of the given yaw (x = right, y = forward).</summary>
        public static Vector2 LocalOf(Vector3 worldVelocity, float yawDegrees)
        {
            var inv = Quaternion.Euler(0f, -yawDegrees, 0f);
            var l = inv * new Vector3(worldVelocity.x, 0f, worldVelocity.z);
            return new Vector2(l.x, l.z);
        }

        private static bool Flat(Vector3 v, out Vector3 flat)
        {
            flat = new Vector3(v.x, 0f, v.z);
            if (flat.sqrMagnitude < 1e-6f) return false;
            flat.Normalize();
            return true;
        }
    }
}
