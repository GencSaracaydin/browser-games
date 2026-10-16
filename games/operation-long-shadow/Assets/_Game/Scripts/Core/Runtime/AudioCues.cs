namespace LongShadow.Core
{
    /// <summary>
    /// Audio cue ids for <see cref="IAudioService"/> and <see cref="IMusicService"/>. A cue id is the ASSET_MANIFEST id of
    /// the sound (<c>SFX_*</c>, <c>AMB_*</c>, <c>MUS_*</c>, <c>STG_*</c>); WP06 builds LIB_Audio from the manifest. These
    /// helpers fix the derived ids that several packages compute (footsteps WP02, impacts WP03, shots WP03/WP04), so
    /// callers never build cue strings by hand. A cue that has not landed yet is skipped by the AudioService (logged once).
    /// </summary>
    public static class AudioCues
    {
        /// <summary>Footstep set for a surface (TECH_PLAN §7.3 FootstepEmitter table).</summary>
        public static string Footstep(SurfaceType s)
        {
            switch (s)
            {
                case SurfaceType.Gravel: return "SFX_FS_GRAVEL";
                case SurfaceType.Stone:
                case SurfaceType.Concrete:
                case SurfaceType.Fruit: return "SFX_FS_STONE";
                case SurfaceType.Plaster: return "SFX_FS_ROOF";
                case SurfaceType.Tile:
                case SurfaceType.Glass: return "SFX_FS_TILE";
                case SurfaceType.Wood: return "SFX_FS_WOOD";
                case SurfaceType.Metal: return "SFX_FS_METALSTAIR";
                case SurfaceType.SaltCrust: return "SFX_FS_SALTCRUST";
                default: return FootstepFallback; // Default, Sand, Fabric, Flesh
            }
        }

        /// <summary>Footstep set used when a set is missing (SALTCRUST is P1, GDD §12).</summary>
        public const string FootstepFallback = "SFX_FS_SAND";

        /// <summary>Bullet impact cue for a surface (SFX_IMPACT_*).</summary>
        public static string Impact(SurfaceType s)
        {
            switch (s)
            {
                case SurfaceType.Stone:
                case SurfaceType.Concrete:
                case SurfaceType.Tile: return "SFX_IMPACT_STONE";
                case SurfaceType.Plaster: return "SFX_IMPACT_PLASTER";
                case SurfaceType.Wood: return "SFX_IMPACT_WOOD";
                case SurfaceType.Metal: return "SFX_IMPACT_METAL";
                case SurfaceType.Fabric: return "SFX_IMPACT_FABRIC";
                case SurfaceType.Flesh: return "SFX_IMPACT_FLESH";
                case SurfaceType.Glass: return "SFX_IMPACT_GLASS";
                case SurfaceType.Fruit: return "SFX_IMPACT_FRUIT";
                default: return "SFX_IMPACT_SAND"; // Default, Sand, Gravel, SaltCrust
            }
        }

        /// <summary>Shot transient of a weapon ("SFX_WPN_AK_SHOT"; the RPG launch is "SFX_RPG_LAUNCH").</summary>
        public static string WeaponShot(string weaponId) =>
            weaponId == WeaponIds.RPG7 ? "SFX_RPG_LAUNCH" : "SFX_WPN_" + weaponId + "_SHOT";
        /// <summary>Mechanical layer of a weapon ("SFX_WPN_AK_MECH"; player weapons and the door gun only).</summary>
        public static string WeaponMech(string weaponId) => "SFX_WPN_" + weaponId + "_MECH";
        /// <summary>Reload foley of a weapon ("SFX_WPN_AK_RELOAD").</summary>
        public static string WeaponReload(string weaponId) => "SFX_WPN_" + weaponId + "_RELOAD";

        /// <summary>Weapon tail for an acoustic zone (GDD §10.2).</summary>
        public static string WeaponTail(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Street:
                case ZoneType.Roof: return "SFX_WPN_TAIL_STREET";
                case ZoneType.Square: return "SFX_WPN_TAIL_SQUARE";
                case ZoneType.Interior:
                case ZoneType.Basement:
                case ZoneType.Cabin: return "SFX_WPN_TAIL_INTERIOR";
                default: return "SFX_WPN_TAIL_DESERT";
            }
        }

        /// <summary>Supersonic crack of a near miss.</summary>
        public const string BulletCrack = "SFX_BULLET_CRACK";
        /// <summary>Whizz of a near miss.</summary>
        public const string BulletWhiz = "SFX_BULLET_WHIZ";
        /// <summary>Hit marker tick.</summary>
        public const string UiHitMarker = "SFX_UI_HITMARKER";
        /// <summary>Headshot tick.</summary>
        public const string UiHeadshot = "SFX_UI_HEADSHOT";
        /// <summary>Kill confirm.</summary>
        public const string UiKill = "SFX_UI_KILL";
    }
}
