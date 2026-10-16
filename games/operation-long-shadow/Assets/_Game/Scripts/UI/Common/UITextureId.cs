namespace LongShadow.UI.Common
{
    /// <summary>
    /// Tiling textures of the UI drawn with RawImage UV tiling (procedural fallbacks in <see cref="UISprites"/>; overrides
    /// in <see cref="UITheme"/>). Values are stable; append at the end and bump <see cref="UISpriteIds.TextureCount"/>.
    /// </summary>
    public enum UITextureId
    {
        /// <summary>CRT scanlines tile (UI_PAUSE_PANEL T_UI_Scanlines), repeated so one tile covers 2 screen px at 1080p.</summary>
        Scanlines = 0,
        /// <summary>Dark radial vignette for CRT panels.</summary>
        Vignette = 1,
        /// <summary>Grey noise for static and grain (tiled, offset per frame).</summary>
        Noise = 2,
    }
}
