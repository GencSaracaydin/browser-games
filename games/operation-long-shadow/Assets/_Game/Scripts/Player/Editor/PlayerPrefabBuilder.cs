using System;
using System.Collections.Generic;
using System.IO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LongShadow.Player.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Game/Prefabs/Player/PF_Player.prefab</c> (TECH_PLAN §7.3) from <see cref="PlayerRigFactory"/>
    /// and nests whatever bodies have landed: <c>PFB_Calder_FP</c> into FPBodySlot (layer Viewmodel, no shadows, view-model
    /// light layers, D19) and <c>PFB_Calder_Shadow</c> into ShadowProxySlot (layer Player, shadows only), both from
    /// <c>Assets/_Game/Prefabs/Bodies</c> (WP05), and the <c>PH_BINOCULARS</c> model (manifest target path) into
    /// BinocularsSlot. Missing ones are logged once and replaced at runtime by greybox stand-ins, so re-running the
    /// builder after the assets land simply upgrades the prefab (its GUID is kept). Ends with
    /// <see cref="PlayerRigValidator"/>; the report goes to the console and <c>Logs/player_prefab.txt</c>.
    /// </summary>
    public static class PlayerPrefabBuilder
    {
        /// <summary>Prefab path.</summary>
        public const string PrefabPath = "Assets/_Game/Prefabs/Player/PF_Player.prefab";
        /// <summary>Folder of WP05's body prefabs.</summary>
        public const string BodiesFolder = "Assets/_Game/Prefabs/Bodies";
        /// <summary>First-person Calder (arms and upper chest, D19).</summary>
        public const string FirstPersonBodyName = "PFB_Calder_FP";
        /// <summary>Shadow-only full Calder (D19).</summary>
        public const string ShadowBodyName = Body.PlayerShadowProxy.BodyPrefabName;
        /// <summary>Report file (project-relative).</summary>
        public const string ReportPath = "Logs/player_prefab.txt";

        [MenuItem("LongShadow/Player/Build Player Prefab", priority = 200)]
        private static void BuildMenu() => Build();

        [MenuItem("LongShadow/Player/Validate Player Prefab", priority = 201)]
        private static void ValidateMenu() => ValidatePrefab();

        /// <summary>Builds and saves PF_Player; returns the prefab asset (null on failure).</summary>
        public static GameObject Build()
        {
            var report = new List<string>();
            var rig = PlayerRigFactory.Build(Vector3.zero, 0f, null, true);
            var root = rig.gameObject;
            try
            {
                var fp = NestPrefab(rig.FirstPersonBodySlot, FirstPersonBodyName, report);
                if (fp != null) PrepareViewmodel(fp);
                var shadow = NestPrefab(rig.ShadowProxy != null ? rig.ShadowProxy.Slot : null, ShadowBodyName, report);
                if (shadow != null) PrepareShadowBody(shadow);
                NestBinoculars(rig, report);
                StripAudioListeners(root, report);

                var prefab = BuilderUtil.SavePrefab(root, PrefabPath);
                root = null;
                if (prefab == null) return null;
                var problems = new List<string>();
                PlayerRigValidator.Validate(prefab, problems);
                report.Add(problems.Count == 0 ? "Validation: OK" : "Validation: " + problems.Count + " problem(s)");
                foreach (var p in problems) report.Add("  - " + p);
                WriteReport("Build " + PrefabPath, report);
                if (problems.Count == 0) LSLog.Info(LogCategory.Player, "PF_Player built: " + PrefabPath + " (" + string.Join("; ", report) + ")", prefab);
                else LSLog.Error(LogCategory.Player, "PF_Player built with problems: " + string.Join("; ", problems), prefab);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Player, e);
                return null;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        /// <summary>Validates the saved prefab; returns the problems (empty = OK).</summary>
        public static List<string> ValidatePrefab()
        {
            var problems = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) problems.Add(PrefabPath + " does not exist (run LongShadow/Player/Build Player Prefab)");
            else PlayerRigValidator.Validate(prefab, problems);
            WriteReport("Validate " + PrefabPath, problems.Count == 0 ? new List<string> { "OK" } : problems);
            if (problems.Count == 0) LSLog.Info(LogCategory.Player, "PF_Player validation: OK", prefab);
            else LSLog.Error(LogCategory.Player, "PF_Player validation: " + string.Join("; ", problems), prefab);
            return problems;
        }

        /// <summary>Finds a body prefab by name (Prefabs/Bodies first, then anywhere under Assets/_Game).</summary>
        public static GameObject FindBodyPrefab(string name)
        {
            var direct = AssetDatabase.LoadAssetAtPath<GameObject>(BodiesFolder + "/" + name + ".prefab");
            if (direct != null) return direct;
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/_Game" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        private static GameObject NestPrefab(Transform slot, string name, List<string> report)
        {
            if (slot == null)
            {
                report.Add(name + ": slot missing");
                return null;
            }
            var source = FindBodyPrefab(name);
            if (source == null)
            {
                LSLog.MissingAsset(LogCategory.Player, name, BodiesFolder + "/" + name + ".prefab");
                report.Add(name + ": missing (runtime greybox)");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, slot);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            report.Add(name + ": nested");
            return instance;
        }

        private static void NestBinoculars(PlayerRig rig, List<string> report)
        {
            var slot = rig.transform.Find(PlayerRigNames.Yaw + "/" + PlayerRigNames.Pitch + "/" + PlayerRigNames.EyeOffset + "/" +
                                          PlayerRigNames.Bob + "/" + PlayerRigNames.Shake + "/" + PlayerRigNames.ViewmodelRoot + "/" +
                                          PlayerRigNames.BinocularsSlot);
            if (slot == null)
            {
                report.Add("PH_BINOCULARS: slot missing");
                return;
            }
            var candidates = AssetManifest.FindAll<GameObject>(Interaction.Binoculars.ModelAssetId, "binocular");
            if (candidates.Count == 0) candidates = AssetManifest.FindAll<GameObject>(Interaction.Binoculars.ModelAssetId);
            GameObject model = null;
            foreach (var c in candidates)
            {
                if (c.name.IndexOf("_LOD1", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("_LOD2", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                model = c;
                break;
            }
            if (model == null)
            {
                LSLog.MissingAsset(LogCategory.Player, Interaction.Binoculars.ModelAssetId, AssetManifest.ResolvePath(Interaction.Binoculars.ModelAssetId));
                report.Add("PH_BINOCULARS: missing (runtime greybox)");
                return;
            }
            var instance = PrefabUtility.InstantiatePrefab(model, slot) as GameObject ?? Object.Instantiate(model, slot);
            instance.name = "SM_Binoculars";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            PrepareViewmodel(instance);
            report.Add("PH_BINOCULARS: nested (" + AssetDatabase.GetAssetPath(model) + ")");
        }

        /// <summary>View-model rules (D5, §3.8): layer Viewmodel, never casts shadows, World + Cabin light, no colliders.</summary>
        public static void PrepareViewmodel(GameObject go)
        {
            BuilderUtil.SetLayerRecursively(go, LayerIds.Viewmodel);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
                r.renderingLayerMask = RenderingLayerIds.ViewmodelReceives;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        /// <summary>Shadow-proxy rules (D19): layer Player, shadows only, no colliders.</summary>
        public static void PrepareShadowBody(GameObject go)
        {
            BuilderUtil.SetLayerRecursively(go, LayerIds.Player);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                r.receiveShadows = false;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        private static void StripAudioListeners(GameObject root, List<string> report)
        {
            foreach (var l in root.GetComponentsInChildren<AudioListener>(true))
            {
                report.Add("removed AudioListener on " + l.name + " (WP06 owns the only one)");
                Object.DestroyImmediate(l);
            }
        }

        private static void WriteReport(string title, List<string> lines)
        {
            try
            {
                var path = Path.Combine(BuilderUtil.ProjectRoot, ReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, title + " (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n" + string.Join("\n", lines) + "\n");
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Player, e);
            }
        }
    }
}
