namespace LongShadow.Cinematics.Booth
{
    /// <summary>What a booth CRT shows (TECH_PLAN §7.12 CRTChannelClip: static / still / live RT / video / clock).</summary>
    public enum CrtContent
    {
        /// <summary>Tube off: dark green-grey glass.</summary>
        Off,
        /// <summary>Snow.</summary>
        Static,
        /// <summary>A still image (map, recon photo, roster page); optional scroll (teletype) and zoom.</summary>
        Still,
        /// <summary>A live feed camera (ridge, deck, clock, film).</summary>
        Feed,
        /// <summary>A video from StreamingAssets/video (Harrou 8 mm, gun camera, film leader).</summary>
        Video,
        /// <summary>A slideshow of stills (team ID photos).</summary>
        Slides,
    }
}
