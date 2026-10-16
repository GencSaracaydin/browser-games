namespace LongShadow.Characters.Animation
{
    /// <summary>
    /// Procedural IK hand gestures solved by <c>BodyIK</c> (TECH_PLAN §7.7 squad scripted actions, CINEMATIC_DIRECTION §2
    /// "IK pose" fallbacks). They play on top of whatever clip the action state has, so they also cover actions whose
    /// clip has not been produced yet (ANIM_HYMOTION_SIGNALS is P1).
    /// </summary>
    public enum Gesture
    {
        /// <summary>No gesture.</summary>
        None = 0,
        /// <summary>Support hand raised in a fist at shoulder height (B5 hold, MID-A).</summary>
        FistHold = 1,
        /// <summary>Support hand "move up" signal: raise beside the face, then sweep forward.</summary>
        HandSignal = 2,
        /// <summary>Support hand holds the PRC-77 handset to the left ear (Tinker, insertion I-6).</summary>
        HandsetToEar = 3,
        /// <summary>Both hands in front of the face holding a camera (Sparrow, B1/B8, insertion I-5).</summary>
        RaiseCamera = 4,
        /// <summary>Both hands in front of the chest bending a chemlight (B6).</summary>
        CrackChemlight = 5,
        /// <summary>Right hand reaches forward to a scripted target (booth toggle, phone).</summary>
        Reach = 6,
    }
}
