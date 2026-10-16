namespace LongShadow.Characters.Animation
{
    /// <summary>How a body carries its current weapon (selects the Arms-layer hold states and the finger pose).</summary>
    public enum WeaponHold
    {
        /// <summary>No weapon: Arms layer off, relaxed hands.</summary>
        Unarmed = 0,
        /// <summary>Rifle, carbine or LMG: right hand on the pistol grip, left hand on the handguard.</summary>
        Rifle = 1,
        /// <summary>Pistol, two-handed.</summary>
        Pistol = 2,
        /// <summary>Shoulder-fired launcher (RPG-7 pattern).</summary>
        Launcher = 3,
    }
}
