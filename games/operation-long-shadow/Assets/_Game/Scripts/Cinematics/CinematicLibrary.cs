using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// LIB_Cinematics (TECH_PLAN D12): timelines, CRT images, FX materials, volume profiles, cast prefabs and fonts used by
    /// the cinematics, looked up by id. Built by <c>LongShadow/Cinematics/Build Library</c>
    /// (<c>Assets/_Game/Data/Libraries/LIB_Cinematics.asset</c>) and registered by S_Persistent's LibraryRegistry.
    /// Missing entries return null (logged once) and callers degrade to placeholders.
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/Libraries/Cinematics", fileName = "LIB_Cinematics")]
    public sealed class CinematicLibrary : AssetLibrary<CinematicAssetEntry>
    {
        /// <summary>Asset path written by the builder.</summary>
        public const string AssetPath = "Assets/_Game/Data/Libraries/LIB_Cinematics.asset";

        /// <summary>Typed lookup without logging; null when missing or of another type.</summary>
        public T Find<T>(string id) where T : Object
        {
            if (!TryGet(id, out var e) || e == null) return null;
            return e.Asset as T;
        }

        /// <summary>Typed lookup; logs a missing-asset warning once when the entry or its asset is missing.</summary>
        public T Require<T>(string id) where T : Object
        {
            var a = Find<T>(id);
            if (a == null) LSLog.MissingAsset(LogCategory.Cinematics, id, AssetPath, this);
            return a;
        }

        /// <summary>The registered library (null when LIB_Cinematics is not registered).</summary>
        public static CinematicLibrary Current => Libraries.TryGet(out CinematicLibrary lib) ? lib : null;

        /// <summary>Typed lookup on the registered library without logging.</summary>
        public static T Lookup<T>(string id) where T : Object
        {
            var lib = Current;
            return lib != null ? lib.Find<T>(id) : null;
        }
    }
}
