using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Editor.Vehicles;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// S8_Heli (GDD §6.7, §7.10–7.11): Magpie with its cabin and HELI.* markers on the shared root of B10.HeliApproach
    /// (poses at the approach start), B10.HeliWaveOff and B11.Exfil; the two Beat 11 square trucks on B11.TruckSquare
    /// (B11.Truck1/2); the burnt technical copy at B11.FireProp (shown from B11.ExfilStart so it never doubles S3's).
    /// </summary>
    public sealed class S8HeliBuilder : SectionBuilderBase
    {
        /// <inheritdoc/>
        public override SectionId Section => SectionId.Heli;

        /// <inheritdoc/>
        public override void Geometry(SectionBuildContext ctx)
        {
        }

        /// <inheritdoc/>
        public override void Wire(SectionBuildContext ctx)
        {
            var magpie = VehicleBuilder.Magpie(ctx, out var cabin);
            var seats = new Transform[4];
            var names = new[] { "HELI.PlayerEye", "HELI.Hollis", "HELI.Sparrow", "HELI.Tinker" };
            for (var i = 0; i < names.Length; i++)
                seats[i] = ctx.Markers.TryGetValue(names[i], out var m) ? m.transform : cabin;
            VehicleBuilder.Attach(ctx, VehicleRoutes.HeliApproach, magpie.transform, seats, true, true, false);
            VehicleBuilder.Attach(ctx, VehicleRoutes.HeliWaveOff, magpie.transform, seats, true, false, false);
            VehicleBuilder.Attach(ctx, VehicleRoutes.Exfil, magpie.transform, seats, true, false, false);
            VehicleBuilder.SquareTrucks(ctx);

            // The burnt technical under B11.FireProp (WP10 Look binds the lit fire to the marker).
            var holder = new GameObject("BurntTechnical");
            holder.transform.SetParent(ctx.Group(SectionBuildContext.GDynamic), false);
            var tech = LevelProps.Place("Technical", holder.transform, new Vector3(232f, 0f, 286f), 234f, 1f, "Technical_Burnt", true, true);
            tech.SetActive(false);
            holder.AddComponent<MissionEventSwitch>().Configure("S8.BurntTechnical", LevelEvents.ExfilStart, string.Empty, new[] { tech }, null, false, string.Empty);
        }
    }
}
