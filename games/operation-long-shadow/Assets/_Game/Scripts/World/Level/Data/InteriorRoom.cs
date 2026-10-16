using System;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// A small room modelled inside an otherwise solid generated building (spawn closets such as SC_B3_BAL "the room behind
    /// the balcony", SC_B4_E, SC_B6_GATE): its walls, floor and ceiling are built facing inward and the building's
    /// collider volume is split around it. Coordinates are world metres (axis-aligned with the building).
    /// </summary>
    [Serializable]
    public struct InteriorRoom
    {
        /// <summary>Room footprint.</summary>
        public AreaRect Rect;
        /// <summary>Floor height (world).</summary>
        public float FloorY;
        /// <summary>Ceiling height (world).</summary>
        public float CeilingY;
        /// <summary>Tag ("SC_B3_BAL").</summary>
        public string Tag;
    }
}
