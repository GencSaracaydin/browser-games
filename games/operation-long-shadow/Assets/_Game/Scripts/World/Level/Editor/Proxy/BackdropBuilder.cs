using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Geometry;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Level.Editor.Proxy
{
    /// <summary>
    /// The backdrop of <c>S9_TownFar</c> (GDD §6.5 "Backdrop", §8.5, §9.3): the western ridge silhouette at 2.5 km from R4
    /// across azimuth 222–294° whose crest on the sun line (258°) is exactly <see cref="SunGeometry.MidARidgeCrest"/> (0.25°
    /// above the R4 eye, so it swallows the disc at MID-A) and never dips within ±3° of it; the distant hills ring at 6 km
    /// (kept below the ridge's angular height in the sunset sector so they never occlude the sun earlier); the desert plane
    /// and the salt pans to the north and east. Meshes are readable, on layer FarProxy, without colliders, named with
    /// "Ridge"/"FarHills" (WP10 Look's SunGeometryChecks ray-tests them) and use Look's backdrop materials
    /// (<c>Art/Materials/Sky/M_Backdrop_Ridge|FarHills.mat</c>) or proxy fallbacks. Terrain003 (WP10_CC0, EditorOnly) shapes
    /// the profiles when present.
    /// </summary>
    public static class BackdropBuilder
    {
        /// <summary>R4 parapet floor point the ridge is laid out from.</summary>
        public static readonly Vector3 R4 = new Vector3(291f, 0f, 328f);
        /// <summary>Centre of the hills ring (the town).</summary>
        public static readonly Vector3 TownCenter = new Vector3(260f, 0f, 280f);
        /// <summary>Hills ring radius.</summary>
        public const float HillsRadius = 6000f;
        /// <summary>Look's backdrop materials.</summary>
        public const string RidgeMaterial = "Assets/_Game/Art/Materials/Sky/M_Backdrop_Ridge.mat";
        /// <summary>Look's far-hills material.</summary>
        public const string HillsMaterial = "Assets/_Game/Art/Materials/Sky/M_Backdrop_FarHills.mat";
        /// <summary>Terrain003 heightmap (raw uint16, 2048², WP10_CC0).</summary>
        public const string Terrain003 = "Assets/_Game/Art/Textures/Env/Terrain003/Terrain003_height_2048_r16.bytes";

        /// <summary>Builds the backdrop under <paramref name="parent"/>.</summary>
        public static void Build(Transform parent, string folder)
        {
            BuilderUtil.EnsureFolder(folder);
            var profile = Profile();
            var ridge = MeshAssets.Save(RidgeMesh(profile), folder + "/Backdrop_Ridge.asset", false);
            Object(parent, "Backdrop_Ridge", ridge, Material(RidgeMaterial, KitSlots.Rock));
            var hills = MeshAssets.Save(HillsMesh(profile), folder + "/Backdrop_FarHills.asset", false);
            Object(parent, "Backdrop_FarHills", hills, Material(HillsMaterial, KitSlots.Rock));
            var ground = MeshAssets.Save(Plane("Backdrop_Desert", new Rect(-2800f, -2800f, 6000f, 6000f), -2.0f, 40f), folder + "/Backdrop_Desert.asset", false);
            Object(parent, "Backdrop_Desert", ground, LevelMaterials.GetProxy(KitSlots.Sand));
            var saltN = MeshAssets.Save(Plane("Backdrop_SaltNorth", new Rect(-600f, 430f, 3600f, 2800f), WorldConst.SaltPanY, 30f), folder + "/Backdrop_SaltNorth.asset", false);
            Object(parent, "Backdrop_SaltNorth", saltN, LevelMaterials.GetProxy(KitSlots.SaltCrust));
            var saltE = MeshAssets.Save(Plane("Backdrop_SaltEast", new Rect(394f, -800f, 2600f, 1230f), WorldConst.SaltPanY, 30f), folder + "/Backdrop_SaltEast.asset", false);
            Object(parent, "Backdrop_SaltEast", saltE, LevelMaterials.GetProxy(KitSlots.SaltCrust));
        }

        /// <summary>Crest height of the ridge at an azimuth (metres; exact MID-A crest within ±3° of the sun line).</summary>
        public static float RidgeCrest(float azimuth, float[] profile)
        {
            var crest = SunGeometry.MidARidgeCrest();
            var d = Mathf.Abs(Mathf.DeltaAngle(azimuth, WorldConst.SunAzimuth));
            var detail = Sample(profile, azimuth / 360f * 3f) * 14f + ValueNoise.Fbm(azimuth * 0.11f, 3.7f, 991, 3) * 6f;
            var blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 9f, d));
            // Within ±3°: exactly the crest; further out: the profile, never lower than 70 % of the crest near the sun line.
            var h = crest + detail * blend;
            if (d < 12f) h = Mathf.Max(h, crest * Mathf.Lerp(1f, 0.7f, d / 12f));
            return h;
        }

        private static Mesh RidgeMesh(float[] profile)
        {
            const float az0 = 222f, az1 = 294f, step = 0.5f;
            var cols = Mathf.RoundToInt((az1 - az0) / step) + 1;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var d = WorldConst.RidgeSilhouetteDistance;
            for (var i = 0; i < cols; i++)
            {
                var az = az0 + i * step;
                var crest = RidgeCrest(az, profile);
                // Rows: front base (250 m nearer, below the plain), shoulder, crest, back fall.
                verts.Add(SunGeometry.AlongBearing(R4, az, d - 250f) + Vector3.up * -18f);
                verts.Add(SunGeometry.AlongBearing(R4, az, d - 60f) + Vector3.up * (crest * 0.55f));
                verts.Add(SunGeometry.AlongBearing(R4, az, d) + Vector3.up * crest);
                verts.Add(SunGeometry.AlongBearing(R4, az, d + 220f) + Vector3.up * (crest * 0.8f));
                for (var r = 0; r < 4; r++) uvs.Add(new Vector2(az / 3f, r / 3f));
            }
            Strip(tris, cols, 4);
            return Finish("Backdrop_Ridge", verts, uvs, tris);
        }

        private static Mesh HillsMesh(float[] profile)
        {
            const float step = 1f;
            var cols = Mathf.RoundToInt(360f / step) + 1;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var i = 0; i < cols; i++)
            {
                var az = i * step;
                var h = 50f + Sample(profile, az / 360f * 7f + 0.31f) * 90f + ValueNoise.Fbm(az * 0.07f, 9.1f, 773, 3) * 25f;
                // In the sunset sector the hills stay below the ridge's angular height (0.25° above the R4 eye): ≤ 20 m at 6 km.
                var sun = Mathf.Abs(Mathf.DeltaAngle(az, WorldConst.SunAzimuth));
                if (sun < 40f) h = Mathf.Lerp(Mathf.Min(h, 20f), h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 40f, sun)));
                // North and east are salt pan: low, flat hills.
                if (az < 120f) h *= 0.45f;
                verts.Add(SunGeometry.AlongBearing(TownCenter, az, HillsRadius - 900f) + Vector3.up * -12f);
                verts.Add(SunGeometry.AlongBearing(TownCenter, az, HillsRadius - 200f) + Vector3.up * (h * 0.6f));
                verts.Add(SunGeometry.AlongBearing(TownCenter, az, HillsRadius) + Vector3.up * h);
                verts.Add(SunGeometry.AlongBearing(TownCenter, az, HillsRadius + 600f) + Vector3.up * (h * 0.7f));
                for (var r = 0; r < 4; r++) uvs.Add(new Vector2(az / 2f, r / 3f));
            }
            Strip(tris, cols, 4);
            return Finish("Backdrop_FarHills", verts, uvs, tris);
        }

        /// <summary>Quads between consecutive rows of consecutive columns, front faces towards the centre.</summary>
        private static void Strip(List<int> tris, int cols, int rows)
        {
            for (var i = 0; i < cols - 1; i++)
            for (var r = 0; r < rows - 1; r++)
            {
                var a = i * rows + r;
                var b = i * rows + r + 1;
                var c = (i + 1) * rows + r + 1;
                var d = (i + 1) * rows + r;
                tris.Add(a);
                tris.Add(b);
                tris.Add(c);
                tris.Add(a);
                tris.Add(c);
                tris.Add(d);
            }
        }

        private static Mesh Finish(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var m = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh Plane(string name, Rect r, float y, float tile)
        {
            var m = new Mesh { name = name };
            m.vertices = new[] { new Vector3(r.xMin, y, r.yMin), new Vector3(r.xMin, y, r.yMax), new Vector3(r.xMax, y, r.yMax), new Vector3(r.xMax, y, r.yMin) };
            m.uv = new[] { new Vector2(r.xMin / tile, r.yMin / tile), new Vector2(r.xMin / tile, r.yMax / tile), new Vector2(r.xMax / tile, r.yMax / tile), new Vector2(r.xMax / tile, r.yMin / tile) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static void Object(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = LayerIds.FarProxy;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveGI = ReceiveGI.LightProbes;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material Material(string path, string fallbackSlot)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            LSLog.MissingAsset(LogCategory.Level, System.IO.Path.GetFileNameWithoutExtension(path), path);
            return LevelMaterials.GetProxy(fallbackSlot);
        }

        /// <summary>A 1D profile from Terrain003 (the middle row, normalised to −1..1) or null.</summary>
        public static float[] Profile()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Terrain003);
            if (ta == null) return null;
            var bytes = ta.bytes;
            const int n = 2048;
            if (bytes.Length < n * n * 2) return null;
            var row = new float[n];
            var mid = n / 2;
            float lo = float.MaxValue, hi = float.MinValue;
            for (var i = 0; i < n; i++)
            {
                var o = (mid * n + i) * 2;
                var v = (bytes[o] | (bytes[o + 1] << 8)) / 65535f;
                row[i] = v;
                lo = Mathf.Min(lo, v);
                hi = Mathf.Max(hi, v);
            }
            var span = Mathf.Max(1e-4f, hi - lo);
            for (var i = 0; i < n; i++) row[i] = (row[i] - lo) / span * 2f - 1f;
            return row;
        }

        private static float Sample(float[] profile, float t)
        {
            if (profile == null) return ValueNoise.Fbm(t * 9f, 1.3f, 4242, 4);
            var f = (t - Mathf.Floor(t)) * (profile.Length - 1);
            var i = Mathf.FloorToInt(f);
            return Mathf.Lerp(profile[i], profile[Mathf.Min(i + 1, profile.Length - 1)], f - i);
        }
    }
}
