using LongShadow.Core;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// An audio/space <see cref="ZoneTag"/> box (Docs/REQUESTS.md WP06 → WP10: weapon tails, NPC footsteps, ambience beds)
    /// or an interior <see cref="EyeAdaptationZone"/> (WP10 Look: G10 relative exposure), as an axis-aligned box.
    /// </summary>
    public sealed class ZoneVolumeDef
    {
        /// <summary>Object name.</summary>
        public string Name;
        /// <summary>Section scene that holds it.</summary>
        public SectionId Section;
        /// <summary>Footprint.</summary>
        public AreaRect Rect;
        /// <summary>Bottom.</summary>
        public float Y0;
        /// <summary>Top.</summary>
        public float Y1;
        /// <summary>Zone type (ZoneTag).</summary>
        public ZoneType Zone;
        /// <summary>Priority (higher wins where zones overlap).</summary>
        public int Priority;
        /// <summary>True for an EyeAdaptationZone instead of a ZoneTag.</summary>
        public bool EyeAdaptation;
        /// <summary>Exposure offset in EV (eye adaptation).</summary>
        public float Ev;
        /// <summary>Volume profile asset name under Settings/Volumes (eye adaptation).</summary>
        public string Profile;
    }
}
