using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// The grey voice-scrambler box (GDD §5.1, G1): its VU needle follows the interrogator's voice (WP06 envelope
    /// amplitude of INTERROGATOR/TALLOW, TECH_PLAN D10) with VU-meter ballistics, and its toggle flips off in outro shot 7,
    /// dropping the needle to zero before D090 plays clean. FAL_SCRAMBLER_BOX conventions (WP09_ART): Needle child at rest
    /// full left, localEulerAngles.z = 94 × level; Toggle child ON as exported, OFF = +50° about local X.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScramblerBox : MonoBehaviour
    {
        /// <summary>Needle sweep at full level in degrees (WP09_ART).</summary>
        public const float NeedleSweep = 94f;
        /// <summary>Toggle throw in degrees (WP09_ART).</summary>
        public const float ToggleThrow = 50f;

        [SerializeField] private Transform _needle;
        [SerializeField] private Transform _toggle;
        [SerializeField, Tooltip("Flip if the needle swings the wrong way in the editor (WP09_ART: verify sign once).")]
        private float _needleSign = 1f;
        [SerializeField] private float _attack = 14f;
        [SerializeField] private float _release = 5f;
        [SerializeField] private float _gain = 1.35f;

        private Quaternion _needleRest = Quaternion.identity;
        private Quaternion _toggleOn = Quaternion.identity;
        private float _level;
        private float _toggleBlend = 1f;
        private bool _powered = true;

        /// <summary>True while the scrambler is on (needle live).</summary>
        public bool Powered => _powered;
        /// <summary>Displayed needle level 0..1.</summary>
        public float Level => _level;

        private void Awake()
        {
            if (_needle != null) _needleRest = _needle.localRotation;
            if (_toggle != null) _toggleOn = _toggle.localRotation;
        }

        /// <summary>Builder wiring.</summary>
        public void Setup(Transform needle, Transform toggle)
        {
            _needle = needle;
            _toggle = toggle;
        }

        /// <summary>Flips the toggle (outro shot 7: off, with a click).</summary>
        public void SetPowered(bool on, bool click = true)
        {
            if (_powered == on) return;
            _powered = on;
            if (click) CinematicAudio.Play(CinematicCues.ToggleClick, transform.position);
        }

        private void LateUpdate()
        {
            var dt = Time.unscaledDeltaTime;
            var target = 0f;
            if (_powered)
            {
                var a = Mathf.Max(CinematicAudio.Amplitude(SpeakerIds.Interrogator), CinematicAudio.Amplitude(SpeakerIds.Tallow));
                target = Mathf.Clamp01(a * _gain);
            }
            var rate = target > _level ? _attack : (_powered ? _release : 1f / 0.15f * 3f);
            _level = Mathf.Lerp(_level, target, 1f - Mathf.Exp(-rate * dt));
            _toggleBlend = Mathf.MoveTowards(_toggleBlend, _powered ? 1f : 0f, dt / 0.06f);
            if (_needle != null) _needle.localRotation = _needleRest * Quaternion.Euler(0f, 0f, NeedleSweep * _level * _needleSign);
            if (_toggle != null) _toggle.localRotation = _toggleOn * Quaternion.Euler(ToggleThrow * (1f - _toggleBlend), 0f, 0f);
        }
    }
}
