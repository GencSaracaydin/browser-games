namespace LongShadow.Core
{
    /// <summary>Shape of an <see cref="IlluminationZone"/>.</summary>
    public enum IlluminationShape
    {
        /// <summary>Radius around the transform (flare fill, headlights pool).</summary>
        Sphere = 0,
        /// <summary>Cone along forward (flood lamps, flare spot, headlights).</summary>
        Cone = 1,
    }
}
