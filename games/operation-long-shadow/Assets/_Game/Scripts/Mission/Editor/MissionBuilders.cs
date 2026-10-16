using System.Collections.Generic;
using System.IO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.Mission.Data;
using LongShadow.Mission.Dev;
using LongShadow.Mission.Objectives;
using LongShadow.Mission.Runtime;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Mission.Editor
{
    /// <summary>
    /// Builders for the mission's assets (prefer builders over hand-authored assets): <c>PF_MissionSystems</c> (the host
    /// that S_Persistent needs) and <c>LIB_Mission</c> (intel pickup models and intel-card photos, looked up by
    /// ASSET_MANIFEST id; missing assets stay empty and the runtime uses greybox pickups). Re-running a builder after an
    /// asset lands upgrades the asset in place (GUIDs kept).
    /// </summary>
    public static class MissionBuilders
    {
        /// <summary>Prefab path of the mission host (WP11 places it in S_Persistent).</summary>
        public const string SystemsPrefabPath = "Assets/_Game/Prefabs/Systems/PF_MissionSystems.prefab";

        /// <summary>Builds LIB_Mission and PF_MissionSystems.</summary>
        [MenuItem("LongShadow/Mission/Build All", false, 1)]
        public static void BuildAll()
        {
            BuildLibrary();
            BuildSystemsPrefab();
        }

        /// <summary>Builds (or updates) Assets/_Game/Data/Libraries/LIB_Mission.asset.</summary>
        [MenuItem("LongShadow/Mission/Build Mission Library", false, 2)]
        public static MissionAssetLibrary BuildLibrary()
        {
            var lib = BuilderUtil.LoadOrCreateAsset<MissionAssetLibrary>(MissionAssetLibrary.AssetPath);
            var entries = new List<MissionAssetEntry>();
            int models = 0, photos = 0;
            foreach (var intel in IntelTable.All)
            {
                GameObject model = null;
                if (!string.IsNullOrEmpty(intel.PropManifestId))
                {
                    model = AssetManifest.FindAll<GameObject>(intel.PropManifestId).Find(g => g != null);
                    if (model == null) LSLog.MissingAsset(LogCategory.Mission, intel.PropManifestId, AssetManifest.ResolvePath(intel.PropManifestId));
                }
                var photo = FindPhoto(intel);
                if (model != null) models++;
                if (photo != null) photos++;
                entries.Add(new MissionAssetEntry(intel.Id, model, photo));
            }
            lib.EditorSetEntries(entries);
            AssetDatabase.SaveAssets();
            LSLog.Info(LogCategory.Mission, "LIB_Mission: " + entries.Count + " intel entries (" + models + " models, " + photos + " photos; the rest greybox).");
            return lib;
        }

        /// <summary>Builds (or updates) Assets/_Game/Prefabs/Systems/PF_MissionSystems.prefab.</summary>
        [MenuItem("LongShadow/Mission/Build Mission Systems Prefab", false, 3)]
        public static GameObject BuildSystemsPrefab()
        {
            var root = new GameObject("PF_MissionSystems");
            var host = root.AddComponent<MissionSystems>();
            var lib = AssetDatabase.LoadAssetAtPath<MissionAssetLibrary>(MissionAssetLibrary.AssetPath);
            var so = new SerializedObject(host);
            so.FindProperty("_library").objectReferenceValue = lib;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = BuilderUtil.SavePrefab(root, SystemsPrefabPath);
            LSLog.Info(LogCategory.Mission, "Built " + SystemsPrefabPath + (lib == null ? " (no LIB_Mission yet: run Build Mission Library)" : ""));
            return prefab;
        }

        private static Sprite FindPhoto(IntelDef intel)
        {
            var sprite = AssetManifest.FindAll<Sprite>(intel.PhotoManifestId).Find(s => s != null);
            if (sprite != null) return sprite;
            // WP09 ships the photos as T_<Name>_BC.png (DECISIONS [WP09]); find them by name under Art/Textures/Images.
            var folder = "Assets/_Game/Art/Textures/Images";
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (var guid in AssetDatabase.FindAssets("T_" + intel.PhotoName, new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is Sprite s) return s;
                LSLog.WarnOnce("mission-photo-notsprite:" + path, LogCategory.Mission,
                    path + " is not imported as a Sprite; the intel card shows no photo until it is.");
            }
            LSLog.MissingAsset(LogCategory.Mission, intel.PhotoManifestId, AssetManifest.ResolvePath(intel.PhotoManifestId));
            return null;
        }

        /// <summary>Checks the data tables against DIALOGUE.json, markup_ids.csv and GDD §7.12; writes Logs/mission_validation.txt.</summary>
        [MenuItem("LongShadow/Mission/Validate Mission Data", false, 20)]
        public static bool ValidateData()
        {
            var problems = MissionDataValidator.Validate(BuilderUtil.ProjectRoot);
            var report = MissionDataValidator.Report(problems);
            WriteLog("mission_validation.txt", report);
            if (problems.Count == 0) LSLog.Info(LogCategory.Mission, report);
            else LSLog.Warn(LogCategory.Mission, report);
            return problems.Count == 0;
        }

        /// <summary>Lists every mission scene id missing from the open scenes, per beat (Logs/mission_markup.txt).</summary>
        [MenuItem("LongShadow/Mission/Validate Scene Markup (Open Scenes)", false, 21)]
        public static int ValidateSceneMarkup()
        {
            var present = new HashSet<string>();
            foreach (var m in Object.FindObjectsByType<SceneMarker>(FindObjectsInactive.Include))
                if (!string.IsNullOrEmpty(m.Id)) present.Add(m.Id);
            var sb = new System.Text.StringBuilder();
            var missing = 0;
            for (var b = 1; b <= BeatTable.PlayableBeats; b++)
            {
                var ids = MissionMarkup.Required(b);
                var lines = new List<string>();
                foreach (var id in ids)
                    if (!present.Contains(id)) lines.Add(id);
                missing += lines.Count;
                sb.AppendLine("Beat " + b + ": " + (ids.Count - lines.Count) + "/" + ids.Count + " present" +
                              (lines.Count > 0 ? "; missing: " + string.Join(", ", lines) : ""));
            }
            WriteLog("mission_markup.txt", sb.ToString());
            LSLog.Info(LogCategory.Mission, "Mission markup in the open scenes: " + missing + " id(s) missing (Logs/mission_markup.txt)\n" + sb);
            return missing;
        }

        /// <summary>Starts the smoke test (play mode only).</summary>
        [MenuItem("LongShadow/Mission/Run Smoke Test (Play Mode)", false, 40)]
        public static void RunSmokeTest()
        {
            if (!Application.isPlaying)
            {
                LSLog.Warn(LogCategory.Mission, "Enter play mode (S_Boot or a scene with PF_MissionSystems) first.");
                return;
            }
            MissionSmokeTest.Run(null);
        }

        private static void WriteLog(string file, string text)
        {
            var dir = Path.Combine(BuilderUtil.ProjectRoot, "Logs");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, file), text);
        }
    }
}
