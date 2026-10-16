using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Proxy;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.World.Level
{
    /// <summary>Sun geometry, terrain knots, vehicle timings and the building layout (GDD §6.5, §7.1, §7.3, §7.10–7.11, §8.5).</summary>
    public sealed class LevelGeometryDataTests
    {
        [Test]
        public void SunsetLaneBandCrossesTheMarketAt238()
        {
            Assert.AreEqual(238.85f, SunGeometry.SunsetLaneBandZ(185f), 0.05f);
        }

        [Test]
        public void WestRoofShadeLineIsAboutSixMetres()
        {
            Assert.AreEqual(6.14f, SunGeometry.ShadeLineOnFarWall(7f, 8f, 6f), 0.05f);
        }

        [Test]
        public void RidgeCrestHidesTheSunAtMidA()
        {
            var crest = SunGeometry.MidARidgeCrest();
            Assert.AreEqual(24.56f, crest, 0.05f);
            for (var az = 255f; az <= 261f; az += 0.5f) Assert.AreEqual(crest, BackdropBuilder.RidgeCrest(az, null), 1e-3f, "az " + az);
            for (var az = 246f; az <= 270f; az += 1f) Assert.GreaterOrEqual(BackdropBuilder.RidgeCrest(az, null), crest * 0.7f - 1e-3f, "az " + az);
            var ridge = DerivedMarkers.Get("B5.Ridge");
            Assert.AreEqual(crest, ridge.Position.y, 1e-3f);
        }

        [Test]
        public void ApproachTerrainCarriesTheBeatOneKnots()
        {
            var expected = new[] { new Vector3(42f, 22f, 30f), new Vector3(70f, 18f, 50f), new Vector3(82f, 13f, 62f), new Vector3(90f, 8f, 72f), new Vector3(98f, 4f, 82f), new Vector3(108f, 0f, 96f) };
            foreach (var k in expected) Assert.AreEqual(k.y, ApproachHeightField.Height(k.x, k.z), 0.35f, k.ToString());
        }

        [Test]
        public void VehicleTimingsMatchTheBeats()
        {
            Assert.AreEqual(WorldConst.ExfilSeconds, VehicleRoutes.Get(VehicleRoutes.Exfil).Duration, 1e-3f);
            var approach = VehicleRoutes.Get(VehicleRoutes.HeliApproach);
            Assert.AreEqual(15f, approach.Duration - approach.KnotTime(VehicleRoutes.ApproachFinalKnot), 1e-3f);
            Assert.AreEqual(20f, VehicleRoutes.Get(VehicleRoutes.HeliWaveOff).Duration, 1e-3f);
            Assert.Less(Mathf.Abs(VehicleRoutes.Get(VehicleRoutes.Truck1).Duration - 4f), 0.7f, "WP04: about 4 s");
        }

        [Test]
        public void CatalogueBuildingsDoNotOverlap()
        {
            var all = BuildingCatalog.All;
            for (var i = 0; i < all.Count; i++)
            for (var j = i + 1; j < all.Count; j++)
            {
                if (!Mathf.Approximately(all[i].Yaw, 0f) || !Mathf.Approximately(all[j].Yaw, 0f)) continue;
                Assert.IsFalse(all[i].Footprint.Overlaps(all[j].Footprint, 0.05f), all[i].Id + " overlaps " + all[j].Id);
            }
        }

        [Test]
        public void DenseHousingKeepsSunsetLaneAndTargetsExist()
        {
            var targets = 0;
            foreach (var h in DenseHousing.All)
            {
                Assert.IsFalse(DenseHousing.CrossesSunsetLane(h.Footprint) && !h.Id.StartsWith("BLD-15.T"), h.Id + " crosses Sunset Lane");
                if (h.Id.StartsWith("BLD-15.T")) targets++;
            }
            Assert.AreEqual(3, targets, "B11 target houses T2-T4");
        }

        [Test]
        public void FloorRelativeClosetsAndGatesLieInTheirSections()
        {
            foreach (var c in ClosetLayout.All.Values)
            {
                Assert.IsTrue(MarkupSections.TryGetSection(c.Id, null, out var s), c.Id);
                Assert.IsTrue(SectionLayout.Contains(s, c.Door) || s == LongShadow.Core.SectionId.Pitch, c.Id + " door outside " + s);
            }
        }
    }
}
