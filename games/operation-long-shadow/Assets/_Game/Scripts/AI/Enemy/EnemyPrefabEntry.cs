using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>One <see cref="EnemyLibrary"/> entry: an enemy variant id (<see cref="EnemyVariants"/>) and its prefab.</summary>
    [Serializable]
    public sealed class EnemyPrefabEntry : ILibraryEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private GameObject _prefab;
        [SerializeField, Tooltip("Body the prefab was built on (WP05 body prefab path or \"DebugCapsuleBody\").")]
        private string _bodySource;

        /// <summary>Serializer constructor.</summary>
        public EnemyPrefabEntry() { }

        /// <summary>Creates an entry (EnemyPrefabBuilder).</summary>
        public EnemyPrefabEntry(string id, GameObject prefab, string bodySource)
        {
            _id = id;
            _prefab = prefab;
            _bodySource = bodySource;
        }

        /// <inheritdoc/>
        public string Id => _id;
        /// <summary>Enemy prefab (EnemyActor + EnemyBrain + EnemyHealth + ICharacterBody + behaviours).</summary>
        public GameObject Prefab => _prefab;
        /// <summary>Body source (diagnostics).</summary>
        public string BodySource => _bodySource;
    }
}
