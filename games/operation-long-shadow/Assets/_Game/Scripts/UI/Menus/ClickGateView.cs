using System;
using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LongShadow.UI.Menus
{
    /// <summary>
    /// The click gate (GDD §8.1; <see cref="IClickGate"/>, WP01 request): a black CRT with a green phosphor terminal line
    /// <c>SESSION 12 // SUBJECT S-3 // CLICK TO BEGIN</c> and a block cursor, the loading progress as a row of five-digit
    /// number groups that fill in as the bundles arrive, and the small line
    /// <c>HEADPHONES RECOMMENDED · MOUSE + KEYBOARD · ESC PAUSES</c>. Lives in S_Boot (PF_ClickGate): it references only
    /// its own font, glow material and two small sprites, so no theme or heavy art lands in the boot data. The click is
    /// raised on the pointer-down (or Enter/Space) inside the browser gesture; WP01's Bootstrap has armed the page to
    /// lock the pointer, resume audio and go fullscreen in that same gesture, so this view never requests them itself.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform - 50)]
    [DisallowMultipleComponent]
    public sealed class ClickGateView : MonoBehaviour, IClickGate
    {
        private const int Groups = 10;
        private const float GroupScramble = 0.28f;
        private const float TypeCps = 42f;

        [Header("Assets (PF_ClickGate; all optional)")]
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private Material _glowMaterial;
        [SerializeField] private Sprite _background;
        [SerializeField] private Sprite _cursor;
        [SerializeField] private Texture2D _scanlines;

        [Header("Text (GDD §8.1)")]
        [SerializeField] private string _session = "SESSION 12 // SUBJECT S-3 // ";
        [SerializeField] private string _readyText = "CLICK TO BEGIN";
        [SerializeField] private string _loadingText = "ESTABLISHING LINK";
        [SerializeField] private string _footer = "HEADPHONES RECOMMENDED · MOUSE + KEYBOARD · ESC PAUSES";
        [SerializeField] private int _sortingOrder = 100;

        private Canvas _canvas;
        private TextMeshProUGUI _line;
        private TextMeshProUGUI _groups;
        private TextMeshProUGUI _footerText;
        private RectTransform _cursorRect;
        private Image _cursorImage;
        private ClickCatcher _catcher;
        private readonly TextBuffer _lineText = new TextBuffer(96);
        private readonly TextBuffer _groupText = new TextBuffer(96);
        private readonly char[] _digits = new char[Groups * NumberGroups.GroupSize];
        private readonly float[] _groupAge = new float[Groups];
        private uint _scrambleSeed = 0x5EED1984u;
        private float _progress;
        private bool _ready;
        private bool _visible = true;
        private float _readyAge;
        private int _typed = -1;
        private int _filledShown = -1;
        private bool _built;
        private bool _clickedThisShow;

        /// <inheritdoc/>
        public event Action Clicked;

        /// <summary>True while ready for the click.</summary>
        public bool Ready => _ready;

        /// <summary>True while shown.</summary>
        public bool Visible => _visible;

        /// <summary>Groups filled (tests).</summary>
        public int FilledGroups => _filledShown;

        private void Awake()
        {
            Build();
            GameServices.Register<IClickGate>(this);
            UiEventSystem.Ensure(transform);
        }

        private void OnDestroy()
        {
            GameServices.Unregister<IClickGate>(this);
        }

        /// <inheritdoc/>
        public void SetProgress(float progress01) => _progress = Mathf.Clamp01(progress01);

        /// <inheritdoc/>
        public void SetReady(bool ready)
        {
            var becameReady = ready && !_ready;
            if (becameReady)
            {
                _readyAge = 0f;
                _typed = -1;
                _clickedThisShow = false;
            }
            _ready = ready;
            if (becameReady && _visible) GameEvents.RaiseMissionEvent(UiEvents.GateReady);
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (!_built) Build();
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
            if (visible)
            {
                _clickedThisShow = false;
                _typed = -1;
                _filledShown = -1;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Build()
        {
            if (_built) return;
            _built = true;
            var font = _font != null ? _font : UIFonts.Fallback;
            var glow = _glowMaterial;
            if (glow == null && font != null && font.material != null)
            {
                glow = new Material(font.material) { name = "ClickGate Glow (runtime)", hideFlags = HideFlags.DontSave };
                UIFonts.SetUnderlay(glow, UIColors.WithAlpha(UIColors.Phosphor, 0.32f), 0f, 0f, 0.35f, 0.9f);
            }

            var go = new GameObject("ClickGateCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.layer = 5;
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudMath.Reference;
            scaler.matchWidthOrHeight = HudMath.Match;
            go.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)go.transform;

            var bg = UIFactory.StretchImage("Background", root, _background != null ? _background : UISprites.Get(UISpriteId.ClickGateBG, null), Color.white);
            bg.type = Image.Type.Simple;
            bg.color = _background != null ? Color.white : UIColors.CrtBlack;

            var block = UIFactory.Rect("Terminal", root, UIFactory.Center, UIFactory.Center, new Vector2(0f, 30f), new Vector2(1100f, 220f));
            _line = MakeText("Line", block, font, glow, 30f, UIColors.Phosphor, new Vector2(0f, 0f));
            _groups = MakeText("Groups", block, font, glow, 26f, UIColors.Phosphor, new Vector2(0f, -86f));
            _footerText = UIFactory.Text("Footer", root, null, FontRole.TypewriterClean, 18f, UIColors.WithAlpha(UIColors.Phosphor, 0.55f),
                TextAlignmentOptions.Bottom, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 64f), new Vector2(1400f, 30f), false);
            if (font != null) _footerText.font = font;
            _footerText.characterSpacing = 3f;
            _footerText.SetText(_footer);

            _cursorImage = UIFactory.Image("Cursor", block, _cursor != null ? _cursor : UISprites.Get(UISpriteId.CrtCursor, null), Color.white,
                UIFactory.TopLeft, UIFactory.TopLeft, Vector2.zero, new Vector2(16f, 26f));
            _cursorRect = _cursorImage.rectTransform;

            var crt = CrtScreen.Add(root, null, 0.55f, 0.4f, true, true);
            if (_scanlines != null)
            {
                var raw = crt.GetComponentInChildren<RawImage>();
                if (raw != null) raw.texture = _scanlines;
            }

            _catcher = ClickCatcher.Create("Catcher", root);
            _catcher.Down += OnPointerDown;

            var seed = NumberGroups.Seed("SESSION 12");
            for (var i = 0; i < _digits.Length; i++) _digits[i] = NumberGroups.Digit(ref seed);
            // The first two groups are the teletype's (GDD §8.2 shot 5d: "74410 00640").
            "7441000640".CopyTo(0, _digits, 0, 10);
            for (var i = 0; i < Groups; i++) _groupAge[i] = 99f;
            _canvas.gameObject.SetActive(_visible);
        }

        private static TextMeshProUGUI MakeText(string name, RectTransform parent, TMP_FontAsset font, Material glow, float size, Color color, Vector2 pos)
        {
            var t = UIFactory.Text(name, parent, null, FontRole.TypewriterClean, size, color, TextAlignmentOptions.TopLeft,
                UIFactory.TopLeft, UIFactory.TopLeft, pos, new Vector2(1100f, 44f), false);
            if (font != null) t.font = font;
            if (glow != null) t.fontSharedMaterial = glow;
            return t;
        }

        /// <summary>Test hook: builds the view without Awake (edit mode).</summary>
        internal void EnsureBuilt() => Build();

        /// <summary>Test hook: a pointer-down on the gate.</summary>
        internal void SimulatePointerDown() => OnPointerDown();

        private void OnPointerDown()
        {
            if (!_ready || !_visible || _clickedThisShow) return;
            _clickedThisShow = true;
            Clicked?.Invoke();
        }

        private void Update()
        {
            if (!_visible || !_built) return;
            var dt = Time.unscaledDeltaTime;
            var t = Time.unscaledTime;

            // Keyboard start (Enter / Space), also a browser gesture.
            if (_ready)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                    OnPointerDown();
            }

            UpdateGroups(dt);
            UpdateLine(dt, t);
        }

        private void UpdateGroups(float dt)
        {
            var filled = Mathf.Clamp(Mathf.FloorToInt(_progress * Groups + 0.0001f), 0, Groups);
            if (_ready) filled = Groups;
            var scrambling = false;
            for (var i = 0; i < Groups; i++)
            {
                if (i < filled && _groupAge[i] >= 99f) _groupAge[i] = 0f;
                if (_groupAge[i] < GroupScramble)
                {
                    _groupAge[i] += dt;
                    scrambling = true;
                }
            }
            if (!scrambling && filled == _filledShown) return;
            _filledShown = filled;
            _groupText.Clear();
            for (var g = 0; g < Groups; g++)
            {
                if (g > 0) _groupText.Append(' ');
                if (g >= filled)
                {
                    _groupText.OpenColor(UIColors.PhosphorDim).Append('·', NumberGroups.GroupSize).CloseColor();
                    continue;
                }
                var settling = _groupAge[g] < GroupScramble;
                for (var d = 0; d < NumberGroups.GroupSize; d++)
                    _groupText.Append(settling ? NumberGroups.Digit(ref _scrambleSeed) : _digits[g * NumberGroups.GroupSize + d]);
            }
            _groupText.ApplyTo(_groups);
        }

        private void UpdateLine(float dt, float t)
        {
            var tail = _ready ? _readyText : _loadingText;
            int typed;
            if (_ready)
            {
                _readyAge += dt;
                typed = Mathf.Min(tail.Length, Mathf.FloorToInt(_readyAge * TypeCps));
            }
            else typed = tail.Length;
            var key = typed + (_ready ? 1000 : 0);
            if (key != _typed)
            {
                _typed = key;
                _lineText.Clear().Append(_session).Append(tail, 0, typed);
                _lineText.ApplyTo(_line);
                _line.ForceMeshUpdate();
                var info = _line.textInfo;
                var x = 0f;
                if (info != null && info.characterCount > 0)
                {
                    var last = info.characterInfo[info.characterCount - 1];
                    x = last.xAdvance;
                }
                _cursorRect.anchoredPosition = new Vector2(x + 6f, -6f);
            }
            var blinkOn = !_ready || typed < tail.Length || UIAnim.Blink(t, 1.06f); // T_UI_CrtCursor note: 0.53 s on / 0.53 s off
            if (_cursorImage.enabled != blinkOn) _cursorImage.enabled = blinkOn;
            if (!_ready) _line.alpha = 0.75f + 0.25f * Mathf.Sin(t * 4f);
            else if (_line.alpha < 1f) _line.alpha = 1f;
        }
    }
}
