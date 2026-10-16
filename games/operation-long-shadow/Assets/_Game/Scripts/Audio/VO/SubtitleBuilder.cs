using LongShadow.Core;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// Builds the <see cref="SubtitleData"/> the HUD shows (GDD §11 radio subtitle box, raised as
    /// <see cref="GameEvents.SubtitleShow"/>): <c>[SUNDIAL-ACTUAL] HOLLIS: text</c> for speakers, <c>[LEGION] (shouting)
    /// text</c> for barks (no speaker name), no bracket when the callsign equals the speaker name (CLOCKMAKER, TALLOW);
    /// speaker colours, italics in the booth and the Clockmaker's monospace come from <see cref="SpeakerIds"/>. Pure.
    /// </summary>
    public static class SubtitleBuilder
    {
        /// <summary>Builds the subtitle of <paramref name="line"/>.</summary>
        public static SubtitleData Build(DialogueLine line, int handle, bool radio, bool inBooth, float duration)
        {
            var style = SpeakerIds.StyleOf(line.Speaker);
            var name = string.IsNullOrEmpty(style.DisplayName) ? line.Speaker : style.DisplayName;
            var d = new SubtitleData
            {
                Handle = handle,
                LineId = line.Id,
                Text = line.Subtitle,
                Color = style.Color,
                Radio = radio,
                Italic = style.ItalicInBooth && inBooth,
                Monospace = style.Monospace,
                IsBark = line.IsBark,
                Duration = duration,
            };
            if (line.IsBark)
            {
                d.Speaker = "";
                d.Callsign = string.IsNullOrEmpty(line.Callsign) ? name : line.Callsign;
            }
            else
            {
                d.Speaker = name;
                d.Callsign = string.IsNullOrEmpty(line.Callsign) || line.Callsign == name ? null : line.Callsign;
            }
            return d;
        }
    }
}
