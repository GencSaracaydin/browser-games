using System.Collections.Generic;
using LongShadow.Combat.Data;
using UnityEngine;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Visuals of explosions (frag, rocket, vehicle fireball): pooled particle bursts, a light pulse and a scorch decal on
    /// the ground below. Damage, shake, tinnitus and noise are applied by CombatResolver.
    /// </summary>
    public sealed class ExplosionEffects
    {
        private readonly Dictionary<string, ParticleBurst> _bursts = new Dictionary<string, ParticleBurst>();
        private readonly Transform _root;
        private readonly CombatFxLibrary _lib;
        private readonly FxMaterialSet _mats;

        /// <summary>Creates the frag, rocket and fireball bursts.</summary>
        public ExplosionEffects(Transform root, CombatFxLibrary lib, FxMaterialSet mats)
        {
            _root = root;
            _lib = lib;
            _mats = mats;
            Get(FxIds.ExplosionFrag);
            Get(FxIds.ExplosionRocket);
            Get(FxIds.ExplosionFireball);
        }

        /// <summary>Plays the burst of an explosion id at a point (up = the surface normal or world up).</summary>
        public void Play(string fxId, Vector3 position, Vector3 up)
        {
            var b = Get(fxId);
            var n = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
            var fwd = Mathf.Abs(Vector3.Dot(n, Vector3.forward)) > 0.95f ? Vector3.right : Vector3.forward;
            // The systems are authored with their emission axis rotated to world up; align the root's up with the normal.
            b?.Emit(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, n), n));
        }

        /// <summary>Clears every live particle.</summary>
        public void Clear()
        {
            foreach (var kv in _bursts) kv.Value?.Clear();
        }

        private ParticleBurst Get(string id)
        {
            if (string.IsNullOrEmpty(id)) id = FxIds.ExplosionFrag;
            if (_bursts.TryGetValue(id, out var b)) return b;
            var go = CombatFx.InstantiateOrBuild(_lib, id, _root, () => FxFactory.BuildExplosion(id, _mats));
            b = new ParticleBurst(go);
            _bursts[id] = b;
            return b;
        }
    }
}
