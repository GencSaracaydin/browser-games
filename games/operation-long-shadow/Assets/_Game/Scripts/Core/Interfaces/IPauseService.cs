namespace LongShadow.Core
{
    /// <summary>Pause state (WP01 PauseService, TECH_PLAN §5.4). Raises <see cref="GameEvents.PauseChanged"/>.</summary>
    public interface IPauseService
    {
        /// <summary>True while paused.</summary>
        bool IsPaused { get; }
        /// <summary>True while pausing is allowed (not during the click gate or the debrief).</summary>
        bool CanPause { get; set; }
        /// <summary>Pauses (time scale, listener pause, input lock, pause menu).</summary>
        void Pause();
        /// <summary>Resumes; call from a click handler so pointer lock can be re-requested.</summary>
        void Resume();
        /// <summary>
        /// Seconds until the browser accepts a new pointer lock (Chrome refuses one for about 1 s after the user pressed
        /// Esc); 0 when a click would lock now. WP08's pause menu shows "Click to resume" while it is above 0 (GDD §11).
        /// </summary>
        float RelockCooldown { get; }
    }
}
