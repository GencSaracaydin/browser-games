using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Geometry;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Level.Editor.Common
{
    /// <summary>
    /// Turns <see cref="MeshBuilder"/> output into scene objects: saved mesh assets, renderers with the slot materials,
    /// LOD groups (LOD0 lightmapped, LOD1 light-probe lit, D8), simplified box colliders with <see cref="SurfaceTag"/>s,
    /// static flags and lightmap scales (GDD §9.4 texel densities).
    /// </summary>
    public static class LevelObjects
    {
        /// <summary>Static flags of baked architecture.</summary>
        public const StaticEditorFlags ArchitectureFlags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                                                           StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;

        /// <summary>Static flags of light-probe lit props and details.</summary>
        public const StaticEditorFlags PropFlags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;

        /// <summary>
        /// Creates <paramref name="name"/> under <paramref name="parent"/> at <paramref name="position"/>/<paramref name="rotation"/>
        /// with the builder's geometry (LOD0 + optional LOD1) and colliders. Returns the root object.
        /// </summary>
        public static GameObject Build(string name, Transform parent, Vector3 position, Quaternion rotation, MeshBuilder mb, LevelObjectOptions o)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            if (mb == null) return root;
            if (mb.IsEmpty)
            {
                // Collider-only builders (ground boxes under decorative quads written elsewhere).
                if (o.Colliders) AddColliders(root, mb.Colliders);
                GameObjectUtility.SetStaticEditorFlags(root, o.Flags);
                return root;
            }
            var safe = MeshAssets.SafeName(name);
            var lod0Mesh = MeshAssets.Save(mb.ToMesh(safe + "_LOD0", 0), o.Folder + "/" + safe + "_LOD0.asset", o.LightmapScale > 0f);
            var mats = LevelMaterials.Resolve(mb.Slots, o.Proxy);
            var lod0 = Renderer(root.transform, safe + "_LOD0", lod0Mesh, mats, o, o.LightmapScale > 0f);
            if (o.Lods && mb.TriangleCount(1) < mb.TriangleCount(0) * 0.92f)
            {
                var lod1Mesh = MeshAssets.Save(mb.ToMesh(safe + "_LOD1", 1), o.Folder + "/" + safe + "_LOD1.asset", false);
                var lod1 = Renderer(root.transform, safe + "_LOD1", lod1Mesh, mats, o, false);
                var group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = true;
                group.SetLODs(new[]
                {
                    new LOD(0.09f, new Renderer[] { lod0 }),
                    new LOD(0f, new Renderer[] { lod1 }),
                });
                group.RecalculateBounds();
            }
            if (o.Colliders) AddColliders(root, mb.Colliders);
            GameObjectUtility.SetStaticEditorFlags(root, o.Flags);
            return root;
        }

        private static MeshRenderer Renderer(Transform parent, string name, Mesh mesh, Material[] mats, LevelObjectOptions o, bool lightmapped)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = o.Layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = o.Shadows;
            r.receiveGI = lightmapped ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            if (lightmapped) SetLightmapScale(r, o.LightmapScale);
            // LOD1 and probe-lit pieces still contribute GI (they occlude bounce) but receive it from light probes (D8).
            GameObjectUtility.SetStaticEditorFlags(go, o.Flags);
            return r;
        }

        /// <summary>Sets the renderer's lightmap scale (Lightmapping "Scale In Lightmap").</summary>
        public static void SetLightmapScale(Renderer r, float scale)
        {
            var so = new SerializedObject(r);
            var p = so.FindProperty("m_ScaleInLightmap");
            if (p == null) return;
            p.floatValue = scale;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Adds the builder's collider boxes under <paramref name="owner"/>: one child per (rotation, layer, surface) group
        /// with several BoxColliders, each group tagged with its <see cref="SurfaceTag"/>.
        /// </summary>
        public static void AddColliders(GameObject owner, IReadOnlyList<ColliderBox> boxes)
        {
            if (boxes == null || boxes.Count == 0) return;
            var groups = new Dictionary<string, GameObject>();
            var parent = new GameObject("Colliders").transform;
            parent.SetParent(owner.transform, false);
            foreach (var b in boxes)
            {
                var e = b.Rotation.eulerAngles;
                var key = Mathf.RoundToInt(e.x) + "_" + Mathf.RoundToInt(e.y) + "_" + Mathf.RoundToInt(e.z) + "_" + b.Layer + "_" + b.Surface + "_" + b.Name;
                if (!groups.TryGetValue(key, out var go))
                {
                    go = new GameObject(string.IsNullOrEmpty(b.Name) ? "COL_" + b.Surface : b.Name);
                    go.transform.SetParent(parent, false);
                    go.transform.localRotation = b.Rotation;
                    go.layer = b.Layer;
                    if (b.Layer == LayerIds.Default) go.AddComponent<SurfaceTag>().Surface = b.Surface;
                    groups.Add(key, go);
                }
                var col = go.AddComponent<BoxCollider>();
                col.center = Quaternion.Inverse(b.Rotation) * b.Center;
                col.size = b.Size;
            }
        }

        /// <summary>A PlayerClip box (invisible, collides with the player only; TECH_PLAN §3.9).</summary>
        public static GameObject PlayerClip(string name, Transform parent, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rotation);
            go.layer = LayerIds.PlayerClip;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>Clears the static flags of a hierarchy (moving doors, vehicles, swapped pieces).</summary>
        public static void ClearStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            foreach (Transform t in go.transform) ClearStatic(t.gameObject);
        }

        /// <summary>Puts a hierarchy on a layer.</summary>
        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }
    }
}
