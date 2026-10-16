using System.Collections.Generic;
using LongShadow.World.Level;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.World.Level
{
    /// <summary>MeshBuilder conventions, LOD masks, collider carving, determinism and the awning hit test.</summary>
    public sealed class LevelGeometryKitTests
    {
        [Test]
        public void GroundQuadFacesUp()
        {
            var mb = new MeshBuilder();
            mb.Slot(KitSlots.Paving);
            mb.AddQuad(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, 0f));
            var mesh = mb.ToMesh("t");
            mesh.RecalculateNormals();
            Assert.Greater(mesh.normals[0].y, 0.99f);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void DetailGeometryIsDroppedFromLodOne()
        {
            var mb = new MeshBuilder();
            mb.Slot(KitSlots.Concrete);
            mb.AddBox(Vector3.zero, Vector3.one);
            mb.LodMask = MeshBuilder.LodDetail;
            mb.AddBox(Vector3.up * 2f, Vector3.one * 0.2f);
            Assert.Greater(mb.TriangleCount(0), mb.TriangleCount(1));
        }

        [Test]
        public void BoxCarveLeavesNoOverlapWithTheHole()
        {
            var box = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f));
            var hole = new Bounds(new Vector3(1f, 0f, 1f), new Vector3(3f, 2f, 3f));
            var parts = BoxCarve.Subtract(box, hole);
            Assert.Greater(parts.Count, 0);
            var volume = 0f;
            foreach (var p in parts)
            {
                Assert.IsFalse(BoxCarve.Intersects(p, hole), "part overlaps the hole");
                volume += p.size.x * p.size.y * p.size.z;
            }
            Assert.AreEqual(400f - 18f, volume, 0.01f);
        }

        [Test]
        public void DetRandomIsDeterministic()
        {
            var a = new DetRandom("S1.Grove");
            var b = new DetRandom("S1.Grove");
            for (var i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void AwningHitTestUsesTheSegment()
        {
            var box = new Bounds(Vector3.zero, new Vector3(3f, 0.6f, 2f));
            Assert.IsTrue(AwningPanel.SegmentHitsBounds(new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f), box));
            Assert.IsFalse(AwningPanel.SegmentHitsBounds(new Vector3(-5f, 2f, 0f), new Vector3(5f, 2f, 0f), box));
            Assert.IsFalse(AwningPanel.SegmentHitsBounds(new Vector3(-5f, 0f, 0f), new Vector3(-3f, 0f, 0f), box));
        }

        [Test]
        public void EveryCatalogueBuildingGenerates()
        {
            var failures = new List<string>();
            foreach (var spec in BuildingCatalog.All)
            {
                var mb = new MeshBuilder();
                try
                {
                    BuildingGeometry.Build(spec, mb);
                    if (mb.IsEmpty) failures.Add(spec.Id + " empty");
                    if (mb.Colliders.Count == 0) failures.Add(spec.Id + " has no colliders");
                }
                catch (System.Exception e)
                {
                    failures.Add(spec.Id + ": " + e.Message);
                }
            }
            Assert.IsEmpty(failures, string.Join("; ", failures));
        }

        [Test]
        public void HeroStructuresGenerateWithAnchors()
        {
            var shell = new MeshBuilder();
            var interior = new MeshBuilder();
            var anchors = new StructureAnchors();
            CinemaGeometry.Build(shell, interior, anchors, 1);
            Assert.IsTrue(anchors.TryGet("SideDoor.Hinge", out _));
            var hall = new MeshBuilder();
            RelayHallGeometry.BuildShell(hall, anchors);
            RelayHallGeometry.BuildInterior(hall, anchors);
            Assert.IsTrue(anchors.TryGet("Hatch", out _));
            Assert.IsTrue(anchors.TryGet("Trench.Spawn.0", out _));
            Assert.Less(shell.VertexCount + interior.VertexCount, 1500000, "cinema vertex budget sanity");
        }
    }
}
