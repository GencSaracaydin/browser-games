using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// One effect in <c>LIB_CombatFx</c> (ids in <see cref="FxIds"/>): a prefab (particle bursts, projectiles, crate), a
    /// material (tracers, decals, muzzle flash), meshes (muzzle-flash variants, casings) and, for atlases, named UV rects
    /// with real-world sizes (decal elements). Fields are optional per kind.
    /// </summary>
    [Serializable]
    public sealed class CombatFxEntry : ILibraryEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Material _material;
        [SerializeField] private Mesh[] _meshes = Array.Empty<Mesh>();
        [SerializeField, Tooltip("Atlas element names (decals: plaster_1, concrete_2 ...).")]
        private string[] _rectNames = Array.Empty<string>();
        [SerializeField, Tooltip("Atlas UV rects (u0, v0, u1, v1), v up.")]
        private Vector4[] _rects = Array.Empty<Vector4>();
        [SerializeField, Tooltip("Real-world size of each element in metres (width, height).")]
        private Vector2[] _sizes = Array.Empty<Vector2>();

        /// <summary>Creates an entry (editor builders).</summary>
        public CombatFxEntry(string id, GameObject prefab, Material material, Mesh[] meshes = null, string[] rectNames = null,
                             Vector4[] rects = null, Vector2[] sizes = null)
        {
            _id = id;
            _prefab = prefab;
            _material = material;
            _meshes = meshes ?? Array.Empty<Mesh>();
            _rectNames = rectNames ?? Array.Empty<string>();
            _rects = rects ?? Array.Empty<Vector4>();
            _sizes = sizes ?? Array.Empty<Vector2>();
        }

        /// <inheritdoc/>
        public string Id => _id;
        /// <summary>Prefab (may be null).</summary>
        public GameObject Prefab => _prefab;
        /// <summary>Material (may be null).</summary>
        public Material Material => _material;
        /// <summary>Meshes (may be empty).</summary>
        public Mesh[] Meshes => _meshes;
        /// <summary>Atlas element names.</summary>
        public string[] RectNames => _rectNames;
        /// <summary>Atlas UV rects (u0, v0, u1, v1).</summary>
        public Vector4[] Rects => _rects;
        /// <summary>Element sizes in metres.</summary>
        public Vector2[] Sizes => _sizes;
    }
}
