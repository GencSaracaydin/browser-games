using System;
using System.Collections;
using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.FX;
using LongShadow.Cinematics.Playables;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// <see cref="ICinematicService"/> (TECH_PLAN §7.12): plays the TL_* timelines from LIB_Cinematics on its own
    /// PlayableDirector in unscaled time, binds their tracks to booth/cast/feed objects by id, owns the hand-off to and
    /// from the player rig (camera, AudioListener, HUD, input locks, holstering), holds the intro at shot 8 while
    /// S1_Approach/S9_TownFar load (D20), freezes with the pause menu, offers "hold Space 1 s" skips by
    /// <see cref="SkipRule"/>, chains the intro into the title card and raises the end event ("Intro.ControlToPlayer")
    /// when the whole chain is over. Missing timelines degrade to a line-by-line fallback.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Cinematics)]
    [DisallowMultipleComponent]
    public sealed class CinematicService : MonoBehaviour, ICinematicService
    {
        /// <summary>Longest hold before the timeline plays on anyway (seconds).</summary>
        public const float HoldTimeout = 90f;
        /// <summary>Seconds the skip hint shows at the start of a skippable sequence.</summary>
        public const float SkipHintSeconds = 3f;
        /// <summary>A title card requested again within this time after the chained one completes immediately.</summary>
        public const float TitleCardDedupeSeconds = 30f;

        [SerializeField] private PlayableDirector _director;

        private ServiceRef<IInputLock> _lock;
        private ServiceRef<IHud> _hud;
        private ServiceRef<IWeaponService> _weapons;
        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<IInputReader> _input;
        private ServiceRef<IPauseService> _pause;
        private ServiceRef<ISectionStreamer> _streamer;
        private ServiceRef<IMissionDirector> _mission;
        private ServiceRef<ITimeScaleService> _time;

        private CinematicSequenceDef _def;
        private Action _onDone;
        private string _chainEndEvent;
        private bool _playing;
        private bool _finishing;
        private bool _seenBefore;
        private float _startedAt;
        private int _lockHandle;
        private int _lookLockHandle;
        private bool _hudHidden;
        private bool _holstered;
        private float _playerPitch;
        private float _playerYaw;
        private bool _lookRecorded;
        private HoldCondition _hold;
        private float _holdSince;
        private float _rampSpeed = 1f;
        private int _rampTimeHandle;
        private float _titleCardDoneAt = -999f;
        private Coroutine _fallback;

        /// <inheritdoc/>
        public bool IsPlaying => _playing || _chainPending;
        /// <inheritdoc/>
        public string Current => _playing && _def != null ? _def.Id : null;
        /// <summary>The director (bound and playing while a sequence runs).</summary>
        public PlayableDirector Director => _director;
        /// <summary>True when WP08's HUD is registered (title card and letterbox are drawn by it).</summary>
        public bool HasHud => _hud.Value != null;
        /// <summary>Playhead of the current sequence in seconds.</summary>
        public double PlayheadTime => _director != null ? _director.time : 0d;

        private void Awake()
        {
            CinematicRuntime.Set(this);
            GameServices.Register<ICinematicService>(this);
            if (_director == null) _director = GetComponent<PlayableDirector>();
            if (_director == null) _director = gameObject.AddComponent<PlayableDirector>();
            _director.playOnAwake = false;
            _director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            _director.extrapolationMode = DirectorWrapMode.None;
        }

        private void OnEnable() => _director.stopped += OnDirectorStopped;

        private void OnDisable()
        {
            if (_director != null) _director.stopped -= OnDirectorStopped;
            if (_playing) Finish(true, false);
        }

        private void OnDestroy()
        {
            GameServices.Unregister<ICinematicService>(this);
            CinematicRuntime.Clear(this);
        }

        // ------------------------------------------------------------------ ICinematicService

        /// <inheritdoc/>
        public void Play(string id, Action onDone)
        {
            var def = CinematicSequences.Get(id);
            if (def == null)
            {
                LSLog.Error(LogCategory.Cinematics, "Unknown cinematic '" + id + "'.", this);
                onDone?.Invoke();
                return;
            }
            if (id == SequenceIds.TitleCard && Time.unscaledTime - _titleCardDoneAt < TitleCardDedupeSeconds)
            {
                LSLog.Verbose(LogCategory.Cinematics, "Title card already played by the intro chain.", this);
                onDone?.Invoke();
                return;
            }
            if (_playing)
            {
                LSLog.Warn(LogCategory.Cinematics, "Cinematic " + id + " requested while " + _def.Id + " plays; ending it first.", this);
                Finish(true, false);
            }
            _onDone = onDone;
            _chainEndEvent = def.EndEvent;
            Begin(def);
        }

        /// <inheritdoc/>
        public void Skip()
        {
            if (!_playing || _finishing) return;
            LSLog.Info(LogCategory.Cinematics, "Skipping " + _def.Id + " at " + _director.time.ToString("0.0") + " s.", this);
            CinematicAudio.VO?.StopAll(0.15f);
            FireSkipCues();
            Finish(true, true);
        }

        // ------------------------------------------------------------------ hooks for clips

        /// <summary>Holds the timeline (root speed 0) until the condition holds (HoldClip, intro shot 8).</summary>
        public void RequestHold(HoldCondition condition)
        {
            if (condition == HoldCondition.None || IsSatisfied(condition)) return;
            _hold = condition;
            _holdSince = Time.unscaledTime;
            LSLog.Info(LogCategory.Cinematics, "Holding " + Current + " until " + condition + ".", this);
            ApplySpeed();
        }

        /// <summary>Speed-ramp clips: timeline speed and the matching time scale (1 = normal).</summary>
        public void SetRampSpeed(float speed)
        {
            speed = Mathf.Clamp(speed, 0.05f, 4f);
            if (Mathf.Approximately(speed, _rampSpeed)) return;
            _rampSpeed = speed;
            var ts = _time.Value;
            if (ts != null)
            {
                if (_rampTimeHandle != 0) ts.Pop(_rampTimeHandle);
                _rampTimeHandle = Mathf.Approximately(speed, 1f) ? 0 : ts.Push(speed, TimePriority.Cinematic, "Cinematic speed ramp");
            }
            ApplySpeed();
        }

        /// <summary>LookControl clips: opens or closes the Look input channel for the cinematic POV.</summary>
        public void SetLookInput(bool open)
        {
            if (_def != null && _def.PlayerLookFree) return;
            var locks = _lock.Value;
            if (locks == null) return;
            if (open && _lookLockHandle != 0)
            {
                locks.Unlock(_lookLockHandle);
                _lookLockHandle = 0;
            }
            else if (!open && _lookLockHandle == 0) _lookLockHandle = locks.Lock(InputLockFlags.Look, "Cinematic look");
        }

        /// <summary>Raises a mission event (through the MissionDirector when present).</summary>
        public void RaiseEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            var m = _mission.Value;
            if (m != null) m.Raise(eventId);
            else GameEvents.RaiseMissionEvent(eventId);
        }

        // ------------------------------------------------------------------ flow

        private void Begin(CinematicSequenceDef def)
        {
            _def = def;
            _playing = true;
            _finishing = false;
            _seenBefore = SeenFlags.HasSeen(def.Id);
            _startedAt = Time.unscaledTime;
            _hold = HoldCondition.None;
            _rampSpeed = 1f;

            var locks = _lock.Value;
            if (locks != null)
            {
                if (_lockHandle == 0) _lockHandle = locks.Lock(def.BaseLock, "Cinematic " + def.Id);
                if (!def.PlayerLookFree && _lookLockHandle == 0) _lookLockHandle = locks.Lock(InputLockFlags.Look, "Cinematic look");
            }
            if (def.PlayerLookFree && locks != null && _lookLockHandle != 0)
            {
                locks.Unlock(_lookLockHandle);
                _lookLockHandle = 0;
            }
            if (def.HideHud && !_hudHidden)
            {
                _hud.Value?.SetVisible(false);
                _hudHidden = true;
            }
            else if (!def.HideHud && _hudHidden)
            {
                _hud.Value?.SetVisible(true);
                _hudHidden = false;
            }
            if (def.Holster && !_holstered)
            {
                _weapons.Value?.SetHolstered(this, true);
                _holstered = true;
            }
            var rig = _rig.Value;
            if (def.RestorePlayerLook && rig != null && !_lookRecorded)
            {
                _playerPitch = rig.Pitch;
                _playerYaw = rig.Yaw;
                _lookRecorded = true;
            }
            var cams = CinematicRuntime.Cameras;
            if (cams != null)
            {
                cams.ResetShotState();
                cams.Push(this, def.StartCamera);
            }
            CinematicRuntime.ScreenFx?.ClearCinematicChannels();
            GameEvents.RaiseCinematicStarted(def.Id);

            var timeline = CinematicLibrary.Lookup<TimelineAsset>(def.TimelineId);
            if (timeline == null)
            {
                LSLog.MissingAsset(LogCategory.Cinematics, def.TimelineId, "Assets/_Game/Timelines/" + def.TimelineId + ".playable", this);
                _fallback = StartCoroutine(RunFallback(def));
                return;
            }
            _director.playableAsset = timeline;
            TrackBindingResolver.Bind(_director, timeline);
            _director.RebuildGraph();
            _director.time = 0d;
            _director.Play();
            ApplySpeed();
        }

        private void Update()
        {
            if (!_playing || _finishing) return;
            if (_hold != HoldCondition.None)
            {
                var waited = Time.unscaledTime - _holdSince;
                if (IsSatisfied(_hold) || waited > HoldTimeout)
                {
                    if (waited > HoldTimeout)
                        LSLog.Warn(LogCategory.Cinematics, "Hold " + _hold + " timed out after " + HoldTimeout + " s; playing on.", this);
                    _hold = HoldCondition.None;
                }
                ApplySpeed();
            }
            else if (IsPaused() != _lastPaused) ApplySpeed();

            var canSkip = _def.CanSkip(_seenBefore);
            var overlay = CinematicRuntime.Overlay;
            if (canSkip)
            {
                var progress = _input.Value != null ? _input.Value.SkipHeld01 : 0f;
                var hint = Time.unscaledTime - _startedAt < SkipHintSeconds;
                overlay?.SetSkipPrompt(progress > 0.01f || hint, progress);
                // The intro cannot be skipped into an unloaded world (D20): wait until S1/S9 are in.
                var worldReady = _def.Id != SequenceIds.Intro || IsSatisfied(HoldCondition.IntroSections);
                if (progress >= 1f && !IsPaused() && worldReady) Skip();
            }
            else overlay?.SetSkipPrompt(false, 0f);
        }

        private bool _lastPaused;

        private void ApplySpeed()
        {
            _lastPaused = IsPaused();
            if (_director == null || !_director.playableGraph.IsValid()) return;
            var speed = _lastPaused || _hold != HoldCondition.None ? 0d : _rampSpeed;
            var root = _director.playableGraph.GetRootPlayable(0);
            if (root.IsValid()) root.SetSpeed(speed);
        }

        private void OnDirectorStopped(PlayableDirector d)
        {
            if (_playing && !_finishing && _fallback == null) Finish(false);
        }

        private void Finish(bool skipped, bool continueChain = true)
        {
            if (!_playing) return;
            _finishing = true;
            var def = _def;
            if (_fallback != null)
            {
                StopCoroutine(_fallback);
                _fallback = null;
            }
            if (_director.state == PlayState.Playing || _director.playableGraph.IsValid()) _director.Stop();
            _director.playableAsset = null;

            var ts = _time.Value;
            if (ts != null && _rampTimeHandle != 0) ts.Pop(_rampTimeHandle);
            _rampTimeHandle = 0;
            _rampSpeed = 1f;
            _hold = HoldCondition.None;

            var cams = CinematicRuntime.Cameras;
            if (cams != null)
            {
                cams.Release(this);
                cams.ResetShotState();
            }
            var overlay = CinematicRuntime.Overlay;
            if (overlay != null)
            {
                overlay.SetSkipPrompt(false, 0f);
                overlay.SetLetterbox(0f);
                overlay.SetCenterText(null, Color.white);
                overlay.SetTitleText(null, Color.white);
            }
            var fx = CinematicRuntime.ScreenFx;
            if (fx != null)
            {
                fx.ClearCinematicChannels();
                switch (def.End)
                {
                    case EndScreen.HoldBlack: fx.Hold(Color.black); break;
                    case EndScreen.HoldWhite: fx.Hold(Color.white); break;
                }
            }
            BoothRoot.Current?.SetResting();
            SeenFlags.MarkSeen(def.Id);
            if (def.Id == SequenceIds.TitleCard) _titleCardDoneAt = Time.unscaledTime;
            _playing = false;
            _finishing = false;
            GameEvents.RaiseCinematicEnded(def.Id);

            var next = continueChain ? CinematicSequences.Get(def.Next) : null;
            if (next != null)
            {
                // Start the chained sequence on the next frame (we may be inside the director's stopped callback).
                _chainPending = true;
                StartCoroutine(BeginNext(next));
                return;
            }
            ReleasePlayer(def);
            if (continueChain && !string.IsNullOrEmpty(_chainEndEvent)) RaiseEvent(_chainEndEvent);
            _chainEndEvent = null;
            var done = _onDone;
            _onDone = null;
            if (done == null) return;
            try
            {
                done();
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Cinematics, e, this);
            }
        }

        private bool _chainPending;

        private IEnumerator BeginNext(CinematicSequenceDef next)
        {
            yield return null;
            _chainPending = false;
            if (_playing) yield break;
            Begin(next);
        }

        private void ReleasePlayer(CinematicSequenceDef def)
        {
            var locks = _lock.Value;
            if (locks != null)
            {
                if (_lockHandle != 0) locks.Unlock(_lockHandle);
                if (_lookLockHandle != 0) locks.Unlock(_lookLockHandle);
            }
            _lockHandle = 0;
            _lookLockHandle = 0;
            if (_hudHidden) _hud.Value?.SetVisible(true);
            _hudHidden = false;
            if (_holstered) _weapons.Value?.SetHolstered(this, false);
            _holstered = false;
            var rig = _rig.Value;
            if (_lookRecorded && rig != null) rig.SetLookAngles(_playerPitch, _playerYaw);
            _lookRecorded = false;
            if (def.End == EndScreen.Clear) CinematicRuntime.ScreenFx?.ClearFade();
        }

        private void FireSkipCues()
        {
            var timeline = _director.playableAsset as TimelineAsset;
            if (timeline == null) return;
            var now = _director.time;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (!(track is CinematicEventTrack)) continue;
                foreach (var clip in track.GetClips())
                {
                    if (clip.start + 1e-4 < now) continue;
                    if (clip.asset is CinematicEventClip e && e.FireOnSkip) e.Execute(this);
                }
            }
        }

        private bool IsSatisfied(HoldCondition c)
        {
            switch (c)
            {
                case HoldCondition.IntroSections:
                    var s = _streamer.Value;
                    return s == null || (s.IsLoaded(SectionId.Approach) && s.IsLoaded(SectionId.TownFar));
                case HoldCondition.Booth:
                    var b = _streamer.Value;
                    return b == null || b.IsLoaded(SectionId.Booth);
                default:
                    return true;
            }
        }

        private bool IsPaused() => _pause.Value != null && _pause.Value.IsPaused;

        // ------------------------------------------------------------------ degraded mode

        private IEnumerator RunFallback(CinematicSequenceDef def)
        {
            var cams = CinematicRuntime.Cameras;
            if (def.StartCamera == CameraMode.Booth && cams != null)
            {
                cams.SetBoothPreset(BoothCameraPreset.Pov);
                cams.Look.Enabled = true;
                SetLookInput(true);
            }
            var vo = CinematicAudio.VO;
            foreach (var line in SequenceFallbacks.Lines(def.Id))
            {
                if (vo == null || !vo.HasLine(line)) continue;
                vo.Play(line, new VOOptions { Priority = VOPriority.Scripted });
                var t = 0f;
                var max = Mathf.Max(1f, vo.Duration(line)) + 2f;
                while (t < max && (vo.IsPlaying(line) || t < 0.3f))
                {
                    yield return null;
                    if (!IsPaused()) t += Time.unscaledDeltaTime;
                }
                yield return null;
            }
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime) yield return null;
            _fallback = null;
            Finish(false);
        }
    }
}
