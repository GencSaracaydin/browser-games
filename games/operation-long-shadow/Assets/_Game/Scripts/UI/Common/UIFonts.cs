using System;
using System.Collections.Generic;
using LongShadow.Core;
using TMPro;
using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Font resolution for the UI (GDD §11, TECH_PLAN §7.13). Fallback chain per role: the theme's font for the role →
    /// a related role of the theme (typewriter ↔ clean typewriter ↔ mono, condensed ↔ condensed medium) → the theme's
    /// Fallback slot → TMP's default font (LiberationSans SDF from TMP Essential Resources) → a dynamic font asset made at
    /// runtime from Unity's built-in font. Each missing role is logged once. Materials: a soft dark underlay ("shadow")
    /// for HUD text and tinted soft glows for CRT text (cached; never created per frame).
    /// </summary>
    public static class UIFonts
    {
        private static readonly Dictionary<UITheme, FontSet> s_sets = new Dictionary<UITheme, FontSet>();
        private static readonly Dictionary<GlowKey, Material> s_glows = new Dictionary<GlowKey, Material>();
        private static readonly Dictionary<TMP_FontAsset, Material> s_shadows = new Dictionary<TMP_FontAsset, Material>();
        private static FontSet s_noTheme;
        private static TMP_FontAsset s_fallback;
        private static bool s_fallbackTried;

        private const string UnderlayKeyword = "UNDERLAY_ON";
        private static readonly int s_underlayColor = Shader.PropertyToID("_UnderlayColor");
        private static readonly int s_underlayOffsetX = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int s_underlayOffsetY = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int s_underlayDilate = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int s_underlaySoftness = Shader.PropertyToID("_UnderlaySoftness");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_sets.Clear();
            s_glows.Clear();
            s_shadows.Clear();
            s_noTheme = null;
            s_fallback = null;
            s_fallbackTried = false;
        }

        /// <summary>
        /// Characters every UI font atlas must contain (the builder prefills them before making the atlas static). Covers
        /// DIALOGUE.json, the GDD §7.12 objectives, §8.3 title card, §8.9 debrief and the menus.
        /// </summary>
        public const string RequiredCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "°·×–—‘’“”…•";

        /// <summary>Characters the Sardani glyph font maps (Tools/ui/glyph_font.py): Latin letters, digits, a little punctuation.</summary>
        public const string SardaniCharacters =
            " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:-!?'\"/\u2014\u2013\u00B7\u2018\u2019\u201C\u201D";

        /// <summary>Resolves (and caches) the fonts of a theme (null = no theme). Never returns null.</summary>
        public static FontSet Resolve(UITheme theme)
        {
            if (theme == null)
            {
                if (s_noTheme == null) s_noTheme = Build(null);
                return s_noTheme;
            }
            if (s_sets.TryGetValue(theme, out var set)) return set;
            set = Build(theme);
            s_sets[theme] = set;
            return set;
        }

        /// <summary>Forgets cached font sets (after the theme builder changed a theme in the editor).</summary>
        public static void ClearCache()
        {
            s_sets.Clear();
            s_noTheme = null;
        }

        private static FontSet Build(UITheme theme)
        {
            const int n = (int)FontRole.Fallback + 1;
            var fonts = new TMP_FontAsset[n];
            var shadows = new Material[n];
            var glows = new Material[n];
            var own = new bool[n];
            var fallback = theme != null ? theme.GetFont(FontRole.Fallback) : null;
            if (fallback == null) fallback = Fallback;

            for (var i = 0; i < n; i++)
            {
                var role = (FontRole)i;
                var f = theme != null ? theme.GetFont(role) : null;
                own[i] = f != null;
                if (f == null && theme != null)
                {
                    f = FirstRelated(theme, role);
                    if (role != FontRole.Fallback)
                        LSLog.WarnOnce("ui-font:" + role, LogCategory.UI,
                            "UI font for role " + role + " is missing (manifest " + ManifestIdOf(role) + "); using " +
                            (f != null ? f.name : fallback != null ? fallback.name : "nothing") + ".");
                }
                if (f == null) f = fallback;
                fonts[i] = f;
                var savedShadow = own[i] && theme != null ? theme.GetShadow(role) : null;
                shadows[i] = savedShadow != null ? savedShadow : ShadowFor(f);
                var savedGlow = own[i] && theme != null ? theme.GetGlow(role) : null;
                glows[i] = savedGlow != null ? savedGlow : (f != null ? f.material : null);
            }
            if (fallback == null)
                LSLog.ErrorOnce("ui-font-none", LogCategory.UI,
                    "No UI font at all: run LongShadow/UI/1 Import TMP Essential Resources, then LongShadow/UI/Build Theme.");

            return new FontSet(fonts, shadows, glows, own);
        }

        /// <summary>
        /// TMP's default font when TMP Essential Resources are imported (checked without triggering TMP's importer
        /// window), else a dynamic font asset from Unity's built-in font, else null.
        /// </summary>
        public static TMP_FontAsset Fallback
        {
            get
            {
                if (s_fallback != null || s_fallbackTried) return s_fallback;
                s_fallbackTried = true;
                var settings = Resources.Load<TMP_Settings>("TMP Settings");
                if (settings != null) s_fallback = TMP_Settings.defaultFontAsset;
                if (s_fallback == null && Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
                {
                    LSLog.ErrorOnce("ui-font-noshader", LogCategory.UI,
                        "TMP shaders are missing (TMP Essential Resources not imported): UI text cannot render. " +
                        "Run LongShadow/UI/1 Import TMP Essential Resources.");
                    return null;
                }
                if (s_fallback == null)
                {
                    try
                    {
                        var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        if (builtin != null)
                        {
                            s_fallback = TMP_FontAsset.CreateFontAsset(builtin);
                            if (s_fallback != null)
                            {
                                s_fallback.name = "LegacyRuntime SDF (runtime)";
                                s_fallback.hideFlags = HideFlags.DontSave;
                                LSLog.WarnOnce("ui-font-builtin", LogCategory.UI,
                                    "TMP default font missing; using a runtime font asset made from Unity's built-in font.");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        LSLog.Exception(LogCategory.UI, e);
                        s_fallback = null;
                    }
                }
                return s_fallback;
            }
        }

        /// <summary>
        /// Applies a role to a text: font plus its shadow material (when <paramref name="shadow"/>), or a tinted glow
        /// when <paramref name="glow"/> has alpha &gt; 0 (CRT text). Call at build time, not per frame.
        /// </summary>
        public static void Apply(TMP_Text text, FontSet fonts, FontRole role, bool shadow, Color glow = default)
        {
            if (text == null || fonts == null) return;
            var f = fonts[role];
            if (f == null) return;
            text.font = f;
            Material m = null;
            if (glow.a > 0f) m = Glow(fonts, role, glow);
            else if (shadow) m = fonts.Shadow(role);
            if (m != null) text.fontSharedMaterial = m;
        }

        /// <summary>A soft glow material (underlay in <paramref name="color"/>) for a role; cached per font and colour.</summary>
        public static Material Glow(FontSet fonts, FontRole role, Color color)
        {
            if (fonts == null) return null;
            var font = fonts[role];
            var source = fonts.GlowBase(role);
            if (font == null || source == null) return null;
            Color32 c = color;
            var key = new GlowKey(font, (uint)((c.r << 24) | (c.g << 16) | (c.b << 8) | c.a));
            if (s_glows.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(source) { name = font.name + " Glow " + ColorUtility.ToHtmlStringRGBA(color), hideFlags = HideFlags.DontSave };
            SetUnderlay(m, color, 0f, 0f, 0.35f, 0.9f);
            s_glows[key] = m;
            return m;
        }

        /// <summary>Writes the soft dark shadow underlay used for HUD text into a material (builders use it for the saved assets).</summary>
        public static void ConfigureShadow(Material m) => SetUnderlay(m, new Color(0f, 0f, 0f, 0.72f), 0.10f, -0.14f, 0.22f, 0.55f);

        /// <summary>Writes the neutral glow underlay used as the glow base (builders use it for the saved assets).</summary>
        public static void ConfigureGlow(Material m) => SetUnderlay(m, new Color(1f, 1f, 1f, 0.35f), 0f, 0f, 0.35f, 0.9f);

        /// <summary>Writes an underlay into a TMP SDF material and enables the keyword.</summary>
        public static void SetUnderlay(Material m, Color color, float offsetX, float offsetY, float dilate, float softness)
        {
            if (m == null) return;
            m.EnableKeyword(UnderlayKeyword);
            if (m.HasProperty(s_underlayColor)) m.SetColor(s_underlayColor, color);
            if (m.HasProperty(s_underlayOffsetX)) m.SetFloat(s_underlayOffsetX, offsetX);
            if (m.HasProperty(s_underlayOffsetY)) m.SetFloat(s_underlayOffsetY, offsetY);
            if (m.HasProperty(s_underlayDilate)) m.SetFloat(s_underlayDilate, dilate);
            if (m.HasProperty(s_underlaySoftness)) m.SetFloat(s_underlaySoftness, softness);
        }

        /// <summary>Manifest id that supplies a role.</summary>
        public static string ManifestIdOf(FontRole role)
        {
            switch (role)
            {
                case FontRole.Typewriter:
                case FontRole.TypewriterClean: return "UI_FONT_TYPEWRITER";
                case FontRole.Condensed:
                case FontRole.CondensedMedium: return "UI_FONT_CONDENSED";
                case FontRole.Mono: return "UI_FONT_MONO";
                case FontRole.Sardani: return "UI_FONT_SARDANI";
                default: return "UI_FONT_FALLBACK";
            }
        }

        private static Material ShadowFor(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return null;
            if (s_shadows.TryGetValue(font, out var m) && m != null) return m;
            m = new Material(font.material) { name = font.name + " Shadow (runtime)", hideFlags = HideFlags.DontSave };
            ConfigureShadow(m);
            s_shadows[font] = m;
            return m;
        }

        private static TMP_FontAsset FirstRelated(UITheme theme, FontRole role)
        {
            switch (role)
            {
                case FontRole.Typewriter: return First(theme, FontRole.TypewriterClean, FontRole.Mono);
                case FontRole.TypewriterClean: return First(theme, FontRole.Typewriter, FontRole.Mono);
                case FontRole.Condensed: return First(theme, FontRole.CondensedMedium, FontRole.Fallback);
                case FontRole.CondensedMedium: return First(theme, FontRole.Condensed, FontRole.Fallback);
                case FontRole.Mono: return First(theme, FontRole.TypewriterClean, FontRole.Typewriter);
                default: return theme.GetFont(FontRole.Fallback);
            }
        }

        private static TMP_FontAsset First(UITheme theme, FontRole a, FontRole b)
        {
            var f = theme.GetFont(a);
            return f != null ? f : theme.GetFont(b);
        }

        private readonly struct GlowKey : IEquatable<GlowKey>
        {
            private readonly TMP_FontAsset _font;
            private readonly uint _color;

            public GlowKey(TMP_FontAsset font, uint color)
            {
                _font = font;
                _color = color;
            }

            public bool Equals(GlowKey other) => ReferenceEquals(_font, other._font) && _color == other._color;
            public override bool Equals(object obj) => obj is GlowKey k && Equals(k);
            public override int GetHashCode() => ((_font != null ? _font.GetHashCode() : 0) * 397) ^ (int)_color;
        }
    }
}
