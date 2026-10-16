using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// URL flags of the web build (TECH_PLAN §5.2.4): <c>?cp=CP5</c> (start at a checkpoint), <c>?dev=1</c> (dev console),
    /// <c>?autotest=1</c> (smoke test, skips the click gate), plus any other key for tools. Read from the page's
    /// <c>location.search</c> and <c>location.hash</c> (both are parsed; the hash wins), from <see cref="DevStart"/> in the
    /// editor. Other systems read them through <c>ISettingsService.GetValue("?cp")</c> (keys starting with "?" are launch
    /// options) or, in Integration, through <see cref="Current"/>.
    /// </summary>
    public sealed class LaunchOptions
    {
        /// <summary>Query keys with a meaning.</summary>
        public const string KeyCheckpoint = "cp", KeyDev = "dev", KeyAutotest = "autotest", KeyNoLock = "nolock", KeyPerf = "perf";

        private static LaunchOptions s_current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_current = null;

        /// <summary>The options of this session (parsed on first access when <see cref="Bootstrap"/> has not set them).</summary>
        public static LaunchOptions Current
        {
            get => s_current ?? (s_current = FromEnvironment(null));
            internal set => s_current = value;
        }

        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Checkpoint id to resume from ("CP5"), or null. Only ids in <see cref="SequenceIds.Checkpoints"/> are accepted.</summary>
        public string Checkpoint { get; private set; }
        /// <summary>Dev console requested (also on in QA builds).</summary>
        public bool Dev { get; private set; }
        /// <summary>Automated smoke test: the gate is skipped and WP11's smoke test drives the run.</summary>
        public bool Autotest { get; private set; }
        /// <summary>Never request pointer lock (automation panes, kiosk tests).</summary>
        public bool NoLock { get; private set; }
        /// <summary>Force performance mode for this session ("?perf=1"), without changing the saved option.</summary>
        public bool ForcePerformanceMode { get; private set; }
        /// <summary>Raw query text the options were parsed from.</summary>
        public string Source { get; private set; } = string.Empty;
        /// <summary>Every key/value (keys case-insensitive; a bare key has the value "").</summary>
        public IReadOnlyDictionary<string, string> Values => _values;

        /// <summary>Value of a key, or null when absent.</summary>
        public string Get(string key) => key != null && _values.TryGetValue(key, out var v) ? v : null;

        /// <summary>True if the key is present and not "0"/"false"/"no"/"off".</summary>
        public bool GetBool(string key) => IsTrue(Get(key));

        /// <summary>Parses "?a=1&amp;b#c=2" style text (leading '?'/'#', both parts, '+' as space, percent-decoding).</summary>
        public static LaunchOptions Parse(string query)
        {
            var o = new LaunchOptions { Source = query ?? string.Empty };
            if (!string.IsNullOrEmpty(query))
            {
                var q = query;
                var start = q.IndexOf('?');
                if (start >= 0) q = q.Substring(start + 1);
                foreach (var part in q.Split(new[] { '&', '#', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = part.IndexOf('=');
                    var k = Decode(eq < 0 ? part : part.Substring(0, eq)).Trim();
                    if (k.Length == 0) continue;
                    var v = eq < 0 ? string.Empty : Decode(part.Substring(eq + 1)).Trim();
                    o._values[k] = v;
                }
            }
            o.Resolve();
            return o;
        }

        /// <summary>
        /// Options of the running build: the page URL on the web (query + hash), <see cref="DevStart"/>'s text in the
        /// editor. QA builds (template variant "qa") turn <see cref="Dev"/> on.
        /// </summary>
        public static LaunchOptions FromEnvironment(DevStart devStart)
        {
            string text = null;
            if (WebNative.Available) text = WebNative.GetQuery(string.Empty);
            if (string.IsNullOrEmpty(text))
            {
                var url = Application.absoluteURL;
                if (!string.IsNullOrEmpty(url))
                {
                    var i = url.IndexOfAny(new[] { '?', '#' });
                    if (i >= 0) text = url.Substring(i);
                }
            }
#if UNITY_EDITOR
            // Components that read the options before Bootstrap.Awake (pointer lock, settings) must still see DevStart.
            if (devStart == null) devStart = UnityEngine.Object.FindAnyObjectByType<DevStart>();
            if (devStart != null && !string.IsNullOrEmpty(devStart.EditorQuery)) text = devStart.EditorQuery;
#endif
            var o = Parse(text);
            if (string.Equals(WebBridge.BuildVariant, "qa", StringComparison.OrdinalIgnoreCase)) o.Dev = true;
#if UNITY_EDITOR
            if (devStart != null && devStart.EditorDev) o.Dev = true;
#endif
            return o;
        }

        private void Resolve()
        {
            var cp = Get(KeyCheckpoint);
            Checkpoint = null;
            if (!string.IsNullOrEmpty(cp))
            {
                foreach (var id in SequenceIds.Checkpoints)
                    if (string.Equals(id, cp, StringComparison.OrdinalIgnoreCase)) Checkpoint = id;
                if (Checkpoint == null)
                    LSLog.Warn(LogCategory.Platform, "Ignoring unknown checkpoint in URL: cp=" + cp);
            }
            Dev = GetBool(KeyDev);
            Autotest = GetBool(KeyAutotest);
            NoLock = GetBool(KeyNoLock);
            ForcePerformanceMode = GetBool(KeyPerf);
        }

        private static bool IsTrue(string v)
        {
            if (v == null) return false;
            if (v.Length == 0) return true;
            return !(v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                     v.Equals("no", StringComparison.OrdinalIgnoreCase) || v.Equals("off", StringComparison.OrdinalIgnoreCase));
        }

        private static string Decode(string s)
        {
            try
            {
                return Uri.UnescapeDataString(s.Replace('+', ' '));
            }
            catch (Exception)
            {
                return s;
            }
        }

        /// <summary>Short description for logs.</summary>
        public override string ToString() =>
            "cp=" + (Checkpoint ?? "-") + " dev=" + (Dev ? 1 : 0) + " autotest=" + (Autotest ? 1 : 0) + " nolock=" + (NoLock ? 1 : 0) +
            " perf=" + (ForcePerformanceMode ? 1 : 0);
    }
}
