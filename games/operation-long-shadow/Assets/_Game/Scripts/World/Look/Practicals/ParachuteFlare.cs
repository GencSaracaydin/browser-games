using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Look.Atmosphere;
using LongShadow.World.Look.Common;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// A Beat 10 parachute illumination flare FL1–FL3 (GDD §7.10, §9.5; TECH_PLAN §7.14; PROC_PARACHUTE_FLARE): a shadowed
    /// 120° spot pointing down (the only shadow caster of BLUE HOUR) plus an unshadowed point fill, descending at
    /// 1.3 m/s under a ±4° pendulum (3 s period) that swings the light and its shadows, drifting with the wind, burning
    /// 30 s with magnesium sputter and a 3 s burn-out. While burning it keeps an <see cref="IlluminationZone"/> (AI sight
    /// ×2 up to 70 m). Only the newest flare casts shadows (FL1–FL3 never overlap, GDD §9.5). Spawned by
    /// <see cref="FlareDirector"/>; counted as fixed lights by <see cref="PracticalBudget"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParachuteFlare : MonoBehaviour
    {
        private static readonly List<ParachuteFlare> s_live = new List<ParachuteFlare>(4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_live.Clear();

        /// <summary>Burning or falling flares.</summary>
        public static IReadOnlyList<ParachuteFlare> Live => s_live;

        /// <summary>Descent speed m/s (GDD §7.10).</summary>
        public const float DescentSpeed = 1.3f;
        /// <summary>Burn time in seconds.</summary>
        public const float BurnSeconds = 30f;
        /// <summary>Pendulum amplitude in degrees.</summary>
        public const float PendulumDegrees = 4f;
        /// <summary>Pendulum period in seconds.</summary>
        public const float PendulumPeriod = 3f;

        [SerializeField, Tooltip("Swinging part: canister, lights and smoke hang from it; pivot = chute attach point.")]
        private Transform _swing;
        [SerializeField, Tooltip("Shadowed spot, 120°, pointing down.")]
        private Light _spot;
        [SerializeField, Tooltip("Unshadowed point fill.")]
        private Light _fill;
        [SerializeField] private Renderer[] _emissive = new Renderer[0];
        [SerializeField] private ParticleSystem _smoke;
        [SerializeField] private IlluminationZone _zone;
        [SerializeField] private float _spotIntensity = 2400f;
        [SerializeField] private float _fillIntensity = 450f;
        [SerializeField, ColorUsage(false, true)] private Color _emission = new Color(60f, 52f, 40f, 1f);
        [SerializeField, Tooltip("Fraction of the wind velocity the chute drifts with.")]
        private float _windDrift = 0.3f;
        [SerializeField] private string _cue = LookIds.CueFlare;

        private MaterialPropertyBlock _mpb;
        private ServiceRef<IAudioService> _audio;
        private float _igniteTime = float.NegativeInfinity;
        private float _seed;
        private bool _burning;
        private bool _hasShadow;

        /// <summary>Owner id given at ignition ("B10.FL1").</summary>
        public string OwnerId { get; private set; }
        /// <summary>True while burning.</summary>
        public bool Burning => _burning;
        /// <summary>Brightness 0..1.</summary>
        public float Level { get; private set; }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (_swing == null) _swing = transform;
            SetLights(0f);
        }

        private void OnEnable()
        {
            PracticalBudget.RegisterFixed(_spot);
            PracticalBudget.RegisterFixed(_fill);
        }

        private void OnDisable()
        {
            PracticalBudget.UnregisterFixed(_spot);
            PracticalBudget.UnregisterFixed(_fill);
            s_live.Remove(this);
            if (_zone != null) _zone.SetActive(false);
        }

        /// <summary>Ignites at the current position (FL marker at +70 m).</summary>
        public void Ignite(string ownerId)
        {
            OwnerId = ownerId;
            _igniteTime = Time.time;
            _seed = Random.Range(1f, 90f);
            _burning = true;
            if (!s_live.Contains(this)) s_live.Add(this);
            // Newest flare takes the shadow (GDD §9.5: one shadowed local light).
            for (var i = 0; i < s_live.Count; i++) s_live[i].SetShadowOwner(s_live[i] == this);
            if (_zone != null)
            {
                _zone.Configure(IlluminationShape.Cone, transform.position.y + 30f, 120f, ownerId);
                _zone.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                _zone.SetActive(true);
            }
            if (_smoke != null) _smoke.Play(true);
            if (!string.IsNullOrEmpty(_cue)) _audio.Value?.PlayAttached(_cue, transform, 1f);
        }

        /// <summary>Puts it out now and removes it (checkpoint reload).</summary>
        public void Extinguish()
        {
            _burning = false;
            SetLights(0f);
            if (_zone != null) _zone.SetActive(false);
            s_live.Remove(this);
            Pools.Despawn(gameObject);
        }

        /// <summary>Burn envelope 0..1 at <paramref name="t"/> seconds after ignition (without sputter).</summary>
        public static float Envelope(float t)
        {
            if (t < 0f || t > BurnSeconds) return 0f;
            if (t < 0.6f) return t / 0.6f;
            if (t > BurnSeconds - 3f) return Mathf.Clamp01((BurnSeconds - t) / 3f);
            return 1f;
        }

        /// <summary>Pendulum angle in degrees at <paramref name="t"/>.</summary>
        public static float PendulumAngle(float t) => PendulumDegrees * Mathf.Sin(2f * Mathf.PI * t / PendulumPeriod);

        private void Update()
        {
            if (float.IsNegativeInfinity(_igniteTime)) return;
            var t = Time.time - _igniteTime;
            var dt = Time.deltaTime;

            var drift = WindController.Velocity * _windDrift;
            transform.position += new Vector3(drift.x * dt, -DescentSpeed * dt, drift.z * dt);
            if (_swing != null)
            {
                var axis = Vector3.Cross(Vector3.up, drift.sqrMagnitude > 1e-4f ? drift.normalized : Vector3.right);
                _swing.localRotation = Quaternion.AngleAxis(PendulumAngle(t), axis.sqrMagnitude > 1e-6f ? axis : Vector3.forward);
            }

            Level = Envelope(t) * (t < BurnSeconds - 3f ? FlickerNoise.Flare(t, _seed) : FlickerNoise.Flare(t, _seed) * FlickerNoise.StrikeSputter(t - (BurnSeconds - 3f) - 0.2f, _seed));
            SetLights(Level);
            if (_zone != null)
            {
                _zone.Configure(IlluminationShape.Cone, Mathf.Max(10f, transform.position.y + 30f), 120f, OwnerId);
                var active = Level > 0.25f;
                if (_zone.IsActive != active) _zone.SetActive(active);
            }

            if (_burning && t > BurnSeconds)
            {
                _burning = false;
                if (_smoke != null) _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!_burning && t > BurnSeconds + 6f)
            {
                s_live.Remove(this);
                _igniteTime = float.NegativeInfinity;
                Pools.Despawn(gameObject);
            }
        }

        private void SetShadowOwner(bool owner)
        {
            _hasShadow = owner;
            if (_spot != null) _spot.shadows = owner ? LightShadows.Hard : LightShadows.None;
        }

        private void SetLights(float level)
        {
            if (_spot != null)
            {
                var on = level > 1e-3f;
                if (_spot.enabled != on) _spot.enabled = on;
                _spot.intensity = _spotIntensity * level;
                if (on && !_hasShadow && _spot.shadows != LightShadows.None) _spot.shadows = LightShadows.None;
            }
            if (_fill != null)
            {
                var on = level > 1e-3f;
                if (_fill.enabled != on) _fill.enabled = on;
                _fill.intensity = _fillIntensity * level;
            }
            if (_emissive == null) return;
            var e = _emission * level;
            for (var i = 0; i < _emissive.Length; i++)
            {
                var r = _emissive[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(LookShaderIds.EmissionColor, e);
                r.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>Wires the parts (FX builder, and the greybox fallback of <see cref="FlareDirector"/>).</summary>
        public void Configure(Transform swing, Light spot, Light fill, Renderer[] emissive, ParticleSystem smoke, IlluminationZone zone)
        {
            _swing = swing != null ? swing : transform;
            _spot = spot;
            _fill = fill;
            _emissive = emissive ?? new Renderer[0];
            _smoke = smoke;
            _zone = zone;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            SetLights(0f);
        }
    }
}
