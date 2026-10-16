namespace LongShadow.Core
{
    /// <summary>HUD objective marker style (GDD §11).</summary>
    public enum MarkerKind
    {
        /// <summary>Filled diamond (default objective).</summary>
        Diamond = 0,
        /// <summary>Hollow diamond (optional / secondary).</summary>
        DiamondHollow = 1,
        /// <summary>Red marker that shows through walls (technical, RPG gunner, racks).</summary>
        HighPriority = 2,
        /// <summary>Target cross (destroy / neutralise).</summary>
        TargetX = 3,
        /// <summary>FOLLOW chevron over a squadmate.</summary>
        Follow = 4,
        /// <summary>Intel pickup icon (optional intel shows within 6 m only).</summary>
        Intel = 5,
        /// <summary>World-space ring (drill %, LZ metres).</summary>
        Ring = 6,
    }
}
