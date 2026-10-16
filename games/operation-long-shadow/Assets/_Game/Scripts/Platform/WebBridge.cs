using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// Receiver of the browser callbacks sent by <c>LongShadowWeb.jslib</c> through
    /// <c>SendMessage("WebBridge", method, value)</c> (TECH_PLAN §7.1), and the static front door to the jslib for the
    /// other Platform components. Lives on a root GameObject named exactly <see cref="ObjectName"/> in S_Boot (built by
    /// <c>BootSceneBuilder</c>). Callbacks are re-raised as static C# events; <see cref="PointerLockController"/> turns
    /// pointer-lock changes into <see cref="GameEvents.PointerLockChanged"/>.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform - 20)]
    [DisallowMultipleComponent]
    public sealed class WebBridge : MonoBehaviour
    {
        /// <summary>GameObject name the jslib sends messages to.</summary>
        public const string ObjectName = "WebBridge";

        /// <summary>Browser pointer lock changed (true = locked to the canvas).</summary>
        public static event Action<bool> PointerLockChanged;
        /// <summary>A pointer-lock request was refused (argument: DOMException name, e.g. "NotAllowedError").</summary>
        public static event Action<string> PointerLockError;
        /// <summary>Page visibility changed (true = visible).</summary>
        public static event Action<bool> VisibilityChanged;
        /// <summary>Fullscreen changed (true = fullscreen).</summary>
        public static event Action<bool> FullscreenChanged;
        /// <summary>The HTML click gate of the CovertOps template was clicked (inside the browser gesture).</summary>
        public static event Action HtmlGateClicked;

        private static WebBridge s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PointerLockChanged = null;
            PointerLockError = null;
            VisibilityChanged = null;
            FullscreenChanged = null;
            HtmlGateClicked = null;
            s_instance = null;
        }

        /// <summary>True in a WebGL player build (the jslib is linked); false in the editor.</summary>
        public static bool IsWebPlayer => WebNative.Available;

        /// <summary>The live bridge (null before S_Boot loads).</summary>
        public static WebBridge Instance => s_instance;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                LSLog.Warn(LogCategory.Platform, "Second WebBridge destroyed (only one may receive browser callbacks)", this);
                Destroy(gameObject);
                return;
            }
            s_instance = this;
            if (gameObject.name != ObjectName)
            {
                LSLog.Warn(LogCategory.Platform, "WebBridge GameObject renamed to '" + ObjectName + "' (SendMessage targets it by name)", this);
                gameObject.name = ObjectName;
            }
            if (transform.parent != null)
                LSLog.Warn(LogCategory.Platform, "WebBridge should be a root object of S_Boot", this);
            WebNative.Init(ObjectName);
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        // ---- SendMessage receivers (names are part of the jslib contract) --------------------------------------

        /// <summary>jslib: "1" locked, "0" released.</summary>
        public void OnPointerLockChanged(string value) => PointerLockChanged?.Invoke(value == "1");

        /// <summary>jslib: pointer lock refused; value is the error name.</summary>
        public void OnPointerLockError(string value)
        {
            LSLog.Verbose(LogCategory.Platform, "Pointer lock refused: " + value);
            PointerLockError?.Invoke(value ?? string.Empty);
        }

        /// <summary>jslib: "1" visible, "0" hidden.</summary>
        public void OnVisibilityChanged(string value) => VisibilityChanged?.Invoke(value == "1");

        /// <summary>jslib: "1" fullscreen, "0" windowed.</summary>
        public void OnFullscreenChanged(string value) => FullscreenChanged?.Invoke(value == "1");

        /// <summary>Template: the HTML click gate was clicked.</summary>
        public void OnHtmlGateClicked(string value) => HtmlGateClicked?.Invoke();

        // ---- static front door ----------------------------------------------------------------------------------

        /// <summary>Requests fullscreen on the page's stage element (must run inside or right after a user gesture).</summary>
        public static bool RequestFullscreen()
        {
            if (WebNative.Available) return WebNative.RequestFullscreen();
            Screen.fullScreen = true;
            return true;
        }

        /// <summary>Leaves fullscreen.</summary>
        public static void ExitFullscreen()
        {
            if (WebNative.Available) WebNative.ExitFullscreen();
            else Screen.fullScreen = false;
        }

        /// <summary>True while the page is fullscreen.</summary>
        public static bool IsFullscreen => WebNative.Available ? WebNative.IsFullscreen() : Screen.fullScreen;

        /// <summary>Resumes the WebAudio context (needs a user gesture on the web; no-op elsewhere).</summary>
        public static void ResumeAudio() => WebNative.ResumeAudio();

        /// <summary>True if the page runs inside an iframe (the artifact host frames it).</summary>
        public static bool InIframe => WebNative.IsInIframe();

        /// <summary>The browser user agent ("" outside the web).</summary>
        public static string UserAgent => WebNative.UserAgent() ?? string.Empty;

        /// <summary>True for Safari (WebKit without Chrome/Chromium/Firefox tokens): performance mode by default (TECH_PLAN §3.3).</summary>
        public static bool IsSafari
        {
            get
            {
                var ua = UserAgent;
                if (ua.Length == 0) return false;
                return ua.IndexOf("Safari", StringComparison.Ordinal) >= 0 &&
                       ua.IndexOf("Chrome", StringComparison.Ordinal) < 0 &&
                       ua.IndexOf("Chromium", StringComparison.Ordinal) < 0 &&
                       ua.IndexOf("CriOS", StringComparison.Ordinal) < 0 &&
                       ua.IndexOf("Firefox", StringComparison.Ordinal) < 0 &&
                       ua.IndexOf("Edg", StringComparison.Ordinal) < 0;
            }
        }

        /// <summary>Publishes the smoke-test result as <c>window.LS_SMOKE_RESULT</c> (TECH_PLAN §10.3).</summary>
        public static void SetSmokeResult(string json) => WebNative.SetSmokeResult(json);

        /// <summary>Total WebAssembly heap in bytes (0 outside the web).</summary>
        public static long HeapBytes => WebNative.HeapBytes();

        /// <summary>Build variant set by the template ("release", "qa"; null outside the web).</summary>
        public static string BuildVariant => WebNative.BuildVariant();
    }
}
