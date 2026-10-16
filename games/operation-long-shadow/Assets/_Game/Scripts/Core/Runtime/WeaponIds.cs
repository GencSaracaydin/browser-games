namespace LongShadow.Core
{
    /// <summary>
    /// Weapon ids shared by Combat (WeaponTable, LIB_WeaponVisuals), AI (archetype weapons), Audio (shot cues), the HUD
    /// and <see cref="DamageInfo.WeaponId"/>. Each id is its ASSET_MANIFEST id without the <c>WPN_</c> prefix
    /// (GDD §13: "WPN_COMMANDO" is "COMMANDO").
    /// </summary>
    public static class WeaponIds
    {
        /// <summary>Player primary at start (XM177E2 pattern, GDD §13.1).</summary>
        public const string Commando = "COMMANDO";
        /// <summary>Player secondary, always slot 2 (S&amp;W Mk 22 pattern, suppressed).</summary>
        public const string HushPuppy = "HUSHPUPPY";
        /// <summary>Legion rifleman rifle; player pickup (AKM pattern).</summary>
        public const string AK = "AK";
        /// <summary>Legion gunner LMG; player pickup (75-round drum).</summary>
        public const string RPK = "RPK";
        /// <summary>Greycoat carbine (never drops).</summary>
        public const string AKMS = "AKMS";
        /// <summary>Marksman rifle (SVD pattern, never drops).</summary>
        public const string SVD = "SVD";
        /// <summary>Legion RPG (RPG-7 pattern, never drops).</summary>
        public const string RPG7 = "RPG7";
        /// <summary>Technical's heavy machine gun (DShK pattern).</summary>
        public const string DShK = "DSHK";
        /// <summary>Beat 11 door gun (M60D pattern, GDD §13.3).</summary>
        public const string DoorGun = "M60D";
        /// <summary>Frag grenade (M67 pattern, GDD §13.2).</summary>
        public const string Frag = "M67";
        /// <summary>Knife (V, GDD §13.2).</summary>
        public const string Knife = "KNIFE";
        /// <summary>Smoke grenade (squad scripted throws).</summary>
        public const string Smoke = "SMOKE_GRENADE";

        /// <summary>Weapons the player can hold in slot 1.</summary>
        public static readonly string[] Primaries = { Commando, AK, RPK };

        /// <summary>True for COMMANDO, AK and RPK.</summary>
        public static bool IsPrimary(string id) => id == Commando || id == AK || id == RPK;

        /// <summary>Manifest id of a weapon ("AK" gives "WPN_AK").</summary>
        public static string ManifestId(string weaponId) => string.IsNullOrEmpty(weaponId) ? null : "WPN_" + weaponId;
    }
}
