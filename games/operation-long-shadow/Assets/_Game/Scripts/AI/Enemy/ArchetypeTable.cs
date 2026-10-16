using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// GDD §14.1 archetypes (Regular) as code data (TECH_PLAN D12, §7.6). Difficulty scaling (damage, accuracy, tokens,
    /// grenade intervals, marksman damage) is applied at runtime from <see cref="DifficultyProfile"/> (GDD §16). Values
    /// the GDD does not give (pauses of the gunner/technical/Greycoat, movement speeds, magazines) are WP04 decisions
    /// logged in Docs/DECISIONS.md and marked "WP04" below.
    /// </summary>
    public static class ArchetypeTable
    {
        private static readonly Archetype[] s_all =
        {
            new Archetype
            {
                Id = ArchetypeId.LegionRifleman, Name = "Legion Rifleman", Team = Team.Legion, MissionCount = 38,
                Hp = 100f, WeaponId = WeaponIds.AK, DamageToPlayer = 20f,                  // GDD §14.1
                BurstMin = 3, BurstMax = 6, PauseMin = 0.6f, PauseMax = 1.2f,              // GDD §14.1
                Rpm = 600f,                                                                // GDD §13.1 AK
                Accuracy20 = 0.30f, RangeMin = 10f, RangeMax = 35f,                        // GDD §14.1
                CoverReevalMin = 6f, CoverReevalMax = 9f,                                  // GDD §14.3
                Grenades = GrenadeRule.LegionSharedPool, FleeGrenadeChance = 0.6f,         // GDD §14.1, §14.3
                UsesNavMesh = true, PrefersFullCover = false, MoraleBreaks = true,         // "half cover", morale (§14.1)
                TracerEvery = 3,                                                           // CINEMATIC_DIRECTION §4: enemy tracers 1 in 3
                Magazine = 30, ReloadSeconds = 2.4f,                                       // AK 30, 2.4 s (§13.1)
                WalkSpeed = 1.6f, RunSpeed = 3.8f, SprintSpeed = 5.0f,                     // WP04
                DropsWeapon = true,                                                        // GDD §13.1 drops an AK
            },
            new Archetype
            {
                Id = ArchetypeId.LegionGunner, Name = "Legion Gunner", Team = Team.Legion, MissionCount = 3,
                Hp = 130f, WeaponId = WeaponIds.RPK, DamageToPlayer = 16f,                 // GDD §14.1
                BurstMin = 8, BurstMax = 15, PauseMin = 1.2f, PauseMax = 2.0f,             // bursts §14.1; pause WP04
                Rpm = 600f,                                                                // GDD §13.1 RPK
                Accuracy20 = 0.22f, RangeMin = 20f, RangeMax = 50f,                        // GDD §14.1
                CoverReevalMin = 0f, CoverReevalMax = 0f,                                  // perch only
                Grenades = GrenadeRule.None, FleeGrenadeChance = 0f,
                UsesNavMesh = false, MoraleBreaks = false,
                TracerEvery = 3,                                                           // GDD §13.4 MG 1 in 3
                Magazine = 75, ReloadSeconds = 4.2f,                                       // RPK drum (§13.1)
                WalkSpeed = 1.4f, RunSpeed = 3.2f, SprintSpeed = 4.2f,
                DropsWeapon = false,                                                       // per spawn (E3-05, E6-05)
            },
            new Archetype
            {
                Id = ArchetypeId.LegionRPG, Name = "Legion RPG", Team = Team.Legion, MissionCount = 1,
                Hp = 100f, WeaponId = WeaponIds.RPG7, DamageToPlayer = 90f,                // GDD §14.1 (4 m splash in WP03)
                BurstMin = 1, BurstMax = 1, PauseMin = 8f, PauseMax = 8f,                  // 8 s reload (§14.1)
                Rpm = 0f, Accuracy20 = 1f, RangeMin = 30f, RangeMax = 80f,                 // scripted targets
                Grenades = GrenadeRule.None, UsesNavMesh = false, MoraleBreaks = false,
                TracerEvery = 0, Magazine = 1, ReloadSeconds = 8f,
                WalkSpeed = 1.4f, RunSpeed = 3.2f, SprintSpeed = 4.2f,
            },
            new Archetype
            {
                Id = ArchetypeId.TechnicalGunner, Name = "Technical Gunner", Team = Team.Legion, MissionCount = 1,
                Hp = 100f, WeaponId = WeaponIds.DShK, DamageToPlayer = 35f,                // GDD §14.1
                BurstMin = 3, BurstMax = 3, PauseMin = 0.6f, PauseMax = 1.0f,              // 3-round bursts; pause WP04
                Rpm = 550f,                                                                // GDD §14.1
                Accuracy20 = 0.25f, RangeMin = 20f, RangeMax = 70f,                        // GDD §14.1
                Grenades = GrenadeRule.None, UsesNavMesh = false, MoraleBreaks = false,
                TracerEvery = 3,                                                           // GDD §13.4 MG
                Magazine = 0, ReloadSeconds = 0f,                                          // belt box: no reloads (WP04)
                WalkSpeed = 0f, RunSpeed = 0f, SprintSpeed = 0f,
            },
            new Archetype
            {
                Id = ArchetypeId.GreycoatAdvisor, Name = "Greycoat Advisor", Team = Team.Greycoat, MissionCount = 11,
                Hp = 150f, TorsoArmour = 0.8f, WeaponId = WeaponIds.AKMS, DamageToPlayer = 24f, // GDD §14.1
                BurstMin = 2, BurstMax = 3, PauseMin = 0.45f, PauseMax = 0.9f,             // bursts §14.1; pause WP04
                Rpm = 600f,                                                                // AKMS = AKM action (WP04)
                Accuracy20 = 0.45f, RangeMin = 8f, RangeMax = 30f,                         // GDD §14.1
                CoverReevalMin = 4f, CoverReevalMax = 6f,                                  // GDD §14.1, §14.3
                Grenades = GrenadeRule.GreycoatOneEach, FleeGrenadeChance = 1f,            // GDD §14.1, §14.3
                UsesNavMesh = true, PrefersFullCover = true, MoraleBreaks = false, Squelch = true,
                TracerEvery = 3, Magazine = 30, ReloadSeconds = 2.4f,                       // tracers: CINEMATIC_DIRECTION §4
                WalkSpeed = 1.7f, RunSpeed = 4.2f, SprintSpeed = 5.4f,                     // WP04
                DropsWeapon = false,                                                       // GDD §13.1 never drop
            },
            new Archetype
            {
                Id = ArchetypeId.GreycoatMarksman, Name = "Greycoat Marksman", Team = Team.Greycoat, MissionCount = 1,
                Hp = 120f, WeaponId = WeaponIds.SVD, DamageToPlayer = 70f,                 // GDD §14.1 (difficulty: §16)
                BurstMin = 1, BurstMax = 1, PauseMin = 3.5f, PauseMax = 3.5f,              // 1 shot per 3.5 s
                Rpm = 0f, Accuracy20 = 0.8f, RangeMin = 60f, RangeMax = 160f,              // §14.1
                Grenades = GrenadeRule.None, UsesNavMesh = false, Squelch = true,
                TracerEvery = 3, FixedSightRange = 160f, MotionAccuracyTable = true,       // §14.2 160 m scope
                Magazine = 10, ReloadSeconds = 2.5f,
            },
            new Archetype
            {
                Id = ArchetypeId.RooftopTarget, Name = "Rooftop Target", Team = Team.Legion, MissionCount = 4,
                Hp = 60f, WeaponId = WeaponIds.AK, DamageToPlayer = 20f,                   // §14.1 (×0.25 by the rig in B11)
                BurstMin = 3, BurstMax = 6, PauseMin = 0.6f, PauseMax = 1.2f,
                Rpm = 600f, Accuracy20 = 0.30f, RangeMin = 30f, RangeMax = 120f,
                Grenades = GrenadeRule.None, UsesNavMesh = false, AlwaysAware = true,      // §14.2
                TracerEvery = 3, Magazine = 0,
            },
        };

        /// <summary>Every archetype.</summary>
        public static IReadOnlyList<Archetype> All => s_all;

        /// <summary>The archetype for an id.</summary>
        public static Archetype Get(ArchetypeId id)
        {
            for (var i = 0; i < s_all.Length; i++)
                if (s_all[i].Id == id) return s_all[i];
            return s_all[0];
        }

        /// <summary>
        /// Damage per hit to the player for a difficulty: the archetype value × the enemy damage multiplier, except the
        /// marksman whose per-hit damage is its own GDD §16 row (45 / 70 / 90 / 100).
        /// </summary>
        public static float DamageToPlayer(Archetype a, in DifficultyProfile d)
        {
            if (a.Id == ArchetypeId.GreycoatMarksman) return d.MarksmanDamagePerHit > 0f ? d.MarksmanDamagePerHit : a.DamageToPlayer;
            return a.DamageToPlayer * (d.EnemyDamageMultiplier > 0f ? d.EnemyDamageMultiplier : 1f);
        }
    }
}
