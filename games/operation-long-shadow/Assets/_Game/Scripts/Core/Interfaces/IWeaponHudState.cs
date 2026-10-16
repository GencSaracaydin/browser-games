namespace LongShadow.Core
{
    /// <summary>Polled by the HUD every frame (WP03 current weapon). Avoids per-frame events.</summary>
    public interface IWeaponHudState
    {
        /// <summary>Crosshair spread 0..1.</summary>
        float CrosshairSpread01 { get; }
        /// <summary>Hidden while ADS and sprinting.</summary>
        bool CrosshairVisible { get; }
        /// <summary>Door-gun heat 0..1.</summary>
        float Heat01 { get; }
        /// <summary>True while the door gun is overheated.</summary>
        bool Overheated { get; }
        /// <summary>Grenade cook progress 0..1 (4 s fuse).</summary>
        float CookTime01 { get; }
        /// <summary>HUD weapon name ("COMMANDO").</summary>
        string DisplayName { get; }
        /// <summary>Rounds in the magazine.</summary>
        int Magazine { get; }
        /// <summary>Magazine capacity.</summary>
        int MagazineSize { get; }
        /// <summary>Reserve rounds.</summary>
        int Reserve { get; }
        /// <summary>Frag grenades carried.</summary>
        int Frags { get; }
        /// <summary>Fire mode icon.</summary>
        FireMode FireMode { get; }
        /// <summary>True during a reload.</summary>
        bool IsReloading { get; }
    }
}
