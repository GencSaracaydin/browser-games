using System.Collections.Generic;
using LongShadow.Combat.Data;
using UnityEngine;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// A decal material with named atlas elements (WP10 layout JSON: "plaster_1", "concrete_3" ...), grouped by the name
    /// prefix before the last underscore. Picks a random element of a group with its UV rect and real-world size.
    /// </summary>
    public sealed class DecalAtlas
    {
        private readonly Dictionary<string, List<int>> _groups = new Dictionary<string, List<int>>();
        private readonly Vector4[] _rects;
        private readonly Vector2[] _sizes;

        /// <summary>The decal material (null = decals disabled).</summary>
        public readonly Material Material;

        /// <summary>Builds from a library entry (material + named rects + sizes).</summary>
        public DecalAtlas(CombatFxEntry entry) : this(entry?.Material, entry?.RectNames, entry?.Rects, entry?.Sizes) { }

        /// <summary>Builds from raw data; with no rects the whole texture is one element named "all".</summary>
        public DecalAtlas(Material material, string[] names, Vector4[] rects, Vector2[] sizes, float defaultSize = 0.09f)
        {
            Material = material;
            if (rects == null || rects.Length == 0)
            {
                _rects = new[] { new Vector4(0f, 0f, 1f, 1f) };
                _sizes = new[] { new Vector2(defaultSize, defaultSize) };
                _groups["all"] = new List<int> { 0 };
                return;
            }
            _rects = rects;
            _sizes = new Vector2[rects.Length];
            for (var i = 0; i < rects.Length; i++)
            {
                _sizes[i] = sizes != null && i < sizes.Length && sizes[i].x > 0f ? sizes[i] : new Vector2(defaultSize, defaultSize);
                var n = names != null && i < names.Length ? names[i] : "all";
                var cut = n.LastIndexOf('_');
                var group = cut > 0 ? n.Substring(0, cut) : n;
                if (!_groups.TryGetValue(group, out var list)) _groups[group] = list = new List<int>();
                list.Add(i);
                if (!_groups.TryGetValue("all", out var all)) _groups["all"] = all = new List<int>();
                all.Add(i);
            }
        }

        /// <summary>True when decals can be drawn.</summary>
        public bool IsValid => Material != null;

        /// <summary>Picks a random element of a group (falls back to "all").</summary>
        public bool TryPick(string group, out Vector4 rect, out Vector2 size)
        {
            if (!(group != null && _groups.TryGetValue(group, out var list)) && !_groups.TryGetValue("all", out list))
            {
                rect = default;
                size = default;
                return false;
            }
            var i = list[Random.Range(0, list.Count)];
            rect = _rects[i];
            size = _sizes[i];
            return true;
        }
    }
}
