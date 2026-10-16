using System;

namespace LongShadow.Core
{
    /// <summary>
    /// Simple countdown (fuses, OOB countdown, cooldowns). Pass scaled or unscaled delta time explicitly (R3).
    /// Mutable struct: tick the field, not a copy.
    /// </summary>
    [Serializable]
    public struct CountdownTimer
    {
        /// <summary>Length of the current run in seconds.</summary>
        public float Duration;
        /// <summary>Seconds left (0 when finished or stopped).</summary>
        public float Remaining;
        /// <summary>True while counting.</summary>
        public bool IsRunning;

        /// <summary>Starts (or restarts) a countdown.</summary>
        public void Start(float seconds)
        {
            Duration = Math.Max(0f, seconds);
            Remaining = Duration;
            IsRunning = true;
        }

        /// <summary>Stops without finishing.</summary>
        public void Stop()
        {
            IsRunning = false;
            Remaining = 0f;
        }

        /// <summary>Advances; returns true exactly on the tick that reaches zero.</summary>
        public bool Tick(float dt)
        {
            if (!IsRunning) return false;
            Remaining -= dt;
            if (Remaining > 0f) return false;
            Remaining = 0f;
            IsRunning = false;
            return true;
        }

        /// <summary>Elapsed fraction 0..1 of the current run (1 when finished).</summary>
        public float Progress01 => Duration <= 0f ? 1f : 1f - Math.Max(0f, Remaining) / Duration;

        /// <summary>Whole seconds left rounded up (for "6…5…4" banners).</summary>
        public int SecondsLeftCeil => (int)Math.Ceiling(Math.Max(0f, Remaining));
    }
}
