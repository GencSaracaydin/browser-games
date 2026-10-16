using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Ambience
{
    /// <summary>
    /// A positional set-piece loop placed on a prop (TECH_PLAN §7.9; GDD §10.2 set-piece sounds): the sodium-lamp ballast
    /// buzz after the practicals strike, the fluorescent buzz, the transmitter hum, the cassette radio in the guard hut,
    /// a truck idling, a reel-to-reel. It plays through <see cref="IAudioService.PlayLoop"/> while its conditions hold
    /// (LightStage window, beat window, start/stop mission events, a destructible that silences it) and the listener is
    /// within <see cref="_cullDistance"/> (it stops beyond, so far loops cost no voices). With
    /// <see cref="_strikeOnPracticals"/> it plays a one-shot when <c>GameEvents.PracticalsStrike</c> fires, after a random
    /// delay, so a street of lamps strikes as a ripple (GDD §8.5 shot 4).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoopEmitter : MonoBehaviour
    {
        [SerializeField, Tooltip("Loop cue (LIB_Audio).")] private string _cue = "SFX_SODIUM_BALLAST";
        [SerializeField, Tooltip("Plays while LightStage s is inside [x, y] (sodium lamps: 1.97 → 99).")]
        private Vector2 _sWindow = new Vector2(-99f, 99f);
        [SerializeField, Tooltip("Beat window (-1 = open).")] private int _beatFrom = -1;
        [SerializeField] private int _beatTo = -1;
        [SerializeField, Tooltip("Mission event that switches it on (empty = on by default).")] private string _startEvent;
        [SerializeField, Tooltip("Mission event that switches it off.")] private string _stopEvent;
        [SerializeField, Tooltip("Destructible id (IDestructible.Id) that silences it when destroyed (floods, racks).")]
        private string _destructibleId;
        [SerializeField, Tooltip("Listener distance beyond which the loop stops (≈ the cue's max distance).")]
        private float _cullDistance = 45f;
        [SerializeField] private float _fade = 0.6f;
        [SerializeField, Tooltip("One-shot on GameEvents.PracticalsStrike (sodium lamps: SFX_SODIUM_STRIKE).")]
        private bool _strikeOnPracticals;
        [SerializeField] private string _strikeCue = "SFX_SODIUM_STRIKE";
        [SerializeField, Tooltip("Random delay range of the strike (s).")] private Vector2 _strikeDelay = new Vector2(0f, 0.9f);

        private ServiceRef<IAudioService> _audio;
        private ServiceRef<ILightStage> _light;
        private ServiceRef<IMissionDirector> _mission;
        private int _handle;
        private bool _switchedOn;
        private int _beat = -1;
        private float _strikeAt = -1f;
        private float _nextCheck;

        /// <summary>True while the loop plays.</summary>
        public bool IsPlaying => _handle != 0;

        /// <summary>Configures the emitter (builders, menu).</summary>
        public void Configure(string cue, Vector2 sWindow, int beatFrom, int beatTo, float cullDistance, bool strikeOnPracticals)
        {
            _cue = cue;
            _sWindow = sWindow;
            _beatFrom = beatFrom;
            _beatTo = beatTo;
            _cullDistance = Mathf.Max(1f, cullDistance);
            _strikeOnPracticals = strikeOnPracticals;
        }

        /// <summary>Configures event switching (builders).</summary>
        public void ConfigureEvents(string startEvent, string stopEvent, string destructibleId)
        {
            _startEvent = startEvent;
            _stopEvent = stopEvent;
            _destructibleId = destructibleId;
        }

        private void OnEnable()
        {
            _switchedOn = string.IsNullOrEmpty(_startEvent);
            GameEvents.MissionEvent += OnMissionEvent;
            GameEvents.BeatStarted += OnBeat;
            GameEvents.PracticalsStrike += OnStrike;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.MissionEvent -= OnMissionEvent;
            GameEvents.BeatStarted -= OnBeat;
            GameEvents.PracticalsStrike -= OnStrike;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            Stop(0.05f);
        }

        private void Update()
        {
            var now = AudioRuntime.Now;
            if (_strikeAt >= 0f && now >= _strikeAt)
            {
                _strikeAt = -1f;
                if (Near(_cullDistance * 4f)) _audio.Value?.Play(_strikeCue, transform.position);
            }
            if (now < _nextCheck) return;
            _nextCheck = now + 0.25f;
            var want = ShouldPlay();
            if (want && _handle == 0 && Near(_cullDistance))
            {
                var audio = _audio.Value;
                if (audio != null) _handle = audio.PlayLoop(_cue, transform);
            }
            else if (_handle != 0 && (!want || !Near(_cullDistance * 1.15f))) Stop(_fade);
        }

        /// <summary>True when every condition holds (ignores the distance).</summary>
        public bool ShouldPlay()
        {
            if (!_switchedOn || string.IsNullOrEmpty(_cue)) return false;
            var light = _light.Value;
            if (light != null && (light.S < _sWindow.x || light.S > _sWindow.y)) return false;
            if (!InBeat()) return false;
            if (!string.IsNullOrEmpty(_destructibleId) && Destructibles.TryGet(_destructibleId, out var d) && d != null && d.IsDestroyed) return false;
            return true;
        }

        private bool Near(float distance) =>
            (transform.position - AudioRuntime.ListenerPosition).sqrMagnitude <= distance * distance;

        private bool InBeat()
        {
            var beat = _beat;
            var m = _mission.Value;
            if (m != null && m.IsRunning) beat = m.Beat;
            if (beat < 0) return true;
            return (_beatFrom < 0 || beat >= _beatFrom) && (_beatTo < 0 || beat <= _beatTo);
        }

        private void Stop(float fade)
        {
            if (_handle == 0) return;
            _audio.Value?.StopLoop(_handle, fade);
            _handle = 0;
        }

        private void OnMissionEvent(string id)
        {
            if (!string.IsNullOrEmpty(_startEvent) && id == _startEvent) _switchedOn = true;
            else if (!string.IsNullOrEmpty(_stopEvent) && id == _stopEvent) _switchedOn = false;
        }

        private void OnBeat(int beat) => _beat = beat;

        private void OnStrike()
        {
            if (_strikeOnPracticals) _strikeAt = AudioRuntime.Now + Random.Range(_strikeDelay.x, _strikeDelay.y);
        }

        private void OnCheckpointLoaded(string cpId)
        {
            _switchedOn = string.IsNullOrEmpty(_startEvent);
            _strikeAt = -1f;
            Stop(0.1f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.7f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _cullDistance);
        }
#endif
    }
}
