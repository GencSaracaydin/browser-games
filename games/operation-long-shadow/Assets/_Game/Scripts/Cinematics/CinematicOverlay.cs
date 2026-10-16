using System.Text;
using LongShadow.Cinematics.FX;
using LongShadow.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// The full-screen overlay canvas of the cinematics and screen FX, built at runtime (no prefab): camera-feed
    /// cross-dissolves (MID-B I3), the film burn-out (MID-B shot 8), glare (Beat 6 floods), static and tear (glitch
    /// fallback, death insert), fades to black or white, 2.39:1 letterbox bars (CINEMATIC_DIRECTION §1.1), typed text
    /// (intro shot 1, "SESSION 12 — RESUMING", the title card fallback) and the "HOLD [SPACE] TO SKIP" prompt. Sorts above
    /// the HUD. Text uses LIB_Cinematics' typewriter font, TMP's default font, or the legacy font as a last resort.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Cinematics - 30)]
    [DisallowMultipleComponent]
    public sealed class CinematicOverlay : MonoBehaviour
    {
        /// <summary>Canvas sort order (above WP08's HUD and menus).</summary>
        public const int SortOrder = 32000;
        /// <summary>Letterbox aspect (CINEMATIC_DIRECTION §1.1: 2.39:1).</summary>
        public const float LetterboxAspect = 2.39f;
        /// <summary>Amber of the title type (GDD §8.3).</summary>
        public static readonly Color Amber = new Color(1f, 0.72f, 0.28f);
        /// <summary>Teletype white-green of the booth type.</summary>
        public static readonly Color Phosphor = new Color(0.86f, 0.95f, 0.86f);

        private RectTransform _root;
        private RawImage _feed;
        private RawImage _burn;
        private Image _glare;
        private RawImage _static;
        private Image _fade;
        private Image _barTop;
        private Image _barBottom;
        private Label _center;
        private Label _lower;
        private Label _title;
        private Label _skipLabel;
        private Image _skipFill;
        private GameObject _skipRoot;
        private Material _burnMat;
        private Material _staticMat;
        private float _letterbox;
        private Color _fadeColor = Color.black;
        private float _fadeAlpha;

        /// <summary>Current fade alpha.</summary>
        public float FadeAlpha => _fadeAlpha;
        /// <summary>True when the screen is fully covered by black.</summary>
        public bool IsBlack => _fadeAlpha >= 0.995f && _fadeColor.r + _fadeColor.g + _fadeColor.b < 0.05f;
        /// <summary>True when the screen is fully covered by white.</summary>
        public bool IsWhite => _fadeAlpha >= 0.995f && _fadeColor.r + _fadeColor.g + _fadeColor.b > 2.9f;
        /// <summary>Current letterbox amount 0..1.</summary>
        public float Letterbox => _letterbox;

        private void Awake()
        {
            CinematicRuntime.Set(this);
            Build();
        }

        private void OnDestroy()
        {
            CinematicRuntime.Clear(this);
            if (_burnMat != null) Destroy(_burnMat);
            if (_staticMat != null) Destroy(_staticMat);
        }

        // ------------------------------------------------------------------ API

        /// <summary>Full-screen fade colour and alpha.</summary>
        public void SetFade(Color color, float alpha)
        {
            _fadeColor = color;
            _fadeAlpha = Mathf.Clamp01(alpha);
            color.a = _fadeAlpha;
            _fade.color = color;
            SetActive(_fade, _fadeAlpha > 0.001f);
        }

        /// <summary>Letterbox bars 0 (off) .. 1 (2.39:1).</summary>
        public void SetLetterbox(float amount01)
        {
            _letterbox = Mathf.Clamp01(amount01);
            var on = _letterbox > 0.001f;
            SetActive(_barTop, on);
            SetActive(_barBottom, on);
            if (!on) return;
            var screenAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            var bar = Mathf.Max(0f, (1f - screenAspect / LetterboxAspect) * 0.5f) * Easing.Evaluate(ShotEase.Smooth, _letterbox);
            _barTop.rectTransform.anchorMin = new Vector2(0f, 1f - bar);
            _barBottom.rectTransform.anchorMax = new Vector2(1f, bar);
        }

        /// <summary>A camera feed over the picture (cross-dissolve); null or alpha 0 hides it.</summary>
        public void SetFeed(Texture texture, float alpha)
        {
            var on = texture != null && alpha > 0.001f;
            SetActive(_feed, on);
            if (!on) return;
            _feed.texture = texture;
            _feed.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        /// <summary>Film burn-out 0 (off) .. 1 (white) through SG_FilmBurn.</summary>
        public void SetBurn(float t01)
        {
            var on = t01 > 0.001f && _burnMat != null;
            SetActive(_burn, on);
            if (on) _burnMat.SetFloat(ShaderIds.Burn, Mathf.Clamp01(t01));
        }

        /// <summary>Warm glare over the picture (IScreenFX.Glare, Beat 6 floodlights).</summary>
        public void SetGlare(float amount01)
        {
            var a = Mathf.Clamp01(amount01) * 0.6f;
            SetActive(_glare, a > 0.002f);
            _glare.color = new Color(1f, 0.93f, 0.8f, a);
        }

        /// <summary>Static/tear overlay (glitch fallback, death insert static).</summary>
        public void SetStatic(float intensity, float tear, float white = 0f)
        {
            var on = (intensity > 0.001f || white > 0.001f) && _staticMat != null;
            SetActive(_static, on);
            if (!on) return;
            _staticMat.SetFloat(ShaderIds.Intensity, Mathf.Clamp01(intensity));
            _staticMat.SetFloat(ShaderIds.Tear, Mathf.Clamp01(tear));
            _staticMat.SetFloat(ShaderIds.White, Mathf.Clamp01(white));
            _staticMat.SetFloat(ShaderIds.Seed, Random.value * 100f);
        }

        /// <summary>Centre text (intro shot 1 slate); null hides it.</summary>
        public void SetCenterText(StringBuilder text, Color color) => _center.Set(text, color);

        /// <summary>Lower-third text ("SESSION 12 — RESUMING"); null hides it.</summary>
        public void SetLowerText(StringBuilder text, Color color) => _lower.Set(text, color);

        /// <summary>Title-card text block (fallback when WP08's HUD is missing); null hides it.</summary>
        public void SetTitleText(StringBuilder text, Color color) => _title.Set(text, color);

        /// <summary>The hold-to-skip prompt with its fill 0..1.</summary>
        public void SetSkipPrompt(bool visible, float progress01)
        {
            if (_skipRoot.activeSelf != visible) _skipRoot.SetActive(visible);
            if (visible) _skipFill.fillAmount = Mathf.Clamp01(progress01);
        }

        /// <summary>Clears every layer (no fade, no bars, no text).</summary>
        public void ClearAll()
        {
            SetFade(Color.black, 0f);
            SetLetterbox(0f);
            SetFeed(null, 0f);
            SetBurn(0f);
            SetGlare(0f);
            SetStatic(0f, 0f);
            _center.Set(null, Color.white);
            _lower.Set(null, Color.white);
            _title.Set(null, Color.white);
            SetSkipPrompt(false, 0f);
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            var canvasGo = new GameObject("CinematicOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _root = canvasGo.GetComponent<RectTransform>();

            _feed = Raw("Feed", null);
            _burnMat = MakeMaterial(CinematicAssetIds.MatFilmBurn, ShaderIds.FilmBurnShader);
            _burn = Raw("Burn", _burnMat);
            _glare = Img("Glare", Color.clear);
            _staticMat = MakeMaterial(CinematicAssetIds.MatGlitchOverlay, ShaderIds.GlitchOverlayShader);
            _static = Raw("Static", _staticMat);
            _static.texture = Texture2D.whiteTexture;
            _fade = Img("Fade", Color.clear);
            _barTop = Img("LetterboxTop", Color.black);
            _barBottom = Img("LetterboxBottom", Color.black);
            _barTop.rectTransform.anchorMin = new Vector2(0f, 1f);
            _barBottom.rectTransform.anchorMax = new Vector2(1f, 0f);

            var font = CinematicFonts.Get(CinematicAssetIds.FontTypewriter);
            _center = Label.Create(_root, "CenterText", font, 44f, TextAlignmentOptions.Center, new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f));
            _lower = Label.Create(_root, "LowerText", font, 36f, TextAlignmentOptions.Center, new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.2f));
            _title = Label.Create(_root, "TitleText", font, 40f, TextAlignmentOptions.BottomLeft, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.46f));

            _skipRoot = new GameObject("SkipPrompt", typeof(RectTransform));
            _skipRoot.transform.SetParent(_root, false);
            var srt = _skipRoot.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.74f, 0.035f);
            srt.anchorMax = new Vector2(0.97f, 0.075f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            _skipLabel = Label.Create(srt, "Label", CinematicFonts.Get(CinematicAssetIds.FontMono), 22f,
                TextAlignmentOptions.Right, new Vector2(0f, 0.25f), new Vector2(1f, 1f));
            var sb = new StringBuilder("HOLD [SPACE] TO SKIP");
            _skipLabel.Set(sb, new Color(1f, 1f, 1f, 0.75f));
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(srt, false);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0.35f, 0f);
            frt.anchorMax = new Vector2(1f, 0.12f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            _skipFill = fillGo.AddComponent<Image>();
            _skipFill.sprite = Placeholders.WhiteSprite;
            _skipFill.type = Image.Type.Filled;
            _skipFill.fillMethod = Image.FillMethod.Horizontal;
            _skipFill.color = new Color(1f, 1f, 1f, 0.8f);
            _skipFill.raycastTarget = false;

            ClearAll();
        }

        private RawImage Raw(string name, Material mat)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            Stretch(go.GetComponent<RectTransform>());
            var r = go.AddComponent<RawImage>();
            r.raycastTarget = false;
            if (mat != null) r.material = mat;
            return r;
        }

        private Image Img(string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            Stretch(go.GetComponent<RectTransform>());
            var i = go.AddComponent<Image>();
            i.raycastTarget = false;
            i.color = c;
            return i;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private Material MakeMaterial(string libraryId, string shaderName)
        {
            var template = CinematicLibrary.Lookup<Material>(libraryId);
            if (template != null) return new Material(template) { name = template.name + "_Runtime" };
            var shader = Shader.Find(shaderName);
            if (shader != null) return new Material(shader) { name = libraryId + "_Runtime" };
            LSLog.MissingAsset(LogCategory.Cinematics, shaderName, "Assets/_Game/Art/Shaders", this);
            return null;
        }

        private static void SetActive(Graphic g, bool on)
        {
            if (g != null && g.gameObject.activeSelf != on) g.gameObject.SetActive(on);
        }

        /// <summary>A text element backed by TMP or, without any TMP font, the legacy UI Text.</summary>
        private sealed class Label
        {
            private TextMeshProUGUI _tmp;
            private Text _legacy;
            private GameObject _go;

            public static Label Create(RectTransform parent, string name, TMP_FontAsset font, float size,
                                       TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax)
            {
                var l = new Label { _go = new GameObject(name, typeof(RectTransform)) };
                l._go.transform.SetParent(parent, false);
                var rt = l._go.GetComponent<RectTransform>();
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                if (font != null)
                {
                    l._tmp = l._go.AddComponent<TextMeshProUGUI>();
                    l._tmp.font = font;
                    l._tmp.fontSize = size;
                    l._tmp.alignment = align;
                    l._tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    l._tmp.richText = false;
                    l._tmp.raycastTarget = false;
                }
                else
                {
                    l._legacy = l._go.AddComponent<Text>();
                    l._legacy.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    l._legacy.fontSize = Mathf.RoundToInt(size);
                    l._legacy.alignment = align == TextAlignmentOptions.BottomLeft ? TextAnchor.LowerLeft :
                        align == TextAlignmentOptions.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
                    l._legacy.raycastTarget = false;
                    l._legacy.horizontalOverflow = HorizontalWrapMode.Overflow;
                    l._legacy.verticalOverflow = VerticalWrapMode.Overflow;
                }
                l._go.SetActive(false);
                return l;
            }

            public void Set(StringBuilder text, Color color)
            {
                var on = text != null && text.Length > 0;
                if (_go.activeSelf != on) _go.SetActive(on);
                if (!on) return;
                if (_tmp != null)
                {
                    _tmp.color = color;
                    _tmp.SetText(text);
                }
                else if (_legacy != null)
                {
                    _legacy.color = color;
                    _legacy.text = text.ToString();
                }
            }
        }
    }
}
