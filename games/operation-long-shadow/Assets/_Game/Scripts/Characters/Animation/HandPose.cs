namespace LongShadow.Characters.Animation
{
    /// <summary>
    /// Finger pose played on the Hands layer (mask AM_Hands) of every WP05 controller (TECH_PLAN §7.8 "Finger poses":
    /// A_Pose_RifleGrip, A_Pose_PistolGrip, A_Pose_Fist, A_Pose_Relaxed, A_Pose_FlatHands). The integer value is the
    /// <see cref="CharacterAnimIds.HandPose"/> animator parameter.
    /// </summary>
    public enum HandPose
    {
        /// <summary>Loose, slightly curled fingers (unarmed, booth, walking without a weapon).</summary>
        Relaxed = 0,
        /// <summary>Right hand wrapped on a pistol grip with the index along the trigger guard; left hand wrapped on a handguard.</summary>
        RifleGrip = 1,
        /// <summary>Two-handed pistol grip (Hush Puppy).</summary>
        PistolGrip = 2,
        /// <summary>Clenched fists (outro cuffs, B5 fist signal).</summary>
        Fist = 3,
        /// <summary>Open flat hands (outro "lie flat").</summary>
        Flat = 4,
    }
}
