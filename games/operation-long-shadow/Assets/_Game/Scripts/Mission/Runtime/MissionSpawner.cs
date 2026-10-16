using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Objectives;
using UnityEngine;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// Runtime <see cref="IMissionSpawner"/> (TECH_PLAN §7.11 "Interactable spawns"): intel pickups from LIB_Mission
    /// (the model, else a greybox box in the missing-asset tint with one log line), each with a collider on layer
    /// Interactable sized to the model and an <see cref="IntelPickup"/>; helper anchors for markers that are missing from the
    /// scene. Everything lives under the MissionSystems object in S_Persistent (positions are world positions).
    /// </summary>
    public sealed class MissionSpawner : IMissionSpawner
    {
        private readonly Transform _root;
        private readonly MissionAssetLibrary _library;
        private readonly Dictionary<string, GameObject> _intel = new Dictionary<string, GameObject>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> _anchors = new Dictionary<string, Transform>(System.StringComparer.Ordinal);
        private System.Func<string, bool> _collect;
        private System.Func<string, bool> _isCollected;

        /// <summary>Creates the spawner under <paramref name="root"/>.</summary>
        public MissionSpawner(Transform root, MissionAssetLibrary library)
        {
            _root = root;
            _library = library;
        }

        /// <summary>Sets the collect callbacks the pickups call (the director).</summary>
        public void Bind(System.Func<string, bool> collect, System.Func<string, bool> isCollected)
        {
            _collect = collect;
            _isCollected = isCollected;
        }

        private MissionAssetLibrary Library => _library != null ? _library : Libraries.TryGet(out MissionAssetLibrary l) ? l : null;

        /// <inheritdoc/>
        public void SpawnIntel(IntelDef intel, Vector3 position, Quaternion rotation, Transform anchor)
        {
            if (intel == null) return;
            if (_intel.TryGetValue(intel.Id, out var existing) && existing != null)
            {
                existing.transform.SetPositionAndRotation(position, rotation);
                existing.SetActive(true);
                return;
            }
            var go = new GameObject("Intel_" + intel.Id);
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.layer = LayerIds.Interactable;
            var entry = Library != null && Library.TryGet(intel.Id, out var e) ? e : null;
            GameObject visual;
            if (entry != null && entry.Model != null)
            {
                visual = Object.Instantiate(entry.Model, go.transform, false);
                visual.name = entry.Model.name;
            }
            else
            {
                LSLog.MissingAsset(LogCategory.Mission, intel.PropManifestId ?? intel.Id);
                visual = Placeholders.Create(intel.Id, PrimitiveType.Cube, new Vector3(0.32f, 0.08f, 0.24f), Placeholders.Missing, go.transform);
                visual.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            }
            foreach (var c in visual.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            var box = go.AddComponent<BoxCollider>();
            FitCollider(box, go.transform, visual);
            var pickup = go.AddComponent<IntelPickup>();
            pickup.Configure(intel.Id, _collect, _isCollected, anchor);
            _intel[intel.Id] = go;
        }

        /// <inheritdoc/>
        public void DespawnIntel(string intelId)
        {
            if (intelId == null || !_intel.TryGetValue(intelId, out var go)) return;
            _intel.Remove(intelId);
            if (go != null) Object.Destroy(go);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            foreach (var kv in _intel)
                if (kv.Value != null) Object.Destroy(kv.Value);
            _intel.Clear();
        }

        /// <inheritdoc/>
        public Sprite IntelPhoto(IntelDef intel)
        {
            if (intel == null) return null;
            var lib = Library;
            if (lib != null && lib.TryGet(intel.Id, out var e) && e.Photo != null) return e.Photo;
            LSLog.MissingAsset(LogCategory.Mission, intel.PhotoManifestId ?? intel.Id);
            return null;
        }

        /// <inheritdoc/>
        public Transform Anchor(string id, Vector3 position)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_anchors.TryGetValue(id, out var t) && t != null) return t;
            var go = new GameObject("Anchor_" + id);
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            _anchors[id] = go.transform;
            return go.transform;
        }

        private static void FitCollider(BoxCollider box, Transform root, GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                box.size = new Vector3(0.4f, 0.2f, 0.4f);
                box.center = new Vector3(0f, 0.1f, 0f);
                return;
            }
            var b = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            box.center = root.InverseTransformPoint(b.center);
            var size = root.InverseTransformVector(b.size);
            // Pad small items so the 1.8 m interaction cast finds them easily.
            box.size = new Vector3(Mathf.Max(0.3f, Mathf.Abs(size.x) + 0.1f), Mathf.Max(0.2f, Mathf.Abs(size.y) + 0.1f),
                Mathf.Max(0.3f, Mathf.Abs(size.z) + 0.1f));
        }
    }
}
