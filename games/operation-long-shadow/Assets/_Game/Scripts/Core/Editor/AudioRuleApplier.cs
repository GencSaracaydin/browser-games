using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Writes an <see cref="AudioRule"/> into an AudioImporter and checks it. Only the default sample settings are set:
    /// Unity 6000.6 accepts no WebGL sample-settings override at all (<c>SetOverrideSampleSettings("WebGL", ...)</c>
    /// returns false for every format) and encodes every clip for the Web as AAC at 44.1 kHz with the default settings'
    /// load type, preload flag and quality; the sample-rate override is not applied on that target (measured 2026-10-16,
    /// Docs/DECISIONS.md). The default platform uses Vorbis. "Normalize" (applied by Force To Mono) is switched off so the
    /// −18 LUFS VO calibration and the SFX mix levels of the offline chains are kept.
    /// </summary>
    public static class AudioRuleApplier
    {
        /// <summary>WebGL platform name.</summary>
        public const string WebGL = "WebGL";

        /// <summary>Applies the rule. Safe inside OnPreprocessAudio.</summary>
        public static void Apply(AudioImporter ai, AudioRule r)
        {
            ai.forceToMono = r.Mono;
            ai.loadInBackground = r.LoadInBackground;
            ai.ambisonic = false;
            SetNormalize(ai, false);

            var s = ai.defaultSampleSettings;
            s.loadType = r.LoadType;
            s.preloadAudioData = r.Preload;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = r.Quality / 100f;
            s.sampleRateSetting = r.SampleRate > 0 ? AudioSampleRateSetting.OverrideSampleRate : AudioSampleRateSetting.PreserveSampleRate;
            if (r.SampleRate > 0) s.sampleRateOverride = (uint)r.SampleRate;
            ai.defaultSampleSettings = s;

            // Stale overrides (from an earlier rule version or a hand edit) would win over the defaults on their platform.
            if (ai.ContainsSampleSettingsOverride(WebGL)) ai.ClearSampleSettingOverride(WebGL);
        }

        /// <summary>
        /// Differences between an importer and a rule; empty when it matches. When the active build target is WebGL the
        /// imported output is checked too (AAC, rule load type, preload and quality), read from the importer's serialized
        /// import result.
        /// </summary>
        public static List<string> Diff(AudioImporter ai, AudioRule r)
        {
            var diffs = new List<string>();
            void Check<T>(string name, T actual, T expected)
            {
                if (!Equals(actual, expected)) diffs.Add(name + ": " + actual + " != " + expected);
            }
            Check("forceToMono", ai.forceToMono, r.Mono);
            Check("loadInBackground", ai.loadInBackground, r.LoadInBackground);
            Check("normalize", GetNormalize(ai), false);
            var s = ai.defaultSampleSettings;
            Check("loadType", s.loadType, r.LoadType);
            Check("preloadAudioData", s.preloadAudioData, r.Preload);
            Check("quality", Mathf.RoundToInt(s.quality * 100f), r.Quality);
            Check("sampleRateSetting", s.sampleRateSetting,
                r.SampleRate > 0 ? AudioSampleRateSetting.OverrideSampleRate : AudioSampleRateSetting.PreserveSampleRate);
            if (r.SampleRate > 0) Check("sampleRateOverride", (int)s.sampleRateOverride, r.SampleRate);
            Check("WebGL override present", ai.ContainsSampleSettingsOverride(WebGL), false);
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                var o = ReadOutput(ai);
                if (o.Valid)
                {
                    Check("output(WebGL).format", o.Format, AudioCompressionFormat.AAC);
                    Check("output(WebGL).loadType", o.LoadType, r.LoadType);
                    Check("output(WebGL).preload", o.Preload, r.Preload);
                    Check("output(WebGL).quality", Mathf.RoundToInt(o.Quality * 100f), r.Quality);
                }
            }
            return diffs;
        }

        /// <summary>What the importer produced for the active build target (serialized <c>m_Output.outputSettings</c>).</summary>
        public struct Output
        {
            /// <summary>The importer has an import result.</summary>
            public bool Valid;
            /// <summary>Codec of the imported data.</summary>
            public AudioCompressionFormat Format;
            /// <summary>Load type of the imported data.</summary>
            public AudioClipLoadType LoadType;
            /// <summary>Preload flag of the imported data.</summary>
            public bool Preload;
            /// <summary>Quality 0–1 of the imported data.</summary>
            public float Quality;
            /// <summary>Sample rate of the imported data in Hz.</summary>
            public int SampleRate;

            /// <inheritdoc />
            public override string ToString() => Valid ? Format + " " + LoadType + (Preload ? " preload" : "") + " q" + Mathf.RoundToInt(Quality * 100f) + " " + SampleRate + " Hz" : "no import result";
        }

        /// <summary>Reads the import result for the active build target.</summary>
        public static Output ReadOutput(AudioImporter ai)
        {
            var o = new Output();
            if (ai == null) return o;
            var so = new SerializedObject(ai);
            var fmt = so.FindProperty("m_Output.outputSettings.compressionFormat");
            var load = so.FindProperty("m_Output.outputSettings.loadType");
            var pre = so.FindProperty("m_Output.outputSettings.preloadAudioData");
            var q = so.FindProperty("m_Output.outputSettings.quality");
            var rate = so.FindProperty("m_Output.outputSettings.sampleRateOverride");
            if (fmt == null || load == null || pre == null || q == null) return o;
            o.Valid = true;
            o.Format = (AudioCompressionFormat)fmt.intValue;
            o.LoadType = (AudioClipLoadType)load.intValue;
            o.Preload = pre.boolValue;
            o.Quality = q.floatValue;
            o.SampleRate = rate != null ? rate.intValue : 0;
            return o;
        }

        /// <summary>"Normalize" has no public API; it is the serialized m_Normalize of the importer.</summary>
        private static void SetNormalize(AudioImporter ai, bool value)
        {
            var so = new SerializedObject(ai);
            var p = so.FindProperty("m_Normalize");
            if (p == null || p.boolValue == value) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool GetNormalize(AudioImporter ai)
        {
            var p = new SerializedObject(ai).FindProperty("m_Normalize");
            return p != null && p.boolValue;
        }
    }
}
