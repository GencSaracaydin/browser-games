using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>One pooled objective marker on screen: icon, edge arrow, short label and distance.</summary>
    internal sealed class MarkerWidget
    {
        public RectTransform Root;
        public Image Icon;
        public Image Arrow;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Distance;
        public readonly TextBuffer DistanceText = new TextBuffer(12);

        public string MarkerId;
        public string LabelShown;
        public int DistanceShown = -1;
        public bool Occluded;
        public bool Active;
        public bool LabelVisible = true;
        public float Alpha = -1f;

        public static MarkerWidget Create(HudContext ctx, RectTransform parent, int index)
        {
            var w = new MarkerWidget();
            w.Root = UIFactory.Rect("Marker" + index, parent, UIFactory.BottomLeft, UIFactory.Center, Vector2.zero, new Vector2(28f, 28f));
            w.Icon = UIFactory.Image("Icon", w.Root, ctx.Sprite(UISpriteId.Diamond), UIColors.MarkerAmber, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(28f, 28f));
            w.Arrow = UIFactory.Image("Arrow", w.Root, ctx.Sprite(UISpriteId.Arrow), UIColors.MarkerAmber, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(18f, 18f));
            w.Arrow.enabled = false;
            w.Label = UIFactory.Text("Label", w.Root, ctx.Fonts, FontRole.CondensedMedium, 17f, UIColors.Ink, TextAlignmentOptions.Bottom,
                UIFactory.Center, UIFactory.BottomCenter, new Vector2(0f, 18f), new Vector2(220f, 22f));
            w.Label.characterSpacing = 4f;
            w.Distance = UIFactory.Text("Distance", w.Root, ctx.Fonts, FontRole.Condensed, 18f, UIColors.Ink, TextAlignmentOptions.Top,
                UIFactory.Center, UIFactory.TopCenter, new Vector2(0f, -17f), new Vector2(120f, 22f));
            w.SetActive(false);
            return w;
        }

        public void SetActive(bool active)
        {
            if (Active == active && Root.gameObject.activeSelf == active) return;
            Active = active;
            Root.gameObject.SetActive(active);
            if (!active)
            {
                MarkerId = null;
                LabelShown = null;
                DistanceShown = -1;
                Alpha = -1f;
            }
        }

        public void SetAlpha(float a)
        {
            if (Mathf.Abs(Alpha - a) < 0.004f) return;
            Alpha = a;
            UIFactory.SetAlpha(Icon, a);
            UIFactory.SetAlpha(Arrow, a);
            Label.alpha = a;
            Distance.alpha = a;
        }

        public void SetColor(Color c)
        {
            c.a = Alpha < 0f ? 1f : Alpha;
            UIFactory.SetColor(Icon, c);
            UIFactory.SetColor(Arrow, c);
        }
    }
}
