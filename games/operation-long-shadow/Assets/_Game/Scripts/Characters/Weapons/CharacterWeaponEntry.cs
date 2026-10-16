using System;
using LongShadow.Characters.Animation;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Weapons
{
    /// <summary>
    /// How bodies carry one weapon id (an entry of <see cref="CharacterWeaponLibrary"/>, LIB_CharacterWeapons): the
    /// world prefab (WP03 <c>PF_W_*</c> or the WP05 fallback built from the manifest mesh), the hold style and finger
    /// pose, and a grip correction for prefabs without a <c>RightGrip</c> socket.
    /// </summary>
    [Serializable]
    public sealed class CharacterWeaponEntry : ILibraryEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private GameObject _worldPrefab;
        [SerializeField] private WeaponHold _hold = WeaponHold.Rifle;
        [SerializeField] private HandPose _handPose = HandPose.RifleGrip;
        [SerializeField, Tooltip("Local position of the prefab root under the hand socket when it has no RightGrip child.")]
        private Vector3 _gripPosition;
        [SerializeField, Tooltip("Local rotation (euler) of the prefab root under the hand socket when it has no RightGrip child.")]
        private Vector3 _gripEuler;
        [SerializeField, Tooltip("Extra correction applied after the RightGrip alignment (tuning per weapon, euler deg).")]
        private Vector3 _tuneEuler;
        [SerializeField] private bool _dropOnDeath = true;

        public CharacterWeaponEntry() { }

        public CharacterWeaponEntry(string id, GameObject worldPrefab, WeaponHold hold, HandPose handPose, bool dropOnDeath = true)
        {
            _id = id;
            _worldPrefab = worldPrefab;
            _hold = hold;
            _handPose = handPose;
            _dropOnDeath = dropOnDeath;
        }

        /// <summary>Weapon id (Core <see cref="WeaponIds"/>).</summary>
        public string Id => _id;
        /// <summary>World model prefab (may be null: the holder builds a placeholder).</summary>
        public GameObject WorldPrefab => _worldPrefab;
        public WeaponHold Hold => _hold;
        public HandPose HandPose => _handPose;
        public Vector3 GripPosition => _gripPosition;
        public Quaternion GripRotation => Quaternion.Euler(_gripEuler);
        public Quaternion TuneRotation => Quaternion.Euler(_tuneEuler);
        /// <summary>The held weapon falls with the body on death.</summary>
        public bool DropOnDeath => _dropOnDeath;

#if UNITY_EDITOR
        /// <summary>Builders: keeps hand-tuned values when the prefab reference is refreshed.</summary>
        public void EditorSetPrefab(GameObject prefab) => _worldPrefab = prefab;
#endif
    }
}
