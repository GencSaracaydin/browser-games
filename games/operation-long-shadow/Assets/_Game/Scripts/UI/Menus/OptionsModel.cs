using LongShadow.Core;
using UnityEngine;

namespace LongShadow.UI.Menus
{
    /// <summary>
    /// Every option of GDD §11 "Options" as data, grouped into pages, plus the controls list (GDD §12, TECH_PLAN §7.2).
    /// Ranges come from <see cref="GameSettings"/> constants so the menu can never write a value the service would clamp.
    /// </summary>
    public static class OptionsModel
    {
        /// <summary>Page names in tab order (KEYS is the static controls list).</summary>
        public static readonly string[] Pages = { "CONTROLS", "DISPLAY", "AUDIO", "GAME", "KEYS" };

        /// <summary>Name of the controls-list page.</summary>
        public const string KeysPage = "KEYS";

        private static readonly string[] s_offOn = { "OFF", "ON" };
        private static readonly string[] s_aim = { "HOLD", "TOGGLE" };
        private static readonly string[] s_subtitleSize = { "SMALL", "MEDIUM", "LARGE" };
        private static readonly string[] s_difficulty = { "RECRUIT", "REGULAR", "HARDENED", "VETERAN" };
        private static readonly string[] s_renderScale = { "AUTO", "75 %", "80 %", "85 %", "90 %", "95 %", "100 %" };

        /// <summary>Every option, in display order.</summary>
        public static readonly OptionDef[] All =
        {
            // CONTROLS (GDD §11: sens 0.1-10 default 3.0 = 9 deg per 100 CSS px; ADS x0.5-1.5 default 0.85; invert Y)
            OptionDef.Slider("sens", "MOUSE SENSITIVITY", "CONTROLS", GameSettings.MinSensitivity, GameSettings.MaxSensitivity, 0.1f,
                s => s.MouseSensitivity, (s, v) => s.MouseSensitivity = v, (v, b) => b.Append(v, 1)),
            OptionDef.Slider("ads", "ADS SENSITIVITY", "CONTROLS", GameSettings.MinAdsSens, GameSettings.MaxAdsSens, 0.05f,
                s => s.AdsSensitivity, (s, v) => s.AdsSensitivity = v, (v, b) => b.Append('×').Append(v, 2)),
            OptionDef.Choice("invert", "INVERT Y", "CONTROLS", s_offOn, s => s.InvertY ? 1 : 0, (s, v) => s.InvertY = v > 0.5f),
            OptionDef.Choice("adsToggle", "AIM DOWN SIGHTS", "CONTROLS", s_aim, s => s.AdsToggle ? 1 : 0, (s, v) => s.AdsToggle = v > 0.5f),

            // DISPLAY (FOV 75-105 horizontal default 90; render scale 75-100 %; performance mode; motion blur; grain; brightness; fullscreen)
            OptionDef.Slider("fov", "FIELD OF VIEW", "DISPLAY", GameSettings.MinFov, GameSettings.MaxFov, 1f,
                s => s.FovHorizontal, (s, v) => s.FovHorizontal = v, (v, b) => b.Append(Mathf.RoundToInt(v)).Append('°')),
            OptionDef.Choice("renderScale", "RENDER SCALE", "DISPLAY", s_renderScale, RenderScaleIndex, SetRenderScaleIndex,
                "AUTO KEEPS THE PICTURE UNDER 2.1 MEGAPIXELS"),
            OptionDef.Choice("perf", "PERFORMANCE MODE", "DISPLAY", s_offOn, s => s.PerformanceMode ? 1 : 0, (s, v) => s.PerformanceMode = v > 0.5f,
                "NO AMBIENT OCCLUSION, SHORTER SHADOWS, 80 % RENDER SCALE"),
            OptionDef.Choice("motionBlur", "MOTION BLUR", "DISPLAY", s_offOn, s => s.MotionBlur ? 1 : 0, (s, v) => s.MotionBlur = v > 0.5f),
            OptionDef.Slider("grain", "FILM GRAIN", "DISPLAY", 0f, 100f, 5f, s => s.FilmGrain * 100f, (s, v) => s.FilmGrain = v / 100f, Percent),
            OptionDef.Slider("brightness", "BRIGHTNESS", "DISPLAY", GameSettings.MinBrightness, GameSettings.MaxBrightness, 0.05f,
                s => s.Brightness, (s, v) => s.Brightness = v, (v, b) => b.Append(v, 2, true).Append(" EV")),
            OptionDef.Choice("fullscreen", "FULLSCREEN", "DISPLAY", s_offOn, s => s.Fullscreen ? 1 : 0, (s, v) => s.Fullscreen = v > 0.5f),

            // AUDIO (category gains on pooled AudioSource.volume, TECH_PLAN D9) and subtitles
            OptionDef.Slider("master", "MASTER VOLUME", "AUDIO", 0f, 100f, 5f, s => s.MasterVolume * 100f, (s, v) => s.MasterVolume = v / 100f, Percent),
            OptionDef.Slider("music", "MUSIC", "AUDIO", 0f, 100f, 5f, s => s.MusicVolume * 100f, (s, v) => s.MusicVolume = v / 100f, Percent),
            OptionDef.Slider("effects", "EFFECTS", "AUDIO", 0f, 100f, 5f, s => s.EffectsVolume * 100f, (s, v) => s.EffectsVolume = v / 100f, Percent),
            OptionDef.Slider("voice", "VOICE", "AUDIO", 0f, 100f, 5f, s => s.VoiceVolume * 100f, (s, v) => s.VoiceVolume = v / 100f, Percent),
            OptionDef.Choice("subtitles", "SUBTITLES", "AUDIO", s_offOn, s => s.Subtitles ? 1 : 0, (s, v) => s.Subtitles = v > 0.5f),
            OptionDef.Choice("subtitleSize", "SUBTITLE SIZE", "AUDIO", s_subtitleSize, s => (int)s.SubtitleSize,
                (s, v) => s.SubtitleSize = (SubtitleSize)Mathf.Clamp(Mathf.RoundToInt(v), 0, 2)),
            OptionDef.Choice("translate", "TRANSLATE ENEMY CHATTER", "AUDIO", s_offOn, s => s.TranslateEnemyChatter ? 1 : 0,
                (s, v) => s.TranslateEnemyChatter = v > 0.5f, "SPARROW'S TRANSLATIONS, WITHIN 25 M"),

            // GAME
            OptionDef.Choice("difficulty", "DIFFICULTY", "GAME", s_difficulty, s => (int)s.Difficulty,
                (s, v) => s.Difficulty = (Difficulty)Mathf.Clamp(Mathf.RoundToInt(v), 0, 3), "APPLIES AT THE NEXT CHECKPOINT"),
        };

        /// <summary>The controls list (KEYS page): action and key, in GDD §12 order.</summary>
        public static readonly string[,] Controls =
        {
            { "MOVE", "W A S D" },
            { "LOOK", "MOUSE" },
            { "FIRE", "LEFT MOUSE" },
            { "AIM DOWN SIGHTS", "RIGHT MOUSE" },
            { "RELOAD", "R" },
            { "SPRINT", "LEFT SHIFT" },
            { "JUMP / VAULT / CLIMB", "SPACE" },
            { "CROUCH / SLIDE", "C" },
            { "FRAG (HOLD TO COOK)", "G" },
            { "MELEE", "V" },
            { "INTERACT (HOLD)", "E" },
            { "WEAPONS", "1 / 2 / WHEEL" },
            { "OBJECTIVES", "TAB (HOLD)" },
            { "PAUSE", "ESC (P)" },
            { "SKIP CINEMATIC", "HOLD SPACE" },
        };

        /// <summary>Number of options on a page.</summary>
        public static int CountOn(string page)
        {
            var n = 0;
            for (var i = 0; i < All.Length; i++)
                if (All[i].Page == page) n++;
            return n;
        }

        /// <summary>Option by id, or null.</summary>
        public static OptionDef Find(string id)
        {
            for (var i = 0; i < All.Length; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        /// <summary>
        /// Returns a sanitised copy of <paramref name="current"/> with one option changed (what the menu passes to
        /// <see cref="ISettingsService.Apply"/>; the service's instance is never mutated).
        /// </summary>
        public static GameSettings With(GameSettings current, OptionDef option, float value)
        {
            var s = current != null ? current.Clone() : new GameSettings();
            option?.Set(s, value);
            return s.Sanitize();
        }

        /// <summary>Choice index of the render scale (0 = AUTO, 1..6 = 75..100 %).</summary>
        public static float RenderScaleIndex(GameSettings s)
        {
            if (s.RenderScaleAuto) return 0;
            return Mathf.Clamp(Mathf.RoundToInt((s.RenderScale - 0.70f) / 0.05f), 1, 6);
        }

        /// <summary>Writes a render-scale choice (D21: AUTO until the player picks a value).</summary>
        public static void SetRenderScaleIndex(GameSettings s, float index)
        {
            var i = Mathf.Clamp(Mathf.RoundToInt(index), 0, 6);
            s.RenderScaleAuto = i == 0;
            if (i > 0) s.RenderScale = 0.70f + 0.05f * i;
        }

        private static void Percent(float v, LongShadow.UI.Common.TextBuffer b) => b.Append(Mathf.RoundToInt(v)).Append(" %");
    }
}
