using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.Player.Bounds
{
    /// <summary>
    /// Rules of the out-of-bounds countdown (GDD §17), pure and clock-driven for tests: entering the first OOB volume
    /// starts a countdown (6 s by default); being inside several overlapping volumes keeps one countdown; leaving every
    /// volume cancels it; reaching zero fails once (until <see cref="Reset"/>). <see cref="Tick"/> reports each new whole
    /// second for the "RETURN TO THE MISSION AREA n…" banner.
    /// </summary>
    public sealed class OutOfBoundsCountdown
    {
        /// <summary>What a <see cref="Tick"/> produced.</summary>
        public enum Result
        {
            /// <summary>Nothing new.</summary>
            None = 0,
            /// <summary>A new whole second is showing (<see cref="SecondsLeft"/>).</summary>
            SecondChanged = 1,
            /// <summary>The countdown reached zero: fail.</summary>
            Expired = 2,
        }

        private readonly List<string> _inside = new List<string>(4);
        private CountdownTimer _timer;
        private int _shownSecond = -1;

        /// <summary>True while counting down.</summary>
        public bool IsCounting => _timer.IsRunning;
        /// <summary>True after the countdown expired (until <see cref="Reset"/>).</summary>
        public bool HasFailed { get; private set; }
        /// <summary>Whole seconds left, rounded up.</summary>
        public int SecondsLeft => _timer.SecondsLeftCeil;
        /// <summary>Seconds left (exact).</summary>
        public float Remaining => _timer.Remaining;
        /// <summary>Number of OOB volumes the player is inside.</summary>
        public int InsideCount => _inside.Count;

        /// <summary>The player entered a volume. Returns true when this started a countdown.</summary>
        public bool Enter(string volumeId, float seconds)
        {
            if (string.IsNullOrEmpty(volumeId) || _inside.Contains(volumeId)) return false;
            _inside.Add(volumeId);
            if (_timer.IsRunning || HasFailed) return false;
            _timer.Start(seconds > 0f ? seconds : 6f); // GDD §17: 6 s
            _shownSecond = -1;
            return true;
        }

        /// <summary>The player left a volume. Returns true when this cancelled the countdown.</summary>
        public bool Exit(string volumeId)
        {
            if (!_inside.Remove(volumeId)) return false;
            if (_inside.Count > 0 || !_timer.IsRunning) return false;
            _timer.Stop();
            _shownSecond = -1;
            return true;
        }

        /// <summary>Advances the countdown.</summary>
        public Result Tick(float dt)
        {
            if (!_timer.IsRunning) return Result.None;
            if (_timer.Tick(dt))
            {
                HasFailed = true;
                return Result.Expired;
            }
            var s = _timer.SecondsLeftCeil;
            if (s == _shownSecond) return Result.None;
            _shownSecond = s;
            return Result.SecondChanged;
        }

        /// <summary>Stops the countdown but remembers which volumes the player is in (death, cinematics).</summary>
        public void Cancel()
        {
            _timer.Stop();
            _shownSecond = -1;
        }

        /// <summary>Forgets everything (checkpoint reload, teleport).</summary>
        public void Reset()
        {
            _inside.Clear();
            _timer.Stop();
            _shownSecond = -1;
            HasFailed = false;
        }
    }
}
