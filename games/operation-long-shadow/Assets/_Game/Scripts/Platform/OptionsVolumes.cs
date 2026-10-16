using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Platform
{
    /// <summary>
    /// Three runtime global volumes owned by <see cref="QualityApplier"/> (priority 1000; one set per volume layer, so the
    /// booth camera's Booth-only volume mask gets them too) that apply the player's options on top of WP10's and WP09's
    /// profiles without touching them (TECH_PLAN §7.1 places brightness on a "VP_Brightness" volume; it is built here at
    /// runtime instead, so no profile asset ships and nothing in S_Persistent has to be wired):
    /// <list type="bullet">
    /// <item>Brightness: Channel Mixer gain 2^EV on the diagonal (no profile uses the Channel Mixer, and it scales colour
    /// linearly before tonemapping, i.e. like exposure, whereas a Post Exposure override would replace the interior
    /// exposure volumes instead of adding to them).</item>
    /// <item>Film grain amount a: Film Grain intensity 0 at weight 1 - a, which blends every profile's grain to a x its value.</item>
    /// <item>Motion blur off: Motion Blur intensity 0 at weight 1.</item>
    /// </list>
    /// </summary>
    internal sealed class OptionsVolumes
    {
        private readonly Volume _brightness;
        private readonly Volume _grain;
        private readonly Volume _motionBlur;
        private readonly ChannelMixer _mixer;
        private readonly VolumeProfile[] _profiles;

        internal OptionsVolumes(Transform parent, int layer, float priority)
        {
            _profiles = new VolumeProfile[3];
            var suffix = layer == 0 ? string.Empty : "_L" + layer;
            _brightness = Create(parent, "VOL_Options_Brightness" + suffix, layer, priority, 0);
            _mixer = _brightness.sharedProfile.Add<ChannelMixer>(true);

            _grain = Create(parent, "VOL_Options_FilmGrain" + suffix, layer, priority, 1);
            var grain = _grain.sharedProfile.Add<FilmGrain>(false);
            grain.intensity.Override(0f);

            _motionBlur = Create(parent, "VOL_Options_MotionBlur" + suffix, layer, priority, 2);
            var blur = _motionBlur.sharedProfile.Add<MotionBlur>(false);
            blur.intensity.Override(0f);
        }

        /// <summary>Applies brightness (EV), the film-grain amount (0..1) and the motion-blur switch.</summary>
        internal void Apply(float brightnessEv, float filmGrain01, bool motionBlur)
        {
            var gain = RenderScalePolicy.BrightnessGain(brightnessEv) * 100f;
            _mixer.redOutRedIn.Override(gain);
            _mixer.greenOutGreenIn.Override(gain);
            _mixer.blueOutBlueIn.Override(gain);
            _brightness.weight = Mathf.Approximately(brightnessEv, 0f) ? 0f : 1f;
            _brightness.enabled = _brightness.weight > 0f;

            _grain.weight = 1f - Mathf.Clamp01(filmGrain01);
            _grain.enabled = _grain.weight > 0f;

            _motionBlur.weight = motionBlur ? 0f : 1f;
            _motionBlur.enabled = !motionBlur;
        }

        /// <summary>Destroys the runtime profiles.</summary>
        internal void Dispose()
        {
            foreach (var p in _profiles)
                if (p != null) Object.Destroy(p);
        }

        private Volume Create(Transform parent, string name, int layer, float priority, int index)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = priority;
            v.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = name;
            _profiles[index] = profile;
            v.sharedProfile = profile;
            v.enabled = false;
            return v;
        }
    }
}
