using System.Collections.Generic;
using LongShadow.Platform;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>TECH_PLAN §7.16: pooled instances are reused (no Instantiate after warm-up).</summary>
    public class PoolServiceTests
    {
        private GameObject _host;
        private GameObject _prefab;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void Make()
        {
            _host = new GameObject("PoolHost");
            _prefab = new GameObject("PF_TestTracer");
            _prefab.AddComponent<BoxCollider>();
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var g in _spawned)
                if (g != null) Object.DestroyImmediate(g);
            _spawned.Clear();
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_prefab);
        }

        private GameObject Spawn(PoolService p)
        {
            var g = p.Spawn(_prefab, new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
            _spawned.Add(g);
            return g;
        }

        [Test]
        public void PrewarmedInstancesAreReusedBeforeNewOnesAreMade()
        {
            var pool = _host.AddComponent<PoolService>();
            pool.Prewarm(_prefab, 3);
            var prewarmed = new HashSet<GameObject>();
            var root = _host.transform.Find("PoolRoot/Pool_" + _prefab.name);
            Assert.IsNotNull(root);
            foreach (Transform t in root) prewarmed.Add(t.gameObject);
            Assert.That(prewarmed.Count, Is.EqualTo(3));

            for (var i = 0; i < 3; i++)
            {
                var g = Spawn(pool);
                Assert.IsTrue(prewarmed.Contains(g), "spawn " + i + " should reuse a prewarmed instance");
                Assert.IsTrue(g.activeSelf);
                Assert.IsNull(g.transform.parent);
                Assert.That(g.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            }
            var fourth = Spawn(pool);
            Assert.IsFalse(prewarmed.Contains(fourth), "the pool grows when it runs dry");
        }

        [Test]
        public void DespawnReturnsTheInstanceForTheNextSpawn()
        {
            var pool = _host.AddComponent<PoolService>();
            var a = Spawn(pool);
            pool.Despawn(a);
            Assert.IsFalse(a.activeSelf);
            pool.Despawn(a); // double despawn is ignored
            var b = Spawn(pool);
            Assert.That(b, Is.SameAs(a));
            Assert.That(pool.PoolCount, Is.EqualTo(1));
        }

        [Test]
        public void ForeignInstancesAreDestroyedOnDespawn()
        {
            var pool = _host.AddComponent<PoolService>();
            var foreign = new GameObject("NotPooled");
            pool.Despawn(foreign);
            Assert.IsTrue(foreign == null);
        }

        [Test]
        public void NullPrefabIsAnError()
        {
            var pool = _host.AddComponent<PoolService>();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("null prefab"));
            Assert.IsNull(pool.Spawn(null, Vector3.zero, Quaternion.identity));
        }
    }
}
