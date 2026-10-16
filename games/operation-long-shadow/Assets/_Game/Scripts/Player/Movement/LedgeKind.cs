namespace LongShadow.Player.Movement
{
    /// <summary>Scripted traversal moves of the player (GDD §12).</summary>
    public enum LedgeKind
    {
        /// <summary>No traversal.</summary>
        None = 0,
        /// <summary>0.6–1.4 m ledge, automatic when moving into it, 0.45 s (onto, or over a thin wall).</summary>
        Vault = 1,
        /// <summary>1.4–2.1 m ledge on Space, 0.8 s.</summary>
        Climb = 2,
        /// <summary>Airborne grab of a ledge 0–1.4 m above the feet and &lt;= 0.8 m ahead, 0.5 s.</summary>
        AirMantle = 3,
        /// <summary>Step-off at the top of a ladder.</summary>
        LadderExit = 4,
    }
}
