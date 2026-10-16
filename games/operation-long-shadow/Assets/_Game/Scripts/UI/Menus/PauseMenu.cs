using LongShadow.Core;
using LongShadow.UI.Common;
using LongShadow.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Menus
{
    /// <summary>
    /// Pause menu (GDD §11): opens on <see cref="GameEvents.PauseChanged"/>(true) (pointer-lock loss / Esc, WP01) as a dark
    /// CRT panel: RESUME · RESTART CHECKPOINT · OPTIONS · QUIT TO SESSION. RESUME calls <see cref="IPauseService.Resume"/>
    /// from the click; while the browser's re-lock cooldown runs (Chrome refuses a new lock for about 1 s after Esc) it
    /// shows a "CLICK TO RESUME" overlay instead and resumes on the next click. The cooldown comes from
    /// <see cref="IPauseService.RelockCooldown"/>; without a pause service it is estimated from the pause time. RESTART and
    /// QUIT ask for confirmation.
    /// </summary>
    public sealed class PauseMenu : HudView
    {
        /// <summary>Estimated Chrome re-lock cooldown when no pause service is registered (seconds after the pause began).</summary>
        public const float EstimatedRelockCooldown = 1.1f;

        private enum State { Closed, Main, Confirm, Options, ClickToResume }

        private readonly OptionsMenu _options;
        private RectTransform _panel;
        private CrtButton _resume, _restart, _optionsButton, _quit;
        private RectTransform _confirm;
        private TextMeshProUGUI _confirmText;
        private CrtButton _yes, _no;
        private RectTransform _resumeOverlay;
        private TextMeshProUGUI _resumeText;
        private TextMeshProUGUI _status;
        private readonly TextBuffer _statusText = new TextBuffer(96);
        private State _state;
        private bool _confirmQuit;
        private float _pausedAt;
        private bool _cooldownShown = true;
        private System.Action<bool> _onPause;

        /// <summary>Creates the pause menu around an options menu built next to it.</summary>
        public PauseMenu(OptionsMenu options)
        {
            _options = options;
        }

        /// <summary>True while open (any sub-state).</summary>
        public bool IsOpen => _state != State.Closed;

        /// <summary>True while the click-to-resume overlay waits for a click (tests).</summary>
        public bool WaitingForClick => _state == State.ClickToResume;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("Pause", parent);
            UIFactory.StretchImage("Dim", Root, Ctx.Sprite(UISpriteId.Square), new Color(0f, 0f, 0f, 0.55f));

            _panel = UIFactory.Rect("Panel", Root, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(780f, 520f));
            UIFactory.Group(_panel.gameObject, 1f, true);
            var glass = UIFactory.StretchImage("Glass", _panel, Ctx.Sprite(UISpriteId.CrtPanel), Color.white);
            glass.type = Image.Type.Sliced;
            var title = UIFactory.Text("Title", _panel, Ctx.Fonts, MenuStyle.Font, 32f, MenuStyle.Text, TextAlignmentOptions.TopLeft,
                UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(56f, -40f), new Vector2(680f, 42f), false, MenuStyle.Glow);
            title.SetText("// SESSION 12 — TAPE HOLD");
            _status = UIFactory.Text("Status", _panel, Ctx.Fonts, MenuStyle.Font, MenuStyle.SmallSize, UIColors.WithAlpha(UIColors.Phosphor, 0.6f), TextAlignmentOptions.TopLeft,
                UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(58f, -86f), new Vector2(680f, 26f), false);
            UIFactory.Image("Rule", _panel, Ctx.Sprite(UISpriteId.Square), UIColors.WithAlpha(UIColors.Phosphor, 0.35f), UIFactory.TopLeft, UIFactory.TopLeft,
                new Vector2(56f, -122f), new Vector2(668f, 2f));

            _resume = Item("Resume", "RESUME", 0);
            _restart = Item("Restart", "RESTART CHECKPOINT", 1);
            _optionsButton = Item("Options", "OPTIONS", 2);
            _quit = Item("Quit", "QUIT TO SESSION", 3);
            _resume.Clicked += OnResume;
            _restart.Clicked += () => AskConfirm(false);
            _optionsButton.Clicked += OpenOptions;
            _quit.Clicked += () => AskConfirm(true);
            Chain(_resume, _restart, _optionsButton, _quit);

            // Confirmation.
            _confirm = UIFactory.Rect("Confirm", _panel, UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(56f, -150f), new Vector2(668f, 260f));
            _confirmText = UIFactory.Text("Text", _confirm, Ctx.Fonts, MenuStyle.Font, 26f, MenuStyle.Text, TextAlignmentOptions.TopLeft,
                UIFactory.TopLeft, UIFactory.TopLeft, Vector2.zero, new Vector2(668f, 110f), false, MenuStyle.Glow);
            _confirmText.textWrappingMode = TextWrappingModes.Normal;
            _yes = CrtButton.Create("Yes", _confirm, Ctx.Fonts, Ctx.Theme, "YES", UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(0f, -140f), new Vector2(200f, MenuStyle.ItemHeight), Ctx.Audio);
            _no = CrtButton.Create("No", _confirm, Ctx.Fonts, Ctx.Theme, "NO", UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(240f, -140f), new Vector2(200f, MenuStyle.ItemHeight), Ctx.Audio);
            _yes.Clicked += OnConfirmed;
            _no.Clicked += ShowMain;
            _yes.Cancelled += ShowMain;
            _no.Cancelled += ShowMain;
            _yes.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _no, selectOnLeft = _no };
            _no.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _yes, selectOnLeft = _yes };
            _confirm.gameObject.SetActive(false);

            CrtScreen.Add(_panel, Ctx.Theme, Ctx.Theme.ScanlineStrength * 0.6f, Ctx.Theme.CrtFlicker, true, true);

            // Click-to-resume overlay (full screen so any click counts).
            _resumeOverlay = UIFactory.Stretch("ClickToResume", Root);
            UIFactory.StretchImage("Dim", _resumeOverlay, Ctx.Sprite(UISpriteId.Square), new Color(0f, 0f, 0f, 0.35f));
            _resumeText = UIFactory.Text("Text", _resumeOverlay, Ctx.Fonts, MenuStyle.Font, 40f, MenuStyle.Text, TextAlignmentOptions.Center,
                UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(1200f, 60f), false, MenuStyle.Glow);
            _resumeText.characterSpacing = 8f;
            var catcher = ClickCatcher.Create("Catcher", _resumeOverlay);
            catcher.Click += OnOverlayClick;
            _resumeOverlay.gameObject.SetActive(false);

            Root.gameObject.SetActive(false);
        }

        private CrtButton Item(string name, string text, int index)
        {
            var b = CrtButton.Create(name, _panel, Ctx.Fonts, Ctx.Theme, text, UIFactory.TopLeft, UIFactory.TopLeft,
                new Vector2(48f, -150f - index * (MenuStyle.ItemHeight + 12f)), new Vector2(480f, MenuStyle.ItemHeight), Ctx.Audio);
            return b;
        }

        private static void Chain(params CrtButton[] items)
        {
            for (var i = 0; i < items.Length; i++)
            {
                items[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = items[(i + items.Length - 1) % items.Length],
                    selectOnDown = items[(i + 1) % items.Length],
                };
            }
        }

        public override void Enable()
        {
            _onPause ??= OnPauseChanged;
            GameEvents.PauseChanged += _onPause;
        }

        public override void Disable() => GameEvents.PauseChanged -= _onPause;

        private void OnPauseChanged(bool paused)
        {
            if (paused) Open();
            else Close();
        }

        /// <summary>Opens the menu (normally through <see cref="GameEvents.PauseChanged"/>).</summary>
        public void Open()
        {
            if (_state != State.Closed) return;
            _pausedAt = Time.unscaledTime;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UpdateStatus();
            var director = Ctx.Director.Value;
            var running = director == null || director.IsRunning;
            _restart.interactable = Ctx.Checkpoints.Value != null && running;
            _quit.interactable = running;
            ShowMain();
        }

        /// <summary>Closes the menu and every sub-panel.</summary>
        public void Close()
        {
            if (_state == State.Closed) return;
            if (_options.IsOpen) _options.Close();
            _state = State.Closed;
            Root.gameObject.SetActive(false);
            MenuSelection.Clear();
        }

        private void ShowMain()
        {
            _state = State.Main;
            _panel.gameObject.SetActive(true);
            SetItemsVisible(true);
            _confirm.gameObject.SetActive(false);
            _resumeOverlay.gameObject.SetActive(false);
            MenuSelection.Select(_resume.gameObject);
        }

        private void SetItemsVisible(bool on)
        {
            _resume.gameObject.SetActive(on);
            _restart.gameObject.SetActive(on);
            _optionsButton.gameObject.SetActive(on);
            _quit.gameObject.SetActive(on);
        }

        private void AskConfirm(bool quit)
        {
            _confirmQuit = quit;
            _state = State.Confirm;
            SetItemsVisible(false);
            _confirm.gameObject.SetActive(true);
            _confirmText.SetText(quit
                ? "QUIT TO SESSION?\nPROGRESS SINCE THE LAST CHECKPOINT IS LOST."
                : "RESTART FROM THE LAST CHECKPOINT?");
            MenuSelection.Select(_no.gameObject);
        }

        private void OnConfirmed()
        {
            var pause = Ctx.Pause.Value;
            if (_confirmQuit)
            {
                // CanPause = false unpauses without asking for the pointer (WP01 PauseService); the click gate's own
                // click re-locks it when the player starts again.
                if (pause != null) pause.CanPause = false;
                Close();
                Ctx.Director.Value?.QuitToSession();
                var gate = Ctx.Gate.Value;
                if (gate != null)
                {
                    gate.SetVisible(true);
                    gate.SetReady(true);
                }
                GameEvents.RaiseMissionEvent(UiEvents.PauseQuit);
            }
            else
            {
                pause?.Resume();
                Close();
                Ctx.Checkpoints.Value?.Reload(false); // GDD §17: the same reload, without the death insert
                GameEvents.RaiseMissionEvent(UiEvents.PauseRestart);
            }
        }

        private void OpenOptions()
        {
            _state = State.Options;
            _panel.gameObject.SetActive(false);
            _options.Open(() =>
            {
                if (_state == State.Options)
                {
                    _panel.gameObject.SetActive(true);
                    ShowMain();
                    MenuSelection.Select(_optionsButton.gameObject);
                }
            });
        }

        /// <summary>Seconds until a click may re-lock the pointer.</summary>
        public float RelockCooldown
        {
            get
            {
                var pause = Ctx.Pause.Value;
                if (pause != null) return Mathf.Max(0f, pause.RelockCooldown);
                return Mathf.Max(0f, EstimatedRelockCooldown - (Time.unscaledTime - _pausedAt));
            }
        }

        private void OnResume()
        {
            if (RelockCooldown > 0.02f)
            {
                _state = State.ClickToResume;
                _resumeOverlay.gameObject.SetActive(true);
                _resumeOverlay.SetAsLastSibling();
                _cooldownShown = true;
                _resumeText.SetText("STAND BY");
                MenuSelection.Clear();
                return;
            }
            DoResume();
        }

        private void OnOverlayClick()
        {
            if (_state != State.ClickToResume) return;
            if (RelockCooldown > 0.02f) return; // the browser would refuse the lock; wait for the next click
            DoResume();
        }

        private void DoResume()
        {
            var pause = Ctx.Pause.Value;
            if (pause != null) pause.Resume();
            else Close();
        }

        public override void Tick(float dt)
        {
            if (_state != State.ClickToResume) return;
            var waiting = RelockCooldown > 0.02f;
            if (waiting != _cooldownShown)
            {
                _cooldownShown = waiting;
                _resumeText.SetText(waiting ? "STAND BY" : "CLICK TO RESUME");
            }
            _resumeText.alpha = waiting ? 0.55f : UIAnim.Blink(Time.unscaledTime, 1.06f) ? 1f : 0.35f;
        }

        private void UpdateStatus()
        {
            _statusText.Clear();
            var cp = Ctx.Checkpoints.Value?.Last;
            _statusText.Append("CHECKPOINT ").Append(string.IsNullOrEmpty(cp) ? "----" : cp);
            var light = Ctx.Light.Value;
            var director = Ctx.Director.Value;
            var minutes = light != null ? light.StoryClockMinutes : director != null ? director.StoryClockMinutes : -1f;
            if (minutes >= 0f)
            {
                var m = Mathf.FloorToInt(minutes);
                _statusText.Append("  ·  ").AppendPadded(m / 60 % 24, 2).Append(':').AppendPadded(m % 60, 2).Append(" LOCAL");
            }
            var settings = Ctx.CurrentSettings;
            if (settings != null) _statusText.Append("  ·  ").Append(DifficultyName(settings.Difficulty));
            _statusText.ApplyTo(_status);
        }

        private static string DifficultyName(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Recruit: return "RECRUIT";
                case Difficulty.Hardened: return "HARDENED";
                case Difficulty.Veteran: return "VETERAN";
                default: return "REGULAR";
            }
        }

        /// <summary>Test hooks.</summary>
        internal void PressResume() => OnResume();
        internal void ClickOverlay() => OnOverlayClick();
        internal void ConfirmQuit()
        {
            AskConfirm(true);
            OnConfirmed();
        }
    }
}
