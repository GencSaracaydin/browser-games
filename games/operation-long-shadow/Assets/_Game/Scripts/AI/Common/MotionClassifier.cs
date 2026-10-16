using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>Classifies a target's motion from its stance and velocity.</summary>
    public static class MotionClassifier
    {
        /// <summary>Planar speed (m/s) above which a target counts as moving.</summary>
        public const float WalkThreshold = 0.6f;
        /// <summary>Planar speed (m/s) treated as a sprint when the stance is unknown (player sprint 6.9 m/s, GDD §12).</summary>
        public const float SprintThreshold = 5.6f;

        /// <summary>Motion class from a stance and a world velocity.</summary>
        public static MotionClass Classify(Stance stance, Vector3 velocity)
        {
            if (stance == Stance.Slide) return MotionClass.Slide;
            var planar = new Vector2(velocity.x, velocity.z).magnitude;
            if (stance == Stance.Sprint && planar > WalkThreshold) return MotionClass.Sprint;
            if (planar >= SprintThreshold) return MotionClass.Sprint;
            if (planar >= WalkThreshold) return MotionClass.Walk;
            if (stance == Stance.Airborne || stance == Stance.Mantle || stance == Stance.Ladder) return MotionClass.Walk;
            return MotionClass.Still;
        }
    }
}
