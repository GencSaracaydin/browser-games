namespace LongShadow.Combat.Data
{
    /// <summary>
    /// Handling class of a weapon. Selects the procedural view-model choreography (reload keys, sprint pose, kick feel),
    /// the muzzle-flash size, the casing calibre and the AI defaults (GDD §13).
    /// </summary>
    public enum WeaponKind
    {
        /// <summary>Carbine or assault rifle (COMMANDO, AK, AKMS).</summary>
        Rifle = 0,
        /// <summary>Hand gun (HUSHPUPPY).</summary>
        Pistol = 1,
        /// <summary>Light machine gun with a drum (RPK).</summary>
        Lmg = 2,
        /// <summary>Designated marksman rifle (SVD).</summary>
        Marksman = 3,
        /// <summary>Shoulder-fired rocket launcher (RPG7).</summary>
        Launcher = 4,
        /// <summary>Vehicle heavy machine gun (DSHK).</summary>
        HeavyMg = 5,
        /// <summary>Pintle-mounted machine gun the player can use (M60D door gun, GDD §13.3).</summary>
        MountedMg = 6,
        /// <summary>Thrown explosive (M67).</summary>
        Grenade = 7,
        /// <summary>Melee (KNIFE).</summary>
        Melee = 8,
    }
}
