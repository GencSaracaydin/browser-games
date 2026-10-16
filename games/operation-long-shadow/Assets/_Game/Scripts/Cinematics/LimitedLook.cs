using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Limited mouse look of the cinematic POV cameras (GDD §8: ±40° yaw, ±20° pitch, springing back to centre 2 s after
    /// input stops; MID-B ±20°). Pure state machine on unscaled time: offsets are added to the shot's base orientation.
    /// A forced recentre (intro shot 7, the gloved hand turning Calder's head) eases the offsets to zero and blocks input
    /// while it runs.
    /// </summary>
    public sealed class LimitedLook
    {
        /// <summary>Default yaw limit in degrees (GDD §8).</summary>
        public const float DefaultYaw = 40f;
        /// <summary>Default pitch limit in degrees (GDD §8).</summary>
        public const float DefaultPitch = 20f;
        /// <summary>Idle seconds before the view springs back (GDD §8).</summary>
        public const float DefaultSpringDelay = 2f;
        /// <summary>Spring-back rate (1/s, exponential).</summary>
        public const float SpringRate = 2.2f;
        /// <summary>Soft edge: the last part of the range resists input (fraction of the limit).</summary>
        public const float SoftEdge = 0.2f;

        private float _yaw;
        private float _pitch;
        private float _idle;
        private float _recentreFrom;
        private float _recentreTime;
        private float _recentreDuration;
        private float _recentreYaw;
        private float _recentrePitch;

        /// <summary>Look enabled (input accepted).</summary>
        public bool Enabled { get; set; }
        /// <summary>Yaw limit (±degrees).</summary>
        public float YawLimit { get; set; } = DefaultYaw;
        /// <summary>Upward pitch limit (degrees).</summary>
        public float PitchUpLimit { get; set; } = DefaultPitch;
        /// <summary>Downward pitch limit (degrees).</summary>
        public float PitchDownLimit { get; set; } = DefaultPitch;
        /// <summary>Idle seconds before springing back (≤ 0: never).</summary>
        public float SpringDelay { get; set; } = DefaultSpringDelay;

        /// <summary>Current yaw offset (degrees, positive = right).</summary>
        public float Yaw => _yaw;
        /// <summary>Current pitch offset (degrees, positive = up).</summary>
        public float Pitch => _pitch;
        /// <summary>True while a forced recentre runs.</summary>
        public bool Recentring => _recentreDuration > 0f;

        /// <summary>Offset rotation to apply after the base orientation (yaw about up, then pitch about right).</summary>
        public Quaternion Rotation => Quaternion.Euler(-_pitch, _yaw, 0f);

        /// <summary>Zeroes the offsets.</summary>
        public void Reset()
        {
            _yaw = _pitch = 0f;
            _idle = 0f;
            _recentreDuration = 0f;
        }

        /// <summary>Starts a forced recentre over <paramref name="seconds"/> (cubic ease). Input is ignored meanwhile.</summary>
        public void Recentre(float seconds)
        {
            if (seconds <= 0f)
            {
                _yaw = _pitch = 0f;
                _recentreDuration = 0f;
                return;
            }
            _recentreYaw = _yaw;
            _recentrePitch = _pitch;
            _recentreFrom = 0f;
            _recentreTime = 0f;
            _recentreDuration = seconds;
        }

        /// <summary>
        /// Advances the look by <paramref name="dt"/> unscaled seconds with an input delta in degrees (x = yaw right,
        /// y = pitch up).
        /// </summary>
        public void Tick(float dt, Vector2 deltaDeg)
        {
            if (_recentreDuration > 0f)
            {
                _recentreTime += dt;
                var k = Easing.Evaluate(ShotEase.EaseInOutCubic, _recentreTime / _recentreDuration);
                _yaw = Mathf.Lerp(_recentreYaw, _recentreFrom, k);
                _pitch = Mathf.Lerp(_recentrePitch, _recentreFrom, k);
                if (_recentreTime >= _recentreDuration) _recentreDuration = 0f;
                _idle = 0f;
                return;
            }

            if (!Enabled) deltaDeg = Vector2.zero;
            if (deltaDeg.sqrMagnitude > 1e-8f)
            {
                _idle = 0f;
                _yaw = ApplySoft(_yaw, deltaDeg.x, YawLimit, YawLimit);
                _pitch = ApplySoft(_pitch, deltaDeg.y, PitchUpLimit, PitchDownLimit);
                return;
            }

            _idle += dt;
            var springAlways = !Enabled;
            if (springAlways || (SpringDelay > 0f && _idle >= SpringDelay))
            {
                var k = 1f - Mathf.Exp(-SpringRate * dt);
                _yaw = Mathf.Lerp(_yaw, 0f, k);
                _pitch = Mathf.Lerp(_pitch, 0f, k);
                if (Mathf.Abs(_yaw) < 0.01f) _yaw = 0f;
                if (Mathf.Abs(_pitch) < 0.01f) _pitch = 0f;
            }
        }

        /// <summary>Adds input with resistance in the outer <see cref="SoftEdge"/> of the range, then clamps.</summary>
        private static float ApplySoft(float value, float delta, float posLimit, float negLimit)
        {
            var limit = delta >= 0f ? posLimit : negLimit;
            if (limit <= 0f) return Mathf.Clamp(value, -negLimit, posLimit);
            var edge = limit * (1f - SoftEdge);
            var outward = delta >= 0f ? value : -value;
            if (outward > edge)
            {
                var over = (outward - edge) / Mathf.Max(0.0001f, limit - edge);
                delta *= Mathf.Clamp01(1f - over);
            }
            return Mathf.Clamp(value + delta, -negLimit, posLimit);
        }
    }
}
