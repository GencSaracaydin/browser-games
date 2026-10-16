namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Overrides for <see cref="MaterialFactory.BuildFromTextureSet"/> (what a texture set's file names cannot say).
    /// Every field is optional; defaults follow D4 (mask in metallic + occlusion, smoothness from mask alpha).
    /// </summary>
    public sealed class MaterialOptions
    {
        /// <summary>Material asset path; default Art/Materials/&lt;Kind&gt;/M_&lt;Set&gt;.mat under the set's root.</summary>
        public string MaterialPath;
        /// <summary>World size of one texture repeat in metres (world-UV kit meshes).</summary>
        public float? TileMeters;
        /// <summary>Alpha-clip threshold (null: clip only foliage/cards with alpha, at 0.5).</summary>
        public float? AlphaClip;
        /// <summary>Transparent surface (glass, film).</summary>
        public bool Transparent;
        /// <summary>Base colour multiplier (alpha = opacity for transparent surfaces).</summary>
        public UnityEngine.Color? BaseColor;
        /// <summary>Smoothness multiplier on the mask alpha (default 1).</summary>
        public float? Smoothness;
        /// <summary>Normal strength (default 1).</summary>
        public float? BumpScale;
        /// <summary>Parallax height scale when an H map exists (default 0.015).</summary>
        public float? Parallax;
        /// <summary>Emission colour in linear HDR values when an E map exists (default white x1).</summary>
        public UnityEngine.Color? EmissionColorLinear;
        /// <summary>Detail normal map path (shared tiling micro detail, e.g. Env/_Shared/T_DetailPlaster_N).</summary>
        public string DetailNormal;
        /// <summary>Detail normal strength (default 0.8).</summary>
        public float DetailNormalScale = 0.8f;
        /// <summary>Detail albedo (linear, 0.5 neutral; e.g. Env/_Shared/T_MacroVariation_BC).</summary>
        public string DetailAlbedo;
        /// <summary>World size of one detail repeat in metres.</summary>
        public float? DetailTileMeters;
        /// <summary>Detail tiling relative to the base UV (wins over <see cref="DetailTileMeters"/>).</summary>
        public float? DetailTiling;
        /// <summary>Pack a missing _M from loose ARM/MR/roughness/AO/metallic members of the set (default true).</summary>
        public bool AutoPackMask = true;
        /// <summary>Render both faces.</summary>
        public bool DoubleSided;
    }
}
