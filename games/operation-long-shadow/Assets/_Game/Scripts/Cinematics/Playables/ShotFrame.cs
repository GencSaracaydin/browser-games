namespace LongShadow.Cinematics.Playables
{
    /// <summary>Coordinate frame of a <see cref="ShotPose"/>.</summary>
    public enum ShotFrame
    {
        /// <summary>World space.</summary>
        World,
        /// <summary>Booth local frame (GDD §8.0; BoothRoot).</summary>
        Booth,
        /// <summary>Local space of a bound object (cast member, heli cabin, marker): position and rotation follow it.</summary>
        Target,
        /// <summary>World-axis offset from a bound object's position (tracking shots that should not roll with it).</summary>
        TargetOffset,
        /// <summary>The player's eye (IPlayerRig.Eye): the hand-off pose of intro shot 9 and the climax.</summary>
        PlayerEye,
    }
}
