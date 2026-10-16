using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Editor.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// Baked reflection probes per section at the GDD §9.4 counts (S1 3, S2 4, S3 4 with 2 interior, S4 2, S5 3, S6 4,
    /// S7 1, S8 1, S9 0): 128 px, box-projected indoors; the three hero probes of the Relay Hall, screening room and
    /// basement are 256 px (the booth mirror is WP09's). WP10 Look's SectionBaker bakes them with the section.
    /// </summary>
    public static class ReflectionProbePlacer
    {
        private struct Def
        {
            public SectionId Section;
            public string Name;
            public Vector3 Center;
            public Vector3 Size;
            public bool Box;
            public int Resolution;
        }

        private static readonly List<Def> s_defs = new List<Def>
        {
            P(SectionId.Approach, "Ridge", 42f, 24f, 30f, 80f, 20f, 60f, false),
            P(SectionId.Approach, "Grove", 132f, 2.5f, 118f, 76f, 12f, 60f, false),
            P(SectionId.Approach, "Weighbridge", 183f, 2.5f, 150f, 60f, 14f, 40f, false),
            P(SectionId.Market, "MarketSouth", 185f, 3f, 186f, 10f, 12f, 36f, true),
            P(SectionId.Market, "MarketNorth", 185f, 3f, 226f, 10f, 12f, 42f, true),
            P(SectionId.Market, "SunsetLane", 150f, 3f, 231f, 60f, 10f, 12f, false),
            P(SectionId.Market, "Tinsmiths", 168.5f, 2.5f, 217f, 6f, 8f, 44f, true),
            P(SectionId.Square, "Square", 218f, 4f, 276f, 70f, 20f, 48f, false),
            P(SectionId.Square, "AlleyYard", 178f, 3f, 290f, 12f, 10f, 60f, true),
            P(SectionId.Square, "CinemaLobby", 192f, 2.2f, 307f, 11.6f, 4.4f, 10.4f, true),
            P(SectionId.Square, "Auditorium", 208.5f, 5.5f, 321f, 44.2f, 11f, 17.6f, true),
            P(SectionId.Roofs, "RoofsWest", 247f, 11f, 320f, 32f, 10f, 34f, false),
            P(SectionId.Roofs, "RoofsEast", 284f, 14f, 320f, 30f, 10f, 34f, false),
            P(SectionId.StationExt, "YardSouth", 330f, 3f, 305f, 60f, 14f, 30f, false),
            P(SectionId.StationExt, "YardNorth", 350f, 3f, 372f, 80f, 14f, 20f, false),
            P(SectionId.StationExt, "YardEast", 375f, 3f, 330f, 32f, 14f, 50f, false),
            P(SectionId.StationInt, "RelayHall", 331f, 2.4f, 340f, 29.2f, 4.2f, 19.2f, true, 256),
            P(SectionId.StationInt, "Screening", 323.2f, 6.7f, 355.8f, 13.2f, 3.8f, 11.6f, true, 256),
            P(SectionId.StationInt, "Basement", 332f, -2.4f, 347f, 28f, 3.6f, 26f, true, 256),
            P(SectionId.StationInt, "Office", 351f, 2.4f, 346f, 9.2f, 4.2f, 31.2f, true),
            P(SectionId.Pitch, "Pitch", 347f, 1f, 405f, 90f, 12f, 40f, false),
            P(SectionId.Heli, "Pad", 350f, 1.5f, 406f, 40f, 12f, 40f, false),
        };

        private static Def P(SectionId s, string name, float x, float y, float z, float sx, float sy, float sz, bool box, int res = 128) =>
            new Def { Section = s, Name = name, Center = new Vector3(x, y, z), Size = new Vector3(sx, sy, sz), Box = box, Resolution = res };

        /// <summary>Places the probes of the context's section; returns the count.</summary>
        public static int Place(SectionBuildContext ctx)
        {
            var n = 0;
            foreach (var d in s_defs)
            {
                if (d.Section != ctx.Section) continue;
                var go = new GameObject("RP_" + d.Name);
                go.transform.SetParent(ctx.Child(SectionBuildContext.GLights, "ReflectionProbes"), false);
                go.transform.position = d.Center;
                var p = go.AddComponent<ReflectionProbe>();
                p.mode = ReflectionProbeMode.Baked;
                p.resolution = d.Resolution;
                p.boxProjection = d.Box;
                p.size = d.Size;
                p.center = Vector3.zero;
                p.importance = d.Box ? 2 : 1;
                p.blendDistance = d.Box ? 0.5f : 2f;
                p.hdr = true;
                n++;
            }
            if (n > 0) ctx.Info("Reflection probes: " + n);
            return n;
        }
    }
}
