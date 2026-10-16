using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// Plays a clip on two AudioSources: once, or as a gapless loop of its loop region with back-to-back
    /// <c>PlayScheduled</c>/<c>SetScheduledEndTime</c> segments (TECH_PLAN §3.12 Web rules: looping AAC can click, so the
    /// clips carry a lead-in and the body is scheduled sample-accurately instead of using <c>AudioSource.loop</c>).
    /// Clips without a loop region, edit mode, or <see cref="SchedulingEnabled"/> = false fall back to
    /// <c>AudioSource.loop</c>. After a pause (<see cref="OnPause"/>/<see cref="OnResume"/>) the loop restarts at the
    /// position it had, because a paused listener and a running DSP clock disagree about scheduled segments.
    /// The owner configures both sources (<see cref="A"/>, <see cref="B"/>) and sets the volume every frame.
    /// </summary>
    public sealed class ScheduledLooper
    {
        /// <summary>Global switch: false makes every loop use <c>AudioSource.loop</c> (in case a browser misbehaves).</summary>
        public static bool SchedulingEnabled = true;

        /// <summary>Seconds ahead of a segment end at which the next one is scheduled.</summary>
        public const double Lookahead = 0.6;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SchedulingEnabled = true;

        private readonly AudioSource[] _src = new AudioSource[2];
        private readonly double[] _segStart = new double[2];
        private readonly double[] _segStop = new double[2];
        private readonly int[] _segSample = new int[2];

        private AudioClip _clip;
        private Vector2 _region;
        private bool _loop;
        private bool _scheduled;
        private bool _running;
        private int _ls;
        private int _le;
        private int _freq;
        private int _next;
        private double _segEnd;
        private double _oneShotEnd;
        private int _pausedAt = -1;

        /// <summary>Wraps two sources (they are stopped and reconfigured by <see cref="Play"/>).</summary>
        public ScheduledLooper(AudioSource a, AudioSource b)
        {
            _src[0] = a;
            _src[1] = b;
        }

        /// <summary>First source (configure 3D/2D settings on both).</summary>
        public AudioSource A => _src[0];
        /// <summary>Second source.</summary>
        public AudioSource B => _src[1];
        /// <summary>The clip being played.</summary>
        public AudioClip Clip => _clip;
        /// <summary>True for a loop.</summary>
        public bool Looping => _loop;
        /// <summary>True while playing or scheduled.</summary>
        public bool IsRunning => _running;
        /// <summary>True when sample-accurate segments are in use.</summary>
        public bool IsScheduled => _scheduled;
        /// <summary>Loop body start in samples.</summary>
        public int LoopStartSample => _ls;
        /// <summary>Loop body end in samples.</summary>
        public int LoopEndSample => _le;

        private static double Dsp => AudioSettings.dspTime;
        private static bool CanSchedule => SchedulingEnabled && Application.isPlaying && AudioSettings.dspTime > 0d;

        /// <summary>
        /// Starts <paramref name="clip"/> at clip sample <paramref name="startSample"/> at DSP time
        /// <paramref name="dspStart"/> (≤ now = immediately). <paramref name="regionSeconds"/> is the loop body
        /// (y ≤ 0 = whole clip).
        /// </summary>
        public void Play(AudioClip clip, bool loop, Vector2 regionSeconds, int startSample, double dspStart)
        {
            Stop();
            _clip = clip;
            _loop = loop;
            _region = regionSeconds;
            _pausedAt = -1;
            if (clip == null) return;
            _freq = clip.frequency;
            var len = clip.samples;
            var hasRegion = LoopSchedule.ToSamples(regionSeconds.x, regionSeconds.y, _freq, len, out _ls, out _le);
            if (!hasRegion)
            {
                _ls = 0;
                _le = len;
            }
            var a = _src[0];
            var b = _src[1];
            var canSchedule = CanSchedule;
            var now = canSchedule ? Dsp : 0d;
            if (dspStart < now + 0.005) dspStart = now + 0.005;
            a.clip = clip;
            b.clip = clip;
            b.loop = false;

            if (!loop)
            {
                a.loop = false;
                a.timeSamples = Mathf.Clamp(startSample, 0, Mathf.Max(0, len - 1));
                if (canSchedule) a.PlayScheduled(dspStart);
                else a.Play();
                _oneShotEnd = (canSchedule ? dspStart : 0d) + (len - a.timeSamples) / (double)Mathf.Max(1, _freq);
                _scheduled = canSchedule;
                _running = true;
                return;
            }

            if (!hasRegion || !canSchedule)
            {
                a.loop = true;
                a.timeSamples = Mathf.Clamp(LoopSchedule.ClampStart(startSample, _ls, _le), 0, Mathf.Max(0, len - 1));
                if (canSchedule) a.PlayScheduled(dspStart);
                else a.Play();
                _scheduled = false;
                _running = true;
                return;
            }

            a.loop = false;
            var s = LoopSchedule.ClampStart(startSample, _ls, _le);
            a.timeSamples = s;
            a.PlayScheduled(dspStart);
            var end = dspStart + LoopSchedule.FirstSegmentSeconds(s, _ls, _le, _freq);
            a.SetScheduledEndTime(end);
            _segStart[0] = dspStart;
            _segStop[0] = end;
            _segSample[0] = s;
            _segStart[1] = _segStop[1] = 0d;
            _segEnd = end;
            _next = 1;
            _scheduled = true;
            _running = true;
            Tick();
        }

        /// <summary>Schedules the next segment when due; detects the end of one-shots. Call every frame.</summary>
        public void Tick()
        {
            if (!_running) return;
            if (!_loop)
            {
                if (_scheduled)
                {
                    if (Dsp > _oneShotEnd + 0.1 && !_src[0].isPlaying) _running = false;
                }
                else if (!_src[0].isPlaying && Application.isPlaying) _running = false;
                return;
            }
            if (!_scheduled || _pausedAt >= 0) return;
            var now = Dsp;
            if (now > _segEnd + 0.25)
            {
                // Fell behind (tab suspended, long hitch): restart from the body start.
                Restart(_ls, now + 0.05);
                return;
            }
            var body = LoopSchedule.BodySeconds(_ls, _le, _freq);
            if (body <= 0.05) return;
            var guard = 0;
            while (_segEnd - now < Lookahead && guard++ < 4)
            {
                var i = _next;
                var src = _src[i];
                src.timeSamples = _ls;
                src.PlayScheduled(_segEnd);
                var e = _segEnd + body;
                src.SetScheduledEndTime(e);
                _segStart[i] = _segEnd;
                _segStop[i] = e;
                _segSample[i] = _ls;
                _segEnd = e;
                _next = 1 - i;
            }
        }

        /// <summary>Current clip position in samples (from the DSP clock while scheduled).</summary>
        public int PositionSamples
        {
            get
            {
                if (!_running || _clip == null) return 0;
                if (!_scheduled || !_loop) return _src[0].timeSamples;
                var now = Dsp;
                for (var i = 0; i < 2; i++)
                {
                    if (now >= _segStart[i] && now < _segStop[i])
                        return _segSample[i] + (int)((now - _segStart[i]) * _freq);
                }
                return _ls;
            }
        }

        /// <summary>Restarts at a clip sample at a DSP time (same clip, loop and region).</summary>
        public void Restart(int sample, double dspStart)
        {
            var clip = _clip;
            if (clip == null) return;
            Play(clip, _loop, _region, sample, dspStart);
        }

        /// <summary>Remembers the position when the listener pauses (scheduled loops only).</summary>
        public void OnPause()
        {
            if (!_running || !_scheduled || !_loop) return;
            _pausedAt = PositionSamples;
        }

        /// <summary>Restarts from the remembered position after a pause.</summary>
        public void OnResume()
        {
            if (_pausedAt < 0) return;
            var p = _pausedAt;
            _pausedAt = -1;
            if (_running && CanSchedule) Restart(p, Dsp + 0.05);
        }

        /// <summary>Stops both sources.</summary>
        public void Stop()
        {
            for (var i = 0; i < 2; i++)
            {
                var s = _src[i];
                if (s == null) continue;
                s.Stop();
                s.loop = false;
            }
            _running = false;
            _scheduled = false;
            _pausedAt = -1;
        }

        /// <summary>Sets the volume of both sources.</summary>
        public void SetVolume(float volume)
        {
            _src[0].volume = volume;
            _src[1].volume = volume;
        }

        /// <summary>Moves both sources.</summary>
        public void SetPosition(Vector3 position)
        {
            _src[0].transform.position = position;
            if (_src[1].transform != _src[0].transform) _src[1].transform.position = position;
        }

        /// <summary>Sets <c>ignoreListenerPause</c> on both sources.</summary>
        public void SetIgnorePause(bool ignore)
        {
            _src[0].ignoreListenerPause = ignore;
            _src[1].ignoreListenerPause = ignore;
        }
    }
}
