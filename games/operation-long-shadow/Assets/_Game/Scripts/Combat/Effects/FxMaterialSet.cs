using UnityEngine;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// The materials FxFactory builds particle effects from, plus the flipbook layout of each texture. The editor FX builder
    /// fills it with saved materials made from the WP10 VFX textures (TEX_VFX_*: dust and smoke 8×8, sparks 4×4, fire 8×8,
    /// muzzle 4×2, tracer); <see cref="Procedural"/> builds a runtime stand-in from <see cref="FxTextures"/>.
    /// </summary>
    public sealed class FxMaterialSet
    {
        /// <summary>Impact dust (alpha, flipbook rising from the cell bottom-centre).</summary>
        public Material Dust;
        /// <summary>Dust flipbook tiles (8×8 for T_VFX_DustImpact_8x8_BC).</summary>
        public Vector2Int DustTiles = Vector2Int.one;
        /// <summary>Smoke (alpha, flipbook).</summary>
        public Material Smoke;
        /// <summary>Smoke flipbook tiles (8×8 for T_VFX_SmokePuff_8x8_BC).</summary>
        public Vector2Int SmokeTiles = Vector2Int.one;
        /// <summary>Sparks (additive atlas: cells 0-7 streaks, 8-11 random streaks, 12-15 glints).</summary>
        public Material Sparks;
        /// <summary>Spark atlas tiles (4×4 for T_VFX_Sparks_4x4_E).</summary>
        public Vector2Int SparkTiles = Vector2Int.one;
        /// <summary>Fire (additive flipbook loop).</summary>
        public Material Fire;
        /// <summary>Fire flipbook tiles (8×8 for T_VFX_Fire_8x8_E).</summary>
        public Vector2Int FireTiles = Vector2Int.one;
        /// <summary>Alpha soft dot (chips, droplets, debris).</summary>
        public Material Soft;
        /// <summary>Additive soft dot (glints, embers, the rocket motor).</summary>
        public Material SoftAdditive;
        /// <summary>Muzzle flash atlas (additive, 4×2).</summary>
        public Material Muzzle;
        /// <summary>Tracer streak (additive; v 0.5-1 red, 0-0.5 green).</summary>
        public Material Tracer;

        /// <summary>True when every material exists.</summary>
        public bool IsComplete => Dust && Smoke && Sparks && Fire && Soft && SoftAdditive && Muzzle && Tracer;

        /// <summary>Runtime stand-in materials from procedural textures (single-frame "flipbooks").</summary>
        public static FxMaterialSet Procedural()
        {
            var dot = FxTextures.SoftDot;
            return new FxMaterialSet
            {
                Dust = FxMaterials.CreateParticle("M_FX_Dust_PH", dot, Color.white, false),
                Smoke = FxMaterials.CreateParticle("M_FX_Smoke_PH", dot, Color.white, false),
                Sparks = FxMaterials.CreateParticle("M_FX_Sparks_PH", dot, Color.white, true),
                Fire = FxMaterials.CreateParticle("M_FX_Fire_PH", dot, Color.white, true),
                Soft = FxMaterials.CreateParticle("M_FX_Soft_PH", dot, Color.white, false),
                SoftAdditive = FxMaterials.CreateParticle("M_FX_SoftAdd_PH", dot, Color.white, true),
                Muzzle = FxMaterials.CreateParticle("M_FX_Muzzle_PH", FxTextures.MuzzleSheet, new Color(2.2f, 1.7f, 1.2f, 1f), true),
                Tracer = FxMaterials.CreateParticle("M_FX_Tracer_PH", FxTextures.Tracer, new Color(4f, 4f, 4f, 1f), true),
            };
        }
    }
}
