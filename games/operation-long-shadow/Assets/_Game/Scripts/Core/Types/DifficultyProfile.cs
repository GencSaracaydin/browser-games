namespace LongShadow.Core
{
    /// <summary>
    /// Every row of GDD §16 for one difficulty. Use <see cref="For"/>; values are code data (TECH_PLAN D12).
    /// The player's damage to enemies is the same on every difficulty.
    /// </summary>
    public struct DifficultyProfile
    {
        /// <summary>The level these values belong to.</summary>
        public Difficulty Level;
        /// <summary>Player hit points (100 on every level).</summary>
        public float PlayerHp;
        /// <summary>Multiplier on all enemy damage dealt to the player.</summary>
        public float EnemyDamageMultiplier;
        /// <summary>Informational: Legion AK hits to down the player (10/5/4/3).</summary>
        public int LegionAkHitsToDown;
        /// <summary>Seconds after the last damage before regeneration starts.</summary>
        public float RegenDelay;
        /// <summary>Regeneration in HP per second.</summary>
        public float RegenRate;
        /// <summary>Immunity in seconds when dropping below <see cref="LastStandThresholdHp"/> (once per regen cycle; 0 = none).</summary>
        public float LastStandImmunity;
        /// <summary>HP threshold for last-stand immunity (20 HP).</summary>
        public float LastStandThresholdHp;
        /// <summary>Attack tokens (enemies allowed to shoot the player at once).</summary>
        public int AttackTokens;
        /// <summary>Multiplier on enemy accuracy.</summary>
        public float EnemyAccuracyMultiplier;
        /// <summary>Shared Legion grenade interval in seconds.</summary>
        public float LegionGrenadeInterval;
        /// <summary>Seconds the player may stay static in one cover before Greycoat throw a grenade.</summary>
        public float GreycoatGrenadeTrigger;
        /// <summary>Marksman damage per hit (100 = lethal on Veteran).</summary>
        public float MarksmanDamagePerHit;

        /// <summary>Returns the GDD §16 profile for a level.</summary>
        public static DifficultyProfile For(Difficulty level)
        {
            // GDD §16 table, column by column.
            switch (level)
            {
                case Difficulty.Recruit:
                    return Make(level, 0.5f, 10, 3.0f, 60f, 1.5f, 2, 0.7f, 90f, 10f, 45f);
                case Difficulty.Hardened:
                    return Make(level, 1.5f, 4, 5.0f, 40f, 0.4f, 4, 1.15f, 40f, 5f, 90f);
                case Difficulty.Veteran:
                    return Make(level, 2.2f, 3, 5.5f, 35f, 0f, 5, 1.3f, 30f, 4f, 100f);
                default:
                    return Make(Difficulty.Regular, 1.0f, 5, 4.0f, 50f, 0.8f, 3, 1.0f, 60f, 6f, 70f);
            }
        }

        private static DifficultyProfile Make(Difficulty level, float dmg, int akHits, float regenDelay, float regenRate,
                                              float lastStand, int tokens, float accuracy, float legionNade,
                                              float greycoatNade, float marksman)
        {
            return new DifficultyProfile
            {
                Level = level,
                PlayerHp = 100f,
                EnemyDamageMultiplier = dmg,
                LegionAkHitsToDown = akHits,
                RegenDelay = regenDelay,
                RegenRate = regenRate,
                LastStandImmunity = lastStand,
                LastStandThresholdHp = 20f,
                AttackTokens = tokens,
                EnemyAccuracyMultiplier = accuracy,
                LegionGrenadeInterval = legionNade,
                GreycoatGrenadeTrigger = greycoatNade,
                MarksmanDamagePerHit = marksman,
            };
        }
    }
}
