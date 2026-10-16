using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Weapons
{
    /// <summary>
    /// <c>Assets/_Game/Data/Libraries/LIB_CharacterWeapons.asset</c>: weapon id → held world model for WP05 bodies
    /// (TECH_PLAN D12). Built by <c>LongShadow/Characters/Build Weapon Library</c> from WP03's <c>PF_W_*</c> prefabs
    /// (or the manifest meshes); missing ids fall back to <see cref="WeaponHoldTable"/> and a placeholder gun.
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/Characters/Character Weapon Library", fileName = "LIB_CharacterWeapons")]
    public sealed class CharacterWeaponLibrary : AssetLibrary<CharacterWeaponEntry>
    {
        /// <summary>Asset path the builder writes.</summary>
        public const string AssetPath = "Assets/_Game/Data/Libraries/LIB_CharacterWeapons.asset";
    }
}
