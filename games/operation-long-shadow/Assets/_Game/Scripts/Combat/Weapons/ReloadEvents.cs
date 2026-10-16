using System;

namespace LongShadow.Combat.Weapons
{
    /// <summary>Reload keys crossed during one tick (several can fire in one long frame).</summary>
    [Flags]
    public enum ReloadEvents
    {
        /// <summary>Nothing happened.</summary>
        None = 0,
        /// <summary>The reload began.</summary>
        Started = 1 << 0,
        /// <summary>The weapon finished tilting in.</summary>
        TiltDone = 1 << 1,
        /// <summary>The old magazine left the well.</summary>
        MagOut = 1 << 2,
        /// <summary>The new magazine is in hand.</summary>
        MagGrab = 1 << 3,
        /// <summary>The new magazine seated: ammunition committed.</summary>
        MagIn = 1 << 4,
        /// <summary>The charge started.</summary>
        ChargeStart = 1 << 5,
        /// <summary>The bolt went home.</summary>
        ChargeEnd = 1 << 6,
        /// <summary>The reload completed.</summary>
        Finished = 1 << 7,
        /// <summary>The reload was cancelled (sprint, switch, melee, grenade).</summary>
        Cancelled = 1 << 8,
    }
}
