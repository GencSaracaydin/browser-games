using System.Collections.Generic;
using System.Text.RegularExpressions;
using LongShadow.Core;
using LongShadow.Core.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LongShadow.Tests.EditMode.Core
{
    public class MarkupTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) Object.DestroyImmediate(o);
            _objects.Clear();
            MarkerRegistry.ResetForTests();
            LSLog.ResetOnce();
        }

        private T Make<T>(string id, Vector3 pos) where T : SceneMarker
        {
            var go = new GameObject(id);
            _objects.Add(go);
            go.transform.position = pos;
            var m = go.AddComponent<T>();
            m.SetId(id);
            return m;
        }

        [Test]
        public void RegistryFindsByIdAndType()
        {
            MarkerRegistry.ResetForTests();
            var reg = MarkerRegistry.Instance;
            var a = Make<SceneMarker>("B4.Technical", new Vector3(232, 1.6f, 286));
            var cp = Make<CheckpointMarker>("CP3b", new Vector3(185, 0, 216));
            var cover = Make<CoverNode>("", Vector3.zero);
            reg.Add(a);
            reg.Add(cp);
            reg.Add(cover);
            Assert.That(reg.Get("B4.Technical"), Is.SameAs(a.transform));
            Assert.IsTrue(reg.TryGet<CheckpointMarker>("CP3b", out var c));
            Assert.AreSame(cp, c);
            Assert.IsFalse(reg.TryGet<CheckpointMarker>("B4.Technical", out _));
            var covers = new List<CoverNode>();
            Assert.That(reg.GetAll(covers), Is.EqualTo(1));
            reg.Remove(cp);
            Assert.IsFalse(reg.Has("CP3b"));
            LogAssert.Expect(LogType.Warning, new Regex("Marker 'CP3b' is not registered"));
            Assert.IsNull(reg.Get("CP3b"));
        }

        [Test]
        public void DuplicateIdKeepsFirst()
        {
            MarkerRegistry.ResetForTests();
            var reg = MarkerRegistry.Instance;
            var a = Make<SceneMarker>("PA.Horn1", Vector3.zero);
            var b = Make<SceneMarker>("PA.Horn1", Vector3.one);
            reg.Add(a);
            LogAssert.Expect(LogType.Error, new Regex("Duplicate marker id 'PA.Horn1'"));
            reg.Add(b);
            Assert.That(reg.Get("PA.Horn1"), Is.SameAs(a.transform));
        }

        [Test]
        public void NodeSetCoverAndGlassing()
        {
            var set = Make<SquadNodeSet>("B3.NodeSet.M2", new Vector3(185, 0, 200));
            var hol = new GameObject("HOL").transform;
            _objects.Add(hol.gameObject);
            hol.position = new Vector3(182.5f, 0, 201);
            set.Configure(new SquadNode { Point = hol, Stance = Stance.Crouch }, default, default);
            Assert.That(set.PositionOf(SquadIds.Hollis), Is.EqualTo(new Vector3(182.5f, 0, 201)));
            Assert.That(set.PositionOf(SquadIds.Tinker), Is.EqualTo(new Vector3(185, 0, 200)), "falls back to the set");
            Assert.That(set.Get(SquadIds.Hollis).Stance, Is.EqualTo(Stance.Crouch));

            var cover = Make<CoverNode>("", Vector3.zero);
            cover.Configure(false, true, true, 120f, "B2.AlertCover");
            Assert.IsTrue(cover.Protects(new Vector3(0, 0, 10)));
            Assert.IsFalse(cover.Protects(new Vector3(0, 0, -10)));

            var poi = Make<GlassingPOI>("B1.POI1", new Vector3(378, 45, 362));
            var eye = new Vector3(42, 23.85f, 30);
            Assert.IsTrue(poi.IsInView(new Ray(eye, poi.Position - eye)));
            var off = Quaternion.Euler(0, 6f, 0) * (poi.Position - eye);
            Assert.IsFalse(poi.IsInView(new Ray(eye, off)));
        }

        [Test]
        public void GateOpensAndCloses()
        {
            var gate = Make<GateMarker>("Gate.Weighbridge", new Vector3(183, 0, 163));
            var clip = gate.gameObject.AddComponent<BoxCollider>();
            gate.Configure(false, new Collider[] { clip }, null, null);
            Assert.IsFalse(gate.IsOpen);
            Assert.IsTrue(clip.enabled);
            string changed = null;
            GameEvents.GateChanged += (id, open) => changed = id + ":" + open;
            gate.SetOpen(true);
            Assert.IsFalse(clip.enabled);
            Assert.That(changed, Is.EqualTo("Gate.Weighbridge:True"));
            GameEvents.ResetAll();
        }

        [Test]
        public void InteractionPointRaisesOnceAndSaves()
        {
            var ip = Make<InteractionPoint>("B4.SideDoor", new Vector3(186, 1.2f, 307));
            ip.Configure("[E] Open", false, 0f, false, true);
            ip.ResetState();
            Assert.IsFalse(ip.CanInteract(null), "starts unavailable");
            string used = null;
            GameEvents.Interacted += id => used = id;
            ip.SetAvailable(true);
            Assert.That(ip.PromptText, Is.EqualTo("[E] Open"));
            Assert.That(ip.HoldSeconds, Is.EqualTo(0f));
            ip.Interact(null);
            Assert.That(used, Is.EqualTo("B4.SideDoor"));
            Assert.IsTrue(ip.WasUsed);
            Assert.IsFalse(ip.IsAvailable, "once");
            var snap = CheckpointSnapshot.Capture("CP4", new ISaveable[] { ip });
            ip.ResetState();
            Assert.IsFalse(ip.WasUsed);
            snap.RestoreInto(new ISaveable[] { ip });
            Assert.IsTrue(ip.WasUsed);
            GameEvents.ResetAll();
        }

        [Test]
        public void MarkupCsvParsesAndContainsAppendixIds()
        {
            var t = MarkupIds.Load();
            Assert.That(t.Problems, Is.Empty, string.Join("\n", t.Problems));
            Assert.That(t.Rows.Count, Is.GreaterThan(250));
            foreach (var id in new[] { "CP1", "CP3b", "CP10", "B3.NodeSet.M2", "B3.T.Z185", "OOB.RidgeWest", "Gate.Weighbridge",
                                       "B6.F1", "B11.Exfil", "BOOTH.C_POV", "HELI.DoorGunPivot", "SC_B3_BAL", "B3.Route.WaveB",
                                       "CritPath.B4", "E3-01", "B3.AW08", "B3.Fruit6", "B9.R4" })
                Assert.IsTrue(t.Contains(id), "missing " + id);
            Assert.IsTrue(t.TryGet("B3.NodeSet.M2", out var m2));
            Assert.That(m2.Points.Count, Is.EqualTo(3));
            Assert.That(m2.Component, Is.EqualTo("SquadNodeSet"));
            Assert.IsTrue(t.TryGet("B3.T.Z185", out var z));
            Assert.IsTrue(z.TryGetBox(out var size));
            Assert.That(size.x, Is.EqualTo(23f).Within(1e-4f));
            Assert.IsTrue(t.TryGet("CP1", out var cp1));
            Assert.IsTrue(cp1.HasHeading);
            Assert.That(cp1.Heading, Is.EqualTo(45f));
        }

        [Test]
        public void CsvParserHandlesQuotes()
        {
            var t = MarkupIdTable.Parse("id,kind,x,y,z,notes\nA,M,1,2,3,\"has, comma\"\nA,M,0,0,0,dup\n");
            Assert.IsTrue(t.TryGet("A", out var a));
            Assert.That(a.Notes, Is.EqualTo("has, comma"));
            Assert.That(a.Position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(t.Problems.Count, Is.EqualTo(1));
        }
    }
}
