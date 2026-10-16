using System.Collections.Generic;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Intel card (GDD §11): an SX-70 polaroid slides in from the right for 2.5 s with the photo, a typed name and the
    /// counter (<c>INTEL 1/3</c> or <c>OPTIONAL 1/2</c>). Photo in the frame window (UI_POLAROID_FRAME layout), caption
    /// in the thick bottom border. Cards queue.
    /// </summary>
    public sealed class IntelCardView : HudView
    {
        /// <summary>Hold time on screen (GDD §11).</summary>
        public const float HoldSeconds = 2.5f;
        private const float SlideSeconds = 0.35f;
        private const int QueueSize = 3;

        private readonly string[] _titles = new string[QueueSize];
        private readonly string[] _counters = new string[QueueSize];
        private readonly Sprite[] _photos = new Sprite[QueueSize];
        private int _queued;
        private RectTransform _card;
        private Image _photo;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _counter;
        private float _age = 99f;
        private bool _showing;

        /// <summary>True while a card is on screen (tests).</summary>
        public bool Showing => _showing;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Rect("IntelCard", parent, UIFactory.MiddleRight, UIFactory.MiddleRight,
                new Vector2(-HudLayout.Margin, HudLayout.IntelCardY), HudLayout.IntelCard);
            _card = UIFactory.Rect("Card", Root, UIFactory.Center, UIFactory.Center, Vector2.zero, HudLayout.IntelCard);
            _card.localEulerAngles = new Vector3(0f, 0f, -3f);

            var photoRect = UIFactory.Rect("Photo", _card, UIFactory.Center, UIFactory.Center, Vector2.zero, Vector2.zero);
            PolaroidLayout.ToAnchors(PolaroidLayout.PhotoWindowPx, out var pMin, out var pMax);
            UIFactory.SetAnchors(photoRect, pMin, pMax, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            _photo = photoRect.gameObject.AddComponent<Image>();
            _photo.raycastTarget = false;
            _photo.color = new Color(0.16f, 0.15f, 0.13f, 1f);
            _photo.preserveAspect = false;

            var frame = UIFactory.StretchImage("Frame", _card, Ctx.Sprite(UISpriteId.PolaroidFrame), Color.white);
            frame.type = Image.Type.Simple;

            var caption = UIFactory.Rect("Caption", _card, UIFactory.Center, UIFactory.Center, Vector2.zero, Vector2.zero);
            PolaroidLayout.ToAnchors(PolaroidLayout.CaptionPx, out var cMin, out var cMax);
            UIFactory.SetAnchors(caption, cMin, cMax);
            _title = UIFactory.Text("Title", caption, Ctx.Fonts, FontRole.Typewriter, 20f, UIColors.TypeInk, TextAlignmentOptions.TopLeft,
                UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(4f, -4f), new Vector2(220f, 26f), false);
            _counter = UIFactory.Text("Counter", caption, Ctx.Fonts, FontRole.TypewriterClean, 17f, UIColors.StampRed, TextAlignmentOptions.BottomRight,
                UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-4f, 2f), new Vector2(160f, 22f), false);
            _counter.characterSpacing = 4f;
            Root.gameObject.SetActive(false);
        }

        /// <summary>Queues a card.</summary>
        public void Show(string title, Sprite photo, string counter)
        {
            if (_queued >= QueueSize) return;
            _titles[_queued] = title;
            _photos[_queued] = photo;
            _counters[_queued] = counter;
            _queued++;
        }

        public override void ResetState()
        {
            _queued = 0;
            _showing = false;
            Root.gameObject.SetActive(false);
        }

        public override void Tick(float dt)
        {
            if (!_showing)
            {
                if (_queued == 0 || !Ctx.GameplayVisible) return;
                _title.SetText(_titles[0] ?? string.Empty);
                _counter.SetText(_counters[0] ?? string.Empty);
                _photo.sprite = _photos[0];
                _photo.color = _photos[0] != null ? Color.white : new Color(0.16f, 0.15f, 0.13f, 1f);
                for (var i = 1; i < _queued; i++)
                {
                    _titles[i - 1] = _titles[i];
                    _photos[i - 1] = _photos[i];
                    _counters[i - 1] = _counters[i];
                }
                _queued--;
                _photos[_queued] = null;
                _showing = true;
                _age = 0f;
                Root.gameObject.SetActive(true);
                Ctx.Audio.Play(UiCues.IntelCard, 0.9f);
            }
            _age += dt;
            var total = SlideSeconds * 2f + HoldSeconds;
            float x;
            if (_age < SlideSeconds) x = (1f - UIAnim.OutCubic(_age / SlideSeconds)) * (HudLayout.IntelCard.x + HudLayout.Margin + 40f);
            else if (_age < SlideSeconds + HoldSeconds) x = 0f;
            else x = UIAnim.InOutCubic((_age - SlideSeconds - HoldSeconds) / SlideSeconds) * (HudLayout.IntelCard.x + HudLayout.Margin + 40f);
            _card.anchoredPosition = new Vector2(x, 0f);
            if (_age >= total)
            {
                _showing = false;
                Root.gameObject.SetActive(false);
            }
        }

        public override void CollectLayout(List<HudLayoutRegion> regions)
        {
            regions.Add(HudLayoutRegion.Of("IntelCard", Root, Ctx.CanvasRect));
        }
    }
}
