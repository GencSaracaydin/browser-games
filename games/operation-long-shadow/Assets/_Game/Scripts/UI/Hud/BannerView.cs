using System.Collections.Generic;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Centre banner (<see cref="LongShadow.Core.IHud.Banner"/>): the out-of-bounds countdown
    /// <c>RETURN TO THE MISSION AREA 6…</c> (GDD §17; WP02 calls it once per second with 1.15 s, and with an empty text
    /// to clear). Typewriter face in warning amber on a dark plate.
    /// </summary>
    public sealed class BannerView : HudView
    {
        private CanvasGroup _group;
        private RectTransform _plate;
        private TextMeshProUGUI _text;
        private string _shown;
        private float _remaining;
        private float _alpha;

        /// <summary>True while a banner is up (tests).</summary>
        public bool Showing => _remaining > 0f;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Rect("Banner", parent, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -HudLayout.BannerTop), HudLayout.Banner);
            _group = UIFactory.Group(Root.gameObject, 0f);
            _plate = UIFactory.Rect("Plate", Root, UIFactory.Center, UIFactory.Center, Vector2.zero, HudLayout.Banner);
            var plate = UIFactory.StretchImage("Image", _plate, Ctx.Sprite(UISpriteId.SoftPanel), new Color(0f, 0f, 0f, 0.5f));
            plate.type = Image.Type.Sliced;
            _text = UIFactory.Text("Text", Root, Ctx.Fonts, FontRole.Typewriter, 33f, UIColors.AmberPhosphor, TextAlignmentOptions.Center,
                UIFactory.Center, UIFactory.Center, Vector2.zero, HudLayout.Banner);
            _text.characterSpacing = 3f;
        }

        /// <summary>Shows text for seconds; empty text or 0 s clears.</summary>
        public void Show(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text) || seconds <= 0f)
            {
                _remaining = 0f;
                return;
            }
            if (!string.Equals(text, _shown))
            {
                _shown = text;
                _text.SetText(text);
                var pref = _text.GetPreferredValues();
                _plate.sizeDelta = new Vector2(Mathf.Min(HudLayout.Banner.x, pref.x + 80f), HudLayout.Banner.y);
            }
            _remaining = seconds;
        }

        public override void ResetState() => _remaining = 0f;

        public override void Tick(float dt)
        {
            if (_remaining > 0f) _remaining -= dt;
            _alpha = UIAnim.Approach(_alpha, _remaining > 0f ? 1f : 0f, _remaining > 0f ? 12f : 5f, dt);
            _group.alpha = _alpha;
        }

        public override void CollectLayout(List<HudLayoutRegion> regions)
        {
            regions.Add(HudLayoutRegion.Of("Banner", Root, Ctx.CanvasRect));
        }
    }
}
