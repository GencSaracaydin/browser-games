using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// AssetPostprocessor for every clip FBX under Art/Animation/Clips/&lt;Source&gt;/ (TECH_PLAN §7.8 ClipImporter, §8.6;
    /// Docs/handoff/WP05_ART.md §2): Humanoid with the right avatar per source (clips baked on the Rocketbox skeleton copy
    /// SK_Calder's avatar, A_SAC_* clips copy SK_SAC_DefaultMaleRig's, the Quaternius UAL libraries and the SAC rig build
    /// their own; UAL needs Bake Axis Conversion), then the per-take settings of <see cref="ClipRules"/>: names, loop,
    /// root-motion bakes, mirrors, aliases, additive references and foot events. Runs after Core's ModelImportRules (order
    /// -100), which already set Humanoid / Optimal compression / 0.5 errors. Clips that need an avatar source before it
    /// exists are re-imported automatically when it lands.
    /// </summary>
    public sealed class ClipImporter : AssetPostprocessor
    {
        /// <summary>Bump to re-import every clip after a rule change.</summary>
        public const uint Version = 2;

        private static readonly Dictionary<string, HashSet<string>> s_waiting = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        /// <inheritdoc/>
        public override uint GetVersion() => Version;

        /// <inheritdoc/>
        public override int GetPostprocessOrder() => 20;

        private void OnPreprocessModel()
        {
            if (!ClipRules.IsClipModel(assetPath)) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null || ImportRulePaths.IsOptedOut(mi)) return;
            var source = ClipRules.SourceOf(assetPath);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.importAnimation = !ClipRules.IsSacRig(assetPath);
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importBlendShapes = false;
            mi.importConstraints = false;
            mi.resampleCurves = true;
            // Quaternius UAL is authored for Bake Axis Conversion (WP05_ART §2; Core's rules turn it off for Blender FBX).
            if (source == ClipSource.UAL1 || source == ClipSource.UAL2) mi.bakeAxisConversion = true;
            // Two-frame static finger poses: no keyframe reduction.
            if (source == ClipSource.Poses) mi.animationCompression = ModelImporterAnimationCompression.Off;

            var avatarPath = ClipRules.AvatarSourceFor(assetPath);
            if (avatarPath == null)
            {
                if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                return;
            }
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
            if (avatar != null && avatar.isValid && avatar.isHuman)
            {
                context.DependsOnArtifact(avatarPath);
                if (mi.avatarSetup != ModelImporterAvatarSetup.CopyFromOther || mi.sourceAvatar != avatar)
                {
                    mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    mi.sourceAvatar = avatar;
                }
                if (s_waiting.TryGetValue(avatarPath, out var set)) set.Remove(assetPath);
            }
            else
            {
                // Armature-only clip files have no T-pose: never build their own avatar. Wait for the avatar source.
                context.DependsOnSourceAsset(avatarPath);
                if (!s_waiting.TryGetValue(avatarPath, out var set)) s_waiting[avatarPath] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(assetPath);
                LSLog.WarnOnce("clip-noavatar:" + avatarPath, LogCategory.Characters,
                    "ClipImporter: " + avatarPath + " is not imported yet; its clips import without a valid avatar and are re-imported " +
                    "automatically when it lands (or run LongShadow/Characters/Reimport Clips).");
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!ClipRules.IsClipModel(assetPath) || ClipRules.IsSacRig(assetPath)) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null || ImportRulePaths.IsOptedOut(mi)) return;
            mi.clipAnimations = ClipRules.Configure(assetPath, mi.defaultClipAnimations);
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (s_waiting.Count == 0) return;
            List<string> pending = null;
            foreach (var p in imported)
            {
                if (!s_waiting.TryGetValue(p, out var set) || set.Count == 0) continue;
                pending = pending ?? new List<string>();
                pending.AddRange(set);
                set.Clear();
            }
            if (pending != null) EditorApplication.delayCall += () => Reimport(pending);
        }

        /// <summary>Force-reimports model files (after an avatar source changed).</summary>
        public static void Reimport(IList<string> paths)
        {
            if (paths == null || paths.Count == 0) return;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var p in paths) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            LSLog.Info(LogCategory.Characters, "ClipImporter: re-imported " + paths.Count + " file(s).");
        }

        /// <summary>Every clip FBX under Art/Animation/Clips (avatar sources first, so copies find them).</summary>
        public static List<string> AllClipFiles()
        {
            var list = new List<string>();
            if (!AssetDatabase.IsValidFolder(CharacterPaths.ClipsRoot)) return list;
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { CharacterPaths.ClipsRoot }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (ClipRules.IsClipModel(p)) list.Add(p);
            }
            list.Sort((a, b) =>
            {
                var ra = ClipRules.IsSacRig(a) ? 0 : 1;
                var rb = ClipRules.IsSacRig(b) ? 0 : 1;
                return ra != rb ? ra.CompareTo(rb) : string.CompareOrdinal(a, b);
            });
            return list;
        }
    }
}
