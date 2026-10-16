using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Anything that takes damage. Implemented by <see cref="Health"/> (Core) and PlayerHealth (WP02).</summary>
    public interface IDamageable
    {
        /// <summary>Team of the owner.</summary>
        Team Team { get; }
        /// <summary>False once dead.</summary>
        bool IsAlive { get; }
        /// <summary>Point AI aims at (chest); never null for live actors.</summary>
        Transform AimPoint { get; }
        /// <summary>Applies damage. <see cref="DamageInfo.Amount"/> is final (see DamageInfo remarks).</summary>
        void ApplyDamage(in DamageInfo d);
    }
}
