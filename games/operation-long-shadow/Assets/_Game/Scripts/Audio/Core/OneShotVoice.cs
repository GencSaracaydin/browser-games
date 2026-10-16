using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>One pooled AudioSource of the <see cref="AudioService"/> playing a one-shot (or an attached sound).</summary>
    internal sealed class OneShotVoice
    {
        public AudioSource Source;
        public Transform Transform;
        public int Handle;
        public string CueId;
        public MixCategory Category;
        public bool Insert;
        public float BaseVolume;
        public float BasePitch = 1f;
        public int Priority;
        public float Audibility;
        public float StartedAt;
        public float EndsAt;
        public Transform Follow;
        public bool Following;
        public float Fade = 1f;
        public float FadeTarget = 1f;
        public float FadeSpeed;
        public bool StopAtSilence;
        public float FadeOutAt = float.MaxValue;
        public float FadeOutSeconds;
        public float HardStopAt = float.MaxValue;

        /// <summary>True while playing or waiting for a delayed start.</summary>
        public bool Active(float now)
        {
            if (Handle == 0 || Source == null) return false;
            return Source.isPlaying || now < EndsAt;
        }

        /// <summary>Clears the voice after it stopped.</summary>
        public void Release()
        {
            Handle = 0;
            CueId = null;
            Follow = null;
            Following = false;
            Fade = 1f;
            FadeTarget = 1f;
            FadeSpeed = 0f;
            StopAtSilence = false;
            FadeOutAt = float.MaxValue;
            FadeOutSeconds = 0f;
            HardStopAt = float.MaxValue;
            EndsAt = 0f;
            if (Source != null)
            {
                Source.Stop();
                Source.clip = null;
                Source.loop = false;
            }
        }

        /// <summary>Starts a fade to silence and stops at the end (0 seconds = stop now).</summary>
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

        /// <summary>Advances the fade by <paramref name="dt"/>; returns false when the voice stopped.</summary>
        public bool StepFade(float now, float dt)
        {
            if (now >= HardStopAt)
            {
                Release();
                return false;
            }
            if (now >= FadeOutAt && FadeTarget > 0f)
            {
                FadeTarget = 0f;
                FadeSpeed = 1f / Mathf.Max(0.01f, FadeOutSeconds);
                StopAtSilence = true;
            }
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
