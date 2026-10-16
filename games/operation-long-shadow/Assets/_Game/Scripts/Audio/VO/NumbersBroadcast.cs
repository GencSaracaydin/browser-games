using System.Collections.Generic;
using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// The live broadcast in the basement (GDD §7.9, TECH_PLAN §7.9): <c>NUM_BASEMENT_LOOP</c> (interval signal + the
    /// Clockmaker's digits, spliced from our own tokens) plays from the room speaker <c>B9.Speaker</c> with the music
    /// ducked under it; when the 4th transmitter rack (<c>B9.R1</c>…<c>R4</c>) dies the loop cuts and <c>D066</c> plays from
    /// the same speaker ("Zero. Six. Fo..." cut to static, baked), then the speaker hisses and dies away.
    /// <para>Starts on <c>BeatStarted(9)</c>, on the checkpoint <c>CP9</c>, or on the mission event
    /// <c>B9.Numbers.Start</c>; stops on a later beat, a checkpoint reload elsewhere, or <c>B9.Numbers.Stop</c>.
    /// <c>D066</c> is requested through <see cref="IVOService"/>, so a beat script that requests it too gets the same
    /// line (requests of a playing line are merged).</para>
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Audio + 10)]
    [DisallowMultipleComponent]
    public sealed class NumbersBroadcast : MonoBehaviour
    {
        /// <summary>Mission event that starts the broadcast.</summary>
        public const string StartEvent = AudioEvents.NumbersStart;
        /// <summary>Mission event that stops it.</summary>
        public const string StopEvent = AudioEvents.NumbersStop;

        [SerializeField] private string _loopCue = "NUM_BASEMENT_LOOP";
        [SerializeField] private string _staticCue = "SFX_RADIO_STATIC_LOOP";
        [SerializeField] private string _cutLine = "D066";
        [SerializeField, Tooltip("GDD §7.0 Beat 9 THE CLOCKMAKER.")] private int _beat = 9;
        [SerializeField] private string _checkpoint = SequenceIds.CP9;
        [SerializeField] private string[] _racks = { "B9.R1", "B9.R2", "B9.R3", "B9.R4" };
        [SerializeField, Tooltip("MUS_04 ducks under the broadcast (GDD §7.9).")] private float _musicDuckDb = -8f;
        [SerializeField] private float _staticSeconds = 4f;

        private readonly HashSet<string> _destroyed = new HashSet<string>(System.StringComparer.Ordinal);
        private ServiceRef<IAudioService> _audio;
        private ServiceRef<IVOService> _vo;
        private Transform _fallbackSpeaker;
        private int _loopHandle;
        private int _staticHandle;
        private bool _running;
        private float _staticStartAt = -1f;
        private float _staticStopAt = -1f;
        private int _holdKey;

        /// <summary>True while the loop plays.</summary>
        public bool IsRunning => _running;

        private void Awake() => _holdKey = AudioRuntime.NewKey();

        private void OnEnable()
        {
            GameEvents.BeatStarted += OnBeatStarted;
            GameEvents.MissionEvent += OnMissionEvent;
            GameEvents.DestructibleDestroyed += OnDestroyed;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.BeatStarted -= OnBeatStarted;
            GameEvents.MissionEvent -= OnMissionEvent;
            GameEvents.DestructibleDestroyed -= OnDestroyed;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            End(0f);
        }

        private void Update()
        {
            var now = AudioRuntime.Now;
            if (_staticStartAt >= 0f && now >= _staticStartAt)
            {
                _staticStartAt = -1f;
                _staticHandle = _audio.Value?.PlayLoop(_staticCue, Speaker()) ?? 0;
                _staticStopAt = now + _staticSeconds;
            }
            if (_staticStopAt >= 0f && now >= _staticStopAt)
            {
                _staticStopAt = -1f;
                _audio.Value?.StopLoop(_staticHandle, 2.5f);
                _staticHandle = 0;
            }
        }

        /// <summary>Starts the loop (idempotent).</summary>
        public void Begin()
        {
            if (_running) return;
            _destroyed.Clear();
            if (AllRacksDown()) return;
            var audio = _audio.Value;
            if (audio == null) return;
            _loopHandle = audio.PlayLoop(_loopCue, Speaker());
            _running = true;
            AudioRuntime.Mix.SetHold(_holdKey, MixCategory.Music, _musicDuckDb);
        }

        /// <summary>Cuts the broadcast as the last rack dies: loop off, D066 from the speaker, then static.</summary>
        public void Cut()
        {
            if (!_running) return;
            End(0.02f);
            var vo = _vo.Value;
            var speaker = Speaker();
            var duration = 2.2f;
            if (vo != null)
            {
                vo.Play(_cutLine, new VOOptions { Speaker = speaker, Interrupt = true, Priority = VOPriority.Scripted });
                var d = vo.Duration(_cutLine);
                if (d > 0f) duration = d;
            }
            _staticStartAt = AudioRuntime.Now + Mathf.Max(0f, duration - 0.15f);
        }

        /// <summary>Stops the loop (no cut line).</summary>
        public void End(float fade)
        {
            if (_running) _audio.Value?.StopLoop(_loopHandle, fade);
            _loopHandle = 0;
            _running = false;
            AudioRuntime.Mix.ClearHolds(_holdKey);
        }

        private void OnBeatStarted(int beat)
        {
            if (beat == _beat) Begin();
            else if (beat > _beat) End(0.5f);
        }

        private void OnMissionEvent(string id)
        {
            if (id == StartEvent) Begin();
            else if (id == StopEvent) End(0.3f);
        }

        private void OnCheckpointLoaded(string cpId)
        {
            End(0f);
            _staticStartAt = _staticStopAt = -1f;
            if (_staticHandle != 0) _audio.Value?.StopLoop(_staticHandle, 0f);
            _staticHandle = 0;
            if (cpId == _checkpoint) Begin();
        }

        private void OnDestroyed(string id)
        {
            if (!IsRack(id)) return;
            _destroyed.Add(id);
            if (!AllRacksDown()) return;
            // The station's tubes lose power with the last rack (AmbienceDirector drops the basement bed).
            GameEvents.RaiseMissionEvent(AudioEvents.TransmittersDown);
            if (_running) Cut();
        }

        private bool IsRack(string id)
        {
            for (var i = 0; i < _racks.Length; i++)
                if (_racks[i] == id) return true;
            return false;
        }

        private bool AllRacksDown()
        {
            for (var i = 0; i < _racks.Length; i++)
            {
                var id = _racks[i];
                if (_destroyed.Contains(id)) continue;
                if (Destructibles.TryGet(id, out var d) && d != null && d.IsDestroyed) continue;
                return false;
            }
            return _racks.Length > 0;
        }

        private Transform Speaker()
        {
            if (MarkerRegistry.Instance.TryGet(VOAnchors.RoomSpeaker, out SceneMarker m)) return m.transform;
            if (_fallbackSpeaker == null)
            {
                var go = new GameObject("B9.Speaker (fallback)");
                go.transform.SetParent(transform, false);
                go.transform.position = VOAnchors.RoomSpeakerFallback;
                _fallbackSpeaker = go.transform;
            }
            return _fallbackSpeaker;
        }
    }
}
