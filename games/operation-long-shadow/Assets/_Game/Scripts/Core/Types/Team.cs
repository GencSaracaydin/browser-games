namespace LongShadow.Core
{
    /// <summary>Faction of an actor. Player and Squad are friendly; Legion and Greycoat are hostile (see <see cref="Teams"/>).</summary>
    public enum Team
    {
        /// <summary>Neutral or unknown (props, civilians that are never on screen).</summary>
        None = 0,
        /// <summary>The player (Calder).</summary>
        Player = 1,
        /// <summary>Hollis, Sparrow, Tinker (and Magpie).</summary>
        Squad = 2,
        /// <summary>Militia regulars (GDD §3).</summary>
        Legion = 3,
        /// <summary>Foreign advisers (GDD §3).</summary>
        Greycoat = 4,
    }
}
