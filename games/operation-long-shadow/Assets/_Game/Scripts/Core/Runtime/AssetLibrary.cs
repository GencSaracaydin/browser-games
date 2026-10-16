using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Base class for the <c>LIB_*</c> ScriptableObjects (TECH_PLAN D12): asset references by id. Derive a concrete,
    /// non-generic class per domain (e.g. <c>WeaponVisualsLibrary : AssetLibrary&lt;WeaponVisualEntry&gt;</c>, asset
    /// <c>Assets/_Game/Data/Libraries/LIB_WeaponVisuals.asset</c>), fill it with an editor builder, and look it up at runtime
    /// through <see cref="Libraries"/>. A missing entry is logged once; callers fall back to <see cref="Placeholders"/>.
    /// </summary>
    public abstract class AssetLibrary<TEntry> : ScriptableObject where TEntry : class, ILibraryEntry
    {
        [SerializeField] private List<TEntry> _entries = new List<TEntry>();

        [System.NonSerialized] private Dictionary<string, TEntry> _index;

        /// <summary>All entries in authored order.</summary>
        public IReadOnlyList<TEntry> Entries => _entries;

        /// <summary>Entry count.</summary>
        public int Count => _entries.Count;

        /// <summary>Finds an entry by id (no logging).</summary>
        public bool TryGet(string id, out TEntry entry)
        {
            if (id == null)
            {
                entry = null;
                return false;
            }
            if (_index == null) BuildIndex();
            return _index.TryGetValue(id, out entry);
        }

        /// <summary>Returns the entry or null; logs a missing-asset warning once per id.</summary>
        public TEntry Get(string id)
        {
            if (TryGet(id, out var e)) return e;
            LSLog.MissingAsset(LogCategory.Assets, name + "/" + id, null, this);
            return null;
        }

        /// <summary>True if an entry exists.</summary>
        public bool Contains(string id) => TryGet(id, out _);

        /// <summary>Called when the asset loads.</summary>
        protected virtual void OnEnable() => _index = null;

        private void BuildIndex()
        {
            _index = new Dictionary<string, TEntry>(_entries.Count);
            foreach (var e in _entries)
            {
                if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                if (_index.ContainsKey(e.Id))
                {
                    LSLog.WarnOnce("lib-dup:" + name + ":" + e.Id, LogCategory.Assets, name + ": duplicate id " + e.Id + " (first wins)", this);
                    continue;
                }
                _index.Add(e.Id, e);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor builders: replaces every entry and marks the asset dirty.</summary>
        public void EditorSetEntries(IEnumerable<TEntry> entries)
        {
            _entries.Clear();
            if (entries != null) _entries.AddRange(entries);
            _index = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Editor builders: adds or replaces one entry by id and marks the asset dirty.</summary>
        public void EditorUpsert(TEntry entry)
        {
            if (entry == null) return;
            var i = _entries.FindIndex(x => x != null && x.Id == entry.Id);
            if (i >= 0) _entries[i] = entry;
            else _entries.Add(entry);
            _index = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Validates ids in the inspector.</summary>
        protected virtual void OnValidate() => _index = null;
#endif
    }
}
