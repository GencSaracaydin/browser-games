using System.Collections.Generic;
using LongShadow.Combat.Data;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// The player's carried weapons, ammunition and frags as plain data (GDD §13.1-13.2, §17). Slot 1 is the primary
    /// (COMMANDO, AK or RPK), slot 2 is always the HUSH PUPPY. No Unity objects: <see cref="WeaponInventory"/> drives it and
    /// the tests exercise it directly.
    /// </summary>
    public sealed class LoadoutState
    {
        /// <summary>Primary slot number.</summary>
        public const int PrimarySlot = 1;
        /// <summary>Secondary slot number.</summary>
        public const int SecondarySlot = 2;

        private readonly string[] _slots = new string[3];
        private readonly Dictionary<string, WeaponAmmo> _ammo = new Dictionary<string, WeaponAmmo>(4);

        /// <summary>Frag grenades carried.</summary>
        public int Frags { get; private set; }
        /// <summary>Selected slot (1 or 2; 0 = none).</summary>
        public int CurrentSlot { get; private set; }

        /// <summary>Weapon id in a slot, or null.</summary>
        public string InSlot(int slot) => slot >= 1 && slot <= 2 ? _slots[slot] : null;

        /// <summary>Id of the selected weapon, or null.</summary>
        public string CurrentId => InSlot(CurrentSlot);

        /// <summary>Ammo of a carried weapon, or null.</summary>
        public WeaponAmmo AmmoOf(string weaponId) => weaponId != null && _ammo.TryGetValue(weaponId, out var a) ? a : null;

        /// <summary>Ammo of the selected weapon, or null.</summary>
        public WeaponAmmo CurrentAmmo => AmmoOf(CurrentId);

        /// <summary>True if the weapon is in a slot.</summary>
        public bool Has(string weaponId) => weaponId != null && (_slots[1] == weaponId || _slots[2] == weaponId);

        /// <summary>Mission-start loadout: COMMANDO + HUSH PUPPY with full reserves, 3 frags, primary drawn (GDD §13.1-13.2).</summary>
        public void Reset()
        {
            _slots[1] = _slots[2] = null;
            _ammo.Clear();
            Give(WeaponIds.Commando);
            Give(WeaponIds.HushPuppy);
            Frags = WeaponTable.Frag.StartCount;
            CurrentSlot = PrimarySlot;
        }

        /// <summary>
        /// Puts a weapon in its slot (a primary replaces slot 1 and its ammo), with <paramref name="magazine"/> and
        /// <paramref name="reserve"/> rounds (-1 = full). Returns false for unknown ids and the door gun.
        /// </summary>
        public bool Give(string weaponId, int reserve = -1, int magazine = -1)
        {
            var w = WeaponTable.Get(weaponId);
            if (w == null || w.Slot < 1 || w.Slot > 2) return false;
            var old = _slots[w.Slot];
            if (old != null && old != weaponId) _ammo.Remove(old);
            _slots[w.Slot] = weaponId;
            var mag = magazine < 0 ? w.MagazineSize : magazine;
            var res = reserve < 0 ? w.MaxReserve : reserve;
            if (_ammo.TryGetValue(weaponId, out var a)) a.Set(mag, res);
            else _ammo[weaponId] = new WeaponAmmo(weaponId, w.MagazineSize, w.MaxReserve, mag, res);
            if (CurrentSlot == 0) CurrentSlot = w.Slot;
            return true;
        }

        /// <summary>
        /// Swaps a picked-up primary into slot 1 (GDD §13.1: "HOLD [E] Take &lt;weapon&gt; swaps it into slot 1 and leaves your
        /// old primary on the ground"); returns the old primary with its ammo.
        /// </summary>
        public DroppedWeapon SwapPrimary(string weaponId, int magazine, int reserve)
        {
            var dropped = default(DroppedWeapon);
            var old = _slots[PrimarySlot];
            if (old != null && old != weaponId)
            {
                var a = AmmoOf(old);
                dropped = new DroppedWeapon { Id = old, Magazine = a?.Magazine ?? 0, Reserve = a?.Reserve ?? 0 };
            }
            Give(weaponId, reserve, magazine);
            return dropped;
        }

        /// <summary>Adds rounds to a carried weapon's reserve (walking over a pickup of the same weapon); returns rounds taken.</summary>
        public int TopUpReserve(string weaponId, int rounds)
        {
            var a = AmmoOf(weaponId);
            return a == null ? 0 : a.AddReserve(rounds);
        }

        /// <summary>Refills every carried weapon and the frags (resupply crates, GDD §13.2).</summary>
        public void Resupply()
        {
            foreach (var kv in _ammo) kv.Value.Fill();
            Frags = WeaponTable.Frag.MaxCount;
        }

        /// <summary>Selects a slot that holds a weapon; false otherwise.</summary>
        public bool Select(int slot)
        {
            if (InSlot(slot) == null) return false;
            CurrentSlot = slot;
            return true;
        }

        /// <summary>The other occupied slot (for 1/2 toggling and the wheel), or the current one.</summary>
        public int OtherSlot()
        {
            var other = CurrentSlot == PrimarySlot ? SecondarySlot : PrimarySlot;
            return InSlot(other) != null ? other : CurrentSlot;
        }

        /// <summary>Uses one frag; false when none are left.</summary>
        public bool TryUseFrag()
        {
            if (Frags <= 0) return false;
            Frags--;
            return true;
        }

        /// <summary>Adds frags up to the maximum; returns how many were added.</summary>
        public int AddFrags(int count)
        {
            var add = Mathf.Clamp(count, 0, WeaponTable.Frag.MaxCount - Frags);
            Frags += add;
            return add;
        }

        /// <summary>Sets frags directly (clamped; dev console and restore).</summary>
        public void SetFrags(int count) => Frags = Mathf.Clamp(count, 0, WeaponTable.Frag.MaxCount);

        /// <summary>Writes the loadout (checkpoint snapshot "Weapons").</summary>
        public void Capture(SaveWriter w)
        {
            w.Write("slot1", _slots[1] ?? string.Empty);
            w.Write("slot2", _slots[2] ?? string.Empty);
            w.Write("current", CurrentSlot);
            w.Write("frags", Frags);
            for (var s = 1; s <= 2; s++)
            {
                var a = AmmoOf(_slots[s]);
                w.Write("mag" + s, a?.Magazine ?? 0);
                w.Write("res" + s, a?.Reserve ?? 0);
            }
        }

        /// <summary>
        /// Restores a captured loadout; frags are topped up to at least <paramref name="minFrags"/> (GDD §17). A snapshot
        /// without data resets to the mission-start loadout.
        /// </summary>
        public void Restore(SaveReader r, int minFrags)
        {
            if (!r.Has("slot1") && !r.Has("slot2"))
            {
                Reset();
                return;
            }
            _slots[1] = _slots[2] = null;
            _ammo.Clear();
            CurrentSlot = 0;
            for (var s = 1; s <= 2; s++)
            {
                var id = r.ReadString("slot" + s, null);
                if (string.IsNullOrEmpty(id)) continue;
                Give(id, r.ReadInt("res" + s, -1), r.ReadInt("mag" + s, -1));
            }
            if (_slots[2] == null) Give(WeaponIds.HushPuppy);
            var current = r.ReadInt("current", PrimarySlot);
            if (!Select(current)) Select(_slots[PrimarySlot] != null ? PrimarySlot : SecondarySlot);
            SetFrags(Mathf.Max(r.ReadInt("frags", WeaponTable.Frag.StartCount), minFrags));
        }
    }
}
