using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Look.Editor
{
    /// <summary>How one section (or one lightmap set of it) is baked (GDD §9.4, TECH_PLAN §3.8, D6, D7, D16).</summary>
    public sealed class SectionBakeSpec
    {
        /// <summary>Section.</summary>
        public SectionId Section;
        /// <summary>Set name ("BASE", "GOLDEN", "SUNSET", "DUSK", "NORMAL", "EMERGENCY").</summary>
        public string SetName = "BASE";
        /// <summary>LightStage value whose sun and sky the bake uses.</summary>
        public float StageS;
        /// <summary>BAKE_Sun elevation override (degrees; NaN = from the LightStage table at <see cref="StageS"/>).</summary>
        public float SunElevation = float.NaN;
        /// <summary>BAKE_Sun on (false = sky only).</summary>
        public bool SunOn = true;
        /// <summary>BAKE_Sun indirect (bounce) multiplier (S4_Roofs: 0, "sun bounce off").</summary>
        public float IndirectMultiplier = 1f;
        /// <summary>BAKE_Sun mode: Mixed / Baked Indirect for sections (D6), fully Baked for S9_TownFar (D7).</summary>
        public LightmapBakeType SunBakeType = LightmapBakeType.Mixed;
        /// <summary>Lightmap budget in 1024² equivalents (D16, GDD §9.4).</summary>
        public float CapEq;
        /// <summary>Reflection probes expected (GDD §9.4).</summary>
        public int ReflectionProbes;
        /// <summary>Starting texel density (texels per metre), lowered until the cap holds.</summary>
        public float TexelsPerMeter;
        /// <summary>Baked lights whose name contains this are disabled for this set (S6 NORMAL: "Emergency").</summary>
        public string DisableLightsContaining;
        /// <summary>Baked lights under an object whose name contains this are disabled for this set (S6 EMERGENCY: "BasementTungsten").</summary>
        public string DisableLightsUnder;
        /// <summary>Adds temporary baked sodium point lights at every <c>LAMP.*</c> marker (S5 yard sodium, GDD §9.4).</summary>
        public bool BakeLampMarkers;
        /// <summary>No sky light at all (underground booth).</summary>
        public bool EnvironmentOff;
        /// <summary>Maximum texture size of a copied alternate set (keeps the slot layout, cuts the download).</summary>
        public int AlternateMaxSize = 1024;
        /// <summary>Beats whose critical path gets light probes when the section has none (Appendix A knots).</summary>
        public int[] ProbeBeats = new int[0];
        /// <summary>Notes for the log.</summary>
        public string Notes;
    }
}
