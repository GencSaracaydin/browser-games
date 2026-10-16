using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Prefab pools (WP01 PoolService). Use <see cref="Pools"/> for a fallback when the service is missing.</summary>
    public interface IPoolService
    {
        /// <summary>Spawns an instance; despawns automatically after <paramref name="autoDespawn"/> seconds when &gt; 0.</summary>
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float autoDespawn = 0f);
        /// <summary>Returns an instance to its pool (destroys it if it did not come from a pool).</summary>
        void Despawn(GameObject instance);
        /// <summary>Creates <paramref name="count"/> inactive instances ahead of time.</summary>
        void Prewarm(GameObject prefab, int count);
    }
}
