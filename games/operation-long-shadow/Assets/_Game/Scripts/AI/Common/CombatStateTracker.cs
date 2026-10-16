using System;
using System.Collections.Generic;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Counts enemies in combat and reports the edges for <c>GameEvents.CombatStateChanged</c> (Core): true when the
    /// first enemy becomes aware of the player or squad, false when none is left in combat (dead, despawned or back to
    /// idle). Pure C#; <see cref="AIDirector"/> raises the Core event from <see cref="Changed"/>.
    /// </summary>
    public sealed class CombatStateTracker
    {
        private readonly HashSet<int> _inCombat = new HashSet<int>();

        /// <summary>Raised on the edges (true = first in, false = last out).</summary>
        public event Action<bool> Changed;

        /// <summary>True while at least one enemy is in combat.</summary>
        public bool InCombat => _inCombat.Count > 0;

        /// <summary>Enemies in combat.</summary>
        public int Count => _inCombat.Count;

        /// <summary>Sets an agent's combat flag.</summary>
        public void Set(int agent, bool inCombat)
        {
            var before = _inCombat.Count > 0;
            if (inCombat) _inCombat.Add(agent);
            else _inCombat.Remove(agent);
            var after = _inCombat.Count > 0;
            if (before != after) Changed?.Invoke(after);
        }

        /// <summary>Clears every flag (reports false if anyone was in combat).</summary>
        public void Clear()
        {
            var before = _inCombat.Count > 0;
            _inCombat.Clear();
            if (before) Changed?.Invoke(false);
        }
    }
}
