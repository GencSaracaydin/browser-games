using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Body
{
    /// <summary>
    /// Opens the jaw bone with the speaker's VO amplitude envelope (TECH_PLAN D10: precomputed 30 Hz RMS through
    /// <see cref="IVOService.Amplitude"/>), e.g. Hollis's D082 in the climax and the booth reel's silent lips (MID-B,
    /// keyed manually with <c>SendMessage("SetJawOpen", 0..1)</c>). Applied after the Animator; does nothing without a
    /// mapped jaw bone.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Audio + 10)]
    [DisallowMultipleComponent]
    public sealed class JawFlap : MonoBehaviour
    {
        [SerializeField] private BodyRig _rig;
        [SerializeField, Tooltip("DIALOGUE.json speaker id (SpeakerIds), e.g. HOLLIS.")]
        private string _speakerId;
        [SerializeField, Tooltip("Jaw opening at full amplitude (degrees).")]
        private float _maxOpenDegrees = 13f;
        [SerializeField, Tooltip("Envelope gain: RMS is ~0.1-0.3 for normal speech.")]
        private float _gain = 4f;
        [SerializeField] private float _smoothing = 22f;
        [SerializeField, Tooltip("Jaw-local hinge axis (computed by the builder from the head's right axis).")]
        private Vector3 _hingeLocal = Vector3.right;

        private ServiceRef<IVOService> _vo;
        private float _open;
        private float _manual = -1f;
        private bool _unscaled;

        /// <summary>Speaker id this jaw follows.</summary>
        public string SpeakerId { get => _speakerId; set => _speakerId = value; }

        private void Awake()
        {
            if (_rig == null) _rig = GetComponent<BodyRig>();
            var a = GetComponent<Animator>();
            _unscaled = a != null && a.updateMode == AnimatorUpdateMode.UnscaledTime;
        }

        /// <summary>Manual jaw opening 0..1 (negative returns to the VO envelope). SendMessage target.</summary>
        public void SetJawOpen(float open01) => _manual = open01;

        private void LateUpdate()
        {
            var jaw = _rig != null ? _rig.Jaw : null;
            if (jaw == null) return;
            float target;
            if (_manual >= 0f) target = Mathf.Clamp01(_manual);
            else
            {
                var vo = _vo.Value;
                target = vo != null && !string.IsNullOrEmpty(_speakerId) ? Mathf.Clamp01(vo.Amplitude(_speakerId) * _gain) : 0f;
            }
            var dt = _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            _open = MathX.Damp(_open, target, _smoothing, dt);
            // If nothing rewrote the jaw since our last write (Animator off, jaw not animated), start from our old base
            // so the opening never accumulates.
            var current = jaw.localRotation;
            var baseRot = _hasWritten && current == _lastWritten ? _lastBase : current;
            var result = _open > 0.002f ? baseRot * Quaternion.AngleAxis(_open * _maxOpenDegrees, _hingeLocal) : baseRot;
            jaw.localRotation = result;
            _lastBase = baseRot;
            _lastWritten = result;
            _hasWritten = true;
        }

        private Quaternion _lastBase;
        private Quaternion _lastWritten;
        private bool _hasWritten;

#if UNITY_EDITOR
        /// <summary>Builders: rig, speaker and hinge axis (jaw-local axis of the head's right direction in the bind pose).</summary>
        public void EditorSetup(BodyRig rig, string speakerId, Vector3 hingeLocal)
        {
            _rig = rig;
            _speakerId = speakerId;
            _hingeLocal = hingeLocal.sqrMagnitude > 1e-6f ? hingeLocal.normalized : Vector3.right;
        }
#endif
    }
}
