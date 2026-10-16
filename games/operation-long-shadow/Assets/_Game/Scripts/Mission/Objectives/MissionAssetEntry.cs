using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Mission.Objectives
{
    /// <summary>
    /// One entry of <see cref="MissionAssetLibrary"/> (LIB_Mission): the pickup model and the intel-card photo of an intel
    /// item (id = <c>IntelTable</c> id, e.g. "Intel.Ledgers"). Filled by LongShadow/Mission/Build Mission Library from the
    /// ASSET_MANIFEST ids in <c>IntelTable</c>; either field may be empty while the asset has not landed.
    /// </summary>
    [Serializable]
    public sealed class MissionAssetEntry : ILibraryEntry
    {
        [SerializeField] private string _id;
        [SerializeField, Tooltip("Pickup model (FAL_LEDGER_STACK, FAL_FILM_CAN, FAL_CODEBOOK); empty = greybox.")]
        private GameObject _model;
        [SerializeField, Tooltip("Intel card photo (IMG_INTEL_*).")]
        private Sprite _photo;

        /// <summary>Creates an entry (builders).</summary>
        public MissionAssetEntry(string id, GameObject model, Sprite photo)
        {
            _id = id;
            _model = model;
            _photo = photo;
        }

        /// <inheritdoc/>
        public string Id => _id;
        /// <summary>Pickup model, or null.</summary>
        public GameObject Model => _model;
        /// <summary>Intel card photo, or null.</summary>
        public Sprite Photo => _photo;
    }
}
