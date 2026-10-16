using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Player.Footsteps
{
    /// <summary>
    /// Dominant terrain layer under a point, mapped to a <see cref="SurfaceType"/> by the layer's name (or its albedo
    /// texture name) through <see cref="SurfaceTag.FromName"/>. The alphamap is read lazily in 64×64 texel chunks and
    /// cached as one byte per texel, so footsteps never allocate after a chunk was visited once.
    /// </summary>
    public static class TerrainSurfaceCache
    {
        private const int ChunkSize = 64;

        private sealed class Entry
        {
            public SurfaceType[] LayerSurfaces;
            public int Resolution;
            public readonly Dictionary<int, byte[]> Chunks = new Dictionary<int, byte[]>();
        }

        private static readonly Dictionary<TerrainData, Entry> s_entries = new Dictionary<TerrainData, Entry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_entries.Clear();

        /// <summary>Surface of the terrain at a world position (Default if unknown).</summary>
        public static SurfaceType Sample(Terrain terrain, Vector3 world)
        {
            if (terrain == null) return SurfaceType.Default;
            var data = terrain.terrainData;
            if (data == null) return SurfaceType.Default;
            if (!s_entries.TryGetValue(data, out var e))
            {
                e = Build(data);
                s_entries[data] = e;
            }
            var layers = e.LayerSurfaces.Length;
            if (layers == 0) return SurfaceType.Default;
            if (layers == 1 || e.Resolution <= 0) return e.LayerSurfaces[0];

            var local = world - terrain.GetPosition();
            var size = data.size;
            if (size.x <= 0f || size.z <= 0f) return e.LayerSurfaces[0];
            var x = Mathf.Clamp((int)(local.x / size.x * e.Resolution), 0, e.Resolution - 1);
            var z = Mathf.Clamp((int)(local.z / size.z * e.Resolution), 0, e.Resolution - 1);
            var cx = x / ChunkSize;
            var cz = z / ChunkSize;
            var key = cz * 4096 + cx;
            if (!e.Chunks.TryGetValue(key, out var bytes))
            {
                bytes = BuildChunk(data, e, cx, cz);
                e.Chunks[key] = bytes;
            }
            var w = Mathf.Min(ChunkSize, e.Resolution - cx * ChunkSize);
            var index = bytes[(z - cz * ChunkSize) * w + (x - cx * ChunkSize)];
            return index < layers ? e.LayerSurfaces[index] : SurfaceType.Default;
        }

        /// <summary>Forgets cached terrains (after terrain edits in the editor).</summary>
        public static void Clear() => s_entries.Clear();

        private static Entry Build(TerrainData data)
        {
            var layers = data.terrainLayers;
            var e = new Entry { LayerSurfaces = new SurfaceType[layers != null ? layers.Length : 0], Resolution = data.alphamapResolution };
            for (var i = 0; i < e.LayerSurfaces.Length; i++)
            {
                var l = layers[i];
                var s = l != null ? SurfaceTag.FromName(l.name) : SurfaceType.Default;
                if (s == SurfaceType.Default && l != null && l.diffuseTexture != null) s = SurfaceTag.FromName(l.diffuseTexture.name);
                e.LayerSurfaces[i] = s;
            }
            return e;
        }

        private static byte[] BuildChunk(TerrainData data, Entry e, int cx, int cz)
        {
            var x0 = cx * ChunkSize;
            var z0 = cz * ChunkSize;
            var w = Mathf.Min(ChunkSize, e.Resolution - x0);
            var h = Mathf.Min(ChunkSize, e.Resolution - z0);
            var bytes = new byte[Mathf.Max(1, w * h)];
            if (w <= 0 || h <= 0) return bytes;
            var maps = data.GetAlphamaps(x0, z0, w, h); // [z, x, layer]
            var layers = Mathf.Min(maps.GetLength(2), 255);
            for (var z = 0; z < h; z++)
            {
                for (var x = 0; x < w; x++)
                {
                    var best = 0;
                    var bestWeight = -1f;
                    for (var l = 0; l < layers; l++)
                    {
                        var v = maps[z, x, l];
                        if (v <= bestWeight) continue;
                        bestWeight = v;
                        best = l;
                    }
                    bytes[z * w + x] = (byte)best;
                }
            }
            return bytes;
        }
    }
}
