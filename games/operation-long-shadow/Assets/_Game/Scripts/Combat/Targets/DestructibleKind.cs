namespace LongShadow.Combat.Targets
{
    /// <summary>Presentation preset of a <see cref="Destructible"/> (effects and sounds of its death).</summary>
    public enum DestructibleKind
    {
        /// <summary>Generic target: spark burst.</summary>
        Generic = 0,
        /// <summary>Floodlight head (GDD §6.6 BLD-13, §7.6 F1-F4): spark burst, buzzing die-off flicker, light off.</summary>
        Flood = 1,
        /// <summary>Transmitter rack (GDD §7.9 R1-R4, 60 HP): shatters in sparks.</summary>
        Rack = 2,
        /// <summary>Vehicle (GDD §7.11 trucks, 20 hits): simple fireball, then burns.</summary>
        Vehicle = 3,
    }
}
