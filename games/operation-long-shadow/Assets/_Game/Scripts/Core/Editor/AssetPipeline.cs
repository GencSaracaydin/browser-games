using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Menu items and batch entry points of the WP00 Phase B asset pipeline (LongShadow/Import/... and
    /// LongShadow/Materials/...). Every menu item calls a public API so agents can run the same thing through
    /// Unity_RunCommand: <see cref="ReapplyRules"/>, <see cref="ReapplyAllGameAssets"/>, <see cref="ImportRuleReport.Run"/>,
    /// <see cref="ReadyContentCheck.CompareNotes"/>, <see cref="ReadyContentCheck.ExportRuleTable"/>,
    /// <see cref="ImportRuleSelfTest.Run"/>, <see cref="MaterialFactory.BuildFromReadySpecs"/>,
    /// <see cref="MaterialFactory.BuildAllInFolder"/>, <see cref="ModelMaterialBinder.BindAll"/>.
    /// </summary>
    public static class AssetPipeline
    {
        private const string Import = "LongShadow/Import/";
        private const string Materials = "LongShadow/Materials/";

        /// <summary>Folders the rules manage (reimport scope of "Reapply Rules (All Game Assets)").</summary>
        public static readonly string[] ManagedFolders =
        {
            "Assets/_Game/Art/Textures", "Assets/_Game/Art/HDRI", "Assets/_Game/UI/Sprites", "Assets/_Game/Art/Models",
            "Assets/_Game/Art/Animation", "Assets/_Game/Audio",
        };

        [MenuItem(Import + "Reapply Rules (Selection)", priority = 1)]
        private static void ReapplySelection() => LSLog.Info(LogCategory.Assets, "Reimported " + ReapplyRules(SelectedPaths().ToArray()) + " asset(s) with the import rules.");

        [MenuItem(Import + "Reapply Rules (All Game Assets)", priority = 2)]
        private static void ReapplyAll() => LSLog.Info(LogCategory.Assets, "Reimported " + ReapplyAllGameAssets() + " asset(s) with the import rules.");

        [MenuItem(Import + "Check Rules (Report)", priority = 20)]
        private static void CheckRules()
        {
            var s = ImportRuleReport.Run();
            if (s.Mismatched > 0) LSLog.Warn(LogCategory.Assets, "Import rule report: " + s);
            else LSLog.Info(LogCategory.Assets, "Import rule report: " + s);
        }

        [MenuItem(Import + "Explain Rule For Selection", priority = 21)]
        private static void Explain()
        {
            foreach (var p in SelectedPaths())
            {
                var line = ImportRuleReport.Check(p, out _, out _);
                LSLog.Info(LogCategory.Assets, line ?? ("no import rule for " + p));
            }
        }

        [MenuItem(Import + "Compare Rules With Ready Notes", priority = 22)]
        private static void CompareReady()
        {
            var sum = ReadyContentCheck.CompareNotes();
            LSLog.Info(LogCategory.Assets, "Ready content vs import rules: " + sum);
        }

        [MenuItem(Import + "Export Rule Table (validate_assets.py)", priority = 23)]
        private static void ExportTable() =>
            LSLog.Info(LogCategory.Assets, "Exported " + ReadyContentCheck.ExportRuleTable() + " texture rule(s) to " + ReadyContentCheck.RuleTablePath);

        [MenuItem(Import + "Run Rule Self-Test", priority = 40)]
        private static void SelfTest() => ImportRuleSelfTest.Run();

        [MenuItem(Import + "Verbose Rule Logging", priority = 60)]
        private static void ToggleVerbose() => EditorPrefs.SetBool(TextureImportRules.VerbosePref, !EditorPrefs.GetBool(TextureImportRules.VerbosePref, false));

        [MenuItem(Import + "Verbose Rule Logging", true)]
        private static bool ToggleVerboseValidate()
        {
            Menu.SetChecked(Import + "Verbose Rule Logging", EditorPrefs.GetBool(TextureImportRules.VerbosePref, false));
            return true;
        }

        [MenuItem(Materials + "Build From Selected Texture Sets", priority = 1)]
        private static void BuildSelected()
        {
            var built = 0;
            var seen = new HashSet<string>();
            foreach (var p in SelectedPaths())
            {
                var set = TextureSet.FromMember(p);
                if (set == null || !seen.Add(set.Folder + "/" + set.Name)) continue;
                if (MaterialFactory.BuildFromTextureSet(set) != null) built++;
            }
            AssetDatabase.SaveAssets();
            LSLog.Info(LogCategory.Assets, "MaterialFactory: built " + built + " material(s) from the selection.");
        }

        [MenuItem(Materials + "Build All Texture Sets", priority = 2)]
        private static void BuildAll()
        {
            var list = MaterialFactory.BuildAllInFolder("Assets/_Game/Art/Textures");
            LSLog.Info(LogCategory.Assets, "MaterialFactory: built " + list.Count + " material(s) from Art/Textures.");
        }

        [MenuItem(Materials + "Build From Ready Specs", priority = 3)]
        private static void BuildSpecs() => LSLog.Info(LogCategory.Assets, "MaterialFactory: built " + MaterialFactory.BuildFromReadySpecs() + " material(s) from spec files.");

        [MenuItem(Materials + "Build From Spec File...", priority = 4)]
        private static void BuildSpecFile()
        {
            var path = EditorUtility.OpenFilePanel("Material spec (JSON)", BuilderUtil.ProjectRoot, "json");
            if (!string.IsNullOrEmpty(path)) MaterialFactory.BuildFromSpecFile(path);
        }

        [MenuItem(Materials + "Bind Model Materials (Selection)", priority = 20)]
        private static void BindSelected()
        {
            foreach (var p in SelectedPaths())
                if (AssetImporter.GetAtPath(p) is ModelImporter)
                    LSLog.Info(LogCategory.Assets, "Bind " + ModelMaterialBinder.Bind(p));
        }

        [MenuItem(Materials + "Bind All Model Materials", priority = 21)]
        private static void BindAll()
        {
            var results = ModelMaterialBinder.BindAll();
            int bound = 0, unbound = 0;
            foreach (var r in results)
            {
                bound += r.Bound.Count;
                unbound += r.Unbound.Count;
            }
            LSLog.Info(LogCategory.Assets, "ModelMaterialBinder: " + results.Count + " model(s), " + bound + " slot(s) bound, " + unbound + " without a material.");
        }

        [MenuItem(Materials + "Pack Mask From Selection", priority = 40)]
        private static void PackSelected()
        {
            foreach (var p in SelectedPaths())
            {
                var set = TextureSet.FromMember(p);
                var src = MaskPacker.FromLooseMaps(set);
                if (src == null)
                {
                    LSLog.Warn(LogCategory.Assets, "Pack Mask: no loose ARM/MR/roughness/AO/metallic maps next to " + p);
                    continue;
                }
                var outPath = set.Folder + "/" + TextureNaming.FileStem(set.Name, TextureSuffix.M) + ".png";
                var written = MaskPacker.Pack(src, outPath);
                if (written != null) LSLog.Info(LogCategory.Assets, "Packed " + written);
            }
        }

        /// <summary>Reimports every asset in <see cref="ManagedFolders"/> (after a rule or manifest budget change).</summary>
        public static int ReapplyAllGameAssets() => ReapplyRules(ManagedFolders);

        /// <summary>
        /// Force-reimports every texture, model and audio file under the given folders/files so the current rules apply
        /// (re-reads the manifest budgets first). Returns the number of files reimported.
        /// </summary>
        public static int ReapplyRules(params string[] foldersOrFiles)
        {
            TextureBudgets.Reload(); // also reloads the manifest and Tools/build/asset_budgets.json
            TextureSourceProbe.ClearCache();
            var files = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in foldersOrFiles)
            {
                if (AssetDatabase.IsValidFolder(r))
                {
                    foreach (var guid in AssetDatabase.FindAssets("", new[] { r }))
                    {
                        var p = AssetDatabase.GUIDToAssetPath(guid);
                        if (!AssetDatabase.IsValidFolder(p) && seen.Add(p)) files.Add(p);
                    }
                }
                else if (File.Exists(Path.Combine(BuilderUtil.ProjectRoot, r)) && seen.Add(r)) files.Add(r);
            }
            var count = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var p in files)
                {
                    var importer = AssetImporter.GetAtPath(p);
                    if (!(importer is TextureImporter || importer is ModelImporter || importer is AudioImporter)) continue;
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return count;
        }

        private static List<string> SelectedPaths()
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in Selection.assetGUIDs)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(p)) continue;
                if (AssetDatabase.IsValidFolder(p))
                {
                    foreach (var g in AssetDatabase.FindAssets("", new[] { p }))
                    {
                        var c = AssetDatabase.GUIDToAssetPath(g);
                        if (!AssetDatabase.IsValidFolder(c) && seen.Add(c)) list.Add(c);
                    }
                }
                else if (seen.Add(p)) list.Add(p);
            }
            return list;
        }
    }
}
