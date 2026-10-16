using System.Collections.Generic;
using LongShadow.Characters.Body;
using LongShadow.Characters.Editor;
using LongShadow.Characters.Ragdoll;
using LongShadow.Core;
using LongShadow.Core.Editor;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Characters
{
    public class BodySetupTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void SpecsCoverTheD19BodiesAndTheManifest()
        {
            var names = new HashSet<string>();
            foreach (var s in BodySpecs.All)
            {
                Assert.That(names.Add(s.Prefab), Is.True, "duplicate " + s.Prefab);
                Assert.That(s.Prefab.StartsWith("PFB_"), Is.True, s.Prefab);
                Assert.That(s.Models, Is.Not.Null.And.Not.Empty, s.Prefab);
                Assert.That(AssetManifest.TryGet(s.Manifest, out _), Is.True, s.Prefab + " manifest id " + s.Manifest);
                if (s.Kind == BodyKind.Enemy || s.Kind == BodyKind.Squad) Assert.That(s.Hp, Is.GreaterThan(0f), s.Prefab);
            }
            Assert.That(BodySpecs.Find("PFB_Calder_FP").Kind, Is.EqualTo(BodyKind.FirstPerson));
            Assert.That(BodySpecs.Find("PFB_Calder_Shadow").Kind, Is.EqualTo(BodyKind.Shadow));
            Assert.That(BodySpecs.Find("PFB_GreycoatA").TorsoArmour, Is.EqualTo(0.8f), "GDD §14.1 Greycoat vest");
            Assert.That(BodySpecs.Find("PFB_GreycoatA").Hp, Is.EqualTo(150f));
            foreach (var id in new[] { "CHR_CALDER", "CHR_TINKER", "CHR_HOLLIS", "CHR_SPARROW", "CHR_LEGION_A", "CHR_LEGION_B", "CHR_LEGION_C",
                                       "CHR_GREYCOAT_A", "CHR_GREYCOAT_B", "CHR_INTERROGATOR", "CHR_HARROU", "CHR_MAGPIE" })
            {
                var found = false;
                foreach (var s in BodySpecs.All) found |= s.Manifest == id;
                Assert.That(found, Is.True, "no body for " + id);
            }
        }

        private Animator Mannequin()
        {
            _go = PlaceholderMannequin.CreateInstance("Test_Mannequin", Color.grey, false);
            var a = _go.GetComponent<Animator>();
            Assert.That(a.avatar, Is.Not.Null);
            Assert.That(a.avatar.isValid && a.avatar.isHuman, Is.True, "generated avatar is a valid Humanoid");
            if (a.GetBoneTransform(HumanBodyBones.Hips) == null) Assert.Ignore("Animator bone lookup unavailable in this editor context.");
            return a;
        }

        [Test]
        public void MannequinRigResolvesWithSockets()
        {
            var a = Mannequin();
            var rig = _go.AddComponent<BodyRig>();
            rig.EditorSetAnimator(a);
            rig.EditorSetHeight(PlaceholderMannequin.Height);
            rig.Resolve(true);
            Assert.That(rig.IsValid, Is.True);
            Assert.That(rig.RightGrip, Is.Not.Null);
            Assert.That(rig.LeftGrip, Is.Not.Null);
            Assert.That(rig.Back, Is.Not.Null);
            Assert.That(rig.Eyes.position.y, Is.GreaterThan(rig.Head.position.y), "eyes above the head pivot");
        }

        [Test]
        public void RagdollHasElevenBodiesWithZones()
        {
            var a = Mannequin();
            var rig = _go.AddComponent<BodyRig>();
            rig.EditorSetAnimator(a);
            rig.EditorSetHeight(PlaceholderMannequin.Height);
            rig.Resolve(true);
            var capsule = _go.AddComponent<CapsuleCollider>();
            var rd = RagdollBuilder.Build(rig, a, LayerIds.Hitbox, new Collider[] { capsule });
            Assert.That(rd, Is.Not.Null);
            Assert.That(rd.Bones.Length, Is.EqualTo(11));
            var mass = 0f;
            var heads = 0;
            foreach (var b in rd.Bones)
            {
                Assert.That(b.Body.isKinematic, Is.True, "alive hitboxes are kinematic");
                Assert.That(b.Collider.gameObject.layer, Is.EqualTo(LayerIds.Hitbox));
                mass += b.Body.mass;
                if (b.Hitbox.Zone == HitZone.Head) heads++;
                if (b.Human != HumanBodyBones.Hips) Assert.That(b.Joint, Is.Not.Null, b.Human + " has a joint");
            }
            Assert.That(heads, Is.EqualTo(1));
            Assert.That(mass, Is.InRange(70f, 82f), "adult body mass");
            // Rebuilding replaces, never duplicates.
            RagdollBuilder.Build(rig, a, LayerIds.Hitbox, new Collider[] { capsule });
            Assert.That(a.GetBoneTransform(HumanBodyBones.Head).GetComponents<Collider>().Length, Is.EqualTo(1));
        }

        [Test]
        public void HeightMeasuresTheMannequin()
        {
            Mannequin();
            var h = BodyPrefabBuilder.MeasureHeight(_go, null, out var feet);
            Assert.That(h, Is.InRange(1.65f, 1.85f));
            Assert.That(feet, Is.EqualTo(0f).Within(0.05f));
        }
    }
}
