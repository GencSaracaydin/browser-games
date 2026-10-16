using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Speaker ids exactly as in the <c>speaker</c> field of <c>Docs/DIALOGUE.json</c>, and their subtitle styles
    /// (GDD §11 radio subtitle box). The speaker id is what <see cref="GameEvents.VOLineStarted"/> and
    /// <see cref="IVOService.Amplitude"/> use; map it to a squad member with <see cref="ToSquadId"/>.
    /// </summary>
    public static class SpeakerIds
    {
        public const string Hollis = "HOLLIS";
        public const string Sparrow = "SPARROW";
        public const string Tinker = "TINKER";
        /// <summary>The player character (SUNDIAL-3); booth voice-over and a few radio lines.</summary>
        public const string Calder = "CALDER";
        /// <summary>Helicopter pilot.</summary>
        public const string Magpie = "MAGPIE";
        /// <summary>The booth interrogator (callsign UNKNOWN until revealed).</summary>
        public const string Interrogator = "INTERROGATOR";
        /// <summary>The interrogator once named (same voice as <see cref="Interrogator"/>).</summary>
        public const string Tallow = "TALLOW";
        /// <summary>Numbers-station voice (spliced tokens).</summary>
        public const string Clockmaker = "CLOCKMAKER";
        /// <summary>PA horn voice.</summary>
        public const string Harrou = "HARROU";
        /// <summary>Legion barks.</summary>
        public const string Legion = "LEGION";
        /// <summary>Greycoat barks.</summary>
        public const string Greycoat = "GREYCOAT";

        /// <summary>Every speaker id in DIALOGUE.json.</summary>
        public static readonly string[] All =
        {
            Hollis, Sparrow, Tinker, Calder, Magpie, Interrogator, Tallow, Clockmaker, Harrou, Legion, Greycoat,
        };

        private static readonly SpeakerStyle[] s_styles =
        {
            // GDD §11 colours. CALDER is not listed in the GDD: warm off-white (DECISIONS.md, WP00).
            new SpeakerStyle(Hollis, "HOLLIS", Hex(0xE8C77A), false, false, SquadIds.Hollis),
            new SpeakerStyle(Sparrow, "SPARROW", Hex(0x9FD3C7), false, false, SquadIds.Sparrow),
            new SpeakerStyle(Tinker, "TINKER", Hex(0xC9B3E6), false, false, SquadIds.Tinker),
            new SpeakerStyle(Calder, "CALDER", Hex(0xF0EAD6), false, false, SquadIds.Player),
            new SpeakerStyle(Magpie, "MAGPIE", Hex(0xB7C4D6), false, false, null),
            new SpeakerStyle(Interrogator, "INTERROGATOR", Hex(0xE6E6E6), true, false, null),
            new SpeakerStyle(Tallow, "TALLOW", Hex(0xE6E6E6), true, false, null),
            new SpeakerStyle(Clockmaker, "CLOCKMAKER", Hex(0x7FE07F), false, true, null),
            new SpeakerStyle(Harrou, "HARROU", Hex(0xE09A6A), false, false, null),
            new SpeakerStyle(Legion, "LEGION", Hex(0xE0705A), false, false, null),
            new SpeakerStyle(Greycoat, "GREYCOAT", Hex(0xA0A8A0), false, false, null),
        };

        /// <summary>Fallback style for unknown speakers (white, plain).</summary>
        public static readonly SpeakerStyle Unknown = new SpeakerStyle("", "", Color.white, false, false, null);

        /// <summary>Style of a speaker id (case-sensitive, upper case); <see cref="Unknown"/> if not listed.</summary>
        public static SpeakerStyle StyleOf(string speakerId)
        {
            for (var i = 0; i < s_styles.Length; i++)
                if (s_styles[i].Id == speakerId) return s_styles[i];
            return Unknown;
        }

        /// <summary>Squad member id (<see cref="SquadIds"/>) of a speaker ("HOLLIS" gives "HOL"; CALDER gives "PLAYER"), or null.</summary>
        public static string ToSquadId(string speakerId) => StyleOf(speakerId).SquadId;

        /// <summary>Speaker id of a squad member id ("HOL" gives "HOLLIS"), or null.</summary>
        public static string FromSquadId(string squadId)
        {
            if (string.IsNullOrEmpty(squadId)) return null;
            for (var i = 0; i < s_styles.Length; i++)
                if (s_styles[i].SquadId == squadId) return s_styles[i].Id;
            return null;
        }

        /// <summary>True for enemy bark speakers (subtitled within 25 m only, GDD §11).</summary>
        public static bool IsEnemy(string speakerId) => speakerId == Legion || speakerId == Greycoat;

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
