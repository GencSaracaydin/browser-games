using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Radio subtitle box (GDD §11): bottom centre, at most 1,100 px wide and two lines per entry,
    /// <c>[SUNDIAL-ACTUAL] HOLLIS: Off the middle of the street.</c>, speaker colours from <see cref="SpeakerIds"/>, a
    /// radio-wave icon for radio lines, italic for the interrogator in the booth, monospace for the Clockmaker, sizes
    /// S/M/L from the options. Enemy barks show only with "Translate enemy chatter" (VO filters the 25 m range).
    /// Driven by <see cref="GameEvents.SubtitleShow"/>/<see cref="GameEvents.SubtitleHide"/>; stays visible while the
    /// gameplay HUD is hidden (cinematics, inserts). Timers stop while the game is paused.
    /// </summary>
    public sealed class SubtitleView : HudView
    {
        private const int Pool = 3;
        private const float PadX = 22f, PadY = 9f, IconSize = 24f, IconGap = 10f;
        private const float FadeIn = 0.12f, FadeOut = 0.25f, Linger = 0.25f;

        private sealed class Entry
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Plate;
            public Image Radio;
            public TextMeshProUGUI Text;
            public readonly TextBuffer Buffer = new TextBuffer(192);
            public int Handle;
            public float Age;
            public float Duration;
            public bool Live;
            public bool Fading;
            public float FadeAge;
            public float Height;
            public float Y;
            public int Order;
            public bool Monospace;
            public bool HasRadio;
        }

        private readonly Entry[] _entries = new Entry[Pool];
        private float _width;
        private int _order;
        private SubtitleSize _size = SubtitleSize.Medium;
        private System.Action<SubtitleData> _onShow;
        private System.Action<int> _onHide;
        private System.Action _onSettings;

        /// <summary>Entries visible (tests).</summary>
        public int VisibleCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Pool; i++) if (_entries[i].Live) n++;
                return n;
            }
        }

        /// <summary>Formatted text of the newest live entry (tests; allocates).</summary>
        internal string NewestText()
        {
            Entry best = null;
            for (var i = 0; i < Pool; i++)
                if (_entries[i].Live && (best == null || _entries[i].Order > best.Order)) best = _entries[i];
            return best != null ? best.Buffer.ToString() : null;
        }

        /// <summary>Font size of the newest entry (tests).</summary>
        internal float NewestFontSize()
        {
            Entry best = null;
            for (var i = 0; i < Pool; i++)
                if (_entries[i].Live && (best == null || _entries[i].Order > best.Order)) best = _entries[i];
            return best != null ? best.Text.fontSize : 0f;
        }

        protected override void OnBuild(RectTransform parent)
        {
            _width = HudLayout.SubtitleWidth(Ctx.CanvasSize);
            Root = UIFactory.Rect("Subtitles", parent, UIFactory.BottomCenter, UIFactory.BottomCenter,
                new Vector2(0f, HudLayout.SubtitleBottom), new Vector2(_width, HudLayout.SubtitleStackHeight));
            var plate = Ctx.Sprite(UISpriteId.SoftPanel);
            var radio = Ctx.Sprite(UISpriteId.IconRadio);
            for (var i = 0; i < Pool; i++)
            {
                var e = new Entry();
                e.Rect = UIFactory.Rect("Entry" + i, Root, UIFactory.BottomCenter, UIFactory.BottomCenter, Vector2.zero, new Vector2(_width, 60f));
                e.Group = UIFactory.Group(e.Rect.gameObject, 0f);
                e.Plate = UIFactory.StretchImage("Plate", e.Rect, plate, UIColors.SubtitlePlate);
                e.Radio = UIFactory.Image("Radio", e.Rect, radio, UIColors.Ink, UIFactory.MiddleLeft, UIFactory.MiddleLeft, new Vector2(PadX, 0f), new Vector2(IconSize, IconSize));
                e.Radio.preserveAspect = true;
                e.Text = UIFactory.Text("Text", e.Rect, Ctx.Fonts, FontRole.CondensedMedium, SubtitleFormatter.FontSize(_size), UIColors.Ink,
                    TextAlignmentOptions.MidlineLeft, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(_width - 2f * PadX, 50f));
                e.Text.textWrappingMode = TextWrappingModes.Normal;
                e.Text.overflowMode = TextOverflowModes.Ellipsis;
                e.Text.lineSpacing = -6f;
                e.Rect.gameObject.SetActive(false);
                _entries[i] = e;
            }
        }

        public override void Enable()
        {
            _onShow ??= Show;
            _onHide ??= Hide;
            _onSettings ??= OnSettingsChanged;
            GameEvents.SubtitleShow += _onShow;
            GameEvents.SubtitleHide += _onHide;
            GameEvents.SettingsChanged += _onSettings;
            OnSettingsChanged();
        }

        public override void Disable()
        {
            GameEvents.SubtitleShow -= _onShow;
            GameEvents.SubtitleHide -= _onHide;
            GameEvents.SettingsChanged -= _onSettings;
        }

        public override void ResetState()
        {
            for (var i = 0; i < Pool; i++) Kill(_entries[i]);
        }

        public override void Relayout(Vector2 canvasSize)
        {
            _width = HudLayout.SubtitleWidth(canvasSize);
            Root.sizeDelta = new Vector2(_width, HudLayout.SubtitleStackHeight);
            for (var i = 0; i < Pool; i++)
                if (_entries[i].Live) Measure(_entries[i]);
        }

        private void OnSettingsChanged()
        {
            var s = Ctx.CurrentSettings;
            var size = s != null ? s.SubtitleSize : SubtitleSize.Medium;
            if (size == _size) return;
            _size = size;
            for (var i = 0; i < Pool; i++)
                if (_entries[i].Live) Measure(_entries[i]);
        }

        /// <summary>Shows a subtitle (event handler; also used by the test bed).</summary>
        public void Show(SubtitleData d)
        {
            if (!SubtitleFormatter.ShouldShow(d, Ctx.CurrentSettings)) return;
            if (string.IsNullOrEmpty(d.Text)) return;

            // Same handle again: update in place.
            Entry e = null;
            for (var i = 0; i < Pool; i++)
                if (_entries[i].Live && _entries[i].Handle == d.Handle && d.Handle != 0) e = _entries[i];
            if (e == null)
            {
                e = Acquire();
                e.Y = 0f;
            }

            e.Handle = d.Handle;
            e.Age = 0f;
            e.Duration = d.Duration;
            e.Live = true;
            e.Fading = false;
            e.FadeAge = 0f;
            e.Order = ++_order;
            if (!e.Rect.gameObject.activeSelf) e.Rect.gameObject.SetActive(true);

            if (e.Monospace != d.Monospace || e.Text.font == null)
            {
                e.Monospace = d.Monospace;
                UIFonts.Apply(e.Text, Ctx.Fonts, d.Monospace ? FontRole.Mono : FontRole.CondensedMedium, true);
            }
            e.HasRadio = d.Radio;
            e.Radio.enabled = d.Radio;
            e.Radio.color = UIColors.WithAlpha(SubtitleFormatter.ColorOf(d), 0.9f);
            SubtitleFormatter.Write(e.Buffer, d, UIColors.Ink);
            e.Buffer.ApplyTo(e.Text);
            Measure(e);

            // Only the newest SubtitleMaxEntries stay; older ones fade.
            var live = 0;
            for (var i = 0; i < Pool; i++) if (_entries[i].Live && !_entries[i].Fading) live++;
            while (live > HudLayout.SubtitleMaxEntries)
            {
                Entry oldest = null;
                for (var i = 0; i < Pool; i++)
                {
                    var x = _entries[i];
                    if (x.Live && !x.Fading && (oldest == null || x.Order < oldest.Order)) oldest = x;
                }
                if (oldest == null) break;
                oldest.Fading = true;
                oldest.FadeAge = 0f;
                live--;
            }
        }

        /// <summary>Hides a subtitle by handle (event handler).</summary>
        public void Hide(int handle)
        {
            for (var i = 0; i < Pool; i++)
            {
                var e = _entries[i];
                if (e.Live && e.Handle == handle && !e.Fading)
                {
                    e.Fading = true;
                    e.FadeAge = 0f;
                }
            }
        }

        private Entry Acquire()
        {
            Entry free = null;
            Entry oldest = null;
            for (var i = 0; i < Pool; i++)
            {
                var e = _entries[i];
                if (!e.Live)
                {
                    free = e;
                    break;
                }
                if (oldest == null || e.Order < oldest.Order) oldest = e;
            }
            return free ?? oldest;
        }

        private void Measure(Entry e)
        {
            var fontSize = SubtitleFormatter.FontSize(_size);
            var textWidth = _width - 2f * PadX - (e.HasRadio ? IconSize + IconGap : 0f);
            e.Text.fontSize = fontSize;
            var pref = e.Text.GetPreferredValues(textWidth, 0f);
            var lineHeight = fontSize * 1.2f;
            // Two lines at most (GDD §11): shrink in steps, then let TMP ellipsise.
            for (var step = 0; step < 5 && pref.y > lineHeight * 2.15f; step++)
            {
                fontSize *= 0.92f;
                lineHeight = fontSize * 1.2f;
                e.Text.fontSize = fontSize;
                pref = e.Text.GetPreferredValues(textWidth, 0f);
            }
            var w = Mathf.Min(textWidth, pref.x + 2f);
            var h = Mathf.Min(pref.y, lineHeight * 2.2f);
            e.Text.rectTransform.sizeDelta = new Vector2(w, h + 4f);
            var plateWidth = w + 2f * PadX + (e.HasRadio ? IconSize + IconGap : 0f);
            e.Rect.sizeDelta = new Vector2(plateWidth, h + 2f * PadY);
            var left = -plateWidth * 0.5f + PadX;
            e.Radio.rectTransform.anchoredPosition = new Vector2(PadX, 0f);
            e.Text.rectTransform.anchoredPosition = new Vector2(left + (e.HasRadio ? IconSize + IconGap : 0f) + w * 0.5f, 0f);
            e.Height = h + 2f * PadY;
        }

        public override void Tick(float dt)
        {
            var pause = Ctx.Pause.Value;
            var step = pause != null && pause.IsPaused ? 0f : dt;
            // Stack: newest at the bottom.
            var y = 0f;
            for (var rank = 0; rank < Pool; rank++)
            {
                Entry next = null;
                for (var i = 0; i < Pool; i++)
                {
                    var e = _entries[i];
                    if (!e.Live || e.Order <= 0) continue;
                    if (IsRanked(e, rank)) next = e;
                }
                if (next == null) break;
                next.Y = UIAnim.Damp(next.Y, y, 18f, dt);
                next.Rect.anchoredPosition = new Vector2(0f, next.Y);
                y += next.Height + 8f;
            }

            for (var i = 0; i < Pool; i++)
            {
                var e = _entries[i];
                if (!e.Live) continue;
                e.Age += step;
                if (!e.Fading && e.Duration > 0f && e.Age > e.Duration + Linger)
                {
                    e.Fading = true;
                    e.FadeAge = 0f;
                }
                float a;
                if (e.Fading)
                {
                    e.FadeAge += dt;
                    a = 1f - Mathf.Clamp01(e.FadeAge / FadeOut);
                    if (e.FadeAge >= FadeOut)
                    {
                        Kill(e);
                        continue;
                    }
                }
                else
                {
                    a = Mathf.Clamp01(e.Age / FadeIn);
                    if (step <= 0f && e.Age <= 0f) a = 1f;
                }
                e.Group.alpha = a;
            }
        }

        private bool IsRanked(Entry e, int rank)
        {
            // rank 0 = newest; count how many live entries are newer.
            var newer = 0;
            for (var i = 0; i < Pool; i++)
            {
                var o = _entries[i];
                if (o != e && o.Live && o.Order > e.Order) newer++;
            }
            return newer == rank;
        }

        private static void Kill(Entry e)
        {
            e.Live = false;
            e.Fading = false;
            e.Order = 0;
            e.Group.alpha = 0f;
            e.Rect.gameObject.SetActive(false);
        }

        public override void CollectLayout(List<HudLayoutRegion> regions)
        {
            regions.Add(HudLayoutRegion.Of("Subtitles", Root, Ctx.CanvasRect));
        }
    }
}
