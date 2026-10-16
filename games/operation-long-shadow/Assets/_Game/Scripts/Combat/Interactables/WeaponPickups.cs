using System.Collections.Generic;
using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LongShadow.Combat.Interactables
{
    /// <summary>
    /// Dropped weapons (GDD §13.1): spawning (library pickup prefab, else the world prefab, else a greybox), the cap of
    /// <see cref="WeaponTable.MaxDroppedWeapons"/> (oldest despawns first), the walk-over reserve top-up, and section
    /// ownership (a pickup moves into the scene of the ground it lies on, so "drops in an unloaded section are gone").
    /// Statics reset on SubsystemRegistration.
    /// </summary>
    public static class WeaponPickups
    {
        /// <summary>Horizontal radius of the walk-over top-up.</summary>
        public const float TopUpRadius = 1.1f;
        /// <summary>Vertical tolerance of the walk-over top-up.</summary>
        public const float TopUpHeight = 1.4f;

        private static readonly List<WeaponPickup> s_all = new List<WeaponPickup>(8);
        private static readonly RaycastHit[] s_hit = new RaycastHit[1];
        private static ServiceRef<IAudioService> s_audio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
            s_audio = default;
        }

        /// <summary>Live pickups (oldest first).</summary>
        public static IReadOnlyList<WeaponPickup> All => s_all;

        /// <summary>Called by <see cref="WeaponPickup"/> on enable.</summary>
        public static void Register(WeaponPickup p)
        {
            if (p == null || s_all.Contains(p)) return;
            s_all.Add(p);
            Prune();
            // GDD §13.1: at most 6 dropped weapons exist at once (oldest despawns first).
            while (s_all.Count > WeaponTable.MaxDroppedWeapons)
            {
                var oldest = Oldest(p);
                if (oldest == null) break;
                s_all.Remove(oldest);
                Despawn(oldest);
            }
        }

        /// <summary>Called by <see cref="WeaponPickup"/> on disable.</summary>
        public static void Unregister(WeaponPickup p) => s_all.Remove(p);

        /// <summary>
        /// Spawns a pickup of <paramref name="weaponId"/> at <paramref name="position"/> (dropped from a dying enemy's hands
        /// or by a pickup swap). Ammo -1 = the weapon's defaults. Returns null for weapons that never drop (GDD §13.1:
        /// AKMS, SVD, RPG-7, DShK; the Hush Puppy is never dropped).
        /// </summary>
        public static WeaponPickup Spawn(string weaponId, Vector3 position, int magazine = -1, int reserve = -1, Vector3 impulse = default)
        {
            if (!WeaponIds.IsPrimary(weaponId)) return null;
            var go = CreateVisual(weaponId);
            go.name = "Pickup_" + weaponId;
            var t = go.transform;
            var yaw = Random.Range(0f, 360f);
            t.SetPositionAndRotation(position + Vector3.up * 0.15f, Quaternion.Euler(0f, yaw, Random.value < 0.5f ? 90f : -90f));
            var pickup = go.GetComponent<WeaponPickup>();
            if (pickup == null) pickup = go.AddComponent<WeaponPickup>();
            EnsurePhysics(go);
            pickup.Setup(weaponId, magazine, reserve);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = t.position;
                rb.rotation = t.rotation;
                rb.linearVelocity = impulse;
                rb.angularVelocity = Random.insideUnitSphere * 2f;
            }
            MoveToGroundScene(go, position);
            return pickup;
        }

        /// <summary>
        /// Walk-over top-up (GDD §13.1): a pickup of a weapon the player carries, within reach of his feet, gives its rounds
        /// to that weapon's reserve; an emptied pickup despawns. Allocation-free.
        /// </summary>
        public static void TickTopUp(Vector3 feet, WeaponInventory inventory)
        {
            if (inventory == null || s_all.Count == 0) return;
            for (var i = s_all.Count - 1; i >= 0; i--)
            {
                var p = s_all[i];
                if (p == null)
                {
                    s_all.RemoveAt(i);
                    continue;
                }
                var d = p.transform.position - feet;
                if (Mathf.Abs(d.y) > TopUpHeight || d.x * d.x + d.z * d.z > TopUpRadius * TopUpRadius) continue;
                if (!inventory.HasWeapon(p.WeaponId)) continue;
                var space = inventory.ReserveSpace(p.WeaponId);
                if (space <= 0) continue;
                var taken = p.TakeRounds(space);
                if (taken <= 0) continue;
                inventory.AddReserve(p.WeaponId, taken);
                s_audio.Value?.Play2D(CombatCues.Pickup, 0.8f);
                if (p.TotalRounds <= 0)
                {
                    s_all.RemoveAt(i);
                    Despawn(p);
                }
            }
        }

        /// <summary>Removes every pickup (checkpoint reload; GDD §17 restores the loadout, not the floor).</summary>
        public static void Clear()
        {
            for (var i = s_all.Count - 1; i >= 0; i--)
                if (s_all[i] != null) Despawn(s_all[i]);
            s_all.Clear();
        }

        private static WeaponPickup Oldest(WeaponPickup except)
        {
            WeaponPickup best = null;
            for (var i = 0; i < s_all.Count; i++)
            {
                var p = s_all[i];
                if (p == null || p == except) continue;
                if (best == null || p.SpawnTime < best.SpawnTime) best = p;
            }
            return best;
        }

        private static void Prune()
        {
            for (var i = s_all.Count - 1; i >= 0; i--)
                if (s_all[i] == null) s_all.RemoveAt(i);
        }

        private static void Despawn(WeaponPickup p)
        {
            if (p == null) return;
            Object.Destroy(p.gameObject);
        }

        private static GameObject CreateVisual(string weaponId)
        {
            if (Libraries.TryGet(out WeaponVisualsLibrary lib) && lib.TryGet(weaponId, out var e))
            {
                if (e.Pickup != null) return Object.Instantiate(e.Pickup);
                if (e.World != null)
                {
                    var w = Object.Instantiate(e.World);
                    StripForPickup(w);
                    return w;
                }
            }
            LSLog.MissingAsset(LogCategory.Combat, "LIB_WeaponVisuals/" + weaponId + " (pickup)", WeaponVisualsLibrary.AssetPath);
            var gb = WeaponGreybox.Build(weaponId, Placeholders.Material);
            StripForPickup(gb);
            return gb;
        }

        private static void StripForPickup(GameObject go)
        {
            CombatUtil.SetLayerRecursively(go, LayerIds.Debris);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>Adds a Debris-layer body collider, a Rigidbody and an Interactable-layer use collider when missing.</summary>
        public static void EnsurePhysics(GameObject go)
        {
            var bounds = LocalBounds(go);
            if (go.GetComponent<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = Vector3.Max(bounds.size, new Vector3(0.04f, 0.04f, 0.2f));
            }
            go.layer = LayerIds.Debris;
            var use = go.transform.Find("Use");
            if (use == null)
            {
                use = new GameObject("Use").transform;
                use.SetParent(go.transform, false);
                var c = use.gameObject.AddComponent<BoxCollider>();
                c.center = bounds.center;
                c.size = Vector3.Max(bounds.size + new Vector3(0.25f, 0.25f, 0.15f), new Vector3(0.3f, 0.3f, 0.5f));
            }
            use.gameObject.layer = LayerIds.Interactable;
            if (!go.TryGetComponent<Rigidbody>(out var rb)) rb = go.AddComponent<Rigidbody>();
            rb.mass = 3.5f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        private static Bounds LocalBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<MeshFilter>(true);
            var has = false;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            var inv = go.transform.worldToLocalMatrix;
            foreach (var mf in renderers)
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = inv * mf.transform.localToWorldMatrix;
                for (var i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!has)
                    {
                        b = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else b.Encapsulate(p);
                }
            }
            return has ? b : new Bounds(new Vector3(0f, 0.03f, 0.2f), new Vector3(0.06f, 0.15f, 0.8f));
        }

        private static void MoveToGroundScene(GameObject go, Vector3 position)
        {
            if (go.transform.parent != null) return;
            if (Physics.RaycastNonAlloc(position + Vector3.up * 0.5f, Vector3.down, s_hit, 4f, LayerIds.Ground, QueryTriggerInteraction.Ignore) == 0) return;
            var scene = s_hit[0].collider.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded && scene != go.scene) SceneManager.MoveGameObjectToScene(go, scene);
        }
    }
}
