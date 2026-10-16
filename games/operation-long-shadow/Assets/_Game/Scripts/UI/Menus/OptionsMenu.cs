using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using LongShadow.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Menus
{
    /// <summary>
    /// Options (GDD §11): every option bound to <see cref="ISettingsService"/> — mouse and ADS sensitivity, invert Y, aim
    /// mode, FOV, render scale, performance mode, motion blur, film grain, brightness, fullscreen, the four volumes,
    /// subtitles, subtitle size, enemy-chatter translation, difficulty — and the controls list. A change is applied at
    /// once from the input event (<c>Apply</c> inside the browser gesture, so fullscreen works) and saved on BACK.
    /// Without a settings service it edits a local copy (test scenes). Terminal look: green phosphor CRT panel, tabs,
    /// inverse-video selection.
    /// </summary>
    public sealed class OptionsMenu : HudView
    {
        private static readonly Vector2 PanelSize = new Vector2(1060f, 740f);
        private const float RowWidth = 920f;
        private const float LabelWidth = 470f;

        private sealed class Row
        {
            public OptionDef Def;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Caret;
            public CrtSlider Slider;
            public CrtStepper Stepper;
            public Selectable Control => Slider != null ? (Selectable)Slider : Stepper;
        }

        private RectTransform _panel;
        private RectTransform _tabMarker;
        private float _tabWidth;
        private readonly CrtButton[] _tabs = new CrtButton[5];
        private readonly RectTransform[] _pages = new RectTransform[5];
        private readonly List<Row>[] _rowsByPage = new List<Row>[5];
        private readonly List<Row> _rows = new List<Row>();
        private CrtButton _back;
        private CrtButton _defaults;
        private TextMeshProUGUI _note;
        private GameSettings _local;
        private int _page;
        private Action _onClosed;

        /// <summary>True while open.</summary>
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        /// <summary>Page shown (tests).</summary>
        public int Page => _page;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("Options", parent);
            UIFactory.StretchImage("Dim", Root, Ctx.Sprite(UISpriteId.Square), new Color(0f, 0f, 0f, 0.6f));
            _panel = UIFactory.Rect("Panel", Root, UIFactory.Center, UIFactory.Center, Vector2.zero, PanelSize);
            UIFactory.Group(_panel.gameObject, 1f, true);
            var bg = UIFactory.StretchImage("Glass", _panel, Ctx.Sprite(UISpriteId.CrtPanel), Color.white);
            bg.type = Image.Type.Sliced;

            var title = UIFactory.Text("Title", _panel, Ctx.Fonts, MenuStyle.Font, 34f, MenuStyle.Text, TextAlignmentOptions.TopLeft,
                UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(56f, -36f), new Vector2(600f, 44f), false, MenuStyle.Glow);
            title.SetText("// OPTIONS");
            var tag = UIFactory.Text("Tag", _panel, Ctx.Fonts, MenuStyle.Font, MenuStyle.SmallSize, UIColors.PhosphorDim, TextAlignmentOptions.TopRight,
                UIFactory.TopRight, UIFactory.TopRight, new Vector2(-56f, -44f), new Vector2(400f, 26f), false);
            tag.SetText("SESSION 12 // SUBJECT S-3");

            // Tabs.
            var tabWidth = (RowWidth + 20f) / OptionsModel.Pages.Length;
            for (var i = 0; i < OptionsModel.Pages.Length; i++)
            {
                var page = i;
                var b = CrtButton.Create("Tab" + i, _panel, Ctx.Fonts, Ctx.Theme, OptionsModel.Pages[i], UIFactory.TopLeft, UIFactory.TopLeft,
                    new Vector2(60f + i * tabWidth, -96f), new Vector2(tabWidth - 8f, 42f), Ctx.Audio, 22f);
                b.Clicked += () => ShowPage(page, true);
                b.Cancelled += Close;
                _tabs[i] = b;
            }
            UIFactory.Image("TabRule", _panel, Ctx.Sprite(UISpriteId.Square), UIColors.WithAlpha(UIColors.Phosphor, 0.35f), UIFactory.TopLeft, UIFactory.TopLeft,
                new Vector2(60f, -144f), new Vector2(RowWidth + 12f, 2f));
            _tabWidth = tabWidth;
            _tabMarker = UIFactory.Image("TabMarker", _panel, Ctx.Sprite(UISpriteId.Square), UIColors.PhosphorBright, UIFactory.TopLeft, UIFactory.TopLeft,
                new Vector2(60f, -140f), new Vector2(tabWidth - 8f, 4f)).rectTransform;

            // Pages.
            for (var p = 0; p < OptionsModel.Pages.Length; p++)
            {
                _pages[p] = UIFactory.Rect("Page" + p, _panel, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(70f, -168f), new Vector2(RowWidth, 470f));
                _rowsByPage[p] = new List<Row>();
                if (OptionsModel.Pages[p] == OptionsModel.KeysPage) BuildKeys(_pages[p]);
                else BuildRows(p);
                _pages[p].gameObject.SetActive(false);
            }

            _note = UIFactory.Text("Note", _panel, Ctx.Fonts, MenuStyle.Font, MenuStyle.SmallSize, UIColors.WithAlpha(UIColors.Phosphor, 0.7f), TextAlignmentOptions.BottomLeft,
                UIFactory.BottomLeft, UIFactory.BottomLeft, new Vector2(70f, 104f), new Vector2(RowWidth, 26f), false);

            _back = CrtButton.Create("Back", _panel, Ctx.Fonts, Ctx.Theme, "BACK", UIFactory.BottomLeft, UIFactory.BottomLeft,
                new Vector2(60f, 36f), new Vector2(200f, MenuStyle.ItemHeight), Ctx.Audio);
            _back.Clicked += Close;
            _back.Cancelled += Close;
            _defaults = CrtButton.Create("Defaults", _panel, Ctx.Fonts, Ctx.Theme, "RESET DEFAULTS", UIFactory.BottomRight, UIFactory.BottomRight,
                new Vector2(-60f, 36f), new Vector2(330f, MenuStyle.ItemHeight), Ctx.Audio);
            _defaults.Clicked += ResetDefaults;
            _defaults.Cancelled += Close;

            CrtScreen.Add(_panel, Ctx.Theme, Ctx.Theme.ScanlineStrength * 0.6f, Ctx.Theme.CrtFlicker, true, true);
            Root.gameObject.SetActive(false);
        }

        private void BuildRows(int page)
        {
            var name = OptionsModel.Pages[page];
            var y = 0f;
            for (var i = 0; i < OptionsModel.All.Length; i++)
            {
                var def = OptionsModel.All[i];
                if (def.Page != name) continue;
                var row = new Row { Def = def };
                var rt = UIFactory.Rect(def.Id, _pages[page], UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(0f, -y), new Vector2(RowWidth, MenuStyle.RowHeight));
                row.Caret = UIFactory.Text("Caret", rt, Ctx.Fonts, MenuStyle.Font, MenuStyle.RowSize, UIColors.PhosphorBright, TextAlignmentOptions.MidlineLeft,
                    UIFactory.MiddleLeft, UIFactory.MiddleLeft, Vector2.zero, new Vector2(24f, MenuStyle.RowHeight), false);
                row.Caret.SetText(">");
                row.Caret.enabled = false;
                row.Label = UIFactory.Text("Label", rt, Ctx.Fonts, MenuStyle.Font, MenuStyle.RowSize, MenuStyle.Text, TextAlignmentOptions.MidlineLeft,
                    UIFactory.MiddleLeft, UIFactory.MiddleLeft, new Vector2(28f, 0f), new Vector2(LabelWidth, MenuStyle.RowHeight), false, MenuStyle.Glow);
                row.Label.SetText(def.Label);
                var controlSize = new Vector2(RowWidth - LabelWidth - 40f, MenuStyle.RowHeight - 6f);
                if (def.Kind == OptionKind.Slider)
                {
                    row.Slider = CrtSlider.Create("Slider", rt, Ctx.Fonts, Ctx.Theme, UIFactory.MiddleRight, UIFactory.MiddleRight, Vector2.zero, controlSize, Ctx.Audio);
                    row.Slider.Configure(def.Min, def.Max, def.Step, def.Formatter);
                    row.Slider.ValueChanged += v => OnChanged(def, v);
                    row.Slider.Cancelled += Close;
                    row.Slider.HighlightChanged += on => Highlight(row, on);
                }
                else
                {
                    row.Stepper = CrtStepper.Create("Stepper", rt, Ctx.Fonts, UIFactory.MiddleRight, UIFactory.MiddleRight, Vector2.zero, controlSize, Ctx.Audio);
                    row.Stepper.Configure(def.Choices);
                    row.Stepper.ValueChanged += v => OnChanged(def, v);
                    row.Stepper.Cancelled += Close;
                    row.Stepper.HighlightChanged += on => Highlight(row, on);
                }
                _rowsByPage[page].Add(row);
                _rows.Add(row);
                y += MenuStyle.RowHeight + 6f;
            }
        }

        private void BuildKeys(RectTransform page)
        {
            var n = OptionsModel.Controls.GetLength(0);
            for (var i = 0; i < n; i++)
            {
                var y = -i * 30f;
                var label = UIFactory.Text("Action" + i, page, Ctx.Fonts, MenuStyle.Font, 21f, MenuStyle.Text, TextAlignmentOptions.MidlineLeft,
                    UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(28f, y), new Vector2(520f, 30f), false, MenuStyle.Glow);
                var b = new TextBuffer(64);
                b.Append(OptionsModel.Controls[i, 0]).Append(' ');
                b.Append('.', Mathf.Max(2, 26 - OptionsModel.Controls[i, 0].Length));
                b.ApplyTo(label);
                var key = UIFactory.Text("Key" + i, page, Ctx.Fonts, MenuStyle.Font, 21f, UIColors.PhosphorBright, TextAlignmentOptions.MidlineLeft,
                    UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(520f, y), new Vector2(380f, 30f), false, MenuStyle.Glow);
                key.SetText(OptionsModel.Controls[i, 1]);
            }
        }

        /// <summary>Opens the menu; <paramref name="onClosed"/> runs after BACK (settings are saved first).</summary>
        public void Open(Action onClosed)
        {
            _onClosed = onClosed;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            PullValues();
            ShowPage(_page, false);
            var rows = _rowsByPage[_page];
            MenuSelection.Select(rows.Count > 0 ? rows[0].Control.gameObject : _tabs[_page].gameObject);
        }

        /// <summary>Closes the menu (saves the settings).</summary>
        public void Close()
        {
            if (!IsOpen) return;
            Ctx.Settings.Value?.Save();
            Root.gameObject.SetActive(false);
            var cb = _onClosed;
            _onClosed = null;
            cb?.Invoke();
        }

        /// <summary>Shows a page (tab).</summary>
        public void ShowPage(int page, bool selectFirst)
        {
            _page = Mathf.Clamp(page, 0, _pages.Length - 1);
            for (var i = 0; i < _pages.Length; i++) _pages[i].gameObject.SetActive(i == _page);
            _tabMarker.anchoredPosition = new Vector2(60f + _page * _tabWidth, -140f);
            WireNavigation();
            _note.SetText(string.Empty);
            if (selectFirst)
            {
                var rows = _rowsByPage[_page];
                if (rows.Count > 0) MenuSelection.Select(rows[0].Control.gameObject);
            }
        }

        private void WireNavigation()
        {
            var rows = _rowsByPage[_page];
            var firstControl = rows.Count > 0 ? rows[0].Control : (Selectable)_back;
            for (var i = 0; i < _tabs.Length; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = _tabs[(i + _tabs.Length - 1) % _tabs.Length],
                    selectOnRight = _tabs[(i + 1) % _tabs.Length],
                    selectOnDown = firstControl,
                };
                _tabs[i].navigation = nav;
            }
            for (var i = 0; i < rows.Count; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? rows[i - 1].Control : _tabs[_page],
                    selectOnDown = i < rows.Count - 1 ? rows[i + 1].Control : _back,
                };
                rows[i].Control.navigation = nav;
            }
            var last = rows.Count > 0 ? rows[rows.Count - 1].Control : (Selectable)_tabs[_page];
            _back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnRight = _defaults, selectOnLeft = _defaults };
            _defaults.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnLeft = _back, selectOnRight = _back };
        }

        private void Highlight(Row row, bool on)
        {
            row.Caret.enabled = on;
            row.Label.color = on ? UIColors.PhosphorBright : MenuStyle.Text;
            if (on) _note.SetText(row.Def.Note ?? string.Empty);
        }

        private GameSettings Current
        {
            get
            {
                var service = Ctx.Settings.Value;
                if (service != null && service.Current != null) return service.Current;
                return _local ??= new GameSettings();
            }
        }

        private void OnChanged(OptionDef def, float value)
        {
            var next = OptionsModel.With(Current, def, value);
            var service = Ctx.Settings.Value;
            if (service != null) service.Apply(next);
            else
            {
                _local = next;
                GameEvents.RaiseSettingsChanged();
            }
        }

        private void ResetDefaults()
        {
            var defaults = new GameSettings();
            var current = Current;
            // Keep what the defaults cannot know: the automatic render scale stays automatic.
            defaults.RenderScaleAuto = true;
            if (current != null) defaults.Version = current.Version;
            var service = Ctx.Settings.Value;
            if (service != null) service.Apply(defaults.Sanitize());
            else
            {
                _local = defaults.Sanitize();
                GameEvents.RaiseSettingsChanged();
            }
            PullValues();
        }

        /// <summary>Copies the settings into the controls (no change events).</summary>
        public void PullValues()
        {
            var s = Current;
            for (var i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                var v = r.Def.Get(s);
                if (r.Slider != null) r.Slider.SetValueWithoutNotify(v);
                else r.Stepper.SetIndexWithoutNotify(Mathf.RoundToInt(v));
            }
        }

        /// <summary>Test hook: changes an option as if the player did.</summary>
        internal void SetFromUser(string id, float value)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Def.Id != id) continue;
                OnChanged(_rows[i].Def, value);
                PullValues();
                return;
            }
        }
    }
}
