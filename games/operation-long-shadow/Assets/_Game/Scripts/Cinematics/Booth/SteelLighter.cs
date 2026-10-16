using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// The interrogator's steel flip lighter (PH_VINTAGE_LIGHTER; MID-B shot 6: "a hand with a steel lighter enters the
    /// film frame"). Lid opens about local Z (~100°), the wheel spins about local X, and a small flame and warm point
    /// light burn while lit (lighting the booth rendering layer only).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteelLighter : MonoBehaviour
    {
        [SerializeField] private Transform _lid;
        [SerializeField] private Transform _wheel;
        [SerializeField] private Light _flameLight;
        [SerializeField] private Transform _flame;
        [SerializeField, Tooltip("Flip if the lid opens into the body (WP09_ART: verify sign).")] private float _lidSign = 1f;

        private Quaternion _lidRest = Quaternion.identity;
        private Quaternion _wheelRest = Quaternion.identity;
        private float _open;
        private bool _wantOpen;
        private bool _lit;
        private float _wheelSpin;
        private float _flameBase = 0.6f;

        /// <summary>True while burning.</summary>
        public bool Lit => _lit;

        private void Awake()
        {
            if (_lid != null) _lidRest = _lid.localRotation;
            if (_wheel != null) _wheelRest = _wheel.localRotation;
            if (_flameLight != null) _flameBase = _flameLight.intensity;
            SetFlame(false);
        }

        /// <summary>Builder wiring.</summary>
        public void Setup(Transform lid, Transform wheel, Light flameLight, Transform flame)
        {
            _lid = lid;
            _wheel = wheel;
            _flameLight = flameLight;
            _flame = flame;
        }

        /// <summary>Opens, strikes and lights (with the lighter sound).</summary>
        public void Strike()
        {
            _wantOpen = true;
            _wheelSpin = 1f;
            _lit = true;
            CinematicAudio.Play(CinematicCues.Lighter, transform.position);
        }

        /// <summary>Snaps the lid shut and puts the flame out.</summary>
        public void Close()
        {
            _wantOpen = false;
            _lit = false;
            SetFlame(false);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _open = Mathf.MoveTowards(_open, _wantOpen ? 1f : 0f, dt / (_wantOpen ? 0.12f : 0.08f));
            if (_lid != null) _lid.localRotation = _lidRest * Quaternion.Euler(0f, 0f, 100f * _open * _lidSign);
            if (_wheel != null && _wheelSpin > 0f)
            {
                _wheelSpin = Mathf.Max(0f, _wheelSpin - dt * 3f);
                _wheel.localRotation *= Quaternion.Euler(1440f * _wheelSpin * dt, 0f, 0f);
            }
            if (_lit && _open > 0.9f) SetFlame(true);
            if (_lit && _flameLight != null)
                _flameLight.intensity = _flameBase * (0.8f + 0.25f * Mathf.PerlinNoise(Time.unscaledTime * 12f, 0.3f));
        }

        private void SetFlame(bool on)
        {
            if (_flameLight != null) _flameLight.enabled = on;
            if (_flame != null && _flame.gameObject.activeSelf != on) _flame.gameObject.SetActive(on);
        }
    }
}
