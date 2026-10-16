using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>What one perceiver knows about one hostile actor (see <see cref="Perception"/>).</summary>
    public sealed class TargetMemory
    {
        /// <summary>The actor.</summary>
        public ITargetable Target;
        /// <summary>Line of sight at the last perception update.</summary>
        public bool Visible;
        /// <summary>Time line of sight began (time-on-target ramp; reset when it breaks, GDD §14.3).</summary>
        public float VisibleSince;
        /// <summary>Last time the actor was seen.</summary>
        public float LastSeen = float.NegativeInfinity;
        /// <summary>Last time the actor was heard.</summary>
        public float LastHeard = float.NegativeInfinity;
        /// <summary>Last known position (seen or heard).</summary>
        public Vector3 LastKnown;
        /// <summary>Detection meter 0..1 (GDD §14.2 detection time; the Beat 2 sentry HUD meter).</summary>
        public float Detection;
        /// <summary>True once detected (stays true until forgotten).</summary>
        public bool Aware;
        /// <summary>Distance at the last update.</summary>
        public float Distance;
        /// <summary>Last time the memory was refreshed (seen, heard or candidate).</summary>
        public float LastTouched;

        /// <summary>Seconds of continuous line of sight at <paramref name="now"/> (0 when not visible).</summary>
        public float TimeOnTarget(float now) => Visible ? Mathf.Max(0f, now - VisibleSince) : 0f;

        /// <summary>Clears the memory for reuse.</summary>
        public void Reset(ITargetable target)
        {
            Target = target;
            Visible = false;
            VisibleSince = 0f;
            LastSeen = float.NegativeInfinity;
            LastHeard = float.NegativeInfinity;
            LastKnown = target != null && target.Root != null ? target.Root.position : Vector3.zero;
            Detection = 0f;
            Aware = false;
            Distance = float.MaxValue;
            LastTouched = 0f;
        }
    }
}
