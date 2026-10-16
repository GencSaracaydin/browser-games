namespace LongShadow.Core
{
    /// <summary>Squad member ids (Appendix A HOL/SPA/TIN) and actor ids used across systems.</summary>
    public static class SquadIds
    {
        public const string Hollis = "HOL";
        public const string Sparrow = "SPA";
        public const string Tinker = "TIN";
        /// <summary>Actor id of the player.</summary>
        public const string Player = "PLAYER";
        /// <summary>The three squad member ids in node-set order.</summary>
        public static readonly string[] All = { Hollis, Sparrow, Tinker };
    }
}
