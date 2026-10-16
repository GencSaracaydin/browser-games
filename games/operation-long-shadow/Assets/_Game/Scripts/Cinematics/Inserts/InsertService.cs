using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.FX;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics.Inserts
{
    /// <summary>
    /// <see cref="IInsertService"/> (GDD §5.5, §8.4; TECH_PLAN §7.12): every insert freezes the game (time scale 0 at
    /// priority Insert), pauses gameplay audio (AudioListener.pause; WP06 keeps lines and stingers started during an
    /// insert audible), locks input, glitches in over 0.15 s, cuts to the booth camera preset for its frames (unscaled,
    /// 60 fps reference, at least 2 rendered frames, one white frame on the cut), plays its line and stinger, glitches out
    /// and restores everything. I4 ends on white. The death insert adds the 0.6 s desaturated slow-down, 2.0 s in the
    /// chair with the CRTs on static and a random DI line, then black with "SESSION 12 — RESUMING" while the checkpoint
    /// reloads behind it. Pausing (Esc) holds every timer.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Cinematics + 10)]
    [DisallowMultipleComponent]
    public sealed class InsertService : MonoBehaviour, IInsertService
    {
        /// <summary>Glitch strength while the booth shot is on screen.</summary>
        public const float BoothGlitch = 0.3f;
        /// <summary>Minimum black after the death insert before fading back in.</summary>
        public const float MinResumeBlack = 1.2f;
        /// <summary>Longest wait for the checkpoint reload.</summary>
        public const float ResumeTimeout = 6f;
        /// <summary>Fade-in after a reload.</summary>
        public const float ResumeFadeIn = 0.6f;

        private readonly ShuffleBag<string> _deathLines = new ShuffleBag<string>(InsertTable.DeathLines);
        private readonly List<Action> _deathCallbacks = new List<Action>(2);
        private readonly StringBuilder _sb = new StringBuilder(64);

        private ServiceRef<ITimeScaleService> _time;
        private ServiceRef<IInputLock> _lock;
        private ServiceRef<IPauseService> _pause;
        private ServiceRef<IVOService> _vo;

        private string _current;
        private bool _resumed;
        private int _timeHandle;
        private int _deathTimeHandle;
        private int _lockHandle;

        /// <inheritdoc/>
        public bool IsPlaying => _current != null;
        /// <summary>Id of the running insert, or null.</summary>
        public string Current => _current;

        private void Awake()
        {
            CinematicRuntime.Set(this);
            GameServices.Register<IInsertService>(this);
        }

        private void OnEnable()
        {
            GameEvents.CheckpointLoaded += OnResumed;
            GameEvents.PlayerRespawned += OnRespawned;
        }

        private void OnDisable()
        {
            GameEvents.CheckpointLoaded -= OnResumed;
            GameEvents.PlayerRespawned -= OnRespawned;
            if (_current != null) RestoreAll(_current);
            // Never leave the mission waiting for a death callback (WP07 gives up after 4.5 s).
            if (_deathCallbacks.Count > 0)
            {
                var pending = _deathCallbacks.ToArray();
                _deathCallbacks.Clear();
                foreach (var cb in pending) cb?.Invoke();
            }
        }

        private void OnDestroy()
        {
            GameServices.Unregister<IInsertService>(this);
            CinematicRuntime.Clear(this);
        }

        // ------------------------------------------------------------------ IInsertService

        /// <inheritdoc/>
        public void Play(string insertId, string lineId = null)
        {
            var def = InsertTable.Get(insertId);
            if (def == null)
            {
                LSLog.Warn(LogCategory.Cinematics, "Unknown insert '" + insertId + "'.", this);
                return;
            }
            if (_current != null)
            {
                LSLog.Warn(LogCategory.Cinematics, "Insert " + insertId + " ignored: " + _current + " is playing.", this);
                return;
            }
            StartCoroutine(RunInsert(def, lineId ?? def.DefaultLine));
        }

        /// <inheritdoc/>
        public void PlayDeath(Action onDone)
        {
            if (onDone != null) _deathCallbacks.Add(onDone);
            if (_current == SequenceIds.InsertDeath) return;
            if (_current != null)
            {
                // A death during an insert: finish that insert's restore first, then die.
                StopAllCoroutines();
                RestoreAll(_current);
            }
            StartCoroutine(RunDeath());
        }

        // ------------------------------------------------------------------ glitch inserts

        private IEnumerator RunInsert(InsertDef def, string lineId)
        {
            Begin(def.Id);
            var fx = CinematicRuntime.ScreenFx;
            var cams = CinematicRuntime.Cameras;
            CinematicAudio.Stinger(def.Stinger);

            // Glitch in over the frozen game.
            for (var t = 0f; t < InsertTiming.GlitchSeconds; t += Dt())
            {
                fx?.CinematicGlitch(t / InsertTiming.GlitchSeconds, 0f);
                yield return null;
            }

            var booth = BoothRoot.Current;
            booth?.SetResting();
            cams?.Push(this, CameraMode.Booth);
            if (cams != null) cams.Look.Enabled = false;

            var handle = 0;
            if (!string.IsNullOrEmpty(lineId))
            {
                var vo = _vo.Value;
                if (vo != null) handle = vo.Play(lineId, new VOOptions { Priority = VOPriority.Scripted, Interrupt = true });
            }

            var first = true;
            foreach (var shot in def.Shots)
            {
                cams?.SetBoothPreset(shot.Preset);
                cams?.SetLens(true, ShotFocus(shot.Preset), 2f, LensMath.FocalFromVerticalFov(PresetFov(shot.Preset)));
                var elapsed = 0f;
                var frames = 0;
                while (!InsertTiming.IsComplete(elapsed, frames, shot.Seconds))
                {
                    var flutter = BoothGlitch * (0.6f + 0.4f * Mathf.PerlinNoise(Time.unscaledTime * 31f, 0.5f));
                    fx?.CinematicGlitch(flutter, first && frames == 0 ? 1f : 0f);
                    if (shot.StaticFrame) CinematicRuntime.Overlay?.SetStatic(1f, 0.4f);
                    yield return null;
                    if (!Paused())
                    {
                        elapsed += Time.unscaledDeltaTime;
                        frames++;
                    }
                }
                if (shot.StaticFrame) CinematicRuntime.Overlay?.SetStatic(0f, 0f);
                first = false;
            }

            if (def.EndWhite)
            {
                fx?.Hold(Color.white);
                fx?.CinematicGlitch(0f, 0f);
                cams?.Release(this);
                if (handle != 0) _vo.Value?.Stop(handle);
                End(def.Id);
                yield break;
            }

            cams?.Release(this);
            if (!def.LineOutlivesInsert && handle != 0) _vo.Value?.Stop(handle);
            for (var t = 0f; t < InsertTiming.GlitchSeconds; t += Dt())
            {
                fx?.CinematicGlitch(1f - t / InsertTiming.GlitchSeconds, 0f);
                yield return null;
            }
            fx?.CinematicGlitch(0f, 0f);
            End(def.Id);
        }

        // ------------------------------------------------------------------ death insert (GDD §8.4)

        private IEnumerator RunDeath()
        {
            Begin(SequenceIds.InsertDeath);
            var fx = CinematicRuntime.ScreenFx;
            var cams = CinematicRuntime.Cameras;
            var overlay = CinematicRuntime.Overlay;

            // 0.6 s desaturated slow-down to 0.3 (TECH_PLAN §5.4 Death priority). WP07's death flow runs it before
            // calling PlayDeath (REQUESTS.md): skip it when the screen is already desaturated.
            var ts = _time.Value;
            var slowDone = fx != null && fx.DeathAmount >= 0.5f;
            for (var t = 0f; !slowDone && t < InsertTable.DeathSlowSeconds; t += Dt())
            {
                var k = t / InsertTable.DeathSlowSeconds;
                fx?.Death(k);
                if (ts != null)
                {
                    var scale = Mathf.Lerp(1f, InsertTable.DeathSlowScale, k);
                    if (_deathTimeHandle == 0 || !ts.SetScale(_deathTimeHandle, scale))
                        _deathTimeHandle = ts.Push(scale, TimePriority.Death, "Death slow-down");
                }
                yield return null;
            }
            fx?.Death(1f);

            // Freeze and glitch into the chair.
            if (ts != null) _timeHandle = ts.Push(0f, TimePriority.Insert, "Death insert");
            AudioListener.pause = true;
            CinematicAudio.Stinger(CinematicCues.StingerDeath);
            for (var t = 0f; t < InsertTiming.GlitchSeconds; t += Dt())
            {
                fx?.CinematicGlitch(t / InsertTiming.GlitchSeconds, 0f);
                yield return null;
            }
            var booth = BoothRoot.Current;
            if (booth != null)
            {
                booth.SetResting();
                booth.AllStatic();
                booth.Tube?.SetMode(FlickerMode.Unstable);
            }
            cams?.Push(this, CameraMode.Booth);
            if (cams != null)
            {
                cams.SetBoothPreset(BoothCameraPreset.Pov);
                cams.SetLens(true, 1.3f, 2.8f, LensMath.FocalFromVerticalFov(BoothLayout.PovFov));
                cams.Look.Enabled = false;
            }
            fx?.Death(0f);
            var line = _deathLines.Next();
            _vo.Value?.Play(line, new VOOptions { Priority = VOPriority.Scripted, Interrupt = true });

            var elapsed = 0f;
            var frames = 0;
            while (!InsertTiming.IsComplete(elapsed, frames, InsertTable.DeathSeconds))
            {
                fx?.CinematicGlitch(frames == 0 ? 1f : BoothGlitch * 0.5f, frames == 0 ? 1f : 0f);
                yield return null;
                if (!Paused())
                {
                    elapsed += Time.unscaledDeltaTime;
                    frames++;
                }
            }

            // Static, then black with the teletype line while the checkpoint reloads.
            for (var t = 0f; t < 0.3f; t += Dt())
            {
                overlay?.SetStatic(1f, 0.6f);
                yield return null;
            }
            overlay?.SetStatic(0f, 0f);
            fx?.CinematicGlitch(0f, 0f);
            fx?.Hold(Color.black);
            cams?.Release(this);
            if (booth != null && booth.Tube != null) booth.Tube.SetMode(FlickerMode.On);
            PopTime();

            _resumed = false;
            var callbacks = _deathCallbacks.ToArray();
            _deathCallbacks.Clear();
            foreach (var cb in callbacks)
            {
                try
                {
                    cb();
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Cinematics, e, this);
                }
            }

            // WP07's death flow takes over from here (FadeToBlack, IHud.Banner "SESSION 12 — RESUMING", restore,
            // FadeIn): release the game now. Without a HUD (test scenes, no mission) type the line and fade in ourselves.
            var typeText = !GameServices.TryGet(out IHud _);
            if (!typeText)
            {
                End(SequenceIds.InsertDeath);
                yield break;
            }
            var type = new TypewriterModel(InsertTable.ResumingText, 22f, false);
            var black = 0f;
            var typed = 0;
            while (black < MinResumeBlack || (!_resumed && black < ResumeTimeout) || (typeText && black < type.Duration + 0.3f))
            {
                if (typeText)
                {
                    type.Build(_sb, black);
                    overlay?.SetLowerText(_sb, CinematicOverlay.Phosphor);
                }
                var n = typeText ? type.TypedCount(black) : 0;
                if (n > typed)
                {
                    typed = n;
                    CinematicAudio.Play2D(CinematicCues.TypeKey, 0.5f);
                }
                yield return null;
                if (!Paused()) black += Time.unscaledDeltaTime;
            }
            overlay?.SetLowerText(null, Color.white);
            fx?.FadeIn(ResumeFadeIn);
            End(SequenceIds.InsertDeath);
        }

        // ------------------------------------------------------------------ shared

        private void Begin(string id)
        {
            _current = id;
            GameEvents.RaiseInsertStarted(id);
            var ts = _time.Value;
            if (ts != null && id != SequenceIds.InsertDeath) _timeHandle = ts.Push(0f, TimePriority.Insert, "Insert " + id);
            var locks = _lock.Value;
            if (locks != null) _lockHandle = locks.Lock(InputLockFlags.All, "Insert " + id);
            if (id != SequenceIds.InsertDeath) AudioListener.pause = true;
        }

        private void End(string id)
        {
            RestoreAll(id);
        }

        private void RestoreAll(string id)
        {
            PopTime();
            var locks = _lock.Value;
            if (locks != null && _lockHandle != 0) locks.Unlock(_lockHandle);
            _lockHandle = 0;
            var paused = _pause.Value != null && _pause.Value.IsPaused;
            AudioListener.pause = paused;
            var fx = CinematicRuntime.ScreenFx;
            fx?.CinematicGlitch(0f, 0f);
            fx?.Death(0f);
            CinematicRuntime.Cameras?.Release(this);
            CinematicRuntime.Overlay?.SetStatic(0f, 0f);
            _current = null;
            GameEvents.RaiseInsertEnded(id);
        }

        private void PopTime()
        {
            var ts = _time.Value;
            if (ts == null) return;
            if (_timeHandle != 0) ts.Pop(_timeHandle);
            if (_deathTimeHandle != 0) ts.Pop(_deathTimeHandle);
            _timeHandle = 0;
            _deathTimeHandle = 0;
        }

        private bool Paused() => _pause.Value != null && _pause.Value.IsPaused;

        private float Dt() => Paused() ? 0f : Time.unscaledDeltaTime;

        private void OnResumed(string cp) => _resumed = true;

        private void OnRespawned() => _resumed = true;

        private static float PresetFov(BoothCameraPreset preset)
        {
            BoothLayout.Preset(preset, out _, out _, out var fov);
            return fov;
        }

        private static float ShotFocus(BoothCameraPreset preset)
        {
            BoothLayout.Preset(preset, out var p, out _, out _);
            switch (preset)
            {
                case BoothCameraPreset.PenEcu: return Vector3.Distance(p, BoothLayout.Pen);
                case BoothCameraPreset.Clock: return Vector3.Distance(p, BoothLayout.Clock);
                case BoothCameraPreset.CrtEcu: return Vector3.Distance(p, BoothLayout.Crt[1]) - 0.2f;
                case BoothCameraPreset.EyeEcu: return Vector3.Distance(p, BoothLayout.EyePoint);
                case BoothCameraPreset.FilmCam: return Vector3.Distance(p, BoothLayout.FilmCamTarget);
                default: return 1.3f;
            }
        }
    }
}
