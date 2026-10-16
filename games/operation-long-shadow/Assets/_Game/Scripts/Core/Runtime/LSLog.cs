using System;
using System.Collections.Generic;
using System.Diagnostics;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace LongShadow.Core
{
    /// <summary>
    /// Project logging (TECH_PLAN R8). Every message is prefixed "[LS:Category]". <see cref="Verbose"/> is compiled out
    /// unless LS_DEV is defined. Errors are never swallowed: use <see cref="Exception"/> in catch blocks.
    /// </summary>
    public static class LSLog
    {
        private static readonly string[] s_prefixes = BuildPrefixes();
        private static readonly HashSet<string> s_once = new HashSet<string>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_once.Clear();

        private static string[] BuildPrefixes()
        {
            var values = (LogCategory[])Enum.GetValues(typeof(LogCategory));
            var max = 0;
            foreach (var v in values) max = Math.Max(max, (int)v);
            var arr = new string[max + 1];
            foreach (var v in values) arr[(int)v] = "[LS:" + v + "] ";
            return arr;
        }

        private static string P(LogCategory c)
        {
            var i = (int)c;
            return i >= 0 && i < s_prefixes.Length && s_prefixes[i] != null ? s_prefixes[i] : "[LS] ";
        }

        /// <summary>Informational message.</summary>
        public static void Info(LogCategory c, string message, Object context = null) => Debug.Log(P(c) + message, context);

        /// <summary>Warning.</summary>
        public static void Warn(LogCategory c, string message, Object context = null) => Debug.LogWarning(P(c) + message, context);

        /// <summary>Error.</summary>
        public static void Error(LogCategory c, string message, Object context = null) => Debug.LogError(P(c) + message, context);

        /// <summary>Logs an exception with the category prefix, then the exception itself (keeps the stack trace).</summary>
        public static void Exception(LogCategory c, Exception e, Object context = null)
        {
            Debug.LogError(P(c) + e.GetType().Name + ": " + e.Message, context);
            Debug.LogException(e, context);
        }

        /// <summary>Chatty development log; removed from builds without LS_DEV.</summary>
        [Conditional("LS_DEV")]
        public static void Verbose(LogCategory c, string message, Object context = null) => Debug.Log(P(c) + message, context);

        /// <summary>Logs a warning once per <paramref name="key"/> (per play session). Returns true if it was logged now.</summary>
        public static bool WarnOnce(string key, LogCategory c, string message, Object context = null)
        {
            if (!s_once.Add(key)) return false;
            Debug.LogWarning(P(c) + message, context);
            return true;
        }

        /// <summary>Logs an error once per key. Returns true if it was logged now.</summary>
        public static bool ErrorOnce(string key, LogCategory c, string message, Object context = null)
        {
            if (!s_once.Add(key)) return false;
            Debug.LogError(P(c) + message, context);
            return true;
        }

        /// <summary>
        /// Standard warning for an asset that has not been produced yet (assets land in parallel; systems must degrade to
        /// placeholders). Logged once per id.
        /// </summary>
        public static void MissingAsset(LogCategory c, string assetId, string expectedPath = null, Object context = null)
        {
            WarnOnce("asset:" + assetId, c,
                "Missing asset '" + assetId + "'" + (string.IsNullOrEmpty(expectedPath) ? "" : " (expected at " + expectedPath + ")") +
                "; using a placeholder.", context);
        }

        /// <summary>Logs an error if the condition is false (development builds and the editor only).</summary>
        [Conditional("UNITY_ASSERTIONS")]
        public static void Assert(bool condition, LogCategory c, string message, Object context = null)
        {
            if (!condition) Debug.LogError(P(c) + "Assertion failed: " + message, context);
        }

        /// <summary>Forgets every once-key (tests).</summary>
        public static void ResetOnce() => s_once.Clear();
    }
}
