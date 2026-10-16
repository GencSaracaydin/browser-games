using UnityEngine;

namespace LongShadow.World.Look.Stage
{
    /// <summary>
    /// Look-dev calibration on top of the GDD §9.2a curves (asset <c>Assets/_Game/Data/Lighting/LST_Default.asset</c>,
    /// built by <c>LongShadow/Look/Build Volumes and Materials</c>). The GDD numbers stay in <see cref="LightStageTable"/>;
    /// this asset only holds scales and calibration that LookCapture review may adjust (GDD §9.2a "fog densities are
    /// starting values"; TECH_PLAN §3.4 "bias tuned in the vertical slice"). A controller without an asset uses
    /// <see cref="CreateDefault"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/Look/LightStage Tuning", fileName = "LST_Default")]
    public sealed class LightStageTuning : ScriptableObject
    {
        [Header("Sky (Sky/PanoBlend, GDD §9.3)")]
        [SerializeField, Tooltip("Linear multiplier of sky texture A (kiara_8_sunset). Physically consistent value = " +
                                 "exposure_multiplier_per_unit_sun_intensity (0.1452, <id>_sky.json) x the s=0 sun intensity 2.0.")]
        private float _skyExposureA = 0.29f;
        [SerializeField, Tooltip("Linear multiplier of sky texture B (kiara_9_dusk). Art-directed: Poly Haven skies are not " +
                                 "absolutely calibrated, so the dusk sky is set to read darker than the golden sky after the stage post exposure.")]
        private float _skyExposureB = 0.075f;
        [SerializeField, Tooltip("Re-expands texels above the 0.6 shoulder of the LDR encode (glow around the painted-out sun) for bloom.")]
        private float _skyGlowBoost = 1.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Horizon haze: blends the sky toward the fog colour near the horizon.")]
        private float _horizonFog = 0.35f;
        [SerializeField, Tooltip("Height of the horizon haze band (sine of the elevation).")]
        private float _horizonFogHeight = 0.06f;

        [Header("Lights")]
        [SerializeField] private float _sunIntensityScale = 1f;
        [SerializeField] private float _skyKeyIntensityScale = 1f;
        [SerializeField, Tooltip("Scales the per-stage depth bias of LightStageTable.")]
        private float _shadowDepthBiasScale = 1f;
        [SerializeField, Tooltip("Scales the per-stage normal bias of LightStageTable.")]
        private float _shadowNormalBiasScale = 1f;
        [SerializeField, Tooltip("Rendering layers lit by SKY_KEY (World by default; exclude interior layers here if Level adds them).")]
        private uint _skyKeyRenderingLayers = 1u;

        [Header("Atmosphere")]
        [SerializeField, Tooltip("Scales every GDD fog density. 0.8: the Relay Station and mast (~470 m) keep ~50 % contrast " +
                                 "from the ridge in Beat 1 (GDD §9.2a acceptance test).")]
        private float _fogDensityScale = 0.8f;
        [SerializeField, Tooltip("Scales the ambient SH lerp (AmbientSHSet).")]
        private float _ambientScale = 1f;

        [Header("Sun disc and flare (GDD §9.3)")]
        [SerializeField, Tooltip("Disc angular diameter in degrees.")]
        private float _sunDiscDiameter = 0.53f;
        [SerializeField, Tooltip("Disc distance from the camera in metres (camera far plane 3000 m).")]
        private float _sunDiscDistance = 2900f;
        [SerializeField, Tooltip("Disc HDR luminance per unit sun intensity (it blooms).")]
        private float _sunDiscLuminance = 60f;
        [SerializeField, Tooltip("Lens flare intensity per unit sun intensity.")]
        private float _flareIntensity = 0.6f;

        [Header("Smoothing")]
        [SerializeField, Tooltip("Seconds for the AI sight range to step between stages (GDD §9.2a: 2 s).")]
        private float _sightSmoothingSeconds = 2f;

        /// <summary>Linear multiplier of sky texture A.</summary>
        public float SkyExposureA => _skyExposureA;
        /// <summary>Linear multiplier of sky texture B.</summary>
        public float SkyExposureB => _skyExposureB;
        /// <summary>Glow re-expansion above the LDR shoulder.</summary>
        public float SkyGlowBoost => _skyGlowBoost;
        /// <summary>Horizon haze amount.</summary>
        public float HorizonFog => _horizonFog;
        /// <summary>Horizon haze height.</summary>
        public float HorizonFogHeight => _horizonFogHeight;
        /// <summary>Sun intensity scale.</summary>
        public float SunIntensityScale => _sunIntensityScale;
        /// <summary>Sky key intensity scale.</summary>
        public float SkyKeyIntensityScale => _skyKeyIntensityScale;
        /// <summary>Depth bias scale.</summary>
        public float ShadowDepthBiasScale => _shadowDepthBiasScale;
        /// <summary>Normal bias scale.</summary>
        public float ShadowNormalBiasScale => _shadowNormalBiasScale;
        /// <summary>Rendering-layer mask of SKY_KEY.</summary>
        public uint SkyKeyRenderingLayers => _skyKeyRenderingLayers;
        /// <summary>Fog density scale.</summary>
        public float FogDensityScale => _fogDensityScale;
        /// <summary>Ambient scale.</summary>
        public float AmbientScale => _ambientScale;
        /// <summary>Disc angular diameter (degrees).</summary>
        public float SunDiscDiameter => _sunDiscDiameter;
        /// <summary>Disc distance (metres).</summary>
        public float SunDiscDistance => _sunDiscDistance;
        /// <summary>Disc HDR luminance per unit sun intensity.</summary>
        public float SunDiscLuminance => _sunDiscLuminance;
        /// <summary>Flare intensity per unit sun intensity.</summary>
        public float FlareIntensity => _flareIntensity;
        /// <summary>AI sight step seconds.</summary>
        public float SightSmoothingSeconds => _sightSmoothingSeconds;

        /// <summary>A runtime instance with the default values (not saved).</summary>
        public static LightStageTuning CreateDefault()
        {
            var t = CreateInstance<LightStageTuning>();
            t.name = "LST_Default (runtime)";
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        /// <summary>Editor builders: writes the sky calibration read from the HDRI json files.</summary>
        public void SetSkyCalibration(float exposureA, float exposureB)
        {
            _skyExposureA = Mathf.Max(0f, exposureA);
            _skyExposureB = Mathf.Max(0f, exposureB);
        }

        private void OnValidate()
        {
            _skyExposureA = Mathf.Max(0f, _skyExposureA);
            _skyExposureB = Mathf.Max(0f, _skyExposureB);
            _fogDensityScale = Mathf.Max(0f, _fogDensityScale);
            _sunDiscDistance = Mathf.Clamp(_sunDiscDistance, 100f, 2990f);
            _sightSmoothingSeconds = Mathf.Max(0f, _sightSmoothingSeconds);
        }
    }
}
