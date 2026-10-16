namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// World-level constants of the Zaroud tile (GDD §6.1, §6.5, §9.3; TECH_PLAN §7.15, D7). Every builder and runtime
    /// component of the Level track reads its numbers from here so the scenes, the markup and the tests agree.
    /// </summary>
    public static class WorldConst
    {
        /// <summary>Playable tile edge length in metres (GDD §6.1: 420 x 420 m, origin at the south-west corner).</summary>
        public const float TileSize = 420f;
        /// <summary>Town datum: Clock Square paving, market street, Relay Station yard (GDD §6.1).</summary>
        public const float TownDatum = 0f;
        /// <summary>Ridge-top height at the spawn (GDD §6.1, §7.1).</summary>
        public const float RidgeTop = 22f;
        /// <summary>The Pitch (LZ) floor height (GDD §6.5 A16).</summary>
        public const float PitchY = -1f;
        /// <summary>Salt pan surface (out of bounds, a little below the Pitch).</summary>
        public const float SaltPanY = -1.6f;

        /// <summary>Sun azimuth in compass degrees (GDD §6.1): WSW, behind the player on the critical path.</summary>
        public const float SunAzimuth = 258f;
        /// <summary>Directional light yaw (azimuth 258 - 180 = 78; TECH_PLAN §3.8 rotation (elevation, 78, 0)).</summary>
        public const float SunLightYaw = 78f;

        /// <summary>Player eye height standing (TECH_PLAN §7.3).</summary>
        public const float EyeHeight = 1.65f;
        /// <summary>Player capsule height (TECH_PLAN §7.3).</summary>
        public const float PlayerHeight = 1.8f;

        /// <summary>Distance below which a far-proxy block of a loaded section hides (TECH_PLAN D7, GDD §6.7).</summary>
        public const float FarProxySwapDistance = 90f;
        /// <summary>Width of the proxy/real hand-over band (the 10 m cross-fade of D7, used as hysteresis).</summary>
        public const float FarProxyFadeBand = 10f;

        /// <summary>Distance of the western ridge silhouette (GDD §9.3).</summary>
        public const float RidgeSilhouetteDistance = 2500f;
        /// <summary>Crest of the silhouette above R4's eye line in degrees (GDD §8.5: the disc vanishes at sun 0°).</summary>
        public const float RidgeCrestAboveEyeDegrees = 0.25f;
        /// <summary>Sun disc distance (GDD §9.3; camera far plane 3000 m).</summary>
        public const float SunDiscDistance = 2900f;
        /// <summary>Angular diameter of the sun disc in degrees (GDD §9.3).</summary>
        public const float SunDiscDegrees = 0.53f;

        /// <summary>NavMesh agent radius (TECH_PLAN §7.15).</summary>
        public const float AgentRadius = 0.35f;
        /// <summary>NavMesh agent height (TECH_PLAN §7.15).</summary>
        public const float AgentHeight = 1.8f;
        /// <summary>NavMesh step height (TECH_PLAN §7.15).</summary>
        public const float AgentClimb = 0.4f;
        /// <summary>NavMesh max slope in degrees (TECH_PLAN §7.15).</summary>
        public const float AgentSlope = 40f;
        /// <summary>How far a section's NavMesh extends past each seam (TECH_PLAN §7.15).</summary>
        public const float SeamOverlap = 4f;

        /// <summary>Name of the NavMesh area the squad never stops in (TECH_PLAN §7.15, WP04 NavUtil).</summary>
        public const string DoorwayAreaName = "Doorway";
        /// <summary>Area index used when the project has no "Doorway" area yet (first user area).</summary>
        public const int DoorwayAreaFallback = 3;
        /// <summary>Doorway area cost (TECH_PLAN §7.15).</summary>
        public const float DoorwayAreaCost = 3f;

        /// <summary>OOB soft volumes sit this far in front of their hard PlayerClip (Appendix A "OOB").</summary>
        public const float OutOfBoundsDepth = 4f;

        /// <summary>Lightmap scale of hero surfaces relative to the section's base density (GDD §9.4: 16 vs 8 texels/m).</summary>
        public const float LightmapScaleHero = 2f;
        /// <summary>Lightmap scale of houses (6-8 texels/m at a base of 8).</summary>
        public const float LightmapScaleHouse = 0.85f;
        /// <summary>Lightmap scale of terrain (4 texels/m at a base of 8).</summary>
        public const float LightmapScaleTerrain = 0.5f;
        /// <summary>Lightmap scale of large ground meshes and backdrop pieces.</summary>
        public const float LightmapScaleGround = 0.5f;

        /// <summary>Exfil flight length in seconds (GDD §7.11; B11 critical-path progress = Elapsed / 50).</summary>
        public const float ExfilSeconds = 50f;
    }
}
