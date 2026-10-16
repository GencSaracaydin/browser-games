using System;

namespace LongShadow.Combat.Data
{
    /// <summary>The knife (V, GDD §13.2). Treat as immutable.</summary>
    [Serializable]
    public sealed class MeleeSpec
    {
        /// <summary>Weapon id.</summary>
        public string Id;
        /// <summary>Damage (flat: melee ignores zone multipliers and armour, so it kills every archetype).</summary>
        public float Damage;
        /// <summary>Reach from the eye.</summary>
        public float Reach;
        /// <summary>Extra reach when a live target is ahead (the lunge).</summary>
        public float Lunge;
        /// <summary>Whole action time.</summary>
        public float Duration;
        /// <summary>Time of the hit test from the press.</summary>
        public float HitTime = 0.17f;
        /// <summary>Half-angle of the lunge acquisition cone (degrees, tuning).</summary>
        public float LungeConeDegrees = 22f;
        /// <summary>Radius of the swing sweep (metres, tuning).</summary>
        public float SweepRadius = 0.28f;
    }
}
