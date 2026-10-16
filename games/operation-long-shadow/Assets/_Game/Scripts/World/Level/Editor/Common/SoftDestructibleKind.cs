namespace LongShadow.World.Level.Editor.Common
{
    /// <summary>Mirror of WP03's <c>LongShadow.Combat.Targets.DestructibleKind</c> values (set by serialized int).</summary>
    public enum SoftDestructibleKind
    {
        /// <summary>Spark burst.</summary>
        Generic = 0,
        /// <summary>Floodlight head (buzzing die-off).</summary>
        Flood = 1,
        /// <summary>Transmitter rack (60 HP).</summary>
        Rack = 2,
        /// <summary>Vehicle (20 hits, fireball).</summary>
        Vehicle = 3,
    }
}
