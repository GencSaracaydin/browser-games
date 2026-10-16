using System.Collections.Generic;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// Enemy prefab variants (TECH_PLAN §7.6 EnemyPrefabBuilder: "variants of WP05 body prefabs + brain + archetype +
    /// world weapon"). Bodies per ASSET_MANIFEST: Legion A/B riflemen, B gunners, C RPG and rooftop targets; Greycoat A
    /// advisors, B NVG flanker and marksman.
    /// </summary>
    public static class EnemyVariants
    {
        public const string LegionRiflemanA = "LEGION_RIFLEMAN_A";
        public const string LegionRiflemanB = "LEGION_RIFLEMAN_B";
        public const string LegionGunner = "LEGION_GUNNER";
        public const string LegionRpg = "LEGION_RPG";
        public const string TechnicalGunner = "TECHNICAL_GUNNER";
        public const string GreycoatAdvisor = "GREYCOAT_ADVISOR";
        public const string GreycoatNvg = "GREYCOAT_NVG";
        public const string GreycoatMarksman = "GREYCOAT_MARKSMAN";
        public const string RooftopTarget = "ROOFTOP_TARGET";
        public const string RooftopRpg = "ROOFTOP_RPG";

        private static readonly EnemyVariant[] s_all =
        {
            new EnemyVariant(LegionRiflemanA, ArchetypeId.LegionRifleman, "CHR_LEGION_A", "PF_Legion_Rifleman_A", new[] { "Legion_A", "LegionA" }),
            new EnemyVariant(LegionRiflemanB, ArchetypeId.LegionRifleman, "CHR_LEGION_B", "PF_Legion_Rifleman_B", new[] { "Legion_B", "LegionB" }),
            new EnemyVariant(LegionGunner, ArchetypeId.LegionGunner, "CHR_LEGION_B", "PF_Legion_Gunner", new[] { "Legion_B", "LegionB" }),
            new EnemyVariant(LegionRpg, ArchetypeId.LegionRPG, "CHR_LEGION_C", "PF_Legion_RPG", new[] { "Legion_C", "LegionC" }),
            new EnemyVariant(TechnicalGunner, ArchetypeId.TechnicalGunner, "CHR_LEGION_A", "PF_Technical_Gunner", new[] { "Legion_A", "LegionA" }),
            new EnemyVariant(GreycoatAdvisor, ArchetypeId.GreycoatAdvisor, "CHR_GREYCOAT_A", "PF_Greycoat_Advisor", new[] { "Greycoat_A", "GreycoatA" }),
            new EnemyVariant(GreycoatNvg, ArchetypeId.GreycoatAdvisor, "CHR_GREYCOAT_B", "PF_Greycoat_NVG", new[] { "GreycoatB_NVG", "Greycoat_B_NVG", "NVG" }, nvg: true),
            new EnemyVariant(GreycoatMarksman, ArchetypeId.GreycoatMarksman, "CHR_GREYCOAT_B", "PF_Greycoat_Marksman", new[] { "Greycoat_B", "GreycoatB" }),
            new EnemyVariant(RooftopTarget, ArchetypeId.RooftopTarget, "CHR_LEGION_C", "PF_Rooftop_Target", new[] { "Legion_C", "LegionC" }),
            new EnemyVariant(RooftopRpg, ArchetypeId.RooftopTarget, "CHR_LEGION_C", "PF_Rooftop_RPG", new[] { "Legion_C", "LegionC" }),
        };

        /// <summary>Every variant.</summary>
        public static IReadOnlyList<EnemyVariant> All => s_all;

        /// <summary>A variant by id, or null.</summary>
        public static EnemyVariant Get(string id)
        {
            for (var i = 0; i < s_all.Length; i++)
                if (s_all[i].Id == id) return s_all[i];
            return null;
        }

        /// <summary>
        /// Default variant of an archetype; riflemen alternate A/B deterministically by enemy id so a street never shows
        /// a row of identical bodies.
        /// </summary>
        public static string DefaultFor(ArchetypeId archetype, string enemyId)
        {
            switch (archetype)
            {
                case ArchetypeId.LegionRifleman:
                    return (LongShadow.Core.MathX.Fnv1a32(enemyId ?? string.Empty) & 1u) == 0u ? LegionRiflemanA : LegionRiflemanB;
                case ArchetypeId.LegionGunner: return LegionGunner;
                case ArchetypeId.LegionRPG: return LegionRpg;
                case ArchetypeId.TechnicalGunner: return TechnicalGunner;
                case ArchetypeId.GreycoatAdvisor: return GreycoatAdvisor;
                case ArchetypeId.GreycoatMarksman: return GreycoatMarksman;
                case ArchetypeId.RooftopTarget: return RooftopTarget;
                default: return LegionRiflemanA;
            }
        }
    }
}
