using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Reads fields written by <see cref="SaveWriter"/>. Every read takes a fallback that is returned when the field is
    /// missing or malformed, so older snapshots stay loadable.
    /// </summary>
    public sealed class SaveReader
    {
        private readonly SaveRecord _record;

        /// <summary>Creates a reader (null record = every field missing).</summary>
        public SaveReader(SaveRecord record) => _record = record ?? new SaveRecord();

        /// <summary>Key of the record.</summary>
        public string Key => _record.Key;

        /// <summary>True if the field exists.</summary>
        public bool Has(string name) => _record.IndexOf(name) >= 0;

        /// <summary>Field names present.</summary>
        public IReadOnlyList<string> Names => _record.Names;

        /// <summary>Reads an int.</summary>
        public int ReadInt(string name, int fallback = 0)
        {
            var s = _record.Get(name);
            return s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        /// <summary>Reads a float.</summary>
        public float ReadFloat(string name, float fallback = 0f)
        {
            var s = _record.Get(name);
            return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        /// <summary>Reads a double.</summary>
        public double ReadDouble(string name, double fallback = 0d)
        {
            var s = _record.Get(name);
            return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        /// <summary>Reads a bool.</summary>
        public bool ReadBool(string name, bool fallback = false)
        {
            var s = _record.Get(name);
            if (s == "1") return true;
            if (s == "0") return false;
            return fallback;
        }

        /// <summary>Reads a string.</summary>
        public string ReadString(string name, string fallback = null)
        {
            var i = _record.IndexOf(name);
            return i >= 0 ? _record.Values[i] : fallback;
        }

        /// <summary>Reads a Vector3.</summary>
        public Vector3 ReadVector3(string name, Vector3 fallback = default)
        {
            var p = Split(name, 3);
            return p == null ? fallback : new Vector3(p[0], p[1], p[2]);
        }

        /// <summary>Reads a Quaternion.</summary>
        public Quaternion ReadQuaternion(string name, Quaternion fallback)
        {
            var p = Split(name, 4);
            return p == null ? fallback : new Quaternion(p[0], p[1], p[2], p[3]);
        }

        /// <summary>Reads a Color.</summary>
        public Color ReadColor(string name, Color fallback)
        {
            var p = Split(name, 4);
            return p == null ? fallback : new Color(p[0], p[1], p[2], p[3]);
        }

        /// <summary>Reads a string list into <paramref name="results"/> (cleared first). Returns false if missing.</summary>
        public bool ReadStrings(string name, List<string> results)
        {
            results.Clear();
            var l = SaveWriter.ParseStrings(_record.Get(name));
            if (l == null) return false;
            results.AddRange(l);
            return true;
        }

        /// <summary>Reads a JsonUtility object, or the fallback.</summary>
        public T ReadJson<T>(string name, T fallback = default)
        {
            var s = _record.Get(name);
            if (string.IsNullOrEmpty(s)) return fallback;
            try
            {
                return JsonUtility.FromJson<T>(s);
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Save, "Bad JSON in " + Key + "." + name + ": " + e.Message);
                return fallback;
            }
        }

        private float[] Split(string name, int count)
        {
            var s = _record.Get(name);
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split(',');
            if (parts.Length != count) return null;
            var r = new float[count];
            for (var i = 0; i < count; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out r[i])) return null;
            return r;
        }
    }
}
