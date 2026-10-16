using System;

namespace LongShadow.Core
{
    /// <summary>The click-to-start gate in S_Boot (WP08 ClickGateView, driven by WP01 Bootstrap, GDD §8.1).</summary>
    public interface IClickGate
    {
        /// <summary>Load progress 0..1 shown as number groups.</summary>
        void SetProgress(float progress01);
        /// <summary>Enables the click once S_Persistent and S0_Booth are loaded (TECH_PLAN D20).</summary>
        void SetReady(bool ready);
        /// <summary>Shows/hides the gate (QUIT TO SESSION shows it again).</summary>
        void SetVisible(bool visible);
        /// <summary>Raised on the pointer-down that starts the mission (inside the user gesture).</summary>
        event Action Clicked;
    }
}
