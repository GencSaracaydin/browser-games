using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Door-gun overlay (GDD §11, §13.3, Beat 11): ring sight and a heat bar that goes white → red with
    /// <see cref="IWeaponHudState.Heat01"/>, <c>OVERHEAT</c> flashing while overheated. WP03 sets it with
    /// <see cref="IHud.SetOverlay"/> on mount.
    /// </summary>
    public sealed class OverlayDoorGun : HudView
    {
        private CanvasGroup _group;
        private Image _fill;
        private TextMeshProUGUI _warning;
        private float _alpha;
        private float _heat;

        /// <summary>Heat shown (tests).</summary>
        public float Heat => _heat;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("DoorGun", parent);
            _group = UIFactory.Group(Root.gameObject, 0f);
            var ring = UIFactory.Image("Ring", Root, Ctx.Sprite(UISpriteId.DoorGunRing), new Color(0.91f, 0.91f, 0.91f, 0.8f),
                UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(520f, 520f));
            ring.preserveAspect = true;
            UIFactory.Image("Pip", Root, Ctx.Sprite(UISpriteId.Disc), new Color(0.91f, 0.91f, 0.91f, 0.9f), UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(5f, 5f));

            var bar = UIFactory.Rect("HeatBar", Root, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, HudLayout.PromptBottom), new Vector2(380f, 30f));
            var frame = UIFactory.StretchImage("Frame", bar, Ctx.Sprite(UISpriteId.HeatBarFrame), UIColors.WithAlpha(UIColors.HeatCold, 0.85f));
            frame.type = Image.Type.Sliced;
            _fill = UIFactory.StretchImage("Fill", bar, Ctx.Sprite(UISpriteId.Square), UIColors.HeatCold, 6f);
            UIFactory.MakeHorizontalFill(_fill);
            var label = UIFactory.Text("Label", bar, Ctx.Fonts, FontRole.Condensed, 17f, UIColors.InkDim, TextAlignmentOptions.MidlineRight,
                UIFactory.MiddleLeft, UIFactory.MiddleRight, new Vector2(-12f, 0f), new Vector2(80f, 24f));
            label.characterSpacing = 6f;
            label.SetText("HEAT");
            _warning = UIFactory.Text("Overheat", bar, Ctx.Fonts, FontRole.Condensed, 24f, UIColors.Red, TextAlignmentOptions.Bottom,
                UIFactory.TopCenter, UIFactory.BottomCenter, new Vector2(0f, 8f), new Vector2(300f, 30f));
            _warning.characterSpacing = 10f;
            _warning.SetText("OVERHEAT");
            _warning.enabled = false;
            Root.gameObject.SetActive(false);
        }

        public override void Tick(float dt)
        {
            var on = Ctx.Overlay == HudOverlay.DoorGun && Ctx.GameplayVisible;
            if (on && !Root.gameObject.activeSelf) Root.gameObject.SetActive(true);
            _alpha = UIAnim.Approach(_alpha, on ? 1f : 0f, 8f, dt);
            _group.alpha = _alpha;
            if (!on && _alpha <= 0f)
            {
                if (Root.gameObject.activeSelf) Root.gameObject.SetActive(false);
                return;
            }
            var w = Ctx.Weapon.Value;
            var heat = w != null ? Mathf.Clamp01(w.Heat01) : 0f;
            var overheated = w != null && w.Overheated;
            _heat = UIAnim.Damp(_heat, heat, 16f, dt);
            _fill.fillAmount = _heat;
            _fill.color = Color.Lerp(UIColors.HeatCold, UIColors.Red, Mathf.SmoothStep(0.35f, 1f, _heat));
            var flash = overheated && UIAnim.Blink(Ctx.Time, 0.36f);
            if (_warning.enabled != flash) _warning.enabled = flash;
        }
    }
}
