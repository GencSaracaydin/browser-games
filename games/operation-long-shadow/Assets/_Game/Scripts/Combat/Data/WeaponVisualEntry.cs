using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// Asset references of one weapon id in <c>LIB_WeaponVisuals</c> (TECH_PLAN §7.5): view-model prefab
    /// (<c>PF_VM_*</c>), world prefab (<c>PF_W_*</c>, also held by AI bodies), dropped-magazine prefab, pickup prefab and HUD
    /// icon. Filled by the WeaponPrefabBuilder; any reference may be null until its art lands (callers use placeholders).
    /// </summary>
    [Serializable]
    public sealed class WeaponVisualEntry : ILibraryEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private GameObject _viewmodel;
        [SerializeField] private GameObject _world;
        [SerializeField] private GameObject _magazineDrop;
        [SerializeField] private GameObject _pickup;
        [SerializeField] private Sprite _icon;
        [SerializeField, Tooltip("True when the prefabs were built from real art (false = greybox placeholder).")]
        private bool _hasArt;

        /// <summary>Creates an entry (editor builders).</summary>
        public WeaponVisualEntry(string id, GameObject viewmodel, GameObject world, GameObject magazineDrop, GameObject pickup,
                                 Sprite icon, bool hasArt)
        {
            _id = id;
            _viewmodel = viewmodel;
            _world = world;
            _magazineDrop = magazineDrop;
            _pickup = pickup;
            _icon = icon;
            _hasArt = hasArt;
        }

        /// <inheritdoc/>
        public string Id => _id;
        /// <summary>First-person prefab (layer Viewmodel, ViewmodelRig on the root).</summary>
        public GameObject Viewmodel => _viewmodel;
        /// <summary>Third-person / world prefab (LODs, Socket_Muzzle, Socket_LeftHand).</summary>
        public GameObject World => _world;
        /// <summary>Physical magazine dropped by an empty reload.</summary>
        public GameObject MagazineDrop => _magazineDrop;
        /// <summary>Ground pickup (WeaponPickup on the root).</summary>
        public GameObject Pickup => _pickup;
        /// <summary>HUD icon (optional).</summary>
        public Sprite Icon => _icon;
        /// <summary>True when built from real art.</summary>
        public bool HasArt => _hasArt;
    }
}
