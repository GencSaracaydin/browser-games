using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using LongShadow.Combat.Editor;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>Surface → impact/decal mapping, decal atlas grouping and the WP10 layout sizes.</summary>
    public class FxDataTests
    {
        [Test]
        public void EverySurfaceHasAnImpactProfile()
        {
            foreach (SurfaceType s in System.Enum.GetValues(typeof(SurfaceType)))
            {
                var p = FxIds.ProfileOf(s);
                Assert.That((int)p, Is.InRange(0, (int)ImpactProfile.Fruit), s.ToString());
            }
            Assert.That(FxIds.ProfileOf(SurfaceType.Sand), Is.EqualTo(ImpactProfile.Dust));
            Assert.That(FxIds.ProfileOf(SurfaceType.Metal), Is.EqualTo(ImpactProfile.Metal));
            Assert.That(FxIds.BulletHoleGroup(SurfaceType.Sand), Is.Null, "no holes in sand");
            Assert.That(FxIds.BulletHoleGroup(SurfaceType.Plaster), Is.EqualTo("plaster"));
        }

        [Test]
        public void AtlasGroupsByPrefix()
        {
            var atlas = new DecalAtlas(null, new[] { "plaster_1", "plaster_2", "wood_1", "spatter" },
                new[] { new Vector4(0, 0, .5f, .5f), new Vector4(.5f, 0, 1, .5f), new Vector4(0, .5f, .5f, 1), new Vector4(.5f, .5f, 1, 1) },
                new[] { new Vector2(0.1f, 0.1f), new Vector2(0.1f, 0.08f), new Vector2(0.08f, 0.05f), new Vector2(0.3f, 0.2f) });
            for (var i = 0; i < 20; i++)
            {
                Assert.That(atlas.TryPick("plaster", out var r, out _), Is.True);
                Assert.That(r.y, Is.EqualTo(0f));
            }
            Assert.That(atlas.TryPick("wood", out var wr, out var ws), Is.True);
            Assert.That(wr, Is.EqualTo(new Vector4(0, .5f, .5f, 1)));
            Assert.That(ws, Is.EqualTo(new Vector2(0.08f, 0.05f)));
            Assert.That(atlas.TryPick("spatter", out _, out _), Is.True);
            Assert.That(atlas.TryPick("metal", out _, out _), Is.True, "unknown group falls back to all");
            Assert.That(atlas.IsValid, Is.False, "no material");
        }

        [Test]
        public void DecalSizesFollowTheWp10Notes()
        {
            Assert.That(CombatFxBuilder.DecalSize(FxIds.DecalBulletHoles, "concrete_2"), Is.EqualTo(0.1f));
            Assert.That(CombatFxBuilder.DecalSize(FxIds.DecalBulletHoles, "wood_1"), Is.EqualTo(0.08f));
            Assert.That(CombatFxBuilder.DecalSize(FxIds.DecalBulletHoles, "metal_4"), Is.EqualTo(0.07f));
            Assert.That(CombatFxBuilder.DecalSize(FxIds.DecalScorch, "blast_large"), Is.InRange(2.5f, 3.5f));
            Assert.That(CombatFxBuilder.DecalSize(FxIds.DecalBlood, "pool"), Is.InRange(0.15f, 0.4f));
        }

        [Test]
        public void ProceduralTexturesHaveTheExpectedLayout()
        {
            var tracer = FxTextures.BuildTracer();
            Assert.That(tracer.width, Is.EqualTo(256));
            Assert.That(tracer.height, Is.EqualTo(64));
            var top = tracer.GetPixel(200, 44); // halo of the red half (the core is white)
            var bottom = tracer.GetPixel(200, 12);
            Assert.That(top.r, Is.GreaterThan(top.g), "v 0.5-1 red");
            Assert.That(bottom.g, Is.GreaterThan(bottom.r), "v 0-0.5 green");
            Object.DestroyImmediate(tracer);
            var meshes = FxFactory.BuildMuzzleMeshes();
            Assert.That(meshes.Length, Is.EqualTo(4));
            foreach (var m in meshes)
            {
                Assert.That(m.vertexCount, Is.EqualTo(12));
                Object.DestroyImmediate(m);
            }
        }

        [Test]
        public void TracerColoursFollowTeams()
        {
            Assert.That(Teams.IsEnemySide(Team.Legion), Is.True);
            Assert.That(Teams.IsEnemySide(Team.Greycoat), Is.True);
            Assert.That(Teams.IsEnemySide(Team.Squad), Is.False, "SUNDIAL tracers are red");
        }
    }
}
