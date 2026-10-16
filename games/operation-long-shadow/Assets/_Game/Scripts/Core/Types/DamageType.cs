namespace LongShadow.Core
{
    /// <summary>What caused a <see cref="DamageInfo"/>.</summary>
    public enum DamageType
    {
        /// <summary>Hitscan or AI bullet.</summary>
        Bullet = 0,
        /// <summary>Grenade, rocket, satchel, vehicle fireball.</summary>
        Explosion = 1,
        /// <summary>Knife (GDD §13.2).</summary>
        Melee = 2,
        /// <summary>Fall damage above 4.5 m (GDD §12).</summary>
        Fall = 3,
        /// <summary>Burning technical, fire props.</summary>
        Fire = 4,
        /// <summary>Scripted kills and debug commands.</summary>
        Script = 5,
    }
}
