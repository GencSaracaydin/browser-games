namespace LongShadow.Core
{
    /// <summary>
    /// Player health as a service (WP02 PlayerHealth). Read by checkpoint deferral (&lt; 50 HP, GDD §17), HUD feedback
    /// and the dev console.
    /// </summary>
    public interface IPlayerHealth : IDamageable
    {
        /// <summary>Current hit points.</summary>
        float Current { get; }
        /// <summary>Maximum hit points (100, GDD §16).</summary>
        float Max { get; }
        /// <summary>Seconds since the player last took damage.</summary>
        float TimeSinceDamage { get; }
        /// <summary>Dev god mode (no damage).</summary>
        bool GodMode { get; set; }
        /// <summary>Restores full health and the alive state (checkpoint reload).</summary>
        void ResetFull();
    }
}
