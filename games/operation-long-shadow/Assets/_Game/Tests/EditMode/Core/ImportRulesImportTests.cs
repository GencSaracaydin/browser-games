using System.IO;
using LongShadow.Core.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Core
{
    /// <summary>
    /// Real imports through the postprocessors: generated files are written into the rule test root
    /// (Assets/__ImportRuleTest, mirrors Assets/_Game), imported, checked and deleted. The full sample run with RawAssets
    /// files is LongShadow/Import/Run Rule Self-Test.
    /// </summary>
    public class ImportRulesImportTests
    {
        private const string Root = ImportRulePaths.TestRoot;

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Root.TrimEnd('/'))) AssetDatabase.DeleteAsset(Root.TrimEnd('/'));
        }

        private static string Write(string rel, byte[] bytes)
        {
            var path = Root + rel;
            var full = Path.Combine(BuilderUtil.ProjectRoot, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
            File.WriteAllBytes(full, bytes);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        private static byte[] Png(int size, bool alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (var i = 0; i < px.Length; i++) px[i] = new Color32(128, 128, 255, (byte)(alpha && i % 2 == 0 ? 0 : 255));
            tex.SetPixels32(px);
            tex.Apply();
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return bytes;
        }

        private static byte[] Wav(int sampleRate, int channels, float seconds)
        {
            var frames = (int)(sampleRate * seconds);
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                var dataBytes = frames * channels * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)channels);
                w.Write(sampleRate);
                w.Write(sampleRate * channels * 2);
                w.Write((short)(channels * 2));
                w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                for (var i = 0; i < frames; i++)
                for (var c = 0; c < channels; c++)
                    w.Write((short)(Mathf.Sin(i * 0.05f) * 8000));
                return ms.ToArray();
            }
        }

        [Test]
        public void NormalMapInEnvSetImportsWithRule()
        {
            var path = Write("Art/Textures/Env/rule_test/T_RuleTest_N.png", Png(256, false));
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(TextureImporterType.NormalMap, ti.textureType);
            var w = ti.GetPlatformTextureSettings("WebGL");
            Assert.IsTrue(w.overridden);
            Assert.AreEqual(TextureImporterFormat.DXT5Crunched, w.format);
            Assert.AreEqual(60, w.compressionQuality);
            Assert.IsFalse(ti.isReadable);
        }

        [Test]
        public void OpaqueAlphaChannelGetsDxt1AndRealAlphaGetsDxt5()
        {
            TextureSourceProbe.ClearCache();
            var opaque = Write("Art/Textures/Props/rule_test/T_RuleTestA_BC.png", Png(128, false));
            var cut = Write("Art/Textures/Props/rule_test/T_RuleTestB_BC.png", Png(128, true));
            Assert.AreEqual(TextureImporterFormat.DXT1Crunched, ((TextureImporter)AssetImporter.GetAtPath(opaque)).GetPlatformTextureSettings("WebGL").format);
            Assert.AreEqual(TextureImporterFormat.DXT5Crunched, ((TextureImporter)AssetImporter.GetAtPath(cut)).GetPlatformTextureSettings("WebGL").format);
        }

        [Test]
        public void VoRadioClipImportsWithRule()
        {
            var path = Write("Audio/VO/RADIO/VO_RuleTest_RADIO.wav", Wav(48000, 2, 0.25f));
            var ai = (AudioImporter)AssetImporter.GetAtPath(path);
            Assert.IsTrue(ai.forceToMono);
            var s = ai.defaultSampleSettings;
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, s.loadType);
            Assert.IsFalse(s.preloadAudioData);
            Assert.AreEqual(AudioSampleRateSetting.OverrideSampleRate, s.sampleRateSetting);
            Assert.AreEqual(22050u, s.sampleRateOverride);
            // Unity 6000.6 accepts no WebGL sample-settings override; the Web output is AAC from the default settings.
            Assert.IsFalse(ai.ContainsSampleSettingsOverride("WebGL"));
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                var o = AudioRuleApplier.ReadOutput(ai);
                Assert.IsTrue(o.Valid);
                Assert.AreEqual(AudioCompressionFormat.AAC, o.Format);
                Assert.AreEqual(AudioClipLoadType.CompressedInMemory, o.LoadType);
                Assert.AreEqual(60, Mathf.RoundToInt(o.Quality * 100f));
            }
            Assert.IsFalse(new SerializedObject(ai).FindProperty("m_Normalize").boolValue);
        }

        [Test]
        public void OptOutTagIsRespected()
        {
            var path = Write("Art/Textures/Env/rule_test/T_RuleTestOpt_M.png", Png(64, false));
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.IsFalse(ti.sRGBTexture);
            ti.userData = ImportRulePaths.OptOutTag;
            ti.sRGBTexture = true;
            ti.SaveAndReimport();
            Assert.IsTrue(((TextureImporter)AssetImporter.GetAtPath(path)).sRGBTexture);
        }
    }
}
