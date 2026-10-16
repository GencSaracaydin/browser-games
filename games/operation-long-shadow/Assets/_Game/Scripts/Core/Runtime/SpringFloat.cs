using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Damped spring on a float (procedural sway, kick, bob). Implicit integration: stable for any time step.
    /// Mutable struct: keep it in a field and call <see cref="Step"/> on the field (not on a copy).
    /// </summary>
    [Serializable]
    public struct SpringFloat
    {
        /// <summary>Natural frequency in Hz.</summary>
        public float Frequency;
        /// <summary>Damping ratio (1 = critical, &lt; 1 overshoots).</summary>
        public float Damping;
        /// <summary>Current value.</summary>
        public float Value;
        /// <summary>Current velocity (units/s).</summary>
        public float Velocity;
        /// <summary>Rest target.</summary>
        public float Target;

        /// <summary>Creates a spring at rest on <paramref name="initial"/>.</summary>
        public SpringFloat(float frequency, float damping, float initial = 0f)
        {
            Frequency = frequency;
            Damping = damping;
            Value = Target = initial;
            Velocity = 0f;
        }

        /// <summary>Advances by <paramref name="dt"/> seconds; returns the new value.</summary>
        public float Step(float dt)
        {
            Integrate(ref Value, ref Velocity, Target, Frequency, Damping, dt);
            return Value;
        }

        /// <summary>Snaps to a value at rest.</summary>
        public void Reset(float value)
        {
            Value = Target = value;
            Velocity = 0f;
        }

        /// <summary>Adds velocity (kicks).</summary>
        public void AddImpulse(float velocity) => Velocity += velocity;

        /// <summary>Implicit damped-spring step shared by all spring types.</summary>
        public static void Integrate(ref float x, ref float v, float target, float frequencyHz, float damping, float dt)
        {
            if (dt <= 0f) return;
            var omega = 2f * Mathf.PI * Mathf.Max(0f, frequencyHz);
            var f = 1f + 2f * dt * damping * omega;
            var oo = omega * omega;
            var hoo = dt * oo;
            var hhoo = dt * hoo;
            var detInv = 1f / (f + hhoo);
            var detX = f * x + dt * v + hhoo * target;
            var detV = v + hoo * (target - x);
            x = detX * detInv;
            v = detV * detInv;
        }
    }
}
