using System.Collections.Generic;
using System.Text;
using LongShadow.Core;

namespace LongShadow.Platform
{
    /// <summary>
    /// Pure bookkeeping behind <see cref="InputLockService"/> (TECH_PLAN §5.4): reference-counted input channel locks.
    /// A channel is locked while at least one live handle includes it. Handles are positive and never reused.
    /// </summary>
    public sealed class InputLockSet
    {
        private readonly Dictionary<int, InputLockFlags> _flags = new Dictionary<int, InputLockFlags>(8);
        private readonly Dictionary<int, string> _reasons = new Dictionary<int, string>(8);
        private int _nextHandle = 1;
        private InputLockFlags _union;

        /// <summary>Union of every live lock.</summary>
        public InputLockFlags Union => _union;

        /// <summary>Number of live handles.</summary>
        public int Count => _flags.Count;

        /// <summary>Adds a lock and returns its handle (a lock with <see cref="InputLockFlags.None"/> is valid and locks nothing).</summary>
        public int Lock(InputLockFlags flags, string reason)
        {
            var h = _nextHandle++;
            _flags[h] = flags;
            _reasons[h] = reason ?? string.Empty;
            _union |= flags;
            return h;
        }

        /// <summary>Releases a handle. Returns false for unknown handles.</summary>
        public bool Unlock(int handle)
        {
            if (!_flags.Remove(handle)) return false;
            _reasons.Remove(handle);
            Recompute();
            return true;
        }

        /// <summary>True if any of <paramref name="flags"/> is locked.</summary>
        public bool IsLocked(InputLockFlags flags) => (_union & flags) != 0;

        /// <summary>Releases every handle.</summary>
        public void Clear()
        {
            _flags.Clear();
            _reasons.Clear();
            _union = InputLockFlags.None;
        }

        /// <summary>Appends one line per live lock (diagnostics; allocates).</summary>
        public void Describe(StringBuilder sb)
        {
            foreach (var kv in _flags)
                sb.Append('#').Append(kv.Key).Append(' ').Append(kv.Value).Append(' ').Append(_reasons[kv.Key]).Append('\n');
        }

        private void Recompute()
        {
            var u = InputLockFlags.None;
            foreach (var kv in _flags) u |= kv.Value;
            _union = u;
        }
    }
}
