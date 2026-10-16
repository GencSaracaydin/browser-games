using LongShadow.Audio.Core;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// One line being heard: up to four sources (0 = 2D, 1-3 = 3D) rendered per its <see cref="VOPlan"/>. Synced pairs
    /// (dry/radio, positional switch) start with <c>PlayScheduled</c> on the same DSP time; PA horns start each delayed
    /// by its distance. Timers use unscaled time and freeze while the listener is paused (unless the line plays in a
    /// glitch insert, which ignores the pause).
    /// </summary>
    internal sealed class VOVoice
    {
        public const int SourceCount = 4;
        private const float ScheduleLead = 0.03f;

        public readonly AudioSource[] Sources = new AudioSource[SourceCount];
        private readonly float[] _gain = new float[SourceCount];
        private readonly float[] _delay = new float[SourceCount];

        public VORequest Request;
        public VOPlan Plan;
        public bool Busy;
        public bool Insert;
        public bool SubtitleShown;
        public float StartedAt;
        public float EndsAt;
        public float SpeechDelay;
        public float Volume = 1f;

        private Transform _speaker;
        private Transform _speakerHead;
        private Vector3 _anchor;
        private bool _hasAnchor;
        private float _fade = 1f;
        private float _fadeTarget = 1f;
        private float _fadeSpeed;
        private bool _stopping;

        /// <summary>Handle of the request (0 when idle).</summary>
        public int Handle => Busy && Request != null ? Request.Handle : 0;
        /// <summary>The line (null when idle).</summary>
        public DialogueLine Line => Busy && Request != null ? Request.Line : null;
        /// <summary>True while fading out.</summary>
        public bool Stopping => _stopping;
        /// <summary>The 3D speaker head, if any.</summary>
        public Transform SpeakerHead => _speakerHead;

        /// <summary>Starts the line.</summary>
        public void Begin(VORequest request, VOPlan plan, Transform speaker, Transform speakerHead, bool insert, float now)
        {
            Request = request;
            Plan = plan;
            Busy = true;
            Insert = insert;
            SubtitleShown = false;
            _speaker = speaker;
            _speakerHead = speakerHead != null ? speakerHead : speaker;
            _fade = 1f;
            _fadeTarget = 1f;
            _fadeSpeed = 0f;
            _stopping = false;
            Volume = request.Options.Volume > 0f ? request.Options.Volume : 1f;
            for (var i = 0; i < SourceCount; i++)
            {
                _gain[i] = 0f;
                _delay[i] = 0f;
                var s = Sources[i];
                s.Stop();
                s.clip = null;
                s.loop = false;
                s.pitch = 1f;
                s.ignoreListenerPause = insert;
                s.volume = 0f;
            }
            _hasAnchor = VOAnchors.TryResolve(plan.AnchorId, out _anchor);

            var duration = request.Line != null ? request.Line.Duration : 0f;
            var canSchedule = Application.isPlaying && AudioSettings.dspTime > 0d;
            var dsp = AudioSettings.dspTime + ScheduleLead;
            var lead = canSchedule ? ScheduleLead : 0f;
            SpeechDelay = 0f;
            switch (plan.Mode)
            {
                case VOPlanMode.TwoD:
                    Configure2D(Sources[0], plan.Main.Clip);
                    Sources[0].Play();
                    _gain[0] = 1f;
                    duration = plan.Main.Duration;
                    break;
                case VOPlanMode.ThreeD:
                    Configure3D(Sources[1], plan.Main.Clip, plan, Position3D());
                    Sources[1].Play();
                    _gain[1] = 1f;
                    duration = plan.Main.Duration;
                    if (!_hasAnchor && _speakerHead == null)
                    {
                        // No anchor and no speaker: fall back to 2D.
                        Sources[1].spatialBlend = 0f;
                    }
                    break;
                case VOPlanMode.DryRadio:
                    Configure3D(Sources[1], plan.Dry.Clip, plan, Position3D());
                    Configure2D(Sources[0], plan.Radio.Clip);
                    StartSynced(Sources[1], Sources[0], canSchedule, dsp);
                    SpeechDelay = lead;
                    duration = Mathf.Max(plan.Dry.Duration, plan.Radio.Duration);
                    break;
                case VOPlanMode.PositionalSwitch:
                    Configure3D(Sources[1], plan.Main.Clip, plan, Position3D());
                    Configure2D(Sources[0], plan.Main.Clip);
                    StartSynced(Sources[1], Sources[0], canSchedule, dsp);
                    SpeechDelay = lead;
                    duration = plan.Main.Duration;
                    break;
                case VOPlanMode.Horns:
                    var listener = AudioRuntime.ListenerPosition;
                    var minDelay = float.MaxValue;
                    for (var h = 0; h < VOAnchors.Horns.Length && h + 1 < SourceCount; h++)
                    {
                        VOAnchors.TryResolve(VOAnchors.Horns[h], out var hp);
                        var src = Sources[h + 1];
                        Configure3D(src, plan.Main.Clip, plan, hp);
                        var d = AudioMath.PropagationDelay(Vector3.Distance(hp, listener));
                        _delay[h + 1] = d;
                        _gain[h + 1] = 1f;
                        if (d > 0.001f) src.PlayDelayed(d);
                        else src.Play();
                        if (d < minDelay) minDelay = d;
                    }
                    SpeechDelay = minDelay == float.MaxValue ? 0f : minDelay;
                    var maxDelay = Mathf.Max(_delay[1], Mathf.Max(_delay[2], _delay[3]));
                    duration = plan.Main.Duration + (maxDelay - SpeechDelay);
                    break;
            }
            StartedAt = now;
            EndsAt = now + SpeechDelay + Mathf.Max(0.2f, duration) + 0.05f;
            UpdateWeights(now);
            ApplyGains(now, AudioRuntime.Gain(MixCategory.Voice, insert));
        }

        /// <summary>Advances the voice; returns false when it finished.</summary>
        public bool Tick(float now, float dt, bool listenerPaused, float mixGain)
        {
            if (!Busy) return false;
            if (listenerPaused && !Insert)
            {
                // Paused with the rest of the game: hold the timers.
                StartedAt += dt;
                EndsAt += dt;
                return true;
            }
            if (_fadeSpeed > 0f) _fade = Mathf.MoveTowards(_fade, _fadeTarget, _fadeSpeed * dt);
            if (_stopping && _fade <= 0.0005f) return false;
            if (now >= EndsAt) return false;
            UpdateWeights(now);
            ApplyGains(now, mixGain);
            return true;
        }

        /// <summary>Fades out (0 = now) and finishes.</summary>
        public void Stop(float fade, float now)
        {
            if (!Busy) return;
            if (fade <= 0.001f)
            {
                _fade = 0f;
                _stopping = true;
                EndsAt = now;
                return;
            }
            _stopping = true;
            _fadeTarget = 0f;
            _fadeSpeed = 1f / fade;
            EndsAt = Mathf.Min(EndsAt, now + fade + 0.02f);
        }

        /// <summary>Seconds since the first word could be heard (envelope time).</summary>
        public float PlaybackTime(float now) => Mathf.Max(0f, now - StartedAt - SpeechDelay);

        /// <summary>Envelope amplitude 0..1 now.</summary>
        public float Amplitude(float now)
        {
            if (!Busy || _stopping) return 0f;
            var c = Plan.EnvelopeClip;
            if (c == null) return 0f;
            return EnvelopeSampler.Sample(c.Envelope, PlaybackTime(now)) * _fade;
        }

        /// <summary>Current radio share (subtitle icon, debug).</summary>
        public float RadioShare
        {
            get
            {
                var total = _gain[0] + _gain[1] + _gain[2] + _gain[3];
                return total <= 0f ? (Plan.IsRadio ? 1f : 0f) : _gain[0] / total;
            }
        }

        /// <summary>Stops every source and frees the voice.</summary>
        public void Release()
        {
            for (var i = 0; i < SourceCount; i++)
            {
                var s = Sources[i];
                if (s == null) continue;
                s.Stop();
                s.clip = null;
            }
            Busy = false;
            Request = null;
            _speaker = null;
            _speakerHead = null;
            _stopping = false;
        }

        private Vector3 Position3D()
        {
            if (_hasAnchor) return _anchor;
            if (_speakerHead != null) return _speakerHead.position;
            return AudioRuntime.ListenerPosition;
        }

        private void UpdateWeights(float now)
        {
            switch (Plan.Mode)
            {
                case VOPlanMode.DryRadio:
                    float dry, radio;
                    if (_speakerHead == null) { dry = 0f; radio = 1f; }
                    else RadioMix.Weights(Vector3.Distance(_speakerHead.position, AudioRuntime.ListenerPosition), out dry, out radio);
                    if (Request.Options.ForceDry) { dry = 1f; radio = 0f; }
                    _gain[1] = dry;
                    _gain[0] = radio;
                    break;
                case VOPlanMode.PositionalSwitch:
                    PositionalRadio.Weights(PlaybackTime(now), Plan.SwitchTime, out var pos, out var rad);
                    _gain[1] = pos;
                    _gain[0] = rad;
                    break;
            }
            if (!_hasAnchor && _speakerHead != null && Plan.Mode != VOPlanMode.Horns)
                Sources[1].transform.position = _speakerHead.position;
        }

        private void ApplyGains(float now, float mixGain)
        {
            var v = Volume * _fade * mixGain;
            for (var i = 0; i < SourceCount; i++)
            {
                var s = Sources[i];
                if (s != null) s.volume = v * _gain[i];
            }
        }

        private void Configure2D(AudioSource s, AudioClip clip)
        {
            s.clip = clip;
            s.spatialBlend = 0f;
            s.priority = MixCategories.UnityPriority(MixCategory.Voice, 100);
        }

        private void Configure3D(AudioSource s, AudioClip clip, VOPlan plan, Vector3 position)
        {
            s.clip = clip;
            s.spatialBlend = plan.SpatialBlend;
            s.minDistance = Mathf.Max(0.05f, plan.MinDistance);
            s.maxDistance = Mathf.Max(s.minDistance + 0.1f, plan.MaxDistance);
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.spread = 0f;
            s.priority = MixCategories.UnityPriority(MixCategory.Voice, 100);
            s.transform.position = position;
        }

        private static void StartSynced(AudioSource a, AudioSource b, bool canSchedule, double dsp)
        {
            if (canSchedule)
            {
                a.PlayScheduled(dsp);
                b.PlayScheduled(dsp);
            }
            else
            {
                a.Play();
                b.Play();
            }
        }
    }
}
