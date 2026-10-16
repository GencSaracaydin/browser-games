using LongShadow.Core;

namespace LongShadow.Combat
{
    /// <summary>
    /// Audio cue ids used by Combat (ASSET_MANIFEST ids, TECH_PLAN §7.9; shot/impact/tail ids come from
    /// <see cref="AudioCues"/>). The manifest delivers some sounds as one take to be sliced (reload: mag out / mag in /
    /// charge; handling: dry fire / raise / ads in / ads out / switch). Combat asks for the sliced cue ids below first and
    /// falls back to the unsliced cue (or silence) when the AudioService does not know them (requested from WP06 in
    /// Docs/REQUESTS.md). Missing cues are skipped by the AudioService (logged once), so nothing here can break play.
    /// </summary>
    public static class CombatCues
    {
        /// <summary>Reload foley, magazine out ("SFX_WPN_AK_RELOAD_MAGOUT").</summary>
        public static string ReloadMagOut(string weaponId) => AudioCues.WeaponReload(weaponId) + "_MAGOUT";
        /// <summary>Reload foley, magazine seated.</summary>
        public static string ReloadMagIn(string weaponId) => AudioCues.WeaponReload(weaponId) + "_MAGIN";
        /// <summary>Reload foley, charging handle / bolt / slide.</summary>
        public static string ReloadCharge(string weaponId) => AudioCues.WeaponReload(weaponId) + "_CHARGE";

        /// <summary>Unsliced handling take.</summary>
        public const string Handling = "SFX_WPN_HANDLING";
        /// <summary>Dry fire click (empty trigger).</summary>
        public const string DryFire = "SFX_WPN_HANDLING_DRYFIRE";
        /// <summary>Weapon raise.</summary>
        public const string Raise = "SFX_WPN_HANDLING_RAISE";
        /// <summary>Aim in (cloth + cheek weld).</summary>
        public const string AdsIn = "SFX_WPN_HANDLING_ADSIN";
        /// <summary>Aim out.</summary>
        public const string AdsOut = "SFX_WPN_HANDLING_ADSOUT";
        /// <summary>Weapon switch.</summary>
        public const string Switch = "SFX_WPN_HANDLING_SWITCH";
        /// <summary>Ejected brass landing (8 slices = variants of one cue).</summary>
        public const string Shells = "SFX_WPN_SHELLS";
        /// <summary>Pin pull and spoon fly.</summary>
        public const string GrenadePin = "SFX_GREN_PIN_SPOON";
        /// <summary>Grenade bounce (falls back to a quiet surface impact when the cue is missing).</summary>
        public const string GrenadeBounce = "SFX_GREN_BOUNCE";
        /// <summary>Frag explosion.</summary>
        public const string ExplosionFrag = "SFX_EXP_FRAG";
        /// <summary>Rocket explosion.</summary>
        public const string ExplosionRocket = "SFX_EXP_RPG";
        /// <summary>Vehicle fireball.</summary>
        public const string ExplosionFireball = "SFX_EXP_FIREBALL";
        /// <summary>Rocket fly-by (passing within 20 m of the listener).</summary>
        public const string RocketFlyby = "SFX_RPG_FLYBY";
        /// <summary>Knife swing.</summary>
        public const string KnifeSwing = "SFX_KNIFE_SWING";
        /// <summary>Knife hit.</summary>
        public const string KnifeHit = "SFX_KNIFE_HIT";
        /// <summary>Smoke grenade hiss.</summary>
        public const string Smoke = "SFX_SMOKE_GRENADE";
        /// <summary>Spark burst (racks, floods).</summary>
        public const string Sparks = "SFX_SPARKS";
        /// <summary>Floodlight buzzing die-off (GDD §7.6).</summary>
        public const string FloodDieOff = "SFX_FLOOD_DIEOFF";
        /// <summary>Burning vehicle loop.</summary>
        public const string FireLoop = "SFX_FIRE_CRACKLE_LOOP";
        /// <summary>Low-ammo tick (magazine below 25 %, GDD §11).</summary>
        public const string LowAmmo = "SFX_UI_LOW_AMMO";
        /// <summary>Door-gun overheat hiss (not in the manifest; optional).</summary>
        public const string Overheat = "SFX_WPN_M60D_OVERHEAT";
        /// <summary>Weapon pickup / ammo top-up / resupply (manifest SFX_UI_PICKUP).</summary>
        public const string Pickup = "SFX_UI_PICKUP";
        /// <summary>Cooking fuse tick, once per second in the hand (manifest SFX_GREN_FUSE_TICK).</summary>
        public const string FuseTick = "SFX_GREN_FUSE_TICK";
    }
}
