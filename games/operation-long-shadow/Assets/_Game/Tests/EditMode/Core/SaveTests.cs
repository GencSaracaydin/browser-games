using System.Collections.Generic;
using System.Text.RegularExpressions;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LongShadow.Tests.EditMode.Core
{
    public class SaveTests
    {
        private sealed class FakeSaveable : ISaveable
        {
            public string Key;
            public int Beat;
            public float S;
            public bool Flag;
            public string Cp;
            public Vector3 Pos;
            public Quaternion Rot = Quaternion.identity;
            public List<string> Lines = new List<string>();
            public string SaveKey => Key;

            public void Capture(SaveWriter w)
            {
                w.Write("beat", Beat);
                w.Write("s", S);
                w.Write("flag", Flag);
                w.Write("cp", Cp);
                w.Write("pos", Pos);
                w.Write("rot", Rot);
                w.WriteStrings("lines", Lines);
            }

            public void Restore(SaveReader r)
            {
                Beat = r.ReadInt("beat", -1);
                S = r.ReadFloat("s", -1f);
                Flag = r.ReadBool("flag");
                Cp = r.ReadString("cp");
                Pos = r.ReadVector3("pos");
                Rot = r.ReadQuaternion("rot", Quaternion.identity);
                r.ReadStrings("lines", Lines);
            }
        }

        [Test]
        public void WriterReaderRoundTrip()
        {
            var rec = new SaveRecord { Key = "k" };
            var w = new SaveWriter(rec);
            w.Write("i", -12345);
            w.Write("f", 1.8f / 3f);
            w.Write("d", 1.0 / 7.0);
            w.Write("b", true);
            w.Write("s", "a,b\"c");
            w.Write("v", new Vector3(1.5f, -2.25f, 1e-7f));
            w.Write("c", new Color(0.1f, 0.2f, 0.3f, 0.4f));
            w.WriteStrings("l", new[] { "D014", "", "D029" });
            w.Write("i", 7); // overwrite
            var r = new SaveReader(rec);
            Assert.That(r.ReadInt("i"), Is.EqualTo(7));
            Assert.That(r.ReadFloat("f"), Is.EqualTo(1.8f / 3f));
            Assert.That(r.ReadDouble("d"), Is.EqualTo(1.0 / 7.0));
            Assert.IsTrue(r.ReadBool("b"));
            Assert.That(r.ReadString("s"), Is.EqualTo("a,b\"c"));
            Assert.That(r.ReadVector3("v"), Is.EqualTo(new Vector3(1.5f, -2.25f, 1e-7f)));
            Assert.That(r.ReadColor("c", Color.black), Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
            var l = new List<string>();
            Assert.IsTrue(r.ReadStrings("l", l));
            Assert.That(l, Is.EqualTo(new[] { "D014", "", "D029" }));
        }

        [Test]
        public void MissingFieldsUseFallbacks()
        {
            var r = new SaveReader(null);
            Assert.That(r.ReadInt("x", 3), Is.EqualTo(3));
            Assert.That(r.ReadFloat("x", 2f), Is.EqualTo(2f));
            Assert.IsTrue(r.ReadBool("x", true));
            Assert.That(r.ReadString("x", "d"), Is.EqualTo("d"));
            Assert.That(r.ReadVector3("x", Vector3.one), Is.EqualTo(Vector3.one));
            Assert.IsFalse(r.ReadStrings("x", new List<string>()));
            Assert.IsFalse(r.Has("x"));
        }

        [Test]
        public void SnapshotCaptureJsonRestore()
        {
            var a = new FakeSaveable { Key = "Mission", Beat = 5, S = 1.97f, Flag = true, Cp = "CP5", Pos = new Vector3(236, 9, 304),
                                       Rot = Quaternion.Euler(0, 45, 0), Lines = { "D037", "D038" } };
            var b = new FakeSaveable { Key = "Objectives", Beat = 2 };
            var snap = CheckpointSnapshot.Capture("CP5", new ISaveable[] { a, b });
            Assert.That(snap.Records.Count, Is.EqualTo(2));
            var json = snap.ToJson();
            var back = CheckpointSnapshot.FromJson(json);
            Assert.That(back.CheckpointId, Is.EqualTo("CP5"));
            var a2 = new FakeSaveable { Key = "Mission" };
            var b2 = new FakeSaveable { Key = "Objectives" };
            Assert.That(back.RestoreInto(new ISaveable[] { a2, b2 }), Is.EqualTo(2));
            Assert.That(a2.Beat, Is.EqualTo(5));
            Assert.That(a2.S, Is.EqualTo(1.97f));
            Assert.IsTrue(a2.Flag);
            Assert.That(a2.Cp, Is.EqualTo("CP5"));
            Assert.That(a2.Pos, Is.EqualTo(new Vector3(236, 9, 304)));
            Assert.That(Quaternion.Angle(a2.Rot, Quaternion.Euler(0, 45, 0)), Is.LessThan(1e-3f));
            Assert.That(a2.Lines, Is.EqualTo(new[] { "D037", "D038" }));
            Assert.That(b2.Beat, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateKeysAreRejectedAndLogged()
        {
            LogAssert.Expect(LogType.Error, new Regex("Duplicate SaveKey 'X'"));
            var snap = CheckpointSnapshot.Capture("CP1", new ISaveable[] { new FakeSaveable { Key = "X" }, new FakeSaveable { Key = "X" } });
            Assert.That(snap.Records.Count, Is.EqualTo(1));
        }

        [Test]
        public void BadJsonReturnsNull()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Snapshot JSON invalid"));
            Assert.IsNull(CheckpointSnapshot.FromJson("{not json"));
        }
    }
}
