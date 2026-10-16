using UnityEngine;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// One pooled instance of a burst effect (impact, explosion, muzzle smoke): every child particle system simulates in
    /// world space and is fed with <c>Emit</c>, so a single instance serves any number of simultaneous impacts without
    /// instantiating (TECH_PLAN §7.16). Counts come from each system's first emission burst (min..max).
    /// </summary>
    public sealed class ParticleBurst
    {
        private readonly Transform _root;
        private readonly ParticleSystem[] _systems;
        private readonly int[] _min;
        private readonly int[] _max;

        /// <summary>Wraps a live instance (already in the scene).</summary>
        public ParticleBurst(GameObject instance)
        {
            _root = instance.transform;
            _systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            _min = new int[_systems.Length];
            _max = new int[_systems.Length];
            for (var i = 0; i < _systems.Length; i++)
            {
                var em = _systems[i].emission;
                if (em.burstCount > 0)
                {
                    var b = em.GetBurst(0);
                    _min[i] = Mathf.RoundToInt(b.count.mode == ParticleSystemCurveMode.TwoConstants ? b.count.constantMin : b.count.constant);
                    _max[i] = Mathf.RoundToInt(b.count.mode == ParticleSystemCurveMode.TwoConstants ? b.count.constantMax : b.count.constant);
                }
                else
                {
                    _min[i] = _max[i] = 0;
                }
                var main = _systems[i].main;
                main.playOnAwake = false;
            }
        }

        /// <summary>The instance root.</summary>
        public Transform Root => _root;

        /// <summary>Emits every system once at a pose. <paramref name="countScale"/> scales the burst counts.</summary>
        public void Emit(Vector3 position, Quaternion rotation, float countScale = 1f)
        {
            if (_root == null) return;
            _root.SetPositionAndRotation(position, rotation);
            for (var i = 0; i < _systems.Length; i++)
            {
                var ps = _systems[i];
                if (ps == null || _max[i] <= 0) continue;
                var n = Mathf.RoundToInt(Random.Range(_min[i], _max[i] + 1) * countScale);
                if (n > 0) ps.Emit(n);
            }
        }

        /// <summary>Emits with a start-colour override (tinted smoke, dust per surface).</summary>
        public void Emit(Vector3 position, Quaternion rotation, Color tint, float countScale = 1f)
        {
            if (_root == null) return;
            _root.SetPositionAndRotation(position, rotation);
            for (var i = 0; i < _systems.Length; i++)
            {
                var ps = _systems[i];
                if (ps == null || _max[i] <= 0) continue;
                var n = Mathf.RoundToInt(Random.Range(_min[i], _max[i] + 1) * countScale);
                if (n <= 0) continue;
                var ep = new ParticleSystem.EmitParams { startColor = tint, applyShapeToPosition = true };
                ps.Emit(ep, n);
            }
        }

        /// <summary>Removes every live particle.</summary>
        public void Clear()
        {
            for (var i = 0; i < _systems.Length; i++)
                if (_systems[i] != null) _systems[i].Clear(true);
        }
    }
}
