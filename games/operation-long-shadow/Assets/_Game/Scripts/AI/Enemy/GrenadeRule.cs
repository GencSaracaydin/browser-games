namespace LongShadow.AI.Enemy
{
    /// <summary>Grenade use of an archetype (GDD §14.1 "Grenades").</summary>
    public enum GrenadeRule
    {
        /// <summary>Never throws.</summary>
        None,
        /// <summary>Shared Legion pool: 1 throw per 60 s mission-wide (difficulty interval, GDD §16).</summary>
        LegionSharedPool,
        /// <summary>One each, thrown after the player stays in the same cover for 6 s (difficulty value, GDD §16).</summary>
        GreycoatOneEach,
    }
}
