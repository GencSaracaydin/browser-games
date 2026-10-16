namespace LongShadow.Core
{
    /// <summary>A bone collider that forwards hits to its owner. Implemented by <see cref="Hitbox"/>.</summary>
    public interface IHitbox
    {
        /// <summary>The damageable this hitbox belongs to.</summary>
        IDamageable Owner { get; }
        /// <summary>Body zone.</summary>
        HitZone Zone { get; }
        /// <summary>Damage multiplier for this zone (GDD §13.1); weapons may override it.</summary>
        float Multiplier { get; }
        /// <summary>Surface for impact effects.</summary>
        SurfaceType Surface { get; }
    }
}
