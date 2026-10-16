using LongShadow.Core;

namespace LongShadow.AI.Squad
{
    /// <summary>
    /// Squad callout line ids per member (Docs/DIALOGUE.json CO_HOL/SPA/TIN pools, GDD §15). A member without a line for
    /// a perch tag returns null (Hollis has no "Balcony", Sparrow no "Window", Tinker no "Rooftop"/"Balcony"); the caller
    /// falls back to the direction line.
    /// </summary>
    public static class CalloutIds
    {
        /// <summary>Every callout id used by the squad (DIALOGUE.json check).</summary>
        public static readonly string[] All =
        {
            "CO_HOL_01", "CO_HOL_02", "CO_HOL_03", "CO_HOL_04", "CO_HOL_05", "CO_HOL_06", "CO_HOL_07", "CO_HOL_08",
            "CO_HOL_09", "CO_HOL_10", "CO_HOL_11", "CO_HOL_12",
            "CO_SPA_01", "CO_SPA_02", "CO_SPA_03", "CO_SPA_04", "CO_SPA_05", "CO_SPA_06", "CO_SPA_07", "CO_SPA_08",
            "CO_SPA_09", "CO_SPA_10", "CO_SPA_11",
            "CO_TIN_01", "CO_TIN_02", "CO_TIN_03", "CO_TIN_04", "CO_TIN_05", "CO_TIN_06", "CO_TIN_07", "CO_TIN_08",
            "CO_TIN_09", "CO_TIN_10", "CO_TIN_11",
        };

        /// <summary>Hollis: "Good kill." when the player kills a high-priority target.</summary>
        public const string GoodKill = "CO_HOL_10";
        /// <summary>Hollis: "On me, Cal." follow nag (player idle 20 s with a FOLLOW objective).</summary>
        public const string OnMe = "CO_HOL_11";
        /// <summary>Hollis: "Keep up, Calder." follow nag (player 30 m behind).</summary>
        public const string KeepUp = "CO_HOL_12";
        /// <summary>Tinker: "Lord, that was close." (player below 25 % health survives).</summary>
        public const string CloseCall = "CO_TIN_10";

        /// <summary>Contact line for a direction.</summary>
        public static string Direction(string member, RelativeDirection d)
        {
            var n = d == RelativeDirection.Front ? "01" : d == RelativeDirection.Left ? "02" : d == RelativeDirection.Right ? "03" : "04";
            switch (member)
            {
                case SquadIds.Hollis: return "CO_HOL_" + n;
                case SquadIds.Sparrow: return "CO_SPA_" + n;
                case SquadIds.Tinker: return "CO_TIN_" + n;
                default: return null;
            }
        }

        /// <summary>Perch line ("Rooftop!", "Balcony!", "Window!") or null when the member has none.</summary>
        public static string Perch(string member, PerchTag tag)
        {
            switch (tag)
            {
                case PerchTag.Rooftop:
                    return member == SquadIds.Hollis ? "CO_HOL_05" : member == SquadIds.Sparrow ? "CO_SPA_06" : null;
                case PerchTag.Balcony:
                    return member == SquadIds.Sparrow ? "CO_SPA_05" : null;
                case PerchTag.Window:
                    return member == SquadIds.Tinker ? "CO_TIN_05" : null;
                default:
                    return null;
            }
        }

        /// <summary>The line for a spotted enemy: its perch line when the member has one, else the direction line.</summary>
        public static string Spot(string member, RelativeDirection direction, PerchTag perch) =>
            Perch(member, perch) ?? Direction(member, direction);

        /// <summary>"Frag out!"</summary>
        public static string FragOut(string member) =>
            member == SquadIds.Hollis ? "CO_HOL_06" : member == SquadIds.Sparrow ? "CO_SPA_07" : member == SquadIds.Tinker ? "CO_TIN_06" : null;

        /// <summary>"Grenade!" (enemy frag within 6 m of the player).</summary>
        public static string Grenade(string member) =>
            member == SquadIds.Hollis ? "CO_HOL_07" : member == SquadIds.Sparrow ? "CO_SPA_08" : member == SquadIds.Tinker ? "CO_TIN_07" : null;

        /// <summary>"Reloading!" (20 s cooldown).</summary>
        public static string Reload(string member) =>
            member == SquadIds.Hollis ? "CO_HOL_08" : member == SquadIds.Sparrow ? "CO_SPA_09" : member == SquadIds.Tinker ? "CO_TIN_08" : null;

        /// <summary>"Moving up!" (squad advances to the next node set).</summary>
        public static string Moving(string member) =>
            member == SquadIds.Hollis ? "CO_HOL_09" : member == SquadIds.Sparrow ? "CO_SPA_10" : member == SquadIds.Tinker ? "CO_TIN_09" : null;

        /// <summary>Scripted-kill line ("Got him." / "Got one!"), null for Hollis.</summary>
        public static string ScriptedKill(string member) =>
            member == SquadIds.Sparrow ? "CO_SPA_11" : member == SquadIds.Tinker ? "CO_TIN_11" : null;
    }
}
