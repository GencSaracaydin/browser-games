using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// Priority queue of one VO channel (TECH_PLAN §7.10: Scripted &gt; Objective &gt; Callout &gt; Bark): the next request is
    /// the eligible one (its delay passed) with the highest priority, urgent ones first within a priority, then FIFO.
    /// Requests past their expiry (stale callouts) are dropped. Small lists, no per-frame allocation. Pure (unit-tested).
    /// </summary>
    public sealed class VOQueue
    {
        /// <summary>Seconds a callout may wait before it is dropped (a late "Rooftop!" is wrong, GDD §15).</summary>
        public const float CalloutMaxWait = 1.5f;

        private readonly List<VORequest> _items = new List<VORequest>(8);

        /// <summary>Queued requests.</summary>
        public int Count => _items.Count;

        /// <summary>Queued request at an index (debug, tests).</summary>
        public VORequest this[int index] => _items[index];

        /// <summary>
        /// True when a new request should cut the one playing on the channel: the caller asked to interrupt, or the
        /// new one outranks a callout or bark (a scripted or objective line never waits for chatter).
        /// </summary>
        public static bool ShouldInterrupt(VOPriority playing, VOPriority incoming, bool interruptRequested)
        {
            if (interruptRequested) return true;
            return incoming > playing && playing <= VOPriority.Callout;
        }

        /// <summary>Adds a request.</summary>
        public void Enqueue(VORequest r)
        {
            if (r != null) _items.Add(r);
        }

        /// <summary>Drops expired requests into <paramref name="dropped"/> (may be null); returns how many.</summary>
        public int DropExpired(float now, List<VORequest> dropped)
        {
            var n = 0;
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (now < _items[i].ExpiresAt) continue;
                dropped?.Add(_items[i]);
                _items.RemoveAt(i);
                n++;
            }
            return n;
        }

        /// <summary>The request that would start now, or null.</summary>
        public VORequest PeekNext(float now)
        {
            VORequest best = null;
            for (var i = 0; i < _items.Count; i++)
            {
                var r = _items[i];
                if (now < r.EligibleAt || now >= r.ExpiresAt) continue;
                if (best == null || Before(r, best)) best = r;
            }
            return best;
        }

        /// <summary>Removes and returns the next request, or null.</summary>
        public VORequest PopNext(float now)
        {
            var r = PeekNext(now);
            if (r != null) _items.Remove(r);
            return r;
        }

        /// <summary>Removes a request by handle.</summary>
        public bool Remove(int handle)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].Handle != handle) continue;
                _items.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>Removes a specific request.</summary>
        public bool Remove(VORequest r) => _items.Remove(r);

        /// <summary>The queued request of a line, or null.</summary>
        public VORequest FindLine(string lineId)
        {
            for (var i = 0; i < _items.Count; i++)
                if (_items[i].LineId == lineId) return _items[i];
            return null;
        }

        /// <summary>The queued request with a handle, or null.</summary>
        public VORequest FindHandle(int handle)
        {
            for (var i = 0; i < _items.Count; i++)
                if (_items[i].Handle == handle) return _items[i];
            return null;
        }

        /// <summary>True if a request of at least <paramref name="priority"/> is queued.</summary>
        public bool HasAtLeast(VOPriority priority)
        {
            for (var i = 0; i < _items.Count; i++)
                if (_items[i].Priority >= priority) return true;
            return false;
        }

        /// <summary>Empties the queue.</summary>
        public void Clear() => _items.Clear();

        private static bool Before(VORequest a, VORequest b)
        {
            if (a.Priority != b.Priority) return a.Priority > b.Priority;
            if (a.Urgent != b.Urgent) return a.Urgent;
            return a.Sequence < b.Sequence;
        }
    }
}
