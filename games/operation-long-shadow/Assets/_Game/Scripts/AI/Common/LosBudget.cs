namespace LongShadow.AI.Common
{
    /// <summary>
    /// Per-frame raycast budget for AI line-of-sight checks (TECH_PLAN §7.6: round-robin LOS budget, at most 24 raycasts
    /// per frame on the AISight mask; §7.16 AI ≤ 3 ms). Every AI raycast (perception, cover tests, closet checks,
    /// in-cover tests) goes through <see cref="TryUse"/>; a caller that gets false defers its query to a later frame.
    /// </summary>
    public sealed class LosBudget
    {
        /// <summary>Default rays per frame (TECH_PLAN §7.6).</summary>
        public const int DefaultPerFrame = 24;

        private int _frame = int.MinValue;
        private int _used;

        /// <summary>Creates a budget.</summary>
        public LosBudget(int perFrame = DefaultPerFrame) => PerFrame = perFrame < 1 ? 1 : perFrame;

        /// <summary>Rays allowed per frame.</summary>
        public int PerFrame { get; set; }

        /// <summary>Rays used in the current frame.</summary>
        public int UsedThisFrame => _used;

        /// <summary>Highest number of rays ever used in one frame (diagnostics; never above <see cref="PerFrame"/>).</summary>
        public int PeakPerFrame { get; private set; }

        /// <summary>Rays left in the frame <paramref name="frame"/>.</summary>
        public int Remaining(int frame)
        {
            Sync(frame);
            return PerFrame - _used;
        }

        /// <summary>Consumes <paramref name="rays"/> rays in frame <paramref name="frame"/> if they fit; false otherwise (nothing consumed).</summary>
        public bool TryUse(int frame, int rays = 1)
        {
            Sync(frame);
            if (rays <= 0) return true;
            if (_used + rays > PerFrame) return false;
            _used += rays;
            if (_used > PeakPerFrame) PeakPerFrame = _used;
            return true;
        }

        private void Sync(int frame)
        {
            if (frame == _frame) return;
            _frame = frame;
            _used = 0;
        }
    }
}
