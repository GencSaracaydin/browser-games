using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Damped spring on a rotation (view punch, weapon sway). Springs the quaternion components in the target's
    /// hemisphere and renormalises; good for the small angles it is used for. Mutable struct: step the field.
    /// </summary>
    [Serializable]
    public struct SpringQuaternion
    {
        /// <summary>Natural frequency in Hz.</summary>
        public float Frequency;
        /// <summary>Damping ratio (1 = critical).</summary>
        public float Damping;
        /// <summary>Current rotation.</summary>
        public Quaternion Value;
        /// <summary>Component velocity.</summary>
        public Vector4 Velocity;
        /// <summary>Rest target.</summary>
        public Quaternion Target;

        /// <summary>Creates a spring at rest on identity.</summary>
        public SpringQuaternion(float frequency, float damping)
        {
            Frequency = frequency;
            Damping = damping;
            Value = Target = Quaternion.identity;
            Velocity = Vector4.zero;
        }

        /// <summary>Advances by <paramref name="dt"/> seconds; returns the new rotation.</summary>
        public Quaternion Step(float dt)
        {
            if (Value.x == 0f && Value.y == 0f && Value.z == 0f && Value.w == 0f) Value = Quaternion.identity;
            if (Target.x == 0f && Target.y == 0f && Target.z == 0f && Target.w == 0f) Target = Quaternion.identity;
            var t = Target;
            if (Quaternion.Dot(Value, t) < 0f) t = new Quaternion(-t.x, -t.y, -t.z, -t.w);
            float x = Value.x, y = Value.y, z = Value.z, w = Value.w;
            SpringFloat.Integrate(ref x, ref Velocity.x, t.x, Frequency, Damping, dt);
            SpringFloat.Integrate(ref y, ref Velocity.y, t.y, Frequency, Damping, dt);
            SpringFloat.Integrate(ref z, ref Velocity.z, t.z, Frequency, Damping, dt);
            SpringFloat.Integrate(ref w, ref Velocity.w, t.w, Frequency, Damping, dt);
            var mag = Mathf.Sqrt(x * x + y * y + z * z + w * w);
            Value = mag > 1e-6f ? new Quaternion(x / mag, y / mag, z / mag, w / mag) : Quaternion.identity;
            return Value;
        }

        /// <summary>Snaps to a rotation at rest.</summary>
        public void Reset(Quaternion value)
        {
            Value = Target = value;
            Velocity = Vector4.zero;
        }

        /// <summary>Offsets the current rotation (a punch that springs back to the target).</summary>
        public void Punch(Quaternion offset) => Value = offset * Value;
    }
}
