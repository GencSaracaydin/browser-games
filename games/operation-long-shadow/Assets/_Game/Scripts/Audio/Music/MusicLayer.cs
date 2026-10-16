using LongShadow.Audio.Core;
using UnityEngine;

namespace LongShadow.Audio.Music
{
    /// <summary>
    /// One playing music layer: a <see cref="ScheduledLooper"/> (gapless loop region, or a one-shot cue) with a gain
    /// ramp that can start at a DSP time (bar-synced crossfades).
    /// </summary>
    internal sealed class MusicLayer
    {
        public ScheduledLooper Looper;
        public AudioCue Cue;
        public bool Busy;
        public float Gain;
        public bool StopWhenSilent;

        private float _target;
        private float _rate;
        private double _pendingAt = -1d;
        private float _pendingTarget;
        private float _pendingSeconds;

        /// <summary>Cue id (null when idle).</summary>
        public string CueId => Busy && Cue != null ? Cue.Id : null;

        /// <summary>Starts the cue's first clip at <paramref name="startDsp"/> with gain 0.</summary>
        public bool Start(AudioCue cue, double startDsp, bool ignorePause)
        {
            Cue = cue;
            var clip = cue != null && cue.Clips.Length > 0 ? cue.Clips[0] : null;
            if (clip == null) return false;
            var region = cue.LoopRegion(0);
            var loop = cue.Loop || region.y > region.x;
            Gain = 0f;
            _target = 0f;
            _rate = 0f;
            _pendingAt = -1d;
            StopWhenSilent = false;
            Looper.SetIgnorePause(ignorePause);
            Looper.SetVolume(0f);
            Looper.Play(clip, loop, region, 0, startDsp);
            Busy = true;
            return true;
        }

        /// <summary>Ramps the gain to <paramref name="target"/> over <paramref name="seconds"/>, starting at <paramref name="atDsp"/>.</summary>
        public void FadeTo(float target, float seconds, double atDsp)
        {
            _pendingAt = atDsp;
            _pendingTarget = Mathf.Clamp01(target);
            _pendingSeconds = Mathf.Max(0f, seconds);
        }

        /// <summary>Advances the ramp and the looper; returns false when the layer stopped.</summary>
        public bool Tick(double dsp, float dt, float mixGain)
        {
            if (!Busy) return false;
            if (_pendingAt >= 0d && dsp >= _pendingAt)
            {
                _pendingAt = -1d;
                _target = _pendingTarget;
                var delta = Mathf.Abs(_target - Gain);
                if (_pendingSeconds <= 0.001f || delta <= 0.0001f)
                {
                    Gain = _target;
                    _rate = 0f;
                }
                else _rate = delta / _pendingSeconds;
            }
            if (_rate > 0f) Gain = Mathf.MoveTowards(Gain, _target, _rate * dt);
            Looper.Tick();
            if (StopWhenSilent && Gain <= 0.0005f && _pendingAt < 0d)
            {
                Stop();
                return false;
            }
            if (!Looper.IsRunning && Application.isPlaying)
            {
                Busy = false;
                return false;
            }
            Looper.SetVolume(Gain * (Cue != null ? Cue.Volume : 1f) * mixGain);
            return true;
        }

        /// <summary>Stops now.</summary>
        public void Stop()
        {
            Looper.Stop();
            Busy = false;
            Gain = 0f;
            _pendingAt = -1d;
            _rate = 0f;
            Cue = null;
        }
    }
}
