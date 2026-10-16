using LongShadow.Core;

namespace LongShadow.World.Look.Common
{
    /// <summary>
    /// String ids the Look systems listen to or use. Mission event ids mirror WP07's <c>LongShadow.Mission.Data.MissionEvents</c>
    /// and WP04's <c>AIEvents</c> (Look cannot reference them; the strings are the contract, same as CORE_CHANGES
    /// requests for <see cref="SequenceIds"/>). Marker ids come from TECH_PLAN Appendix A; cue ids are ASSET_MANIFEST ids.
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.SequenceIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class LookIds
    {
        // ---- mission events (GameEvents.MissionEvent)
        /// <summary>MID-A starts (Beat 5 script drives s 1.80 → 2.00).</summary>
        public const string MidAStart = SequenceIds.MidAStart;
        /// <summary>MID-A ends.</summary>
        public const string MidAEnd = SequenceIds.MidAEnd;
        /// <summary>Beat 4 cinema sky swap (1.00 → 1.35 over 3 s, inside the cinema).</summary>
        public const string CinemaSkySwap = SequenceIds.CinemaSkySwap;
        /// <summary>Beat 4: the technical's fuel tank ignited (fire prop at B4.FireProp lights; WP04 request).</summary>
        public const string TechnicalIgnited = SequenceIds.B4TechnicalIgnited;
        /// <summary>Beat 6: floodlights F1-F4 slam on (4 s after the blast).</summary>
        public const string FloodsOn = SequenceIds.FloodsOn;
        /// <summary>Beat 9: the fourth rack died; the emergency lightmap set swaps in (G11).</summary>
        public const string TransmitterDead = SequenceIds.TransmitterDead;
        /// <summary>Beat 10 flares: "B10.FL1" (t 12), "B10.FL2" (t 42), "B10.FL3" (t 72, wave-off only).</summary>
        public const string Flare1 = SequenceIds.Flare1;
        /// <summary>Second flare.</summary>
        public const string Flare2 = SequenceIds.Flare2;
        /// <summary>Third flare.</summary>
        public const string Flare3 = SequenceIds.Flare3;

        // ---- markers (Appendix A)
        /// <summary>Flood marker/destructible ids (also the IlluminationZone owner ids WP04 reads, "B6.F" prefix).</summary>
        public static readonly string[] Floods = { "B6.F1", "B6.F2", "B6.F3", "B6.F4" };
        /// <summary>Flood aim point (Appendix A: aim (345, 0, 325)).</summary>
        public static readonly UnityEngine.Vector3 FloodAim = new UnityEngine.Vector3(345f, 0f, 325f);
        /// <summary>Basement emergency light markers (realtime red lights at the stair feet, GDD §7.9).</summary>
        public static readonly string[] EmergencyMarkers = { "B9.Emergency1", "B9.Emergency2" };
        /// <summary>Beauty-shot camera marker prefix (LookCapture).</summary>
        public const string LookCameraPrefix = "CAM.Look.";
        /// <summary>Fire prop marker (Beat 4).</summary>
        public const string B4FireProp = "B4.FireProp";

        // ---- audio cues (ASSET_MANIFEST ids, WP06 LIB_Audio)
        /// <summary>Sodium lamp strike.</summary>
        public const string CueSodiumStrike = "SFX_SODIUM_STRIKE";
        /// <summary>Flood slam-on.</summary>
        public const string CueFloodSlamOn = "SFX_FLOOD_SLAM_ON";
        /// <summary>Parachute flare ignition and burn.</summary>
        public const string CueFlare = "SFX_FLARE";
    }
}
