using LongShadow.UI.Common;
using UnityEngine;

namespace LongShadow.UI.Menus
{
    /// <summary>
    /// Look of the 1984 terminal menus (pause, options, debrief buttons, click gate): green phosphor on CRT black, the
    /// selected entry in inverse video, typewriter monospace (Courier Prime) so columns line up.
    /// </summary>
    public static class MenuStyle
    {
        /// <summary>Menu text role (monospaced typewriter).</summary>
        public const FontRole Font = FontRole.TypewriterClean;
        /// <summary>Item size.</summary>
        public const float ItemSize = 30f;
        /// <summary>Row label size (options).</summary>
        public const float RowSize = 24f;
        /// <summary>Small print.</summary>
        public const float SmallSize = 18f;
        /// <summary>Item height.</summary>
        public const float ItemHeight = 46f;
        /// <summary>Options row height.</summary>
        public const float RowHeight = 44f;

        /// <summary>Normal text.</summary>
        public static Color Text => UIColors.Phosphor;
        /// <summary>Text on the inverse-video block.</summary>
        public static Color TextInverse => UIColors.CrtBlack;
        /// <summary>Inverse-video block.</summary>
        public static Color Block => UIColors.WithAlpha(UIColors.Phosphor, 0.92f);
        /// <summary>Disabled text.</summary>
        public static Color Disabled => UIColors.WithAlpha(UIColors.PhosphorDim, 1f);
        /// <summary>Phosphor glow under menu text.</summary>
        public static Color Glow => UIColors.WithAlpha(UIColors.Phosphor, 0.32f);
    }
}
