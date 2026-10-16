namespace LongShadow.UI.Common
{
    /// <summary>Typographic roles of the UI (GDD §11 fonts; manifest UI_FONT_*). Indexes <see cref="UITheme"/> font slots.</summary>
    public enum FontRole
    {
        /// <summary>Distressed typewriter (Special Elite, UI_FONT_TYPEWRITER): title card, fail card, debrief stamp.</summary>
        Typewriter = 0,
        /// <summary>Clean monospaced typewriter (Courier Prime, UI_FONT_TYPEWRITER): objectives, dossier, menus.</summary>
        TypewriterClean = 1,
        /// <summary>Condensed sans for numerals (Barlow Condensed SemiBold, UI_FONT_CONDENSED): ammo, distances, compass.</summary>
        Condensed = 2,
        /// <summary>Condensed sans, medium weight (Barlow Condensed Medium): subtitles, prompts, labels.</summary>
        CondensedMedium = 3,
        /// <summary>Monospace terminal face (Share Tech Mono / IBM Plex Mono, UI_FONT_MONO): Clockmaker lines, CRT readouts.</summary>
        Mono = 4,
        /// <summary>The invented Sardani glyph font (UI_FONT_SARDANI, Tools/ui/glyph_font.py).</summary>
        Sardani = 5,
        /// <summary>TMP LiberationSans SDF (UI_FONT_FALLBACK).</summary>
        Fallback = 6,
    }
}
