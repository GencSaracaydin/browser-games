using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Named points a structure generator leaves for the section builder: where doors hinge, where props, lights, spawn
    /// points and markers go (world space). Keeps the geometry code pure while the editor side places gameplay objects.
    /// </summary>
    public sealed class StructureAnchors
    {
        private readonly Dictionary<string, Pose> _points = new Dictionary<string, Pose>();
        private readonly List<string> _order = new List<string>();

        /// <summary>Anchor keys in insertion order.</summary>
        public IReadOnlyList<string> Keys => _order;

        /// <summary>Adds or replaces an anchor with a compass heading.</summary>
        public void Add(string key, Vector3 position, float heading = 0f) => Add(key, position, Quaternion.Euler(0f, heading, 0f));

        /// <summary>Adds or replaces an anchor with a rotation.</summary>
        public void Add(string key, Vector3 position, Quaternion rotation)
        {
            if (!_points.ContainsKey(key)) _order.Add(key);
            _points[key] = new Pose(position, rotation);
        }

        /// <summary>Finds an anchor.</summary>
        public bool TryGet(string key, out Pose pose) => _points.TryGetValue(key, out pose);

        /// <summary>An anchor's position (or the fallback).</summary>
        public Vector3 Position(string key, Vector3 fallback = default) => _points.TryGetValue(key, out var p) ? p.position : fallback;

        /// <summary>Every anchor whose key starts with a prefix ("Seat.").</summary>
        public IEnumerable<KeyValuePair<string, Pose>> WithPrefix(string prefix)
        {
            foreach (var k in _order)
                if (k.StartsWith(prefix)) yield return new KeyValuePair<string, Pose>(k, _points[k]);
        }
    }
}
