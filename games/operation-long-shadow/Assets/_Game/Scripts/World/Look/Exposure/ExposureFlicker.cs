using LongShadow.World.Look.Practicals;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.World.Look.Exposure
{
    /// <summary>
    /// Fluorescent-tube exposure flicker for a volume (GDD §9.6 VP_Booth: "tube flicker drives exposure ±0.15"): the
    /// volume's profile is deep-copied at start and its post exposure follows <see cref="FlickerNoise.Fluorescent"/>
    /// around the authored value. Unscaled time (the booth plays during inserts at time scale 0). WP09 adds it to the
    /// booth volume by type name (<c>LongShadow.World.Look.Exposure.ExposureFlicker</c>).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class ExposureFlicker : MonoBehaviour
    {
        [SerializeField, Tooltip("Peak deviation in EV (GDD: ±0.15).")]
        private float _amplitude = 0.15f;
        [SerializeField, Tooltip("Noise seed.")]
        private float _seed = 11f;

        private Volume _volume;
        private VolumeProfile _runtime;
        private ColorAdjustments _adjust;
        private float _base;

        private void Start()
        {
            _volume = GetComponent<Volume>();
            if (_volume == null || _volume.sharedProfile == null) return;
            _runtime = VolumeProfileUtil.DeepCopy(_volume.sharedProfile, _volume.sharedProfile.name + " (flicker)");
            _adjust = VolumeProfileUtil.GetOrAdd<ColorAdjustments>(_runtime);
            _base = _adjust.postExposure.overrideState ? _adjust.postExposure.value : 0f;
            _volume.sharedProfile = _runtime;
        }

        private void Update()
        {
            if (_adjust == null) return;
            // Fluorescent() sits at 0.97 ± 0.03 and dips to 0.35–0.75: map 0.97 → 0 EV, ±0.03 shimmer → ±0.02 EV, dips → −amplitude.
            var f = FlickerNoise.Fluorescent(Time.unscaledTime, _seed);
            var ev = Mathf.Clamp((f - 0.97f) / 0.6f, -1f, 0.1f) * _amplitude;
            _adjust.postExposure.Override(_base + ev);
        }

        private void OnDestroy() => VolumeProfileUtil.DestroyRuntime(_runtime);
    }
}
