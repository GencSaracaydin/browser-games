using LongShadow.Core;
using LongShadow.World.Look.Common;
using LongShadow.World.Look.Stage;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// The relay mast's red obstruction lights (GDD §8.5 row 4, §6.6 BLD-12; TECH_PLAN §7.14): dark before the strike;
    /// on <see cref="GameEvents.PracticalsStrike"/> they come on top to bottom 0.3 s apart, then all blink together at
    /// 1 Hz. A checkpoint or late load past s 1.97 starts them blinking at once. Emissive only (90 m up, far from the
    /// player): no realtime light, no budget slot. The lens renderers are hidden while dark, so the prefab can live in
    /// S_Persistent at the mast position (378, 0, 362) whether or not the mast's section is loaded.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AviationLights : MonoBehaviour
    {
        [SerializeField, Tooltip("Lamp renderers ordered top to bottom (5 on the mast).")]
        private Renderer[] _lamps = new Renderer[0];
        [SerializeField, ColorUsage(false, true)] private Color _emission = new Color(12f, 0.6f, 0.3f, 1f);
        [SerializeField, Tooltip("Seconds between lamps coming on (GDD: 0.3 s).")]
        private float _stagger = 0.3f;
        [SerializeField, Tooltip("Blink frequency after the cascade (GDD: 1 Hz).")]
        private float _blinkHz = 1f;
        [SerializeField, Range(0.05f, 0.95f), Tooltip("On fraction of each blink.")]
        private float _duty = 0.5f;

        private MaterialPropertyBlock _mpb;
        private float _startTime = float.PositiveInfinity;
        private bool[] _state = new bool[0];

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _state = new bool[_lamps.Length];
            for (var i = 0; i < _lamps.Length; i++) Set(i, false, true);
        }

        private void OnEnable()
        {
            GameEvents.PracticalsStrike += OnStrike;
            GameEvents.LightStageChanged += OnStage;
            var ls = LightStageController.Active;
            if (ls != null && ls.S >= LightStageTable.StrikeS) _startTime = Time.time - 10f;
        }

        private void OnDisable()
        {
            GameEvents.PracticalsStrike -= OnStrike;
            GameEvents.LightStageChanged -= OnStage;
        }

        private void OnStrike() => _startTime = Time.time;

        private void OnStage(float s)
        {
            if (s < LightStageTable.StrikeS - 0.005f) _startTime = float.PositiveInfinity;
            else if (float.IsPositiveInfinity(_startTime)) _startTime = Time.time - 10f;
        }

        private void Update()
        {
            var since = Time.time - _startTime;
            var cascadeEnd = _stagger * Mathf.Max(0, _lamps.Length - 1) + 0.6f;
            for (var i = 0; i < _lamps.Length; i++)
            {
                bool on;
                if (since < 0f || float.IsNaN(since)) on = false;
                else if (since < cascadeEnd) on = since >= _stagger * i;
                else
                {
                    var phase = (since - cascadeEnd) * _blinkHz;
                    on = phase - Mathf.Floor(phase) < _duty;
                }
                Set(i, on, false);
            }
        }

        private void Set(int i, bool on, bool force)
        {
            if (!force && _state[i] == on) return;
            _state[i] = on;
            var r = _lamps[i];
            if (r == null) return;
            r.enabled = on; // lens glow only: dark lenses are not drawn (the mast mesh carries the housings)
            if (!on) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(LookShaderIds.EmissionColor, on ? _emission : Color.black);
            r.SetPropertyBlock(_mpb);
        }

#if UNITY_EDITOR
        /// <summary>Editor builders: lamps ordered top to bottom.</summary>
        public void EditorSetup(Renderer[] lampsTopToBottom) => _lamps = lampsTopToBottom ?? new Renderer[0];
#endif
    }
}
