using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// Spins a helicopter rotor about a local axis and swaps the blades for the blur disc above a speed threshold
    /// (WP10_FAL handoff: blades below ~150 rpm, blur discs above). Speed eases between idle and flight rpm.
    /// </summary>
    public sealed class RotorSpin : MonoBehaviour
    {
        [SerializeField] private Vector3 _axis = Vector3.up;
        [SerializeField, Tooltip("Revolutions per minute at full speed (Bell 212 main rotor ≈ 324, tail ≈ 1660).")]
        private float _rpm = 324f;
        [SerializeField, Range(0f, 1f)] private float _throttle = 1f;
        [SerializeField] private float _spoolSeconds = 6f;
        [SerializeField] private GameObject _blades;
        [SerializeField] private GameObject _blur;
        [SerializeField] private float _blurAboveRpm = 150f;

        private float _current;

        /// <summary>Target throttle 0..1 (cinematics spool the rotor up/down).</summary>
        public float Throttle
        {
            get => _throttle;
            set => _throttle = Mathf.Clamp01(value);
        }

        /// <summary>Configures the rotor (editor builders).</summary>
        public void Configure(Vector3 axis, float rpm, GameObject blades, GameObject blur)
        {
            _axis = axis;
            _rpm = rpm;
            _blades = blades;
            _blur = blur;
        }

        private void OnEnable() => _current = _rpm * _throttle;

        private void Update()
        {
            var target = _rpm * _throttle;
            _current = Mathf.MoveTowards(_current, target, _rpm / Mathf.Max(0.1f, _spoolSeconds) * Time.deltaTime);
            transform.Rotate(_axis, _current * 6f * Time.deltaTime, Space.Self);
            var blur = _current > _blurAboveRpm;
            if (_blades != null && _blades.activeSelf == blur) _blades.SetActive(!blur);
            if (_blur != null && _blur.activeSelf != blur) _blur.SetActive(blur);
        }
    }
}
