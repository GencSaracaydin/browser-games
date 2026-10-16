using System;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// An AI weapon as Combat sees it (GDD §13.4, §14.1): sound, tracers, noise, and the reference damage/cadence rows the
    /// AI package mirrors in its ArchetypeTable (WP04 owns hit chance and firing logic; it passes the final damage in
    /// <c>AIShotRequest</c>). Treat as immutable.
    /// </summary>
    [Serializable]
    public sealed class EnemyWeaponSpec
    {
        /// <summary>Weapon id (<see cref="LongShadow.Core.WeaponIds"/>).</summary>
        public string Id;
        /// <summary>Handling class (muzzle flash size, casing).</summary>
        public WeaponKind Kind;
        /// <summary>Damage per hit to the player at Regular (GDD §14.1).</summary>
        public float DamageToPlayer;
        /// <summary>Rounds per minute while bursting.</summary>
        public float Rpm;
        /// <summary>Shortest burst.</summary>
        public int BurstMin;
        /// <summary>Longest burst.</summary>
        public int BurstMax;
        /// <summary>Shortest pause between bursts (seconds).</summary>
        public float PauseMin;
        /// <summary>Longest pause between bursts (seconds).</summary>
        public float PauseMax;
        /// <summary>Tracer every N rounds when the request does not say (0 = never).</summary>
        public int TracerEvery;
        /// <summary>AI hearing radius of a shot.</summary>
        public float NoiseRadius = 60f;
        /// <summary>Muzzle flash scale.</summary>
        public float MuzzleFlashScale = 1f;
        /// <summary>CombatFx casing id (null = none).</summary>
        public string CasingId;
    }
}
