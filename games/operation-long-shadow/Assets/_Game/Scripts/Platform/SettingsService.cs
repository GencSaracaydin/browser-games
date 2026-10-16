using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// <see cref="ISettingsService"/> (TECH_PLAN §7.1, GDD §11 Options, §17 persistence). Options are a
    /// <see cref="GameSettings"/> JSON under <see cref="StorageKeys.Settings"/> in localStorage (try/catch; memory
    /// fallback in private windows) or PlayerPrefs in the editor. <see cref="Apply"/> sanitises, notifies and schedules
    /// a save 0.5 s later (so sliders do not hammer storage); <see cref="Save"/> writes now. A difficulty change takes
    /// effect at the next checkpoint save/load (GDD §11), immediately when no mission is running. Keys starting with "?"
    /// read the launch URL (<see cref="LaunchOptions"/>): <c>GetValue("?cp")</c>. First run on Safari defaults to
    /// performance mode (TECH_PLAN §3.3).
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform - 5)]
    [DisallowMultipleComponent]
    public sealed class SettingsService : MonoBehaviour, ISettingsService
    {
        [SerializeField, Tooltip("Seconds between Apply and the automatic save.")]
        private float _saveDelay = 0.5f;

        private IKeyValueStore _store;
        private GameSettings _current = new GameSettings();
        private Difficulty _difficultyInEffect = LongShadow.Core.Difficulty.Regular;
        private DifficultyProfile _difficulty = DifficultyProfile.For(LongShadow.Core.Difficulty.Regular);
        private ServiceRef<IMissionDirector> _director;
        private bool _dirty;
        private float _saveAt = -1f;
        private bool _initialised;

        /// <inheritdoc/>
        public event Action Changed;

        /// <inheritdoc/>
        public GameSettings Current => _current;
        /// <inheritdoc/>
        public DifficultyProfile Difficulty => _difficulty;
        /// <summary>True if the options survive a reload (false in private windows).</summary>
        public bool IsPersistent => _store != null && _store.IsPersistent;
        /// <summary>True when the options came from storage (false on a first run or after a reset).</summary>
        public bool LoadedFromStorage { get; private set; }

        private void Awake()
        {
            if (!_initialised) Initialize(KeyValueStores.CreateDefault());
            GameServices.Register<ISettingsService>(this);
        }

        private void OnEnable()
        {
            GameEvents.CheckpointSaved += OnCheckpoint;
            GameEvents.CheckpointLoaded += OnCheckpoint;
        }

        private void OnDisable()
        {
            GameEvents.CheckpointSaved -= OnCheckpoint;
            GameEvents.CheckpointLoaded -= OnCheckpoint;
        }

        private void OnDestroy()
        {
            if (_dirty) Save();
            GameServices.Unregister<ISettingsService>(this);
        }

        private void OnApplicationQuit()
        {
            if (_dirty) Save();
        }

        /// <summary>Loads the options from <paramref name="store"/> (Awake calls it with the platform store; tests inject one).</summary>
        internal void Initialize(IKeyValueStore store)
        {
            _store = store ?? new KeyValueStores.MemoryStore();
            _initialised = true;
            var json = _store.Get(StorageKeys.Settings);
            LoadedFromStorage = !string.IsNullOrEmpty(json);
            _current = LoadedFromStorage ? GameSettings.FromJson(json) : FirstRunDefaults();
            if (LaunchOptions.Current.ForcePerformanceMode) _current.PerformanceMode = true;
            _difficultyInEffect = _current.Difficulty;
            _difficulty = DifficultyProfile.For(_difficultyInEffect);
            _dirty = false;
            LSLog.Info(LogCategory.Settings, "Options " + (LoadedFromStorage ? "loaded" : "defaults") + " (" + _store.Name + ")");
        }

        /// <inheritdoc/>
        public void Apply(GameSettings settings)
        {
            if (settings == null) return;
            _current = settings.Clone().Sanitize();
            var director = _director.Value;
            if (director == null || !director.IsRunning) ApplyPendingDifficulty();
            _dirty = true;
            _saveAt = Time.unscaledTime + _saveDelay;
            Changed?.Invoke();
            GameEvents.RaiseSettingsChanged();
        }

        /// <inheritdoc/>
        public void Save()
        {
            if (_store == null) return;
            _dirty = false;
            _saveAt = -1f;
            if (!_store.Set(StorageKeys.Settings, _current.ToJson()))
                LSLog.WarnOnce("settings-save", LogCategory.Settings, "Options could not be saved (" + _store.Name + ")");
        }

        /// <summary>Restores GDD defaults (Options "reset"), saves and notifies.</summary>
        public void ResetToDefaults()
        {
            Apply(FirstRunDefaults());
            Save();
        }

        /// <inheritdoc/>
        public string GetValue(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (key[0] == '?') return LaunchOptions.Current.Get(key.Substring(1));
            return _store?.Get(key);
        }

        /// <inheritdoc/>
        public void SetValue(string key, string value)
        {
            if (string.IsNullOrEmpty(key) || _store == null) return;
            if (key[0] == '?')
            {
                LSLog.Warn(LogCategory.Settings, "Launch options are read-only: " + key);
                return;
            }
            if (value == null) _store.Remove(key);
            else _store.Set(key, value);
        }

        private void Update()
        {
            if (_dirty && _saveAt >= 0f && Time.unscaledTime >= _saveAt) Save();
        }

        private void OnCheckpoint(string cpId) => ApplyPendingDifficulty();

        private void ApplyPendingDifficulty()
        {
            if (_difficultyInEffect == _current.Difficulty) return;
            _difficultyInEffect = _current.Difficulty;
            _difficulty = DifficultyProfile.For(_difficultyInEffect);
            LSLog.Info(LogCategory.Settings, "Difficulty now " + _difficultyInEffect);
        }

        private static GameSettings FirstRunDefaults()
        {
            var s = new GameSettings();
            if (WebBridge.IsSafari) s.PerformanceMode = true; // TECH_PLAN §3.3: Safari default until the Safari pass
            return s;
        }
    }
}
