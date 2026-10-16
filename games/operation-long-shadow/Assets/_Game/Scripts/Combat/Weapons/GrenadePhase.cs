namespace LongShadow.Combat.Weapons
{
    /// <summary>Phases of a player frag throw (GDD §13.2: hold G to cook, release to throw).</summary>
    public enum GrenadePhase
    {
        /// <summary>No grenade in hand.</summary>
        None = 0,
        /// <summary>Hand to the vest and pin pull (the fuse already burns: "4.0 s fuse from the pin pull").</summary>
        PinPull = 1,
        /// <summary>Held back, cooking.</summary>
        Cooking = 2,
        /// <summary>Throwing arc; the grenade leaves the hand at the end.</summary>
        Release = 3,
        /// <summary>Hand back to the weapon.</summary>
        Recover = 4,
    }
}
