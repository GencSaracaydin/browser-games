using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// <c>Assets/_Game/Data/Libraries/LIB_CombatFx.asset</c>: tracer, muzzle, impact, decal, explosion, smoke, casing,
    /// projectile and crate assets by <see cref="FxIds"/> id. Built by <c>LongShadow/Combat/Build FX Library</c>; registered by
    /// the LibraryRegistry in S_Persistent (WP11). Missing entries fall back to procedural FX (FxFactory).
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/Libraries/Combat FX", fileName = "LIB_CombatFx")]
    public sealed class CombatFxLibrary : AssetLibrary<CombatFxEntry>
    {
        /// <summary>Asset path written by the builder.</summary>
        public const string AssetPath = Libraries.Folder + "/LIB_CombatFx.asset";
    }
}
