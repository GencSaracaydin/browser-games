using System;
using System.Collections.Generic;

namespace LongShadow.Core
{
    /// <summary>
    /// Random draws without repetition (DI_01-06 death lines, bark variants): every item is drawn once per cycle, in a
    /// fresh random order each cycle, and the first draw of a cycle never repeats the last draw of the previous one
    /// (when there are at least two items). Allocation-free after construction.
    /// </summary>
    public sealed class ShuffleBag<T>
    {
        private readonly List<T> _items = new List<T>();
        private readonly List<int> _order = new List<int>();
        private readonly Random _random;
        private int _cursor;
        private int _lastIndex = -1;

        /// <summary>Creates a bag. A seed of 0 uses a time-based seed.</summary>
        public ShuffleBag(IEnumerable<T> items = null, int seed = 0)
        {
            _random = seed == 0 ? new Random() : new Random(seed);
            if (items != null)
                foreach (var i in items) _items.Add(i);
            _cursor = int.MaxValue;
        }

        /// <summary>Number of distinct items.</summary>
        public int Count => _items.Count;

        /// <summary>Items left in the current cycle.</summary>
        public int RemainingInCycle => _cursor >= _order.Count ? 0 : _order.Count - _cursor;

        /// <summary>The last drawn item (default if none).</summary>
        public T Last => _lastIndex >= 0 && _lastIndex < _items.Count ? _items[_lastIndex] : default;

        /// <summary>Adds an item; it joins from the next cycle.</summary>
        public void Add(T item) => _items.Add(item);

        /// <summary>Starts a new cycle on the next draw.</summary>
        public void Reset() => _cursor = int.MaxValue;

        /// <summary>Draws the next item. Throws if the bag is empty.</summary>
        public T Next()
        {
            if (_items.Count == 0) throw new InvalidOperationException("ShuffleBag is empty");
            if (_cursor >= _order.Count) Refill();
            _lastIndex = _order[_cursor++];
            return _items[_lastIndex];
        }

        private void Refill()
        {
            _order.Clear();
            for (var i = 0; i < _items.Count; i++) _order.Add(i);
            for (var i = _order.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
            if (_order.Count > 1 && _order[0] == _lastIndex)
            {
                var j = 1 + _random.Next(_order.Count - 1);
                (_order[0], _order[j]) = (_order[j], _order[0]);
            }
            _cursor = 0;
        }
    }
}
