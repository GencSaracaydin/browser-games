using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Core
{
    /// <summary>
    /// Interior exposure volume data (GDD G10): entering blends the interior profile in over <see cref="FadeIn"/>
    /// seconds and out over <see cref="FadeOut"/>. The tween itself is WP10 Look's EyeAdaptation driver.
    /// </summary>
    public class EyeAdaptationZone : TriggerVolume
    {
        [SerializeField, Tooltip("VP_Interior_* profile.")]
        private VolumeProfile _profile;
        [SerializeField, Tooltip("Exposure offset in EV (cinema +1.2, hall +0.8, screening +1.0, basement +1.0).")]
        private float _ev = 1f;
        [SerializeField] private float _fadeIn = 1.5f;
        [SerializeField] private float _fadeOut = 1.0f;

        /// <summary>Interior volume profile.</summary>
        public VolumeProfile Profile => _profile;
        /// <summary>Exposure offset in EV.</summary>
        public float Ev => _ev;
        /// <summary>Blend-in seconds.</summary>
        public float FadeIn => _fadeIn;
        /// <summary>Blend-out seconds.</summary>
        public float FadeOut => _fadeOut;

        /// <summary>Configures the zone (builders).</summary>
        public void Configure(VolumeProfile profile, float ev, float fadeIn = 1.5f, float fadeOut = 1f)
        {
            _profile = profile;
            _ev = ev;
            _fadeIn = fadeIn;
            _fadeOut = fadeOut;
        }

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(1f, 1f, 0.6f, 0.4f);
#endif
    }
}
