using System;
using LongShadow.Core;
using UnityEditor;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Common
{
    /// <summary>
    /// Adds components owned by other packages without referencing their assemblies (TECH_PLAN §4.3: Level may reference
    /// only Core): WP03's <c>LongShadow.Combat.Targets.Destructible</c> on the floods F1–F4, racks R1–R4 and the Beat 11
    /// trucks (Docs/REQUESTS.md WP03 → WP10). Editor-only reflection (R5 forbids it in runtime code only); when the type has
    /// not landed the call logs once and returns null, and re-running the section builder after WP03 lands adds it.
    /// </summary>
    public static class SoftComponents
    {
        /// <summary>Full name of WP03's Destructible.</summary>
        public const string DestructibleType = "LongShadow.Combat.Targets.Destructible";

        /// <summary>Finds a type by full name in the loaded assemblies.</summary>
        public static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>True when WP03's Destructible is available.</summary>
        public static bool HasDestructible => FindType(DestructibleType) != null;

        /// <summary>
        /// Adds and configures WP03's Destructible (fields as in <c>Staging/Combat/Targets/Destructible.cs</c>). Returns the
        /// component or null when Combat has not landed.
        /// </summary>
        public static Component AddDestructible(GameObject go, string id, SoftDestructibleKind kind, float maxHp, int hitsToDestroy, float hitRadius, Transform core,
                                                bool invulnerable, GameObject[] intact, GameObject[] destroyed, Light[] lights, Renderer[] emissive)
        {
            var t = FindType(DestructibleType);
            if (t == null)
            {
                LSLog.WarnOnce("soft-destructible", LogCategory.Level,
                    "WP03 Destructible is not in the project yet: " + id + " gets no Destructible. Re-run the section builder after Combat lands.");
                return null;
            }
            var c = go.GetComponent(t) ?? go.AddComponent(t);
            var so = new SerializedObject(c);
            Set(so, "_id", p => p.stringValue = id);
            Set(so, "_kind", p => p.intValue = (int)kind);
            Set(so, "_maxHp", p => p.floatValue = maxHp);
            Set(so, "_hitsToDestroy", p => p.intValue = hitsToDestroy);
            Set(so, "_hitRadius", p => p.floatValue = hitRadius);
            Set(so, "_core", p => p.objectReferenceValue = core);
            Set(so, "_invulnerable", p => p.boolValue = invulnerable);
            SetArray(so, "_intact", intact);
            SetArray(so, "_destroyed", destroyed);
            SetArray(so, "_lights", lights);
            SetArray(so, "_emissive", emissive);
            so.ApplyModifiedPropertiesWithoutUndo();
            return c;
        }

        private static void Set(SerializedObject so, string name, Action<SerializedProperty> set)
        {
            var p = so.FindProperty(name);
            if (p == null)
            {
                LSLog.WarnOnce("soft-field:" + name, LogCategory.Level, "Destructible has no serialized field " + name + " (API changed?)");
                return;
            }
            set(p);
        }

        private static void SetArray<T>(SerializedObject so, string name, T[] values) where T : UnityEngine.Object
        {
            var p = so.FindProperty(name);
            if (p == null || !p.isArray) return;
            values = values ?? new T[0];
            p.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
