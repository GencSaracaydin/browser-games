using LongShadow.Core;
using UnityEngine;

namespace LongShadow.UI.Dev
{
    /// <summary>
    /// <see cref="IPauseService"/> stand-in for the UI test bed: time scale 0 while paused, PauseChanged events, and a
    /// simulated 1 s browser re-lock cooldown (<see cref="RelockCooldown"/>), which tests can pin with
    /// <see cref="RelockCooldownOverride"/>.
    /// </summary>
    public sealed class FakePauseService : IPauseService
    {
        private float _pausedAt = -10f;

        public bool IsPaused { get; private set; }
        public bool CanPause { get; set; } = true;

        /// <summary>When set, <see cref="RelockCooldown"/> returns this value (tests).</summary>
        public float? RelockCooldownOverride { get; set; }

        /// <summary>Simulated Chrome cooldown after a pause (seconds left).</summary>
        public float RelockCooldown => RelockCooldownOverride
            ?? (IsPaused ? Mathf.Max(0f, 1f - (Time.unscaledTime - _pausedAt)) : 0f);

        public void Pause()
        {
            if (!CanPause || IsPaused) return;
            IsPaused = true;
            _pausedAt = Time.unscaledTime;
            Time.timeScale = 0f;
            GameEvents.RaisePauseChanged(true);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            GameEvents.RaisePauseChanged(false);
        }
    }
}
