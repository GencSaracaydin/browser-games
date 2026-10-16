using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// A marker that Appendix A names without coordinates (or that WP10 adds for other packages), with the placement WP10
    /// chose for it. See <see cref="DerivedMarkers"/>.
    /// </summary>
    public sealed class DerivedMarker
    {
        /// <summary>Marker id (may repeat for anonymous groups such as cover nodes; then <see cref="Group"/> is set).</summary>
        public string Id;
        /// <summary>Section scene.</summary>
        public SectionId Section;
        /// <summary>Core component name ("SceneMarker", "OutOfBoundsVolume", "CoverNode").</summary>
        public string Component;
        /// <summary>Anchor position (world metres; trigger centre for boxes).</summary>
        public Vector3 Position;
        /// <summary>Compass heading of the forward axis.</summary>
        public float Heading;
        /// <summary>Downward pitch in degrees (beauty-shot cameras).</summary>
        public float Pitch;
        /// <summary>Box size for trigger boxes (zero = none).</summary>
        public Vector3 BoxSize;
        /// <summary>Group id for anonymous nodes ("B2.AlertCover").</summary>
        public string Group;
        /// <summary>Full-height cover (cover nodes).</summary>
        public bool Standing;
        /// <summary>Why it sits there.</summary>
        public string Notes;
    }
}
