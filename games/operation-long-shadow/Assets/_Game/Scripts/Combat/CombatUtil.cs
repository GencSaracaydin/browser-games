using System;
using UnityEngine;

namespace LongShadow.Combat
{
    /// <summary>Small transform and layer helpers shared by Combat runtime code and its builders (allocation-free).</summary>
    public static class CombatUtil
    {
        /// <summary>Sets the layer of a whole hierarchy.</summary>
        public static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            var t = root.transform;
            for (var i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }

        /// <summary>First descendant (or self) named exactly <paramref name="name"/> (case-insensitive), depth-first.</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>First descendant whose name matches any of <paramref name="names"/> (list order wins).</summary>
        public static Transform FindAny(Transform root, params string[] names)
        {
            if (names == null) return null;
            for (var i = 0; i < names.Length; i++)
            {
                var t = FindDeep(root, names[i]);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Wraps an angle to [-180, 180).</summary>
        public static float WrapAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees >= 180f) degrees -= 360f;
            if (degrees < -180f) degrees += 360f;
            return degrees;
        }

        /// <summary>Smoothstep of a 0..1 value (clamped).</summary>
        public static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Normalised position of <paramref name="t"/> inside [a, b] (0 before, 1 after).</summary>
        public static float Phase(float t, float a, float b) => b <= a ? (t >= b ? 1f : 0f) : Mathf.Clamp01((t - a) / (b - a));
    }
}
