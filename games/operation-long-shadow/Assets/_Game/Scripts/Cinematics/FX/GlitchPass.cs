using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Cinematics.FX
{
    /// <summary>
    /// Drives the inactive FullScreenPassRendererFeature "Glitch" that WP00 put on PC_Renderer (TECH_PLAN §3.5): WP09
    /// assigns its SG_Glitch material at runtime and toggles the feature, so nobody edits the renderer asset. The feature
    /// and its previous material are restored when this object goes away (in the editor the renderer asset would
    /// otherwise stay modified after play mode). When the feature cannot be found the overlay's static/tear layer is the
    /// fallback (no RGB split).
    /// </summary>
    public sealed class GlitchPass
    {
        /// <summary>Name of the renderer feature (TECH_PLAN §3.5).</summary>
        public const string FeatureName = "Glitch";

        private FullScreenPassRendererFeature _feature;
        private Material _previous;
        private bool _previousActive;
        private Material _material;
        private bool _searched;
        private bool _active;

        /// <summary>True when the renderer feature was found and a material is available.</summary>
        public bool Available
        {
            get
            {
                Find();
                return _feature != null && _material != null;
            }
        }

        /// <summary>The runtime material (intensity, seed, white frame).</summary>
        public Material Material => _material;

        /// <summary>Creates the driver with the SG_Glitch material template (instanced).</summary>
        public GlitchPass(Material template)
        {
            if (template != null) _material = new Material(template) { name = template.name + "_Runtime" };
            else
            {
                var shader = Shader.Find(ShaderIds.GlitchShader);
                if (shader != null) _material = new Material(shader) { name = "M_FX_Glitch_Runtime" };
            }
        }

        /// <summary>Sets the glitch amount (0 turns the feature off), RGB split and the one-frame white.</summary>
        public void Set(float intensity, float white)
        {
            Find();
            if (_feature == null || _material == null) return;
            var on = intensity > 0.001f || white > 0.001f;
            if (on)
            {
                _material.SetFloat(ShaderIds.Intensity, Mathf.Clamp01(intensity));
                _material.SetFloat(ShaderIds.White, Mathf.Clamp01(white));
                _material.SetFloat(ShaderIds.Seed, Random.value * 97f);
                _material.SetFloat(ShaderIds.Split, 0.004f + 0.012f * intensity);
                _material.SetFloat(ShaderIds.Tear, intensity);
            }
            if (on == _active) return;
            _active = on;
            if (on) _feature.passMaterial = _material;
            _feature.SetActive(on);
        }

        /// <summary>Restores the feature to its state before the first use.</summary>
        public void Restore()
        {
            if (_feature != null)
            {
                _feature.passMaterial = _previous;
                _feature.SetActive(_previousActive);
            }
            _active = false;
            if (_material != null) Object.Destroy(_material);
            _material = null;
        }

        private void Find()
        {
            if (_searched) return;
            _searched = true;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;
            var list = urp.rendererDataList;
            for (var i = 0; i < list.Length; i++)
            {
                var data = list[i];
                if (data == null) continue;
                foreach (var f in data.rendererFeatures)
                {
                    if (f is FullScreenPassRendererFeature fs && f.name == FeatureName)
                    {
                        _feature = fs;
                        _previous = fs.passMaterial;
                        _previousActive = fs.isActive;
                        return;
                    }
                }
            }
            LSLog.WarnOnce("glitch-feature", LogCategory.Cinematics,
                "Renderer feature '" + FeatureName + "' not found on the URP renderers; glitches use the overlay fallback.");
        }
    }
}
