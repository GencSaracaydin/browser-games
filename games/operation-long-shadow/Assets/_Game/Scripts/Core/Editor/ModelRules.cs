using System;
using System.IO;
using UnityEditor;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// The model import table of TECH_PLAN §3.11 (pure function; applied by <see cref="ModelImportRules"/>), refined by
    /// ORCHESTRATION §3 (static meshes arrive as Blender FBX with real-world metres and baked axes):
    /// <list type="bullet">
    /// <item>Props, Vehicles, Vegetation: anim None, normals Import, tangents Mikk, compression Medium, UV2 only for
    /// <c>*_LM</c> files and the lightmapped big props of asset_budgets.json <c>model.lightmapped_props</c> (D8), Mesh LOD
    /// on (3 levels) unless the file carries authored <c>*_LOD1</c> meshes.</item>
    /// <item>Weapons: FP files (<c>_FP</c>, <c>_Mag</c>) compression Off and no Mesh LOD; world files (<c>_W</c>/<c>_World</c>
    /// or unmarked) Medium with Mesh LOD (unless authored).</item>
    /// <item>Characters (except Characters/Props): Humanoid, avatar created from the model, materials kept as slots
    /// (ImportViaMaterialDescription, so WP05's ModelMaterialBinder can bind M_Calder_Body etc.), compression Off, blend
    /// shapes on.</item>
    /// <item>Animation/Clips: Humanoid, own avatar, Optimal compression, keyframe reduction 0.5° / 0.5 %.</item>
    /// <item>Kit, Terrain, Backdrop: architecture, UV2 on, compression Medium, no Mesh LOD (builders own LODs).</item>
    /// </list>
    /// Static meshes keep their material slots (ImportViaMaterialDescription) so <see cref="ModelMaterialBinder"/> can
    /// remap each slot to the MaterialFactory material of the same name; this replaces §3.11 "Materials None", which would
    /// drop the slot names (Docs/DECISIONS.md).
    /// </summary>
    public static class ModelRules
    {
        /// <summary>Resolves the rule for a project path, or null when the rules do not manage it.</summary>
        public static ModelRule Resolve(string assetPath, ModelSourceInfo src)
        {
            var rel = ImportRulePaths.ToRuleRelative(assetPath);
            if (rel == null) return null;
            var stem = Path.GetFileNameWithoutExtension(rel);

            if (ImportRulePaths.Under(rel, "Art/Animation/Clips/") || ImportRulePaths.Under(rel, "Art/Animation/Poses/"))
            {
                return new ModelRule
                {
                    Name = "Clips",
                    Animation = ModelImporterAnimationType.Human,
                    ImportAnimation = true,
                    Materials = ModelImporterMaterialImportMode.None,
                    Compression = ModelImporterMeshCompression.Off,
                    BlendShapes = true, // facial clips animate blend shapes
                };
            }
            if (!ImportRulePaths.Under(rel, "Art/Models/")) return null;

            if (ImportRulePaths.Under(rel, "Art/Models/Characters/Props/"))
                return Static("Characters/Props", stem, src, ModelImporterMeshCompression.Medium, meshLodsAllowed: false);

            if (ImportRulePaths.Under(rel, "Art/Models/Characters/"))
            {
                return new ModelRule
                {
                    Name = "Characters",
                    Animation = ModelImporterAnimationType.Human,
                    ImportAnimation = false,
                    // Slot names must survive for WP05's ModelMaterialBinder (CORE_CHANGES "WP05 character models keep
                    // material slots"); WP05 RocketboxImporter/BodyPrefabBuilder build the URP materials.
                    Materials = ModelImporterMaterialImportMode.ImportViaMaterialDescription,
                    Compression = ModelImporterMeshCompression.Off,
                    BlendShapes = true,
                };
            }

            if (ImportRulePaths.Under(rel, "Art/Models/Weapons/"))
            {
                var fp = !HasToken(stem, "W") && !HasToken(stem, "World") && (HasToken(stem, "FP") || HasToken(stem, "Mag"));
                return fp
                    ? Static("Weapons/FP", stem, src, ModelImporterMeshCompression.Off, meshLodsAllowed: false)
                    : Static("Weapons/World", stem, src, ModelImporterMeshCompression.Medium, meshLodsAllowed: true);
            }

            if (ImportRulePaths.Under(rel, "Art/Models/Kit/") || ImportRulePaths.Under(rel, "Art/Models/Terrain/") ||
                ImportRulePaths.Under(rel, "Art/Models/Backdrop/"))
            {
                var r = Static("Architecture", stem, src, ModelImporterMeshCompression.Medium, meshLodsAllowed: false);
                r.LightmapUVs = true;
                return r;
            }

            var kind = ImportRulePaths.FolderBelow(rel, "Art/Models/") ?? "Models";
            var rule = Static(kind, stem, src, ModelImporterMeshCompression.Medium, meshLodsAllowed: true);
            var folder = ImportRulePaths.FolderBelow(rel, "Art/Models/" + kind + "/");
            if (!rule.LightmapUVs && TextureBudgets.In(TextureBudgets.LightmappedProps, folder))
            {
                rule.LightmapUVs = true; // D8: big static props are lightmapped
                rule.Name += "/Lightmapped";
            }
            return rule;
        }

        private static ModelRule Static(string name, string stem, ModelSourceInfo src, ModelImporterMeshCompression compression, bool meshLodsAllowed)
        {
            return new ModelRule
            {
                Name = name,
                Animation = ModelImporterAnimationType.None,
                ImportAnimation = false,
                Materials = ModelImporterMaterialImportMode.ImportViaMaterialDescription,
                Compression = compression,
                LightmapUVs = HasToken(stem, "LM"),
                MeshLods = meshLodsAllowed && !src.HasAuthoredLods,
                AuthoredLods = src.HasAuthoredLods,
                BlendShapes = false,
            };
        }

        /// <summary>True when the underscore-separated file stem contains <paramref name="token"/> ("SM_WPN_AK_FP" has "FP").</summary>
        public static bool HasToken(string stem, string token)
        {
            foreach (var part in stem.Split('_'))
                if (string.Equals(part, token, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
