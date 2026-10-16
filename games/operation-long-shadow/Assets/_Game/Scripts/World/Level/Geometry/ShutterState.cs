namespace LongShadow.World.Level.Geometry
{
    /// <summary>State of a pair of window shutters (about half are shut for the curfew, GDD §2.2).</summary>
    public enum ShutterState
    {
        /// <summary>Both leaves closed.</summary>
        Closed = 0,
        /// <summary>One leaf ajar.</summary>
        Ajar = 1,
        /// <summary>Folded back against the facade.</summary>
        Open = 2,
    }
}
