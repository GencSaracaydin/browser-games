using TMPro;
using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Resolved fonts of one <see cref="UITheme"/>: every role has a usable font (see <see cref="UIFonts.Resolve"/> for the
    /// fallback chain), a soft-underlay "shadow" material for readable HUD text over a sunlit desert and a glow base
    /// material for CRT text.
    /// </summary>
    public sealed class FontSet
    {
        private readonly TMP_FontAsset[] _fonts;
        private readonly Material[] _shadows;
        private readonly Material[] _glows;
        private readonly bool[] _isOwnFont;

        internal FontSet(TMP_FontAsset[] fonts, Material[] shadows, Material[] glows, bool[] isOwnFont)
        {
            _fonts = fonts;
            _shadows = shadows;
            _glows = glows;
            _isOwnFont = isOwnFont;
        }

        /// <summary>Font of a role (null only when no font at all could be found; text then does not render).</summary>
        public TMP_FontAsset this[FontRole role] => _fonts[(int)role];

        /// <summary>Soft dark underlay material for the role's font, or null (plain material).</summary>
        public Material Shadow(FontRole role) => _shadows[(int)role];

        /// <summary>Glow base material (underlay keyword on) for the role's font, or null; tinted copies come from <see cref="UIFonts.Glow"/>.</summary>
        public Material GlowBase(FontRole role) => _glows[(int)role];

        /// <summary>True when the role uses its own font from the theme (not a fallback).</summary>
        public bool HasOwnFont(FontRole role) => _isOwnFont[(int)role];
    }
}
