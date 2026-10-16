using System.Collections.Generic;

namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// A wave start (or alert) rule: an OR of AND-groups of <see cref="WaveCondition"/>s, e.g. GDD §7.7 stair wave =
    /// (breach wave dead AND player in B7.T.OfficeDoor) OR 25 s after the breach. Evaluated by the encounter runtime
    /// at a few Hz; mission scripts can always start a wave directly with <c>IEncounterService.TriggerWave</c>.
    /// </summary>
    public sealed class WaveTrigger
    {
        private readonly List<WaveCondition[]> _anyOf = new List<WaveCondition[]>();

        /// <summary>The OR list of AND groups.</summary>
        public IReadOnlyList<WaveCondition[]> AnyOf => _anyOf;

        /// <summary>Starts with the encounter.</summary>
        public static WaveTrigger OnActivate() => new WaveTrigger().Or(WaveCondition.Activated());

        /// <summary>Only <c>TriggerWave</c> starts it.</summary>
        public static WaveTrigger Manual() => new WaveTrigger().Or(WaveCondition.Manual());

        /// <summary>Any one of several single conditions.</summary>
        public static WaveTrigger Any(params WaveCondition[] conditions)
        {
            var t = new WaveTrigger();
            foreach (var c in conditions) t.Or(c);
            return t;
        }

        /// <summary>Adds an AND group (all of <paramref name="allOf"/>) as one OR alternative.</summary>
        public WaveTrigger Or(params WaveCondition[] allOf)
        {
            if (allOf != null && allOf.Length > 0) _anyOf.Add(allOf);
            return this;
        }

        /// <summary>True when the trigger can only be fired manually.</summary>
        public bool IsManualOnly
        {
            get
            {
                for (var i = 0; i < _anyOf.Count; i++)
                    foreach (var c in _anyOf[i])
                        if (c.Kind != ConditionKind.Manual) return false;
                return true;
            }
        }

        /// <summary>Evaluates the rule.</summary>
        public bool Evaluate(IConditionContext ctx)
        {
            for (var g = 0; g < _anyOf.Count; g++)
            {
                var group = _anyOf[g];
                var all = true;
                for (var i = 0; i < group.Length && all; i++) all = Holds(group[i], ctx);
                if (all) return true;
            }
            return false;
        }

        /// <summary>Evaluates one condition.</summary>
        public static bool Holds(in WaveCondition c, IConditionContext ctx)
        {
            switch (c.Kind)
            {
                case ConditionKind.Activated: return true;
                case ConditionKind.Event: return ctx.EventTime(c.Id, out _);
                case ConditionKind.SecondsAfterEvent: return ctx.EventTime(c.Id, out var t) && ctx.Now - t >= c.Value;
                case ConditionKind.PlayerInside: return ctx.PlayerInside(c.Id);
                case ConditionKind.SecondsAfterActivate: return ctx.Now - ctx.ActivatedAt >= c.Value;
                case ConditionKind.WaveAliveAtMost: return ctx.WaveAllSpawned(c.Id) && ctx.WaveAlive(c.Id) <= c.Count;
                case ConditionKind.WaveDead: return ctx.WaveDead(c.Id);
                case ConditionKind.KilledAtLeast: return ctx.Killed >= c.Count;
                case ConditionKind.EnemyDead: return ctx.EnemyDeathTime(c.Id, out var d) && ctx.Now - d >= c.Value;
                default: return false;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (var g = 0; g < _anyOf.Count; g++)
            {
                if (g > 0) sb.Append(" OR ");
                sb.Append('(');
                for (var i = 0; i < _anyOf[g].Length; i++)
                {
                    if (i > 0) sb.Append(" AND ");
                    sb.Append(_anyOf[g][i]);
                }
                sb.Append(')');
            }
            return sb.ToString();
        }
    }
}
