using System;
using System.Collections.Generic;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// The played-line set of the checkpoint snapshot (GDD §17, TECH_PLAN §5.5): lines played before the checkpoint
    /// was saved do not play again after a reload ("spent"), lines after it play again as the beat re-runs, and
    /// objective-critical lines (D014, D029, D034, D048, D053, D059, D065, D067) and systemic repeatable lines
    /// (callouts, barks, death inserts, the OOB warning, nags) always play. Pure (unit-tested).
    /// </summary>
    public sealed class PlayedLineSet
    {
        private readonly HashSet<string> _played = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _spent = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Lines played since the start or the last restore (including the restored ones).</summary>
        public int Count => _played.Count;

        /// <summary>Records that a line started.</summary>
        public void MarkPlayed(string lineId)
        {
            if (!string.IsNullOrEmpty(lineId)) _played.Add(lineId);
        }

        /// <summary>True if the line played (and was not reset by a checkpoint reload).</summary>
        public bool HasPlayed(string lineId) => lineId != null && _played.Contains(lineId);

        /// <summary>True when a request for <paramref name="line"/> must be skipped after a checkpoint reload.</summary>
        public bool ShouldSkip(DialogueLine line)
        {
            if (line == null) return false;
            if (line.ObjectiveCritical || line.Repeatable) return false;
            return _spent.Contains(line.Id);
        }

        /// <summary>Copies the played set (snapshot capture) into <paramref name="into"/> in a stable order.</summary>
        public void CopyTo(List<string> into)
        {
            into.Clear();
            into.AddRange(_played);
            into.Sort(StringComparer.Ordinal);
        }

        /// <summary>Replaces the state with a snapshot: those lines are played and spent.</summary>
        public void Restore(IEnumerable<string> lineIds)
        {
            _played.Clear();
            _spent.Clear();
            if (lineIds == null) return;
            foreach (var id in lineIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                _played.Add(id);
                _spent.Add(id);
            }
        }

        /// <summary>Forgets everything (new mission).</summary>
        public void Clear()
        {
            _played.Clear();
            _spent.Clear();
        }
    }
}
