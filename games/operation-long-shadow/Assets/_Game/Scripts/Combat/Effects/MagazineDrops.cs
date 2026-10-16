using System.Collections.Generic;
using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Physical magazines dropped by empty reloads (GDD §13.1 "the separately generated magazine"; CINEMATIC_DIRECTION §4).
    /// A small ring per weapon id, created on first use from <c>LIB_WeaponVisuals</c> MagazineDrop (PF_Mag_*) or a greybox
    /// magazine, on layer Debris; each lies for <see cref="Lifetime"/> seconds.
    /// </summary>
    public sealed class MagazineDrops
    {
        /// <summary>Magazines per weapon id.</summary>
        public const int PerWeapon = 3;
        /// <summary>Seconds a dropped magazine stays.</summary>
        public const float Lifetime = 12f;

        private sealed class Ring
        {
            public GameObject[] Items;
            public Rigidbody[] Bodies;
            public float[] Age;
            public int Next;
        }

        private readonly Transform _root;
        private readonly Dictionary<string, Ring> _rings = new Dictionary<string, Ring>();

        /// <summary>Creates an empty set of rings under <paramref name="root"/>.</summary>
        public MagazineDrops(Transform root) => _root = root;

        /// <summary>Drops a magazine of <paramref name="weaponId"/> at a world pose with a velocity.</summary>
        public void Drop(string weaponId, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            if (!_rings.TryGetValue(weaponId, out var ring)) _rings[weaponId] = ring = CreateRing(weaponId);
            var i = ring.Next;
            ring.Next = (ring.Next + 1) % ring.Items.Length;
            var go = ring.Items[i];
            if (go == null) return;
            go.SetActive(false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            var rb = ring.Bodies[i];
            if (rb != null)
            {
                rb.position = position;
                rb.rotation = rotation;
                rb.linearVelocity = velocity;
                rb.angularVelocity = new Vector3(Random.Range(-4f, 4f), Random.Range(-2f, 2f), Random.Range(-6f, 6f));
            }
            ring.Age[i] = 0f;
        }

        /// <summary>Ages and hides old magazines.</summary>
        public void Tick(float dt)
        {
            foreach (var kv in _rings)
            {
                var r = kv.Value;
                for (var i = 0; i < r.Items.Length; i++)
                {
                    var go = r.Items[i];
                    if (go == null || !go.activeSelf) continue;
                    r.Age[i] += dt;
                    if (r.Age[i] >= Lifetime) go.SetActive(false);
                }
            }
        }

        /// <summary>Hides every magazine.</summary>
        public void Clear()
        {
            foreach (var kv in _rings)
                foreach (var go in kv.Value.Items)
                    if (go != null) go.SetActive(false);
        }

        private Ring CreateRing(string weaponId)
        {
            GameObject prefab = null;
            if (Libraries.TryGet(out WeaponVisualsLibrary lib) && lib.TryGet(weaponId, out var e)) prefab = e.MagazineDrop;
            if (prefab == null) LSLog.MissingAsset(LogCategory.Combat, "LIB_WeaponVisuals/" + weaponId + " (magazine)", WeaponVisualsLibrary.AssetPath);
            var ring = new Ring { Items = new GameObject[PerWeapon], Bodies = new Rigidbody[PerWeapon], Age = new float[PerWeapon] };
            for (var i = 0; i < PerWeapon; i++)
            {
                var go = prefab != null ? Object.Instantiate(prefab, _root) : WeaponGreybox.BuildMagazineOnly(weaponId, Placeholders.Material);
                go.transform.SetParent(_root, false);
                go.name = "DroppedMag_" + weaponId + "_" + i;
                CombatUtil.SetLayerRecursively(go, LayerIds.Debris);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;
                if (go.GetComponentInChildren<Collider>() == null)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, -0.07f, 0.015f);
                    box.size = new Vector3(0.03f, 0.15f, 0.06f);
                }
                if (!go.TryGetComponent<Rigidbody>(out var rb)) rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.45f;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.3f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                go.SetActive(false);
                ring.Items[i] = go;
                ring.Bodies[i] = rb;
            }
            return ring;
        }
    }
}
