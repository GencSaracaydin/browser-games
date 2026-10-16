using System.Collections.Generic;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Attack tokens (GDD §14.3, §16): at most <see cref="Capacity"/> enemies fire at the player at once (Recruit 2,
    /// Regular 3, Hardened 4, Veteran 5). Tokens rotate every 2-4 s when others are waiting, and the enemy under the
    /// player's crosshair gets one first (it pre-empts the longest holder). Enemies without a token shoot squadmates or
    /// reposition. Brains call <see cref="Request"/> at their decision rate while they want to fire at the player and
    /// <see cref="Release"/> when they stop; a holder that stops asking for <see cref="StaleSeconds"/> loses its token.
    /// Pure C#, allocation-free after warm-up.
    /// </summary>
    public sealed class AttackTokenManager
    {
        /// <summary>Shortest hold before a token rotates (GDD §14.3 "every 2-4 s").</summary>
        public const float MinHoldSeconds = 2f;
        /// <summary>Longest hold before a token rotates.</summary>
        public const float MaxHoldSeconds = 4f;
        /// <summary>A holder or waiter that has not asked for this long is dropped.</summary>
        public const float StaleSeconds = 0.6f;

        private struct Holder
        {
            public int Agent;
            public float GrantedAt;
            public float Until;
            public float LastAsk;
        }

        private struct Waiter
        {
            public int Agent;
            public float Since;
            public float LastAsk;
            public bool AimedAt;
        }

        private readonly List<Holder> _holders = new List<Holder>(8);
        private readonly List<Waiter> _waiters = new List<Waiter>(16);
        private AIRandom _rng;
        private int _capacity;

        /// <summary>Creates a manager with a capacity (GDD §16 attack tokens).</summary>
        public AttackTokenManager(int capacity = 3, uint seed = 12345u)
        {
            _capacity = capacity < 0 ? 0 : capacity;
            _rng = new AIRandom(seed);
        }

        /// <summary>Tokens available (difficulty). Lowering it releases the longest holders at once.</summary>
        public int Capacity
        {
            get => _capacity;
            set
            {
                _capacity = value < 0 ? 0 : value;
                while (_holders.Count > _capacity) _holders.RemoveAt(OldestHolderIndex(-1));
            }
        }

        /// <summary>Current number of token holders (never above <see cref="Capacity"/>).</summary>
        public int HolderCount => _holders.Count;

        /// <summary>Agents waiting for a token.</summary>
        public int WaiterCount => _waiters.Count;

        /// <summary>True if the agent holds a token.</summary>
        public bool Has(int agent) => IndexOfHolder(agent) >= 0;

        /// <summary>
        /// The agent wants to fire at the player. Returns true while it holds a token. <paramref name="aimedAt"/> is true
        /// when the player is aiming at this agent (it gets a token first).
        /// </summary>
        public bool Request(int agent, float now, bool aimedAt)
        {
            Prune(now);
            var h = IndexOfHolder(agent);
            if (h >= 0)
            {
                var holder = _holders[h];
                holder.LastAsk = now;
                if (now >= holder.Until)
                {
                    if (HasOtherWaiters(agent))
                    {
                        // Rotate: give the slot up and queue behind the others.
                        _holders.RemoveAt(h);
                        AddOrRefreshWaiter(agent, now, aimedAt);
                        GrantFree(now);
                        return Has(agent);
                    }
                    holder.Until = now + _rng.Range(MinHoldSeconds, MaxHoldSeconds);
                }
                _holders[h] = holder;
                return true;
            }

            AddOrRefreshWaiter(agent, now, aimedAt);
            if (aimedAt && _holders.Count >= _capacity && _capacity > 0)
            {
                // The enemy under the crosshair pre-empts the longest holder.
                var victim = OldestHolderIndex(agent);
                if (victim >= 0) _holders.RemoveAt(victim);
            }
            GrantFree(now);
            return Has(agent);
        }

        /// <summary>The agent no longer wants to fire at the player (lost sight, retargeted, dead).</summary>
        public void Release(int agent)
        {
            var h = IndexOfHolder(agent);
            if (h >= 0) _holders.RemoveAt(h);
            var w = IndexOfWaiter(agent);
            if (w >= 0) _waiters.RemoveAt(w);
        }

        /// <summary>Drops every holder and waiter (checkpoint reload, encounter reset).</summary>
        public void Clear()
        {
            _holders.Clear();
            _waiters.Clear();
        }

        /// <summary>Drops stale holders/waiters and hands free tokens to waiters (call once per frame or less).</summary>
        public void Tick(float now)
        {
            Prune(now);
            GrantFree(now);
        }

        private void GrantFree(float now)
        {
            while (_holders.Count < _capacity && _waiters.Count > 0)
            {
                var best = -1;
                for (var i = 0; i < _waiters.Count; i++)
                {
                    if (best < 0) { best = i; continue; }
                    var a = _waiters[i];
                    var b = _waiters[best];
                    // Aimed-at first, then the longest wait.
                    if (a.AimedAt && !b.AimedAt) best = i;
                    else if (a.AimedAt == b.AimedAt && a.Since < b.Since) best = i;
                }
                var w = _waiters[best];
                _waiters.RemoveAt(best);
                _holders.Add(new Holder
                {
                    Agent = w.Agent,
                    GrantedAt = now,
                    Until = now + _rng.Range(MinHoldSeconds, MaxHoldSeconds),
                    LastAsk = w.LastAsk,
                });
            }
        }

        private void Prune(float now)
        {
            for (var i = _holders.Count - 1; i >= 0; i--)
                if (now - _holders[i].LastAsk > StaleSeconds) _holders.RemoveAt(i);
            for (var i = _waiters.Count - 1; i >= 0; i--)
                if (now - _waiters[i].LastAsk > StaleSeconds) _waiters.RemoveAt(i);
        }

        private void AddOrRefreshWaiter(int agent, float now, bool aimedAt)
        {
            var w = IndexOfWaiter(agent);
            if (w >= 0)
            {
                var x = _waiters[w];
                x.LastAsk = now;
                x.AimedAt = aimedAt;
                _waiters[w] = x;
                return;
            }
            _waiters.Add(new Waiter { Agent = agent, Since = now, LastAsk = now, AimedAt = aimedAt });
        }

        private bool HasOtherWaiters(int agent)
        {
            for (var i = 0; i < _waiters.Count; i++)
                if (_waiters[i].Agent != agent) return true;
            return false;
        }

        private int OldestHolderIndex(int exceptAgent)
        {
            var best = -1;
            for (var i = 0; i < _holders.Count; i++)
            {
                if (_holders[i].Agent == exceptAgent) continue;
                if (best < 0 || _holders[i].GrantedAt < _holders[best].GrantedAt) best = i;
            }
            return best;
        }

        private int IndexOfHolder(int agent)
        {
            for (var i = 0; i < _holders.Count; i++)
                if (_holders[i].Agent == agent) return i;
            return -1;
        }

        private int IndexOfWaiter(int agent)
        {
            for (var i = 0; i < _waiters.Count; i++)
                if (_waiters[i].Agent == agent) return i;
            return -1;
        }
    }
}
