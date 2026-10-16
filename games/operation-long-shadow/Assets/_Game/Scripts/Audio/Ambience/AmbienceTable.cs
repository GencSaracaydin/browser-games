using LongShadow.Audio.Core;
using UnityEngine;

namespace LongShadow.Audio.Ambience
{
    /// <summary>
    /// The ambience beds of the mission (GDD §10.2, §9.2a, §7.0; TECH_PLAN §7.9 AmbienceDirector) as code data
    /// (TECH_PLAN D12). Zone gains are indexed by <see cref="LongShadow.Core.ZoneType"/>:
    /// OpenDesert, Street, Square, Interior, Basement, Roof, Cabin. Outdoor beds leak into interiors at about −18 dB
    /// (0.12) and never reach the basement or the helicopter cabin.
    /// <list type="bullet">
    /// <item>Golden (s 0–1): desert wind, cicadas, the curfewed town (far dog, shutters, wind in cables).</item>
    /// <item>Sunset: at <c>s</c> 1.97 the cicadas stop and the crickets start (GDD §8.5 MID-A 0:14).</item>
    /// <item>Station (Beats 6–10): the generator hum within ~120 m of the compound, the relay hall's room tone, the basement
    /// tube racks until the fourth rack dies.</item>
    /// <item>Blue hour (s ≥ 2.9): salt-pan wind, distant shouting (built from our own Legion takes), distant firefights in
    /// Beat 10.</item>
    /// <item>Cinema interior (Beat 4), the helicopter cabin (Beat 11).</item>
    /// </list>
    /// </summary>
    public static class AmbienceTable
    {
        /// <summary>Booth room tone (booth mode and glitch inserts).</summary>
        public const string BoothRoomTone = "AMB_ROOMTONE_BOOTH";
        /// <summary>Level of the booth room tone.</summary>
        public const float BoothLevel = 0.8f;

        /// <summary>The station compound centre (Appendix A: relay yard and hall, x 300–350, z 300–360).</summary>
        public static readonly Vector3 StationCentre = new Vector3(335f, 0f, 325f);

        private const float Open = 99f;

        // Zone order: OpenDesert, Street, Square, Interior, Basement, Roof, Cabin.
        private static float[] Z(float desert, float street, float square, float interior, float basement, float roof, float cabin) =>
            new[] { desert, street, square, interior, basement, roof, cabin };

        /// <summary>Every bed, in evaluation order.</summary>
        public static readonly AmbienceBedRule[] Beds =
        {
            // Dry desert wind: full on the ridge and the roofs, sheltered in the streets; hands over to the salt-pan wind
            // through the afterglow (GDD §10.2 Golden "wind").
            new AmbienceBedRule("AMB_DESERT_WIND", 0.75f, -Open, 2.7f, 0.5f, -1, -1, Z(1f, 0.45f, 0.6f, 0.12f, 0f, 0.85f, 0f), 3f),
            // Cicadas in the olive trees, golden hour only; they stop at s 1.97 (GDD §9.2a, §8.5 MID-A 0:14).
            new AmbienceBedRule("AMB_CICADAS", 0.7f, -Open, AudioConstants.CricketsStageS, 0f, 1, 5, Z(1f, 0.55f, 0.6f, 0.1f, 0f, 0.75f, 0f), 1.2f),
            // Crickets start at the same instant and fill in slowly.
            new AmbienceBedRule("AMB_CRICKETS", 0.65f, AudioConstants.CricketsStageS, Open, 0f, 5, 11, Z(1f, 0.55f, 0.65f, 0.12f, 0f, 0.75f, 0f), 4f),
            // The curfewed town (GDD §2 "curfew at 17:30"), Beats 1–5 while the light lasts.
            new AmbienceBedRule("AMB_TOWN_CURFEW", 0.6f, -Open, 2.0f, 0.2f, 1, 5, Z(0.25f, 0.9f, 0.8f, 0.12f, 0f, 0.6f, 0f), 3f),
            // The station's generator (GDD §10.2 "Station: generator hum"), heard within ~120 m of the compound.
            new AmbienceBedRule("AMB_GENERATOR_HUM", 0.7f, -Open, Open, 0f, 6, 10, Z(1f, 1f, 1f, 0.55f, 0.35f, 1f, 0f), 2f,
                AmbienceCondition.None, StationCentre, 30f, 120f),
            // Blue hour: salt-pan wind (GDD §10.2).
            new AmbienceBedRule("AMB_SALTPAN_WIND", 0.7f, 2.9f, Open, 0.4f, -1, -1, Z(1f, 0.4f, 0.5f, 0.1f, 0f, 0.85f, 0f), 4f),
            // Blue hour: the alarmed town shouting far away (our own Legion takes through the FAR chain).
            new AmbienceBedRule("AMB_DISTANT_SHOUTING", 0.45f, 2.9f, Open, 0.2f, 9, 11, Z(0.8f, 0.9f, 0.9f, 0.15f, 0.05f, 0.9f, 0f), 5f),
            // Distant firefights while Magpie comes in (CINEMATIC_DIRECTION §4 "distant firefights").
            new AmbienceBedRule("AMB_DISTANT_GUNFIRE", 0.35f, 3.2f, Open, 0.1f, 10, 10, Z(1f, 0.8f, 0.8f, 0.1f, 0f, 0.9f, 0f), 5f),
            // The relay hall and the station's rooms (Beats 6–9).
            new AmbienceBedRule("AMB_ROOMTONE_HALL", 0.6f, -Open, Open, 0f, 6, 9, Z(0f, 0f, 0f, 1f, 0.4f, 0f, 0f), 0.8f),
            // The basement full of tube transmitters: dies with the fourth rack (GDD §7.9, D066/D067).
            new AmbienceBedRule("AMB_BASEMENT_TUBES", 0.85f, -Open, Open, 0f, 8, 9, Z(0f, 0f, 0f, 0.1f, 1f, 0f, 0f), 0.8f,
                AmbienceCondition.TransmittersOn, default, 0f, 0f, 0.25f),
            // The cinema's lobby and auditorium (Beat 4).
            new AmbienceBedRule("AMB_CINEMA_INTERIOR", 0.75f, -Open, Open, 0f, 4, 4, Z(0f, 0f, 0f, 1f, 0f, 0f, 0f), 0.8f),
            // The helicopter cabin (Beat 11 door gun; the cabin kit carries a Cabin ZoneTag).
            new AmbienceBedRule("SFX_HELI_CABIN_LOOP", 1f, -Open, Open, 0f, 10, 11, Z(0f, 0f, 0f, 0f, 0f, 0f, 1f), 1f),
        };

        /// <summary>The rule of a cue id, or null.</summary>
        public static AmbienceBedRule Find(string cueId)
        {
            for (var i = 0; i < Beds.Length; i++)
                if (Beds[i].CueId == cueId) return Beds[i];
            return null;
        }
    }
}
