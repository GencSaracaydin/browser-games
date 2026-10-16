using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Interactables
{
    /// <summary>
    /// A weapon lying on the ground (GDD §13.1 "Slots and pickups"): walking over a pickup of the primary you carry tops
    /// up its reserve automatically (<see cref="WeaponPickups"/>); <c>HOLD [E] Take &lt;weapon&gt;</c> (0.4 s) swaps it into
    /// slot 1 and leaves your old primary on the ground. Needs a collider on layer Interactable (the WP02
    /// PlayerInteractor finds it through <see cref="IInteractable"/>). Spawned by <see cref="WeaponPickups.Spawn"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _weaponId = WeaponIds.AK;
        [SerializeField, Tooltip("Rounds in the magazine (-1 = full).")]
        private int _magazine = -1;
        [SerializeField, Tooltip("Reserve rounds (-1 = the weapon's pickup reserve).")]
        private int _reserve = -1;
        [SerializeField, Tooltip("Prompt anchor / line-of-sight target (defaults to this transform).")]
        private Transform _anchor;

        private string _prompt;
        private ServiceRef<IWeaponService> _weapons;

        /// <summary>Weapon id.</summary>
        public string WeaponId => _weaponId;
        /// <summary>Rounds in the magazine.</summary>
        public int Magazine => _magazine;
        /// <summary>Reserve rounds.</summary>
        public int Reserve => _reserve;
        /// <summary>Total rounds carried by this pickup.</summary>
        public int TotalRounds => Mathf.Max(0, _magazine) + Mathf.Max(0, _reserve);
        /// <summary>Time.time when it appeared (oldest despawns first).</summary>
        public float SpawnTime { get; private set; }

        /// <summary>Sets the weapon and its ammunition (-1 = defaults from WeaponTable).</summary>
        public void Setup(string weaponId, int magazine, int reserve)
        {
            _weaponId = weaponId;
            var w = WeaponTable.Get(weaponId);
            _magazine = magazine >= 0 ? magazine : w != null ? w.MagazineSize : 0;
            _reserve = reserve >= 0 ? reserve : w != null ? w.PickupReserve : 0;
            _prompt = null;
            SpawnTime = Time.time;
        }

        /// <summary>Removes rounds for an automatic top-up (magazine rounds first); returns rounds removed.</summary>
        public int TakeRounds(int wanted)
        {
            if (wanted <= 0) return 0;
            var fromMag = Mathf.Min(wanted, Mathf.Max(0, _magazine));
            _magazine -= fromMag;
            var fromRes = Mathf.Min(wanted - fromMag, Mathf.Max(0, _reserve));
            _reserve -= fromRes;
            return fromMag + fromRes;
        }

        /// <inheritdoc/>
        public string PromptText => _prompt ?? (_prompt = "HOLD [E] Take " + WeaponTable.DisplayName(_weaponId));
        /// <inheritdoc/>
        public bool Hold => true;
        /// <inheritdoc/>
        public float HoldSeconds => WeaponTable.PickupHoldSeconds; // GDD §13.1: 0.4 s
        /// <inheritdoc/>
        public Transform Anchor => _anchor != null ? _anchor : transform;

        /// <inheritdoc/>
        public bool CanInteract(IPlayerRig player)
        {
            var svc = _weapons.Value;
            if (svc == null || svc.IsHolstered || svc.IsDoorGunMounted) return false;
            if (!WeaponIds.IsPrimary(_weaponId)) return false;
            return !svc.HasWeapon(_weaponId); // the same weapon is topped up by walking over it
        }

        /// <inheritdoc/>
        public void Interact(IPlayerRig player)
        {
            if (!(_weapons.Value is WeaponInventory inventory)) return;
            inventory.TakePickup(this);
        }

        private void OnEnable()
        {
            if (SpawnTime <= 0f) SpawnTime = Time.time;
            WeaponPickups.Register(this);
        }

        private void OnDisable() => WeaponPickups.Unregister(this);
    }
}
