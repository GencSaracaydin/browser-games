using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Spawning through <see cref="IPoolService"/> with a fallback to Instantiate/Destroy when the service is not
    /// registered (test scenes, before WP01 lands).
    /// </summary>
    public static class Pools
    {
        private static ServiceRef<IPoolService> s_pool;

        /// <summary>Spawns from the pool, or instantiates when there is no pool service.</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float autoDespawn = 0f)
        {
            if (prefab == null)
            {
                LSLog.Error(LogCategory.Core, "Pools.Spawn(null prefab)");
                return null;
            }
            if (s_pool.TryGet(out var pool)) return pool.Spawn(prefab, position, rotation, autoDespawn);
            var go = Object.Instantiate(prefab, position, rotation);
            if (autoDespawn > 0f) Object.Destroy(go, autoDespawn);
            return go;
        }

        /// <summary>Despawns to the pool, or destroys when there is no pool service.</summary>
        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;
            if (s_pool.TryGet(out var pool)) pool.Despawn(instance);
            else Object.Destroy(instance);
        }

        /// <summary>Prewarms when a pool service exists (no-op otherwise).</summary>
        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab != null && s_pool.TryGet(out var pool)) pool.Prewarm(prefab, count);
        }
    }
}
