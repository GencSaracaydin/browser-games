using LongShadow.Core;
using LongShadow.UI.Common;
using LongShadow.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Screens
{
    /// <summary>
    /// Mission title card (GDD §8.3, <see cref="IHud.TitleCard"/>): 2.39:1 bars slide in, amber teletype type in the
    /// typewriter face prints each line first as five-digit number groups that are corrected into the real text, line 3
    /// reads 18:40 for 6 frames before settling to 17:58, then the bars retract and
    /// <see cref="UiEvents.TitleCardFinished"/> is raised (D007 follows, WP07). Amber glow and a faint warm bloom stand in
    /// for the lens dirt CINEMATIC_DIRECTION §3 asks for (the overlay canvas is not post-processed).
    /// </summary>
    public sealed class TitleCardView : HudView
    {
        private readonly LetterboxBars _bars;
        private readonly TextMeshProUGUI[] _lines = new TextMeshProUGUI[4];
        private readonly TextBuffer[] _shown = new TextBuffer[4];
        private readonly int[] _visible = new int[4];
        private readonly TextBuffer _scratch = new TextBuffer(64);
        private CanvasGroup _group;
        private RectTransform _block;
        private TitleCardScript _script;
        private float _t;
        private bool _playing;
        private bool _barsOut;
        private int _flashFrames;
        private int _lastCorrectLine = -1;

        /// <summary>Creates the view; it drives the shared letterbox bars.</summary>
        public TitleCardView(LetterboxBars bars)
        {
            _bars = bars;
        }

        /// <summary>True while the card plays.</summary>
        public bool Playing => _playing;

        /// <summary>Current text of a line (tests; allocates).</summary>
        internal string LineText(int i) => _shown[i].ToString();

        /// <summary>Seconds since the card started (tests).</summary>
        internal float Elapsed => _t;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("TitleCard", parent);
            _group = UIFactory.Group(Root.gameObject, 0f);
            _block = UIFactory.Rect("Block", Root, UIFactory.BottomLeft, UIFactory.BottomLeft, new Vector2(150f, 200f), new Vector2(1300f, 240f));
            UIFactory.Image("Bloom", _block, Ctx.Sprite(UISpriteId.SoftDisc), new Color(1f, 0.7f, 0.3f, 0.07f), UIFactory.MiddleLeft, UIFactory.Center,
                new Vector2(420f, 0f), new Vector2(1500f, 420f));
            var glow = UIColors.WithAlpha(UIColors.AmberPhosphor, 0.55f);
            var y = 0f;
            for (var i = 0; i < 4; i++)
            {
                var size = i == 0 ? 54f : 36f;
                _lines[i] = UIFactory.Text("Line" + i, _block, Ctx.Fonts, FontRole.Typewriter, size, UIColors.AmberPhosphor, TextAlignmentOptions.TopLeft,
                    UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(0f, -y), new Vector2(1300f, size * 1.3f), false, Ctx.Theme.PhosphorGlow ? glow : default);
                _lines[i].characterSpacing = i == 0 ? 6f : 4f;
                _shown[i] = new TextBuffer(48);
                y += size * 1.35f;
            }
            Root.gameObject.SetActive(false);
        }

        /// <summary>Plays the card for <paramref name="seconds"/> (8 s in the GDD).</summary>
        public void Play(float seconds)
        {
            _script = new TitleCardScript(seconds > 0f ? seconds : TitleCardScript.ReferenceSeconds);
            _t = 0f;
            _playing = true;
            _barsOut = false;
            _flashFrames = 0;
            _lastCorrectLine = -1;
            for (var i = 0; i < 4; i++)
            {
                _shown[i].Clear();
                _visible[i] = -1;
                _lines[i].SetText(string.Empty);
            }
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            _group.alpha = 1f;
            var bar = HudMath.LetterboxBarHeight(Ctx.CanvasSize.x, Ctx.CanvasSize.y, HudLayout.LetterboxAspect);
            _block.anchoredPosition = new Vector2(Mathf.Max(90f, Ctx.CanvasSize.x * 0.08f), bar + 40f);
            _bars?.Set(true, TitleCardScript.BarsSeconds);
            Ctx.Audio.Stinger(UiCues.TitleStinger);
        }

        /// <summary>Stops the card at once (skip, checkpoint load).</summary>
        public void Stop()
        {
            if (!_playing) return;
            _playing = false;
            _bars?.Set(false, 0.2f);
            Root.gameObject.SetActive(false);
        }

        public override void ResetState() => Stop();

        public override void Tick(float dt)
        {
            if (!_playing) return;
            _t += dt;
            var s = _script;

            // 18:40 must stay up for at least 2 rendered frames even at low frame rates (GDD §8.4 convention).
            var forceFlash = false;
            if (_t >= s.FlashStart && _flashFrames < 2)
            {
                _flashFrames++;
                forceFlash = true;
            }

            for (var i = 0; i < 4; i++)
            {
                var visible = s.Evaluate(i, _t, _scratch, i == TitleCardScript.FlashLine && forceFlash);
                var changed = !_scratch.ContentEquals(_shown[i]);
                if (changed)
                {
                    _scratch.CopyTo(_shown[i]);
                    _shown[i].ApplyTo(_lines[i]);
                }
                if (visible != _visible[i])
                {
                    if (visible > _visible[i] && visible > 0) Ctx.Audio.TypeKey(0.42f, 0.045f);
                    _visible[i] = visible;
                    _lines[i].maxVisibleCharacters = visible;
                }
                if (_t >= s.PrintEnd(i) && _t < s.CorrectEnd(i) && _lastCorrectLine != i)
                {
                    _lastCorrectLine = i;
                    Ctx.Audio.Play(UiCues.TeletypeLine, 0.4f);
                }
            }

            if (!_barsOut && _t >= s.BarsOutAt)
            {
                _barsOut = true;
                _bars?.Set(false, TitleCardScript.BarsSeconds);
            }
            if (_barsOut) _group.alpha = 1f - Mathf.Clamp01((_t - s.BarsOutAt) / TitleCardScript.BarsSeconds);
            if (_t >= s.Duration)
            {
                _playing = false;
                Root.gameObject.SetActive(false);
                GameEvents.RaiseMissionEvent(UiEvents.TitleCardFinished);
            }
        }
    }
}
