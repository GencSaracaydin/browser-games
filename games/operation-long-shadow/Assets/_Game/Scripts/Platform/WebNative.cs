#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace LongShadow.Platform
{
    /// <summary>
    /// The C# side of <c>Assets/_Game/Plugins/WebGL/LongShadowWeb.jslib</c> (TECH_PLAN §7.1). Every import is behind
    /// <c>UNITY_WEBGL &amp;&amp; !UNITY_EDITOR</c> (rule R7); the editor and other players get the neutral fallbacks below.
    /// The JS side never throws into C#: every function catches and returns a neutral value. Callbacks arrive as
    /// <c>SendMessage("WebBridge", ...)</c> (see <see cref="WebBridge"/>).
    /// </summary>
    internal static class WebNative
    {
        /// <summary>Gesture actions run by the page on the next pointerdown/keydown (LS_ArmGesture).</summary>
        internal const int GestureLock = 1, GestureFullscreen = 2, GestureAudio = 4;

        /// <summary>Every function the jslib must export (checked by an EditMode test against the jslib text).</summary>
        internal static readonly string[] ExportedFunctions =
        {
            "LS_Init", "LS_GetDevicePixelRatio", "LS_RequestPointerLock", "LS_ExitPointerLock", "LS_IsPointerLocked",
            "LS_PointerLockSupported", "LS_ArmGesture", "LS_RequestFullscreen", "LS_ExitFullscreen", "LS_IsFullscreen",
            "LS_ResumeAudio", "LS_StorageGet", "LS_StorageSet", "LS_StorageRemove", "LS_StorageAvailable", "LS_IsInIframe",
            "LS_GetQuery", "LS_GetUserAgent", "LS_SetSmokeResult", "LS_GetAssetSuffix", "LS_GetServedExtensions",
            "LS_GetBuildVariant", "LS_LoaderProgress", "LS_LoaderReady", "LS_LoaderVisible", "LS_LoaderError",
            "LS_GetHeapBytes",
        };

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>True in a WebGL player (the jslib is linked).</summary>
        internal static bool Available => true;

        [DllImport("__Internal")] private static extern void LS_Init(string bridgeObjectName);
        [DllImport("__Internal")] private static extern double LS_GetDevicePixelRatio();
        [DllImport("__Internal")] private static extern int LS_RequestPointerLock(int unadjustedMovement);
        [DllImport("__Internal")] private static extern void LS_ExitPointerLock();
        [DllImport("__Internal")] private static extern int LS_IsPointerLocked();
        [DllImport("__Internal")] private static extern int LS_PointerLockSupported();
        [DllImport("__Internal")] private static extern void LS_ArmGesture(int flags, int unadjustedMovement);
        [DllImport("__Internal")] private static extern int LS_RequestFullscreen();
        [DllImport("__Internal")] private static extern void LS_ExitFullscreen();
        [DllImport("__Internal")] private static extern int LS_IsFullscreen();
        [DllImport("__Internal")] private static extern void LS_ResumeAudio();
        [DllImport("__Internal")] private static extern string LS_StorageGet(string key);
        [DllImport("__Internal")] private static extern int LS_StorageSet(string key, string value);
        [DllImport("__Internal")] private static extern int LS_StorageRemove(string key);
        [DllImport("__Internal")] private static extern int LS_StorageAvailable();
        [DllImport("__Internal")] private static extern int LS_IsInIframe();
        [DllImport("__Internal")] private static extern string LS_GetQuery(string name);
        [DllImport("__Internal")] private static extern string LS_GetUserAgent();
        [DllImport("__Internal")] private static extern void LS_SetSmokeResult(string json);
        [DllImport("__Internal")] private static extern string LS_GetAssetSuffix();
        [DllImport("__Internal")] private static extern string LS_GetServedExtensions();
        [DllImport("__Internal")] private static extern string LS_GetBuildVariant();
        [DllImport("__Internal")] private static extern void LS_LoaderProgress(float progress01, string label);
        [DllImport("__Internal")] private static extern void LS_LoaderReady(int flags);
        [DllImport("__Internal")] private static extern void LS_LoaderVisible(int visible);
        [DllImport("__Internal")] private static extern void LS_LoaderError(string message);
        [DllImport("__Internal")] private static extern double LS_GetHeapBytes();

        internal static void Init(string bridgeObjectName) => LS_Init(bridgeObjectName);
        internal static float DevicePixelRatio() => (float)LS_GetDevicePixelRatio();
        internal static bool RequestPointerLock(bool unadjusted) => LS_RequestPointerLock(unadjusted ? 1 : 0) != 0;
        internal static void ExitPointerLock() => LS_ExitPointerLock();
        internal static bool IsPointerLocked() => LS_IsPointerLocked() != 0;
        internal static bool PointerLockSupported() => LS_PointerLockSupported() != 0;
        internal static void ArmGesture(int flags, bool unadjusted) => LS_ArmGesture(flags, unadjusted ? 1 : 0);
        internal static bool RequestFullscreen() => LS_RequestFullscreen() != 0;
        internal static void ExitFullscreen() => LS_ExitFullscreen();
        internal static bool IsFullscreen() => LS_IsFullscreen() != 0;
        internal static void ResumeAudio() => LS_ResumeAudio();
        internal static string StorageGet(string key) => LS_StorageGet(key);
        internal static bool StorageSet(string key, string value) => LS_StorageSet(key, value) != 0;
        internal static bool StorageRemove(string key) => LS_StorageRemove(key) != 0;
        internal static bool StorageAvailable() => LS_StorageAvailable() != 0;
        internal static bool IsInIframe() => LS_IsInIframe() != 0;
        internal static string GetQuery(string name) => LS_GetQuery(name ?? string.Empty);
        internal static string UserAgent() => LS_GetUserAgent();
        internal static void SetSmokeResult(string json) => LS_SetSmokeResult(json ?? "null");
        internal static string AssetSuffix() => LS_GetAssetSuffix();
        internal static string ServedExtensions() => LS_GetServedExtensions();
        internal static string BuildVariant() => LS_GetBuildVariant();
        internal static void LoaderProgress(float progress01, string label) => LS_LoaderProgress(progress01, label ?? string.Empty);
        internal static void LoaderReady(int flags) => LS_LoaderReady(flags);
        internal static void LoaderVisible(bool visible) => LS_LoaderVisible(visible ? 1 : 0);
        internal static void LoaderError(string message) => LS_LoaderError(message ?? string.Empty);
        internal static long HeapBytes() => (long)LS_GetHeapBytes();
#else
        /// <summary>True in a WebGL player (the jslib is linked).</summary>
        internal static bool Available => false;

        internal static void Init(string bridgeObjectName) { }
        internal static float DevicePixelRatio() => 1f;
        internal static bool RequestPointerLock(bool unadjusted) => false;
        internal static void ExitPointerLock() { }
        internal static bool IsPointerLocked() => false;
        internal static bool PointerLockSupported() => true;
        internal static void ArmGesture(int flags, bool unadjusted) { }
        internal static bool RequestFullscreen() => false;
        internal static void ExitFullscreen() { }
        internal static bool IsFullscreen() => false;
        internal static void ResumeAudio() { }
        internal static string StorageGet(string key) => null;
        internal static bool StorageSet(string key, string value) => false;
        internal static bool StorageRemove(string key) => false;
        internal static bool StorageAvailable() => false;
        internal static bool IsInIframe() => false;
        internal static string GetQuery(string name) => null;
        internal static string UserAgent() => string.Empty;
        internal static void SetSmokeResult(string json) { }
        internal static string AssetSuffix() => null;
        internal static string ServedExtensions() => null;
        internal static string BuildVariant() => null;
        internal static void LoaderProgress(float progress01, string label) { }
        internal static void LoaderReady(int flags) { }
        internal static void LoaderVisible(bool visible) { }
        internal static void LoaderError(string message) { }
        internal static long HeapBytes() => 0L;
#endif
    }
}
