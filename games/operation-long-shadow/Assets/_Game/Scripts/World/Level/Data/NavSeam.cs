using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// A NavMeshLink across the seam between two section scenes (TECH_PLAN §7.15: surfaces from different scenes do not
    /// join; the link lives in the section that loads later).
    /// </summary>
    public sealed class NavSeam
    {
        /// <summary>Name ("S1-S2.Weighbridge").</summary>
        public string Name;
        /// <summary>Section that holds the link (the later-loading one).</summary>
        public SectionId Owner;
        /// <summary>The other section.</summary>
        public SectionId Other;
        /// <summary>Link start (on the other section's surface).</summary>
        public Vector3 Start;
        /// <summary>Link end (on the owner's surface).</summary>
        public Vector3 End;
        /// <summary>Link width in metres (0 = a point link).</summary>
        public float Width;
        /// <summary>Bidirectional.</summary>
        public bool Bidirectional = true;
        /// <summary>Starts deactivated (breach hole until the blast).</summary>
        public bool StartsInactive;
        /// <summary>Notes.</summary>
        public string Notes;
    }
}
