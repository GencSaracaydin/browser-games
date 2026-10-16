namespace LongShadow.AI.Common
{
    /// <summary>How fast a target moves, for the hit-chance motion factor (GDD §14.3) and the marksman table (§14.1).</summary>
    public enum MotionClass
    {
        /// <summary>Below <see cref="MotionClassifier.WalkThreshold"/>.</summary>
        Still,
        /// <summary>Walking, crouch-walking, ADS-walking, airborne, climbing.</summary>
        Walk,
        /// <summary>Sprinting.</summary>
        Sprint,
        /// <summary>Sliding.</summary>
        Slide,
    }
}
