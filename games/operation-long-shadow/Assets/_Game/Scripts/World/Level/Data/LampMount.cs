namespace LongShadow.World.Level.Data
{
    /// <summary>How a sodium street lamp is mounted (GDD §8.5, manifest PROC_LAMP_POSTS).</summary>
    public enum LampMount
    {
        /// <summary>Cast-iron bracket arm on a facade, head about 6 m up.</summary>
        WallBracket = 0,
        /// <summary>Free-standing steel post with an outreach arm, head about 6.5 m up.</summary>
        Post = 1,
    }
}
