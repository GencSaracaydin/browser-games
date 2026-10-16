using System;
using LongShadow.Core;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// Every number of one player-usable weapon (GDD §13.1, §13.3). Built once by <see cref="WeaponTable"/> and shared;
    /// treat as immutable. Distances are metres, times seconds, angles degrees.
    /// </summary>
    [Serializable]
    public sealed class WeaponSpec
    {
        /// <summary>Weapon id (<see cref="WeaponIds"/>).</summary>
        public string Id;
        /// <summary>HUD name ("COMMANDO", GDD §11).</summary>
        public string DisplayName;
        /// <summary>Handling class.</summary>
        public WeaponKind Kind;
        /// <summary>Inventory slot: 1 primary, 2 secondary, 0 none (door gun).</summary>
        public int Slot;
        /// <summary>Folder and file stem of the art (Art/Models/Weapons/&lt;ArtName&gt;/SM_WPN_&lt;ArtName&gt;_FP).</summary>
        public string ArtName;

        // ---- Damage (GDD §13.1) ----
        /// <summary>Damage at or below <see cref="FalloffStart"/>.</summary>
        public float DamageClose;
        /// <summary>Damage at or beyond <see cref="FalloffEnd"/>.</summary>
        public float DamageFar;
        /// <summary>Falloff start distance.</summary>
        public float FalloffStart;
        /// <summary>Falloff end distance.</summary>
        public float FalloffEnd;
        /// <summary>Head multiplier override (0 = the hitbox's own, i.e. ×2.0). Hush Puppy ×3.0.</summary>
        public float HeadMultiplierOverride;
        /// <summary>Limb multiplier override (0 = the hitbox's own, i.e. ×0.85).</summary>
        public float LimbMultiplierOverride;
        /// <summary>Hitscan range.</summary>
        public float Range = 500f;

        // ---- Fire ----
        /// <summary>Fire mode (HUD icon and trigger behaviour).</summary>
        public FireMode FireMode;
        /// <summary>Rounds per minute (semi weapons: the cap).</summary>
        public float Rpm;

        // ---- Ammunition ----
        /// <summary>Magazine capacity.</summary>
        public int MagazineSize;
        /// <summary>Maximum reserve.</summary>
        public int MaxReserve;
        /// <summary>Reserve carried by a weapon dropped by an enemy (tuning: two magazines).</summary>
        public int PickupReserve;
        /// <summary>True for the door gun (no magazine, heat instead).</summary>
        public bool InfiniteAmmo;

        // ---- Reloads ----
        /// <summary>Tactical reload (rounds left in the magazine).</summary>
        public ReloadSpec TacticalReload;
        /// <summary>Empty reload (charges the action).</summary>
        public ReloadSpec EmptyReload;

        // ---- Aiming ----
        /// <summary>Hip to ADS blend time.</summary>
        public float AdsTime;
        /// <summary>World-camera vertical FOV while aiming at the default FOV setting (GDD §13: 90° h ≈ 58.7° v).</summary>
        public float AdsFovVertical;
        /// <summary>Spread.</summary>
        public SpreadSpec Spread;
        /// <summary>Recoil.</summary>
        public RecoilSpec Recoil;

        // ---- Handling ----
        /// <summary>Player move-speed multiplier (GDD §13.1 "Move speed").</summary>
        public float MoveMultiplier = 1f;
        /// <summary>Sprint-out-to-fire time (GDD §12: 0.20 s, RPK 0.32 s).</summary>
        public float SprintOutTime = 0.2f;
        /// <summary>Raise (draw) time.</summary>
        public float RaiseTime = 0.45f;
        /// <summary>Lower (put away) time.</summary>
        public float LowerTime = 0.3f;

        // ---- Signature ----
        /// <summary>AI hearing radius of a shot (GDD §13.1 "Noise", §14.2).</summary>
        public float NoiseRadius = 60f;
        /// <summary>Noise kind (suppressed pistol).</summary>
        public NoiseKind NoiseKind = NoiseKind.Gunshot;
        /// <summary>A tracer every N rounds (0 = never; GDD §13.4: MG 1 in 3, rifles 1 in 5).</summary>
        public int TracerEvery;

        // ---- Reference rows used by table tests ----
        /// <summary>GDD §13.1 body hits to kill a Legion rifleman (100 HP) at close range.</summary>
        public int BodyHitsToKillClose;
        /// <summary>GDD §13.1 body hits to kill a Legion rifleman (100 HP) at long range.</summary>
        public int BodyHitsToKillFar;

        /// <summary>View-model presentation.</summary>
        public ViewmodelSpec Viewmodel;

        /// <summary>Seconds between shots at <see cref="Rpm"/>.</summary>
        public float ShotInterval => Rpm > 0f ? 60f / Rpm : 0f;

        /// <summary>Zoom factor of the ADS FOV relative to the default vertical FOV (GDD §13.1: Commando 1.25×).</summary>
        public float AdsZoom => AdsFovVertical > 0f ? WeaponTable.DefaultVerticalFov / AdsFovVertical : 1f;

        /// <summary>True when the weapon can aim down sights.</summary>
        public bool CanAds => AdsTime > 0f && AdsFovVertical > 0f;

        /// <summary>True for automatic fire.</summary>
        public bool IsAutomatic => FireMode == FireMode.Auto;
    }
}
