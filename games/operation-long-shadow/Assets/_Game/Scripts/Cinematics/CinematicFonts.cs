using TMPro;
using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// TMP fonts for the overlay and the CRT burn-ins: LIB_Cinematics' typewriter/mono fonts (WP08's OFL fonts), else
    /// TMP's default font when TMP Essential Resources are imported, else null (callers fall back to the legacy font or
    /// hide the text).
    /// </summary>
    public static class CinematicFonts
    {
        private static bool s_checked;
        private static TMP_FontAsset s_default;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_checked = false;
            s_default = null;
        }

        /// <summary>A font by LIB_Cinematics id, else the TMP default (null when TMP resources are missing).</summary>
        public static TMP_FontAsset Get(string libraryId)
        {
            var f = string.IsNullOrEmpty(libraryId) ? null : CinematicLibrary.Lookup<TMP_FontAsset>(libraryId);
            return f != null ? f : Default;
        }

        /// <summary>TMP's default font asset, without touching TMP_Settings when it does not exist.</summary>
        public static TMP_FontAsset Default
        {
            get
            {
                if (s_checked) return s_default;
                s_checked = true;
                var settings = Resources.Load<TMP_Settings>("TMP Settings");
                s_default = settings != null ? TMP_Settings.defaultFontAsset : null;
                return s_default;
            }
        }
    }
}
