using System.Collections.Generic;
using System.Text;

namespace LongShadow.Platform
{
    /// <summary>
    /// Pure bookkeeping behind <see cref="TimeScaleService"/> (TECH_PLAN §5.4): a set of time-scale requests where the
    /// request with the highest priority wins; between equal priorities the most recent one wins. With no request the
    /// scale is 1. Handles are positive and never reused within one stack.
    /// </summary>
    public sealed class TimeScaleStack
    {
        /// <summary>Upper bound accepted for a requested scale (fast-forward in dev tools).</summary>
        public const float MaxScale = 100f;

        private struct Request
        {
            public int Handle;
            public float Scale;
            public int Priority;
            public int Order;
            public string Reason;
        }

        private readonly List<Request> _requests = new List<Request>(8);
        private int _nextHandle = 1;
        private int _order;

        /// <summary>Number of live requests.</summary>
        public int Count => _requests.Count;

        /// <summary>Adds a request and returns its handle. NaN/infinite scales become 1; negative ones 0.</summary>
        public int Push(float scale, int priority, string reason)
        {
            var r = new Request
            {
                Handle = _nextHandle++,
                Scale = Sanitize(scale),
                Priority = priority,
                Order = ++_order,
                Reason = reason ?? string.Empty,
            };
            _requests.Add(r);
            return r.Handle;
        }

        /// <summary>Removes a request. Returns false for unknown (or already popped) handles.</summary>
        public bool Pop(int handle)
        {
            for (var i = 0; i < _requests.Count; i++)
            {
                if (_requests[i].Handle != handle) continue;
                _requests.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>Changes the scale of a live request (ramps such as the death slow-down). Returns false if unknown.</summary>
        public bool SetScale(int handle, float scale)
        {
            for (var i = 0; i < _requests.Count; i++)
            {
                if (_requests[i].Handle != handle) continue;
                var r = _requests[i];
                r.Scale = Sanitize(scale);
                _requests[i] = r;
                return true;
            }
            return false;
        }

        /// <summary>True if the handle is live.</summary>
        public bool Contains(int handle)
        {
            for (var i = 0; i < _requests.Count; i++)
                if (_requests[i].Handle == handle) return true;
            return false;
        }

        /// <summary>Effective scale: highest priority, then most recent; 1 when empty.</summary>
        public float Evaluate() => Evaluate(out _);

        /// <summary>Effective scale and the reason of the winning request (empty when none).</summary>
        public float Evaluate(out string reason)
        {
            reason = string.Empty;
            if (_requests.Count == 0) return 1f;
            var best = 0;
            for (var i = 1; i < _requests.Count; i++)
            {
                var a = _requests[i];
                var b = _requests[best];
                if (a.Priority > b.Priority || (a.Priority == b.Priority && a.Order > b.Order)) best = i;
            }
            reason = _requests[best].Reason;
            return _requests[best].Scale;
        }

        /// <summary>Removes every request.</summary>
        public void Clear() => _requests.Clear();

        /// <summary>Appends one line per request (diagnostics; allocates).</summary>
        public void Describe(StringBuilder sb)
        {
            foreach (var r in _requests)
                sb.Append('#').Append(r.Handle).Append(" x").Append(r.Scale.ToString("0.###"))
                  .Append(" p").Append(r.Priority).Append(' ').Append(r.Reason).Append('\n');
        }

        private static float Sanitize(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale)) return 1f;
            if (scale < 0f) return 0f;
            return scale > MaxScale ? MaxScale : scale;
        }
    }
}
