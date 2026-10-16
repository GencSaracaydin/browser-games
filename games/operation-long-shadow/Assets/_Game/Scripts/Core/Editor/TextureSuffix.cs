namespace LongShadow.Core.Editor
{
    /// <summary>Texture role from the file-name suffix of <c>T_&lt;Name&gt;_&lt;Suffix&gt;</c> (TECH_PLAN §4.2).</summary>
    public enum TextureSuffix
    {
        /// <summary>No recognised suffix (images, sky panoramas, UI sprites).</summary>
        None = 0,
        /// <summary>Base colour (sRGB; alpha = opacity when present).</summary>
        BC,
        /// <summary>Tangent-space normal map, OpenGL convention (+Y).</summary>
        N,
        /// <summary>Mask (D4): R metallic, G AO, B height (terrain sets) or 0, A smoothness. Linear.</summary>
        M,
        /// <summary>Emission (sRGB).</summary>
        E,
        /// <summary>Height for parallax (linear); 16-bit PNGs are editor-only height data.</summary>
        H,
        /// <summary>Auxiliary linear mask (opacity, paint/feature mask).</summary>
        A,
    }
}
