using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Damped spring on a Vector3 (see <see cref="SpringFloat"/>). Mutable struct: step the field, not a copy.</summary>
    [Serializable]
    public struct SpringVector3
    {
        /// <summary>Natural frequency in Hz.</summary>
        public float Frequency;
        /// <summary>Damping ratio (1 = critical).</summary>
        public float Damping;
        /// <summary>Current value.</summary>
        public Vector3 Value;
        /// <summary>Current velocity.</summary>
        public Vector3 Velocity;
        /// <summary>Rest target.</summary>
        public Vector3 Target;

        /// <summary>Creates a spring at rest on <paramref name="initial"/>.</summary>
        public SpringVector3(float frequency, float damping, Vector3 initial = default)
        {
            Frequency = frequency;
            Damping = damping;
            Value = Target = initial;
            Velocity = Vector3.zero;
        }

        /// <summary>Advances by <paramref name="dt"/> seconds; returns the new value.</summary>
        public Vector3 Step(float dt)
        {
            SpringFloat.Integrate(ref Value.x, ref Velocity.x, Target.x, Frequency, Damping, dt);
            SpringFloat.Integrate(ref Value.y, ref Velocity.y, Target.y, Frequency, Damping, dt);
            SpringFloat.Integrate(ref Value.z, ref Velocity.z, Target.z, Frequency, Damping, dt);
            return Value;
        }

        /// <summary>Snaps to a value at rest.</summary>
        public void Reset(Vector3 value)
        {
            Value = Target = value;
            Velocity = Vector3.zero;
        }

        /// <summary>Adds velocity.</summary>
        public void AddImpulse(Vector3 velocity) => Velocity += velocity;
    }
}
