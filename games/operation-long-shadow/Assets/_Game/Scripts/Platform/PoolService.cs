using System.Collections.Generic;
using System.Text;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace LongShadow.Platform
{
    /// <summary>
    /// <see cref="IPoolService"/> (TECH_PLAN §7.1, §7.16): one <see cref="ObjectPool{T}"/> per prefab. Idle instances
    /// wait inactive under an inactive per-prefab root below this object (S_Boot, never unloaded), so prewarmed copies
    /// run Awake/OnEnable only when first spawned. <see cref="Spawn"/> unparents, places and activates;
    /// <see cref="Despawn"/> deactivates and returns the instance (instances that did not come from a pool are
    /// destroyed). Timed despawns use scaled time (effects freeze with the game while paused). Instances destroyed
    /// by their owners are skipped safely. No per-frame allocation after warm-up (R2).
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform)]
    [DisallowMultipleComponent]
    public sealed class PoolService : MonoBehaviour, IPoolService
    {
        [SerializeField, Tooltip("Maximum idle instances kept per prefab (extra despawns are destroyed).")]
        private int _maxIdlePerPrefab = 256;

        private sealed class Pool
        {
            public GameObject Prefab;
            public Transform Root;
            public ObjectPool<GameObject> Inner;
            public int Spawned;
        }

        private struct Timed
        {
            public GameObject Instance;
            public int Generation;
            public float At;
        }

        private readonly Dictionary<GameObject, Pool> _pools = new Dictionary<GameObject, Pool>(32);
        private readonly Dictionary<GameObject, Pool> _owner = new Dictionary<GameObject, Pool>(256);
        private readonly Dictionary<GameObject, int> _generation = new Dictionary<GameObject, int>(256);
        private readonly HashSet<GameObject> _idle = new HashSet<GameObject>();
        private readonly List<Timed> _timed = new List<Timed>(64);
        private Transform _root;
        private bool _registered;

        /// <summary>Number of prefabs with a pool.</summary>
        public int PoolCount => _pools.Count;

        private void Awake()
        {
            EnsureRoot();
            GameServices.Register<IPoolService>(this);
            _registered = true;
        }

        private void OnDestroy()
        {
            if (_registered) GameServices.Unregister<IPoolService>(this);
            foreach (var p in _pools.Values) p.Inner.Dispose();
            _pools.Clear();
            _owner.Clear();
            _idle.Clear();
            _timed.Clear();
        }

        /// <inheritdoc/>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float autoDespawn = 0f)
        {
            if (prefab == null)
            {
                LSLog.Error(LogCategory.Core, "PoolService.Spawn(null prefab)", this);
                return null;
            }
            var pool = GetPool(prefab);
            GameObject go = null;
            for (var attempt = 0; attempt < 64 && go == null; attempt++)
            {
                go = pool.Inner.Get();
                if (go == null) continue; // destroyed while idle (scene unload, someone called Destroy)
            }
            if (go == null) go = CreateInstance(pool);

            _idle.Remove(go);
            var gen = _generation.TryGetValue(go, out var g) ? g + 1 : 1;
            _generation[go] = gen;
            var t = go.transform;
            t.SetParent(null, false);
            t.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            pool.Spawned++;
            if (autoDespawn > 0f)
                _timed.Add(new Timed { Instance = go, Generation = gen, At = Time.time + autoDespawn });
            return go;
        }

        /// <inheritdoc/>
        public void Despawn(GameObject instance)
        {
            if (instance == null) return;
            if (!_owner.TryGetValue(instance, out var pool) || pool.Inner == null)
            {
                DestroySafe(instance);
                return;
            }
            if (_idle.Contains(instance)) return; // double despawn
            _generation[instance] = _generation.TryGetValue(instance, out var g) ? g + 1 : 1; // cancels pending timed despawns
            instance.SetActive(false);
            instance.transform.SetParent(pool.Root, false);
            _idle.Add(instance);
            pool.Spawned = Mathf.Max(0, pool.Spawned - 1);
            pool.Inner.Release(instance);
        }

        /// <inheritdoc/>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            var pool = GetPool(prefab);
            var missing = Mathf.Min(count, _maxIdlePerPrefab) - pool.Inner.CountInactive;
            for (var i = 0; i < missing; i++)
            {
                var go = CreateInstance(pool);
                _idle.Add(go);
                pool.Inner.Release(go);
            }
        }

        /// <summary>Destroys the idle instances of every pool (e.g. after a section unload); spawned ones stay tracked.</summary>
        public void TrimIdle()
        {
            foreach (var p in _pools.Values) p.Inner.Clear();
        }

        /// <summary>One line per pool: prefab, spawned, idle (dev console; allocates).</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            foreach (var p in _pools.Values)
                sb.Append(p.Prefab != null ? p.Prefab.name : "(destroyed prefab)").Append(": spawned ").Append(p.Spawned)
                  .Append(", idle ").Append(p.Inner.CountInactive).Append('\n');
            return sb.ToString();
        }

        private void Update()
        {
            if (_timed.Count == 0) return;
            var now = Time.time;
            for (var i = _timed.Count - 1; i >= 0; i--)
            {
                var t = _timed[i];
                if (t.At > now) continue;
                _timed.RemoveAt(i);
                if (t.Instance == null) continue;
                if (_generation.TryGetValue(t.Instance, out var g) && g == t.Generation) Despawn(t.Instance);
            }
        }

        private Pool GetPool(GameObject prefab)
        {
            if (_pools.TryGetValue(prefab, out var pool)) return pool;
            EnsureRoot();
            var root = new GameObject("Pool_" + prefab.name).transform;
            root.SetParent(_root, false);
            pool = new Pool { Prefab = prefab, Root = root };
            var p = pool;
            pool.Inner = new ObjectPool<GameObject>(
                () => CreateInstance(p),
                null,
                null,
                go =>
                {
                    _owner.Remove(go);
                    _idle.Remove(go);
                    _generation.Remove(go);
                    if (go != null) DestroySafe(go);
                },
                false, 8, Mathf.Max(1, _maxIdlePerPrefab));
            _pools[prefab] = pool;
            return pool;
        }

        private GameObject CreateInstance(Pool pool)
        {
            var go = Instantiate(pool.Prefab, pool.Root, false);
            go.name = pool.Prefab.name;
            go.SetActive(false);
            _owner[go] = pool;
            return go;
        }

        private static void DestroySafe(Object o)
        {
            // EditMode tests and editor tools run pools outside play mode, where Destroy is not allowed.
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        private void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("PoolRoot");
            go.transform.SetParent(transform, false);
            go.SetActive(false); // inactive parent: prewarmed instances do not Awake until spawned
            _root = go.transform;
        }
    }
}
