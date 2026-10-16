using UnityEngine;

namespace LongShadow.Input
{
    /// <summary>
    /// Drops isolated one-frame mouse-delta spikes (browser pointer-lock glitches): a frame whose magnitude exceeds
    /// <see cref="Factor"/> x the running median of recent non-zero magnitudes (and <see cref="MinSpike"/> px) is replaced by
    /// zero, but never two frames in a row, so real fast flicks pass after one frame. Allocation-free.
    /// </summary>
    public sealed class SpikeFilter
    {
        private readonly float[] _history;
        private readonly float[] _scratch;
        private int _count;
        private int _next;
        private bool _droppedLast;

        /// <summary>Spike threshold as a multiple of the median (TECH_PLAN §7.2: 8).</summary>
        public float Factor { get; set; }
        /// <summary>Minimum magnitude in CSS px for a spike (avoids dropping small moves after stillness).</summary>
        public float MinSpike { get; set; }
        /// <summary>Samples needed before filtering starts.</summary>
        public int MinSamples { get; set; } = 5;
        /// <summary>Number of frames dropped so far.</summary>
        public int Dropped { get; private set; }

        /// <summary>Creates a filter with a median window of <paramref name="window"/> samples.</summary>
        public SpikeFilter(int window = 15, float factor = 8f, float minSpike = 40f)
        {
            window = Mathf.Max(3, window);
            _history = new float[window];
            _scratch = new float[window];
            Factor = factor;
            MinSpike = minSpike;
        }

        /// <summary>Current running median of non-zero magnitudes (0 until samples exist).</summary>
        public float Median
        {
            get
            {
                if (_count == 0) return 0f;
                for (var i = 0; i < _count; i++) _scratch[i] = _history[i];
                // insertion sort (window is small)
                for (var i = 1; i < _count; i++)
                {
                    var v = _scratch[i];
                    var j = i - 1;
                    while (j >= 0 && _scratch[j] > v)
                    {
                        _scratch[j + 1] = _scratch[j];
                        j--;
                    }
                    _scratch[j + 1] = v;
                }
                return (_count & 1) == 1 ? _scratch[_count / 2] : 0.5f * (_scratch[_count / 2 - 1] + _scratch[_count / 2]);
            }
        }

        /// <summary>Filters one frame's delta.</summary>
        public Vector2 Filter(Vector2 delta)
        {
            var mag = delta.magnitude;
            if (mag <= 0f) return delta;
            if (!_droppedLast && _count >= MinSamples && mag > MinSpike)
            {
                var median = Median;
                if (median > 0f && mag > Factor * median)
                {
                    _droppedLast = true;
                    Dropped++;
                    return Vector2.zero;
                }
            }
            _droppedLast = false;
            _history[_next] = mag;
            _next = (_next + 1) % _history.Length;
            if (_count < _history.Length) _count++;
            return delta;
        }

        /// <summary>Clears the history (pointer re-lock).</summary>
        public void Reset()
        {
            _count = 0;
            _next = 0;
            _droppedLast = false;
        }
    }
}
