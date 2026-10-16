using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// AssetPostprocessor applying TECH_PLAN §3.11 to every model under Assets/_Game/Art/{Models,Animation} (rules in
    /// <see cref="ModelRules"/>). Opt out per asset with <see cref="ImportRulePaths.OptOutTag"/> in the importer userData;
    /// bump <see cref="Version"/> when a rule changes. Runs early (order -100) so WP05's character importers can refine.
    /// </summary>
    public sealed class ModelImportRules : AssetPostprocessor
    {
        /// <summary>Rule version (reimport trigger).</summary>
        public const uint Version = 2;

        private static readonly Regex s_lodName = new Regex(@"_LOD[1-9]", RegexOptions.IgnoreCase);

        /// <inheritdoc />
        public override uint GetVersion() => Version;

        /// <inheritdoc />
        public override int GetPostprocessOrder() => -100;

        private void OnPreprocessModel()
        {
            if (ImportRulePaths.RootOf(assetPath) == null) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null || ImportRulePaths.IsOptedOut(mi)) return;
            var src = ModelSourceProbe.Probe(assetPath);
            var rule = ModelRules.Resolve(assetPath, src);
            if (rule == null) return;
            ModelRuleApplier.Apply(mi, rule);
            if (EditorPrefs.GetBool(TextureImportRules.VerbosePref, false))
                LSLog.Info(LogCategory.Assets, "Model rule " + assetPath + " [" + src + "] -> " + rule);
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (ImportRulePaths.RootOf(assetPath) == null) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null || ImportRulePaths.IsOptedOut(mi) || !mi.generateMeshLods) return;
            // Authored LODs that the byte scan missed (unusual naming) would be doubled by Mesh LOD generation.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!s_lodName.IsMatch(t.name)) continue;
                LSLog.WarnOnce("meshlod:" + assetPath, LogCategory.Assets,
                    "Model has LOD-named objects but Mesh LOD generation is on (name the LOD meshes *_LOD0/_LOD1/...): " + assetPath);
                break;
            }
        }
    }
}
