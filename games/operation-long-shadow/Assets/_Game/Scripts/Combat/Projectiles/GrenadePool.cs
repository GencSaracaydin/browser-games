using System.Collections.Generic;
using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Projectiles
{
    /// <summary>
    /// Pool of thrown frags (TECH_PLAN §7.16: grenades 8). The prefab is <c>LIB_CombatFx/Projectile.M67</c> (the M67 world
    /// model with a Rigidbody, collider and <see cref="Grenade"/>); without it a dark-olive sphere stand-in is built.
    /// </summary>
    public sealed class GrenadePool
    {
        private readonly List<Grenade> _all = new List<Grenade>();
        private readonly Stack<Grenade> _free = new Stack<Grenade>();
        private readonly Transform _root;
        private readonly GameObject _prefab;

        /// <summary>Creates and prewarms the pool.</summary>
        public GrenadePool(Transform root, CombatFxLibrary lib, int count)
        {
            _root = root;
            if (lib != null && lib.TryGet(FxIds.GrenadeProjectile, out var e)) _prefab = e.Prefab;
            for (var i = 0; i < count; i++) _free.Push(Create(i));
        }

        /// <summary>Live grenades (checkpoint deferral reads <see cref="AnyLive"/>).</summary>
        public bool AnyLive
        {
            get
            {
                for (var i = 0; i < _all.Count; i++)
                    if (_all[i] != null && _all[i].IsLive) return true;
                return false;
            }
        }

        /// <summary>Throws a grenade.</summary>
        public Grenade Launch(Vector3 position, Vector3 velocity, float fuse, Team team, GameObject instigator)
        {
            var g = _free.Count > 0 ? _free.Pop() : StealOldest();
            if (g == null) g = Create(_all.Count);
            g.Launch(position, velocity, fuse, team, instigator);
            return g;
        }

        /// <summary>Removes every live grenade without exploding.</summary>
        public void Clear()
        {
            for (var i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i].gameObject.activeSelf) _all[i].Defuse();
        }

        private Grenade StealOldest()
        {
            for (var i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i].gameObject.activeSelf)
                {
                    _all[i].Defuse();
                    return _free.Count > 0 ? _free.Pop() : null;
                }
            return null;
        }

        private void Release(Grenade g)
        {
            if (g == null) return;
            g.gameObject.SetActive(false);
            if (!_free.Contains(g)) _free.Push(g);
        }

        private Grenade Create(int index)
        {
            GameObject go;
            if (_prefab != null)
            {
                go = Object.Instantiate(_prefab, _root);
            }
            else
            {
                go = Placeholders.Create("Grenade_M67", PrimitiveType.Sphere, new Vector3(0.064f, 0.075f, 0.064f), new Color(0.24f, 0.27f, 0.18f), _root, true);
            }
            go.name = "Grenade_" + index;
            go.layer = LayerIds.Projectile;
            if (!go.TryGetComponent<Collider>(out var col))
            {
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = 0.035f;
                col = sc;
            }
            if (col.sharedMaterial == null)
                col.sharedMaterial = new PhysicsMaterial("PM_Grenade") { bounciness = 0.25f, dynamicFriction = 0.55f, staticFriction = 0.6f, bounceCombine = PhysicsMaterialCombine.Average };
            if (!go.TryGetComponent<Rigidbody>(out var rb)) rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (!go.TryGetComponent<Grenade>(out var g)) g = go.AddComponent<Grenade>();
            g.Bind(Release);
            go.SetActive(false);
            _all.Add(g);
            return g;
        }
    }
}
