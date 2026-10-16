using LongShadow.Combat.Data;
using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>Magazine and reserve of one carried weapon (GDD §13.1 "Magazine / max reserve").</summary>
    public sealed class WeaponAmmo
    {
        /// <summary>Weapon id.</summary>
        public readonly string WeaponId;
        /// <summary>Magazine capacity.</summary>
        public readonly int MagazineSize;
        /// <summary>Reserve capacity.</summary>
        public readonly int MaxReserve;
        /// <summary>Rounds in the magazine.</summary>
        public int Magazine { get; private set; }
        /// <summary>Rounds in reserve.</summary>
        public int Reserve { get; private set; }

        /// <summary>Creates ammo for a weapon with the given counts (clamped).</summary>
        public WeaponAmmo(string weaponId, int magazineSize, int maxReserve, int magazine, int reserve)
        {
            WeaponId = weaponId;
            MagazineSize = Mathf.Max(0, magazineSize);
            MaxReserve = Mathf.Max(0, maxReserve);
            Set(magazine, reserve);
        }

        /// <summary>Full ammo for a weapon spec (mission start, crates).</summary>
        public static WeaponAmmo Full(WeaponSpec w) => new WeaponAmmo(w.Id, w.MagazineSize, w.MaxReserve, w.MagazineSize, w.MaxReserve);

        /// <summary>True when the magazine is empty.</summary>
        public bool IsEmpty => Magazine <= 0;
        /// <summary>True when a reload would add rounds.</summary>
        public bool CanReload => Magazine < MagazineSize && Reserve > 0;
        /// <summary>Magazine fill 0..1.</summary>
        public float Magazine01 => MagazineSize > 0 ? (float)Magazine / MagazineSize : 0f;

        /// <summary>Sets both counts (clamped to capacity).</summary>
        public void Set(int magazine, int reserve)
        {
            Magazine = Mathf.Clamp(magazine, 0, MagazineSize);
            Reserve = Mathf.Clamp(reserve, 0, MaxReserve);
        }

        /// <summary>Removes one round from the magazine; false when empty.</summary>
        public bool TryConsume()
        {
            if (Magazine <= 0) return false;
            Magazine--;
            return true;
        }

        /// <summary>Moves rounds from the reserve into the magazine (the moment the new magazine seats); returns rounds moved.</summary>
        public int CommitReload()
        {
            var take = Mathf.Min(MagazineSize - Magazine, Reserve);
            if (take <= 0) return 0;
            Magazine += take;
            Reserve -= take;
            return take;
        }

        /// <summary>Adds up to <paramref name="rounds"/> to the reserve; returns how many were taken.</summary>
        public int AddReserve(int rounds)
        {
            if (rounds <= 0) return 0;
            var take = Mathf.Min(rounds, MaxReserve - Reserve);
            Reserve += take;
            return take;
        }

        /// <summary>Full magazine and reserve (resupply crate).</summary>
        public void Fill() => Set(MagazineSize, MaxReserve);
    }
}
