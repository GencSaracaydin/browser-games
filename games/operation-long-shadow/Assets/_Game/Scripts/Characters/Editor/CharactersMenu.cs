using System.Collections.Generic;
using LongShadow.Core;
using UnityEditor;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// WP05 editor entry points (TECH_PLAN §7.8, §8.6). Every item is idempotent and safe to re-run when assets land; each
    /// is also callable from Unity_RunCommand (<c>LongShadow.Characters.Editor.CharactersMenu.BuildAll()</c>).
    /// Order of <see cref="BuildAll"/>: masks + finger poses → materials → bind material slots → weapon library →
    /// controllers → body prefabs → validation.
    /// </summary>
    public static class CharactersMenu
    {
        private const string Root = "LongShadow/Characters/";

        /// <summary>Runs the whole WP05 build. Returns the validation failure count.</summary>
        [MenuItem(Root + "Build All", false, 0)]
        public static int BuildAll()
        {
            AvatarMaskBuilder.BuildAll();
            BuildMaterials();
            CharacterWeaponLibraryBuilder.Build();
            AnimatorControllerBuilder.BuildAll();
            BodyPrefabBuilder.BuildAll();
            return BodyValidator.ValidateAll();
        }

        /// <summary>Builds character materials and binds every character FBX slot to them.</summary>
        [MenuItem(Root + "Build Materials", false, 20)]
        public static void BuildMaterials()
        {
            CharacterMaterialBuilder.BuildAll();
            foreach (var r in CharacterMaterialBuilder.BindAll()) LSLog.Verbose(LogCategory.Characters, "Bind " + r);
        }

        [MenuItem(Root + "Build Finger Poses", false, 21)]
        public static void BuildFingerPoses() => FingerPoseAuthoring.BuildAll();

        [MenuItem(Root + "Build Controllers", false, 22)]
        public static void BuildControllers() => AnimatorControllerBuilder.BuildAll();

        [MenuItem(Root + "Build Weapon Library", false, 23)]
        public static void BuildWeaponLibrary() => CharacterWeaponLibraryBuilder.Build();

        [MenuItem(Root + "Build Body Prefabs", false, 24)]
        public static void BuildBodies() => BodyPrefabBuilder.BuildAll();

        [MenuItem(Root + "Validate Bodies", false, 40)]
        public static int Validate() => BodyValidator.ValidateAll();

        /// <summary>Re-imports every body FBX (RocketboxImporter) under Art/Models/Characters.</summary>
        [MenuItem(Root + "Reimport Character Models", false, 60)]
        public static void ReimportModels()
        {
            var paths = new List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { CharacterPaths.ModelsRoot }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (RocketboxImporter.IsCharacterModel(p)) paths.Add(p);
            }
            ClipImporter.Reimport(paths);
        }

        /// <summary>Re-imports every clip FBX (ClipImporter) under Art/Animation/Clips.</summary>
        [MenuItem(Root + "Reimport Clips", false, 61)]
        public static void ReimportClips() => ClipImporter.Reimport(ClipImporter.AllClipFiles());
    }
}
