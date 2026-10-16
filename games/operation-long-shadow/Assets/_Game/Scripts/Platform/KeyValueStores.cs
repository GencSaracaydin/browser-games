using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>The <see cref="IKeyValueStore"/> implementations and the default choice per platform.</summary>
    internal static class KeyValueStores
    {
        /// <summary>
        /// localStorage on the web (falling back to memory per write when it throws), PlayerPrefs elsewhere
        /// (TECH_PLAN §7.1: "localStorage in try/catch, PlayerPrefs in the editor").
        /// </summary>
        internal static IKeyValueStore CreateDefault()
        {
            if (WebNative.Available)
            {
                if (WebNative.StorageAvailable()) return new FallbackStore(new WebStorageStore(), new MemoryStore());
                LSLog.Warn(LogCategory.Settings, "localStorage is unavailable (private window or blocked storage); options last for this session only.");
                return new MemoryStore();
            }
            return new PlayerPrefsStore();
        }

        /// <summary>In-memory store (tests, private windows).</summary>
        internal sealed class MemoryStore : IKeyValueStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
            public string Name => "memory";
            public bool IsPersistent => false;
            public string Get(string key) => key != null && _values.TryGetValue(key, out var v) ? v : null;

            public bool Set(string key, string value)
            {
                if (key == null) return false;
                _values[key] = value ?? string.Empty;
                return true;
            }

            public bool Remove(string key) => key != null && _values.Remove(key);
        }

        /// <summary>Browser localStorage through the jslib (each call is try/catch on the JS side).</summary>
        internal sealed class WebStorageStore : IKeyValueStore
        {
            public string Name => "localStorage";
            public bool IsPersistent => true;
            public string Get(string key) => WebNative.StorageGet(key);
            public bool Set(string key, string value) => WebNative.StorageSet(key, value ?? string.Empty);
            public bool Remove(string key) => WebNative.StorageRemove(key);
        }

        /// <summary>PlayerPrefs (editor and non-web players).</summary>
        internal sealed class PlayerPrefsStore : IKeyValueStore
        {
            public string Name => "PlayerPrefs";
            public bool IsPersistent => true;

            public string Get(string key)
            {
                try
                {
                    return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Settings, e);
                    return null;
                }
            }

            public bool Set(string key, string value)
            {
                try
                {
                    PlayerPrefs.SetString(key, value ?? string.Empty);
                    PlayerPrefs.Save();
                    return true;
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Settings, e);
                    return false;
                }
            }

            public bool Remove(string key)
            {
                try
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                    return true;
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Settings, e);
                    return false;
                }
            }
        }

        /// <summary>Primary store with a memory shadow: reads prefer values the primary refused to keep.</summary>
        internal sealed class FallbackStore : IKeyValueStore
        {
            private readonly IKeyValueStore _primary;
            private readonly MemoryStore _shadow;
            private readonly HashSet<string> _shadowed = new HashSet<string>(StringComparer.Ordinal);

            internal FallbackStore(IKeyValueStore primary, MemoryStore shadow)
            {
                _primary = primary;
                _shadow = shadow;
            }

            public string Name => _primary.Name + "+memory";
            public bool IsPersistent => _shadowed.Count == 0 && _primary.IsPersistent;

            public string Get(string key) => _shadowed.Contains(key) ? _shadow.Get(key) : _primary.Get(key);

            public bool Set(string key, string value)
            {
                if (_primary.Set(key, value))
                {
                    _shadowed.Remove(key);
                    _shadow.Remove(key);
                    return true;
                }
                LSLog.WarnOnce("store-refused:" + key, LogCategory.Settings, _primary.Name + " refused '" + key + "'; kept in memory for this session.");
                _shadowed.Add(key);
                return _shadow.Set(key, value);
            }

            public bool Remove(string key)
            {
                _shadowed.Remove(key);
                _shadow.Remove(key);
                return _primary.Remove(key);
            }
        }
    }
}
