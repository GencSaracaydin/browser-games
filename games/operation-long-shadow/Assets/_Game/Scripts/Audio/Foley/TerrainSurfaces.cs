using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Foley
{
    /// <summary>
    /// Surface of a point on a Unity terrain for NPC footsteps: the dominant terrain layer of a 64 × 64 grid, mapped by
    /// the layer's name (or its diffuse texture's name) with <see cref="SurfaceTag.FromName"/> ("sand_01" → Sand,
    /// "salt…" → SaltCrust). The grid is sampled once per <see cref="TerrainData"/> (one-off allocation when the section
    /// loads), then every lookup is allocation-free. Statics reset on SubsystemRegistration (R6).
    /// </summary>
    public static class TerrainSurfaces
    {
        /// <summary>Grid resolution per side.</summary>
        public const int Grid = 64;

        private static readonly Dictionary<TerrainData, SurfaceType[]> s_cache = new Dictionary<TerrainData, SurfaceType[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_cache.Clear();

        /// <summary>Surface at a world position on <paramref name="terrain"/> (Default when unknown).</summary>
        public static SurfaceType Resolve(Terrain terrain, Vector3 worldPosition)
        {
            if (terrain == null || terrain.terrainData == null) return SurfaceType.Default;
            var data = terrain.terrainData;
            if (!s_cache.TryGetValue(data, out var grid))
            {
                grid = Build(data);
                s_cache[data] = grid;
            }
            if (grid == null) return SurfaceType.Default;
            var local = worldPosition - terrain.transform.position;
            var size = data.size;
            if (size.x <= 0f || size.z <= 0f) return SurfaceType.Default;
            var gx = Mathf.Clamp((int)(local.x / size.x * Grid), 0, Grid - 1);
            var gz = Mathf.Clamp((int)(local.z / size.z * Grid), 0, Grid - 1);
            return grid[gz * Grid + gx];
        }

        /// <summary>Surface of a terrain layer from its name or diffuse texture name.</summary>
        public static SurfaceType OfLayer(TerrainLayer layer)
        {
            if (layer == null) return SurfaceType.Default;
            var t = SurfaceTag.FromName(layer.name);
            if (t == SurfaceType.Default && layer.diffuseTexture != null) t = SurfaceTag.FromName(layer.diffuseTexture.name);
            return t;
        }

        private static SurfaceType[] Build(TerrainData data)
        {
            var layers = data.terrainLayers;
            if (layers == null || layers.Length == 0) return null;
            var types = new SurfaceType[layers.Length];
            for (var i = 0; i < layers.Length; i++) types[i] = OfLayer(layers[i]);
            var grid = new SurfaceType[Grid * Grid];
            if (layers.Length == 1)
            {
                for (var i = 0; i < grid.Length; i++) grid[i] = types[0];
                return grid;
            }
            var res = data.alphamapResolution;
            if (res <= 0) return null;
            for (var z = 0; z < Grid; z++)
            {
                for (var x = 0; x < Grid; x++)
                {
                    var ax = Mathf.Clamp((int)((x + 0.5f) / Grid * res), 0, res - 1);
                    var az = Mathf.Clamp((int)((z + 0.5f) / Grid * res), 0, res - 1);
                    var w = data.GetAlphamaps(ax, az, 1, 1);
                    var best = 0;
                    for (var l = 1; l < layers.Length; l++)
                        if (w[0, 0, l] > w[0, 0, best]) best = l;
                    grid[z * Grid + x] = types[best];
                }
            }
            return grid;
        }
    }
}
