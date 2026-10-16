using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Body
{
    /// <summary>
    /// Fallback footstep timing from the locomotion cycle phase (two plants per cycle) for clips that carry no
    /// Foot_L/Foot_R events yet (the ClipImporter footstep pass adds real contact times). As soon as a clip event
    /// arrives the body stops using this clock for a while, so events are never doubled.
    /// </summary>
    public sealed class FootstepClock
    {
        /// <summary>Phase of the left plant within a cycle (0..1).</summary>
        public float LeftPhase = 0.0f;
        /// <summary>Phase of the right plant within a cycle (0..1).</summary>
        public float RightPhase = 0.5f;
        /// <summary>No steps below this speed (m/s).</summary>
        public float MinSpeed = 0.35f;

        private float _lastPhase = -1f;

        /// <summary>Forgets the previous phase (state change, teleport).</summary>
        public void Reset() => _lastPhase = -1f;

        /// <summary>
        /// Advances with the locomotion state's normalized time. Returns <see cref="AnimEventIds.FootL"/>,
        /// <see cref="AnimEventIds.FootR"/> or null. At most one event per call (frame).
        /// </summary>
        public string Step(float normalizedTime, float speed)
        {
            var phase = normalizedTime - Mathf.Floor(normalizedTime);
            if (speed < MinSpeed || _lastPhase < 0f)
            {
                _lastPhase = phase;
                return null;
            }
            var last = _lastPhase;
            _lastPhase = phase;
            if (Crossed(last, phase, LeftPhase)) return AnimEventIds.FootL;
            if (Crossed(last, phase, RightPhase)) return AnimEventIds.FootR;
            return null;
        }

        /// <summary>True if the phase moved forward over <paramref name="mark"/> (wrapping at 1).</summary>
        public static bool Crossed(float from, float to, float mark)
        {
            if (Mathf.Approximately(from, to)) return false;
            if (to >= from) return mark > from && mark <= to;
            // Wrapped past 1 → 0.
            return mark > from || mark <= to;
        }
    }
}
