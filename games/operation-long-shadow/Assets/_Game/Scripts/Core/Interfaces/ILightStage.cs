using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Time-of-day controller (WP10 LightStageController, GDD §9.1-9.2a). <see cref="S"/> runs 0..4 through Golden,
    /// Sunset, Afterglow, Blue Hour.
    /// </summary>
    public interface ILightStage
    {
        /// <summary>Current stage value 0..4.</summary>
        float S { get; }
        /// <summary>Named stage containing <see cref="S"/>.</summary>
        StageId Stage { get; }
        /// <summary>Smoothly moves towards a target (time constant in seconds).</summary>
        void SetTarget(float s, float smoothing = 2f);
        /// <summary>Jumps immediately (checkpoints, debug).</summary>
        void SetImmediate(float s);
        /// <summary>While any owner holds the override, the mission progress driver is suspended (cinema swap, MID-A).</summary>
        void Override(object owner, bool on);
        /// <summary>True while any override is held.</summary>
        bool IsOverridden { get; }
        /// <summary>AI sight range in metres for the current stage (GDD §14.2).</summary>
        float AISightRange { get; }
        /// <summary>AI hearing scale.</summary>
        float HearingScale { get; }
        /// <summary>True from s 2.0 (marker palette, AI).</summary>
        bool AfterSunset { get; }
        /// <summary>HUD marker colour (amber before sunset, pale green after).</summary>
        Color HudColor { get; }
        /// <summary>Story clock in minutes after midnight (GDD §9.2a curve).</summary>
        float StoryClockMinutes { get; }
    }
}
