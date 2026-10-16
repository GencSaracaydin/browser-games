namespace LongShadow.Core
{
    /// <summary>Streaming sections (GDD §6.7). The numeric value is the scene number (S0..S9).</summary>
    public enum SectionId
    {
        /// <summary>S0_Booth (interrogation booth, local frame at (-600, 0, -600)).</summary>
        Booth = 0,
        /// <summary>S1_Approach (ridge, wadi, grove).</summary>
        Approach = 1,
        /// <summary>S2_Market (Weighbridge, Long Market).</summary>
        Market = 2,
        /// <summary>S3_Square (Clock Square, Cinema Zenith).</summary>
        Square = 3,
        /// <summary>S4_Roofs (Laundry Roofs R1-R4).</summary>
        Roofs = 4,
        /// <summary>S5_StationExt (relay yard).</summary>
        StationExt = 5,
        /// <summary>S6_StationInt (Relay Hall, screening room, basement).</summary>
        StationInt = 6,
        /// <summary>S7_Pitch (LZ).</summary>
        Pitch = 7,
        /// <summary>S8_Heli (cabin and exfil).</summary>
        Heli = 8,
        /// <summary>S9_TownFar (baked far proxies).</summary>
        TownFar = 9,
    }
}
