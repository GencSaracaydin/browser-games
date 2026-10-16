using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Subtitle style of a speaker (GDD §11). Look it up with <see cref="SpeakerIds.StyleOf"/>.</summary>
    public readonly struct SpeakerStyle
    {
        /// <summary>DIALOGUE.json speaker id ("HOLLIS").</summary>
        public readonly string Id;
        /// <summary>Name printed in the subtitle ("HOLLIS").</summary>
        public readonly string DisplayName;
        /// <summary>Subtitle colour.</summary>
        public readonly Color Color;
        /// <summary>Italic when the line plays in the booth (interrogator).</summary>
        public readonly bool ItalicInBooth;
        /// <summary>Monospace font (Clockmaker).</summary>
        public readonly bool Monospace;
        /// <summary>Squad member id (<see cref="SquadIds"/>) or null for non-squad speakers.</summary>
        public readonly string SquadId;

        /// <summary>Creates a style.</summary>
        public SpeakerStyle(string id, string displayName, Color color, bool italicInBooth, bool monospace, string squadId)
        {
            Id = id;
            DisplayName = displayName;
            Color = color;
            ItalicInBooth = italicInBooth;
            Monospace = monospace;
            SquadId = squadId;
        }
    }
}
