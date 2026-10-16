using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// One-time tutorial prompts (GDD §11): each key is shown once ever (persisted as <c>ls.tut.&lt;key&gt;</c> through
    /// <see cref="ISettingsService.SetValue"/>; in memory when no settings service exists). Queued, shown one at a time
    /// for 5 s in a typed "field note" on the left, below the objectives.
    /// </summary>
    public sealed class TutorialPrompts : HudView
    {
        private const float ShowSeconds = 5f;
        private const float Cps = 70f;
        private const int QueueSize = 6;

        private readonly string[] _queueKeys = new string[QueueSize];
        private readonly string[] _queueTexts = new string[QueueSize];
        private int _queueCount;
        private readonly HashSet<string> _seenThisSession = new HashSet<string>();
        private CanvasGroup _group;
        private TextMeshProUGUI _header;
        private TextMeshProUGUI _body;
        private float _age = 99f;
        private int _bodyLength;

        /// <summary>Key on screen, or null (tests).</summary>
        public string CurrentKey { get; private set; }

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Rect("Tutorial", parent, UIFactory.TopLeft, UIFactory.TopLeft,
                new Vector2(HudLayout.Margin, -HudLayout.TutorialTop), HudLayout.Tutorial);
            _group = UIFactory.Group(Root.gameObject, 0f);
            var plate = UIFactory.StretchImage("Plate", Root, Ctx.Sprite(UISpriteId.SoftPanel), new Color(0f, 0f, 0f, 0.4f));
            plate.type = Image.Type.Sliced;
            var bar = UIFactory.Image("Bar", Root, Ctx.Sprite(UISpriteId.Square), UIColors.KeyAmber, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(0f, 0f), new Vector2(4f, HudLayout.Tutorial.y));
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(4f, 0f);
            _header = UIFactory.Text("Header", Root, Ctx.Fonts, FontRole.Condensed, 17f, UIColors.KeyAmber, TextAlignmentOptions.TopLeft,
                UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(20f, -10f), new Vector2(HudLayout.Tutorial.x - 30f, 22f));
            _header.characterSpacing = 8f;
            _header.SetText("FIELD NOTE");
            _body = UIFactory.Text("Body", Root, Ctx.Fonts, FontRole.TypewriterClean, 21f, UIColors.Ink, TextAlignmentOptions.TopLeft,
                UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(20f, -36f), new Vector2(HudLayout.Tutorial.x - 36f, HudLayout.Tutorial.y - 44f));
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.overflowMode = TextOverflowModes.Ellipsis;
        }

        /// <summary>Queues a tutorial unless its key was already shown (persisted).</summary>
        public void Show(string key, string text)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(text)) return;
            if (!_seenThisSession.Add(key)) return;
            var settings = Ctx.Settings.Value;
            if (settings != null)
            {
                var storageKey = StorageKeys.TutorialPrefix + key;
                if (settings.GetValue(storageKey) == "1") return;
                settings.SetValue(storageKey, "1");
            }
            if (_queueCount >= QueueSize) return;
            _queueKeys[_queueCount] = key;
            _queueTexts[_queueCount] = text;
            _queueCount++;
        }

        public override void ResetState()
        {
            _queueCount = 0;
            _age = 99f;
            CurrentKey = null;
            _group.alpha = 0f;
        }

        public override void Tick(float dt)
        {
            if (CurrentKey == null)
            {
                if (_queueCount == 0 || !Ctx.GameplayVisible) return;
                CurrentKey = _queueKeys[0];
                _body.SetText(_queueTexts[0]);
                _bodyLength = _queueTexts[0].Length;
                _body.maxVisibleCharacters = 0;
                for (var i = 1; i < _queueCount; i++)
                {
                    _queueKeys[i - 1] = _queueKeys[i];
                    _queueTexts[i - 1] = _queueTexts[i];
                }
                _queueCount--;
                _age = 0f;
                Ctx.Audio.Play(UiCues.TeletypeLine, 0.5f);
            }
            _age += dt;
            var chars = Mathf.Min(_bodyLength, Mathf.FloorToInt(_age * Cps));
            _body.maxVisibleCharacters = chars >= _bodyLength ? 99999 : chars;
            _group.alpha = UIAnim.Envelope(_age, 0.2f, ShowSeconds, 0.5f) * (Ctx.GameplayVisible ? 1f : 0f);
            if (_age > ShowSeconds)
            {
                CurrentKey = null;
                _group.alpha = 0f;
            }
        }

        public override void CollectLayout(List<HudLayoutRegion> regions)
        {
            regions.Add(HudLayoutRegion.Of("Tutorial", Root, Ctx.CanvasRect));
        }
    }
}
