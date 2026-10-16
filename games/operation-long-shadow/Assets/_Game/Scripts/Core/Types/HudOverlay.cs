namespace LongShadow.Core
{
    /// <summary>Full-screen HUD overlay (GDD §11 "Special overlays").</summary>
    public enum HudOverlay
    {
        None = 0,
        /// <summary>Beat 1 binocular reticle.</summary>
        Binoculars = 1,
        /// <summary>Beat 11 door-gun ring sight and heat bar.</summary>
        DoorGun = 2,
    }
}
