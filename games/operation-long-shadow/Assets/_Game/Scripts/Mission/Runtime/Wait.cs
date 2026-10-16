using System;
using System.Collections.Generic;
using System.Text;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// A yieldable condition for <see cref="ScriptRunner"/> coroutines. Beat scripts get waits from
    /// <see cref="MissionContext"/> helpers (<c>ctx.Trigger(id)</c>, <c>ctx.Cleared(enc)</c>, ...). <see cref="Kind"/>,
    /// <see cref="Id"/> and <see cref="Radius"/> describe what the wait needs, so tools and the EditMode autopilot can see
    /// what a beat is waiting for.
    /// </summary>
    public sealed class Wait
    {
        private readonly Func<bool> _done;
        private readonly Wait[] _children;

        /// <summary>What the wait waits for.</summary>
        public readonly WaitKind Kind;
        /// <summary>Id of the thing waited for (trigger, line, encounter, ...), or null.</summary>
        public readonly string Id;
        /// <summary>Second id (wave of an encounter) or null.</summary>
        public readonly string Sub;
        /// <summary>Radius in metres (PlayerNear) or a count (Encounter "at most N alive").</summary>
        public readonly float Radius;

        /// <summary>Creates a wait.</summary>
        public Wait(WaitKind kind, string id, Func<bool> done, string sub = null, float radius = 0f, Wait[] children = null)
        {
            Kind = kind;
            Id = id;
            Sub = sub;
            Radius = radius;
            _done = done;
            _children = children;
        }

        /// <summary>Child waits of <see cref="WaitKind.Any"/>/<see cref="WaitKind.All"/>.</summary>
        public IReadOnlyList<Wait> Children => _children ?? (IReadOnlyList<Wait>)Array.Empty<Wait>();

        /// <summary>True when satisfied (evaluated every tick while the script waits).</summary>
        public bool IsDone
        {
            get
            {
                if (_done != null) return _done();
                if (_children == null || _children.Length == 0) return true;
                if (Kind == WaitKind.All)
                {
                    for (var i = 0; i < _children.Length; i++)
                        if (!_children[i].IsDone) return false;
                    return true;
                }
                for (var i = 0; i < _children.Length; i++)
                    if (_children[i].IsDone) return true;
                return false;
            }
        }

        /// <summary>Any of the waits (null entries are ignored).</summary>
        public static Wait Any(params Wait[] waits) => new Wait(WaitKind.Any, null, null, children: Compact(waits));

        /// <summary>All of the waits (null entries are ignored).</summary>
        public static Wait All(params Wait[] waits) => new Wait(WaitKind.All, null, null, children: Compact(waits));

        /// <summary>An arbitrary condition with a description.</summary>
        public static Wait Until(Func<bool> condition, string detail) => new Wait(WaitKind.Until, detail, condition);

        /// <summary>Fills <paramref name="results"/> with this wait's leaves (the autopilot satisfies leaves).</summary>
        public void CollectLeaves(List<Wait> results)
        {
            if (_children == null || _children.Length == 0)
            {
                results.Add(this);
                return;
            }
            for (var i = 0; i < _children.Length; i++) _children[i].CollectLeaves(results);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (_children == null || _children.Length == 0)
                return Kind + (Id != null ? " " + Id : "") + (Sub != null ? "/" + Sub : "") + (Radius > 0f ? " (" + Radius + ")" : "");
            var sb = new StringBuilder(Kind.ToString()).Append('(');
            for (var i = 0; i < _children.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(_children[i]);
            }
            return sb.Append(')').ToString();
        }

        private static Wait[] Compact(Wait[] waits)
        {
            if (waits == null) return Array.Empty<Wait>();
            var n = 0;
            for (var i = 0; i < waits.Length; i++)
                if (waits[i] != null) n++;
            if (n == waits.Length) return waits;
            var r = new Wait[n];
            n = 0;
            for (var i = 0; i < waits.Length; i++)
                if (waits[i] != null) r[n++] = waits[i];
            return r;
        }
    }
}
