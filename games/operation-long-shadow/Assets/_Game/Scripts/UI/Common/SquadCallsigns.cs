using LongShadow.Core;

namespace LongShadow.UI.Common
{
    /// <summary>Squad nameplate text (GDD §4.1 callsigns, §11 "the green callsign appears over a squadmate's head").</summary>
    public static class SquadCallsigns
    {
        /// <summary>Callsign of a squad member id ("HOL" gives "SUNDIAL-ACTUAL"), or null.</summary>
        public static string CallsignOf(string squadId)
        {
            switch (squadId)
            {
                case SquadIds.Hollis: return "SUNDIAL-ACTUAL";
                case SquadIds.Sparrow: return "SUNDIAL-2";
                case SquadIds.Tinker: return "SUNDIAL-4";
                case SquadIds.Player: return "SUNDIAL-3";
                default: return null;
            }
        }

        /// <summary>Short name of a squad member id ("HOL" gives "HOLLIS"), or null.</summary>
        public static string NameOf(string squadId)
        {
            var speaker = SpeakerIds.FromSquadId(squadId);
            return speaker != null ? SpeakerIds.StyleOf(speaker).DisplayName : null;
        }
    }
}
