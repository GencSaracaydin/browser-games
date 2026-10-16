namespace LongShadow.UI.Common
{
    /// <summary>Counts and flags of <see cref="UISpriteId"/> and <see cref="UITextureId"/> (theme array sizes).</summary>
    public static class UISpriteIds
    {
        /// <summary>Number of <see cref="UISpriteId"/> values (highest value + 1).</summary>
        public const int Count = 41;

        /// <summary>Number of <see cref="UITextureId"/> values.</summary>
        public const int TextureCount = 3;

        /// <summary>
        /// True for decorative art that has no procedural stand-in: the views draw their own CRT-style shapes when the
        /// theme has no sprite (<see cref="UISprites.GetOptional"/> returns null).
        /// </summary>
        public static bool IsOptional(UISpriteId id) =>
            id == UISpriteId.Stamp || id == UISpriteId.RedactionBar || id == UISpriteId.StrikeThrough ||
            id == UISpriteId.DossierBackground;
    }
}
