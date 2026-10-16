using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// World-space rings (GDD §11): drill progress in Beat 9 (%) and the LZ hold in Beat 10 (Magpie's distance in
    /// metres), set through <see cref="LongShadow.Core.IHud.SetRing"/>. A ring tracks its target on screen with a radial
    /// fill and a label, and clamps to the screen edge when off-screen. The label is rebuilt only when it changes.
    /// </summary>
    public sealed class WorldRings : HudView
    {
        private const int Pool = 4;

        private sealed class Ring
        {
            public string Id;
            public Transform Target;
            public float Value;
            public string Label;
            public string LabelShown;
            public RectTransform Rect;
            public Image Back;
            public Image Fill;
            public TextMeshProUGUI Text;
        }

        private readonly Ring[] _rings = new Ring[Pool];

        /// <summary>Rings in use (tests).</summary>
        public int Count
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Pool; i++) if (_rings[i].Id != null) n++;
                return n;
            }
        }

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("WorldRings", parent);
            var thin = Ctx.Sprite(UISpriteId.Ring);
            var thick = Ctx.Sprite(UISpriteId.RingThick);
            for (var i = 0; i < Pool; i++)
            {
                var r = new Ring();
                r.Rect = UIFactory.Rect("Ring" + i, Root, UIFactory.BottomLeft, UIFactory.Center, Vector2.zero, new Vector2(74f, 74f));
                r.Back = UIFactory.Image("Back", r.Rect, thin, UIColors.WithAlpha(UIColors.Ink, 0.5f), UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(74f, 74f));
                r.Fill = UIFactory.Image("Fill", r.Rect, thick, UIColors.MarkerAmber, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(66f, 66f));
                UIFactory.MakeRadial(r.Fill);
                r.Text = UIFactory.Text("Label", r.Rect, Ctx.Fonts, FontRole.Condensed, 20f, UIColors.Ink, TextAlignmentOptions.Top,
                    UIFactory.Center, UIFactory.TopCenter, new Vector2(0f, -42f), new Vector2(240f, 26f));
                r.Text.characterSpacing = 3f;
                r.Rect.gameObject.SetActive(false);
                _rings[i] = r;
            }
        }

        /// <summary>Adds or updates a ring.</summary>
        public void Set(string id, Transform target, float value01, string label)
        {
            if (string.IsNullOrEmpty(id)) return;
            Ring free = null;
            for (var i = 0; i < Pool; i++)
            {
                if (string.Equals(_rings[i].Id, id))
                {
                    free = _rings[i];
                    break;
                }
                if (free == null && _rings[i].Id == null) free = _rings[i];
            }
            if (free == null) return;
            free.Id = id;
            free.Target = target;
            free.Value = Mathf.Clamp01(value01);
            free.Label = label;
        }

        /// <summary>Removes a ring.</summary>
        public void Clear(string id)
        {
            for (var i = 0; i < Pool; i++)
            {
                var r = _rings[i];
                if (!string.Equals(r.Id, id)) continue;
                r.Id = null;
                r.Target = null;
                r.LabelShown = null;
                r.Rect.gameObject.SetActive(false);
            }
        }

        public override void ResetState()
        {
            for (var i = 0; i < Pool; i++) Clear(_rings[i].Id);
        }

        public override void Tick(float dt)
        {
            var cam = Ctx.Camera;
            var center = Ctx.CanvasSize * 0.5f;
            var half = new Vector2(Mathf.Max(10f, center.x - HudLayout.MarkerEdgeInset - 20f), Mathf.Max(10f, center.y - HudLayout.MarkerEdgeInset - 20f));
            for (var i = 0; i < Pool; i++)
            {
                var r = _rings[i];
                var show = r.Id != null && r.Target != null && cam != null && Ctx.GameplayVisible;
                if (r.Rect.gameObject.activeSelf != show) r.Rect.gameObject.SetActive(show);
                if (!show) continue;
                var p = HudMath.WorldToCanvas(cam, r.Target.position + Vector3.up * 1.2f, Ctx.ScaleFactor, out _);
                HudMath.ClampToRect(ref p, center, half, out _);
                r.Rect.anchoredPosition = p;
                r.Fill.fillAmount = r.Value;
                var color = Ctx.MarkerColor;
                color.a = Mathf.Lerp(1f, 0.35f, Ctx.AdsBlend);
                UIFactory.SetColor(r.Fill, color);
                if (!string.Equals(r.Label, r.LabelShown))
                {
                    r.LabelShown = r.Label;
                    r.Text.SetText(r.Label ?? string.Empty);
                }
            }
        }
    }
}
