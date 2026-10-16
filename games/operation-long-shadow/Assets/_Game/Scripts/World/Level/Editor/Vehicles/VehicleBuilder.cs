using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Editor.Markup;
using LongShadow.World.Level.Geometry;
using UnityEngine;
using UnityEngine.Splines;

namespace LongShadow.World.Level.Editor.Vehicles
{
    /// <summary>
    /// Builds the spline vehicles (TECH_PLAN §7.15, D11): Magpie (the Huey-class helicopter, VEH_HELICOPTER with spinning
    /// rotors or a greybox) with its cabin, the cabin-local HELI.* markers (cabin floor 1 m above the skids) and the Cabin
    /// ZoneTag, shared by B10.HeliApproach, B10.HeliWaveOff and B11.Exfil; the Beat 10 trucks (seat 0 = tailgate, seats 1–3
    /// on the bed; WP04); the Beat 11 square trucks (B11.Truck1/2: markers + WP03 Destructibles, 20 hits). Each route's
    /// <see cref="SplineVehicle"/> sits on its Appendix A spline marker (ISplineVehicle found through the MarkerRegistry).
    /// Vehicles are not static and use light probes.
    /// </summary>
    public static class VehicleBuilder
    {
        /// <summary>Main rotor hub in the VEH_HELICOPTER model (manifest note).</summary>
        public static readonly Vector3 MainHub = new Vector3(0.004f, 4.537f, 2.121f);
        /// <summary>Tail rotor hub in the VEH_HELICOPTER model.</summary>
        public static readonly Vector3 TailHub = new Vector3(-0.519f, 3.992f, -5.592f);

        /// <summary>Builds Magpie under Vehicles and returns its root and cabin.</summary>
        public static GameObject Magpie(SectionBuildContext ctx, out Transform cabin)
        {
            var root = new GameObject("Magpie");
            root.transform.SetParent(ctx.Group(SectionBuildContext.GVehicles), false);
            var model = LevelProps.Place("Helicopter", root.transform, Vector3.zero, 0f, 1f, "Body", false, false);
            model.transform.localPosition = Vector3.zero;
            LevelObjects.SetLayer(root, LayerIds.Vehicle);
            var main = LevelProps.Place("HeliMainRotor", root.transform, MainHub, 0f, 1f, "MainRotor", false, false);
            main.transform.localPosition = MainHub;
            main.AddComponent<RotorSpin>().Configure(Vector3.up, 324f, main, null);
            var tail = LevelProps.Place("HeliTailRotor", root.transform, TailHub, 0f, 1f, "TailRotor", false, false);
            tail.transform.localPosition = TailHub;
            tail.AddComponent<RotorSpin>().Configure(Vector3.right, 1660f, tail, null);

            cabin = new GameObject("Cabin").transform;
            cabin.SetParent(root.transform, false);
            cabin.localPosition = new Vector3(0f, VehicleRoutes.HeliCabinFloorHeight, 0f);
            CabinInterior(ctx, cabin);
            foreach (var row in ctx.Markup.Rows)
            {
                if (!row.Id.StartsWith("HELI.") || !row.HasPosition) continue;
                var m = MarkupPlacer.Add<SceneMarker>(ctx, MarkupPlacer.SubMarkers, row.Id, Vector3.zero, 0f, row.Notes);
                m.transform.SetParent(cabin, false);
                m.transform.localPosition = row.Position;
                m.transform.localRotation = Quaternion.identity;
            }
            // Cabin ZoneTag (WP06: rotor bed, weapon tails) riding with the helicopter.
            var zone = new GameObject("Zone_Cabin");
            zone.transform.SetParent(cabin, false);
            zone.transform.localPosition = new Vector3(0f, 0.95f, 0.2f);
            zone.layer = LayerIds.Trigger;
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.7f, 1.9f, 3.4f);
            var tag = zone.AddComponent<ZoneTag>();
            tag.SetZone(ZoneType.Cabin, 10);
            tag.Configure(true, false, true);
            LevelObjects.ClearStatic(root);
            return root;
        }

        private static void CabinInterior(SectionBuildContext ctx, Transform cabin)
        {
            // A simple cabin kit inside the hull: floor plate, rear bench, door-gun post, red cabin lamp housing.
            var mb = new MeshBuilder();
            mb.Slot(KitSlots.SteelPlate);
            mb.AddBox(new Vector3(0f, -0.04f, 0.2f), new Vector3(2.4f, 0.08f, 3.2f));
            mb.Slot(KitSlots.Canvas);
            mb.AddBox(new Vector3(0f, 0.42f, -1.0f), new Vector3(2.2f, 0.1f, 0.5f));
            mb.AddBox(new Vector3(0f, 0.75f, -1.25f), new Vector3(2.2f, 0.6f, 0.08f));
            mb.Slot(KitSlots.SteelFrame);
            mb.AddCylinder(new Vector3(-1.05f, 0f, 0.3f), 0.04f, 1.05f, 6);
            mb.AddBox(new Vector3(0f, 1.92f, 0f), new Vector3(0.18f, 0.06f, 0.18f));
            var go = ctx.Mesh("Magpie_Cabin", cabin, cabin.position, cabin.rotation, mb, 0f, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            LevelObjects.ClearStatic(go);
        }

        /// <summary>Adds and configures the SplineVehicle of a route on its spline marker.</summary>
        public static SplineVehicle Attach(SectionBuildContext ctx, string routeId, Transform root, Transform[] seats, bool aircraft, bool poseOnAwake, bool hideUntilPlay)
        {
            var route = VehicleRoutes.Get(routeId);
            var marker = MarkupPlacer.SplineMarker(ctx, routeId, null);
            if (route == null || marker == null)
            {
                ctx.Warn("Cannot attach vehicle to route " + routeId);
                return null;
            }
            var sv = marker.GetComponent<SplineVehicle>() ?? marker.AddComponent<SplineVehicle>();
            float? heading = float.IsNaN(route.FinalHeading) ? (float?)null : route.FinalHeading;
            sv.Configure(routeId, marker.GetComponent<SplineContainer>(), root, seats, route.SegmentSeconds, route.Speed, route.ArrivalEase > 0f, heading,
                route.RootOffsetLocal, aircraft, string.Empty, hideUntilPlay, poseOnAwake);
            if (route.FollowerSpacing != null && route.FollowerSpacing.Length > 0) ctx.Info(routeId + ": followers expected (" + route.FollowerSpacing.Length + ").");
            if (poseOnAwake && root != null)
            {
                var spline = marker.GetComponent<SplineContainer>().Spline;
                var p0 = (Vector3)spline.EvaluatePosition(0f);
                var t0 = (Vector3)spline.EvaluateTangent(0f);
                t0.y = aircraft ? t0.y : 0f;
                root.SetPositionAndRotation(p0 + Quaternion.LookRotation(t0.sqrMagnitude > 1e-6f ? t0.normalized : Vector3.forward, Vector3.up) * route.RootOffsetLocal,
                    Quaternion.LookRotation(t0.sqrMagnitude > 1e-6f ? t0.normalized : Vector3.forward, Vector3.up));
            }
            return sv;
        }

        /// <summary>A cargo truck root with seats (0 = tailgate on the ground, 1–3 on the bed).</summary>
        public static GameObject Truck(SectionBuildContext ctx, string name, string key, out Transform[] seats)
        {
            var root = new GameObject(name);
            root.transform.SetParent(ctx.Group(SectionBuildContext.GVehicles), false);
            var body = LevelProps.Place(key, root.transform, Vector3.zero, 0f, 1f, "Body", false, true);
            body.transform.localPosition = Vector3.zero;
            LevelObjects.SetLayer(root, LayerIds.Vehicle);
            seats = new Transform[4];
            var local = new[] { new Vector3(0f, 0f, -4.1f), new Vector3(-0.65f, 1.35f, -1.6f), new Vector3(0.65f, 1.35f, -2.3f), new Vector3(0f, 1.35f, -3.0f) };
            for (var i = 0; i < seats.Length; i++)
            {
                seats[i] = new GameObject("Seat" + i).transform;
                seats[i].SetParent(root.transform, false);
                seats[i].localPosition = local[i];
                seats[i].localRotation = i == 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.Euler(0f, i == 1 ? 270f : 90f, 0f);
            }
            LevelObjects.ClearStatic(root);
            return root;
        }

        /// <summary>The two Beat 11 square trucks on B11.TruckSquare (markers, WP03 Destructibles, follower spacing 12 m).</summary>
        public static void SquareTrucks(SectionBuildContext ctx)
        {
            var lead = Truck(ctx, VehicleRoutes.SquareTruck1Id, "CargoTruck", out var seats);
            var follow = Truck(ctx, VehicleRoutes.SquareTruck2Id, "CargoTruckTarpOff", out _);
            foreach (var t in new[] { lead, follow })
            {
                var m = t.AddComponent<SceneMarker>();
                m.SetId(t.name);
                m.SetNotes("Beat 11 square truck (Docs/REQUESTS.md WP07 -> WP10: kind D, WP03 Destructible Vehicle, 20 hits).");
                ctx.Markers[t.name] = t;
                SoftComponents.AddDestructible(t, t.name, SoftDestructibleKind.Vehicle, 400f, 20, 0f, t.transform, false, null, null, null, null);
            }
            var sv = Attach(ctx, VehicleRoutes.TruckSquare, lead.transform, seats, false, true, true);
            var route = VehicleRoutes.Get(VehicleRoutes.TruckSquare);
            if (sv != null) sv.SetFollowers(new[] { follow.transform }, route.FollowerSpacing);
            // Park the follower behind the lead at the start so the scene reads correctly before Play.
            follow.transform.SetPositionAndRotation(lead.transform.position - lead.transform.forward * route.FollowerSpacing[0], lead.transform.rotation);
        }
    }
}
