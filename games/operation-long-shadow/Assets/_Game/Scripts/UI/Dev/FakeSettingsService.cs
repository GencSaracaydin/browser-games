using System;
using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.UI.Dev
{
    /// <summary>
    /// In-memory <see cref="ISettingsService"/> for the UI test bed and tests: Apply sanitises, notifies and counts saves;
    /// key/values live in a dictionary (optionally mirrored to PlayerPrefs by the test bed).
    /// </summary>
    public sealed class FakeSettingsService : ISettingsService
    {
        private GameSettings _current = new GameSettings();
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public GameSettings Current => _current;
        public DifficultyProfile Difficulty => DifficultyProfile.For(_current.Difficulty);
        public event Action Changed;

        /// <summary>Number of <see cref="Save"/> calls (tests).</summary>
        public int SaveCount { get; private set; }

        /// <summary>Number of <see cref="Apply"/> calls (tests).</summary>
        public int ApplyCount { get; private set; }

        public void Apply(GameSettings settings)
        {
            _current = (settings ?? new GameSettings()).Clone().Sanitize();
            ApplyCount++;
            Changed?.Invoke();
            GameEvents.RaiseSettingsChanged();
        }

        public void Save()
        {
            SaveCount++;
            _values[StorageKeys.Settings] = _current.ToJson();
        }

        public string GetValue(string key) => key != null && _values.TryGetValue(key, out var v) ? v : null;

        public void SetValue(string key, string value)
        {
            if (key == null) return;
            if (value == null) _values.Remove(key);
            else _values[key] = value;
        }
    }
}
