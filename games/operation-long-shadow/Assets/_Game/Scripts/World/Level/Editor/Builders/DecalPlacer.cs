using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// URP decal projectors per section (WP10_CC0 decal materials <c>Art/Materials/Decals/M_Decal_*</c>): tyre tracks on the
    /// Weighbridge track and the Customs Road, leaks and grime on the market facades, posters, chalk on Tinsmiths' Lane,
    /// the scorch under the technical, the STAND CLEAR ring (B6.StandClear, GDD A10), the painted company lettering on the
    /// Relay Hall (BLD-10), asphalt damage on the road, tape and scratches inside the Hall. A missing material skips the
    /// decal (logged once). Ground decals project down, wall decals into the wall.
    /// </summary>
    public static class DecalPlacer
    {
        private struct Def
        {
            public SectionId Section;
            public string Material;
            public Vector3 Position;
            public Vector3 Into;
            public float Yaw;
            public Vector3 Size;
        }

        private static readonly List<Def> s_defs = new List<Def>
        {
            G(SectionId.Approach, "M_Decal_TireTracks", 183f, 0.05f, 150f, 0f, 3.2f, 14f),
            G(SectionId.Approach, "M_Decal_TireTracks", 189.2f, 0.05f, 140f, 160f, 3.2f, 10f),
            W(SectionId.Market, "M_Decal_Leaks", 189f, 6.2f, 192f, 270f, 2.2f, 3.4f),
            W(SectionId.Market, "M_Decal_Leaks", 189f, 6.6f, 211f, 270f, 2.2f, 3.4f),
            W(SectionId.Market, "M_Decal_Leaks", 181f, 4.6f, 222f, 90f, 2.0f, 3.0f),
            W(SectionId.Market, "M_Decal_Grime", 181f, 0.6f, 186f, 90f, 4f, 1.2f),
            W(SectionId.Market, "M_Decal_Grime", 189f, 0.6f, 214f, 270f, 4f, 1.2f),
            W(SectionId.Market, "M_Decal_PosterArt", 181f, 1.9f, 199.4f, 90f, 0.9f, 1.3f),
            W(SectionId.Market, "M_Decal_PosterArt", 189f, 1.9f, 238.6f, 270f, 0.9f, 1.3f),
            W(SectionId.Market, "M_Decal_ChalkDrawings", 171f, 0.9f, 220f, 270f, 2.4f, 1.2f),
            G(SectionId.Square, "M_Decal_Scorch", 232f, 0.05f, 286f, 234f, 5f, 6.5f),
            G(SectionId.Square, "M_Decal_TireTracks", 244f, 0.05f, 280f, 250f, 3.2f, 16f),
            W(SectionId.Square, "M_Decal_PosterArt", 197.8f, 1.8f, 306f, 270f, 0.9f, 1.3f),
            W(SectionId.Square, "M_Decal_Leaks", 252f, 5.4f, 266f, 270f, 2.2f, 3.2f),
            G(SectionId.Roofs, "M_Decal_StandClearRing", 296f, 0.05f, 310f, 0f, 5.2f, 5.2f),
            W(SectionId.Roofs, "M_Decal_Leaks", 292f, 7.4f, 312f, 90f, 2.2f, 3.6f),
            W(SectionId.StationExt, "M_Decal_CompanyLettering", 338f, 5.9f, 330f, 180f, 9f, 1.4f),
            G(SectionId.StationExt, "M_Decal_AsphaltDamage", 330f, 0.05f, 286f, 0f, 6f, 6f),
            G(SectionId.StationExt, "M_Decal_AsphaltDamage", 372f, 0.05f, 287f, 90f, 5f, 5f),
            G(SectionId.StationExt, "M_Decal_Grime", 372f, 0.05f, 332f, 0f, 4f, 7f),
            G(SectionId.StationInt, "M_Decal_Scratches", 331f, 0.32f, 345f, 0f, 6f, 6f),
            W(SectionId.StationInt, "M_Decal_Tape", 330f, 6.0f, 351.2f, 90f, 1.2f, 0.4f),
            W(SectionId.StationInt, "M_Decal_Fingerprints", 345.9f, -3.4f, 342f, 270f, 0.5f, 0.5f),
            G(SectionId.Pitch, "M_Decal_TireTracks", 394f, -0.95f, 396f, 90f, 3.2f, 30f),
            G(SectionId.Pitch, "M_Decal_TireTracks", 362f, -0.95f, 440f, 0f, 3.2f, 30f),
        };

        private static Def G(SectionId s, string mat, float x, float y, float z, float yaw, float w, float l) =>
            new Def { Section = s, Material = mat, Position = new Vector3(x, y, z), Into = Vector3.down, Yaw = yaw, Size = new Vector3(w, l, 1f) };

        private static Def W(SectionId s, string mat, float x, float y, float z, float outward, float w, float h) =>
            new Def { Section = s, Material = mat, Position = new Vector3(x, y, z), Into = -MathX.DirectionFromBearing(outward), Yaw = outward, Size = new Vector3(w, h, 0.6f) };

        /// <summary>Places the decals of the context's section; returns the count.</summary>
        public static int Place(SectionBuildContext ctx)
        {
            var n = 0;
            foreach (var d in s_defs)
            {
                if (d.Section != ctx.Section) continue;
                var path = LevelPaths.DecalMaterials + "/" + d.Material + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    LSLog.WarnOnce("decal:" + d.Material, LogCategory.Level, "Decal material missing (WP10_CC0 import): " + path);
                    continue;
                }
                var go = new GameObject("Decal_" + d.Material.Substring(8));
                go.transform.SetParent(ctx.Child(SectionBuildContext.GProps, "Decals"), false);
                var up = d.Into == Vector3.down ? MathX.DirectionFromBearing(d.Yaw) : Vector3.up;
                go.transform.SetPositionAndRotation(d.Position, Quaternion.LookRotation(d.Into, up));
                var p = go.AddComponent<DecalProjector>();
                p.material = mat;
                p.size = d.Size;
                p.pivot = new Vector3(0f, 0f, d.Size.z * 0.5f);
                p.drawDistance = 120f;
                p.fadeScale = 0.9f;
                n++;
            }
            if (n > 0) ctx.Info("Decals: " + n);
            return n;
        }
    }
}
