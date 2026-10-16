namespace LongShadow.AI.Common
{
    /// <summary>Everything the GDD §14.3 hit-chance formula needs for one shot (see <see cref="HitChance.Compute"/>).</summary>
    public struct HitChanceInputs
    {
        /// <summary>Archetype accuracy at 20 m (GDD §14.1), or the marksman's motion-table value.</summary>
        public float BaseAccuracy;
        /// <summary>Shooter-to-target distance in metres.</summary>
        public float Distance;
        /// <summary>Upper end of the archetype's range band (extra falloff beyond it).</summary>
        public float RangeMax;
        /// <summary>True for scoped shooters whose accuracy does not fall with range (marksman).</summary>
        public bool FlatRange;
        /// <summary>Target motion (still 1.0, walk 0.8, sprint 0.6, slide 0.5).</summary>
        public MotionClass TargetMotion;
        /// <summary>False when <see cref="BaseAccuracy"/> already depends on motion (marksman table).</summary>
        public bool ApplyMotionFactor;
        /// <summary>Target is in cover from the shooter (×0.6).</summary>
        public bool TargetInCover;
        /// <summary>Seconds of continuous line of sight on this target (ramp 0.5 → 1.0 over 2 s).</summary>
        public float TimeOnTarget;
        /// <summary>Target stands in an active flood cone (×1.25, Beat 6).</summary>
        public bool TargetInFlood;
        /// <summary>Difficulty accuracy multiplier (GDD §16: 0.7 / 1.0 / 1.15 / 1.3).</summary>
        public float DifficultyMultiplier;
        /// <summary>The shooter is suppressed by squad fire (×0.5 for 2 s).</summary>
        public bool ShooterSuppressed;
    }
}
