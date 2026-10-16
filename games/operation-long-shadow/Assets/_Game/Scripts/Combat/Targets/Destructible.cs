using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using LongShadow.Combat.Service;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Targets
{
    /// <summary>
    /// A shootable scripted target (TECH_PLAN §7.5 Destructible; <see cref="IDestructible"/>, <see cref="IDamageable"/>,
    /// <see cref="ISaveable"/>): hit points or a hit count (trucks take 20 hits, GDD §7.11), an optional hit radius around
    /// its core (flood lamp heads 0.4 m: hits on the tower do nothing), invulnerability until a script releases it (racks
    /// until D065, GDD §7.13), intact/destroyed object swaps, practical lights that die with it (G4), a spark burst, the
    /// buzzing die-off flicker of the floods, a fireball and lingering fire for vehicles, and the DestructibleDamaged /
    /// DestructibleDestroyed events. Registered by id in <see cref="Destructibles"/> (the id matches its SceneMarker:
    /// "B6.F1", "B9.R2") and with the checkpoint service.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Destructible : MonoBehaviour, IDestructible, IDamageable, ISaveable
    {
        [SerializeField, Tooltip("Id (matches its SceneMarker / Appendix A id, e.g. B6.F1, B9.R2).")]
        private string _id;
        [SerializeField] private DestructibleKind _kind = DestructibleKind.Generic;
        [SerializeField, Tooltip("Hit points (racks 60, GDD §7.9). Ignored when Hits To Destroy > 0.")]
        private float _maxHp = 60f;
        [SerializeField, Tooltip("Destroyed after this many hits (trucks 20, GDD §7.11); 0 = use hit points.")]
        private int _hitsToDestroy;
        [SerializeField, Tooltip("Hits farther than this from the core are ignored (flood lamp head 0.4 m, TECH_PLAN §7.5); 0 = any hit.")]
        private float _hitRadius;
        [SerializeField, Tooltip("Aim point and hit-radius centre (defaults to this transform).")]
        private Transform _core;
        [SerializeField] private bool _invulnerable;
        [SerializeField, Tooltip("Shown while intact.")]
        private GameObject[] _intact = new GameObject[0];
        [SerializeField, Tooltip("Shown once destroyed.")]
        private GameObject[] _destroyed = new GameObject[0];
        [SerializeField, Tooltip("Practical lights that die with it.")]
        private Light[] _lights = new Light[0];
        [SerializeField, Tooltip("Renderers whose emission is switched off on death (lamp lens).")]
        private Renderer[] _emissive = new Renderer[0];
        [SerializeField, Tooltip("Flicker before the lights die (floods: buzzing die-off).")]
        private float _dieOffSeconds = 0.6f;
        [SerializeField, Tooltip("Vehicle fireball damage at the centre (0 = visual only).")]
        private float _explosionDamage = 120f;
        [SerializeField, Tooltip("Vehicle fireball damage radius.")]
        private float _explosionRadius = 6f;
        [SerializeField, Tooltip("Hits an explosion counts as in hit-count mode, per this much damage.")]
        private float _explosionDamagePerHit = 30f;

        private float _hp;
        private int _hits;
        private bool _destroyedState;
        private float _dieOff = -1f;
        private float[] _lightIntensity;
        private GameObject _fire;
        private int _fireLoop = -1;
        private bool _registered;
        private ServiceRef<IAudioService> _audio;
        private ServiceRef<ICheckpointService> _checkpoints;
        private static MaterialPropertyBlock s_block;

        /// <inheritdoc/>
        public string Id => _id;
        /// <inheritdoc/>
        public bool IsDestroyed => _destroyedState;
        /// <inheritdoc/>
        public float Health01 => _hitsToDestroy > 0 ? 1f - (float)_hits / _hitsToDestroy : _maxHp > 0f ? _hp / _maxHp : 0f;
        /// <inheritdoc/>
        public bool Invulnerable { get => _invulnerable; set => _invulnerable = value; }
        /// <inheritdoc/>
        public Transform Root => transform;
        /// <inheritdoc/>
        public Team Team => Team.None;
        /// <inheritdoc/>
        public bool IsAlive => !_destroyedState;
        /// <inheritdoc/>
        public Transform AimPoint => _core != null ? _core : transform;
        /// <summary>Preset.</summary>
        public DestructibleKind Kind => _kind;

        /// <summary>Configures from code (builders, tests).</summary>
        public void Configure(string id, DestructibleKind kind, float maxHp, int hitsToDestroy, float hitRadius, Transform core)
        {
            _id = id;
            _kind = kind;
            _maxHp = maxHp;
            _hitsToDestroy = hitsToDestroy;
            _hitRadius = hitRadius;
            _core = core;
            _hp = maxHp;
            _hits = 0;
        }

        private void Awake()
        {
            _hp = _maxHp;
            if (_lights != null)
            {
                _lightIntensity = new float[_lights.Length];
                for (var i = 0; i < _lights.Length; i++) _lightIntensity[i] = _lights[i] != null ? _lights[i].intensity : 0f;
            }
        }

        private void OnEnable()
        {
            Destructibles.Register(this);
            var cp = _checkpoints.Value;
            if (cp != null && !string.IsNullOrEmpty(_id))
            {
                cp.Register(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            Destructibles.Unregister(this);
            if (_registered) _checkpoints.Value?.Unregister(this);
            _registered = false;
            StopFireLoop();
        }

        /// <inheritdoc/>
        public void ApplyDamage(in DamageInfo d)
        {
            if (_destroyedState || _invulnerable || d.Amount <= 0f) return;
            if (_hitRadius > 0f && d.Type != DamageType.Explosion && (d.Point - AimPoint.position).sqrMagnitude > _hitRadius * _hitRadius) return;
            if (_hitsToDestroy > 0)
            {
                _hits += d.Type == DamageType.Explosion ? Mathf.Max(1, Mathf.CeilToInt(d.Amount / Mathf.Max(1f, _explosionDamagePerHit))) : 1;
                if (_hits > _hitsToDestroy) _hits = _hitsToDestroy;
            }
            else
            {
                _hp = Mathf.Max(0f, _hp - d.Amount);
            }
            if (_kind != DestructibleKind.Vehicle) Sparks(d.Point, 0.35f);
            GameEvents.RaiseDestructibleDamaged(_id, Health01);
            if (Health01 <= 0f) DestroyNow(true);
        }

        /// <inheritdoc/>
        public void ForceDestroy() => DestroyNow(true);

        /// <inheritdoc/>
        public void ResetIntact()
        {
            _destroyedState = false;
            _hp = _maxHp;
            _hits = 0;
            _dieOff = -1f;
            SetSwap(false);
            SetLights(1f);
            SetEmission(true);
            StopFireLoop();
            if (_fire != null) Destroy(_fire);
            _fire = null;
        }

        private void DestroyNow(bool withEffects)
        {
            if (_destroyedState) return;
            _destroyedState = true;
            _hp = 0f;
            SetSwap(true);
            SetEmission(false);
            var audio = _audio.Value;
            var p = AimPoint.position;
            if (withEffects)
            {
                switch (_kind)
                {
                    case DestructibleKind.Flood:
                        Sparks(p, 1f);
                        audio?.Play(CombatCues.FloodDieOff, p);
                        _dieOff = 0f; // lights flicker out in Update
                        break;
                    case DestructibleKind.Rack:
                        Sparks(p, 1.2f);
                        audio?.Play(CombatCues.Sparks, p);
                        SetLights(0f);
                        break;
                    case DestructibleKind.Vehicle:
                        CombatResolver.Explode(p, _explosionDamage, _explosionRadius * 0.4f, _explosionRadius, null, Team.None, null,
                            FxIds.ExplosionFireball, CombatCues.ExplosionFireball, WeaponTable.Frag.NoiseRadius, this);
                        StartFire();
                        SetLights(0f);
                        break;
                    default:
                        Sparks(p, 1f);
                        audio?.Play(CombatCues.Sparks, p);
                        SetLights(0f);
                        break;
                }
            }
            else
            {
                SetLights(0f);
                return; // restored from a checkpoint: the mission already counted it
            }
            GameEvents.RaiseDestructibleDestroyed(_id);
        }

        private void Update()
        {
            if (_dieOff < 0f) return;
            _dieOff += Time.deltaTime;
            var k = _dieOffSeconds > 0f ? _dieOff / _dieOffSeconds : 1f;
            if (k >= 1f)
            {
                SetLights(0f);
                _dieOff = -1f;
                return;
            }
            // A dying ballast: random flicker that weakens.
            var on = Random.value > k * 0.8f;
            SetLights(on ? (1f - k) * Random.Range(0.4f, 1f) : 0f);
        }

        private void Sparks(Vector3 at, float scale)
        {
            var fx = CombatFx.Instance;
            if (fx == null) return;
            fx.Impacts.Play(SurfaceType.Metal, at, (fx.ListenerPosition - at).normalized, null, false, null, at);
            if (scale >= 1f) fx.Lights.Flash(at, new Color(1f, 0.85f, 0.6f), 4f * scale, 6f, 0.12f);
        }

        private void StartFire()
        {
            var fx = CombatFx.Instance;
            _fire = CombatFx.InstantiateOrBuild(fx != null ? fx.Library : null, FxIds.Fire, transform,
                () => FxFactory.BuildFire(fx != null && fx.Materials != null ? fx.Materials : FxMaterialSet.Procedural()));
            _fire.transform.position = AimPoint.position;
            foreach (var ps in _fire.GetComponentsInChildren<ParticleSystem>(true)) ps.Play(true);
            _fireLoop = _audio.Value?.PlayLoop(CombatCues.FireLoop, _fire.transform) ?? -1;
        }

        private void StopFireLoop()
        {
            if (_fireLoop >= 0) _audio.Value?.StopLoop(_fireLoop, 0.5f);
            _fireLoop = -1;
        }

        private void SetSwap(bool destroyed)
        {
            if (_intact != null)
                foreach (var g in _intact)
                    if (g != null) g.SetActive(!destroyed);
            if (_destroyed != null)
                foreach (var g in _destroyed)
                    if (g != null) g.SetActive(destroyed);
        }

        private void SetLights(float k)
        {
            if (_lights == null) return;
            for (var i = 0; i < _lights.Length; i++)
            {
                var l = _lights[i];
                if (l == null) continue;
                var base0 = _lightIntensity != null && i < _lightIntensity.Length ? _lightIntensity[i] : l.intensity;
                l.intensity = base0 * k;
                l.enabled = k > 0.001f;
            }
        }

        private void SetEmission(bool on)
        {
            if (_emissive == null) return;
            if (s_block == null) s_block = new MaterialPropertyBlock();
            foreach (var r in _emissive)
            {
                if (r == null) continue;
                r.GetPropertyBlock(s_block);
                s_block.SetColor("_EmissionColor", on ? Color.white : Color.black);
                if (on) r.SetPropertyBlock(null);
                else r.SetPropertyBlock(s_block);
            }
        }

        // ---- ISaveable -----------------------------------------------------------------------------------------------

        /// <inheritdoc/>
        public string SaveKey => "Destructible." + _id;

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            w.Write("destroyed", _destroyedState);
            w.Write("hp", _hp);
            w.Write("hits", _hits);
            w.Write("invulnerable", _invulnerable);
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            var destroyed = r.ReadBool("destroyed");
            _invulnerable = r.ReadBool("invulnerable", _invulnerable);
            if (destroyed)
            {
                if (!_destroyedState) DestroyNow(false);
            }
            else
            {
                ResetIntact();
                _hp = r.ReadFloat("hp", _maxHp);
                _hits = r.ReadInt("hits");
            }
        }
    }
}
