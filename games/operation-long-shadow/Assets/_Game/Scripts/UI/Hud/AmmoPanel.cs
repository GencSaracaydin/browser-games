using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Ammo and grenades (GDD §11): bottom-right weapon name (<c>COMMANDO</c>), magazine and reserve in large numerals
    /// (<c>30 / 120</c>), a fire-mode icon, a flash below 25 % of the magazine, <c>R — RELOAD</c> at 0, and frag icons
    /// beside it (with the cook timer as a radial ring on the first icon while a grenade is cooked). Polls
    /// <see cref="IWeaponHudState"/> every frame and rewrites text only when a number changes. Hidden while the door gun
    /// is mounted (its heat bar replaces it) and while no weapon reports a name.
    /// </summary>
    public sealed class AmmoPanel : HudView
    {
        private const int FragIcons = 4;
        private const float LowAmmoFraction = 0.25f; // GDD §11

        private TextMeshProUGUI _name;
        private TextMeshProUGUI _mag;
        private TextMeshProUGUI _reserve;
        private TextMeshProUGUI _hint;
        private Image _fireMode;
        private readonly Image[] _frags = new Image[FragIcons];
        private Image _cook;
        private CanvasGroup _group;
        private readonly TextBuffer _buf = new TextBuffer(24);

        private string _lastName;
        private int _lastMag = int.MinValue, _lastReserve = int.MinValue, _lastFrags = -1;
        private FireMode _lastMode = (FireMode)(-1);
        private int _hintState = -1;
        private float _alpha;

        /// <summary>True while the low-ammo flash runs (tests).</summary>
        public bool LowAmmo { get; private set; }

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Rect("Ammo", parent, UIFactory.BottomRight, UIFactory.BottomRight,
                new Vector2(-HudLayout.Margin, HudLayout.AmmoBottom), HudLayout.Ammo);
            _group = UIFactory.Group(Root.gameObject, 0f);

            _name = UIFactory.Text("Weapon", Root, Ctx.Fonts, FontRole.CondensedMedium, 23f, UIColors.InkDim, TextAlignmentOptions.BottomRight,
                UIFactory.TopRight, UIFactory.TopRight, new Vector2(-40f, -6f), new Vector2(300f, 28f));
            _name.characterSpacing = 9f;
            _fireMode = UIFactory.Image("FireMode", Root, Ctx.Sprite(UISpriteId.IconFireAuto), UIColors.InkDim, UIFactory.TopRight, UIFactory.TopRight,
                new Vector2(0f, -6f), new Vector2(30f, 30f));
            _fireMode.preserveAspect = true;

            _mag = UIFactory.Text("Magazine", Root, Ctx.Fonts, FontRole.Condensed, 76f, UIColors.Ink, TextAlignmentOptions.BaselineRight,
                UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-132f, 44f), new Vector2(130f, 80f));
            _reserve = UIFactory.Text("Reserve", Root, Ctx.Fonts, FontRole.Condensed, 36f, UIColors.InkDim, TextAlignmentOptions.BaselineLeft,
                UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(0f, 44f), new Vector2(128f, 80f));
            _hint = UIFactory.Text("Hint", Root, Ctx.Fonts, FontRole.CondensedMedium, 21f, UIColors.KeyAmber, TextAlignmentOptions.BottomRight,
                UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(0f, 4f), new Vector2(300f, 26f));
            _hint.characterSpacing = 6f;

            var frag = Ctx.Sprite(UISpriteId.IconFrag);
            for (var i = 0; i < FragIcons; i++)
            {
                var img = UIFactory.Image("Frag" + i, Root, frag, UIColors.Ink, UIFactory.BottomRight, UIFactory.BottomRight,
                    new Vector2(-270f - i * 28f, 58f), new Vector2(26f, 26f));
                img.preserveAspect = true;
                _frags[i] = img;
            }
            _cook = UIFactory.Image("Cook", _frags[0].rectTransform, Ctx.Sprite(UISpriteId.RingThick), UIColors.KeyAmber, UIFactory.Center, UIFactory.Center,
                Vector2.zero, new Vector2(44f, 44f));
            UIFactory.MakeRadial(_cook);
            _cook.enabled = false;
        }

        public override void Tick(float dt)
        {
            var w = Ctx.Weapon.Value;
            var visible = w != null && !string.IsNullOrEmpty(w.DisplayName) && Ctx.Overlay == HudOverlay.None && w.MagazineSize > 0;
            _alpha = UIAnim.Approach(_alpha, visible ? 1f : 0f, 6f, dt);
            _group.alpha = _alpha;
            if (w == null || _alpha <= 0f) return;

            var name = w.DisplayName;
            if (!ReferenceEquals(name, _lastName))
            {
                _lastName = name;
                _name.SetText(name ?? string.Empty);
            }
            var mode = w.FireMode;
            if (mode != _lastMode)
            {
                _lastMode = mode;
                _fireMode.sprite = Ctx.Sprite(mode == FireMode.Auto ? UISpriteId.IconFireAuto :
                    mode == FireMode.Burst ? UISpriteId.IconFireBurst : UISpriteId.IconFireSemi);
            }
            var mag = w.Magazine;
            if (mag != _lastMag)
            {
                _lastMag = mag;
                _buf.Clear().Append(Mathf.Max(0, mag)).ApplyTo(_mag);
            }
            var reserve = w.Reserve;
            if (reserve != _lastReserve)
            {
                _lastReserve = reserve;
                _buf.Clear().Append(" / ").Append(Mathf.Max(0, reserve)).ApplyTo(_reserve);
            }
            var frags = w.Frags;
            if (frags != _lastFrags)
            {
                _lastFrags = frags;
                for (var i = 0; i < FragIcons; i++)
                {
                    var on = i < frags || (i == 0 && frags == 0);
                    UIFactory.SetActive(_frags[i], on);
                    UIFactory.SetColor(_frags[i], frags == 0 ? UIColors.WithAlpha(UIColors.Ink, 0.25f) : UIColors.Ink);
                }
            }

            // Low ammo flash below 25 % (GDD §11) and the reload hint at 0.
            var size = Mathf.Max(1, w.MagazineSize);
            LowAmmo = mag > 0 && mag < size * LowAmmoFraction;
            if (LowAmmo)
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin(Ctx.Time * Mathf.PI * 2f * 3.2f);
                _mag.color = Color.Lerp(UIColors.Ink, UIColors.Warning, pulse);
            }
            else
            {
                var c = mag <= 0 ? UIColors.Red : UIColors.Ink;
                if (_mag.color != c) _mag.color = c;
            }

            var hint = w.IsReloading ? 0 : mag <= 0 ? (reserve > 0 ? 1 : 2) : 0;
            if (hint != _hintState)
            {
                _hintState = hint;
                _hint.SetText(hint == 1 ? "R — RELOAD" : hint == 2 ? "NO RESERVE" : string.Empty);
                _hint.color = hint == 2 ? UIColors.Red : UIColors.KeyAmber;
            }
            if (hint == 1) _hint.alpha = UIAnim.Blink(Ctx.Time, 0.9f) ? 1f : 0.45f;

            // Cook timer (4 s fuse, GDD §13.2).
            var cook = w.CookTime01;
            var cooking = cook > 0f;
            if (_cook.enabled != cooking) _cook.enabled = cooking;
            if (cooking)
            {
                _cook.fillAmount = cook;
                _cook.color = cook > 0.75f ? UIColors.Red : UIColors.KeyAmber;
            }
        }

        public override void CollectLayout(List<HudLayoutRegion> regions)
        {
            regions.Add(HudLayoutRegion.Of("Ammo", Root, Ctx.CanvasRect));
        }
    }
}
