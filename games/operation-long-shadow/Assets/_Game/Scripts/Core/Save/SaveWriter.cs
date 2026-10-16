using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Writes one saveable's fields into a <see cref="SaveRecord"/>. Numbers use the invariant culture with round-trip
    /// precision; lists and objects use <see cref="JsonUtility"/>. Writing a name twice overwrites it.
    /// </summary>
    public sealed class SaveWriter
    {
        [Serializable]
        private class StringList
        {
            public List<string> Items = new List<string>();
        }

        private readonly SaveRecord _record;

        /// <summary>Creates a writer for a record.</summary>
        public SaveWriter(SaveRecord record) => _record = record ?? throw new ArgumentNullException(nameof(record));

        /// <summary>Key of the record being written.</summary>
        public string Key => _record.Key;

        /// <summary>Writes an int.</summary>
        public void Write(string name, int value) => _record.Set(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Writes a float (round-trip).</summary>
        public void Write(string name, float value) => _record.Set(name, value.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>Writes a double (round-trip).</summary>
        public void Write(string name, double value) => _record.Set(name, value.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>Writes a bool ("1"/"0").</summary>
        public void Write(string name, bool value) => _record.Set(name, value ? "1" : "0");

        /// <summary>Writes a string (null is stored as null).</summary>
        public void Write(string name, string value) => _record.Set(name, value);

        /// <summary>Writes a Vector3.</summary>
        public void Write(string name, Vector3 v) => _record.Set(name, F(v.x) + "," + F(v.y) + "," + F(v.z));

        /// <summary>Writes a Quaternion.</summary>
        public void Write(string name, Quaternion q) => _record.Set(name, F(q.x) + "," + F(q.y) + "," + F(q.z) + "," + F(q.w));

        /// <summary>Writes a Color.</summary>
        public void Write(string name, Color c) => _record.Set(name, F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a));

        /// <summary>Writes a list of strings (null entries become empty strings).</summary>
        public void WriteStrings(string name, IEnumerable<string> values)
        {
            var l = new StringList();
            if (values != null)
                foreach (var v in values) l.Items.Add(v ?? string.Empty);
            _record.Set(name, JsonUtility.ToJson(l));
        }

        /// <summary>Writes a [Serializable] object with JsonUtility.</summary>
        public void WriteJson<T>(string name, T value) => _record.Set(name, value == null ? null : JsonUtility.ToJson(value));

        private static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);

        internal static List<string> ParseStrings(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<StringList>(json)?.Items;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
