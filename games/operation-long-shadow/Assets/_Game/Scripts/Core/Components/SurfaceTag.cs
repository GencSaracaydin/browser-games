using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Overrides the <see cref="SurfaceType"/> of a collider (TECH_PLAN §6.4). Without a tag, <see cref="Resolve"/> maps the
    /// physic material name (e.g. "PM_Sand", "Concrete") to a type, else <see cref="SurfaceType.Default"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurfaceTag : MonoBehaviour
    {
        [SerializeField] private SurfaceType _surface = SurfaceType.Default;

        private static readonly Dictionary<PhysicsMaterial, SurfaceType> s_materialCache = new Dictionary<PhysicsMaterial, SurfaceType>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_materialCache.Clear();

        /// <summary>The tagged surface.</summary>
        public SurfaceType Surface { get => _surface; set => _surface = value; }

        /// <summary>
        /// Surface of a collider: SurfaceTag on the collider, its attached rigidbody or a parent; else the physic material
        /// name; else <see cref="IHitbox.Surface"/>; else Default. Allocation-free after the first lookup per material.
        /// </summary>
        public static SurfaceType Resolve(Collider c)
        {
            if (c == null) return SurfaceType.Default;
            if (c.TryGetComponent(out SurfaceTag tag)) return tag._surface;
            if (c.TryGetComponent(out IHitbox hb)) return hb.Surface;
            var parentTag = c.GetComponentInParent<SurfaceTag>();
            if (parentTag != null) return parentTag._surface;
            var pm = c.sharedMaterial;
            if (pm != null)
            {
                if (!s_materialCache.TryGetValue(pm, out var t))
                {
                    t = FromName(pm.name);
                    s_materialCache[pm] = t;
                }
                return t;
            }
            return SurfaceType.Default;
        }

        /// <summary>Maps a material/texture name to a surface (case-insensitive substring match).</summary>
        public static SurfaceType FromName(string n)
        {
            if (string.IsNullOrEmpty(n)) return SurfaceType.Default;
            n = n.ToLowerInvariant();
            if (n.Contains("salt")) return SurfaceType.SaltCrust;
            if (n.Contains("sand") || n.Contains("dirt") || n.Contains("dust")) return SurfaceType.Sand;
            if (n.Contains("gravel") || n.Contains("rubble")) return SurfaceType.Gravel;
            if (n.Contains("concrete") || n.Contains("cement")) return SurfaceType.Concrete;
            if (n.Contains("plaster") || n.Contains("stucco")) return SurfaceType.Plaster;
            if (n.Contains("tile")) return SurfaceType.Tile;
            if (n.Contains("wood") || n.Contains("plank") || n.Contains("crate")) return SurfaceType.Wood;
            if (n.Contains("metal") || n.Contains("steel") || n.Contains("iron") || n.Contains("corrugated")) return SurfaceType.Metal;
            if (n.Contains("glass")) return SurfaceType.Glass;
            if (n.Contains("fabric") || n.Contains("cloth") || n.Contains("awning") || n.Contains("canvas")) return SurfaceType.Fabric;
            if (n.Contains("fruit")) return SurfaceType.Fruit;
            if (n.Contains("flesh") || n.Contains("body")) return SurfaceType.Flesh;
            if (n.Contains("stone") || n.Contains("rock") || n.Contains("brick")) return SurfaceType.Stone;
            return SurfaceType.Default;
        }
    }
}
