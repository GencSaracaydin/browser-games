using System;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// Bullet spread of a weapon as a cone half-angle in degrees (GDD §13.1 "Spread, hip / ADS"). Hip and ADS values are
    /// GDD numbers; the modifiers (movement, crouch, airborne, bloom) are tuning that only ever widen the hip cone or
    /// narrow it slightly when crouched. Treat instances as immutable (shared by <see cref="WeaponTable"/>).
    /// </summary>
    [Serializable]
    public sealed class SpreadSpec
    {
        /// <summary>Hip-fire cone half-angle in degrees (GDD §13.1).</summary>
        public float Hip;
        /// <summary>Aimed cone half-angle in degrees (GDD §13.1).</summary>
        public float Ads;
        /// <summary>Degrees added to the hip cone at full walking speed (tuning).</summary>
        public float MoveAdd = 1.2f;
        /// <summary>Degrees added to the ADS cone at full ADS walking speed (tuning).</summary>
        public float AdsMoveAdd = 0.25f;
        /// <summary>Multiplier while airborne (tuning).</summary>
        public float AirborneMul = 1.8f;
        /// <summary>Multiplier while crouched and still (tuning).</summary>
        public float CrouchMul = 0.85f;
        /// <summary>Hip bloom added per shot in degrees (tuning).</summary>
        public float BloomPerShot = 0.18f;
        /// <summary>Maximum hip bloom in degrees (tuning).</summary>
        public float BloomMax = 1.4f;
        /// <summary>Bloom decay in degrees per second (tuning).</summary>
        public float BloomDecay = 5f;
        /// <summary>Spread that maps to a fully open crosshair (HUD normalisation).</summary>
        public float CrosshairMax = 7f;
    }
}
