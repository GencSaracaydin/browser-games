using System;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>One rule of <see cref="PracticalBinder"/>: markers whose id matches get a Look prefab if they have none.</summary>
    [Serializable]
    public sealed class PracticalBindRule
    {
        [SerializeField, Tooltip("Exact marker id, or a prefix when it ends with '.' or '*' (\"LAMP.\", \"B6.F*\").")]
        private string _match;
        [SerializeField, Tooltip("Prefab to attach under the marker.")]
        private GameObject _prefab;
        [SerializeField, Tooltip("Optional world point the prefab's forward aims at (floods: (345, 0, 325)).")]
        private bool _aim;
        [SerializeField] private Vector3 _aimPoint;

        /// <summary>Match pattern.</summary>
        public string Match => _match;
        /// <summary>Prefab.</summary>
        public GameObject Prefab => _prefab;
        /// <summary>True when the prefab aims at <see cref="AimPoint"/>.</summary>
        public bool Aim => _aim;
        /// <summary>Aim point.</summary>
        public Vector3 AimPoint => _aimPoint;

        /// <summary>Creates a rule.</summary>
        public PracticalBindRule(string match, GameObject prefab, bool aim = false, Vector3 aimPoint = default)
        {
            _match = match;
            _prefab = prefab;
            _aim = aim;
            _aimPoint = aimPoint;
        }

        /// <summary>True if <paramref name="id"/> matches the rule.</summary>
        public bool Matches(string id) => Matches(_match, id);

        /// <summary>Pattern test: exact, or prefix when the pattern ends with '.' or '*'.</summary>
        public static bool Matches(string pattern, string id)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(id)) return false;
            var last = pattern[pattern.Length - 1];
            if (last == '*') return id.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.Ordinal);
            if (last == '.') return id.StartsWith(pattern, StringComparison.Ordinal);
            return string.Equals(pattern, id, StringComparison.Ordinal);
        }
    }
}
