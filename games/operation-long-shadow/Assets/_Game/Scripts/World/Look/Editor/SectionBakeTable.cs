using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// The baking plan of GDD §9.4 as data (TECH_PLAN §3.8, D6, D7, D16): bake stage, BAKE_Sun, lightmap cap in 1024²
    /// equivalents, reflection probes, starting texel density, lightmap sets (S9_TownFar GOLDEN/SUNSET/DUSK, S6 NORMAL/
    /// EMERGENCY; the last spec of a section is the one its scene keeps), and the critical-path knots of TECH_PLAN
    /// Appendix A used to place light probes when a section has none.
    /// </summary>
    public static class SectionBakeTable
    {
        /// <summary>Total lightmap budget in 1024² equivalents (D16).</summary>
        public const float TotalCapEq = 27f;

        /// <summary>Specs of a section in bake order (the last one stays in the scene).</summary>
        public static List<SectionBakeSpec> For(SectionId section)
        {
            var list = new List<SectionBakeSpec>();
            switch (section)
            {
                case SectionId.Booth:
                    list.Add(new SectionBakeSpec { Section = section, StageS = 0f, SunOn = false, EnvironmentOff = true, CapEq = 1f, ReflectionProbes = 1, TexelsPerMeter = 12f, Notes = "Interior fluorescent 4000 K (WP09 lights), no sky" });
                    break;
                case SectionId.Approach:
                    list.Add(new SectionBakeSpec { Section = section, StageS = 0.35f, SunElevation = 6.5f, CapEq = 2f, ReflectionProbes = 3, TexelsPerMeter = 4f, ProbeBeats = new[] { 1, 2 }, Notes = "GOLDEN 6.5°, terrain at 4 texels/m" });
                    break;
                case SectionId.Market:
                    list.Add(new SectionBakeSpec { Section = section, StageS = 0.8f, SunElevation = 5f, CapEq = 4f, ReflectionProbes = 4, TexelsPerMeter = 8f, ProbeBeats = new[] { 3 }, Notes = "GOLDEN 5°; torn-awning shafts are realtime cards" });
                    break;
                case SectionId.Square:
                    list.Add(new SectionBakeSpec { Section = section, StageS = 1.35f, SunElevation = 3f, CapEq = 4f, ReflectionProbes = 4, TexelsPerMeter = 8f, ProbeBeats = new[] { 4 }, Notes = "SUNSET 3°; cinema interior bakes with its own lights" });
                    break;
                case SectionId.Roofs:
                    list.Add(new SectionBakeSpec { Section = section, StageS = 1.6f, SunElevation = 1.5f, IndirectMultiplier = 0f, CapEq = 2f, ReflectionProbes = 2, TexelsPerMeter = 8f, ProbeBeats = new[] { 5 }, Notes = "SUNSET 1.5°, sun bounce off (sky-only indirect)" });
                    break;
                case SectionId.StationExt:
                    list.Add(new SectionBakeSpec { Section = section, StageS = 2.4f, SunOn = false, BakeLampMarkers = true, CapEq = 3f, ReflectionProbes = 3, TexelsPerMeter = 6f, ProbeBeats = new[] { 6 }, Notes = "AFTERGLOW sky only; yard sodium baked; floods realtime (G4)" });
                    break;
                case SectionId.StationInt:
                    list.Add(new SectionBakeSpec { Section = section, SetName = "EMERGENCY", StageS = 2.8f, SunOn = false, DisableLightsUnder = "BasementTungsten", CapEq = 1f, AlternateMaxSize = 512, ReflectionProbes = 4, TexelsPerMeter = 10f, ProbeBeats = new[] { 7, 8, 9 }, Notes = "basement emergency set (G11)" });
                    list.Add(new SectionBakeSpec { Section = section, SetName = "NORMAL", StageS = 2.6f, SunOn = false, DisableLightsContaining = "Emergency", CapEq = 2f, ReflectionProbes = 4, TexelsPerMeter = 10f, ProbeBeats = new[] { 7, 8, 9 }, Notes = "interior tungsten 2700 K" });
                    break;
                case SectionId.Pitch:
                    list.Add(new SectionBakeSpec { Section = section, StageS = 3.45f, SunOn = false, CapEq = 1f, ReflectionProbes = 1, TexelsPerMeter = 4f, ProbeBeats = new[] { 10 }, Notes = "BLUE HOUR sky only; flares and headlights realtime" });
                    break;
                case SectionId.Heli:
                    list.Add(new SectionBakeSpec { Section = section, StageS = 3.7f, SunOn = false, CapEq = 0f, ReflectionProbes = 1, TexelsPerMeter = 2f, Notes = "light probes and the realtime red cabin lamp only" });
                    break;
                case SectionId.TownFar:
                    list.Add(new SectionBakeSpec { Section = section, SetName = "DUSK", StageS = 2.4f, SunOn = false, SunBakeType = LightmapBakeType.Baked, CapEq = 1f, AlternateMaxSize = 256, TexelsPerMeter = 2f, Notes = "Baked Lit, sky only" });
                    list.Add(new SectionBakeSpec { Section = section, SetName = "SUNSET", StageS = 1.6f, SunElevation = 1.5f, SunBakeType = LightmapBakeType.Baked, CapEq = 2f, AlternateMaxSize = 512, TexelsPerMeter = 2f, Notes = "Baked Lit, sun 1.5°" });
                    list.Add(new SectionBakeSpec { Section = section, SetName = "GOLDEN", StageS = 0.1f, SunElevation = 7f, SunBakeType = LightmapBakeType.Baked, CapEq = 4f, TexelsPerMeter = 2f, Notes = "Baked Lit, sun 7° (Beat 1 long shadows)" });
                    break;
            }
            return list;
        }

        /// <summary>Stage thresholds of the S9_TownFar swap (alternates in order SUNSET, DUSK; D7).</summary>
        public static readonly float[] TownFarThresholds = { 1.35f, 1.97f };

        /// <summary>Critical-path knots of a beat (TECH_PLAN Appendix A), for probe placement.</summary>
        public static Vector3[] Knots(int beat)
        {
            switch (beat)
            {
                case 1: return V(42, 22, 30, 70, 18, 50, 82, 13, 62, 90, 8, 72, 98, 4, 82, 108, 0, 96);
                case 2: return V(108, 0, 96, 136, 0, 112, 158, 0, 106, 172, 0, 146, 183, 0, 163, 185, 0, 170);
                case 3: return V(185, 0, 170, 185, 0, 200, 185, 0, 216, 185, 0, 246, 185, 0, 252);
                case 4: return V(185, 0, 248, 181, 0, 244, 177, 0, 270, 177, 0, 300, 186, 0, 307, 192, 0, 307, 210, 0, 320, 226, 0, 314, 226, 4.5f, 315, 214, 4.5f, 306, 214, 4.5f, 301, 227, 4.5f, 300, 227, 0, 297, 236, 0, 301);
                case 5: return V(236, 9, 304, 240, 9, 318, 247, 8.5f, 318, 254, 8, 316, 260, 9, 324, 266, 10, 320, 276, 11, 320, 284, 12, 322, 291, 12, 328);
                case 6: return V(291, 12, 330, 295, 0, 334, 296, 0, 310, 302, 0, 322, 316, 0, 316, 330, 0, 314, 338, 0, 328.5f);
                case 7: return V(338, 0.3f, 331, 330, 0.3f, 342, 346, 0.3f, 350, 351, 0.3f, 355, 334.5f, 0.3f, 358, 334.5f, 4.8f, 358, 335, 4.8f, 355);
                case 8: return V(335, 4.8f, 355, 323, 4.8f, 352, 334.5f, 4.8f, 358, 334.5f, 0.3f, 358, 322, 0.3f, 340, 319, 0.3f, 337, 320.5f, -4.2f, 343);
                case 9: return V(320.5f, -4.2f, 343, 332, -4.2f, 348, 344, -4.2f, 358, 344, -4.2f, 362, 344, -4.2f, 380, 346, 0, 382);
                case 10: return V(346, 0, 383, 346, -1, 391, 340, -1, 390.5f, 350, -1, 404);
                default: return new Vector3[0];
            }
        }

        private static Vector3[] V(params float[] xyz)
        {
            var r = new Vector3[xyz.Length / 3];
            for (var i = 0; i < r.Length; i++) r[i] = new Vector3(xyz[i * 3], xyz[i * 3 + 1], xyz[i * 3 + 2]);
            return r;
        }
    }
}
