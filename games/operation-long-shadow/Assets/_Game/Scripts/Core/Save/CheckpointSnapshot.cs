using System;
using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// A checkpoint: one <see cref="SaveRecord"/> per <see cref="ISaveable"/> (TECH_PLAN D11, §5.5). Kept in memory by
    /// the CheckpointService; JSON round-trips through <see cref="ToJson"/>/<see cref="FromJson"/>.
    /// </summary>
    [Serializable]
    public class CheckpointSnapshot
    {
        /// <summary>Checkpoint id ("CP3b").</summary>
        public string CheckpointId;
        /// <summary>Format version.</summary>
        public int Version = 1;
        /// <summary>Real time of capture, seconds since startup (diagnostics).</summary>
        public float CapturedAt;
        /// <summary>Records, one per saveable key.</summary>
        public List<SaveRecord> Records = new List<SaveRecord>();

        /// <summary>Record for a key, or null.</summary>
        public SaveRecord Find(string key)
        {
            for (var i = 0; i < Records.Count; i++)
                if (Records[i].Key == key) return Records[i];
            return null;
        }

        /// <summary>Gets or creates a record.</summary>
        public SaveRecord GetOrAdd(string key)
        {
            var r = Find(key);
            if (r != null) return r;
            r = new SaveRecord { Key = key };
            Records.Add(r);
            return r;
        }

        /// <summary>Captures every saveable (exceptions are logged, never swallowed silently; that saveable is skipped).</summary>
        public static CheckpointSnapshot Capture(string checkpointId, IEnumerable<ISaveable> saveables)
        {
            var snap = new CheckpointSnapshot { CheckpointId = checkpointId, CapturedAt = Time.realtimeSinceStartup };
            if (saveables == null) return snap;
            foreach (var s in saveables)
            {
                if (s == null || (s is UnityEngine.Object o && o == null)) continue;
                var key = s.SaveKey;
                if (string.IsNullOrEmpty(key))
                {
                    LSLog.Error(LogCategory.Save, "ISaveable with an empty SaveKey: " + s);
                    continue;
                }
                if (snap.Find(key) != null)
                {
                    LSLog.Error(LogCategory.Save, "Duplicate SaveKey '" + key + "' (" + s + ")");
                    continue;
                }
                var rec = snap.GetOrAdd(key);
                try
                {
                    s.Capture(new SaveWriter(rec));
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Save, e);
                    snap.Records.Remove(rec);
                }
            }
            return snap;
        }

        /// <summary>Restores every saveable that has a record; returns how many were restored.</summary>
        public int RestoreInto(IEnumerable<ISaveable> saveables)
        {
            var n = 0;
            if (saveables == null) return 0;
            foreach (var s in saveables)
            {
                if (s == null || (s is UnityEngine.Object o && o == null)) continue;
                var rec = Find(s.SaveKey);
                if (rec == null)
                {
                    LSLog.Warn(LogCategory.Save, "No record for '" + s.SaveKey + "' in " + CheckpointId + "; left as is");
                    continue;
                }
                try
                {
                    s.Restore(new SaveReader(rec));
                    n++;
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Save, e);
                }
            }
            return n;
        }

        /// <summary>Serialises to JSON.</summary>
        public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);

        /// <summary>Parses JSON (null on error, logged).</summary>
        public static CheckpointSnapshot FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<CheckpointSnapshot>(json);
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Save, "Snapshot JSON invalid: " + e.Message);
                return null;
            }
        }
    }
}
