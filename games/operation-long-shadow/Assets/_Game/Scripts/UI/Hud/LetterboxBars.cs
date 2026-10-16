using LongShadow.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Letterbox bars (<see cref="LongShadow.Core.IHud.Letterbox"/>, the title card): 2.39:1 (CINEMATIC_DIRECTION §1, which
    /// overrides the GDD's 2.35 so the title-card bars match the insertion's), animated over the given seconds with an
    /// ease. The bar sprite has a soft inner edge (UI_LETTERBOX); the bottom bar is flipped.
    /// </summary>
    public sealed class LetterboxBars : HudView
    {
        private RectTransform _top;
        private RectTransform _bottom;
        private float _from, _to = 0f, _t = 1f, _duration = 0.5f;
        private float _amount;

        /// <summary>Current coverage 0 (open) .. 1 (full 2.39:1) (tests).</summary>
        public float Amount => _amount;

        /// <summary>Requested state.</summary>
        public bool On => _to > 0.5f;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("Letterbox", parent);
            var sprite = Ctx.Sprite(UISpriteId.LetterboxBar);
            var top = UIFactory.Image("Top", Root, sprite, Color.white, UIFactory.TopCenter, UIFactory.TopCenter, Vector2.zero, new Vector2(0f, 0f));
            top.type = Image.Type.Sliced;
            _top = top.rectTransform;
            _top.anchorMin = new Vector2(0f, 1f);
            _top.anchorMax = new Vector2(1f, 1f);
            var bottom = UIFactory.Image("Bottom", Root, sprite, Color.white, UIFactory.BottomCenter, UIFactory.TopCenter, Vector2.zero, new Vector2(0f, 0f));
            bottom.type = Image.Type.Sliced;
            _bottom = bottom.rectTransform;
            _bottom.anchorMin = new Vector2(0f, 0f);
            _bottom.anchorMax = new Vector2(1f, 0f);
            _bottom.localScale = new Vector3(1f, -1f, 1f); // soft edge faces the picture
            Apply(0f);
        }

        /// <summary>Slides the bars in or out over seconds.</summary>
        public void Set(bool on, float seconds)
        {
            _from = _amount;
            _to = on ? 1f : 0f;
            _duration = Mathf.Max(0f, seconds);
            _t = 0f;
            if (_duration <= 0f) Apply(_to);
        }

        public override void ResetState() => Set(false, 0f);

        public override void Tick(float dt)
        {
            if (_t >= 1f && Mathf.Approximately(_amount, _to)) return;
            _t = _duration > 0f ? Mathf.Min(1f, _t + dt / _duration) : 1f;
            Apply(Mathf.Lerp(_from, _to, UIAnim.InOutCubic(_t)));
        }

        public override void Relayout(Vector2 canvasSize) => Apply(_amount);

        private void Apply(float amount)
        {
            _amount = amount;
            var size = Ctx.CanvasSize;
            var h = HudMath.LetterboxBarHeight(size.x, size.y, HudLayout.LetterboxAspect) * amount;
            var on = h > 0.5f;
            if (_top.gameObject.activeSelf != on)
            {
                _top.gameObject.SetActive(on);
                _bottom.gameObject.SetActive(on);
            }
            _top.sizeDelta = new Vector2(0f, h + 6f);
            _top.anchoredPosition = Vector2.zero;
            _bottom.sizeDelta = new Vector2(0f, h + 6f);
            _bottom.anchoredPosition = Vector2.zero;
        }
    }
}
