using UnityEditor;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// AssetPostprocessor applying TECH_PLAN §3.12 to every clip under Assets/_Game/Audio (rules in <see cref="AudioRules"/>).
    /// Opt out per asset with <see cref="ImportRulePaths.OptOutTag"/>; bump <see cref="Version"/> when a rule changes.
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        /// <summary>Rule version (reimport trigger).</summary>
        public const uint Version = 2;

        /// <inheritdoc />
        public override uint GetVersion() => Version;

        /// <inheritdoc />
        public override int GetPostprocessOrder() => -100;

        private void OnPreprocessAudio()
        {
            if (ImportRulePaths.RootOf(assetPath) == null) return;
            var ai = assetImporter as AudioImporter;
            if (ai == null || ImportRulePaths.IsOptedOut(ai)) return;
            var rule = AudioRules.Resolve(assetPath);
            if (rule == null) return;
            AudioRuleApplier.Apply(ai, rule);
            if (EditorPrefs.GetBool(TextureImportRules.VerbosePref, false))
                LSLog.Info(LogCategory.Assets, "Audio rule " + assetPath + " -> " + rule);
        }
    }
}
