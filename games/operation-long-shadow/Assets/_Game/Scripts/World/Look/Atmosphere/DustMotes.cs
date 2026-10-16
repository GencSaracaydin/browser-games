using LongShadow.World.Look.Stage;
using UnityEngine;

namespace LongShadow.World.Look.Atmosphere
{
    /// <summary>
    /// A dust-mote volume (GDD §9.7 "dust-mote volumes"; PROC_LIGHT_SHAFTS): a box particle system of slow, soft motes
    /// (<c>LongShadow/World/DustMotes</c> material) that catch the key light. Emission and colour follow the LightStage:
    /// full in GOLDEN sun, thinning through SUNSET, faint after sunset (motes then only show in practical light).
    /// Particles drift with <see cref="WindController.Velocity"/> scaled down (sheltered air).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class DustMotes : MonoBehaviour
    {
        [SerializeField, Tooltip("Particles per second in full sun.")]
        private float _rate = 30f;
        [SerializeField, Tooltip("Fraction of the rate after sunset.")]
        private float _nightFraction = 0.15f;
        [SerializeField, Tooltip("Wind drift fraction (0 = still interior air).")]
        private float _windFactor = 0.08f;
        [SerializeField, Tooltip("Colour gain per unit key light.")]
        private float _gain = 0.6f;

        private ParticleSystem _ps;
        private float _next;

        private void Awake() => _ps = GetComponent<ParticleSystem>();

        private void Update()
        {
            if (Time.time < _next || _ps == null) return;
            _next = Time.time + 0.25f;
            var ls = LightStageController.Active;
            float amount;
            Color c;
            if (ls == null)
            {
                amount = 1f;
                c = new Color(1f, 0.85f, 0.65f);
            }
            else
            {
                var s = ls.CurrentSample;
                amount = s.SunEnabled ? Mathf.Lerp(_nightFraction, 1f, s.SunDiscVisibility) : _nightFraction;
                var key = ls.SunColorLinear;
                if (!s.SunEnabled) key = new Color(0.35f, 0.4f, 0.55f);
                c = key * _gain;
            }
            var em = _ps.emission;
            em.rateOverTimeMultiplier = _rate * amount;
            var main = _ps.main;
            main.startColor = new Color(Mathf.Min(c.r, 4f), Mathf.Min(c.g, 4f), Mathf.Min(c.b, 4f), 1f);
            var vel = _ps.velocityOverLifetime;
            if (vel.enabled)
            {
                var w = WindController.Velocity * _windFactor;
                vel.x = new ParticleSystem.MinMaxCurve(w.x - 0.03f, w.x + 0.03f);
                vel.z = new ParticleSystem.MinMaxCurve(w.z - 0.03f, w.z + 0.03f);
            }
        }
    }
}
