using System.Collections.Generic;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Round-robin perception scheduler (TECH_PLAN §7.6). Each frame it walks the agents starting where the previous
    /// frame stopped; a due agent runs only if its rays fit in the frame's <see cref="LosBudget"/>. The first agent that
    /// does not fit stops the walk and is first in line next frame, so no agent starves and the budget is never exceeded.
    /// Allocation-free after warm-up; agents may be removed while the scheduler runs.
    /// </summary>
    public sealed class PerceptionScheduler
    {
        private readonly List<IPerceptionAgent> _agents = new List<IPerceptionAgent>();
        private int _cursor;
        private bool _dirty;

        /// <summary>Registered agents (including removed slots not compacted yet).</summary>
        public int Count => _agents.Count;

        /// <summary>Agents serviced in the last <see cref="Run"/>.</summary>
        public int ServicedLastRun { get; private set; }

        /// <summary>Adds an agent (idempotent).</summary>
        public void Add(IPerceptionAgent agent)
        {
            if (agent == null || _agents.Contains(agent)) return;
            _agents.Add(agent);
        }

        /// <summary>Removes an agent (safe during <see cref="Run"/>).</summary>
        public void Remove(IPerceptionAgent agent)
        {
            var i = _agents.IndexOf(agent);
            if (i < 0) return;
            _agents[i] = null;
            _dirty = true;
        }

        /// <summary>Removes every agent.</summary>
        public void Clear()
        {
            _agents.Clear();
            _cursor = 0;
            _dirty = false;
        }

        /// <summary>Services due agents within the budget of frame <paramref name="frame"/>. Returns rays used.</summary>
        public int Run(LosBudget budget, int frame, float now)
        {
            Compact();
            ServicedLastRun = 0;
            var n = _agents.Count;
            if (n == 0) return 0;
            if (_cursor >= n) _cursor = 0;
            var before = budget.Remaining(frame);
            for (var k = 0; k < n; k++)
            {
                var index = (_cursor + k) % n;
                var a = _agents[index];
                if (a == null || !a.PerceptionDue(now)) continue;
                var cost = a.PreparePerception(now);
                if (cost > budget.PerFrame) cost = budget.PerFrame;
                if (!budget.TryUse(frame, cost))
                {
                    _cursor = index; // first in line next frame
                    return before - budget.Remaining(frame);
                }
                a.RunPerception(now);
                ServicedLastRun++;
            }
            _cursor = (_cursor + 1) % n;
            return before - budget.Remaining(frame);
        }

        private void Compact()
        {
            if (!_dirty) return;
            _dirty = false;
            var write = 0;
            for (var read = 0; read < _agents.Count; read++)
            {
                if (_agents[read] == null)
                {
                    if (read < _cursor) _cursor--;
                    continue;
                }
                _agents[write++] = _agents[read];
            }
            _agents.RemoveRange(write, _agents.Count - write);
            if (_cursor < 0) _cursor = 0;
        }
    }
}
