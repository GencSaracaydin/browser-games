namespace LongShadow.Platform
{
    /// <summary>
    /// A string key/value store behind <see cref="SettingsService"/>: browser localStorage on the web, PlayerPrefs in the
    /// editor, memory when storage is unavailable (private windows, blocked third-party storage in frames).
    /// Implementations never throw.
    /// </summary>
    internal interface IKeyValueStore
    {
        /// <summary>Short name for logs.</summary>
        string Name { get; }
        /// <summary>True if values survive a reload.</summary>
        bool IsPersistent { get; }
        /// <summary>Value or null.</summary>
        string Get(string key);
        /// <summary>Writes a value; false if the store refused it.</summary>
        bool Set(string key, string value);
        /// <summary>Removes a key; false if the store refused it.</summary>
        bool Remove(string key);
    }
}
