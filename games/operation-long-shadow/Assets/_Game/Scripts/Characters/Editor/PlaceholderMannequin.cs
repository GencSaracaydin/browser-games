using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// A generated 1.78 m Humanoid stand-in for bodies whose FBX has not landed (TECH_PLAN: degrade gracefully): a real
    /// bone hierarchy in a T-pose with a Humanoid avatar built by <see cref="AvatarBuilder"/>
    /// (Art/Models/Characters/_Mannequin/AV_Mannequin.asset) and greybox primitives parented to the bones, so every clip,
    /// the IK, the sockets and the ragdoll work exactly as on a Rocketbox body. Bone names follow the Rocketbox Biped so
    /// the same tools apply.
    /// </summary>
    public static class PlaceholderMannequin
    {
        /// <summary>Saved avatar of the mannequin.</summary>
        public const string AvatarPath = CharacterPaths.MannequinFolder + "/AV_Mannequin.asset";
        /// <summary>Standing height (m).</summary>
        public const float Height = 1.78f;

        // name, parent, local position (T-pose, character faces +Z, left = -X)
        private static readonly (string name, string parent, Vector3 pos)[] s_bones =
        {
            ("Bip01", null, new Vector3(0f, 0f, 0f)),
            ("Bip01 Pelvis", "Bip01", new Vector3(0f, 0.95f, 0f)),
            ("Bip01 Spine", "Bip01 Pelvis", new Vector3(0f, 0.10f, 0f)),
            ("Bip01 Spine1", "Bip01 Spine", new Vector3(0f, 0.13f, 0.005f)),
            ("Bip01 Spine2", "Bip01 Spine1", new Vector3(0f, 0.13f, 0f)),
            ("Bip01 Neck", "Bip01 Spine2", new Vector3(0f, 0.17f, -0.01f)),
            ("Bip01 Head", "Bip01 Neck", new Vector3(0f, 0.09f, 0.02f)),
            ("Bip01 L Clavicle", "Bip01 Spine2", new Vector3(-0.04f, 0.12f, 0f)),
            ("Bip01 L UpperArm", "Bip01 L Clavicle", new Vector3(-0.15f, 0f, 0f)),
            ("Bip01 L Forearm", "Bip01 L UpperArm", new Vector3(-0.28f, 0f, 0f)),
            ("Bip01 L Hand", "Bip01 L Forearm", new Vector3(-0.26f, 0f, 0f)),
            ("Bip01 R Clavicle", "Bip01 Spine2", new Vector3(0.04f, 0.12f, 0f)),
            ("Bip01 R UpperArm", "Bip01 R Clavicle", new Vector3(0.15f, 0f, 0f)),
            ("Bip01 R Forearm", "Bip01 R UpperArm", new Vector3(0.28f, 0f, 0f)),
            ("Bip01 R Hand", "Bip01 R Forearm", new Vector3(0.26f, 0f, 0f)),
            ("Bip01 L Thigh", "Bip01 Pelvis", new Vector3(-0.09f, -0.03f, 0f)),
            ("Bip01 L Calf", "Bip01 L Thigh", new Vector3(0f, -0.43f, 0f)),
            ("Bip01 L Foot", "Bip01 L Calf", new Vector3(0f, -0.41f, 0f)),
            ("Bip01 L Toe0", "Bip01 L Foot", new Vector3(0f, -0.06f, 0.13f)),
            ("Bip01 R Thigh", "Bip01 Pelvis", new Vector3(0.09f, -0.03f, 0f)),
            ("Bip01 R Calf", "Bip01 R Thigh", new Vector3(0f, -0.43f, 0f)),
            ("Bip01 R Foot", "Bip01 R Calf", new Vector3(0f, -0.41f, 0f)),
            ("Bip01 R Toe0", "Bip01 R Foot", new Vector3(0f, -0.06f, 0.13f)),
        };

        // bone, primitive, local centre, size
        private static readonly (string bone, PrimitiveType type, Vector3 centre, Vector3 size)[] s_parts =
        {
            ("Bip01 Pelvis", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.32f, 0.18f, 0.2f)),
            ("Bip01 Spine1", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.34f, 0.22f)),
            ("Bip01 Head", PrimitiveType.Sphere, new Vector3(0f, 0.09f, 0.01f), new Vector3(0.2f, 0.24f, 0.22f)),
            ("Bip01 Neck", PrimitiveType.Capsule, new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.06f, 0.1f)),
            ("Bip01 L UpperArm", PrimitiveType.Capsule, new Vector3(-0.14f, 0f, 0f), new Vector3(0.1f, 0.15f, 0.1f)),
            ("Bip01 L Forearm", PrimitiveType.Capsule, new Vector3(-0.13f, 0f, 0f), new Vector3(0.085f, 0.14f, 0.085f)),
            ("Bip01 L Hand", PrimitiveType.Cube, new Vector3(-0.08f, 0f, 0f), new Vector3(0.16f, 0.03f, 0.09f)),
            ("Bip01 R UpperArm", PrimitiveType.Capsule, new Vector3(0.14f, 0f, 0f), new Vector3(0.1f, 0.15f, 0.1f)),
            ("Bip01 R Forearm", PrimitiveType.Capsule, new Vector3(0.13f, 0f, 0f), new Vector3(0.085f, 0.14f, 0.085f)),
            ("Bip01 R Hand", PrimitiveType.Cube, new Vector3(0.08f, 0f, 0f), new Vector3(0.16f, 0.03f, 0.09f)),
            ("Bip01 L Thigh", PrimitiveType.Capsule, new Vector3(0f, -0.21f, 0f), new Vector3(0.15f, 0.22f, 0.15f)),
            ("Bip01 L Calf", PrimitiveType.Capsule, new Vector3(0f, -0.2f, 0f), new Vector3(0.11f, 0.21f, 0.11f)),
            ("Bip01 L Foot", PrimitiveType.Cube, new Vector3(0f, -0.04f, 0.06f), new Vector3(0.1f, 0.08f, 0.26f)),
            ("Bip01 R Thigh", PrimitiveType.Capsule, new Vector3(0f, -0.21f, 0f), new Vector3(0.15f, 0.22f, 0.15f)),
            ("Bip01 R Calf", PrimitiveType.Capsule, new Vector3(0f, -0.2f, 0f), new Vector3(0.11f, 0.21f, 0.11f)),
            ("Bip01 R Foot", PrimitiveType.Cube, new Vector3(0f, -0.04f, 0.06f), new Vector3(0.1f, 0.08f, 0.26f)),
        };

        /// <summary>
        /// Creates a scene instance (root named <paramref name="name"/>) with an Animator using the saved mannequin avatar
        /// (built on first use) and greybox parts in <paramref name="color"/>.
        /// </summary>
        public static GameObject CreateInstance(string name, Color color) => CreateInstance(name, color, true);

        /// <summary>
        /// As <see cref="CreateInstance(string, Color)"/>; with <paramref name="persistent"/> false nothing is written to the
        /// project (in-memory avatar and material: EditMode tests).
        /// </summary>
        public static GameObject CreateInstance(string name, Color color, bool persistent)
        {
            var root = BuildSkeleton(name);
            var animator = root.AddComponent<Animator>();
            animator.avatar = persistent ? GetOrBuildAvatar() : BuildAvatarInMemory();
            animator.applyRootMotion = false;
            var mat = persistent ? BuilderUtil.PlaceholderMaterial(color) : Placeholders.Material(color);
            foreach (var p in s_parts)
            {
                var bone = RocketboxBones.FindDeep(root.transform, p.bone);
                if (bone == null) continue;
                var go = GameObject.CreatePrimitive(p.type);
                go.name = "PH_" + p.bone.Replace("Bip01 ", "").Replace(' ', '_');
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(bone, false);
                go.transform.localPosition = p.centre;
                // Limb capsules lie along the bone (X for arms, Y for legs).
                if (p.type == PrimitiveType.Capsule && Mathf.Abs(p.centre.x) > Mathf.Abs(p.centre.y))
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                go.transform.localScale = p.size;
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            return root;
        }

        /// <summary>The mannequin avatar asset (built and saved on first use).</summary>
        public static Avatar GetOrBuildAvatar()
        {
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            if (avatar != null) return avatar;
            var temp = BuildSkeleton("SK_Mannequin");
            try
            {
                avatar = AvatarBuilder.BuildHumanAvatar(temp, Describe(temp.transform));
                avatar.name = "AV_Mannequin";
                if (!avatar.isValid || !avatar.isHuman)
                {
                    LSLog.Error(LogCategory.Characters, "PlaceholderMannequin: generated avatar is invalid.");
                    return avatar;
                }
                BuilderUtil.EnsureFolder(CharacterPaths.MannequinFolder);
                AssetDatabase.CreateAsset(avatar, AvatarPath);
                return avatar;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        /// <summary>Builds the mannequin avatar without saving it.</summary>
        public static Avatar BuildAvatarInMemory()
        {
            var temp = BuildSkeleton("SK_Mannequin");
            try
            {
                var avatar = AvatarBuilder.BuildHumanAvatar(temp, Describe(temp.transform));
                avatar.name = "AV_Mannequin (memory)";
                return avatar;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        /// <summary>The Humanoid description of a mannequin hierarchy (T-pose skeleton = its current pose).</summary>
        public static HumanDescription Describe(Transform root)
        {
            var skeleton = new List<SkeletonBone>();
            Collect(root, skeleton, true);
            return new HumanDescription
            {
                human = RocketboxBones.BuildHumanBones(root),
                skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
        }

        private static void Collect(Transform t, List<SkeletonBone> list, bool isRoot)
        {
            list.Add(new SkeletonBone
            {
                name = t.name,
                position = isRoot ? Vector3.zero : t.localPosition,
                rotation = isRoot ? Quaternion.identity : t.localRotation,
                scale = isRoot ? Vector3.one : t.localScale,
            });
            for (var i = 0; i < t.childCount; i++) Collect(t.GetChild(i), list, false);
        }

        private static GameObject BuildSkeleton(string name)
        {
            var map = new Dictionary<string, Transform>();
            GameObject root = null;
            foreach (var b in s_bones)
            {
                var go = new GameObject(b.name);
                if (b.parent == null)
                {
                    root = new GameObject(name);
                    go.transform.SetParent(root.transform, false);
                }
                else go.transform.SetParent(map[b.parent], false);
                go.transform.localPosition = b.pos;
                map[b.name] = go.transform;
            }
            return root;
        }
    }
}
