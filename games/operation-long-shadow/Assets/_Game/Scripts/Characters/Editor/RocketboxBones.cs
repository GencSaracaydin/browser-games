using System;
using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Microsoft Rocketbox (3ds Max Biped) skeleton knowledge for the importers (TECH_PLAN §7.8 "RocketboxImporter =
    /// adapted FixRocketboxMaxImport.cs with usingMixamoAnimations = false"): the explicit Humanoid bone map (so the jaw is
    /// never mapped to a face bone), detection of raw vs content-fixed hierarchies, the Bip02 rename and the thigh/clavicle
    /// re-parenting. Pure transform work, unit-tested.
    /// </summary>
    public static class RocketboxBones
    {
        /// <summary>Biped root node.</summary>
        public const string Root = "Bip01";
        public const string Pelvis = "Bip01 Pelvis";
        public const string Spine = "Bip01 Spine";
        public const string Spine2 = "Bip01 Spine2";
        public const string Neck = "Bip01 Neck";

        private static readonly KeyValuePair<HumanBodyBones, string>[] s_map =
        {
            P(HumanBodyBones.Hips, "Bip01 Pelvis"),
            P(HumanBodyBones.Spine, "Bip01 Spine"),
            P(HumanBodyBones.Chest, "Bip01 Spine1"),
            P(HumanBodyBones.UpperChest, "Bip01 Spine2"),
            P(HumanBodyBones.Neck, "Bip01 Neck"),
            P(HumanBodyBones.Head, "Bip01 Head"),
            P(HumanBodyBones.Jaw, "Bip01 MJaw"),
            P(HumanBodyBones.LeftEye, "Bip01 LEye"),
            P(HumanBodyBones.RightEye, "Bip01 REye"),
            P(HumanBodyBones.LeftShoulder, "Bip01 L Clavicle"),
            P(HumanBodyBones.LeftUpperArm, "Bip01 L UpperArm"),
            P(HumanBodyBones.LeftLowerArm, "Bip01 L Forearm"),
            P(HumanBodyBones.LeftHand, "Bip01 L Hand"),
            P(HumanBodyBones.RightShoulder, "Bip01 R Clavicle"),
            P(HumanBodyBones.RightUpperArm, "Bip01 R UpperArm"),
            P(HumanBodyBones.RightLowerArm, "Bip01 R Forearm"),
            P(HumanBodyBones.RightHand, "Bip01 R Hand"),
            P(HumanBodyBones.LeftUpperLeg, "Bip01 L Thigh"),
            P(HumanBodyBones.LeftLowerLeg, "Bip01 L Calf"),
            P(HumanBodyBones.LeftFoot, "Bip01 L Foot"),
            P(HumanBodyBones.LeftToes, "Bip01 L Toe0"),
            P(HumanBodyBones.RightUpperLeg, "Bip01 R Thigh"),
            P(HumanBodyBones.RightLowerLeg, "Bip01 R Calf"),
            P(HumanBodyBones.RightFoot, "Bip01 R Foot"),
            P(HumanBodyBones.RightToes, "Bip01 R Toe0"),
            // Fingers: Finger0x thumb, Finger1x index, Finger2x middle, Finger3x ring, Finger4x little.
            P(HumanBodyBones.LeftThumbProximal, "Bip01 L Finger0"),
            P(HumanBodyBones.LeftThumbIntermediate, "Bip01 L Finger01"),
            P(HumanBodyBones.LeftThumbDistal, "Bip01 L Finger02"),
            P(HumanBodyBones.LeftIndexProximal, "Bip01 L Finger1"),
            P(HumanBodyBones.LeftIndexIntermediate, "Bip01 L Finger11"),
            P(HumanBodyBones.LeftIndexDistal, "Bip01 L Finger12"),
            P(HumanBodyBones.LeftMiddleProximal, "Bip01 L Finger2"),
            P(HumanBodyBones.LeftMiddleIntermediate, "Bip01 L Finger21"),
            P(HumanBodyBones.LeftMiddleDistal, "Bip01 L Finger22"),
            P(HumanBodyBones.LeftRingProximal, "Bip01 L Finger3"),
            P(HumanBodyBones.LeftRingIntermediate, "Bip01 L Finger31"),
            P(HumanBodyBones.LeftRingDistal, "Bip01 L Finger32"),
            P(HumanBodyBones.LeftLittleProximal, "Bip01 L Finger4"),
            P(HumanBodyBones.LeftLittleIntermediate, "Bip01 L Finger41"),
            P(HumanBodyBones.LeftLittleDistal, "Bip01 L Finger42"),
            P(HumanBodyBones.RightThumbProximal, "Bip01 R Finger0"),
            P(HumanBodyBones.RightThumbIntermediate, "Bip01 R Finger01"),
            P(HumanBodyBones.RightThumbDistal, "Bip01 R Finger02"),
            P(HumanBodyBones.RightIndexProximal, "Bip01 R Finger1"),
            P(HumanBodyBones.RightIndexIntermediate, "Bip01 R Finger11"),
            P(HumanBodyBones.RightIndexDistal, "Bip01 R Finger12"),
            P(HumanBodyBones.RightMiddleProximal, "Bip01 R Finger2"),
            P(HumanBodyBones.RightMiddleIntermediate, "Bip01 R Finger21"),
            P(HumanBodyBones.RightMiddleDistal, "Bip01 R Finger22"),
            P(HumanBodyBones.RightRingProximal, "Bip01 R Finger3"),
            P(HumanBodyBones.RightRingIntermediate, "Bip01 R Finger31"),
            P(HumanBodyBones.RightRingDistal, "Bip01 R Finger32"),
            P(HumanBodyBones.RightLittleProximal, "Bip01 R Finger4"),
            P(HumanBodyBones.RightLittleIntermediate, "Bip01 R Finger41"),
            P(HumanBodyBones.RightLittleDistal, "Bip01 R Finger42"),
        };

        /// <summary>Human bone → Biped transform name.</summary>
        public static IReadOnlyList<KeyValuePair<HumanBodyBones, string>> Map => s_map;

        /// <summary>Biped name of a human bone, or null when the Biped has no such bone.</summary>
        public static string NameOf(HumanBodyBones bone)
        {
            foreach (var kv in s_map)
                if (kv.Key == bone) return kv.Value;
            return null;
        }

        /// <summary>Raw, fixed or not a Biped (looks at the parents of the thighs and clavicles).</summary>
        public static BipedLayout Classify(Transform modelRoot)
        {
            if (modelRoot == null) return BipedLayout.NotBiped;
            var pelvis = FindDeep(modelRoot, Pelvis) ?? FindDeep(modelRoot, "Bip02 Pelvis");
            if (pelvis == null) return BipedLayout.NotBiped;
            var thigh = FindDeep(modelRoot, "Bip01 L Thigh") ?? FindDeep(modelRoot, "Bip02 L Thigh");
            var clav = FindDeep(modelRoot, "Bip01 L Clavicle") ?? FindDeep(modelRoot, "Bip02 L Clavicle");
            var raw = (thigh != null && thigh.parent != null && thigh.parent != pelvis) ||
                      (clav != null && clav.parent != null && clav.parent.name.EndsWith(" Neck", StringComparison.Ordinal));
            if (modelRoot.Find("Bip02") != null) raw = true;
            return raw ? BipedLayout.Raw : BipedLayout.Fixed;
        }

        /// <summary>Renames a "Bip02" skeleton to "Bip01" (some Rocketbox exports). Returns the number of renamed transforms.</summary>
        public static int RenameBip02(Transform root)
        {
            var n = 0;
            if (root == null) return 0;
            if (root.name.StartsWith("Bip02", StringComparison.Ordinal))
            {
                root.name = "Bip01" + root.name.Substring(5);
                n++;
            }
            for (var i = 0; i < root.childCount; i++) n += RenameBip02(root.GetChild(i));
            return n;
        }

        /// <summary>
        /// FixRocketboxMaxImport (usingMixamoAnimations = false), done before the avatar is built: thighs under Bip01 Pelvis,
        /// clavicles under Bip01 Spine2. World poses are kept (worldPositionStays), so skin bind poses stay valid. Returns
        /// the number of re-parented bones. Safe on a fixed hierarchy (returns 0).
        /// </summary>
        public static int FixHierarchy(Transform modelRoot)
        {
            if (modelRoot == null) return 0;
            RenameBip02(modelRoot);
            var pelvis = FindDeep(modelRoot, Pelvis);
            var spine2 = FindDeep(modelRoot, Spine2);
            var n = 0;
            if (pelvis != null)
            {
                n += Reparent(FindDeep(modelRoot, "Bip01 L Thigh"), pelvis);
                n += Reparent(FindDeep(modelRoot, "Bip01 R Thigh"), pelvis);
            }
            if (spine2 != null)
            {
                n += Reparent(FindDeep(modelRoot, "Bip01 L Clavicle"), spine2);
                n += Reparent(FindDeep(modelRoot, "Bip01 R Clavicle"), spine2);
            }
            return n;
        }

        /// <summary>Human bone mapping (human name → transform name) for every Biped bone present under <paramref name="modelRoot"/>.</summary>
        public static HumanBone[] BuildHumanBones(Transform modelRoot)
        {
            var list = new List<HumanBone>(s_map.Length);
            foreach (var kv in s_map)
            {
                if (FindDeep(modelRoot, kv.Value) == null) continue;
                var hb = new HumanBone { humanName = HumanTrait.BoneName[(int)kv.Key], boneName = kv.Value };
                hb.limit.useDefaultValues = true;
                list.Add(hb);
            }
            return list.ToArray();
        }

        /// <summary>True when every bone Unity requires for a Humanoid is present.</summary>
        public static bool HasRequiredBones(Transform modelRoot)
        {
            foreach (var kv in s_map)
            {
                if (!HumanTrait.RequiredBone((int)kv.Key)) continue;
                if (FindDeep(modelRoot, kv.Value) == null) return false;
            }
            return true;
        }

        /// <summary>Depth-first search by exact name (self included).</summary>
        public static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            for (var i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private static int Reparent(Transform bone, Transform parent)
        {
            if (bone == null || parent == null || bone.parent == parent || bone == parent) return 0;
            bone.SetParent(parent, true);
            return 1;
        }

        private static KeyValuePair<HumanBodyBones, string> P(HumanBodyBones b, string n) => new KeyValuePair<HumanBodyBones, string>(b, n);
    }
}
