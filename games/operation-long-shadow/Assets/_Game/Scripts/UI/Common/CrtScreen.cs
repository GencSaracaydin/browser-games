using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// 1984 CRT treatment for a UI panel (click gate, pause and options panels, debrief): tiled scanlines, a dark
    /// vignette, a slow rolling refresh band and phosphor flicker on the panel's CanvasGroup. Unscaled time, so it keeps
    /// running while the game is paused. Allocation-free per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrtScreen : MonoBehaviour
    {
        /// <summary>Scanline tile height in reference pixels (one dark + one clear band).</summary>
        public const float ScanlineTile = 3f;

        [SerializeField] private RawImage _scanlines;
        [SerializeField] private RawImage _vignette;
        [SerializeField] private Image _rollBar;
        [SerializeField] private CanvasGroup _group;
        [SerializeField, Range(0f, 1f)] private float _flicker = 0.35f;
        [SerializeField] private float _rollSpeed = 0.07f;

        private RectTransform _rect;
        private Vector2 _lastSize;
        private float _baseAlpha = 1f;

        /// <summary>Panel opacity before flicker (fades multiply with it).</summary>
        public float BaseAlpha
        {
            get => _baseAlpha;
            set => _baseAlpha = Mathf.Clamp01(value);
        }

        /// <summary>The CanvasGroup the flicker drives.</summary>
        public CanvasGroup Group => _group;

        /// <summary>
        /// Adds the CRT layers on top of <paramref name="target"/>'s children (call after the panel content is built so the
        /// overlays draw over it) and returns the component.
        /// </summary>
        public static CrtScreen Add(RectTransform target, UITheme theme, float scanlineStrength, float flicker, bool vignette, bool rollBar)
        {
            var crt = target.gameObject.GetComponent<CrtScreen>();
            if (crt == null) crt = target.gameObject.AddComponent<CrtScreen>();
            crt._rect = target;
            crt._group = UIFactory.Group(target.gameObject, 1f, target.GetComponent<CanvasGroup>() != null && target.GetComponent<CanvasGroup>().interactable);
            crt._flicker = flicker;
            var overlays = UIFactory.Stretch("CRT", target);
            crt._scanlines = UIFactory.StretchRaw("Scanlines", overlays, UISprites.Get(UITextureId.Scanlines, theme), new Color(1f, 1f, 1f, Mathf.Clamp01(scanlineStrength)));
            if (vignette)
                crt._vignette = UIFactory.StretchRaw("Vignette", overlays, UISprites.Get(UITextureId.Vignette, theme), new Color(1f, 1f, 1f, 0.85f));
            if (rollBar)
            {
                crt._rollBar = UIFactory.Image("RollBar", overlays, UISprites.Get(UISpriteId.SoftDisc, theme), new Color(0.6f, 1f, 0.6f, 0.035f),
                    UIFactory.TopCenter, UIFactory.Center, Vector2.zero, new Vector2(100f, 140f));
                var rt = crt._rollBar.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.sizeDelta = new Vector2(0f, 160f);
            }
            crt.UpdateTiling(true);
            return crt;
        }

        private void Awake()
        {
            if (_rect == null) _rect = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            UpdateTiling(false);
            var t = Time.unscaledTime;
            if (_group != null) _group.alpha = _baseAlpha * UIAnim.Flicker(t, _flicker);
            if (_rollBar != null && _rect != null)
            {
                var h = _rect.rect.height + 320f;
                var y = -Mathf.Repeat(t * _rollSpeed, 1f) * h + 160f;
                _rollBar.rectTransform.anchoredPosition = new Vector2(0f, y);
            }
        }

        private void UpdateTiling(bool force)
        {
            if (_rect == null || _scanlines == null) return;
            var size = _rect.rect.size;
            if (!force && size == _lastSize) return;
            _lastSize = size;
            _scanlines.uvRect = new Rect(0f, 0f, Mathf.Max(1f, size.x / ScanlineTile), Mathf.Max(1f, size.y / ScanlineTile));
        }
    }
}
