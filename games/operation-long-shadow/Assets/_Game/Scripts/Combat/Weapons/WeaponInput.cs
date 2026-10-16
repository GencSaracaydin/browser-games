namespace LongShadow.Combat.Weapons
{
    /// <summary>One frame of input and player state for <see cref="WeaponRuntime.Tick"/> (sampled by PlayerWeapon).</summary>
    public struct WeaponInput
    {
        /// <summary>Fire held.</summary>
        public bool TriggerHeld;
        /// <summary>Fire pressed this frame.</summary>
        public bool TriggerDown;
        /// <summary>Wants to aim down sights (hold or toggle already resolved).</summary>
        public bool AimHeld;
        /// <summary>Reload pressed this frame.</summary>
        public bool ReloadDown;
        /// <summary>The player is sprinting (no fire, no ADS blend, reloads cancel).</summary>
        public bool Sprinting;
        /// <summary>Seconds since sprint ended (GDD §12 sprint-out-to-fire).</summary>
        public float SinceSprintEnd;
        /// <summary>Planar speed relative to walking speed (0 still, 1 walk).</summary>
        public float Move01;
        /// <summary>Standing on ground.</summary>
        public bool Grounded;
        /// <summary>Crouched.</summary>
        public bool Crouched;
        /// <summary>Another action owns the hands (switching, grenade, knife, holstered): no fire, ADS or reload start.</summary>
        public bool Blocked;
    }
}
