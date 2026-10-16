using UnityEditor;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// AssetPostprocessor applying TECH_PLAN §3.10 to every texture under Assets/_Game/Art/{Textures,HDRI} and
    /// Assets/_Game/UI/Sprites (rules in <see cref="TextureRules"/>). It runs on every import, so the folder + suffix
    /// decide the settings and hand edits do not survive a reimport; to own an asset's settings put
    /// <see cref="ImportRulePaths.OptOutTag"/> into its importer userData. Bump <see cref="Version"/> when a rule changes:
    /// Unity then reimports every texture this postprocessor handled. Runs early (order -100) so package-specific
    /// postprocessors can refine afterwards. Non-power-of-two sources are rescaled by Unity; validate_assets.py reports them.
    /// </summary>
    public sealed class TextureImportRules : AssetPostprocessor
    {
        /// <summary>Rule version (reimport trigger).</summary>
        public const uint Version = 2;

        /// <summary>EditorPrefs key: log the resolved rule for every import.</summary>
        public const string VerbosePref = "LongShadow.ImportRules.Verbose";

        /// <inheritdoc />
        public override uint GetVersion() => Version;

        /// <inheritdoc />
        public override int GetPostprocessOrder() => -100;

        private void OnPreprocessTexture()
        {
            if (ImportRulePaths.RootOf(assetPath) == null) return;
            var ti = assetImporter as TextureImporter;
            if (ti == null || ImportRulePaths.IsOptedOut(ti)) return;
            var suffix = TextureNaming.ParseSuffix(assetPath);
            var colour = suffix == TextureSuffix.None || suffix == TextureSuffix.BC || suffix == TextureSuffix.E;
            var src = TextureSourceProbe.Probe(assetPath, ti, scanAlpha: colour);
            var rule = TextureRules.Resolve(assetPath, src);
            if (rule == null) return;
            TextureRuleApplier.Apply(ti, rule);
            if (EditorPrefs.GetBool(VerbosePref, false))
                LSLog.Info(LogCategory.Assets, "Texture rule " + assetPath + " [" + src + "] -> " + rule);
        }
    }
}
