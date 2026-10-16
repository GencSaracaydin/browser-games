namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// The spawner's hard rules as pure functions (GDD §7 conventions, §14.3): closets spawn only out of the player's
    /// sight and at least 15 m from him; an encounter never exceeds its max alive; the mission never exceeds 12 alive.
    /// </summary>
    public static class SpawnRules
    {
        /// <summary>Minimum closet distance from the player (GDD §7, §14.3).</summary>
        public const float ClosetMinDistance = 15f;

        /// <summary>
        /// True if a closet spawn is allowed: neither the closet door nor the spawn point is visible to the player and
        /// the spawn point is at least <paramref name="minDistance"/> away.
        /// </summary>
        public static bool ClosetAllowed(float playerDistance, float minDistance, bool doorVisible, bool pointVisible) =>
            !doorVisible && !pointVisible && playerDistance >= minDistance;

        /// <summary>True if one more enemy may spawn under both caps.</summary>
        public static bool UnderCaps(int encounterAlive, int maxAlive, int totalAlive, int hardCap) =>
            encounterAlive < maxAlive && totalAlive < hardCap;
    }
}
