namespace LongShadow.World.Level.Geometry
{
    /// <summary>An opening in a wall measured along the wall from its start point (see <see cref="Interiors"/>).</summary>
    public struct WallGap
    {
        /// <summary>Start along the wall.</summary>
        public float From;
        /// <summary>End along the wall.</summary>
        public float To;
        /// <summary>Bottom (relative to the wall base).</summary>
        public float V0;
        /// <summary>Top (relative to the wall base).</summary>
        public float V1;
        /// <summary>Round-arched.</summary>
        public bool Arched;
        /// <summary>Fill (doors are usually separate animated objects: None).</summary>
        public OpeningFill Fill;

        /// <summary>Creates a gap.</summary>
        public WallGap(float from, float to, float v0, float v1, bool arched = false, OpeningFill fill = OpeningFill.None)
        {
            From = from;
            To = to;
            V0 = v0;
            V1 = v1;
            Arched = arched;
            Fill = fill;
        }
    }
}
