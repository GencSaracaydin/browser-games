namespace LongShadow.AI.Squad
{
    /// <summary>What the squad is doing (GDD §15).</summary>
    public enum SquadMode
    {
        /// <summary>Follow mode along the critical path.</summary>
        Follow,
        /// <summary>Holding or moving to an authored node set.</summary>
        NodeSet,
    }
}
