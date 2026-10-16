using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// An AI shot resolved by <see cref="ICombatService.FireAI"/>. The AI (WP04) computes <see cref="HitChance"/>
    /// (GDD §14.3 formula); Combat (WP03) rolls it, applies damage or places a near miss 0.5-3 m off the player's head.
    /// </summary>
    public struct AIShotRequest
    {
        /// <summary>The shooting actor.</summary>
        public ITargetable Shooter;
        /// <summary>Muzzle position.</summary>
        public Vector3 Muzzle;
        /// <summary>Intended target.</summary>
        public IDamageable Target;
        /// <summary>Probability 0..1 that the shot hits.</summary>
        public float HitChance;
        /// <summary>Damage on hit (already scaled by difficulty for shots at the player).</summary>
        public float Damage;
        /// <summary>WeaponTable id (tracer colour, sound).</summary>
        public string WeaponId;
        /// <summary>Shooter team.</summary>
        public Team Team;
        /// <summary>Tracer every N rounds (MG 3, rifles 5; 0 = none).</summary>
        public int TracerEvery;
        /// <summary>Squad kill-share rule: the shot only suppresses (never damages).</summary>
        public bool SuppressOnly;
    }
}
