using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// Editor-only start options on S_Boot (TECH_PLAN §5.2.4): a URL-style query ("cp=CP5&amp;dev=1") used instead of the
    /// page URL, and a switch that skips the click gate. Ignored in players (web builds read the real URL).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevStart : MonoBehaviour
    {
        [SerializeField, Tooltip("Apply these options when entering play mode in the editor.")]
        private bool _enabledInEditor = true;
        [SerializeField, Tooltip("URL-style query, e.g. cp=CP5&dev=1 (see LaunchOptions).")]
        private string _query = string.Empty;
        [SerializeField, Tooltip("Start the mission without waiting for a click (editor only).")]
        private bool _skipClickGate;
        [SerializeField, Tooltip("Enable the dev console in the editor.")]
        private bool _dev = true;

        /// <summary>Query text used in the editor ("" when disabled).</summary>
        public string EditorQuery => _enabledInEditor ? _query ?? string.Empty : string.Empty;
        /// <summary>Skip the click gate in the editor.</summary>
        public bool SkipClickGate => _enabledInEditor && _skipClickGate;
        /// <summary>Dev console on in the editor.</summary>
        public bool EditorDev => _enabledInEditor && _dev;
    }
}
