using LongShadow.Core;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// One row of GDD §14.1 (Regular) plus the runtime numbers the GDD leaves to the AI (pauses, movement speeds,
    /// magazine sizes); every number cites its source in <see cref="ArchetypeTable"/>. Code data (TECH_PLAN D12).
    /// </summary>
    public sealed class Archetype
    {
        /// <summary>Id.</summary>
        public ArchetypeId Id { get; internal set; }
        /// <summary>Display name.</summary>
        public string Name { get; internal set; }
        /// <summary>Faction.</summary>
        public Team Team { get; internal set; }
        /// <summary>Number in the mission (GDD §14.1 Count).</summary>
        public int MissionCount { get; internal set; }
        /// <summary>Hit points.</summary>
        public float Hp { get; internal set; }
        /// <summary>Torso damage factor (Greycoat vest 0.8).</summary>
        public float TorsoArmour { get; internal set; } = 1f;
        /// <summary>Weapon id (<see cref="WeaponIds"/>).</summary>
        public string WeaponId { get; internal set; }
        /// <summary>Damage per hit to the player at Regular (scaled by the difficulty multiplier).</summary>
        public float DamageToPlayer { get; internal set; }
        /// <summary>Rounds per burst, minimum.</summary>
        public int BurstMin { get; internal set; }
        /// <summary>Rounds per burst, maximum.</summary>
        public int BurstMax { get; internal set; }
        /// <summary>Pause between bursts, minimum (s).</summary>
        public float PauseMin { get; internal set; }
        /// <summary>Pause between bursts, maximum (s).</summary>
        public float PauseMax { get; internal set; }
        /// <summary>Rounds per minute inside a burst.</summary>
        public float Rpm { get; internal set; }
        /// <summary>Hit chance at 20 m before modifiers (GDD §14.1).</summary>
        public float Accuracy20 { get; internal set; }
        /// <summary>Preferred engagement range, near end (m).</summary>
        public float RangeMin { get; internal set; }
        /// <summary>Preferred engagement range, far end (m).</summary>
        public float RangeMax { get; internal set; }
        /// <summary>Cover re-evaluation interval, minimum (s).</summary>
        public float CoverReevalMin { get; internal set; }
        /// <summary>Cover re-evaluation interval, maximum (s).</summary>
        public float CoverReevalMax { get; internal set; }
        /// <summary>Grenade rule.</summary>
        public GrenadeRule Grenades { get; internal set; }
        /// <summary>Chance to flee a frag landing within 5 m (Legion 0.6, Greycoat 1.0, GDD §14.3).</summary>
        public float FleeGrenadeChance { get; internal set; }
        /// <summary>Moves on the NavMesh (false = perch/static).</summary>
        public bool UsesNavMesh { get; internal set; }
        /// <summary>Wants full (standing) cover (Greycoat) rather than half cover (Legion).</summary>
        public bool PrefersFullCover { get; internal set; }
        /// <summary>Takes part in the 60 % morale break (GDD §14.1).</summary>
        public bool MoraleBreaks { get; internal set; }
        /// <summary>Uses squelch clicks (Greycoat, GDD §14.1).</summary>
        public bool Squelch { get; internal set; }
        /// <summary>Tracer every N rounds (MG 3, rifles 5, GDD §13.4).</summary>
        public int TracerEvery { get; internal set; }
        /// <summary>Fixed sight range (marksman 160 m); 0 = LightStage range.</summary>
        public float FixedSightRange { get; internal set; }
        /// <summary>Always aware of its target, no perception (rooftop targets, GDD §14.2).</summary>
        public bool AlwaysAware { get; internal set; }
        /// <summary>Accuracy depends on target motion (marksman table) instead of the motion factor.</summary>
        public bool MotionAccuracyTable { get; internal set; }
        /// <summary>Magazine size (0 = no reloads).</summary>
        public int Magazine { get; internal set; }
        /// <summary>Reload time (s).</summary>
        public float ReloadSeconds { get; internal set; }
        /// <summary>Walk speed (m/s).</summary>
        public float WalkSpeed { get; internal set; }
        /// <summary>Run speed between covers (m/s).</summary>
        public float RunSpeed { get; internal set; }
        /// <summary>Sprint speed (grenade flight, flanks) (m/s).</summary>
        public float SprintSpeed { get; internal set; }
        /// <summary>Drops its weapon as a pickup on death (Legion riflemen drop AKs, GDD §13.1).</summary>
        public bool DropsWeapon { get; internal set; }
        /// <summary>Uses Legion barks (else Greycoat barks).</summary>
        public bool LegionBarks => Team == Team.Legion;
        /// <summary>Seconds between shots inside a burst.</summary>
        public float ShotInterval => Rpm > 0f ? 60f / Rpm : 1f;
    }
}
