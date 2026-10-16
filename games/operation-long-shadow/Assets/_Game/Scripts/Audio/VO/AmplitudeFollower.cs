using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// Drives a needle, jaw bone or blend shape from <see cref="IVOService.Amplitude"/> (TECH_PLAN D10 envelopes): the
    /// scrambler box's VU needle in the booth (G1, interrogator/TALLOW), a jaw flap (D082, MID-B lips). Uses unscaled
    /// time so it keeps moving during glitch inserts (time scale 0). Ballistics: a VU meter integrates over ~300 ms
    /// (attack = release = 0.3 s); a jaw follows within ~50 ms. Add it in a builder or with
    /// <b>LongShadow/Audio/Add Amplitude Follower</b>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmplitudeFollower : MonoBehaviour
    {
        [SerializeField, Tooltip("Speaker ids (DIALOGUE.json); the loudest one drives the target.")]
        private string[] _speakers = { SpeakerIds.Interrogator, SpeakerIds.Tallow };
        [SerializeField, Tooltip("Rotated transform (defaults to this one).")] private Transform _target;
        [SerializeField] private Vector3 _axis = Vector3.forward;
        [SerializeField, Tooltip("Angle at silence (degrees).")] private float _restAngle = -42f;
        [SerializeField, Tooltip("Angle at full level (degrees).")] private float _fullAngle = 42f;
        [SerializeField] private float _attackSeconds = 0.3f;
        [SerializeField] private float _releaseSeconds = 0.3f;
        [SerializeField, Tooltip("Envelope gain before clamping to 1.")] private float _gain = 1.1f;
        [SerializeField, Tooltip("Small mechanical wobble of the needle at level.")] private float _wobble = 0.02f;
        [SerializeField] private SkinnedMeshRenderer _blendShapeRenderer;
        [SerializeField] private int _blendShapeIndex = -1;
        [SerializeField] private float _blendShapeMax = 100f;

        private ServiceRef<IVOService> _vo;
        private Quaternion _base = Quaternion.identity;
        private float _value;
        private float _phase;

        /// <summary>Current smoothed level 0..1.</summary>
        public float Level => _value;

        /// <summary>Configures the follower (builders, menu).</summary>
        public void Configure(string[] speakers, Transform target, Vector3 axis, float restAngle, float fullAngle, float attack, float release)
        {
            _speakers = speakers ?? new string[0];
            _target = target;
            _axis = axis;
            _restAngle = restAngle;
            _fullAngle = fullAngle;
            _attackSeconds = Mathf.Max(0.001f, attack);
            _releaseSeconds = Mathf.Max(0.001f, release);
            if (isActiveAndEnabled) CaptureBase();
        }

        /// <summary>Uses a blend shape instead of (or with) a rotation.</summary>
        public void ConfigureBlendShape(SkinnedMeshRenderer renderer, int index, float max)
        {
            _blendShapeRenderer = renderer;
            _blendShapeIndex = index;
            _blendShapeMax = max;
        }

        private void OnEnable() => CaptureBase();

        private void CaptureBase()
        {
            if (_target == null) _target = transform;
            _base = _target.localRotation;
        }

        private void LateUpdate()
        {
            var dt = Time.unscaledDeltaTime;
            var vo = _vo.Value;
            var amp = 0f;
            if (vo != null && _speakers != null)
            {
                for (var i = 0; i < _speakers.Length; i++)
                {
                    var a = vo.Amplitude(_speakers[i]);
                    if (a > amp) amp = a;
                }
            }
            amp = Mathf.Clamp01(amp * _gain);
            var tau = amp > _value ? _attackSeconds : _releaseSeconds;
            _value = Mathf.Lerp(_value, amp, 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, tau)));
            _phase += dt * 23f;
            var shown = Mathf.Clamp01(_value + (_value > 0.05f ? Mathf.Sin(_phase) * _wobble : 0f));
            if (_target != null && _axis.sqrMagnitude > 1e-6f)
                _target.localRotation = _base * Quaternion.AngleAxis(Mathf.Lerp(_restAngle, _fullAngle, shown), _axis.normalized);
            if (_blendShapeRenderer != null && _blendShapeIndex >= 0)
                _blendShapeRenderer.SetBlendShapeWeight(_blendShapeIndex, shown * _blendShapeMax);
        }
    }
}
