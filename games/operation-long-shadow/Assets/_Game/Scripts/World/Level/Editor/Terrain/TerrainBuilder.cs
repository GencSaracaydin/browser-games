using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Terrain
{
    /// <summary>
    /// TerrainBuilder (TECH_PLAN §7.15): the approach Terrain of <c>S1_Approach</c> from <see cref="ApproachHeightField"/> —
    /// the basalt shoulder with the spawn pad at +22 (GDD A1), the wadi from (70, 18, 50) down to the grove (A2), the flat
    /// grove and Weighbridge plaza at 0 (A3/A4) and the insertion apron to the south-west (CINEMATIC_DIRECTION §2) — 420 x 340 m,
    /// 513² heights, splat-mapped with the four WP10_CC0 terrain layers (TL_Sand01, TL_RockyTrail02, TL_DryGroundRocks,
    /// TL_AerialSand; flat-colour fallbacks until they land). Layer Default (the insertion splines ray-cast it), lightmapped
    /// at the terrain texel density (GDD §9.4).
    /// </summary>
    public static class TerrainBuilder
    {
        /// <summary>Heightmap resolution.</summary>
        public const int HeightRes = 513;
        /// <summary>Splat resolution.</summary>
        public const int SplatRes = 512;

        /// <summary>Terrain layer asset paths (WP10_CC0 materials_cc0.json terrain_layer blocks).</summary>
        public static readonly string[] LayerPaths =
        {
            LevelPaths.EnvMaterials + "/TL_Sand01.terrainlayer",
            LevelPaths.EnvMaterials + "/TL_RockyTrail02.terrainlayer",
            LevelPaths.EnvMaterials + "/TL_DryGroundRocks.terrainlayer",
            LevelPaths.EnvMaterials + "/TL_AerialSand.terrainlayer",
        };

        private static readonly Color[] s_fallbackColors =
        {
            new Color(0.78f, 0.62f, 0.44f), new Color(0.6f, 0.5f, 0.4f), new Color(0.5f, 0.43f, 0.36f), new Color(0.84f, 0.7f, 0.52f),
        };

        private static readonly float[] s_tileSizes = { 1.5f, 2f, 4f, 15f };

        /// <summary>Builds the Terrain under <paramref name="parent"/>; returns it.</summary>
        public static UnityEngine.Terrain Build(Transform parent, string log = null)
        {
            BuilderUtil.EnsureFolder(LevelPaths.Terrain);
            var ext = ApproachHeightField.Extent;
            var data = new TerrainData { heightmapResolution = HeightRes };
            data.size = new Vector3(ext.Width, ApproachHeightField.RangeY, ext.Depth);
            var heights = new float[HeightRes, HeightRes];
            for (var j = 0; j < HeightRes; j++)
            for (var i = 0; i < HeightRes; i++)
            {
                var x = ext.X0 + ext.Width * i / (HeightRes - 1);
                var z = ext.Z0 + ext.Depth * j / (HeightRes - 1);
                heights[j, i] = Mathf.Clamp01((ApproachHeightField.Height(x, z) - ApproachHeightField.MinY) / ApproachHeightField.RangeY);
            }
            data.SetHeights(0, 0, heights);

            data.terrainLayers = Layers();
            data.alphamapResolution = SplatRes;
            var splat = new float[SplatRes, SplatRes, 4];
            for (var j = 0; j < SplatRes; j++)
            for (var i = 0; i < SplatRes; i++)
            {
                var x = ext.X0 + ext.Width * (i + 0.5f) / SplatRes;
                var z = ext.Z0 + ext.Depth * (j + 0.5f) / SplatRes;
                var w = ApproachHeightField.Splat(x, z);
                splat[j, i, 0] = w.x;
                splat[j, i, 1] = w.y;
                splat[j, i, 2] = w.z;
                splat[j, i, 3] = w.w;
            }
            data.SetAlphamaps(0, 0, splat);
            data.name = "TD_Approach";
            data = BuilderUtil.CreateOrReplaceAsset(data, LevelPaths.Terrain + "/TD_Approach.asset");

            var go = UnityEngine.Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain_Approach";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(ext.X0, ApproachHeightField.MinY, ext.Z0);
            go.layer = LayerIds.Default;
            var terrain = go.GetComponent<UnityEngine.Terrain>();
            terrain.materialTemplate = TerrainMaterial();
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 220f;
            terrain.drawInstanced = true;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            var so = new SerializedObject(terrain);
            var scale = so.FindProperty("m_ScaleInLightmap");
            if (scale != null)
            {
                scale.floatValue = WorldConst.LightmapScaleTerrain;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            GameObjectUtility.SetStaticEditorFlags(go, LevelObjects.ArchitectureFlags);
            var tag = go.AddComponent<SurfaceTag>();
            tag.Surface = SurfaceType.Sand;
            return terrain;
        }

        /// <summary>The four terrain layers (WP10_CC0) or generated flat-colour fallbacks.</summary>
        public static TerrainLayer[] Layers()
        {
            var layers = new TerrainLayer[LayerPaths.Length];
            for (var i = 0; i < layers.Length; i++)
            {
                var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPaths[i]);
                if (l == null)
                {
                    LSLog.MissingAsset(LogCategory.Level, System.IO.Path.GetFileNameWithoutExtension(LayerPaths[i]), LayerPaths[i]);
                    l = Fallback(i);
                }
                layers[i] = l;
            }
            return layers;
        }

        private static TerrainLayer Fallback(int i)
        {
            var folder = LevelPaths.Terrain + "/Fallback";
            BuilderUtil.EnsureFolder(folder);
            var name = System.IO.Path.GetFileNameWithoutExtension(LayerPaths[i]) + "_Fallback";
            var path = folder + "/" + name + ".terrainlayer";
            var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (existing != null) return existing;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = name + "_BC" };
            var px = new Color[16];
            for (var p = 0; p < 16; p++) px[p] = s_fallbackColors[i] * (0.94f + 0.06f * ((p * 7) % 5) / 4f);
            tex.SetPixels(px);
            tex.Apply();
            tex = BuilderUtil.CreateOrReplaceAsset(tex, folder + "/" + name + "_BC.asset");
            var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(s_tileSizes[i], s_tileSizes[i]), name = name };
            return BuilderUtil.CreateOrReplaceAsset(layer, path);
        }

        /// <summary>URP Terrain/Lit material (created once next to the terrain data).</summary>
        public static Material TerrainMaterial()
        {
            var path = LevelPaths.Terrain + "/M_Terrain_Approach.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null) return null;
            m = new Material(shader) { name = "M_Terrain_Approach" };
            m.SetFloat("_EnableHeightBlend", 1f);
            m.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
            return BuilderUtil.CreateOrReplaceAsset(m, path);
        }

        /// <summary>The approach height field as a mesh (S9's far terrain): <paramref name="step"/> metres per quad, lowered by <paramref name="drop"/>.</summary>
        public static Mesh FarMesh(float step, float drop)
        {
            var ext = ApproachHeightField.Extent;
            var nx = Mathf.CeilToInt(ext.Width / step) + 1;
            var nz = Mathf.CeilToInt(ext.Depth / step) + 1;
            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (var j = 0; j < nz; j++)
            for (var i = 0; i < nx; i++)
            {
                var x = Mathf.Min(ext.X0 + i * step, ext.X1);
                var z = Mathf.Min(ext.Z0 + j * step, ext.Z1);
                verts[j * nx + i] = new Vector3(x, ApproachHeightField.Height(x, z) - drop, z);
                uvs[j * nx + i] = new Vector2(x / 15f, z / 15f);
            }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            var t = 0;
            for (var j = 0; j < nz - 1; j++)
            for (var i = 0; i < nx - 1; i++)
            {
                var a = j * nx + i;
                tris[t++] = a;
                tris[t++] = a + nx;
                tris[t++] = a + nx + 1;
                tris[t++] = a;
                tris[t++] = a + nx + 1;
                tris[t++] = a + 1;
            }
            var mesh = new Mesh { name = "Approach_Far", indexFormat = verts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
