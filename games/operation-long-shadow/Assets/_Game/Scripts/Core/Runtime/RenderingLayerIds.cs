namespace LongShadow.Core
{
    /// <summary>
    /// URP rendering layers (light layers, TECH_PLAN §3.8). Index 0 keeps Unity's built-in name "Default" and means
    /// World. Masks are for <c>Light.renderingLayerMask</c> / <c>Renderer.renderingLayerMask</c>.
    /// </summary>
    public static class RenderingLayerIds
    {
        public const int World = 0;
        public const int Booth = 1;
        public const int Cabin = 2;
        public const int Viewmodel = 3;

        public const uint WorldMask = 1u << World;
        public const uint BoothMask = 1u << Booth;
        public const uint CabinMask = 1u << Cabin;
        public const uint ViewmodelMask = 1u << Viewmodel;
        /// <summary>View model receives World and Cabin light (§3.8).</summary>
        public const uint ViewmodelReceives = WorldMask | CabinMask | ViewmodelMask;

        /// <summary>Names written by ProjectSetup (index 0 stays "Default").</summary>
        public static readonly string[] Names = { "Default", "Booth", "Cabin", "Viewmodel" };
    }
}
