using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>One subtitle shown by the HUD (GDD §11 radio subtitle box). Raised by VO via <see cref="GameEvents.SubtitleShow"/>.</summary>
    public struct SubtitleData
    {
        /// <summary>Handle used by <see cref="GameEvents.SubtitleHide"/>.</summary>
        public int Handle;
        /// <summary>DIALOGUE.json line id.</summary>
        public string LineId;
        /// <summary>Speaker display name ("HOLLIS").</summary>
        public string Speaker;
        /// <summary>Callsign shown in brackets ("SUNDIAL-ACTUAL"), may be null.</summary>
        public string Callsign;
        /// <summary>Subtitle text (DIALOGUE.json "subtitle" if present, else "text").</summary>
        public string Text;
        /// <summary>Speaker colour (GDD §11 table).</summary>
        public Color Color;
        /// <summary>Show the radio-wave icon.</summary>
        public bool Radio;
        /// <summary>Italic (interrogator in the booth).</summary>
        public bool Italic;
        /// <summary>Monospace (Clockmaker).</summary>
        public bool Monospace;
        /// <summary>Enemy bark (only within 25 m and when "Translate enemy chatter" is on).</summary>
        public bool IsBark;
        /// <summary>Seconds to show (0 = until hidden).</summary>
        public float Duration;
    }
}
