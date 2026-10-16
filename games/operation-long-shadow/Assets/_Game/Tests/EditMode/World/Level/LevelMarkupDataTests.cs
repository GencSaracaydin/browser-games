using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Markup;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.World.Level
{
    /// <summary>Appendix A coverage of the Level data tables (TECH_PLAN Appendix A, GDD §6.7, §7.13).</summary>
    public sealed class LevelMarkupDataTests
    {
        private static MarkupIdTable Table() => MarkupIds.Load();

        [Test]
        public void EveryAppendixIdHasASectionExceptBoothAndInsertCameras()
        {
            var missing = new List<string>();
            foreach (var row in Table().Rows)
            {
                if (row.Id.EndsWith(".*") || row.Id.StartsWith("BOOTH.") || row.Id.StartsWith("INS.Cam")) continue;
                if (!MarkupSections.TryGetSection(row.Id, row.Beat, out _)) missing.Add(row.Id);
            }
            Assert.IsEmpty(missing, "ids without a section: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryGateHasABlockerAndTheRightStartState()
        {
            foreach (var row in Table().Rows)
            {
                if (row.Kind != "K") continue;
                var def = GateBlockers.Get(row.Id);
                Assert.IsNotNull(def, row.Id);
                Assert.AreEqual(row.Id.StartsWith("Block."), def.StartsOpen, row.Id + " start state (WP07: Gate.* closed, Block.* open)");
            }
        }

        [Test]
        public void EveryBlockerHasItsOutOfBoundsVolume()
        {
            foreach (var row in Table().Rows)
            {
                if (!row.Id.StartsWith("Block.")) continue;
                var oob = DerivedMarkers.Get("OOB." + row.Id);
                Assert.IsNotNull(oob, "OOB." + row.Id);
                Assert.AreEqual("OutOfBoundsVolume", oob.Component);
                Assert.Greater(oob.BoxSize.x * oob.BoxSize.y * oob.BoxSize.z, 0.1f);
            }
        }

        [Test]
        public void EveryClosetHasDoorAndTwoSpawnsAndStairWaveDistance()
        {
            foreach (var row in Table().Rows)
            {
                if (row.Kind != "C") continue;
                var c = ClosetLayout.Get(row.Id);
                Assert.IsNotNull(c, row.Id);
                Assert.GreaterOrEqual(c.Spawns.Length, 2, row.Id);
            }
            foreach (var s in ClosetLayout.Get("SC_B7_UP").Spawns)
                Assert.GreaterOrEqual(Vector3.Distance(s, new Vector3(346f, 0.3f, 350f)), 15f, "WP04: SC_B7_UP spawns ≥ 15 m from the office door");
        }

        [Test]
        public void CheckpointNodeSetsFollowMission()
        {
            Assert.AreEqual("B1.Squad.Start", MarkupDefaults.CheckpointNodeSet("CP1"));
            Assert.AreEqual("B3.NodeSet.M3", MarkupDefaults.CheckpointNodeSet("CP3b"));
            Assert.AreEqual("B9.NodeSet.Defend", MarkupDefaults.CheckpointNodeSet("CP9"));
            Assert.AreEqual(string.Empty, MarkupDefaults.CheckpointNodeSet("CP2"));
        }

        [Test]
        public void InteractionIdsAreTheFourWp07Prompts()
        {
            foreach (var id in new[] { "B4.SideDoor", "B6.HallDoor", "B8.Projector", "B10.HeliDoor" }) Assert.IsTrue(MarkupPlacer.IsInteraction(id), id);
            Assert.IsFalse(MarkupPlacer.IsInteraction("B7.Ledgers"));
        }

        [Test]
        public void DerivedIdsAreUniqueAndLampsMatchLayout()
        {
            var seen = new HashSet<string>();
            foreach (var d in DerivedMarkers.All)
                if (!string.IsNullOrEmpty(d.Id)) Assert.IsTrue(seen.Add(d.Id), "duplicate derived id " + d.Id);
            Assert.AreEqual(38, LampLayout.All.Count, "GDD §8.5: 38 sodium heads");
            foreach (var l in LampLayout.All) Assert.IsNotNull(DerivedMarkers.Get(l.Id), l.Id);
        }

        [Test]
        public void MastLightsMatchLookRigHeights()
        {
            var want = new[] { 90f, 72f, 54f, 36f, 18f };
            for (var i = 0; i < want.Length; i++)
                Assert.AreEqual(want[i], DerivedMarkers.Get("MAST.AvLight" + (i + 1)).Position.y, 1e-3f);
        }

        [Test]
        public void InteriorEyeAdaptationZonesExist()
        {
            var ev = new Dictionary<string, float>();
            foreach (var z in ZoneVolumes.All)
                if (z.EyeAdaptation) ev[z.Profile] = z.Ev;
            Assert.AreEqual(1.2f, ev["VP_Interior_Cinema"], 1e-4f);
            Assert.AreEqual(0.8f, ev["VP_Interior_Hall"], 1e-4f);
            Assert.AreEqual(1.0f, ev["VP_Interior_Screening"], 1e-4f);
            Assert.AreEqual(1.0f, ev["VP_Interior_Basement"], 1e-4f);
        }

        [Test]
        public void SeamsJoinDifferentSectionsAndLinksStayInside()
        {
            foreach (var s in NavSeams.All) Assert.AreNotEqual(s.Owner, s.Other, s.Name);
            foreach (var l in NavLinks.All) Assert.AreEqual(l.Owner, l.Other, l.Name);
        }

        [Test]
        public void OverridesStayCloseToAppendixA()
        {
            var t = Table();
            foreach (var o in MarkupOverrides.All)
            {
                Assert.IsTrue(t.TryGet(o.Id, out var row), o.Id);
                Assert.Less(Vector3.Distance(row.Position, o.Position), 3f, o.Id + " moved too far");
                Assert.IsFalse(string.IsNullOrEmpty(o.Reason));
            }
        }
    }
}
