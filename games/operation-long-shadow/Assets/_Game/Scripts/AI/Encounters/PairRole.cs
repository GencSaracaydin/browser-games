namespace LongShadow.AI.Encounters
{
    /// <summary>Greycoat pair roles (GDD §14.1: "one suppresses while the other takes the designated flank route").</summary>
    public enum PairRole
    {
        /// <summary>No pair.</summary>
        None,
        /// <summary>Takes the flank route.</summary>
        Flanker,
        /// <summary>Suppresses from a position while the partner flanks.</summary>
        Suppressor,
    }
}
