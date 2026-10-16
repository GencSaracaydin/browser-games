using LongShadow.Core;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// A far-proxy block (TECH_PLAN D7, GDD §6.7): an area of a section whose real geometry (buildings, ground, big props)
    /// is replaced by a fully baked proxy in <c>S9_TownFar</c> beyond <see cref="SwapDistance"/> from the player, so the
    /// long baked shadows of distant blocks stay visible even while their section is loaded.
    /// </summary>
    public sealed class ProxyBlockDef
    {
        /// <summary>Block id ("S2.South").</summary>
        public string Id;
        /// <summary>Section that holds the real geometry.</summary>
        public SectionId Section;
        /// <summary>Area of the block (player distance is measured to this rectangle).</summary>
        public AreaRect Rect;
        /// <summary>Hand-over distance in metres (D7: 90).</summary>
        public float SwapDistance = WorldConst.FarProxySwapDistance;
    }
}
