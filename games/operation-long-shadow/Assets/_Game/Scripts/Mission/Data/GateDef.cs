namespace LongShadow.Mission.Data
{
    /// <summary>
    /// One progression gate, backtrack blocker or gate-armed element (GDD §6.7, §7.13; Appendix A "K"). Open means
    /// passable / armed / available / damageable. <see cref="OpensInBeat"/> and <see cref="ClosesInBeat"/> give the state
    /// at any beat start for cold jumps; the beat scripts apply the "Opens when" rule during play.
    /// </summary>
    public sealed class GateDef
    {
        /// <summary>Marker id (GateMarker, TriggerVolume, InteractionPoint) or a group id for destructibles.</summary>
        public readonly string Id;
        /// <summary>Kind.</summary>
        public readonly GateKind Kind;
        /// <summary>What it blocks (GDD §7.13 "Blocks").</summary>
        public readonly string Blocks;
        /// <summary>When it opens or closes (GDD §7.13 "Opens when", §6.7).</summary>
        public readonly string Rule;
        /// <summary>Beat during which it opens (0 = open from the start).</summary>
        public readonly int OpensInBeat;
        /// <summary>Beat at whose start it closes again (0 = never).</summary>
        public readonly int ClosesInBeat;
        /// <summary>Destructible ids for <see cref="GateKind.Invulnerable"/> entries.</summary>
        public readonly string[] Members;

        /// <summary>Creates an entry.</summary>
        public GateDef(string id, GateKind kind, string blocks, string rule, int opensInBeat, int closesInBeat, string[] members = null)
        {
            Id = id;
            Kind = kind;
            Blocks = blocks;
            Rule = rule;
            OpensInBeat = opensInBeat;
            ClosesInBeat = closesInBeat;
            Members = members ?? new string[0];
        }

        /// <summary>
        /// State at the start of <paramref name="beat"/> in a normal run: open once its opening beat is over (or from the start),
        /// closed again from <see cref="ClosesInBeat"/>.
        /// </summary>
        public bool OpenAtBeatStart(int beat)
        {
            if (ClosesInBeat > 0 && beat >= ClosesInBeat) return false;
            return OpensInBeat <= 0 || beat > OpensInBeat;
        }
    }
}
