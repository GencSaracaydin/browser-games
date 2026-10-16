using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Projectiles
{
    /// <summary>
    /// A coloured smoke screen (TECH_PLAN §7.5: "smoke (spawns a SmokeLOS volume for 20 s)"; GDD §7.3 white smoke, §7.10
    /// green smoke). Looping particles plus a solid sphere on layer SmokeLOS that only AI sight rays hit (layer collides with
    /// nothing). The sphere grows to 4.5 m in 3 s, holds for the duration, then the smoke thins out over 4 s.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmokeCloud : MonoBehaviour
    {
        /// <summary>Full blocking radius.</summary>
        public const float FullRadius = 4.5f;
        /// <summary>Seconds to reach full radius.</summary>
        public const float GrowSeconds = 3f;
        /// <summary>Seconds the cloud takes to thin out after its duration.</summary>
        public const float ThinSeconds = 4f;

        private ParticleSystem[] _systems;
        private SphereCollider _blocker;
        private float _age;
        private float _duration;
        private bool _emitting;
        private Action<SmokeCloud> _release;

        /// <summary>True while the cloud blocks sight.</summary>
        public bool IsBlocking => _blocker != null && _blocker.enabled;

        /// <summary>Caches parts (called once by the pool).</summary>
        public void Bind(Action<SmokeCloud> release, ParticleSystem[] systems)
        {
            _release = release;
            _systems = systems;
            var blockerGo = new GameObject("SmokeLOS") { layer = LayerIds.SmokeLOS };
            blockerGo.transform.SetParent(transform, false);
            blockerGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            _blocker = blockerGo.AddComponent<SphereCollider>();
            _blocker.isTrigger = false; // queries ignore triggers (TECH_PLAN §3.9), so the blocker must be solid; SmokeLOS collides with nothing
            _blocker.radius = 0.5f;
            _blocker.enabled = false;
        }

        /// <summary>Starts the cloud.</summary>
        public void Begin(Vector3 position, Color color, float seconds)
        {
            gameObject.SetActive(false);
            transform.SetPositionAndRotation(position, Quaternion.identity);
            gameObject.SetActive(true);
            _age = 0f;
            _duration = Mathf.Max(1f, seconds);
            _emitting = true;
            if (_systems != null)
                foreach (var ps in _systems)
                {
                    if (ps == null) continue;
                    ps.Clear(true);
                    var main = ps.main;
                    var c = color;
                    c.a = main.startColor.color.a > 0f ? main.startColor.color.a : 0.85f;
                    main.startColor = c;
                    ps.Play(true);
                }
            _blocker.radius = 0.5f;
            _blocker.enabled = true;
        }

        /// <summary>Ends the cloud immediately (checkpoint reload).</summary>
        public void Kill()
        {
            if (_systems != null)
                foreach (var ps in _systems)
                    if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _blocker.enabled = false;
            _release?.Invoke(this);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_emitting)
            {
                _blocker.radius = Mathf.Lerp(0.5f, FullRadius, Mathf.Clamp01(_age / GrowSeconds));
                if (_age >= _duration)
                {
                    _emitting = false;
                    if (_systems != null)
                        foreach (var ps in _systems)
                            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                return;
            }
            var thin = (_age - _duration) / ThinSeconds;
            _blocker.radius = Mathf.Lerp(FullRadius, 0.5f, Mathf.Clamp01(thin));
            if (thin >= 1f) _blocker.enabled = false;
            if (_age >= _duration + ThinSeconds + 8f) _release?.Invoke(this);
        }
    }
}
