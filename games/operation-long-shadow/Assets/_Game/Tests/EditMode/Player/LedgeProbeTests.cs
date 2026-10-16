using System.Collections.Generic;
using LongShadow.Player.Movement;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>
    /// Vault / climb / air-mantle ledge detection against real colliders (TECH_PLAN §7.3 "capsule casts + top probe"):
    /// the 0.6 / 1.0 / 1.4 / 1.8 / 2.2 m ledges of the WP02 test course, thin walls, reach and headroom.
    /// </summary>
    public class LedgeProbeTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private MotorSettings _s;

        [SetUp]
        public void SetUp()
        {
            _s = new MotorSettings();
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
            Physics.SyncTransforms();
        }

        private GameObject Box(string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = center;
            go.transform.localScale = size;
            _objects.Add(go);
            return go;
        }

        private LedgeQuery Query(float maxTop, float reach, bool overVault = true, float stepOffset = 0.35f, float minTop = 0.6f) => new LedgeQuery
        {
            Feet = Vector3.zero,
            Forward = Vector3.forward,
            Radius = _s.Radius,
            StandHeight = _s.StandHeight,
            CrouchHeight = _s.CrouchHeight,
            MinTop = minTop,
            MaxTop = maxTop,
            MaxFaceDistance = reach,
            AllowOverVault = overVault,
            OverVaultMaxThickness = _s.OverVaultMaxThickness,
            SlopeLimit = _s.SlopeLimit,
            StepOffset = stepOffset,
        };

        [TestCase(0.6f, LedgeKind.Vault)]
        [TestCase(1.0f, LedgeKind.Vault)]
        [TestCase(1.4f, LedgeKind.Vault)]
        [TestCase(1.8f, LedgeKind.Climb)]
        public void CourseLedgesAreFoundAndClassified(float height, LedgeKind expected)
        {
            Box("Ledge", new Vector3(0f, height * 0.5f, 1.5f), new Vector3(3f, height, 2f)); // face at z = 0.5
            Physics.SyncTransforms();
            Assert.IsTrue(LedgeProbe.Probe(Query(_s.ClimbMaxHeight, _s.GroundLedgeReach), out var info));
            Assert.That(info.TopHeight, Is.EqualTo(height).Within(0.02f));
            Assert.That(info.FaceDistance, Is.EqualTo(0.5f).Within(0.02f));
            Assert.IsFalse(info.OverVault);
            Assert.IsFalse(info.EndCrouched);
            Assert.That(info.End.y, Is.EqualTo(height).Within(0.02f), "ends standing on the top");
            Assert.That(info.End.z, Is.GreaterThan(0.5f + _s.Radius), "past the face");
            Assert.That(MovementMath.ClassifyGroundLedge(info.TopHeight, _s), Is.EqualTo(expected));
        }

        [Test]
        public void TwoPointTwoMetresIsTooHigh()
        {
            Box("Ledge", new Vector3(0f, 1.1f, 1.5f), new Vector3(3f, 2.2f, 2f));
            Physics.SyncTransforms();
            Assert.IsFalse(LedgeProbe.Probe(Query(_s.ClimbMaxHeight, _s.GroundLedgeReach), out _));
        }

        [Test]
        public void LowerThanVaultHeightIsIgnored()
        {
            Box("Kerb", new Vector3(0f, 0.25f, 1.5f), new Vector3(3f, 0.5f, 2f));
            Physics.SyncTransforms();
            Assert.IsFalse(LedgeProbe.Probe(Query(_s.ClimbMaxHeight, _s.GroundLedgeReach), out _));
        }

        [Test]
        public void OutOfReachIsIgnored()
        {
            Box("Ledge", new Vector3(0f, 0.5f, 2.5f), new Vector3(3f, 1f, 2f)); // face at 1.5 m
            Physics.SyncTransforms();
            Assert.IsFalse(LedgeProbe.Probe(Query(_s.ClimbMaxHeight, _s.GroundLedgeReach), out _));
        }

        [Test]
        public void ThinWallIsVaultedOver()
        {
            Box("Wall", new Vector3(0f, 0.5f, 0.625f), new Vector3(3f, 1f, 0.25f));
            Physics.SyncTransforms();
            Assert.IsTrue(LedgeProbe.Probe(Query(_s.VaultMaxHeight, _s.GroundLedgeReach), out var info));
            Assert.IsTrue(info.OverVault);
            Assert.That(info.End.y, Is.EqualTo(0f).Within(0.02f), "lands on the far side");
            Assert.That(info.End.z, Is.GreaterThan(0.75f + _s.Radius));
        }

        [Test]
        public void LowCeilingBlocksTheClimb()
        {
            Box("Ledge", new Vector3(0f, 0.5f, 1.5f), new Vector3(3f, 1f, 2f));
            Box("Ceiling", new Vector3(0f, 2.25f, 0.5f), new Vector3(3f, 0.5f, 4f)); // underside at 2.0 m
            Physics.SyncTransforms();
            Assert.IsFalse(LedgeProbe.Probe(Query(_s.ClimbMaxHeight, _s.GroundLedgeReach), out _), "no room to rise over the edge");
        }

        [Test]
        public void AirMantleGrabsATopAboveTheFeet()
        {
            // Feet 0.8 m up in the air, a 2.0 m wall top 0.6 m ahead: top 1.2 m above the feet (GDD §12 air mantle).
            Box("Wall", new Vector3(0f, 1f, 1.6f), new Vector3(3f, 2f, 2f)); // face at z = 0.6
            Physics.SyncTransforms();
            var q = Query(_s.AirMantleMaxAboveFeet, _s.AirMantleReach, false, 0f, _s.AirMantleMinAboveFeet);
            q.Feet = new Vector3(0f, 0.8f, 0f);
            Assert.IsTrue(LedgeProbe.Probe(q, out var info));
            Assert.That(info.TopHeight, Is.EqualTo(1.2f).Within(0.02f));
            Assert.IsTrue(MovementMath.IsAirMantle(info.TopHeight, info.FaceDistance, _s));
        }

        [Test]
        public void FitsChecksHeadroom()
        {
            Box("Ceiling", new Vector3(0f, 1.55f, 0f), new Vector3(3f, 0.5f, 3f)); // underside at 1.3 m
            Physics.SyncTransforms();
            Assert.IsFalse(LedgeProbe.Fits(Vector3.zero, _s.StandHeight, _s.Radius), "cannot stand up under 1.3 m");
            Assert.IsTrue(LedgeProbe.Fits(Vector3.zero, _s.CrouchHeight, _s.Radius), "crouched fits");
        }
    }
}
