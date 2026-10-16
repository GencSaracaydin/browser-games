using LongShadow.Core;
using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Formats a subtitle exactly as GDD §11 shows it: <c>[SUNDIAL-ACTUAL] HOLLIS: Off the middle of the street.</c>, and
    /// enemy barks as <c>[LEGION] (shouting) Push! Push!</c>. The speaker prefix is drawn in the speaker colour, the
    /// line in the same colour lifted towards the HUD ink; italic for the interrogator in the booth. Writes rich text
    /// into a <see cref="TextBuffer"/> (no allocation).
    /// </summary>
    public static class SubtitleFormatter
    {
        /// <summary>How far the spoken text is lifted from the speaker colour towards the ink (readability).</summary>
        public const float TextLift = 0.45f;

        /// <summary>Speaker colour of a subtitle (its own colour, else the GDD §11 style of the speaker, else white).</summary>
        public static Color ColorOf(in SubtitleData d)
        {
            if (d.Color.a > 0f) return d.Color;
            var style = SpeakerIds.StyleOf(d.Speaker);
            return style.Color.a > 0f ? style.Color : Color.white;
        }

        /// <summary>Writes the formatted subtitle (clears the buffer first).</summary>
        public static void Write(TextBuffer b, in SubtitleData d, Color ink)
        {
            b.Clear();
            var color = ColorOf(d);
            var textColor = Color.Lerp(color, ink, TextLift);
            textColor.a = 1f;
            b.OpenColor(color);
            if (d.IsBark)
            {
                b.Append('[').Append(!string.IsNullOrEmpty(d.Callsign) ? d.Callsign : d.Speaker).Append("]");
            }
            else
            {
                var hasCallsign = !string.IsNullOrEmpty(d.Callsign) && !string.Equals(d.Callsign, d.Speaker);
                if (hasCallsign) b.Append('[').Append(d.Callsign).Append("] ");
                b.Append(string.IsNullOrEmpty(d.Speaker) ? "UNKNOWN" : d.Speaker).Append(':');
            }
            b.CloseColor().Append(' ');
            b.OpenColor(textColor);
            if (d.Italic) b.Append("<i>");
            b.Append(d.Text);
            if (d.Italic) b.Append("</i>");
            b.CloseColor();
        }

        /// <summary>
        /// True when a subtitle should be shown under the player's options: subtitles on, and barks only with
        /// "Translate enemy chatter" (GDD §11; VO already filters barks beyond 25 m).
        /// </summary>
        public static bool ShouldShow(in SubtitleData d, GameSettings settings)
        {
            if (settings == null) return true;
            if (!settings.Subtitles) return false;
            if (d.IsBark && !settings.TranslateEnemyChatter) return false;
            return true;
        }

        /// <summary>Font size in reference pixels for a subtitle size option.</summary>
        public static float FontSize(SubtitleSize size)
        {
            switch (size)
            {
                case SubtitleSize.Small: return 27f;
                case SubtitleSize.Large: return 40f;
                default: return 33f;
            }
        }
    }
}
