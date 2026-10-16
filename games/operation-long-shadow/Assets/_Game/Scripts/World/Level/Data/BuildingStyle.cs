namespace LongShadow.World.Level.Data
{
    /// <summary>Architectural style of a generated building (GDD §6.6; Sardahn mudbrick-plaster-sandstone and Art Deco).</summary>
    public enum BuildingStyle
    {
        /// <summary>Rendered mudbrick houses: lime-wash tints, timber lintels, shutters, flat earthen roofs, spouts.</summary>
        Vernacular = 0,
        /// <summary>Colonial-era render with sandstone sills, string courses, round-arched arcades and keystones.</summary>
        Colonial = 1,
        /// <summary>Civic sandstone (clock tower, Weighbridge and market arches).</summary>
        Sandstone = 2,
        /// <summary>1930s Art Deco render (Relay Hall): fins, speed lines, stepped crown, steel windows.</summary>
        ArtDeco = 3,
        /// <summary>Board-marked concrete (gatehouse, compound wall).</summary>
        Concrete = 4,
        /// <summary>Corrugated-iron shed on a low block plinth.</summary>
        Shed = 5,
    }
}
