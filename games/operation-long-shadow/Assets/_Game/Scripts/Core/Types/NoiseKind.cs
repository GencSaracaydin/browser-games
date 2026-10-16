namespace LongShadow.Core
{
    /// <summary>Source of a <see cref="NoiseEvent"/> heard by AI perception (GDD §14.2).</summary>
    public enum NoiseKind
    {
        Gunshot = 0,
        SuppressedShot = 1,
        Explosion = 2,
        Footstep = 3,
        Voice = 4,
        Impact = 5,
    }
}
