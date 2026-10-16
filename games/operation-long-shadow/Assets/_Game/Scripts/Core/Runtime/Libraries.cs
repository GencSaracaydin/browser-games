using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Runtime registry of library ScriptableObjects (<c>LIB_*</c>, TECH_PLAN D12). <see cref="LibraryRegistry"/> in
    /// S_Persistent registers them on Awake. In the editor, a library that is not registered is loaded from
    /// <c>Assets/_Game/Data/Libraries</c> (so test scenes work without S_Persistent). Missing libraries are logged once.
    /// </summary>
    public static class Libraries
    {
        /// <summary>Folder searched by the editor fallback.</summary>
        public const string Folder = "Assets/_Game/Data/Libraries";

        private static readonly List<ScriptableObject> s_libraries = new List<ScriptableObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_libraries.Clear();

        /// <summary>Registers a library (idempotent).</summary>
        public static void Register(ScriptableObject library)
        {
            if (library != null && !s_libraries.Contains(library)) s_libraries.Add(library);
        }

        /// <summary>Unregisters a library.</summary>
        public static void Unregister(ScriptableObject library) => s_libraries.Remove(library);

        /// <summary>Finds the first registered library of type <typeparamref name="T"/> (no logging).</summary>
        public static bool TryGet<T>(out T library) where T : ScriptableObject
        {
            for (var i = s_libraries.Count - 1; i >= 0; i--)
            {
                var l = s_libraries[i];
                if (l == null)
                {
                    s_libraries.RemoveAt(i);
                    continue;
                }
                if (l is T t)
                {
                    library = t;
                    return true;
                }
            }
#if UNITY_EDITOR
            library = EditorFallback<T>();
            if (library != null)
            {
                Register(library);
                return true;
            }
#endif
            library = null;
            return false;
        }

        /// <summary>Returns the library or null (logs a missing-asset warning once per type).</summary>
        public static T Get<T>() where T : ScriptableObject
        {
            if (TryGet(out T lib)) return lib;
            LSLog.MissingAsset(LogCategory.Assets, "library " + typeof(T).Name, Folder);
            return null;
        }

#if UNITY_EDITOR
        private static T EditorFallback<T>() where T : ScriptableObject
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(Folder)) return null;
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Folder });
            foreach (var g in guids)
            {
                var a = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (a != null) return a;
            }
            return null;
        }
#endif
    }
}
