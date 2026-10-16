namespace LongShadow.Core
{
    /// <summary>Team relations.</summary>
    public static class Teams
    {
        /// <summary>Player and Squad are friends; Legion and Greycoat are friends; the two sides are hostile.</summary>
        public static bool AreHostile(Team a, Team b)
        {
            if (a == Team.None || b == Team.None) return false;
            return IsFriendlySide(a) != IsFriendlySide(b);
        }

        /// <summary>True for Player and Squad.</summary>
        public static bool IsFriendlySide(Team t) => t == Team.Player || t == Team.Squad;

        /// <summary>True for Legion and Greycoat.</summary>
        public static bool IsEnemySide(Team t) => t == Team.Legion || t == Team.Greycoat;
    }
}
