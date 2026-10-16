using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// Fallback <see cref="IClickGate"/> used until WP08's ClickGateView exists (GDD §8.1). On the web it drives the
    /// CovertOps template's HTML loader, which already shows the engine download as number groups and becomes the click
    /// gate: the click is handled by the page inside the browser gesture (pointer lock, audio unlock, optional
    /// fullscreen) and reported back through <see cref="WebBridge.HtmlGateClicked"/>. In the editor it draws the same
    /// gate with IMGUI. <see cref="Bootstrap"/> registers it only when no other gate is registered.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HtmlClickGate : MonoBehaviour, IClickGate
    {
        /// <summary>Page flag: the gate accepts the click.</summary>
        internal const int ReadyFlag = 1;
        /// <summary>Page flag: request fullscreen inside the click.</summary>
        internal const int FullscreenFlag = 2;
        /// <summary>Page flag: request pointer lock inside the click.</summary>
        internal const int LockFlag = 4;

        [SerializeField] private string _title = "SESSION 12 // SUBJECT S-3";
        [SerializeField] private string _readyText = "CLICK TO BEGIN";
        [SerializeField] private string _footer = "HEADPHONES RECOMMENDED · MOUSE + KEYBOARD · ESC PAUSES";

        private float _progress;
        private bool _ready;
        private bool _visible = true;
        private bool _fullscreenOnClick;
        private bool _lockOnClick = true;
        private string _label = string.Empty;
        private GUIStyle _style;
        private Texture2D _black;

        /// <inheritdoc/>
        public event Action Clicked;

        /// <summary>True while the gate is shown.</summary>
        public bool Visible => _visible;

        private void OnEnable() => WebBridge.HtmlGateClicked += OnPageClicked;
        private void OnDisable() => WebBridge.HtmlGateClicked -= OnPageClicked;

        private void OnDestroy()
        {
            if (_black != null) Destroy(_black);
        }

        /// <summary>What the page does inside the click: fullscreen (Options) and pointer lock (off with ?nolock=1).</summary>
        public void ConfigureClick(bool fullscreen, bool pointerLock)
        {
            _fullscreenOnClick = fullscreen;
            _lockOnClick = pointerLock;
            if (_ready) PushReady();
        }

        /// <summary>Status line shown under the number groups ("RECEIVING S0_BOOTH").</summary>
        public void SetLabel(string label)
        {
            _label = label ?? string.Empty;
            WebNative.LoaderProgress(_progress, _label);
        }

        /// <inheritdoc/>
        public void SetProgress(float progress01)
        {
            _progress = Mathf.Clamp01(progress01);
            WebNative.LoaderProgress(_progress, _label);
        }

        /// <inheritdoc/>
        public void SetReady(bool ready)
        {
            _ready = ready;
            PushReady();
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            enabled = visible; // no IMGUI pass while hidden; re-enabled (and re-subscribed) when shown again
            WebNative.LoaderVisible(visible);
            if (visible) PushReady();
        }

        /// <summary>Shows a fatal boot error on the page (never an alert dialog).</summary>
        public void ShowError(string message)
        {
            _label = message ?? string.Empty;
            WebNative.LoaderError(_label);
        }

        private void PushReady()
        {
            var flags = 0;
            if (_ready) flags |= ReadyFlag;
            if (_fullscreenOnClick) flags |= FullscreenFlag;
            if (_lockOnClick) flags |= LockFlag;
            WebNative.LoaderReady(flags);
        }

        private void OnPageClicked()
        {
            if (!_ready) return;
            _visible = false;
            Clicked?.Invoke();
            enabled = _visible;
        }

        private void OnGUI()
        {
            if (WebNative.Available || !_visible) return;
            if (_style == null)
            {
                _black = new Texture2D(1, 1);
                _black.SetPixel(0, 0, Color.black);
                _black.Apply();
                _style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(14, Screen.height / 40),
                    richText = false,
                };
                _style.normal.textColor = new Color(0.55f, 0.89f, 0.55f);
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _black);
            var groups = Mathf.RoundToInt(_progress * 12f);
            var bar = new string('■', groups) + new string('·', 12 - groups);
            var status = _ready ? _readyText + "_" : "ESTABLISHING LINK " + Mathf.RoundToInt(_progress * 100f) + "%";
            var text = _title + "\n\n" + bar + "\n" + _label + "\n\n" + status + "\n\n" + _footer;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), text, _style);
            var e = Event.current;
            if (_ready && e != null && (e.type == EventType.MouseDown || (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space))))
            {
                e.Use();
                _visible = false;
                Clicked?.Invoke();
                enabled = _visible;
            }
        }
    }
}
