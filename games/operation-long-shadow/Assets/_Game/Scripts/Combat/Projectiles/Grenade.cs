using System;
using LongShadow.Combat.Data;
using LongShadow.Combat.Service;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Projectiles
{
    /// <summary>
    /// A thrown M67 frag (TECH_PLAN §7.5 Grenade): physical bounce and roll on layer Projectile, fuse countdown,
    /// <see cref="GameEvents.GrenadeLanded"/> once it settles (HUD indicator within 8 m, AI flee), then a frag explosion
    /// with cover occlusion through <see cref="CombatResolver"/>. Pooled by <see cref="GrenadePool"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class Grenade : MonoBehaviour
    {
        private const float SettleSpeed = 0.8f;
        private const float SettleTime = 0.18f;

        private Rigidbody _rb;
        private float _fuse;
        private Team _team;
        private GameObject _instigator;
        private bool _live;
        private bool _landed;
        private bool _touched;
        private float _slow;
        private float _lastBounce;
        private Action<Grenade> _release;
        private ServiceRef<IAudioService> _audio;

        /// <summary>True while the fuse is burning.</summary>
        public bool IsLive => _live;
        /// <summary>Team of the thrower.</summary>
        public Team Team => _team;
        /// <summary>Seconds left on the fuse.</summary>
        public float FuseLeft => _fuse;

        /// <summary>Sets the pool callback (called once by the pool).</summary>
        public void Bind(Action<Grenade> release)
        {
            _release = release;
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>Throws the grenade.</summary>
        public void Launch(Vector3 position, Vector3 velocity, float fuse, Team team, GameObject instigator)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            gameObject.SetActive(false);
            transform.SetPositionAndRotation(position, UnityEngine.Random.rotation);
            gameObject.SetActive(true);
            _rb.position = position;
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 12f;
            _fuse = Mathf.Max(0.05f, fuse);
            _team = team;
            _instigator = instigator;
            _live = true;
            _landed = false;
            _touched = false;
            _slow = 0f;
            _lastBounce = -1f;
            GameEvents.RaiseGrenadeThrown(position, team);
        }

        /// <summary>Removes the grenade without exploding (checkpoint reload).</summary>
        public void Defuse()
        {
            _live = false;
            _release?.Invoke(this);
        }

        private void Update()
        {
            if (!_live) return;
            var dt = Time.deltaTime;
            _fuse -= dt;
            if (!_landed && _touched)
            {
                _slow = _rb.linearVelocity.sqrMagnitude < SettleSpeed * SettleSpeed ? _slow + dt : 0f;
                if (_slow >= SettleTime)
                {
                    _landed = true;
                    GameEvents.RaiseGrenadeLanded(transform.position, _team);
                }
            }
            if (_fuse <= 0f) Detonate();
        }

        private void Detonate()
        {
            _live = false;
            var pos = transform.position;
            if (!_landed) GameEvents.RaiseGrenadeLanded(pos, _team); // detonated before settling (cooked): still a live grenade
            var spec = WeaponTable.Frag;
            CombatResolver.Explode(pos, spec.Damage, spec.LethalRadius, spec.MaxRadius, _instigator, _team, WeaponIds.Frag,
                FxIds.ExplosionFrag, CombatCues.ExplosionFrag, spec.NoiseRadius, null);
            GameEvents.RaiseGrenadeExploded(pos, spec.MaxRadius);
            _release?.Invoke(this);
        }

        private void OnCollisionEnter(Collision c)
        {
            if (!_live) return;
            _touched = true;
            var speed = c.relativeVelocity.magnitude;
            if (speed < 1.2f || Time.time - _lastBounce < 0.12f) return;
            _lastBounce = Time.time;
            var audio = _audio.Value;
            if (audio == null) return;
            var vol = Mathf.Clamp01(speed / 8f) * 0.7f;
            if (audio.HasCue(CombatCues.GrenadeBounce)) audio.Play(CombatCues.GrenadeBounce, transform.position, vol);
            else audio.Play(AudioCues.Impact(SurfaceTag.Resolve(c.collider)), transform.position, vol * 0.5f);
        }
    }
}
