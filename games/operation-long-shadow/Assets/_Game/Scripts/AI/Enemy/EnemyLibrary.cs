using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// LIB_Enemies (TECH_PLAN D12): enemy prefab per variant id, written by EnemyPrefabBuilder to
    /// <see cref="AssetPath"/> (WP04's own prefab folder) and referenced by PF_AIServices' EncounterSpawner. A variant
    /// without an entry spawns a runtime <see cref="Common.DebugCapsuleBody"/> soldier (logged once), so encounters run
    /// before any body asset lands.
    /// </summary>
    [CreateAssetMenu(fileName = "LIB_Enemies", menuName = "LongShadow/Libraries/Enemies")]
    public sealed class EnemyLibrary : AssetLibrary<EnemyPrefabEntry>
    {
        /// <summary>Asset path written by EnemyPrefabBuilder.</summary>
        public const string AssetPath = "Assets/_Game/Prefabs/Enemies/LIB_Enemies.asset";

        /// <summary>The prefab of a variant, or null (no logging).</summary>
        public GameObject PrefabFor(string variantId) => TryGet(variantId, out var e) && e != null ? e.Prefab : null;
    }
}
