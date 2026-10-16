using LongShadow.Core;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// A special behaviour on an enemy (TECH_PLAN §7.6 "Specials": sentry, technical, marksman, RPG, rooftop target,
    /// vehicle dismount, squelch, NVG). The brain calls it every tick before its own logic; returning true from
    /// <see cref="OverrideTick"/> skips the default combat logic for that tick.
    /// </summary>
    public interface IEnemyBehaviour
    {
        /// <summary>Called after the brain was configured for a spawn.</summary>
        void OnConfigured(EnemyBrain brain);
        /// <summary>Per-tick hook; true = the brain does nothing else this tick.</summary>
        bool OverrideTick(EnemyBrain brain, float now, float dt);
        /// <summary>Called once on death.</summary>
        void OnDeath(EnemyBrain brain, in DamageInfo d);
    }
}
