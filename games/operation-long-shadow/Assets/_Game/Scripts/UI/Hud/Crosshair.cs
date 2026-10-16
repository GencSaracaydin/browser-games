using LongShadow.Core;
using LongShadow.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Dynamic hip-fire cross (GDD §11): four lines that spread with <see cref="IWeaponHudState.CrosshairSpread01"/>,
    /// hidden while ADS (iron sights), while sprinting (WP02 stance) and under the binocular / door-gun overlays. Each arm
    /// has a dark underlay so it reads on sunlit sand.
    /// </summary>
    public sealed class Crosshair : HudView
    {
        private const float MinGap = 7f;
        private const float MaxGap = 58f;
        private const float ArmLength = 14f;
        private const float ArmWidth = 3f;

        private readonly RectTransform[] _arms = new RectTransform[4];
        private CanvasGroup _group;
        private float _gap = MinGap;
        private float _alpha;

        /// <summary>Current arm offset in reference pixels (tests).</summary>
        public float Gap => _gap;

        /// <summary>Current opacity (tests).</summary>
        public float Alpha => _alpha;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Rect("Crosshair", parent, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(160f, 160f));
            _group = UIFactory.Group(Root.gameObject, 0f);
            var line = Ctx.Sprite(UISpriteId.Line);
            for (var i = 0; i < 4; i++)
            {
                var arm = UIFactory.Rect("Arm" + i, Root, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(ArmWidth, ArmLength));
                arm.localEulerAngles = new Vector3(0f, 0f, i * 90f);
                var shade = UIFactory.Image("Shade", arm, line, UIColors.Shadow, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(ArmWidth + 3f, ArmLength + 3f));
                shade.type = Image.Type.Sliced;
                var img = UIFactory.Image("Line", arm, line, UIColors.WithAlpha(UIColors.Ink, 0.92f), UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(ArmWidth, ArmLength));
                img.type = Image.Type.Sliced;
                _arms[i] = arm;
            }
            Place();
        }

        public override void Tick(float dt)
        {
            var w = Ctx.Weapon.Value;
            var rig = Ctx.Rig.Value;
            var visible = w != null && w.CrosshairVisible && Ctx.Stance != Stance.Sprint && Ctx.Overlay == HudOverlay.None &&
                          (rig == null || !rig.IsGlassing) && Ctx.AdsBlend < 0.5f;
            _alpha = UIAnim.Approach(_alpha, visible ? 1f : 0f, 9f, dt);
            _group.alpha = _alpha;
            if (w == null) return;
            var target = HudMath.CrosshairGap(w.CrosshairSpread01, MinGap, MaxGap);
            var gap = UIAnim.Damp(_gap, target, 22f, dt);
            if (Mathf.Abs(gap - _gap) > 0.05f)
            {
                _gap = gap;
                Place();
            }
        }

        private void Place()
        {
            var d = _gap + ArmLength * 0.5f;
            _arms[0].anchoredPosition = new Vector2(0f, d);
            _arms[1].anchoredPosition = new Vector2(-d, 0f);
            _arms[2].anchoredPosition = new Vector2(0f, -d);
            _arms[3].anchoredPosition = new Vector2(d, 0f);
        }
    }
}
