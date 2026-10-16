using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Level.Editor.Common
{
    /// <summary>
    /// Places props by <see cref="PropCatalog"/> key (TECH_PLAN D12/D13): the model is looked up by manifest id through
    /// <see cref="AssetManifest"/> (WP10_CC0 / WP10_FAL output under <c>Art/Models/...</c>); while it has not landed a greybox
    /// of the real size stands in (logged once by the manifest lookup). Props get a box/capsule/sphere collider with a
    /// <see cref="SurfaceTag"/>, light-probe GI (D8) unless they are big lightmapped pieces, and static flags.
    /// </summary>
    public static class LevelProps
    {
        private static readonly Dictionary<string, GameObject> s_models = new Dictionary<string, GameObject>();
        private static readonly HashSet<string> s_missing = new HashSet<string>();

        /// <summary>Keys whose model was missing during this editor session (for build reports).</summary>
        public static IEnumerable<string> MissingKeys => s_missing;

        /// <summary>Forgets cached lookups (after assets landed).</summary>
        public static void Reload()
        {
            s_models.Clear();
            s_missing.Clear();
        }

        /// <summary>The model asset of a prop (FBX root) or null.</summary>
        public static GameObject Model(string key)
        {
            if (s_models.TryGetValue(key, out var m)) return m;
            var def = PropCatalog.Get(key);
            if (def == null)
            {
                LSLog.WarnOnce("prop-unknown:" + key, LogCategory.Level, "Unknown prop key " + key);
                s_models[key] = null;
                return null;
            }
            var found = AssetManifest.FindAll<GameObject>(def.ManifestId, def.NameContains);
            // Prefer the exact name ("SM_FilmCan" must not pick "SM_FilmCanClosed").
            GameObject best = null;
            foreach (var g in found)
            {
                if (g.name == def.NameContains)
                {
                    best = g;
                    break;
                }
                if (best == null) best = g;
            }
            if (best == null)
            {
                s_missing.Add(key);
                LSLog.MissingAsset(LogCategory.Level, def.ManifestId + "/" + def.NameContains, AssetManifest.ResolvePath(def.ManifestId));
            }
            s_models[key] = best;
            return best;
        }

        /// <summary>
        /// Places a prop at a world position with a compass heading and uniform scale. Returns the instance (model or
        /// greybox). <paramref name="isStatic"/> false for movable things (doors, fruit, vehicles).
        /// </summary>
        public static GameObject Place(string key, Transform parent, Vector3 position, float heading, float scale = 1f, string name = null,
                                       bool isStatic = true, bool collider = true)
        {
            var def = PropCatalog.Get(key);
            var model = Model(key);
            GameObject go;
            if (model != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = Greybox(key, def);
                go.transform.SetParent(parent, false);
            }
            go.name = name ?? key;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            go.transform.localScale = Vector3.one * scale;
            if (def != null)
            {
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.receiveGI = def.Lightmapped && isStatic ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
                    r.lightProbeUsage = LightProbeUsage.BlendProbes;
                    if (def.Lightmapped && isStatic) LevelObjects.SetLightmapScale(r, 0.5f);
                    if (isStatic) GameObjectUtility.SetStaticEditorFlags(r.gameObject, LevelObjects.PropFlags);
                }
                if (collider && def.Collider != 0) AddCollider(go, def);
            }
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, LevelObjects.PropFlags);
            return go;
        }

        private static GameObject Greybox(string key, PropDef def)
        {
            var size = def != null ? def.Size : Vector3.one * 0.5f;
            var root = new GameObject("PH_" + key);
            var prim = BuilderUtil.PlaceholderPrimitive(key, def != null ? def.Primitive : PrimitiveType.Cube, size, def != null ? def.Color : Placeholders.Missing, root.transform);
            prim.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            if (def != null && (def.Primitive == PrimitiveType.Cylinder || def.Primitive == PrimitiveType.Capsule))
                prim.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
            return root;
        }

        private static void AddCollider(GameObject go, PropDef def)
        {
            var bounds = LocalBounds(go, def);
            var host = go;
            host.layer = def.Layer == LayerIds.Default ? host.layer : def.Layer;
            switch (def.Collider)
            {
                case 2:
                {
                    // Trees: trunk capsule only (the canopy never blocks bullets or movement).
                    var c = host.AddComponent<CapsuleCollider>();
                    c.center = new Vector3(0f, 1.1f, 0f);
                    c.radius = 0.22f;
                    c.height = 2.2f;
                    break;
                }
                case 3:
                {
                    var c = host.AddComponent<SphereCollider>();
                    c.center = bounds.center;
                    c.radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
                    break;
                }
                default:
                {
                    var c = host.AddComponent<BoxCollider>();
                    c.center = bounds.center;
                    c.size = bounds.size;
                    break;
                }
            }
            if (host.GetComponent<SurfaceTag>() == null) host.AddComponent<SurfaceTag>().Surface = def.Surface;
        }

        /// <summary>Bounds of a hierarchy's meshes in the root's local space (unscaled).</summary>
        public static Bounds LocalBounds(GameObject go, PropDef def)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>(true);
            var has = false;
            var b = new Bounds();
            var inv = go.transform.worldToLocalMatrix;
            foreach (var f in filters)
            {
                if (f.sharedMesh == null) continue;
                var lb = f.sharedMesh.bounds;
                var m = inv * f.transform.localToWorldMatrix;
                for (var i = 0; i < 8; i++)
                {
                    var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!has)
                    {
                        b = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else b.Encapsulate(p);
                }
            }
            if (!has && def != null) b = new Bounds(new Vector3(0f, def.Size.y * 0.5f, 0f), def.Size);
            return b;
        }
    }
}
