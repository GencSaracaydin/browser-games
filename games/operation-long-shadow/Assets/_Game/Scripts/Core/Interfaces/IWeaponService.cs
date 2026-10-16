using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// The player's loadout and weapon state as seen by other systems (WP03 WeaponInventory + PlayerWeapon). Mission
    /// scripts, cinematics, inserts, the booth and the dev console change the loadout only through this interface; the
    /// HUD reads per-frame values from <see cref="IWeaponHudState"/>. Slots: 1 = primary (COMMANDO, AK or RPK),
    /// 2 = HUSHPUPPY (GDD §13.1). Weapon ids are <see cref="WeaponIds"/>. The inventory is also an
    /// <see cref="ISaveable"/> (key "Weapons"), so checkpoint reloads restore it without calls from here.
    /// </summary>
    public interface IWeaponService
    {
        /// <summary>Weapon id in the player's hands (<see cref="WeaponIds.DoorGun"/> while mounted), or null.</summary>
        string CurrentWeaponId { get; }
        /// <summary>Selected slot (1 or 2; 0 while holstered or mounted).</summary>
        int CurrentSlot { get; }
        /// <summary>Weapon id in a slot (1 or 2), or null.</summary>
        string WeaponInSlot(int slot);
        /// <summary>True if the player carries the weapon in any slot.</summary>
        bool HasWeapon(string weaponId);
        /// <summary>Frag grenades carried (3 at start, 4 max, GDD §13.2).</summary>
        int Frags { get; }

        /// <summary>
        /// Lowers and hides the view model while any owner holds the request (cinematics, inserts, booth, ladders,
        /// cabin before the mount, the climax pistol rise). Firing, reloading and switching are blocked while holstered.
        /// </summary>
        void SetHolstered(object owner, bool holstered);
        /// <summary>True while any owner holds a holster request.</summary>
        bool IsHolstered { get; }

        /// <summary>Switches to a slot (1 or 2) with the normal raise/lower timing; ignored while holstered.</summary>
        void SelectSlot(int slot);
        /// <summary>
        /// Puts a weapon into its slot (primary weapons replace slot 1), with <paramref name="reserveRounds"/> in reserve
        /// (-1 = the weapon's full reserve). Used by scripts and the dev console; pickups use the same path.
        /// </summary>
        void Give(string weaponId, int reserveRounds = -1);
        /// <summary>Refills magazines, reserves and frags (resupply crates, GDD §13.2).</summary>
        void Resupply();
        /// <summary>Resets to the mission-start loadout: COMMANDO + HUSHPUPPY, full reserves, 3 frags (GDD §13.1-13.2).</summary>
        void ResetLoadout();

        /// <summary>
        /// Beat 11: mounts the player on the door gun at <paramref name="pivot"/> (the <c>HELI.DoorGunPivot</c> marker,
        /// TECH_PLAN Appendix A). The service holsters the carried weapons, calls <see cref="IPlayerRig.Mount"/>, sets the
        /// look limits and the damage modifiers (x0.25, floor 1 HP) and shows the door-gun overlay (GDD §13.3).
        /// </summary>
        void MountDoorGun(Transform pivot);
        /// <summary>Leaves the door gun and undoes everything <see cref="MountDoorGun"/> set (climax shot 2).</summary>
        void DismountDoorGun();
        /// <summary>True while mounted on the door gun.</summary>
        bool IsDoorGunMounted { get; }
    }
}
