namespace LongShadow.Combat.Ballistics
{
    /// <summary>
    /// Rate-of-fire clock (GDD §13.1 RPM). Automatic fire keeps exact cadence across frames (the next shot time advances by
    /// the interval, not from the frame time), so 750 RPM is 12.5 shots per second at any frame rate; up to
    /// <see cref="MaxShotsPerFrame"/> shots are released in one long frame. Semi-automatic fire keeps a press made up to
    /// <see cref="SemiBuffer"/> seconds before the weapon is ready (the Hush Puppy's 300 RPM cap). Mutable struct.
    /// </summary>
    public struct FireCadence
    {
        /// <summary>Most shots released in one frame (long frames on the web).</summary>
        public const int MaxShotsPerFrame = 3;
        /// <summary>A semi-auto press this early before the weapon is ready still fires when it becomes ready (seconds).</summary>
        public const float SemiBuffer = 0.12f;

        /// <summary>Time (callers' clock) when the next shot is allowed.</summary>
        public float NextShotTime;
        private float _pressTime;
        private bool _pressPending;
        private bool _held;

        /// <summary>Automatic fire: the number of shots due at <paramref name="now"/> while the trigger is held.</summary>
        public int TickAuto(float now, float interval, bool triggerHeld)
        {
            _pressPending = false;
            if (!triggerHeld || interval <= 0f)
            {
                _held = false;
                return 0;
            }
            // A fresh pull fires now: no catch-up for the time the trigger was up (or blocked).
            if (!_held && NextShotTime < now) NextShotTime = now;
            _held = true;
            var shots = 0;
            while (NextShotTime <= now && shots < MaxShotsPerFrame)
            {
                shots++;
                NextShotTime += interval;
            }
            // A hitch longer than the per-frame cap drops the backlog instead of spraying it over the next frames.
            if (NextShotTime < now - interval) NextShotTime = now;
            return shots;
        }

        /// <summary>Semi-automatic: 1 when a press (this frame, or buffered) fires at <paramref name="now"/>, else 0.</summary>
        public int TickSemi(float now, float interval, bool pressedThisFrame)
        {
            if (pressedThisFrame)
            {
                _pressPending = true;
                _pressTime = now;
            }
            if (!_pressPending || NextShotTime > now) return 0;
            _pressPending = false;
            if (now - _pressTime > SemiBuffer) return 0; // pressed too early: dropped
            NextShotTime = now + interval;
            return 1;
        }

        /// <summary>Blocks firing until <paramref name="time"/> (sprint-out, raise, melee recovery).</summary>
        public void BlockUntil(float time)
        {
            if (time > NextShotTime) NextShotTime = time;
        }

        /// <summary>Clears buffered input and timing.</summary>
        public void Reset(float now)
        {
            NextShotTime = now;
            _pressPending = false;
            _held = false;
        }
    }
}
