using LongShadow.Cinematics.FX;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// The booth grade (TECH_PLAN §3.7 VP_Booth: colour filter #C8D6C0, saturation −35, grain Large01 0.55, lens
    /// distortion −0.12, vignette 0.35, CA 0.2; tube flicker drives exposure ±0.15). Cam_Booth's volume mask is Booth,
    /// so the booth carries its own tonemapping and bloom. Uses WP10's VP_Booth profile from LIB_Cinematics when it
    /// exists, otherwise builds the §3.7 values at runtime; a second, higher-priority driver volume moves exposure with
    /// the tube and takes the outro's 30 % desaturation (GDD §8.8 shot 5).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoothLook : MonoBehaviour
    {
        /// <summary>Exposure swing of the tube flicker in EV (TECH_PLAN §3.7).</summary>
        public const float FlickerEv = 0.15f;
        /// <summary>Booth base saturation (§3.7).</summary>
        public const float BaseSaturation = -35f;

        private Volume _base;
        private Volume _driver;
        private ColorAdjustments _drive;
        private float _baseExposure;
        private float _baseSaturation = BaseSaturation;
        private float _tube = 1f;
        private float _saturationOffset;
        private float _exposureOffset;

        private void Awake()
        {
            var template = CinematicLibrary.Lookup<VolumeProfile>(CinematicAssetIds.ProfileBooth);
            _base = VolumeFactory.Create(transform, "VP_Booth", LayerIds.Booth, 10f, template);
            _base.weight = 1f;
            if (template == null) BuildFallback(_base.sharedProfile);
            if (_base.sharedProfile.TryGet(out ColorAdjustments ca))
            {
                if (ca.postExposure.overrideState) _baseExposure = ca.postExposure.value;
                if (ca.saturation.overrideState) _baseSaturation = ca.saturation.value;
            }
            _driver = VolumeFactory.Create(transform, "VP_Booth_Driver", LayerIds.Booth, 20f, null);
            _driver.weight = 1f;
            _drive = VolumeFactory.Get<ColorAdjustments>(_driver);
            _drive.postExposure.overrideState = true;
            _drive.saturation.overrideState = true;
            Apply();
        }

        private void OnDestroy()
        {
            VolumeFactory.Destroy(_base);
            VolumeFactory.Destroy(_driver);
        }

        /// <summary>Tube level 0..1 from <see cref="FlickerLight"/>.</summary>
        public void SetTubeLevel(float level)
        {
            _tube = Mathf.Clamp01(level);
            Apply();
        }

        /// <summary>Extra saturation (−100..0; the outro loses 30 %).</summary>
        public void SetSaturationOffset(float offset)
        {
            _saturationOffset = offset;
            Apply();
        }

        /// <summary>Extra exposure in EV (flashes inside the booth).</summary>
        public void SetExposureOffset(float ev)
        {
            _exposureOffset = ev;
            Apply();
        }

        private void Apply()
        {
            if (_drive == null) return;
            _drive.postExposure.value = _baseExposure + (_tube - 1f) * FlickerEv + _exposureOffset;
            _drive.saturation.value = Mathf.Clamp(_baseSaturation + _saturationOffset, -100f, 100f);
        }

        /// <summary>The §3.7 VP_Booth values plus tonemapping and bloom (booth cameras see only the Booth volume layer).</summary>
        public static void BuildFallback(VolumeProfile p)
        {
            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            var ca = p.Add<ColorAdjustments>(false);
            ca.colorFilter.Override(new Color(0xC8 / 255f, 0xD6 / 255f, 0xC0 / 255f));
            ca.saturation.Override(BaseSaturation);
            ca.contrast.Override(12f);
            ca.postExposure.Override(0.35f);
            var grain = p.Add<FilmGrain>(false);
            grain.type.Override(FilmGrainLookup.Large01);
            grain.intensity.Override(0.55f);
            grain.response.Override(0.8f);
            var ld = p.Add<LensDistortion>(false);
            ld.intensity.Override(-0.12f);
            var vig = p.Add<Vignette>(false);
            vig.intensity.Override(0.35f);
            vig.smoothness.Override(0.45f);
            var chroma = p.Add<ChromaticAberration>(false);
            chroma.intensity.Override(0.2f);
            var bloom = p.Add<Bloom>(false);
            bloom.intensity.Override(0.6f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.65f);
        }
    }
}
