using System;
using System.Collections.Generic;
using System.IO;
using LongShadow.Characters.Body;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// The clip import rules of <see cref="ClipImporter"/> as pure functions (TECH_PLAN §3.11 "Art/Animation/Clips/**:
    /// Humanoid, own avatar per source rig", §7.8 "ClipImporter sets loop flags, root-motion bakes (rotation + Y into
    /// pose), mirror flags"): source per folder, deterministic clip names (A_&lt;Source&gt;[RM]_&lt;Take&gt;), and the
    /// per-take settings (table first, naming heuristics otherwise) including Foot_L/Foot_R events, mirrored duplicates,
    /// alias copies and additive reference poses.
    /// </summary>
    public static class ClipRules
    {
        /// <summary>Takes never imported (bind/reference poses of the clip libraries).</summary>
        public static readonly string[] SkippedTakes = { "A_TPose", "TPose", "T-Pose", "Armature|A_TPose" };

        /// <summary>Source of a clip file by its folder (Art/Animation/Clips/&lt;Source&gt;/...); Other outside known folders.</summary>
        public static ClipSource SourceOf(string assetPath)
        {
            var rel = ImportRulePaths.ToRuleRelative(assetPath);
            var folder = ImportRulePaths.FolderBelow(rel, "Art/Animation/Clips/");
            if (folder == null) return ClipSource.Other;
            switch (folder.ToLowerInvariant())
            {
                case "meshy": return ClipSource.Meshy;
                case "hymotion": return ClipSource.HYMotion;
                case "rocketbox": return ClipSource.Rocketbox;
                case "poses": return ClipSource.Poses;
                case "ual1": return ClipSource.UAL1;
                case "ual2": return ClipSource.UAL2;
                case "sac": return ClipSource.SAC;
                default: return ClipSource.Other;
            }
        }

        /// <summary>True for clip files under Art/Animation/Clips/ (the generated .anim folder holds no models).</summary>
        public static bool IsClipModel(string assetPath)
        {
            var rel = ImportRulePaths.ToRuleRelative(assetPath);
            return rel != null && ImportRulePaths.Under(rel, "Art/Animation/Clips/") &&
                   assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Sources whose clips are baked on the Rocketbox skeleton and must copy the reference avatar (SK_Calder).</summary>
        public static bool CopiesReferenceAvatar(ClipSource s) =>
            s == ClipSource.Meshy || s == ClipSource.HYMotion || s == ClipSource.Rocketbox || s == ClipSource.Poses;

        /// <summary>
        /// Model whose avatar a clip file copies: SK_Calder for clips baked on the Rocketbox skeleton, the SAC rig for
        /// A_SAC_* clips; null when the file builds its own avatar (UAL libraries, the SAC rig itself, unknown folders).
        /// </summary>
        public static string AvatarSourceFor(string assetPath)
        {
            var source = SourceOf(assetPath);
            if (CopiesReferenceAvatar(source)) return CharacterPaths.ReferenceModel;
            if (source == ClipSource.SAC && !IsSacRig(assetPath)) return CharacterPaths.SacRigModel;
            return null;
        }

        /// <summary>The SAC rig file (avatar source, not a clip).</summary>
        public static bool IsSacRig(string assetPath) =>
            string.Equals(Path.GetFileName(assetPath), Path.GetFileName(CharacterPaths.SacRigModel), StringComparison.OrdinalIgnoreCase);

        /// <summary>Root-motion variant file (Quaternius "UAL1_Standard_RM.fbx").</summary>
        public static bool IsRootMotionFile(string assetPath) =>
            Path.GetFileNameWithoutExtension(assetPath).EndsWith("_RM", StringComparison.OrdinalIgnoreCase);

        /// <summary>Deterministic clip name for a take.</summary>
        public static string ClipName(ClipSource source, string fileStem, string takeName, int takeCount, bool rmFile)
        {
            var take = CleanTake(takeName);
            switch (source)
            {
                case ClipSource.UAL1:
                case ClipSource.UAL2:
                    return "A_" + source + (rmFile ? "RM" : "") + "_" + take;
                case ClipSource.SAC:
                {
                    if (fileStem.StartsWith("A_", StringComparison.Ordinal))
                        return takeCount <= 1 ? fileStem : fileStem + "_" + take;
                    var at = fileStem.IndexOf('@');
                    var stem = at >= 0 ? fileStem.Substring(at + 1) : fileStem;
                    return takeCount <= 1 ? "A_SAC_" + stem : "A_SAC_" + stem + "_" + take;
                }
                default:
                    if (fileStem.StartsWith("A_", StringComparison.Ordinal))
                        return takeCount <= 1 ? fileStem : fileStem + "_" + take;
                    return "A_" + source + "_" + (takeCount <= 1 ? fileStem : fileStem + "_" + take);
            }
        }

        /// <summary>"Armature|Idle_Loop" → "Idle_Loop"; spaces become underscores.</summary>
        public static string CleanTake(string take)
        {
            if (string.IsNullOrEmpty(take)) return "Take";
            var bar = take.LastIndexOf('|');
            if (bar >= 0 && bar < take.Length - 1) take = take.Substring(bar + 1);
            return take.Trim().Replace(' ', '_');
        }

        /// <summary>Settings for a clip: the table row, else heuristics from the name and source.</summary>
        public static ClipSetting SettingFor(string clipName, ClipSource source, bool rmFile)
        {
            if (ClipSettingsTable.TryGet(clipName, out var row)) return row;
            var n = clipName;
            var turn = n.IndexOf("Turn", StringComparison.OrdinalIgnoreCase) >= 0;
            var loop = n.EndsWith("_Loop", StringComparison.OrdinalIgnoreCase) || n.IndexOf("Inplace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       (source == ClipSource.SAC && !turn && n.IndexOf("Land", StringComparison.OrdinalIgnoreCase) < 0);
            var additive = n.IndexOf("Hit_", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("HitReaction", StringComparison.OrdinalIgnoreCase) >= 0;
            var mirror = source == ClipSource.SAC && n.IndexOf("Right", StringComparison.Ordinal) >= 0;
            return new ClipSetting(clipName, loop, rmFile && !loop, bakeRotation: !turn, mirror: mirror, additive: additive);
        }

        /// <summary>True when the root height must stay root motion (climbs and drops change level).</summary>
        public static bool KeepsRootHeight(string clipName) =>
            clipName.IndexOf("Climb", StringComparison.OrdinalIgnoreCase) >= 0 ||
            clipName.IndexOf("JumpingDown", StringComparison.OrdinalIgnoreCase) >= 0 ||
            clipName.IndexOf("DiveDown", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The clip list for an importer (one entry per take, plus mirrored duplicates).</summary>
        public static ModelImporterClipAnimation[] Configure(string assetPath, ModelImporterClipAnimation[] defaults)
        {
            var result = new List<ModelImporterClipAnimation>();
            if (defaults == null) return result.ToArray();
            var source = SourceOf(assetPath);
            var rm = IsRootMotionFile(assetPath);
            var stem = Path.GetFileNameWithoutExtension(assetPath);
            var takes = new List<ModelImporterClipAnimation>();
            foreach (var d in defaults)
            {
                if (d == null) continue;
                var take = CleanTake(d.takeName);
                if (Array.IndexOf(SkippedTakes, take) >= 0 || Array.IndexOf(SkippedTakes, d.takeName) >= 0) continue;
                takes.Add(d);
            }
            foreach (var d in takes)
            {
                var name = ClipName(source, stem, d.takeName, takes.Count, rm);
                var s = SettingFor(name, source, rm);
                result.Add(Make(d, name, s, false));
                if (s.Mirror) result.Add(Make(d, name + "_Mirror", s, true));
                if (s.Aliases != null)
                    foreach (var suffix in s.Aliases) result.Add(Make(d, name + suffix, s, false));
            }
            return result.ToArray();
        }

        private static ModelImporterClipAnimation Make(ModelImporterClipAnimation d, string name, ClipSetting s, bool mirrored)
        {
            var keepHeight = s.RootMotion && KeepsRootHeight(name);
            return new ModelImporterClipAnimation
            {
                name = name,
                takeName = d.takeName,
                firstFrame = s.FirstFrame >= 0 && s.LastFrame > s.FirstFrame ? Mathf.Max(d.firstFrame, s.FirstFrame) : d.firstFrame,
                lastFrame = s.FirstFrame >= 0 && s.LastFrame > s.FirstFrame ? Mathf.Min(d.lastFrame, s.LastFrame) : d.lastFrame,
                loopTime = s.Loop,
                loopPose = s.Loop,
                cycleOffset = 0f,
                lockRootRotation = s.BakeRotation,
                keepOriginalOrientation = false,
                lockRootHeightY = !keepHeight,
                keepOriginalPositionY = s.OriginalHeight,
                heightFromFeet = !s.OriginalHeight,
                lockRootPositionXZ = !s.RootMotion,
                keepOriginalPositionXZ = false,
                mirror = mirrored,
                maskType = ClipAnimationMaskType.None,
                hasAdditiveReferencePose = s.Additive,
                additiveReferencePoseFrame = s.FirstFrame >= 0 ? Mathf.Max(d.firstFrame, s.FirstFrame) : d.firstFrame,
                events = FootEvents(s, mirrored),
            };
        }

        /// <summary>Foot plant events (normalised times) as AnimEventRelay events; mirrored clips swap the feet.</summary>
        public static AnimationEvent[] FootEvents(ClipSetting s, bool mirrored)
        {
            var list = new List<AnimationEvent>();
            Add(list, mirrored ? s.FootR : s.FootL, AnimEventIds.FootL);
            Add(list, mirrored ? s.FootL : s.FootR, AnimEventIds.FootR);
            list.Sort((a, b) => a.time.CompareTo(b.time));
            return list.ToArray();
        }

        private static void Add(List<AnimationEvent> list, float[] times, string id)
        {
            if (times == null) return;
            foreach (var t in times)
                list.Add(new AnimationEvent { time = Mathf.Clamp01(t), functionName = AnimEventRelay.EventFunction, stringParameter = id });
        }
    }
}
