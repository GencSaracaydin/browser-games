namespace LongShadow.Mission.Data
{
    /// <summary>
    /// Encounter, wave and enemy ids of WP04's <c>EncounterTable</c> (GDD §7.2-7.11) as the mission scripts address them
    /// through <c>IEncounterService</c>. Mission cannot reference the AI assembly, so the strings are mirrored here; the
    /// waves the scripts trigger are exactly those whose GDD condition the scripts also own (both sides are idempotent).
    /// </summary>
    public static class EncounterIds
    {
        public const string E2 = "E2";
        public const string E3 = "E3";
        public const string E4 = "E4";
        public const string E5 = "E5";
        public const string E6 = "E6";
        public const string E7 = "E7";
        public const string E9 = "E9";
        public const string E10 = "E10";
        public const string E11 = "E11";

        // ---- waves
        public const string E2Sentries = "Sentries";
        public const string E2Gate = "Gate";
        public const string E3A = "A";
        public const string E3Balcony = "Balcony";
        public const string E3B = "B";
        public const string E4Square = "Square";
        public const string E4Lobby = "Lobby";
        public const string E4Greycoat = "Greycoat";
        public const string E5Main = "Main";
        public const string E6Yard = "Yard";
        public const string E6Gatehouse = "Gatehouse";
        public const string E7Breach = "Breach";
        public const string E7Stair = "Stair";
        public const string E9W1 = "W1";
        public const string E9W2 = "W2";
        public const string E9W3 = "W3";
        public const string E10Truck1 = "Truck1";
        public const string E10Nvg = "NVG";
        public const string E10Truck2 = "Truck2";
        public const string E11TargetsWave = "Targets";

        // ---- enemies with script roles
        /// <summary>Beat 2 left sentry (the player's).</summary>
        public const string SentryLeft = "E2-01";
        /// <summary>Beat 2 right sentry (Hollis's ScriptedKill on "Mark").</summary>
        public const string SentryRight = "E2-02";
        /// <summary>Beat 2 RPK gunner on the truck bed (D018).</summary>
        public const string TruckGunner = "E2-06";
        /// <summary>Beat 3 balcony gunner (D022).</summary>
        public const string BalconyGunner = "E3-05";
        /// <summary>Beat 4 technical gunner (B4.Technical).</summary>
        public const string TechnicalGunner = "E4-01";
        /// <summary>Beat 5 marksman on the mast platform.</summary>
        public const string Marksman = "E5-01";
        /// <summary>Beat 5 R3 nest guards (MID-A trigger).</summary>
        public const string NestGuardA = "E5-02";
        /// <summary>Beat 5 R3 nest guards (MID-A trigger).</summary>
        public const string NestGuardB = "E5-03";
        /// <summary>Beat 10 RPG gunner on truck 2 (B10.RPG).</summary>
        public const string RpgGunner = "E10-07";

        /// <summary>Beat 2 gate team (B2.Weighbridge completes when they are dead).</summary>
        public static readonly string[] E2GateTeam = { "E2-03", "E2-04", "E2-05", "E2-06" };
        /// <summary>Beat 3 wave B (D026 when one of them leaves the z 197 cross-passage).</summary>
        public static readonly string[] E3WaveB = { "E3-07", "E3-08", "E3-09" };
        /// <summary>Beat 7 breach targets (B7.Breach).</summary>
        public static readonly string[] E7BreachTeam = { "E7-01", "E7-02", "E7-03" };
        /// <summary>Beat 9 wave-2 Greycoats (D062 at the east stair foot).</summary>
        public static readonly string[] E9Wave2 = { "E9-04", "E9-05", "E9-06" };
        /// <summary>Beat 11 rooftop targets T1-T4.</summary>
        public static readonly string[] E11Targets = { "E11-T1", "E11-T2", "E11-T3", "E11-T4" };
    }
}
