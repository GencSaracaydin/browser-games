namespace LongShadow.AI.Common
{
    /// <summary>
    /// The shared Legion grenade pool (GDD §14.1: "1 throw per 60 s mission-wide"; GDD §16 interval 90 / 60 / 40 / 30 s by
    /// difficulty). The first throw is available <see cref="FirstDelaySeconds"/> after the pool starts so an ambush does
    /// not open with a frag. Pure C#.
    /// </summary>
    public sealed class LegionGrenadePool
    {
        /// <summary>Grace period before the first Legion throw of the mission.</summary>
        public const float FirstDelaySeconds = 15f;

        private float _nextAllowed = float.NegativeInfinity;
        private bool _started;

        /// <summary>Seconds between throws (difficulty).</summary>
        public float Interval { get; set; } = 60f;

        /// <summary>Time of the last throw (NegativeInfinity if none).</summary>
        public float LastThrow { get; private set; } = float.NegativeInfinity;

        /// <summary>True if a throw is allowed now (does not consume).</summary>
        public bool Available(float now)
        {
            EnsureStarted(now);
            return now >= _nextAllowed;
        }

        /// <summary>Consumes the pool if a throw is allowed now.</summary>
        public bool TryTake(float now)
        {
            if (!Available(now)) return false;
            LastThrow = now;
            _nextAllowed = now + Interval;
            return true;
        }

        /// <summary>Resets the pool (checkpoint reload): the next throw waits the grace period again.</summary>
        public void Reset(float now)
        {
            _started = true;
            _nextAllowed = now + FirstDelaySeconds;
            LastThrow = float.NegativeInfinity;
        }

        private void EnsureStarted(float now)
        {
            if (_started) return;
            _started = true;
            _nextAllowed = now + FirstDelaySeconds;
        }
    }
}
