using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// "Seen once" flags behind skip-after-first-viewing (GDD §8; TECH_PLAN §7.12), stored in the settings key/values
    /// (localStorage on the web, PlayerPrefs in the editor, through ISettingsService): the intro uses
    /// <see cref="StorageKeys.IntroSeen"/>, every other sequence "ls.cine.&lt;id&gt;". Without the settings service the flags
    /// live in memory for the session.
    /// </summary>
    public static class SeenFlags
    {
        /// <summary>Key prefix for sequences other than the intro.</summary>
        public const string Prefix = StorageKeys.CinematicSeenPrefix;

        private static readonly HashSet<string> s_memory = new HashSet<string>();
        private static ServiceRef<ISettingsService> s_settings;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_memory.Clear();
            s_settings = default;
        }

        /// <summary>Storage key of a sequence id.</summary>
        public static string KeyOf(string sequenceId) =>
            sequenceId == SequenceIds.Intro ? StorageKeys.IntroSeen : Prefix + sequenceId;

        /// <summary>True if the sequence has been seen (in storage or this session).</summary>
        public static bool HasSeen(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId)) return false;
            if (s_memory.Contains(sequenceId)) return true;
            var s = s_settings.Value;
            if (s == null) return false;
            string v;
            try
            {
                v = s.GetValue(KeyOf(sequenceId));
            }
            catch (System.Exception e)
            {
                LSLog.Exception(LogCategory.Cinematics, e);
                return false;
            }
            var seen = v == "1";
            if (seen) s_memory.Add(sequenceId);
            return seen;
        }

        /// <summary>Marks a sequence as seen (session and storage).</summary>
        public static void MarkSeen(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId) || !s_memory.Add(sequenceId)) return;
            var s = s_settings.Value;
            if (s == null) return;
            try
            {
                s.SetValue(KeyOf(sequenceId), "1");
            }
            catch (System.Exception e)
            {
                LSLog.Exception(LogCategory.Cinematics, e);
            }
        }

        /// <summary>Forgets the session flags (tests; storage is untouched).</summary>
        public static void ClearMemoryForTests() => s_memory.Clear();
    }
}
