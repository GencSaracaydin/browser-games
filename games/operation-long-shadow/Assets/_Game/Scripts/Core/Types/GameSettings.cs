using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Every player option of GDD §11 "Options", with GDD defaults. Serialised with <see cref="JsonUtility"/> by
    /// <see cref="ISettingsService"/> (localStorage on the web, PlayerPrefs in the editor). Call <see cref="Sanitize"/>
    /// after loading untrusted data.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        /// <summary>Schema version for migrations.</summary>
        public int Version = 1;

        // ---- controls (GDD §11, §12)
        /// <summary>Mouse sensitivity 0.1-10. Rotation in degrees = CSS px x sens x <see cref="DegreesPerPixelPerSens"/>.</summary>
        public float MouseSensitivity = 3.0f;
        /// <summary>ADS sensitivity multiplier 0.5-1.5 (also scaled by the FOV ratio).</summary>
        public float AdsSensitivity = 0.85f;
        /// <summary>Invert vertical look.</summary>
        public bool InvertY;
        /// <summary>Aim-down-sights as a toggle instead of hold.</summary>
        public bool AdsToggle;

        // ---- view
        /// <summary>Horizontal field of view in degrees, 75-105.</summary>
        public float FovHorizontal = 90f;

        // ---- audio (category gains on pooled AudioSource.volume, TECH_PLAN D9)
        /// <summary>Master volume 0..1.</summary>
        public float MasterVolume = 1f;
        /// <summary>Music volume 0..1.</summary>
        public float MusicVolume = 1f;
        /// <summary>Effects volume 0..1.</summary>
        public float EffectsVolume = 1f;
        /// <summary>Voice volume 0..1.</summary>
        public float VoiceVolume = 1f;

        // ---- subtitles
        /// <summary>Show subtitles.</summary>
        public bool Subtitles = true;
        /// <summary>Subtitle size S/M/L.</summary>
        public SubtitleSize SubtitleSize = SubtitleSize.Medium;
        /// <summary>Subtitle enemy barks within 25 m (Sparrow's translations).</summary>
        public bool TranslateEnemyChatter = true;

        // ---- graphics
        /// <summary>Performance mode (TECH_PLAN §3.3: SSAO off, 2 cascades, 50 m shadows, render scale 0.8, no motion blur).</summary>
        public bool PerformanceMode;
        /// <summary>True until the player picks a render scale; then <see cref="RenderScale"/> is used (TECH_PLAN D21).</summary>
        public bool RenderScaleAuto = true;
        /// <summary>Render scale 0.75-1.0 (used when <see cref="RenderScaleAuto"/> is false).</summary>
        public float RenderScale = 1f;
        /// <summary>Camera motion blur (VP_Base, 0.15 Low).</summary>
        public bool MotionBlur = true;
        /// <summary>Film grain amount 0..1 (1 = the profile's value).</summary>
        public float FilmGrain = 1f;
        /// <summary>Brightness in EV, -0.5..+0.5.</summary>
        public float Brightness;
        /// <summary>Fullscreen requested.</summary>
        public bool Fullscreen;

        // ---- gameplay
        /// <summary>Difficulty (applies at the next checkpoint).</summary>
        public Difficulty Difficulty = Difficulty.Regular;

        /// <summary>GDD §11 mouse formula constant: 3.0 sens gives 9 degrees per 100 CSS px.</summary>
        public const float DegreesPerPixelPerSens = 0.03f;

        /// <summary>Allowed ranges (GDD §11).</summary>
        public const float MinSensitivity = 0.1f, MaxSensitivity = 10f, MinAdsSens = 0.5f, MaxAdsSens = 1.5f,
                           MinFov = 75f, MaxFov = 105f, MinRenderScale = 0.75f, MaxRenderScale = 1f,
                           MinBrightness = -0.5f, MaxBrightness = 0.5f;

        /// <summary>Clamps every value into its GDD range (call after deserialising).</summary>
        public GameSettings Sanitize()
        {
            MouseSensitivity = ClampFinite(MouseSensitivity, MinSensitivity, MaxSensitivity, 3f);
            AdsSensitivity = ClampFinite(AdsSensitivity, MinAdsSens, MaxAdsSens, 0.85f);
            FovHorizontal = ClampFinite(FovHorizontal, MinFov, MaxFov, 90f);
            MasterVolume = ClampFinite(MasterVolume, 0f, 1f, 1f);
            MusicVolume = ClampFinite(MusicVolume, 0f, 1f, 1f);
            EffectsVolume = ClampFinite(EffectsVolume, 0f, 1f, 1f);
            VoiceVolume = ClampFinite(VoiceVolume, 0f, 1f, 1f);
            RenderScale = ClampFinite(RenderScale, MinRenderScale, MaxRenderScale, 1f);
            FilmGrain = ClampFinite(FilmGrain, 0f, 1f, 1f);
            Brightness = ClampFinite(Brightness, MinBrightness, MaxBrightness, 0f);
            if (SubtitleSize < SubtitleSize.Small || SubtitleSize > SubtitleSize.Large) SubtitleSize = SubtitleSize.Medium;
            if (Difficulty < Difficulty.Recruit || Difficulty > Difficulty.Veteran) Difficulty = Difficulty.Regular;
            return this;
        }

        /// <summary>Deep copy.</summary>
        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        /// <summary>Serialises to JSON.</summary>
        public string ToJson() => JsonUtility.ToJson(this);

        /// <summary>Parses JSON; returns defaults on any error. Never throws.</summary>
        public static GameSettings FromJson(string json)
        {
            var s = new GameSettings();
            if (string.IsNullOrEmpty(json)) return s;
            try
            {
                JsonUtility.FromJsonOverwrite(json, s);
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Settings, "GameSettings JSON invalid, using defaults: " + e.Message);
                s = new GameSettings();
            }
            return s.Sanitize();
        }

        private static float ClampFinite(float v, float min, float max, float fallback)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return fallback;
            return Mathf.Clamp(v, min, max);
        }
    }
}
