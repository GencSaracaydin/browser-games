using System.Collections.Generic;
using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using UnityEngine;

namespace LongShadow.Combat.Projectiles
{
    /// <summary>Pool of smoke clouds (4; the mission uses one at a time: B3 white, B10 green).</summary>
    public sealed class SmokePool
    {
        private readonly List<SmokeCloud> _all = new List<SmokeCloud>();
        private readonly Stack<SmokeCloud> _free = new Stack<SmokeCloud>();
        private readonly Transform _root;
        private readonly CombatFxLibrary _lib;
        private readonly FxMaterialSet _mats;

        /// <summary>Creates and prewarms the pool.</summary>
        public SmokePool(Transform root, CombatFxLibrary lib, FxMaterialSet mats, int count)
        {
            _root = root;
            _lib = lib;
            _mats = mats;
            for (var i = 0; i < count; i++) _free.Push(Create(i));
        }

        /// <summary>Starts a cloud.</summary>
        public SmokeCloud Spawn(Vector3 position, Color color, float seconds)
        {
            var s = _free.Count > 0 ? _free.Pop() : Create(_all.Count);
            s.Begin(position, color, seconds);
            return s;
        }

        /// <summary>Ends every cloud.</summary>
        public void Clear()
        {
            for (var i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i].gameObject.activeSelf) _all[i].Kill();
        }

        private void Release(SmokeCloud s)
        {
            if (s == null) return;
            s.gameObject.SetActive(false);
            if (!_free.Contains(s)) _free.Push(s);
        }

        private SmokeCloud Create(int index)
        {
            var go = CombatFx.InstantiateOrBuild(_lib, FxIds.SmokeCloud, _root, () => FxFactory.BuildSmokeCloud(_mats));
            go.name = "SmokeCloud_" + index;
            if (!go.TryGetComponent<SmokeCloud>(out var s)) s = go.AddComponent<SmokeCloud>();
            s.Bind(Release, go.GetComponentsInChildren<ParticleSystem>(true));
            go.SetActive(false);
            _all.Add(s);
            return s;
        }
    }
}
