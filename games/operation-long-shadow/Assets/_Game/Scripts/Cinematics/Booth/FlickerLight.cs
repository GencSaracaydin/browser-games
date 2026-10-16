using LongShadow.Cinematics.FX;
using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// The booth's fluorescent tube (GDD §8.0 "baked plus a realtime flicker curve"): a shadowed realtime key light, a
    /// weak bounce fill, the batten glass emission and the buzz loop, all driven by one level 0..1. Stutters on with four
    /// blinks (intro shot 2), dies (outro shot 10), and reports its level to <see cref="BoothLook"/>, which moves the booth
    /// exposure ±0.15 EV with it (TECH_PLAN §3.7 VP_Booth). Unscaled time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlickerLight : MonoBehaviour
    {
        private static readonly int s_emission = Shader.PropertyToID("_EmissionColor");

        // Four blinks then on (seconds on, seconds off), GDD §8.2 shot 2.
        private static readonly float[] s_stutter = { 0.06f, 0.24f, 0.05f, 0.38f, 0.11f, 0.16f, 0.08f, 0.30f };
        private static readonly float[] s_dying = { 0.20f, 0.09f, 0.35f, 0.05f, 0.07f, 0.22f, 0.04f, 0.5f };

        [SerializeField] private Light _key;
        [SerializeField] private Light _fill;
        [SerializeField] private Renderer _glass;
        [SerializeField] private int _glassMaterialIndex = -1;
        [SerializeField] private Color _emission = new Color(3.2f, 3.4f, 3.0f);
        [SerializeField] private FlickerMode _startMode = FlickerMode.On;

        private float _keyIntensity = 1f;
        private float _fillIntensity = 0.2f;
        private Material _glassMat;
        private FlickerMode _mode;
        private float _modeTime;
        private float _level;
        private int _buzz;
        private int _blinks = 4;

        /// <summary>Current light level 0..1.</summary>
        public float Level => _level;
        /// <summary>Current mode.</summary>
        public FlickerMode Mode => _mode;

        private void Awake()
        {
            if (_key != null) _keyIntensity = _key.intensity;
            if (_fill != null) _fillIntensity = _fill.intensity;
            if (_glass != null && _glassMaterialIndex >= 0)
            {
                var mats = _glass.sharedMaterials;
                if (_glassMaterialIndex < mats.Length && mats[_glassMaterialIndex] != null)
                {
                    _glassMat = new Material(mats[_glassMaterialIndex]) { name = mats[_glassMaterialIndex].name + "_Runtime" };
                    _glassMat.EnableKeyword("_EMISSION");
                    _glassMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mats[_glassMaterialIndex] = _glassMat;
                    _glass.sharedMaterials = mats;
                }
            }
            SetMode(_startMode);
            _level = _mode == FlickerMode.Off ? 0f : 1f;
            ApplyLevel();
        }

        private void OnDisable()
        {
            CinematicAudio.Stop(_buzz, 0.1f);
            _buzz = 0;
        }

        private void OnDestroy()
        {
            if (_glassMat != null) Destroy(_glassMat);
        }

        /// <summary>Builder wiring.</summary>
        public void Setup(Light key, Light fill, Renderer glass, int glassMaterialIndex, FlickerMode startMode)
        {
            _key = key;
            _fill = fill;
            _glass = glass;
            _glassMaterialIndex = glassMaterialIndex;
            _startMode = startMode;
        }

        /// <summary>Switches mode; <paramref name="blinks"/> applies to <see cref="FlickerMode.StutterOn"/> (1..4).</summary>
        public void SetMode(FlickerMode mode, int blinks = 4)
        {
            _mode = mode;
            _modeTime = 0f;
            _blinks = Mathf.Clamp(blinks, 1, 4);
            if (mode == FlickerMode.StutterOn || mode == FlickerMode.Dying)
                CinematicAudio.Play(CinematicCues.FluorescentFlicker, transform.position);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _modeTime += dt;
            _level = Evaluate(_mode, _modeTime, _blinks);
            if (_mode == FlickerMode.StutterOn && _modeTime > PatternLength(s_stutter, _blinks * 2) + 1.2f) _mode = FlickerMode.On;
            if (_mode == FlickerMode.Dying && _modeTime > PatternLength(s_dying, s_dying.Length)) _mode = FlickerMode.Off;
            ApplyLevel();
            UpdateBuzz();
        }

        /// <summary>Light level of a mode <paramref name="t"/> seconds after it started (pure; used by tests and scrubbing).</summary>
        public static float Evaluate(FlickerMode mode, float t, int blinks = 4)
        {
            switch (mode)
            {
                case FlickerMode.Off: return 0f;
                case FlickerMode.StutterOn:
                {
                    var steps = Mathf.Clamp(blinks, 1, 4) * 2;
                    var end = PatternLength(s_stutter, steps);
                    if (t < end) return PatternLevel(s_stutter, steps, t, true) * 0.9f;
                    // Tubes come up a little dim and warm up.
                    return Mathf.Lerp(0.82f, 1f, Mathf.Clamp01((t - end) / 1.2f)) * Ripple(t);
                }
                case FlickerMode.Dying:
                {
                    var end = PatternLength(s_dying, s_dying.Length);
                    if (t >= end) return 0f;
                    return PatternLevel(s_dying, s_dying.Length, t, true) * Mathf.Lerp(1f, 0.55f, t / end);
                }
                case FlickerMode.Unstable:
                {
                    var n = Mathf.PerlinNoise(t * 3.1f, 0.37f);
                    var dip = n < 0.22f ? Mathf.Lerp(0.35f, 1f, n / 0.22f) : 1f;
                    return dip * Ripple(t);
                }
                default: return Ripple(t);
            }
        }

        private static float Ripple(float t) => 0.97f + 0.03f * Mathf.PerlinNoise(t * 23f, 1.7f);

        private static float PatternLength(float[] pattern, int steps)
        {
            var s = 0f;
            for (var i = 0; i < steps && i < pattern.Length; i++) s += pattern[i];
            return s;
        }

        /// <summary>Alternating on/off durations; even steps are "on" when <paramref name="startOn"/>.</summary>
        private static float PatternLevel(float[] pattern, int steps, float t, bool startOn)
        {
            var acc = 0f;
            for (var i = 0; i < steps && i < pattern.Length; i++)
            {
                acc += pattern[i];
                if (t < acc) return (i % 2 == 0) == startOn ? 1f : 0f;
            }
            return 0f;
        }

        private void ApplyLevel()
        {
            if (_key != null)
            {
                _key.intensity = _keyIntensity * _level;
                _key.enabled = _level > 0.001f;
            }
            if (_fill != null)
            {
                _fill.intensity = _fillIntensity * _level;
                _fill.enabled = _level > 0.001f;
            }
            if (_glassMat != null) _glassMat.SetColor(s_emission, _emission * _level);
            var look = BoothRoot.Current != null ? BoothRoot.Current.Look : null;
            if (look != null) look.SetTubeLevel(_level);
        }

        private void UpdateBuzz()
        {
            var on = _level > 0.5f && (_mode == FlickerMode.On || _mode == FlickerMode.Unstable);
            if (on && _buzz == 0) _buzz = CinematicAudio.Loop(CinematicCues.FluorescentBuzz, transform);
            else if (!on && _buzz != 0 && _mode != FlickerMode.StutterOn)
            {
                CinematicAudio.Stop(_buzz, 0.05f);
                _buzz = 0;
            }
        }
    }
}
