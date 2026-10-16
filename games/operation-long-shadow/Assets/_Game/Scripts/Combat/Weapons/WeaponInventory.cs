using System.Collections.Generic;
using LongShadow.Combat.Data;
using LongShadow.Combat.Interactables;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// The player's loadout as a service (TECH_PLAN §7.5 WeaponInventory; <see cref="IWeaponService"/>, <see cref="ISaveable"/>
    /// key "Weapons"): slot 1 primary (COMMANDO, AK or RPK), slot 2 the HUSH PUPPY, ammunition, frags (3 at start, 4 max),
    /// holster requests, pickups (walk-over top-up, hold-E swap that drops the old primary), resupply, the door-gun mount
    /// and checkpoint capture/restore (frags topped up to at least 2, GDD §17). The per-frame weapon behaviour lives in
    /// <see cref="PlayerWeapon"/> on the same object; this component owns the data and the requests.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Weapons - 5)]
    [DisallowMultipleComponent]
    public sealed class WeaponInventory : MonoBehaviour, IWeaponService, ISaveable
    {
        /// <summary>Checkpoint key (WP00 handoff: "the inventory saves itself as the ISaveable 'Weapons'").</summary>
        public const string Key = "Weapons";

        private readonly LoadoutState _loadout = new LoadoutState();
        private readonly List<object> _holsterOwners = new List<object>(4);
        private PlayerWeapon _player;
        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<ICheckpointService> _checkpoints;
        private ServiceRef<IAudioService> _audio;
        private bool _registered;
        private bool _checkpointRegistered;
        private int _version;

        /// <summary>The loadout data.</summary>
        public LoadoutState Loadout => _loadout;
        /// <summary>Incremented whenever slots or ammunition objects change (PlayerWeapon re-reads them).</summary>
        public int Version => _version;

        private void Awake()
        {
            _loadout.Reset();
            _player = GetComponent<PlayerWeapon>();
            GameServices.Register<IWeaponService>(this);
            _registered = true;
        }

        private void Start() => TryRegisterCheckpoint();

        private void OnDestroy()
        {
            if (_registered) GameServices.Unregister<IWeaponService>(this);
            if (_checkpointRegistered) _checkpoints.Value?.Unregister(this);
        }

        private void Update()
        {
            if (!_checkpointRegistered) TryRegisterCheckpoint();
            var rig = _rig.Value;
            if (rig != null && rig.Root != null && !IsDoorGunMounted) WeaponPickups.TickTopUp(rig.Root.position, this);
        }

        private void TryRegisterCheckpoint()
        {
            var cp = _checkpoints.Value;
            if (cp == null) return;
            cp.Register(this);
            _checkpointRegistered = true;
        }

        // ---- IWeaponService -----------------------------------------------------------------------------------------

        /// <inheritdoc/>
        public string CurrentWeaponId
        {
            get
            {
                if (IsDoorGunMounted) return WeaponIds.DoorGun;
                var equipped = _player != null ? _player.EquippedId : null;
                return equipped ?? _loadout.CurrentId;
            }
        }

        /// <inheritdoc/>
        public int CurrentSlot => IsDoorGunMounted || IsHolstered ? 0 : _loadout.CurrentSlot;

        /// <inheritdoc/>
        public string WeaponInSlot(int slot) => _loadout.InSlot(slot);

        /// <inheritdoc/>
        public bool HasWeapon(string weaponId) => _loadout.Has(weaponId);

        /// <inheritdoc/>
        public int Frags => _loadout.Frags;

        /// <inheritdoc/>
        public void SetHolstered(object owner, bool holstered)
        {
            if (owner == null) return;
            if (holstered)
            {
                if (!_holsterOwners.Contains(owner)) _holsterOwners.Add(owner);
            }
            else _holsterOwners.Remove(owner);
        }

        /// <inheritdoc/>
        public bool IsHolstered
        {
            get
            {
                for (var i = _holsterOwners.Count - 1; i >= 0; i--)
                    if (_holsterOwners[i] == null || (_holsterOwners[i] is Object o && o == null)) _holsterOwners.RemoveAt(i);
                return _holsterOwners.Count > 0;
            }
        }

        /// <inheritdoc/>
        public void SelectSlot(int slot)
        {
            if (_loadout.InSlot(slot) == null) return;
            if (_player != null) _player.RequestSlot(slot);
            else _loadout.Select(slot);
        }

        /// <inheritdoc/>
        public void Give(string weaponId, int reserveRounds = -1)
        {
            if (weaponId == WeaponIds.Frag)
            {
                // Scripts and the dev console give frags the same way (-1 = up to the maximum of 4).
                _loadout.AddFrags(reserveRounds < 0 ? WeaponTable.Frag.MaxCount : reserveRounds);
                Changed();
                return;
            }
            var w = WeaponTable.Get(weaponId);
            if (w == null || w.Slot < 1)
            {
                LSLog.Warn(LogCategory.Combat, "IWeaponService.Give: unknown or unslotted weapon '" + weaponId + "'.");
                return;
            }
            if (_loadout.Has(weaponId))
            {
                var a = _loadout.AmmoOf(weaponId);
                if (reserveRounds < 0) a?.Fill();
                else a?.AddReserve(reserveRounds);
            }
            else _loadout.Give(weaponId, reserveRounds);
            Changed();
        }

        /// <inheritdoc/>
        public void Resupply()
        {
            _loadout.Resupply(); // GDD §13.2: crates refill ammo and frags
            Changed();
        }

        /// <inheritdoc/>
        public void ResetLoadout()
        {
            _loadout.Reset();
            Changed();
            _player?.EquipImmediate();
        }

        /// <inheritdoc/>
        public void MountDoorGun(Transform pivot)
        {
            if (_player != null) _player.MountDoorGun(pivot);
            else LSLog.Warn(LogCategory.Combat, "MountDoorGun: no PlayerWeapon on the inventory object.", this);
        }

        /// <inheritdoc/>
        public void DismountDoorGun() => _player?.DismountDoorGun();

        /// <inheritdoc/>
        public bool IsDoorGunMounted => _player != null && _player.IsMounted;

        // ---- pickups and ammo ---------------------------------------------------------------------------------------

        /// <summary>Rounds the reserve of <paramref name="weaponId"/> can still take.</summary>
        public int ReserveSpace(string weaponId)
        {
            var a = _loadout.AmmoOf(weaponId);
            return a == null ? 0 : a.MaxReserve - a.Reserve;
        }

        /// <summary>Adds rounds to a carried weapon's reserve; returns rounds taken.</summary>
        public int AddReserve(string weaponId, int rounds)
        {
            var taken = _loadout.TopUpReserve(weaponId, rounds);
            if (taken > 0) Changed();
            return taken;
        }

        /// <summary>
        /// HOLD [E] Take (GDD §13.1): swaps the pickup into slot 1, drops the old primary where the pickup lay, and draws it.
        /// </summary>
        public void TakePickup(WeaponPickup pickup)
        {
            if (pickup == null || !WeaponIds.IsPrimary(pickup.WeaponId)) return;
            var at = pickup.transform.position;
            var old = _loadout.SwapPrimary(pickup.WeaponId, pickup.Magazine, pickup.Reserve);
            Destroy(pickup.gameObject);
            if (old.IsValid) WeaponPickups.Spawn(old.Id, at, old.Magazine, old.Reserve);
            Changed();
            _audio.Value?.Play2D(CombatCues.Pickup);
            if (_player != null) _player.RequestSlot(LoadoutState.PrimarySlot, true);
            else _loadout.Select(LoadoutState.PrimarySlot);
        }

        /// <summary>Takes one frag for a throw; false when none are left.</summary>
        public bool TryUseFrag()
        {
            if (!_loadout.TryUseFrag()) return false;
            _version++;
            return true;
        }

        /// <summary>Selects a slot in the data only (PlayerWeapon calls this when a switch completes).</summary>
        public bool CommitSlot(int slot) => _loadout.Select(slot);

        private void Changed()
        {
            _version++;
            _player?.OnLoadoutChanged();
        }

        // ---- ISaveable ----------------------------------------------------------------------------------------------

        /// <inheritdoc/>
        public string SaveKey => Key;

        /// <inheritdoc/>
        public void Capture(SaveWriter w) => _loadout.Capture(w);

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            _loadout.Restore(r, WeaponTable.Frag.CheckpointMinimum); // GDD §17: frags topped up to at least 2
            _version++;
            _player?.EquipImmediate();
        }
    }
}
