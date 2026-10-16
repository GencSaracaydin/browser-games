namespace LongShadow.World.Level.Geometry
{
    /// <summary>Outline of a wall opening (GDD §6.6: colonial round arches, never pointed).</summary>
    public enum OpeningShape
    {
        /// <summary>Rectangular opening under a lintel.</summary>
        Rect = 0,
        /// <summary>Semicircular (round) arch: the springline sits half the width below the apex.</summary>
        RoundArch = 1,
        /// <summary>Segmental (shallow) arch, rise = a quarter of the width.</summary>
        SegmentalArch = 2,
    }
}
