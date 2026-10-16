using System;

namespace LongShadow.Combat.Data
{
    /// <summary>RPG-7 pattern rocket (GDD §14.1 "90 direct; 4 m splash with falloff"). Immutable.</summary>
    [Serializable]
    public sealed class RocketSpec
    {
        /// <summary>Weapon id.</summary>
        public string Id;
        /// <summary>Damage to a damageable hit directly.</summary>
        public float DirectDamage;
        /// <summary>Splash damage at the centre.</summary>
        public float SplashDamage;
        /// <summary>Full splash damage inside this radius.</summary>
        public float LethalRadius;
        /// <summary>Splash falls to zero at this radius.</summary>
        public float SplashRadius;
        /// <summary>Visual flight speed (tuning: slower than real so the streak reads on screen).</summary>
        public float Speed;
        /// <summary>Self-destruct distance when nothing is hit (airburst).</summary>
        public float MaxDistance;
        /// <summary>AI hearing radius of the launch and the blast.</summary>
        public float NoiseRadius;
        /// <summary>Lateral wobble amplitude in metres (tuning).</summary>
        public float Wobble = 0.35f;
    }
}
