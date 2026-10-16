using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// AssetPostprocessor for character bodies under Art/Models/Characters/ (not Props/), TECH_PLAN §7.8 "RocketboxImporter":
    /// <list type="bullet">
    /// <item>Supports both inputs: files fixed offline by the content pipeline (Tools/characters/blender/rb_export.py:
    /// thighs under the pelvis, clavicles under Spine2, T-pose node pose; imported as is) and raw Rocketbox Max exports
    /// (Bip02 renamed, thighs/clavicles re-parented before the avatar is built = FixRocketboxMaxImport with
    /// usingMixamoAnimations = false, LOD meshes other than "hipoly" disabled, and a generated T-pose avatar skeleton).</item>
    /// <item>Humanoid from this model with the explicit Rocketbox bone map (jaw = Bip01 MJaw, eyes mapped); applied by a
    /// one-time second import when Unity's auto-mapping differs (stamp in the importer userData).</item>
    /// <item>Keeps material slot names (ImportViaMaterialDescription, refines Core's "None") so CharacterMaterialBuilder +
    /// ModelMaterialBinder can bind M_* materials by slot; Optimize Game Objects OFF (BodyIK, ragdoll and sockets need the
    /// bone transforms); 4 skin weights; no animation import.</item>
    /// </list>
    /// Runs after Core's ModelImportRules (order -100).
    /// </summary>
    public sealed class RocketboxImporter : AssetPostprocessor
    {
        /// <summary>Bump to re-run on every character after a rule change.</summary>
        public const uint Version = 1;
        /// <summary>userData stamp prefix written after the avatar fix-up import.</summary>
        public const string StampPrefix = "ls-rbx:";

        /// <inheritdoc/>
        public override uint GetVersion() => Version;

        /// <inheritdoc/>
        public override int GetPostprocessOrder() => 10;

        /// <summary>True for body models (Art/Models/Characters/**.fbx except Props/ and the generated mannequin).</summary>
        public static bool IsCharacterModel(string assetPath)
        {
            var rel = ImportRulePaths.ToRuleRelative(assetPath);
            if (rel == null || !ImportRulePaths.Under(rel, "Art/Models/Characters/")) return false;
            if (ImportRulePaths.Under(rel, "Art/Models/Characters/Props/") || ImportRulePaths.Under(rel, "Art/Models/Characters/_Mannequin/")) return false;
            return assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        private void OnPreprocessModel()
        {
            if (!IsCharacterModel(assetPath)) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null || ImportRulePaths.IsOptedOut(mi)) return;
            mi.animationType = ModelImporterAnimationType.Human;
            if (mi.avatarSetup == ModelImporterAvatarSetup.NoAvatar || mi.avatarSetup == ModelImporterAvatarSetup.CopyFromOther)
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.autoGenerateAvatarMappingIfUnspecified = true;
            mi.importAnimation = false;
            mi.optimizeGameObjects = false;
            mi.skinWeights = ModelImporterSkinWeights.Standard;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.isReadable = false;
        }

        private void OnPostprocessMeshHierarchy(GameObject root)
        {
            if (!IsCharacterModel(assetPath) || root == null) return;
            var mi = assetImporter as ModelImporter;
            if (mi != null && ImportRulePaths.IsOptedOut(mi)) return;
            var n = root.name.ToLowerInvariant();
            // Raw Rocketbox files carry hipoly/midpoly/lowpoly/ultralowpoly meshes: keep the hipoly one (FixRocketboxMaxImport).
            if (n.Contains("poly") && !n.Contains("hipoly"))
            {
                root.SetActive(false);
                return;
            }
            if (RocketboxBones.Classify(root.transform) != BipedLayout.Raw) return;
            var count = RocketboxBones.FixHierarchy(root.transform);
            if (count > 0) LSLog.Info(LogCategory.Characters, "RocketboxImporter: raw Biped hierarchy fixed (" + count + " bones re-parented) in " + assetPath);
        }

        private void OnPostprocessModel(GameObject g)
        {
            if (!IsCharacterModel(assetPath) || g == null) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null || ImportRulePaths.IsOptedOut(mi)) return;
            var root = g.transform;
            if (RocketboxBones.Classify(root) == BipedLayout.NotBiped) return; // Meshy fallback etc.: Unity's auto mapping
            if (!RocketboxBones.HasRequiredBones(root))
            {
                LSLog.Warn(LogCategory.Characters, "RocketboxImporter: " + assetPath + " lacks required Humanoid bones; avatar left to Unity.");
                return;
            }
            var wanted = RocketboxBones.BuildHumanBones(root);
            var current = mi.humanDescription;
            var mappingDiffers = !SameMapping(current.human, wanted);
            var skeleton = PoseSkeleton.FromHierarchy(root);
            var needsTPose = ArmsDroop(skeleton) > 12f;
            if (!mappingDiffers && !needsTPose) return;

            var stamp = StampPrefix + "v" + Version + (needsTPose ? ":tpose" : ":map");
            if (!string.IsNullOrEmpty(mi.userData) && mi.userData.Contains(stamp))
            {
                if (mappingDiffers)
                    LSLog.WarnOnce("rbx-map:" + assetPath, LogCategory.Characters, "RocketboxImporter: avatar mapping of " + assetPath +
                                                                                   " still differs after the fix-up import; check the Avatar.");
                return;
            }
            if (needsTPose) TPoseSolver.Apply(skeleton);
            var desc = current;
            desc.human = wanted;
            if (needsTPose || desc.skeleton == null || desc.skeleton.Length == 0) desc.skeleton = skeleton.ToSkeletonBones();
            if (desc.upperArmTwist <= 0f && desc.lowerArmTwist <= 0f) ApplyDefaultMuscleSettings(ref desc);
            var path = assetPath;
            // Importer settings cannot change for the import in progress: apply them and import once more.
            EditorApplication.delayCall += () => ApplyDescription(path, desc, stamp);
        }

        private static void ApplyDescription(string path, HumanDescription desc, string stamp)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return;
            mi.humanDescription = desc;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.userData = WithStamp(mi.userData, stamp);
            mi.SaveAndReimport();
            LSLog.Info(LogCategory.Characters, "RocketboxImporter: avatar of " + path + " rebuilt (" + stamp + ").");
        }

        /// <summary>userData with every previous ls-rbx stamp replaced by <paramref name="stamp"/> (other tags kept).</summary>
        public static string WithStamp(string userData, string stamp)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(userData))
                foreach (var p in userData.Split(' '))
                    if (p.Length > 0 && !p.StartsWith(StampPrefix, StringComparison.Ordinal)) parts.Add(p);
            parts.Add(stamp);
            return string.Join(" ", parts);
        }

        /// <summary>True when every wanted human bone maps to the same transform in <paramref name="current"/>.</summary>
        public static bool SameMapping(HumanBone[] current, HumanBone[] wanted)
        {
            if (current == null || current.Length == 0) return false;
            var map = new Dictionary<string, string>(current.Length);
            foreach (var h in current)
                if (!string.IsNullOrEmpty(h.humanName)) map[h.humanName] = h.boneName;
            foreach (var w in wanted)
                if (!map.TryGetValue(w.humanName, out var b) || b != w.boneName) return false;
            return true;
        }

        /// <summary>Average angle (deg) of the upper arms below horizontal: ~0 in a T-pose, ~40-50 in Rocketbox's A-pose bind.</summary>
        public static float ArmsDroop(PoseSkeleton s)
        {
            var total = 0f;
            var n = 0;
            foreach (var side in new[] { "Bip01 L ", "Bip01 R " })
            {
                if (!s.Has(side + "UpperArm") || !s.Has(side + "Forearm")) continue;
                var d = s.WorldPosition(side + "Forearm") - s.WorldPosition(side + "UpperArm");
                if (d.sqrMagnitude < 1e-8f) continue;
                total += Mathf.Abs(Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg);
                n++;
            }
            return n > 0 ? total / n : 0f;
        }

        private static void ApplyDefaultMuscleSettings(ref HumanDescription d)
        {
            // Unity's Humanoid defaults.
            d.upperArmTwist = 0.5f;
            d.lowerArmTwist = 0.5f;
            d.upperLegTwist = 0.5f;
            d.lowerLegTwist = 0.5f;
            d.armStretch = 0.05f;
            d.legStretch = 0.05f;
            d.feetSpacing = 0f;
            d.hasTranslationDoF = false;
        }
    }
}
