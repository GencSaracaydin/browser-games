using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Objectives;
using LongShadow.Mission.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>ObjectiveService rules (GDD §7.12, §11): markers, counters, optional intel within 6 m, completed objectives stay done.</summary>
    public class ObjectiveServiceTests
    {
        private Vector3 _player;
        private readonly System.Collections.Generic.HashSet<string> _dead = new System.Collections.Generic.HashSet<string>();

        [TearDown]
        public void TearDown() => GameServices.Clear();

        private ObjectiveService Make() => new ObjectiveService(() => _player, id => _dead.Contains(id));

        [Test]
        public void ShowAddsTheObjectiveAndItsMarkersAtTheirFallbacks()
        {
            var s = Make();
            Assert.IsTrue(s.Show(ObjectiveTable.B1Observe));
            Assert.That(s.Active.Count, Is.EqualTo(1));
            Assert.That(s.Active[0].Text, Is.EqualTo("OBSERVE ZAROUD 0/3"));
            Assert.That(s.Markers.Count, Is.EqualTo(3), "hollow diamonds on POI1-3");
            Assert.That(s.Markers[0].Kind, Is.EqualTo(MarkerKind.DiamondHollow));
            Assert.That(s.Markers[0].WorldPosition, Is.EqualTo(new Vector3(378f, 45f, 362f)), "Appendix A fallback until the marker resolves");
            s.SetText(ObjectiveTable.B1Observe, ObjectiveFormat.WithCounter(s.Active[0].Text, 1, 3));
            Assert.That(s.TextOf(ObjectiveTable.B1Observe), Is.EqualTo("OBSERVE ZAROUD 1/3"));
        }

        [Test]
        public void CompletedObjectivesNeverComeBack()
        {
            var s = Make();
            s.Show(ObjectiveTable.B4Flank);
            s.Complete(ObjectiveTable.B4Flank);
            Assert.That(s.Active.Count, Is.EqualTo(0));
            Assert.That(s.Markers.Count, Is.EqualTo(0), "its marker goes with it");
            Assert.IsFalse(s.Show(ObjectiveTable.B4Flank), "a resumed beat cannot re-show it");
            Assert.That(s.CompletionOfBeat(4), Is.EqualTo(0.25f));
        }

        [Test]
        public void OptionalIntelIsUnlistedAndShowsOnlyWithin6m()
        {
            var s = Make();
            _player = new Vector3(0f, 0f, 0f);
            s.Show(ObjectiveTable.B7O1);
            s.Tick(0f);
            Assert.That(s.Active.Count, Is.EqualTo(0), "optional intel is not in the objective list");
            Assert.That(s.Markers.Count, Is.EqualTo(0), "far away: no icon");
            _player = new Vector3(341f, 1.2f, 355f);
            s.Tick(1f);
            Assert.That(s.Markers.Count, Is.EqualTo(1), "within 6 m: the intel icon shows");
            Assert.That(s.Markers[0].Kind, Is.EqualTo(MarkerKind.Intel));
            Assert.IsTrue(s.CollectIntel(IntelTable.Schedule));
            Assert.That(s.OptionalCount, Is.EqualTo(1));
            Assert.That(s.IntelCount, Is.EqualTo(0));
            Assert.That(s.Markers.Count, Is.EqualTo(0));
            Assert.IsFalse(s.CollectIntel(IntelTable.Schedule), "only once");
        }

        [Test]
        public void EnemyMarkersDropWhenTheTargetDies()
        {
            var s = Make();
            s.Show(ObjectiveTable.B4Technical);
            Assert.That(s.Markers.Count, Is.EqualTo(1));
            Assert.IsTrue(s.Markers[0].ThroughWalls, "red, through walls");
            _dead.Add(EncounterIds.TechnicalGunner);
            s.Tick(1f);
            Assert.That(s.Markers.Count, Is.EqualTo(0));
        }

        [Test]
        public void DropBeforeRemovesStaleObjectivesButKeepsLiveOptionalIntel()
        {
            var s = Make();
            s.Show(ObjectiveTable.B7Upstairs);
            s.Show(ObjectiveTable.B7O1);
            s.DropBefore(8);
            Assert.That(s.Active.Count, Is.EqualTo(0));
            Assert.IsTrue(s.IsActive(ObjectiveTable.B7O1), "O1 stays collectable while the Hall is loaded");
            s.DropBefore(10);
            Assert.IsFalse(s.IsActive(ObjectiveTable.B7O1), "gone when S6 unloads");
        }

        [Test]
        public void IntelCompletesItsObjectiveAndCounts()
        {
            var s = Make();
            s.Show(ObjectiveTable.B7Ledgers);
            Assert.IsTrue(s.CollectIntel(IntelTable.Ledgers));
            Assert.IsTrue(s.IsComplete(ObjectiveTable.B7Ledgers));
            Assert.That(s.IntelCount, Is.EqualTo(1));
            Assert.That(IntelTable.Get(IntelTable.Ledgers).Counter(1), Is.EqualTo("INTEL 1/3"));
            Assert.That(IntelTable.Get(IntelTable.FilmLabel).Counter(2), Is.EqualTo("OPTIONAL 2/2"));
        }

        [Test]
        public void SaveRoundTrip()
        {
            var s = Make();
            s.Show(ObjectiveTable.B9Defend);
            s.Complete(ObjectiveTable.B9Defend);
            s.Show(ObjectiveTable.B9Transmitter, "DESTROY THE TRANSMITTER 2/4");
            s.CollectIntel(IntelTable.Codebook);
            var rec = new SaveRecord { Key = ObjectiveService.Key };
            s.Capture(new SaveWriter(rec));
            var t = Make();
            t.Restore(new SaveReader(rec));
            Assert.IsTrue(t.IsComplete(ObjectiveTable.B9Defend));
            Assert.IsTrue(t.HasIntel(IntelTable.Codebook));
            Assert.That(t.TextOf(ObjectiveTable.B9Transmitter), Is.EqualTo("DESTROY THE TRANSMITTER 2/4"));
        }

        [Test]
        public void SaveCodecRoundTrips()
        {
            var list = new System.Collections.Generic.List<string>();
            Assert.That(SaveCodec.Split(SaveCodec.Join(new[] { "a", "", "B7.O1", null, "x.y" }), list), Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { "a", "B7.O1", "x.y" }, list);
            Assert.That(SaveCodec.Split("", list), Is.EqualTo(0));
        }
    }
}
