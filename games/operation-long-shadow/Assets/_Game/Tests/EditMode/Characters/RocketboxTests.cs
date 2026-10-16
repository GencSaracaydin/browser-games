using LongShadow.Characters.Editor;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Characters
{
    public class RocketboxTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private Transform Bone(string name, Transform parent, Vector3 worldPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, true);
            t.position = worldPos;
            return t;
        }

        /// <summary>A raw Max-export Biped: thighs under the spine, clavicles under the neck, arms in a 45° A-pose.</summary>
        private Transform RawBiped()
        {
            _root = new GameObject("Military_Male_01");
            var r = _root.transform;
            var bip = Bone("Bip01", r, Vector3.zero);
            var pelvis = Bone("Bip01 Pelvis", bip, new Vector3(0f, 0.95f, 0f));
            var spine = Bone("Bip01 Spine", pelvis, new Vector3(0f, 1.05f, 0f));
            var spine1 = Bone("Bip01 Spine1", spine, new Vector3(0f, 1.18f, 0f));
            var spine2 = Bone("Bip01 Spine2", spine1, new Vector3(0f, 1.31f, 0f));
            var neck = Bone("Bip01 Neck", spine2, new Vector3(0f, 1.48f, 0f));
            Bone("Bip01 Head", neck, new Vector3(0f, 1.57f, 0f));
            foreach (var side in new[] { "L", "R" })
            {
                var s = side == "L" ? -1f : 1f;
                var clav = Bone("Bip01 " + side + " Clavicle", neck, new Vector3(0.04f * s, 1.43f, 0f));
                var up = Bone("Bip01 " + side + " UpperArm", clav, new Vector3(0.19f * s, 1.42f, 0f));
                var d = new Vector3(s, -1f, 0f).normalized; // 45° A-pose
                var fore = Bone("Bip01 " + side + " Forearm", up, up.position + d * 0.28f);
                var hand = Bone("Bip01 " + side + " Hand", fore, fore.position + d * 0.26f);
                Bone("Bip01 " + side + " Finger0", hand, hand.position + d * 0.03f + new Vector3(0f, -0.01f, 0.035f));
                Bone("Bip01 " + side + " Finger1", hand, hand.position + d * 0.09f + new Vector3(0f, 0f, 0.03f));
                Bone("Bip01 " + side + " Finger2", hand, hand.position + d * 0.095f);
                Bone("Bip01 " + side + " Finger4", hand, hand.position + d * 0.085f + new Vector3(0f, 0f, -0.035f));
                var thigh = Bone("Bip01 " + side + " Thigh", spine, new Vector3(0.09f * s, 0.92f, 0f));
                var calf = Bone("Bip01 " + side + " Calf", thigh, new Vector3(0.1f * s, 0.49f, 0.02f));
                var foot = Bone("Bip01 " + side + " Foot", calf, new Vector3(0.1f * s, 0.08f, 0f));
                Bone("Bip01 " + side + " Toe0", foot, new Vector3(0.1f * s, 0.02f, 0.13f));
            }
            return r;
        }

        [Test]
        public void DetectsAndFixesARawHierarchyKeepingWorldPoses()
        {
            var root = RawBiped();
            Assert.That(RocketboxBones.Classify(root), Is.EqualTo(BipedLayout.Raw));
            var thigh = RocketboxBones.FindDeep(root, "Bip01 L Thigh");
            var before = thigh.position;
            Assert.That(RocketboxBones.FixHierarchy(root), Is.EqualTo(4));
            Assert.That(thigh.parent.name, Is.EqualTo("Bip01 Pelvis"));
            Assert.That(RocketboxBones.FindDeep(root, "Bip01 R Clavicle").parent.name, Is.EqualTo("Bip01 Spine2"));
            Assert.That((thigh.position - before).magnitude, Is.LessThan(1e-5f));
            Assert.That(RocketboxBones.Classify(root), Is.EqualTo(BipedLayout.Fixed));
            Assert.That(RocketboxBones.FixHierarchy(root), Is.EqualTo(0), "idempotent");
        }

        [Test]
        public void NotABipedIsLeftAlone()
        {
            _root = new GameObject("Mannequin");
            new GameObject("pelvis").transform.SetParent(_root.transform);
            Assert.That(RocketboxBones.Classify(_root.transform), Is.EqualTo(BipedLayout.NotBiped));
        }

        [Test]
        public void RenamesBip02()
        {
            _root = new GameObject("X");
            var b = new GameObject("Bip02").transform;
            b.SetParent(_root.transform);
            new GameObject("Bip02 Pelvis").transform.SetParent(b);
            Assert.That(RocketboxBones.RenameBip02(_root.transform), Is.EqualTo(2));
            Assert.That(RocketboxBones.FindDeep(_root.transform, "Bip01 Pelvis"), Is.Not.Null);
        }

        [Test]
        public void HumanMappingUsesTheJawAndUnityNames()
        {
            var root = RawBiped();
            RocketboxBones.FixHierarchy(root);
            Assert.That(RocketboxBones.HasRequiredBones(root), Is.True);
            var bones = RocketboxBones.BuildHumanBones(root);
            var hasHips = false;
            foreach (var b in bones)
                if (b.humanName == HumanTrait.BoneName[(int)HumanBodyBones.Hips]) hasHips = b.boneName == "Bip01 Pelvis";
            Assert.That(hasHips, Is.True);
            Assert.That(RocketboxBones.NameOf(HumanBodyBones.Jaw), Is.EqualTo("Bip01 MJaw"));
            Assert.That(RocketboxBones.NameOf(HumanBodyBones.LeftLittleDistal), Is.EqualTo("Bip01 L Finger42"));
        }

        [Test]
        public void TPoseSolverLevelsTheArms()
        {
            var root = RawBiped();
            RocketboxBones.FixHierarchy(root);
            var s = PoseSkeleton.FromHierarchy(root);
            Assert.That(RocketboxImporter.ArmsDroop(s), Is.EqualTo(45f).Within(1f));
            Assert.That(TPoseSolver.Apply(s), Is.True);
            Assert.That(RocketboxImporter.ArmsDroop(s), Is.LessThan(0.5f));
            var hand = s.WorldPosition("Bip01 L Hand") - s.WorldPosition("Bip01 L Forearm");
            Assert.That(Vector3.Angle(hand, Vector3.left), Is.LessThan(0.5f), "left forearm straight out to -X");
            var calf = s.WorldPosition("Bip01 R Foot") - s.WorldPosition("Bip01 R Calf");
            Assert.That(Vector3.Angle(calf, Vector3.down), Is.LessThan(0.5f), "legs straight down");
            Assert.That(s.ToSkeletonBones().Length, Is.EqualTo(s.Nodes.Count));
        }

        [Test]
        public void ImporterHelpers()
        {
            Assert.That(RocketboxImporter.WithStamp("ls-import:manual ls-rbx:v1:map", "ls-rbx:v2:tpose"), Is.EqualTo("ls-import:manual ls-rbx:v2:tpose"));
            Assert.That(RocketboxImporter.WithStamp(null, "ls-rbx:v1:map"), Is.EqualTo("ls-rbx:v1:map"));
            var a = new[] { new HumanBone { humanName = "Hips", boneName = "Bip01 Pelvis" }, new HumanBone { humanName = "Jaw", boneName = "Bip01 MNose" } };
            var b = new[] { new HumanBone { humanName = "Hips", boneName = "Bip01 Pelvis" }, new HumanBone { humanName = "Jaw", boneName = "Bip01 MJaw" } };
            Assert.That(RocketboxImporter.SameMapping(a, b), Is.False, "a jaw mapped to a face bone is fixed");
            Assert.That(RocketboxImporter.SameMapping(b, b), Is.True);
            Assert.That(RocketboxImporter.IsCharacterModel("Assets/_Game/Art/Models/Characters/Calder/SK_Calder.fbx"), Is.True);
            Assert.That(RocketboxImporter.IsCharacterModel("Assets/_Game/Art/Models/Characters/Props/boonie/SM_CHP_Boonie.fbx"), Is.False);
        }
    }
}
