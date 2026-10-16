using LongShadow.Core;
using UnityEditor;
using UnityEngine.Rendering;

namespace LongShadow.World.Level.Editor.Common
{
    /// <summary>Options for <see cref="LevelObjects.Build"/>.</summary>
    public struct LevelObjectOptions
    {
        /// <summary>Folder for the mesh assets.</summary>
        public string Folder;
        /// <summary>Lightmap scale of LOD0 (0 = light probes only).</summary>
        public float LightmapScale;
        /// <summary>Write a LOD1 (details removed) and a LODGroup.</summary>
        public bool Lods;
        /// <summary>Use Baked Lit proxy materials (S9).</summary>
        public bool Proxy;
        /// <summary>Layer of the renderers.</summary>
        public int Layer;
        /// <summary>Shadow casting.</summary>
        public ShadowCastingMode Shadows;
        /// <summary>Create colliders from the builder.</summary>
        public bool Colliders;
        /// <summary>Static flags (architecture by default).</summary>
        public StaticEditorFlags Flags;

        /// <summary>Defaults for section architecture.</summary>
        public static LevelObjectOptions Architecture(string folder, float lightmapScale) => new LevelObjectOptions
        {
            Folder = folder, LightmapScale = lightmapScale, Lods = true, Proxy = false, Layer = LayerIds.Default,
            Shadows = ShadowCastingMode.On, Colliders = true, Flags = LevelObjects.ArchitectureFlags,
        };

        /// <summary>Defaults for far proxies (S9).</summary>
        public static LevelObjectOptions FarProxy(string folder) => new LevelObjectOptions
        {
            Folder = folder, LightmapScale = 0.25f, Lods = false, Proxy = true, Layer = LayerIds.FarProxy,
            Shadows = ShadowCastingMode.On, Colliders = false, Flags = LevelObjects.ArchitectureFlags,
        };
    }
}
