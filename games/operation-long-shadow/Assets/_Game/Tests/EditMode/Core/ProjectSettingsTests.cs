using LongShadow.Core;
using LongShadow.Core.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Core
{
    /// <summary>Checks that ProjectSetup has been applied (TECH_PLAN §3.9, §3.1).</summary>
    public class ProjectSettingsTests
    {
        [Test]
        public void LayerNamesMatchLayerIds()
        {
            for (var i = 6; i < LayerIds.Names.Length; i++)
                Assert.That(LayerMask.LayerToName(i), Is.EqualTo(LayerIds.Names[i]), "layer " + i);
            Assert.That(LayerMask.NameToLayer("Viewmodel"), Is.EqualTo(LayerIds.Viewmodel));
        }

        [Test]
        public void CollisionMatrixMatchesTable()
        {
            for (var a = 0; a < 32; a++)
            for (var b = a; b < 32; b++)
                Assert.That(!Physics.GetIgnoreLayerCollision(a, b), Is.EqualTo(ProjectSetup.ShouldCollide(a, b)), a + " vs " + b);
            Assert.IsFalse(Physics.queriesHitTriggers);
        }

        [Test]
        public void MasksContainExpectedLayers()
        {
            Assert.That(LayerIds.PlayerShot & (1 << LayerIds.Hitbox), Is.Not.Zero);
            Assert.That(LayerIds.PlayerShot & (1 << LayerIds.SquadHitbox), Is.Zero, "player shots ignore squad hitboxes");
            Assert.That(LayerIds.EnemyShot & (1 << LayerIds.SquadHitbox), Is.Not.Zero);
            Assert.That(LayerIds.AISight & (1 << LayerIds.SmokeLOS), Is.Not.Zero);
        }

        [Test]
        public void WebPlayerSettingsApplied()
        {
            Assert.That(EditorUserBuildSettings.activeBuildTarget, Is.EqualTo(BuildTarget.WebGL));
            Assert.That(PlayerSettings.WebGL.compressionFormat, Is.EqualTo(WebGLCompressionFormat.Brotli));
            Assert.IsTrue(PlayerSettings.WebGL.decompressionFallback);
            Assert.That(PlayerSettings.colorSpace, Is.EqualTo(ColorSpace.Linear));
            Assert.That(QualitySettings.names, Does.Contain("Web_High"));
            Assert.That(RenderingLayerMask.RenderingLayerToName(RenderingLayerIds.Booth), Is.EqualTo("Booth"));
        }
    }
}
