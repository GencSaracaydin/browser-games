using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Look.FX
{
    /// <summary>
    /// Rotor downwash dust (VFX_ROTOR_DOWNWASH; CINEMATIC_DIRECTION §2 I-1, I-7, I-8 and the Beat 10/11 LZ): a ground
    /// ring burst, outward streaks and a hanging, sun-lit dust sheet under the helicopter. Ten times a second it casts
    /// down from the rotor hub, moves the emitters to the ground hit, tints them by the surface (<see cref="SurfaceTag"/>)
    /// and scales emission with rotor power and height (strong below 6 m, gone above <see cref="_maxHeight"/>). Put it
    /// on the helicopter prefab (WP10 Level / WP09 insertion copy); <see cref="RotorPower"/> lets timelines spool it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RotorDownwash : MonoBehaviour
    {
        [SerializeField, Tooltip("Main rotor hub (defaults to this transform).")]
        private Transform _hub;
        [SerializeField, Tooltip("Ground-ring burst (circle/donut shape).")]
        private ParticleSystem _ring;
        [SerializeField, Tooltip("Outward low streaks.")]
        private ParticleSystem _streaks;
        [SerializeField, Tooltip("Slow hanging dust sheet.")]
        private ParticleSystem _sheet;
        [SerializeField, Tooltip("Rotor radius in metres (Bell 212 class: 7.3).")]
        private float _rotorRadius = 7.3f;
        [SerializeField, Tooltip("No dust above this hub height (metres).")]
        private float _maxHeight = 28f;
        [SerializeField] private float _ringRate = 90f;
        [SerializeField] private float _streakRate = 40f;
        [SerializeField] private float _sheetRate = 14f;
        [SerializeField] private LayerMask _ground = LayerIds.Ground;
        [SerializeField, Range(0f, 1f), Tooltip("Rotor power 0..1 (timelines spool it up/down).")]
        private float _rotorPower = 1f;

        private float _next;
        private float _strength;

        /// <summary>Rotor power 0..1.</summary>
        public float RotorPower
        {
            get => _rotorPower;
            set => _rotorPower = Mathf.Clamp01(value);
        }

        /// <summary>Dust strength 0..1 at the last update.</summary>
        public float Strength => _strength;

        /// <summary>Dust strength for a hub height and rotor power (pure; tests).</summary>
        public static float StrengthFor(float height, float maxHeight, float power)
        {
            if (height < 0f || maxHeight <= 0f) return 0f;
            var h = Mathf.Clamp01(1f - height / maxHeight);
            return Mathf.Clamp01(power) * h * Mathf.Sqrt(h);
        }

        /// <summary>Dust colour of a surface (sRGB).</summary>
        public static Color DustColor(SurfaceType s)
        {
            switch (s)
            {
                case SurfaceType.Concrete:
                case SurfaceType.Stone:
                case SurfaceType.Tile: return new Color(0.66f, 0.62f, 0.55f, 1f);
                case SurfaceType.Gravel: return new Color(0.70f, 0.63f, 0.52f, 1f);
                case SurfaceType.SaltCrust: return new Color(0.86f, 0.84f, 0.80f, 1f);
                case SurfaceType.Plaster: return new Color(0.85f, 0.81f, 0.73f, 1f);
                default: return new Color(0.79f, 0.66f, 0.48f, 1f); // sand #C9A87A
            }
        }

        private void OnEnable() => _next = 0f;

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.1f;
            var hub = _hub != null ? _hub : transform;
            var origin = hub.position;
            if (Physics.Raycast(origin, Vector3.down, out var hit, _maxHeight + 2f, _ground, QueryTriggerInteraction.Ignore))
            {
                _strength = StrengthFor(hit.distance, _maxHeight, _rotorPower);
                var surface = SurfaceTag.Resolve(hit.collider);
                var color = DustColor(surface);
                var ground = hit.point + hit.normal * 0.1f;
                Drive(_ring, ground, hit.normal, _ringRate, color, _rotorRadius * (1f + 0.02f * hit.distance));
                Drive(_streaks, ground, hit.normal, _streakRate, color, _rotorRadius * 1.1f);
                Drive(_sheet, ground + Vector3.up * 1.2f, hit.normal, _sheetRate, color, _rotorRadius * 1.6f);
            }
            else
            {
                _strength = 0f;
                Drive(_ring, origin, Vector3.up, 0f, Color.white, _rotorRadius);
                Drive(_streaks, origin, Vector3.up, 0f, Color.white, _rotorRadius);
                Drive(_sheet, origin, Vector3.up, 0f, Color.white, _rotorRadius);
            }
        }

        private void Drive(ParticleSystem ps, Vector3 position, Vector3 normal, float rate, Color color, float radius)
        {
            if (ps == null) return;
            var t = ps.transform;
            t.position = position;
            t.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            var em = ps.emission;
            em.rateOverTimeMultiplier = rate * _strength;
            var main = ps.main;
            main.startColor = new Color(color.r, color.g, color.b, Mathf.Lerp(0.25f, 0.8f, _strength));
            var shape = ps.shape;
            if (shape.enabled) shape.radius = radius;
            if (_strength > 0.01f && !ps.isPlaying) ps.Play(false);
        }

#if UNITY_EDITOR
        /// <summary>Editor builders.</summary>
        public void EditorSetup(Transform hub, ParticleSystem ring, ParticleSystem streaks, ParticleSystem sheet)
        {
            _hub = hub;
            _ring = ring;
            _streaks = streaks;
            _sheet = sheet;
        }
#endif
    }
}
