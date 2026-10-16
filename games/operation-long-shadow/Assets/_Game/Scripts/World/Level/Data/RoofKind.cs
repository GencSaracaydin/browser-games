namespace LongShadow.World.Level.Data
{
    /// <summary>Roof of a generated building.</summary>
    public enum RoofKind
    {
        /// <summary>Flat roof behind a parapet (most of Zaroud).</summary>
        FlatParapet = 0,
        /// <summary>Corrugated mono-pitch roof with eaves (sheds, pump house).</summary>
        Corrugated = 1,
        /// <summary>No roof (open courtyards, ruins).</summary>
        None = 2,
    }
}
