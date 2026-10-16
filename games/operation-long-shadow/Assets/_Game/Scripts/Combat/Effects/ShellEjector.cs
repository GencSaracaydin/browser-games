using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Ring buffer of physical casings on layer Debris (TECH_PLAN §7.16: shells pool of 32). Meshes per calibre come from
    /// LIB_CombatFx (SM_WPN_Casings: 5.56, 7.62×39, 7.62×51, 9 mm), else a generated cylinder in a brass placeholder
    /// material. Casings live 3.5 s.
    /// </summary>
    public sealed class ShellEjector
    {
        /// <summary>Seconds a casing stays.</summary>
        public const float Lifetime = 3.5f;

        private readonly ShellCasing[] _items;
        private readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private readonly Mesh _fallbackMesh;
        private readonly Material _fallbackMaterial;
        private int _next;

        /// <summary>Creates <paramref name="count"/> inactive casings.</summary>
        public ShellEjector(Transform root, int count)
        {
            _fallbackMesh = FxFactory.BuildCasingMesh(0.045f, 0.0048f);
            _fallbackMaterial = Placeholders.Material(new Color(0.78f, 0.6f, 0.28f));
            var pm = new PhysicsMaterial("PM_Brass") { bounciness = 0.35f, dynamicFriction = 0.4f, staticFriction = 0.5f };
            _items = new ShellCasing[count];
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Casing_" + i) { layer = LayerIds.Debris };
                go.transform.SetParent(root, false);
                go.SetActive(false);
                go.AddComponent<MeshFilter>().sharedMesh = _fallbackMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _fallbackMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                var col = go.AddComponent<CapsuleCollider>();
                col.direction = 2;
                col.radius = 0.005f;
                col.height = 0.045f;
                col.sharedMaterial = pm;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.012f;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.05f;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                var c = go.AddComponent<ShellCasing>();
                c.Init();
                _items[i] = c;
            }
        }

        /// <summary>Registers the mesh and material of a calibre id (FxIds.Casing*).</summary>
        public void SetCalibre(string casingId, Mesh mesh, Material material)
        {
            if (string.IsNullOrEmpty(casingId)) return;
            if (mesh != null) _meshes[casingId] = mesh;
            if (material != null) _materials[casingId] = material;
        }

        /// <summary>Ejects a casing at a pose with a velocity (world units).</summary>
        public void Eject(string casingId, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            if (string.IsNullOrEmpty(casingId) || _items.Length == 0) return;
            var c = _items[_next];
            _next = (_next + 1) % _items.Length;
            if (c == null) return;
            c.Filter.sharedMesh = _meshes.TryGetValue(casingId, out var m) ? m : _fallbackMesh;
            c.Renderer.sharedMaterial = _materials.TryGetValue(casingId, out var mat) ? mat : _fallbackMaterial;
            var go = c.gameObject;
            go.SetActive(false);
            c.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            c.Body.position = position;
            c.Body.rotation = rotation;
            c.Body.linearVelocity = velocity;
            c.Body.angularVelocity = angularVelocity;
            c.Arm();
        }

        /// <summary>Recycles old casings.</summary>
        public void Tick(float dt)
        {
            for (var i = 0; i < _items.Length; i++)
            {
                var c = _items[i];
                if (c == null || !c.gameObject.activeSelf) continue;
                c.Age += dt;
                if (c.Age >= Lifetime) c.gameObject.SetActive(false);
            }
        }

        /// <summary>Hides every casing.</summary>
        public void Clear()
        {
            for (var i = 0; i < _items.Length; i++)
                if (_items[i] != null) _items[i].gameObject.SetActive(false);
        }
    }
}
