using LongShadow.Audio.Core;
using UnityEngine;

namespace LongShadow.Audio.Ambience
{
    /// <summary>
    /// One looping take of an ambience bed: a <see cref="ScheduledLooper"/> (gapless loop region, TECH_PLAN §3.12) with
    /// a crossfade share used while the bed rotates to its other variant.
    /// </summary>
    internal sealed class BedPlayer
    {
        public ScheduledLooper Looper;
        public AudioCue Cue;
        public int ClipIndex = -1;
        public bool Busy;
        public bool Insert;
        /// <summary>Crossfade share 0..1 (equal-power gain is applied by the owner).</summary>
        public float Share;
        /// <summary>Unscaled time the take started (variant rotation).</summary>
        public float StartedAt;
        /// <summary>Seconds of one loop body (variant rotation).</summary>
        public float BodySeconds;

        /// <summary>Starts clip <paramref name="clipIndex"/> of <paramref name="cue"/> at a random point of its loop body.</summary>
        public bool Start(AudioCue cue, int clipIndex, float random01, bool insert, float now)
        {
            Stop();
            if (cue == null || clipIndex < 0 || clipIndex >= cue.Clips.Length) return false;
            var clip = cue.Clips[clipIndex];
            if (clip == null) return false;
            var region = cue.LoopRegion(clipIndex);
            var a = region.y > region.x ? region.x : 0f;
            var b = region.y > region.x ? region.y : clip.length;
            var startSample = clip.frequency > 0 ? (int)((a + (b - a) * Mathf.Clamp01(random01)) * clip.frequency) : 0;
            Cue = cue;
            ClipIndex = clipIndex;
            Insert = insert;
            Share = 0f;
            StartedAt = now;
            BodySeconds = Mathf.Max(1f, b - a);
            Looper.SetIgnorePause(insert);
            Looper.SetVolume(0f);
            Looper.Play(clip, true, region, startSample, AudioSettings.dspTime);
            Busy = true;
            return true;
        }

        /// <summary>Stops and frees the player.</summary>
        public void Stop()
        {
            if (Looper != null) Looper.Stop();
            Busy = false;
            Cue = null;
            ClipIndex = -1;
            Share = 0f;
        }
    }
}
