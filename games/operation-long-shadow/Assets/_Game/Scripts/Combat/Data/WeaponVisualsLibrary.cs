using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// <c>Assets/_Game/Data/Libraries/LIB_WeaponVisuals.asset</c> (TECH_PLAN D12, §7.5): weapon id → prefabs. Built by
    /// <c>LongShadow/Combat/Build Weapon Prefabs</c>; registered by the LibraryRegistry in S_Persistent (WP11).
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/Libraries/Weapon Visuals", fileName = "LIB_WeaponVisuals")]
    public sealed class WeaponVisualsLibrary : AssetLibrary<WeaponVisualEntry>
    {
        /// <summary>Asset path written by the builder.</summary>
        public const string AssetPath = Libraries.Folder + "/LIB_WeaponVisuals.asset";

        /// <summary>World prefab of a weapon id, or null (for bodies holding a weapon; logs once when missing).</summary>
        public GameObject WorldPrefab(string weaponId) => TryGet(weaponId, out var e) ? e.World : null;

        /// <summary>View-model prefab of a weapon id, or null.</summary>
        public GameObject ViewmodelPrefab(string weaponId) => TryGet(weaponId, out var e) ? e.Viewmodel : null;
    }
}
