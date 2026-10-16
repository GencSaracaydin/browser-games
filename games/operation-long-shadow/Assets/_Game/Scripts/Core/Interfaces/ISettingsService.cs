using System;

namespace LongShadow.Core
{
    /// <summary>Options and persistent key/values (WP01 SettingsService; localStorage in try/catch, PlayerPrefs in the editor).</summary>
    public interface ISettingsService
    {
        /// <summary>Current options (do not mutate; use <see cref="Apply"/>).</summary>
        GameSettings Current { get; }
        /// <summary>Difficulty in effect (changes apply at the next checkpoint).</summary>
        DifficultyProfile Difficulty { get; }
        /// <summary>Raised after <see cref="Apply"/> (also raised as <see cref="GameEvents.SettingsChanged"/>).</summary>
        event Action Changed;
        /// <summary>Replaces the options (sanitised) and notifies listeners.</summary>
        void Apply(GameSettings settings);
        /// <summary>Persists the options.</summary>
        void Save();
        /// <summary>Reads a persisted value (see <see cref="StorageKeys"/>); null if absent.</summary>
        string GetValue(string key);
        /// <summary>Writes a persisted value immediately.</summary>
        void SetValue(string key, string value);
    }
}
