using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Soft out-of-bounds zone (Appendix A "OOB.*", GDD §17): entering starts a countdown (6 s), D093 plays, then the
    /// fail card and a checkpoint reload. Placed 4 m in front of a hard PlayerClip. WP02 OutOfBoundsWatcher listens to
    /// TriggerEntered and looks the volume up by id.
    /// </summary>
    public class OutOfBoundsVolume : TriggerVolume
    {
        [SerializeField] private float _countdownSeconds = 6f;

        /// <summary>Countdown length in seconds (GDD §17: 6).</summary>
        public float CountdownSeconds => _countdownSeconds;

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(1f, 0.3f, 0.2f, 0.8f);
#endif
    }
}
