using System.Text;
using LongShadow.Combat.Effects;
using LongShadow.Combat.Service;
using LongShadow.Combat.Viewmodel;
using LongShadow.Combat.Weapons;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Combat.Editor
{
    /// <summary>
    /// One-click setup of the combat package for the editor holder (all menus under <c>LongShadow/Combat/</c>):
    /// weapon materials, weapon prefabs, the FX library, the <c>PF_CombatSystems</c> prefab (CombatService + CombatFx,
    /// placed in S_Persistent by WP11) and the player components (WeaponInventory, PlayerWeapon, ViewmodelController).
    /// Every step is idempotent and can also be called from <c>Unity_RunCommand</c> (public static methods).
    /// </summary>
    public static class CombatSetupMenu
    {
        /// <summary>The systems prefab (TECH_PLAN §5.1: CombatService lives in S_Persistent).</summary>
        public const string SystemsPrefabPath = WeaponPrefabBuilder.PrefabFolder + "/PF_CombatSystems.prefab";
        /// <summary>WP02's player prefab (PlayerPrefabBuilder).</summary>
        public const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player/PF_Player.prefab";

        /// <summary>Materials → weapon prefabs → FX library → systems prefab, in that order; returns the combined report.</summary>
        [MenuItem("LongShadow/Combat/Build All", priority = 100)]
        public static string BuildAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine(BuildWeaponMaterials());
            sb.AppendLine(WeaponPrefabBuilder.BuildAll());
            sb.AppendLine(CombatFxBuilder.BuildAll());
            sb.AppendLine(BuildSystemsPrefab());
            sb.AppendLine(WeaponPrefabBuilder.ReportBudgets());
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        /// <summary>
        /// Builds the weapon materials from the landed texture sets (MaterialFactory: <c>T_WPN_*_{BC,N,M}</c> →
        /// <c>Art/Materials/Weapons/M_WPN_*</c>) and binds the weapon models' material slots to them (ModelMaterialBinder).
        /// </summary>
        [MenuItem("LongShadow/Combat/Build Weapon Materials", priority = 105)]
        public static string BuildWeaponMaterials()
        {
            var sb = new StringBuilder("[Combat] weapon materials\n");
            if (AssetDatabase.IsValidFolder(WeaponArt.TexturesRoot))
            {
                var mats = MaterialFactory.BuildAllInFolder(WeaponArt.TexturesRoot);
                sb.AppendLine("  MaterialFactory: " + mats.Count + " material(s) from " + WeaponArt.TexturesRoot);
            }
            else sb.AppendLine("  no textures yet at " + WeaponArt.TexturesRoot + " (models keep their importer materials)");
            if (AssetDatabase.IsValidFolder(WeaponArt.ModelsRoot))
            {
                var results = ModelMaterialBinder.BindAll(WeaponArt.ModelsRoot);
                foreach (var r in results) sb.AppendLine("  " + r);
            }
            var text = sb.ToString();
            LSLog.Info(LogCategory.Combat, text);
            return text;
        }

        /// <summary>Builds <c>PF_CombatSystems</c> (CombatService + CombatFx).</summary>
        [MenuItem("LongShadow/Combat/Build Combat Systems Prefab", priority = 130)]
        public static string BuildSystemsPrefab()
        {
            var go = new GameObject("PF_CombatSystems");
            go.AddComponent<CombatFx>();
            go.AddComponent<CombatService>();
            var prefab = BuilderUtil.SavePrefab(go, SystemsPrefabPath);
            var text = "[Combat] systems prefab: " + (prefab != null ? SystemsPrefabPath : "FAILED");
            LSLog.Info(LogCategory.Combat, text);
            return text;
        }

        /// <summary>Adds WeaponInventory, PlayerWeapon and ViewmodelController to the selected scene object or prefab asset.</summary>
        [MenuItem("LongShadow/Combat/Add Player Combat To Selection", priority = 131)]
        public static void AddToSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                LSLog.Warn(LogCategory.Combat, "Select the player rig root (PF_Player) first.");
                return;
            }
            var path = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(path)) AddToPlayerPrefab(path);
            else
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Add Player Combat");
                AddComponents(go);
                EditorUtility.SetDirty(go);
            }
        }

        /// <summary>Adds the player combat components to WP02's PF_Player prefab (WP11 integration step).</summary>
        [MenuItem("LongShadow/Combat/Add Player Combat To PF_Player", priority = 132)]
        public static void AddToDefaultPlayer() => AddToPlayerPrefab(PlayerPrefabPath);

        /// <summary>Adds the player combat components to a prefab asset; returns false when the prefab is missing.</summary>
        public static bool AddToPlayerPrefab(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                LSLog.Warn(LogCategory.Combat, "Player prefab not found at " + prefabPath + " (WP02 PlayerPrefabBuilder builds it).");
                return false;
            }
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                AddComponents(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                LSLog.Info(LogCategory.Combat, "Player combat components added to " + prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The component set on a player rig root (idempotent).</summary>
        public static void AddComponents(GameObject root)
        {
            if (root.GetComponent<WeaponInventory>() == null) root.AddComponent<WeaponInventory>();
            if (root.GetComponent<ViewmodelController>() == null) root.AddComponent<ViewmodelController>();
            if (root.GetComponent<PlayerWeapon>() == null) root.AddComponent<PlayerWeapon>();
        }
    }
}
