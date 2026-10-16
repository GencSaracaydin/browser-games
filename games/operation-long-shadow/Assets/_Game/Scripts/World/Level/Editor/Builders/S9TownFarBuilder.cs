using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Editor.Markup;
using LongShadow.World.Level.Editor.Proxy;
using LongShadow.World.Level.Geometry;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// S9_TownFar (GDD §6.7, TECH_PLAN D7; never unloaded): the far town (paired <see cref="FarProxyBlock"/>s, the dense
    /// housing, distant lamp sprites), the backdrop (ridge silhouette, far hills, desert, salt pans), the 90 m guyed mast
    /// (BLD-12, real geometry: it is seen from every section; maintenance platform at +25 under B5.Perch.Mast, aviation light
    /// housings at the MAST.AvLight heights), the <see cref="CriticalPath"/> service from Appendix A's knots and the
    /// <see cref="ProxyBlockDirector"/>.
    /// </summary>
    public sealed class S9TownFarBuilder : SectionBuilderBase
    {
        /// <inheritdoc/>
        public override SectionId Section => SectionId.TownFar;

        /// <inheritdoc/>
        public override void Geometry(SectionBuildContext ctx)
        {
            FarProxyBuilder.Build(ctx);
            BackdropBuilder.Build(ctx.Child(SectionBuildContext.GArchitecture, "Backdrop"), LevelPaths.Backdrop);
            Mast(ctx);
        }

        /// <inheritdoc/>
        public override void Wire(SectionBuildContext ctx)
        {
            var go = new GameObject("CriticalPathService");
            go.transform.SetParent(ctx.Group(SectionBuildContext.GMarkup), false);
            var cp = go.AddComponent<CriticalPath>();
            var beats = new Dictionary<int, Vector3[]>();
            for (var b = 1; b <= CriticalPathTable.BeatCount; b++)
            {
                var k = CriticalPathTable.Knots(b);
                if (k != null) beats[b] = k;
            }
            cp.Configure(beats);
            var director = new GameObject("ProxyBlockDirector");
            director.transform.SetParent(ctx.Root.transform, false);
            director.AddComponent<ProxyBlockDirector>();
            ctx.Info("CriticalPath: " + beats.Count + " beats; ProxyBlockDirector installed.");
        }

        private static void Mast(SectionBuildContext ctx)
        {
            var foot = DerivedMarkers.MastBase;
            var mb = new MeshBuilder();
            KitIndustrial.LatticeMast(mb, foot, DerivedMarkers.MastHeight, 4f, 1.2f, DerivedMarkers.MastPlatformHeight);
            // Guy anchors at 120° and wires to two heights (GDD BLD-12 "guyed").
            for (var i = 0; i < 3; i++)
            {
                var dir = MathX.DirectionFromBearing(30f + i * 120f);
                var anchor = foot + dir * 38f;
                KitIndustrial.GuyAnchor(mb, anchor);
                foreach (var h in new[] { 40f, 75f })
                    KitRoofProps.Cable(mb, anchor + Vector3.up * 0.4f, foot + Vector3.up * h + dir * 0.6f, 0.4f, 0.016f, KitSlots.Cable, 6);
            }
            // Aviation light housings at the MAST.AvLight heights (WP10 Look's PF_LookRig carries the blinking lenses).
            mb.Slot(KitSlots.AviationLens);
            for (var i = 0; i < 5; i++)
            {
                var y = DerivedMarkers.MastHeight - i * DerivedMarkers.AvLightSpacing;
                mb.AddCylinder(foot + new Vector3(0f, y - 0.15f, 0f), 0.16f, 0.3f, 8, true, false);
            }
            // The marksman's platform floor collider (B5.Perch.Mast at +25).
            mb.AddCollider(foot + Vector3.up * (DerivedMarkers.MastPlatformHeight - 0.05f), new Vector3(3.4f, 0.1f, 3.4f), SurfaceType.Metal);
            ctx.Mesh("Mast", ctx.Group(SectionBuildContext.GArchitecture), Vector3.zero, Quaternion.identity, mb, WorldConst.LightmapScaleHouse * 0.5f);
        }
    }
}
