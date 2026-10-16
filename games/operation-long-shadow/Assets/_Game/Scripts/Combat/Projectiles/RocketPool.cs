using System;
using System.Collections.Generic;
using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Projectiles
{
    /// <summary>
    /// Pool of RPG rockets (4). The prefab is <c>LIB_CombatFx/Projectile.RPG7</c> (rocket world model); a smoke trail from
    /// <c>Smoke.Trail</c> (or FxFactory) is attached to each. Without the model an olive cone stand-in is built.
    /// </summary>
    public sealed class RocketPool
    {
        private readonly List<Rocket> _all = new List<Rocket>();
        private readonly Stack<Rocket> _free = new Stack<Rocket>();
        private readonly Transform _root;
        private readonly CombatFxLibrary _lib;
        private readonly FxMaterialSet _mats;
        private readonly GameObject _prefab;

        /// <summary>Creates and prewarms the pool.</summary>
        public RocketPool(Transform root, CombatFxLibrary lib, FxMaterialSet mats, int count)
        {
            _root = root;
            _lib = lib;
            _mats = mats;
            if (lib != null && lib.TryGet(FxIds.RocketProjectile, out var e)) _prefab = e.Prefab;
            for (var i = 0; i < count; i++) _free.Push(Create(i));
        }

        /// <summary>Fires a rocket.</summary>
        public Rocket Launch(Vector3 from, Vector3 to, Team team, GameObject instigator, Action<Vector3> onImpact)
        {
            var r = _free.Count > 0 ? _free.Pop() : Create(_all.Count);
            r.Launch(from, to, team, instigator, onImpact);
            return r;
        }

        /// <summary>Removes every rocket without exploding.</summary>
        public void Clear()
        {
            for (var i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i].gameObject.activeSelf) _all[i].Cancel();
        }

        private void Release(Rocket r)
        {
            if (r == null) return;
            r.gameObject.SetActive(false);
            if (!_free.Contains(r)) _free.Push(r);
        }

        private Rocket Create(int index)
        {
            GameObject go;
            if (_prefab != null) go = UnityEngine.Object.Instantiate(_prefab, _root);
            else
            {
                go = new GameObject();
                go.transform.SetParent(_root, false);
                var body = Placeholders.Create("Rocket_Body", PrimitiveType.Cylinder, new Vector3(0.085f, 0.4f, 0.085f), new Color(0.3f, 0.32f, 0.22f), go.transform);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var head = Placeholders.Create("Rocket_Head", PrimitiveType.Sphere, new Vector3(0.085f, 0.085f, 0.2f), new Color(0.28f, 0.3f, 0.2f), go.transform);
                head.transform.localPosition = new Vector3(0f, 0f, 0.42f);
            }
            go.name = "Rocket_" + index;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false; // the rocket sweeps, it does not collide
            var trailGo = CombatFx.InstantiateOrBuild(_lib, FxIds.SmokeTrail, go.transform, () => FxFactory.BuildSmokeTrail(_mats));
            trailGo.transform.localPosition = new Vector3(0f, 0f, -0.45f);
            trailGo.transform.localRotation = Quaternion.identity;
            if (!go.TryGetComponent<Rocket>(out var r)) r = go.AddComponent<Rocket>();
            r.Bind(Release, trailGo.GetComponentsInChildren<ParticleSystem>(true));
            go.SetActive(false);
            _all.Add(r);
            return r;
        }
    }
}
