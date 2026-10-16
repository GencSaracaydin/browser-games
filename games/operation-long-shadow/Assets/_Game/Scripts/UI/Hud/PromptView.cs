using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Interaction prompts (GDD §11): centre-bottom, <c>[E] Thread the projector</c>, <c>HOLD [E] Take the ledgers</c> with
    /// a radial fill. The key in brackets is drawn in amber. Several prompts may be registered (by id); the most recent
    /// one shows. <see cref="IHud.ShowPrompt"/> may be called every frame with the same text: the text is rebuilt only
    /// when it changes, the radial every call.
    /// </summary>
    public sealed class PromptView : HudView
    {
        private const int Capacity = 4;

        private struct Slot
        {
            public string Id;
            public string Text;
            public bool Hold;
            public float Progress;
            public int Order;
        }

        private readonly Slot[] _slots = new Slot[Capacity];
        private int _order;
        private CanvasGroup _group;
        private RectTransform _plate;
        private TextMeshProUGUI _text;
        private Image _ring;
        private Image _ringBack;
        private readonly TextBuffer _buf = new TextBuffer(96);
        private string _shownText;
        private bool _shownHold;
        private float _alpha;

        /// <summary>Id of the prompt on screen, or null (tests).</summary>
        public string CurrentId { get; private set; }

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Rect("Prompt", parent, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, HudLayout.PromptBottom), HudLayout.Prompt);
            _group = UIFactory.Group(Root.gameObject, 0f);
            _plate = UIFactory.Rect("Plate", Root, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(400f, 52f));
            var plate = UIFactory.StretchImage("Image", _plate, Ctx.Sprite(UISpriteId.SoftPanel), new Color(0f, 0f, 0f, 0.42f));
            plate.type = Image.Type.Sliced;
            _ringBack = UIFactory.Image("RingBack", Root, Ctx.Sprite(UISpriteId.RingThick), UIColors.WithAlpha(UIColors.Ink, 0.18f), UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(40f, 40f));
            _ring = UIFactory.Image("Ring", Root, Ctx.Sprite(UISpriteId.RingThick), UIColors.KeyAmber, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(40f, 40f));
            UIFactory.MakeRadial(_ring);
            _text = UIFactory.Text("Text", Root, Ctx.Fonts, FontRole.CondensedMedium, 29f, UIColors.Ink, TextAlignmentOptions.Midline,
                UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(HudLayout.Prompt.x, HudLayout.Prompt.y));
            _text.characterSpacing = 2f;
            _ring.enabled = _ringBack.enabled = false;
        }

        /// <summary>Shows or updates a prompt.</summary>
        public void Show(in PromptData p)
        {
            if (string.IsNullOrEmpty(p.Id)) return;
            var i = IndexOf(p.Id);
            if (i < 0)
            {
                i = Free();
                _slots[i].Order = ++_order;
            }
            _slots[i].Id = p.Id;
            _slots[i].Text = p.Text;
            _slots[i].Hold = p.Hold;
            _slots[i].Progress = Mathf.Clamp01(p.Progress01);
        }

        /// <summary>Hides a prompt by id.</summary>
        public void Hide(string id)
        {
            var i = IndexOf(id);
            if (i < 0) return;
            _slots[i] = default;
        }

        public override void ResetState()
        {
            for (var i = 0; i < Capacity; i++) _slots[i] = default;
        }

        public override void Tick(float dt)
        {
            var best = -1;
            for (var i = 0; i < Capacity; i++)
                if (_slots[i].Id != null && (best < 0 || _slots[i].Order > _slots[best].Order)) best = i;

            var visible = best >= 0 && Ctx.GameplayVisible && Ctx.Overlay != HudOverlay.DoorGun;
            _alpha = UIAnim.Approach(_alpha, visible ? 1f : 0f, 10f, dt);
            _group.alpha = _alpha;
            CurrentId = best >= 0 ? _slots[best].Id : null;
            if (best < 0) return;

            var s = _slots[best];
            if (!string.Equals(s.Text, _shownText) || s.Hold != _shownHold)
            {
                _shownText = s.Text;
                _shownHold = s.Hold;
                Format(_buf, s.Text);
                _buf.ApplyTo(_text);
                var pref = _text.GetPreferredValues();
                var ringSpace = s.Hold ? 52f : 0f;
                var width = Mathf.Min(HudLayout.Prompt.x, pref.x + 44f + ringSpace);
                _plate.sizeDelta = new Vector2(width, 52f);
                _text.rectTransform.anchoredPosition = new Vector2(ringSpace * 0.5f, 0f);
                var ringX = -width * 0.5f + 34f;
                _ring.rectTransform.anchoredPosition = new Vector2(ringX, 0f);
                _ringBack.rectTransform.anchoredPosition = new Vector2(ringX, 0f);
                _ring.enabled = _ringBack.enabled = s.Hold;
            }
            if (s.Hold) _ring.fillAmount = s.Progress;
        }

        /// <summary>Copies a prompt text, wrapping every "[KEY]" in the key colour.</summary>
        internal static void Format(TextBuffer b, string text)
        {
            b.Clear();
            if (string.IsNullOrEmpty(text)) return;
            var i = 0;
            while (i < text.Length)
            {
                var open = text.IndexOf('[', i);
                if (open < 0)
                {
                    b.Append(text, i, text.Length - i);
                    break;
                }
                var close = text.IndexOf(']', open + 1);
                if (close < 0)
                {
                    b.Append(text, i, text.Length - i);
                    break;
                }
                b.Append(text, i, open - i);
                b.OpenColor(UIColors.KeyAmber).Append(text, open, close - open + 1).CloseColor();
                i = close + 1;
            }
        }

        private int IndexOf(string id)
        {
            for (var i = 0; i < Capacity; i++)
                if (string.Equals(_slots[i].Id, id)) return i;
            return -1;
        }

        private int Free()
        {
            var oldest = 0;
            for (var i = 0; i < Capacity; i++)
            {
                if (_slots[i].Id == null) return i;
                if (_slots[i].Order < _slots[oldest].Order) oldest = i;
            }
            return oldest;
        }

        public override void CollectLayout(List<HudLayoutRegion> regions)
        {
            regions.Add(HudLayoutRegion.Of("Prompt", Root, Ctx.CanvasRect));
        }
    }
}
