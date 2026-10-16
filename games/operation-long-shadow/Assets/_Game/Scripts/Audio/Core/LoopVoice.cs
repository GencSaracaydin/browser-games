using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>A loop slot of the <see cref="AudioService"/>: two sources driven by a <see cref="ScheduledLooper"/>.</summary>
    internal sealed class LoopVoice
    {
        public ScheduledLooper Looper;
        public Transform Transform;
        public int Handle;
        public string CueId;
        public MixCategory Category;
        public bool Insert;
        public float BaseVolume;
        public int Priority;
        public float StartedAt;
        public Transform Follow;
        public bool Following;
        public bool Is3D;
        public float Fade;
        public float FadeTarget = 1f;
        public float FadeSpeed;
        public bool StopAtSilence;

        /// <summary>True while the slot is in use.</summary>
        public bool Active => Handle != 0;

        /// <summary>Stops and frees the slot.</summary>
        public void Release()
        {
            Handle = 0;
            CueId = null;
            Follow = null;
            Following = false;
            Fade = 0f;
            FadeTarget = 1f;
            FadeSpeed = 0f;
            StopAtSilence = false;
            Looper?.Stop();
        }

        /// <summary>Fades out and stops (0 seconds = now).</summary>
        public void FadeOutAndStop(float seconds)
        {
            if (seconds <= 0.001f)
            {
                Release();
                return;
            }
            FadeTarget = 0f;
            FadeSpeed = 1f / seconds;
            StopAtSilence = true;
        }

        /// <summary>Advances the fade; returns false when the slot stopped.</summary>
        public bool StepFade(float dt)
        {
            if (FadeSpeed > 0f && !Mathf.Approximately(Fade, FadeTarget))
                Fade = Mathf.MoveTowards(Fade, FadeTarget, FadeSpeed * dt);
            if (StopAtSilence && Fade <= 0.0005f)
            {
                Release();
                return false;
            }
            return true;
        }
    }
}
