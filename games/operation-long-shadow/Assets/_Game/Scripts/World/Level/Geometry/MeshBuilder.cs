using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Multi-material mesh builder for the procedural kit, buildings, terrain far meshes and props (TECH_PLAN §7.15).
    /// <list type="bullet">
    /// <item>Geometry is written into <b>material slots</b> (string keys such as "Plaster", "Sandstone", "Wood") that the
    /// editor maps to the CC0/fal materials; one submesh per slot, so a whole building is one renderer.</item>
    /// <item>UV0 is in metres (1 unit = 1 m) by box projection of the local position on the face's dominant axis
    /// (TECH_PLAN §7.15: "world-scale UV0, tiling = 1 / tile_m"); materials carry the tiling. Lightmap UV1 is generated in
    /// the editor (Unwrapping).</item>
    /// <item>Each triangle carries a LOD mask: <see cref="LodAll"/> geometry is kept in LOD1, <see cref="LodDetail"/>
    /// (trims, shutters, grilles, balusters) only in LOD0, so LOD1 is about half the triangles without decimation.</item>
    /// <item>Faces are written clockwise as seen from their front (Unity's winding); <see cref="AddQuad"/> takes the four
    /// corners clockwise from the front.</item>
    /// </list>
    /// Pure runtime API (no UnityEditor), deterministic, testable in EditMode.
    /// </summary>
    public sealed class MeshBuilder
    {
        /// <summary>Geometry kept in every LOD.</summary>
        public const int LodAll = 0x3;
        /// <summary>Geometry kept only in LOD0 (small details).</summary>
        public const int LodDetail = 0x1;
        /// <summary>Geometry only in LOD1 (simplified replacements of details).</summary>
        public const int LodOnly1 = 0x2;

        private readonly List<Vector3> _positions = new List<Vector3>(1024);
        private readonly List<Vector3> _normals = new List<Vector3>(1024);
        private readonly List<Vector2> _uvs = new List<Vector2>(1024);
        private readonly List<string> _slots = new List<string>();
        private readonly Dictionary<string, int> _slotIndex = new Dictionary<string, int>();
        private readonly List<List<int>> _triangles = new List<List<int>>();
        private readonly List<List<int>> _triangleLods = new List<List<int>>();
        private readonly List<ColliderBox> _colliders = new List<ColliderBox>();
        private readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();

        private Matrix4x4 _matrix = Matrix4x4.identity;
        private Matrix4x4 _normalMatrix = Matrix4x4.identity;
        private int _slot = -1;

        /// <summary>Current LOD mask for new triangles.</summary>
        public int LodMask { get; set; } = LodAll;

        /// <summary>UV offset added to box-projected UVs (decorrelates texture repeats between buildings).</summary>
        public Vector2 UvOffset { get; set; }

        /// <summary>Scale applied to box-projected UVs (1 = metres).</summary>
        public float UvScale { get; set; } = 1f;

        /// <summary>Material slot keys in submesh order.</summary>
        public IReadOnlyList<string> Slots => _slots;

        /// <summary>Generated colliders (local space).</summary>
        public IReadOnlyList<ColliderBox> Colliders => _colliders;

        /// <summary>Vertex count so far.</summary>
        public int VertexCount => _positions.Count;

        /// <summary>Triangle count of a LOD (0 or 1).</summary>
        public int TriangleCount(int lod)
        {
            var bit = lod == 0 ? 1 : 2;
            var n = 0;
            for (var s = 0; s < _triangleLods.Count; s++)
            {
                var lods = _triangleLods[s];
                for (var i = 0; i < lods.Count; i++)
                    if ((lods[i] & bit) != 0) n++;
            }
            return n;
        }

        /// <summary>True when nothing was written.</summary>
        public bool IsEmpty => _positions.Count == 0;

        /// <summary>Current transform (local → builder space).</summary>
        public Matrix4x4 Matrix => _matrix;

        /// <summary>Selects (and creates) the material slot for the following geometry.</summary>
        public MeshBuilder Slot(string slot)
        {
            if (string.IsNullOrEmpty(slot)) slot = "Default";
            if (!_slotIndex.TryGetValue(slot, out var i))
            {
                i = _slots.Count;
                _slots.Add(slot);
                _slotIndex.Add(slot, i);
                _triangles.Add(new List<int>(256));
                _triangleLods.Add(new List<int>(128));
            }
            _slot = i;
            return this;
        }

        /// <summary>The current slot key (null before the first <see cref="Slot"/>).</summary>
        public string CurrentSlot => _slot >= 0 ? _slots[_slot] : null;

        /// <summary>Pushes a transform (multiplied onto the current one).</summary>
        public void Push(Matrix4x4 m)
        {
            _stack.Push(_matrix);
            SetMatrix(_matrix * m);
        }

        /// <summary>Pushes a translation/rotation/scale.</summary>
        public void Push(Vector3 position, Quaternion rotation, Vector3? scale = null) =>
            Push(Matrix4x4.TRS(position, rotation, scale ?? Vector3.one));

        /// <summary>Pops the last pushed transform.</summary>
        public void Pop()
        {
            if (_stack.Count > 0) SetMatrix(_stack.Pop());
        }

        private void SetMatrix(Matrix4x4 m)
        {
            _matrix = m;
            _normalMatrix = m.inverse.transpose;
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>
        /// Adds a quad given clockwise from its front (a bottom-left, b top-left, c top-right, d bottom-right for a wall
        /// seen from outside). UVs are box-projected in metres.
        /// </summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var wa = _matrix.MultiplyPoint3x4(a);
            var wb = _matrix.MultiplyPoint3x4(b);
            var wc = _matrix.MultiplyPoint3x4(c);
            var wd = _matrix.MultiplyPoint3x4(d);
            var n = Vector3.Cross(wb - wa, wc - wa);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(wc - wa, wd - wa);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            AddQuadWorld(wa, wb, wc, wd, n, Project(wa, n), Project(wb, n), Project(wc, n), Project(wd, n));
        }

        /// <summary>Adds a quad with explicit UVs (metres or any unit the material expects).</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd)
        {
            var wa = _matrix.MultiplyPoint3x4(a);
            var wb = _matrix.MultiplyPoint3x4(b);
            var wc = _matrix.MultiplyPoint3x4(c);
            var wd = _matrix.MultiplyPoint3x4(d);
            var n = Vector3.Cross(wb - wa, wc - wa);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(wc - wa, wd - wa);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            AddQuadWorld(wa, wb, wc, wd, n, uva, uvb, uvc, uvd);
        }

        /// <summary>Adds a quad visible from both sides (cloth, awnings, laundry, cards).</summary>
        public void AddQuadDoubleSided(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddQuad(a, b, c, d);
            AddQuad(d, c, b, a);
        }

        /// <summary>Adds a single triangle given clockwise from its front.</summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            EnsureSlot();
            var wa = _matrix.MultiplyPoint3x4(a);
            var wb = _matrix.MultiplyPoint3x4(b);
            var wc = _matrix.MultiplyPoint3x4(c);
            var n = Vector3.Cross(wb - wa, wc - wa);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            var i = _positions.Count;
            AddVertex(wa, n, Project(wa, n));
            AddVertex(wb, n, Project(wb, n));
            AddVertex(wc, n, Project(wc, n));
            AddTri(i, i + 1, i + 2);
        }

        /// <summary>Adds a convex planar polygon (fan) given clockwise from its front.</summary>
        public void AddConvexPolygon(IReadOnlyList<Vector3> loop)
        {
            if (loop == null || loop.Count < 3) return;
            EnsureSlot();
            var w = new Vector3[loop.Count];
            for (var i = 0; i < loop.Count; i++) w[i] = _matrix.MultiplyPoint3x4(loop[i]);
            var n = Vector3.zero;
            for (var i = 1; i < w.Length - 1; i++) n += Vector3.Cross(w[i] - w[0], w[i + 1] - w[0]);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            var start = _positions.Count;
            for (var i = 0; i < w.Length; i++) AddVertex(w[i], n, Project(w[i], n));
            for (var i = 1; i < w.Length - 1; i++) AddTri(start, start + i, start + i + 1);
        }

        /// <summary>
        /// Adds an axis-aligned box (local space, centre and size) with outward faces. <paramref name="faces"/> selects
        /// faces (bit 0 +X, 1 −X, 2 +Y, 3 −Y, 4 +Z, 5 −Z).
        /// </summary>
        public void AddBox(Vector3 center, Vector3 size, int faces = 0x3F)
        {
            var h = size * 0.5f;
            var x0 = center.x - h.x;
            var x1 = center.x + h.x;
            var y0 = center.y - h.y;
            var y1 = center.y + h.y;
            var z0 = center.z - h.z;
            var z1 = center.z + h.z;
            if ((faces & 1) != 0) AddQuad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1));
            if ((faces & 2) != 0) AddQuad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0));
            if ((faces & 4) != 0) AddQuad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0));
            if ((faces & 8) != 0) AddQuad(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1));
            if ((faces & 16) != 0) AddQuad(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1));
            if ((faces & 32) != 0) AddQuad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0));
        }

        /// <summary>Box faces bit for <see cref="AddBox"/>: everything but the bottom (things standing on a floor).</summary>
        public const int FacesNoBottom = 0x3F & ~8;

        /// <summary>
        /// Adds a box whose four vertical edges are chamfered by <paramref name="chamfer"/> (soft plaster corners, stone
        /// copings, sills). Top and bottom are octagonal caps.
        /// </summary>
        public void AddChamferBox(Vector3 center, Vector3 size, float chamfer, bool top = true, bool bottom = false)
        {
            chamfer = Mathf.Clamp(chamfer, 0f, Mathf.Min(size.x, size.z) * 0.45f);
            if (chamfer <= 1e-4f)
            {
                AddBox(center, size, (top ? 4 : 0) | (bottom ? 8 : 0) | 1 | 2 | 16 | 32);
                return;
            }
            var h = size * 0.5f;
            var y0 = center.y - h.y;
            var y1 = center.y + h.y;
            var c = chamfer;
            // Octagon outline (clockwise seen from above is counter-clockwise in XZ with +Z north... keep consistent below).
            var pts = new[]
            {
                new Vector2(center.x - h.x + c, center.z - h.z), new Vector2(center.x + h.x - c, center.z - h.z),
                new Vector2(center.x + h.x, center.z - h.z + c), new Vector2(center.x + h.x, center.z + h.z - c),
                new Vector2(center.x + h.x - c, center.z + h.z), new Vector2(center.x - h.x + c, center.z + h.z),
                new Vector2(center.x - h.x, center.z + h.z - c), new Vector2(center.x - h.x, center.z - h.z + c),
            };
            AddPrism(pts, y0, y1, top, bottom);
        }

        /// <summary>
        /// Extrudes a closed polygon (XZ, counter-clockwise when seen from above with x right and z up) between two
        /// heights, with outward side faces and optional caps.
        /// </summary>
        public void AddPrism(IReadOnlyList<Vector2> polygonCcw, float y0, float y1, bool top = true, bool bottom = false)
        {
            var n = polygonCcw.Count;
            if (n < 3) return;
            for (var i = 0; i < n; i++)
            {
                var a = polygonCcw[i];
                var b = polygonCcw[(i + 1) % n];
                // Outward side for a CCW (seen from +Y) loop: walk a→b with the outside on the right.
                AddQuad(new Vector3(a.x, y0, a.y), new Vector3(a.x, y1, a.y), new Vector3(b.x, y1, b.y), new Vector3(b.x, y0, b.y));
            }
            if (top)
            {
                var loop = new Vector3[n];
                for (var i = 0; i < n; i++) loop[i] = new Vector3(polygonCcw[n - 1 - i].x, y1, polygonCcw[n - 1 - i].y);
                AddConvexPolygon(loop);
            }
            if (bottom)
            {
                var loop = new Vector3[n];
                for (var i = 0; i < n; i++) loop[i] = new Vector3(polygonCcw[i].x, y0, polygonCcw[i].y);
                AddConvexPolygon(loop);
            }
        }

        /// <summary>Adds a vertical cylinder (smooth sides) from <paramref name="baseCenter"/> up by <paramref name="height"/>.</summary>
        public void AddCylinder(Vector3 baseCenter, float radius, float height, int segments, bool top = true, bool bottom = false)
        {
            AddFrustum(baseCenter, radius, radius, height, segments, top, bottom);
        }

        /// <summary>Adds a vertical truncated cone (smooth sides).</summary>
        public void AddFrustum(Vector3 baseCenter, float radiusBottom, float radiusTop, float height, int segments, bool top = true, bool bottom = false)
        {
            EnsureSlot();
            segments = Mathf.Max(3, segments);
            var start = _positions.Count;
            var slope = (radiusBottom - radiusTop) / Mathf.Max(1e-4f, height);
            var circumference = 2f * Mathf.PI * Mathf.Max(radiusBottom, radiusTop);
            for (var i = 0; i <= segments; i++)
            {
                var a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var nLocal = new Vector3(dir.x, slope, dir.z).normalized;
                var pb = baseCenter + dir * radiusBottom;
                var pt = baseCenter + dir * radiusTop + Vector3.up * height;
                var u = UvOffset.x + circumference * i / segments;
                AddVertex(_matrix.MultiplyPoint3x4(pb), TransformNormal(nLocal), new Vector2(u, UvOffset.y + pb.y) * UvScale);
                AddVertex(_matrix.MultiplyPoint3x4(pt), TransformNormal(nLocal), new Vector2(u, UvOffset.y + pt.y) * UvScale);
            }
            for (var i = 0; i < segments; i++)
            {
                var b0 = start + i * 2;
                var t0 = b0 + 1;
                var b1 = b0 + 2;
                var t1 = b0 + 3;
                // Outward faces, clockwise seen from outside (the angle increases counter-clockwise seen from above).
                AddTri(b0, t0, t1);
                AddTri(b0, t1, b1);
            }
            if (top && radiusTop > 1e-4f) AddDisc(baseCenter + Vector3.up * height, radiusTop, segments, true);
            if (bottom && radiusBottom > 1e-4f) AddDisc(baseCenter, radiusBottom, segments, false);
        }

        /// <summary>Adds a horizontal disc facing up (or down).</summary>
        public void AddDisc(Vector3 center, float radius, int segments, bool up)
        {
            var loop = new Vector3[segments];
            for (var i = 0; i < segments; i++)
            {
                var a = (up ? -1f : 1f) * i / (float)segments * Mathf.PI * 2f;
                loop[i] = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            }
            AddConvexPolygon(loop);
        }

        /// <summary>
        /// Adds a tube between two points (any direction) with a round cross-section: pipes, rails, rods, cables.
        /// </summary>
        public void AddTube(Vector3 from, Vector3 to, float radius, int sides = 6, bool caps = false)
        {
            EnsureSlot();
            var axis = to - from;
            var len = axis.magnitude;
            if (len < 1e-5f) return;
            var dir = axis / len;
            var side = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            var u = Vector3.Cross(dir, side).normalized;
            var v = Vector3.Cross(u, dir);
            sides = Mathf.Max(3, sides);
            var start = _positions.Count;
            for (var i = 0; i <= sides; i++)
            {
                var a = i / (float)sides * Mathf.PI * 2f;
                var nLocal = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                var uv = 2f * Mathf.PI * radius * i / sides;
                AddVertex(_matrix.MultiplyPoint3x4(from + nLocal * radius), TransformNormal(nLocal), new Vector2(uv, 0f) * UvScale + UvOffset);
                AddVertex(_matrix.MultiplyPoint3x4(to + nLocal * radius), TransformNormal(nLocal), new Vector2(uv, len) * UvScale + UvOffset);
            }
            for (var i = 0; i < sides; i++)
            {
                var a0 = start + i * 2;
                var b0 = a0 + 1;
                var a1 = a0 + 2;
                var b1 = a0 + 3;
                AddTri(a0, b0, b1);
                AddTri(a0, b1, a1);
            }
            if (caps)
            {
                var c0 = new Vector3[sides];
                var c1 = new Vector3[sides];
                for (var i = 0; i < sides; i++)
                {
                    var a = i / (float)sides * Mathf.PI * 2f;
                    var o = (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
                    c0[i] = from + o;
                    c1[sides - 1 - i] = to + o;
                }
                AddConvexPolygon(c0);
                AddConvexPolygon(c1);
            }
        }

        /// <summary>Adds a thin box beam between two points (square timber, steel angle, rungs).</summary>
        public void AddBeam(Vector3 from, Vector3 to, float width, float height)
        {
            var axis = to - from;
            var len = axis.magnitude;
            if (len < 1e-5f) return;
            var rot = Quaternion.LookRotation(axis / len, Mathf.Abs(axis.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
            Push((from + to) * 0.5f, rot);
            AddBox(Vector3.zero, new Vector3(width, height, len));
            Pop();
        }

        /// <summary>
        /// Adds a quad grid spanned by two axes from <paramref name="origin"/> with a per-vertex offset (sagging cloth,
        /// awnings, laundry). The front face is cross(axisV, axisU); <paramref name="doubleSided"/> adds the back.
        /// </summary>
        public void AddSheet(Vector3 origin, Vector3 axisU, Vector3 axisV, int segU, int segV, System.Func<float, float, Vector3> offset, bool doubleSided)
        {
            segU = Mathf.Max(1, segU);
            segV = Mathf.Max(1, segV);
            var grid = new Vector3[(segU + 1) * (segV + 1)];
            for (var j = 0; j <= segV; j++)
            for (var i = 0; i <= segU; i++)
            {
                var fu = i / (float)segU;
                var fv = j / (float)segV;
                var p = origin + axisU * fu + axisV * fv;
                if (offset != null) p += offset(fu, fv);
                grid[j * (segU + 1) + i] = p;
            }
            for (var j = 0; j < segV; j++)
            for (var i = 0; i < segU; i++)
            {
                var a = grid[j * (segU + 1) + i];
                var b = grid[(j + 1) * (segU + 1) + i];
                var c = grid[(j + 1) * (segU + 1) + i + 1];
                var d = grid[j * (segU + 1) + i + 1];
                var lu = axisU.magnitude;
                var lv = axisV.magnitude;
                var uva = new Vector2(lu * i / segU, lv * j / segV);
                var uvb = new Vector2(lu * i / segU, lv * (j + 1) / segV);
                var uvc = new Vector2(lu * (i + 1) / segU, lv * (j + 1) / segV);
                var uvd = new Vector2(lu * (i + 1) / segU, lv * j / segV);
                AddQuad(a, b, c, d, uva, uvb, uvc, uvd);
                if (doubleSided) AddQuad(d, c, b, a, uvd, uvc, uvb, uva);
            }
        }

        // ------------------------------------------------------------------ colliders

        /// <summary>Adds a collider box in local space (transformed by the current matrix; non-uniform rotations are kept).</summary>
        public void AddCollider(Vector3 center, Vector3 size, SurfaceType surface, int layer = LayerIds.Default, string name = null)
        {
            var c = _matrix.MultiplyPoint3x4(center);
            var rot = _matrix.rotation;
            var scale = _matrix.lossyScale;
            var s = new Vector3(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y), Mathf.Abs(size.z * scale.z));
            _colliders.Add(new ColliderBox(c, s, rot, surface, layer, name));
        }

        /// <summary>Adds an already transformed collider.</summary>
        public void AddColliderRaw(ColliderBox box) => _colliders.Add(box);

        // ------------------------------------------------------------------ output

        /// <summary>
        /// Builds a Unity mesh for a LOD (0 = everything, 1 = without <see cref="LodDetail"/> geometry). Submeshes follow
        /// <see cref="Slots"/>; empty submeshes are kept (so materials stay aligned). Unused vertices are dropped.
        /// </summary>
        public Mesh ToMesh(string name, int lod = 0)
        {
            var bit = lod == 0 ? 1 : 2;
            var remap = new int[_positions.Count];
            for (var i = 0; i < remap.Length; i++) remap[i] = -1;
            var pos = new List<Vector3>(_positions.Count);
            var nrm = new List<Vector3>(_positions.Count);
            var uv = new List<Vector2>(_positions.Count);
            var subs = new List<int>[_slots.Count];
            for (var s = 0; s < _slots.Count; s++)
            {
                var tris = _triangles[s];
                var lods = _triangleLods[s];
                var outTris = new List<int>(tris.Count);
                for (var t = 0; t < lods.Count; t++)
                {
                    if ((lods[t] & bit) == 0) continue;
                    for (var k = 0; k < 3; k++)
                    {
                        var v = tris[t * 3 + k];
                        if (remap[v] < 0)
                        {
                            remap[v] = pos.Count;
                            pos.Add(_positions[v]);
                            nrm.Add(_normals[v]);
                            uv.Add(_uvs[v]);
                        }
                        outTris.Add(remap[v]);
                    }
                }
                subs[s] = outTris;
            }
            var mesh = new Mesh { name = name };
            if (pos.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs.Length;
            for (var s = 0; s < subs.Length; s++) mesh.SetTriangles(subs[s], s, false);
            mesh.RecalculateBounds();
            if (pos.Count > 0) mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Appends another builder's geometry (slots merged by key) under an optional transform.</summary>
        public void Append(MeshBuilder other, Matrix4x4 transform)
        {
            if (other == null || other.IsEmpty) return;
            var normalM = transform.inverse.transpose;
            var baseIndex = _positions.Count;
            for (var i = 0; i < other._positions.Count; i++)
            {
                _positions.Add(transform.MultiplyPoint3x4(other._positions[i]));
                _normals.Add(normalM.MultiplyVector(other._normals[i]).normalized);
                _uvs.Add(other._uvs[i]);
            }
            var keepSlot = _slot;
            for (var s = 0; s < other._slots.Count; s++)
            {
                Slot(other._slots[s]);
                var tris = other._triangles[s];
                var lods = other._triangleLods[s];
                var dst = _triangles[_slot];
                var dstLods = _triangleLods[_slot];
                for (var i = 0; i < tris.Count; i++) dst.Add(tris[i] + baseIndex);
                dstLods.AddRange(lods);
            }
            _slot = keepSlot;
            foreach (var c in other._colliders)
            {
                var box = c;
                box.Center = transform.MultiplyPoint3x4(c.Center);
                box.Rotation = transform.rotation * c.Rotation;
                _colliders.Add(box);
            }
        }

        // ------------------------------------------------------------------ internals

        private void EnsureSlot()
        {
            if (_slot < 0) Slot("Default");
        }

        private Vector3 TransformNormal(Vector3 n) => _normalMatrix.MultiplyVector(n).normalized;

        private void AddQuadWorld(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd)
        {
            EnsureSlot();
            var i = _positions.Count;
            AddVertex(a, n, uva);
            AddVertex(b, n, uvb);
            AddVertex(c, n, uvc);
            AddVertex(d, n, uvd);
            AddTri(i, i + 1, i + 2);
            AddTri(i, i + 2, i + 3);
        }

        private void AddVertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            _positions.Add(p);
            _normals.Add(n);
            _uvs.Add(uv);
        }

        private void AddTri(int a, int b, int c)
        {
            var tris = _triangles[_slot];
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
            _triangleLods[_slot].Add(LodMask);
        }

        /// <summary>Box projection in metres: walls use (horizontal, height), floors and roofs use (x, z).</summary>
        private Vector2 Project(Vector3 p, Vector3 n)
        {
            var ax = Mathf.Abs(n.x);
            var ay = Mathf.Abs(n.y);
            var az = Mathf.Abs(n.z);
            Vector2 uv;
            if (ay >= ax && ay >= az) uv = new Vector2(p.x, n.y >= 0f ? p.z : -p.z);
            else if (ax >= az) uv = new Vector2(n.x >= 0f ? -p.z : p.z, p.y);
            else uv = new Vector2(n.z >= 0f ? -p.x : p.x, p.y);
            return uv * UvScale + UvOffset;
        }
    }
}
