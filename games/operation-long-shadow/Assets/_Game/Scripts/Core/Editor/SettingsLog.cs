using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Before/after log for idempotent settings scripts (ProjectSetup). Every Set* call records either
    /// "= name: value" (already correct) or "SET name: before -> after" (or "DIFF" in dry-run mode).
    /// </summary>
    public sealed class SettingsLog
    {
        private readonly StringBuilder _sb = new StringBuilder();

        /// <summary>When true nothing is changed; differences are recorded as DIFF.</summary>
        public bool DryRun { get; }
        /// <summary>Number of values changed (or that would change in dry-run mode).</summary>
        public int Changes { get; private set; }
        /// <summary>Number of values checked.</summary>
        public int Checked { get; private set; }
        /// <summary>Problems (missing properties, failed writes).</summary>
        public List<string> Problems { get; } = new List<string>();

        /// <summary>Creates a log.</summary>
        public SettingsLog(bool dryRun) => DryRun = dryRun;

        /// <summary>Adds a section header.</summary>
        public void Section(string title) => _sb.AppendLine().AppendLine("## " + title);

        /// <summary>Adds a free-form note.</summary>
        public void Note(string text) => _sb.AppendLine("  note: " + text);

        /// <summary>Records a problem.</summary>
        public void Problem(string text)
        {
            Problems.Add(text);
            _sb.AppendLine("  PROBLEM: " + text);
        }

        /// <summary>Sets a value through getter/setter delegates if it differs.</summary>
        public void Set<T>(string name, Func<T> get, Action<T> set, T value)
        {
            Checked++;
            T before;
            try
            {
                before = get();
            }
            catch (Exception e)
            {
                Problem(name + ": read failed: " + e.Message);
                return;
            }
            if (Same(before, value))
            {
                _sb.AppendLine("  = " + name + ": " + Fmt(before));
                return;
            }
            Changes++;
            if (DryRun)
            {
                _sb.AppendLine("  DIFF " + name + ": " + Fmt(before) + " -> " + Fmt(value));
                return;
            }
            try
            {
                set(value);
                var after = get();
                _sb.AppendLine("  SET " + name + ": " + Fmt(before) + " -> " + Fmt(after));
                if (!Same(after, value)) Problem(name + ": wrote " + Fmt(value) + " but reads back " + Fmt(after));
            }
            catch (Exception e)
            {
                Problem(name + ": write failed: " + e.Message);
            }
        }

        /// <summary>Sets a serialized property (int, bool, float, string, enum index, object reference, vectors).</summary>
        public void SetProp(SerializedObject so, string path, object value, string label = null)
        {
            var name = (label ?? so.targetObject.name) + "." + path;
            var p = so.FindProperty(path);
            if (p == null)
            {
                Problem(name + ": property not found");
                return;
            }
            SetProp(p, value, name);
        }

        /// <summary>Sets a property directly.</summary>
        public void SetProp(SerializedProperty p, object value, string name)
        {
            Checked++;
            var before = Read(p);
            if (Same(before, value))
            {
                _sb.AppendLine("  = " + name + ": " + Fmt(before));
                return;
            }
            Changes++;
            if (DryRun)
            {
                _sb.AppendLine("  DIFF " + name + ": " + Fmt(before) + " -> " + Fmt(value));
                return;
            }
            if (!Write(p, value))
            {
                Problem(name + ": unsupported property type " + p.propertyType);
                return;
            }
            p.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            p.serializedObject.Update();
            _sb.AppendLine("  SET " + name + ": " + Fmt(before) + " -> " + Fmt(Read(p)));
        }

        /// <summary>Records an action that is not a simple value (created asset, added feature).</summary>
        public void Step(string description, bool changed)
        {
            Checked++;
            if (changed) Changes++;
            _sb.AppendLine((changed ? (DryRun ? "  DIFF " : "  DO ") : "  = ") + description);
        }

        private static object Read(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.longValue;
                case SerializedPropertyType.Boolean: return p.boolValue;
                case SerializedPropertyType.Float: return p.floatValue;
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum: return (long)p.intValue;
                case SerializedPropertyType.ObjectReference: return p.objectReferenceValue;
                case SerializedPropertyType.Vector2: return p.vector2Value;
                case SerializedPropertyType.Vector3: return p.vector3Value;
                case SerializedPropertyType.Vector4: return p.vector4Value;
                case SerializedPropertyType.Color: return p.colorValue;
                case SerializedPropertyType.LayerMask: return (long)p.intValue;
                default: return "<" + p.propertyType + ">";
            }
        }

        private static bool Write(SerializedProperty p, object v)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                    p.longValue = Convert.ToInt64(v, CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.Enum:
                    p.intValue = Convert.ToInt32(v, CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.Boolean:
                    p.boolValue = Convert.ToBoolean(v, CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.Float:
                    p.floatValue = Convert.ToSingle(v, CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.String:
                    p.stringValue = (string)v;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    p.objectReferenceValue = (UnityEngine.Object)v;
                    return true;
                case SerializedPropertyType.Vector2:
                    p.vector2Value = (Vector2)v;
                    return true;
                case SerializedPropertyType.Vector3:
                    p.vector3Value = (Vector3)v;
                    return true;
                case SerializedPropertyType.Vector4:
                    p.vector4Value = (Vector4)v;
                    return true;
                case SerializedPropertyType.Color:
                    p.colorValue = (Color)v;
                    return true;
                default:
                    return false;
            }
        }

        private static bool Same(object a, object b)
        {
            if (a == null || b == null) return (a == null || a is UnityEngine.Object ua && ua == null) && (b == null || b is UnityEngine.Object ub && ub == null);
            if (a is UnityEngine.Object || b is UnityEngine.Object) return ReferenceEquals(a, b);
            if (IsNumber(a) && IsNumber(b))
            {
                var da = Convert.ToDouble(a, CultureInfo.InvariantCulture);
                var db = Convert.ToDouble(b, CultureInfo.InvariantCulture);
                return Math.Abs(da - db) <= 1e-5 * Math.Max(1.0, Math.Abs(db));
            }
            if (a is bool ba && IsNumber(b)) return ba == (Convert.ToDouble(b, CultureInfo.InvariantCulture) != 0);
            if (a is Enum || b is Enum)
            {
                try
                {
                    return Convert.ToInt64(a, CultureInfo.InvariantCulture) == Convert.ToInt64(b, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    return Equals(a, b);
                }
            }
            if (a is Vector2 v2a && b is Vector2 v2b) return (v2a - v2b).sqrMagnitude < 1e-10f;
            if (a is Vector3 v3a && b is Vector3 v3b) return (v3a - v3b).sqrMagnitude < 1e-10f;
            if (a is Vector4 v4a && b is Vector4 v4b) return (v4a - v4b).sqrMagnitude < 1e-10f;
            return Equals(a, b);
        }

        private static bool IsNumber(object o) =>
            o is int || o is long || o is float || o is double || o is short || o is byte || o is uint || o is ulong;

        private static string Fmt(object o)
        {
            if (o == null) return "null";
            if (o is UnityEngine.Object uo) return uo == null ? "null" : uo.name + " (" + AssetDatabase.GetAssetPath(uo) + ")";
            if (o is float f) return f.ToString("0.#####", CultureInfo.InvariantCulture);
            if (o is double d) return d.ToString("0.#####", CultureInfo.InvariantCulture);
            if (o is string s) return "\"" + s + "\"";
            return Convert.ToString(o, CultureInfo.InvariantCulture);
        }

        /// <summary>The full log text.</summary>
        public override string ToString() => _sb.ToString();

        /// <summary>Writes the log to a file (project-relative) with a header; returns the absolute path.</summary>
        public string WriteTo(string projectRelativePath, string header)
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var path = Path.Combine(root, projectRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var text = header + "\n" + "checked " + Checked + ", " + (DryRun ? "would change " : "changed ") + Changes +
                       ", problems " + Problems.Count + "\n" + _sb;
            File.WriteAllText(path, text);
            return path;
        }
    }
}
