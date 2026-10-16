using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>Recording <see cref="IHud"/> for rig tests.</summary>
    internal sealed class FakeHud : IHud
    {
        public readonly List<string> Banners = new List<string>();
        public readonly List<string> FailCards = new List<string>();
        public readonly List<PromptData> Prompts = new List<PromptData>();
        public HudOverlay Overlay;

        public void ShowPrompt(PromptData prompt) => Prompts.Add(prompt);
        public void HidePrompt(string id) { }
        public void Tutorial(string key, string text) { }
        public void IntelCard(string title, Sprite photo, string counter) { }
        public void FailCard(string text) => FailCards.Add(text);
        public void Banner(string text, float seconds) => Banners.Add(text);
        public void SetOverlay(HudOverlay overlay) => Overlay = overlay;
        public void SetRing(string id, Transform target, float value01, string label) { }
        public void ClearRing(string id) { }
        public void Letterbox(bool on, float seconds) { }
        public void SetVisible(bool visible) { }
        public void TitleCard(float seconds) { }
        public void ShowDebrief(MissionStats stats) { }
    }
}
