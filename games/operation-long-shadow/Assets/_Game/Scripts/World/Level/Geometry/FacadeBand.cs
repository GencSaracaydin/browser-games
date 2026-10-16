using System.Collections.Generic;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>One storey band of a facade: its height range and the openings composed for it.</summary>
    public sealed class FacadeBand
    {
        /// <summary>Storey index (0 = ground).</summary>
        public int Storey;
        /// <summary>Bottom of the band (above the building base).</summary>
        public float V0;
        /// <summary>Top of the band.</summary>
        public float V1;
        /// <summary>Openings in the band.</summary>
        public readonly List<WallOpening> Openings = new List<WallOpening>();
        /// <summary>Bay index ranges that carry a balcony (first upper storey only).</summary>
        public readonly List<UnityEngine.Vector2> Balconies = new List<UnityEngine.Vector2>();
        /// <summary>True for the ground band of an arcade facade (built by the arcade routine).</summary>
        public bool IsArcade;
    }
}
