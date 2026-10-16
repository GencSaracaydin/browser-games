using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// Shows or hides level objects on a <see cref="GameEvents.MissionEvent"/> (level scripting without a Mission
    /// dependency): the static fire prop and burnt technical on <c>B4.TechnicalIgnited</c> (WP04), the emergency lamps on
    /// <c>B9.TransmitterDead</c>, the S8 copy of the fire from the start of Beat 11. The state is saved in checkpoints so a
    /// reload after the event keeps the fire burning.
    /// </summary>
    public sealed class MissionEventSwitch : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("Mission event that switches the objects on.")]
        private string _onEvent;
        [SerializeField, Tooltip("Optional mission event that switches them off again.")]
        private string _offEvent;
        [SerializeField, Tooltip("Shown when on.")]
        private GameObject[] _show = new GameObject[0];
        [SerializeField, Tooltip("Hidden when on.")]
        private GameObject[] _hide = new GameObject[0];
        [SerializeField, Tooltip("Initial state.")]
        private bool _startsOn;
        [SerializeField, Tooltip("Looping cue while on (fire crackle), empty = none.")]
        private string _loopCue;
        [SerializeField, Tooltip("Save key suffix (unique per switch).")]
        private string _key;

        private bool _on;
        private int _loopHandle = -1;
        private SaveRegistration _save;
        private ServiceRef<IAudioService> _audio;

        /// <summary>True while on.</summary>
        public bool IsOn => _on;

        /// <summary>Configures the switch (editor builders).</summary>
        public void Configure(string key, string onEvent, string offEvent, GameObject[] show, GameObject[] hide, bool startsOn, string loopCue)
        {
            _key = key;
            _onEvent = onEvent;
            _offEvent = offEvent;
            _show = show ?? new GameObject[0];
            _hide = hide ?? new GameObject[0];
            _startsOn = startsOn;
            _loopCue = loopCue;
        }

        private void Awake()
        {
            _save = new SaveRegistration(this);
            Apply(_startsOn);
        }

        private void Start()
        {
            _save.TryRegister();
            if (_on) StartLoop();
        }

        private void OnEnable()
        {
            GameEvents.MissionEvent += OnMissionEvent;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.MissionEvent -= OnMissionEvent;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            StopLoop();
        }

        private void OnDestroy() => _save?.Unregister();

        private void OnCheckpointLoaded(string id) => _save.TryRegister();

        private void OnMissionEvent(string id)
        {
            if (!string.IsNullOrEmpty(_onEvent) && id == _onEvent) Set(true);
            else if (!string.IsNullOrEmpty(_offEvent) && id == _offEvent) Set(false);
        }

        /// <summary>Switches on or off.</summary>
        public void Set(bool on)
        {
            if (on == _on) return;
            Apply(on);
            if (on) StartLoop();
            else StopLoop();
        }

        private void Apply(bool on)
        {
            _on = on;
            foreach (var g in _show)
                if (g != null) g.SetActive(on);
            foreach (var g in _hide)
                if (g != null) g.SetActive(!on);
        }

        private void StartLoop()
        {
            if (string.IsNullOrEmpty(_loopCue) || _loopHandle >= 0) return;
            var audio = _audio.Value;
            if (audio != null) _loopHandle = audio.PlayLoop(_loopCue, transform);
        }

        private void StopLoop()
        {
            if (_loopHandle < 0) return;
            _audio.Value?.StopLoop(_loopHandle, 0.5f);
            _loopHandle = -1;
        }

        /// <inheritdoc/>
        public string SaveKey => "LevelSwitch." + (string.IsNullOrEmpty(_key) ? name : _key);

        /// <inheritdoc/>
        public void Capture(SaveWriter w) => w.Write("on", _on);

        /// <inheritdoc/>
        public void Restore(SaveReader r) => Set(r.ReadBool("on", _startsOn));
    }
}
