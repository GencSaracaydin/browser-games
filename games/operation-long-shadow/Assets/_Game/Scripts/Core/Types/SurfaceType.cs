namespace LongShadow.Core
{
    /// <summary>Material class of a surface; drives impacts, decals and footsteps (TECH_PLAN §7.3, §7.5).</summary>
    public enum SurfaceType
    {
        /// <summary>Unknown; footsteps fall back to SAND.</summary>
        Default = 0,
        Sand = 1,
        Gravel = 2,
        Stone = 3,
        Plaster = 4,
        Concrete = 5,
        Tile = 6,
        Wood = 7,
        Metal = 8,
        SaltCrust = 9,
        Fabric = 10,
        Glass = 11,
        Flesh = 12,
        Fruit = 13,
    }
}
