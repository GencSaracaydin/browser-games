using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>HUD commands (WP08). Continuous data (weapon, objectives, subtitles) is polled or event-driven, not pushed.</summary>
    public interface IHud
    {
        /// <summary>Shows or updates a prompt.</summary>
        void ShowPrompt(PromptData prompt);
        /// <summary>Hides a prompt by id.</summary>
        void HidePrompt(string id);
        /// <summary>One-time tutorial prompt (shown once per key, persisted).</summary>
        void Tutorial(string key, string text);
        /// <summary>Polaroid intel card for 2.5 s.</summary>
        void IntelCard(string title, Sprite photo, string counter);
        /// <summary>Fail card ("YOU ABANDONED YOUR TEAM").</summary>
        void FailCard(string text);
        /// <summary>Centre banner for seconds (OOB countdown "RETURN TO THE MISSION AREA 6…").</summary>
        void Banner(string text, float seconds);
        /// <summary>Overlay (binoculars, door gun).</summary>
        void SetOverlay(HudOverlay overlay);
        /// <summary>World ring with value 0..1 and label (drill %, LZ metres).</summary>
        void SetRing(string id, Transform target, float value01, string label);
        /// <summary>Removes a ring.</summary>
        void ClearRing(string id);
        /// <summary>Letterbox bars on/off over seconds.</summary>
        void Letterbox(bool on, float seconds);
        /// <summary>Hides the whole HUD (cinematics).</summary>
        void SetVisible(bool visible);
        /// <summary>Mission title card (GDD §8.3) for seconds.</summary>
        void TitleCard(float seconds);
        /// <summary>Opens the debrief dossier (GDD §8.9).</summary>
        void ShowDebrief(MissionStats stats);
    }
}
