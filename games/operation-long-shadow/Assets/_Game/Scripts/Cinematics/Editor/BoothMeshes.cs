using System.Collections.Generic;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Procedural meshes of the booth shell (PROC_BOOTH_SHELL): wall panels with rectangular holes (mirror, door), floor,
    /// ceiling, the steel table and door. UV0 is world-scale (1 unit = 1 m; the materials tile by 1/tile size), UV1 is
    /// generated for the booth lightmap. Saved as mesh assets under Prefabs/Booth/Meshes so re-running the builder keeps
    /// their GUIDs.
    /// </summary>
    public static class BoothMeshes
    {
        /// <summary>
        /// A planar panel spanning <paramref name="u0"/>..<paramref name="u1"/> × <paramref name="v0"/>..<paramref name="v1"/>
        /// in a plane (origin + u·axisU + v·axisV) with optional rectangular holes (in u, v), facing <paramref name="normal"/>.
        /// The panel is split into a grid of quads around the holes.
        /// </summary>
        public static Mesh Panel(string name, Vector3 origin, Vector3 axisU, Vector3 axisV, Vector3 normal, float u0, float u1,
                                 float v0, float v1, IList<Rect> holes = null)
        {
            var us = new List<float> { u0, u1 };
            var vs = new List<float> { v0, v1 };
            if (holes != null)
            {
                foreach (var h in holes)
                {
                    us.Add(Mathf.Clamp(h.xMin, u0, u1));
                    us.Add(Mathf.Clamp(h.xMax, u0, u1));
                    vs.Add(Mathf.Clamp(h.yMin, v0, v1));
                    vs.Add(Mathf.Clamp(h.yMax, v0, v1));
                }
            }
            us.Sort();
            vs.Sort();
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            var flip = Vector3.Dot(Vector3.Cross(axisU, axisV), normal) < 0f;
            for (var i = 0; i < us.Count - 1; i++)
            for (var j = 0; j < vs.Count - 1; j++)
            {
                var a = us[i];
                var b = us[i + 1];
                var c = vs[j];
                var d = vs[j + 1];
                if (b - a < 1e-4f || d - c < 1e-4f) continue;
                var mid = new Vector2((a + b) * 0.5f, (c + d) * 0.5f);
                var inHole = false;
                if (holes != null)
                    foreach (var h in holes)
                        if (h.Contains(mid)) inHole = true;
                if (inHole) continue;
                var k = verts.Count;
                verts.Add(origin + axisU * a + axisV * c);
                verts.Add(origin + axisU * b + axisV * c);
                verts.Add(origin + axisU * b + axisV * d);
                verts.Add(origin + axisU * a + axisV * d);
                uvs.Add(new Vector2(a, c));
                uvs.Add(new Vector2(b, c));
                uvs.Add(new Vector2(b, d));
                uvs.Add(new Vector2(a, d));
                for (var n = 0; n < 4; n++) normals.Add(normal);
                // Unity's front faces wind so that cross(v1 - v0, v2 - v0) points along the normal.
                if (flip)
                {
                    tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                    tris.Add(k); tris.Add(k + 3); tris.Add(k + 2);
                }
                else
                {
                    tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
                    tris.Add(k); tris.Add(k + 2); tris.Add(k + 3);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>An axis-aligned box (outward normals, world-scale UVs on each face).</summary>
        public static Mesh Box(string name, Vector3 centre, Vector3 size)
        {
            var h = size * 0.5f;
            var faces = new[]
            {
                (Vector3.up, Vector3.right, Vector3.forward), (Vector3.down, Vector3.right, Vector3.back),
                (Vector3.forward, Vector3.left, Vector3.up), (Vector3.back, Vector3.right, Vector3.up),
                (Vector3.right, Vector3.forward, Vector3.up), (Vector3.left, Vector3.back, Vector3.up),
            };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            foreach (var (n, u, v) in faces)
            {
                var hu = Mathf.Abs(Vector3.Dot(h, u));
                var hv = Mathf.Abs(Vector3.Dot(h, v));
                var hn = Mathf.Abs(Vector3.Dot(h, n));
                var c = centre + n * hn;
                var k = verts.Count;
                verts.Add(c - u * hu - v * hv);
                verts.Add(c + u * hu - v * hv);
                verts.Add(c + u * hu + v * hv);
                verts.Add(c - u * hu + v * hv);
                uvs.Add(new Vector2(-hu, -hv));
                uvs.Add(new Vector2(hu, -hv));
                uvs.Add(new Vector2(hu, hv));
                uvs.Add(new Vector2(-hu, hv));
                for (var i = 0; i < 4; i++) normals.Add(n);
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                tris.Add(k); tris.Add(k + 3); tris.Add(k + 2);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Combines boxes into one mesh (steel table: top, legs, stretchers).</summary>
        public static Mesh Boxes(string name, IList<(Vector3 centre, Vector3 size)> boxes)
        {
            var combine = new CombineInstance[boxes.Count];
            for (var i = 0; i < boxes.Count; i++)
                combine[i] = new CombineInstance { mesh = Box(name + "_" + i, boxes[i].centre, boxes[i].size), transform = Matrix4x4.identity };
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            foreach (var c in combine) Object.DestroyImmediate(c.mesh);
            return mesh;
        }

        /// <summary>Generates lightmap UVs and saves (or replaces in place) the mesh asset.</summary>
        public static Mesh Save(Mesh mesh, bool lightmapUv = true)
        {
            if (lightmapUv) Unwrapping.GenerateSecondaryUVSet(mesh);
            var path = CinematicPaths.BoothMeshes + "/" + mesh.name + ".asset";
            return BuilderUtil.CreateOrReplaceAsset(mesh, path);
        }
    }
}
