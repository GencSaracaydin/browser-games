using System;

namespace LongShadow.Core
{
    /// <summary>First-person body actions (WP05 FPArmsIK on PFB_Calder_FP), used by mission scripts and cinematics.</summary>
    public interface IFirstPersonBody
    {
        /// <summary>Watch check (MID-A, 1.2 s).</summary>
        void PlayWatchCheck(float seconds, Action onDone);
        /// <summary>Pistol rise (climax, 3 s).</summary>
        void PlayPistolRise(float seconds, Action onDone);
        /// <summary>Booth fists clench/unclench (outro).</summary>
        void SetFists(bool clenched);
        /// <summary>True while an action plays.</summary>
        bool IsPlaying { get; }
    }
}
