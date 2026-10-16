using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Fail card (GDD §17): the screen darkens and <c>YOU ABANDONED YOUR TEAM</c> is typed in the distressed typewriter
    /// face with key clacks; it holds until <see cref="GameEvents.CheckpointLoaded"/> (or 6 s) and fades. Death uses the
    /// death insert instead (WP09).
    /// </summary>
    public sealed class FailCardView : HudView
    {
        private const float Cps = 20f;
        private const float MaxHold = 6f;

        private CanvasGroup _group;
        private TextMeshProUGUI _text;
        private Image _rule;
        private float _age = 99f;
        private int _length;
        private int _typed;
        private bool _closing;
        private float _alpha;
        private System.Action<string> _onLoaded;

        /// <summary>True while the card is up (tests).</summary>
        public bool Showing => _age < MaxHold && !_closing;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("FailCard", parent);
            _group = UIFactory.Group(Root.gameObject, 0f);
            UIFactory.StretchImage("Dim", Root, Ctx.Sprite(UISpriteId.Square), new Color(0f, 0f, 0f, 0.78f));
            _text = UIFactory.Text("Text", Root, Ctx.Fonts, FontRole.Typewriter, 58f, UIColors.Ink, TextAlignmentOptions.Center,
                UIFactory.Center, UIFactory.Center, new Vector2(0f, 10f), new Vector2(1600f, 90f), false);
            _text.characterSpacing = 6f;
            _rule = UIFactory.Image("Rule", Root, Ctx.Sprite(UISpriteId.Square), UIColors.StampRed, UIFactory.Center, UIFactory.Center, new Vector2(0f, -40f), new Vector2(0f, 3f));
            Root.gameObject.SetActive(false);
        }

        public override void Enable()
        {
            _onLoaded ??= _ => Close();
            GameEvents.CheckpointLoaded += _onLoaded;
        }

        public override void Disable() => GameEvents.CheckpointLoaded -= _onLoaded;

        /// <summary>Shows the card with a text.</summary>
        public void Show(string text)
        {
            _text.SetText(string.IsNullOrEmpty(text) ? "YOU ABANDONED YOUR TEAM" : text);
            _length = string.IsNullOrEmpty(text) ? 23 : text.Length;
            _text.maxVisibleCharacters = 0;
            _typed = 0;
            _age = 0f;
            _closing = false;
            Root.gameObject.SetActive(true);
        }

        /// <summary>Starts the fade-out.</summary>
        public void Close()
        {
            if (Root.gameObject.activeSelf) _closing = true;
        }

        public override void ResetState()
        {
            _closing = false;
            _age = 99f;
            _alpha = 0f;
            _group.alpha = 0f;
            Root.gameObject.SetActive(false);
        }

        public override void Tick(float dt)
        {
            if (!Root.gameObject.activeSelf) return;
            _age += dt;
            if (_age > MaxHold) _closing = true;
            _alpha = UIAnim.Approach(_alpha, _closing ? 0f : 1f, _closing ? 2f : 3f, dt);
            _group.alpha = _alpha;
            var chars = Mathf.Min(_length, Mathf.FloorToInt(Mathf.Max(0f, _age - 0.3f) * Cps));
            if (chars != _typed)
            {
                _typed = chars;
                _text.maxVisibleCharacters = chars >= _length ? 99999 : chars;
                Ctx.Audio.TypeKey(0.6f);
            }
            var w = Mathf.Lerp(0f, Mathf.Min(1200f, _length * 34f), UIAnim.OutCubic((_age - 0.3f) * Cps / Mathf.Max(1f, _length)));
            _rule.rectTransform.sizeDelta = new Vector2(w, 3f);
            if (_closing && _alpha <= 0f) ResetState();
        }
    }
}
