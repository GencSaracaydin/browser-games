using System;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// An explicit opening on a building side (doors the GDD names, closet doorways, the balcony loggia of E3-05): side
    /// 0 S, 1 E, 2 N, 3 W; u along the wall from its left end seen from outside; v from the building base.
    /// </summary>
    [Serializable]
    public struct FeatureOpening
    {
        /// <summary>Side index.</summary>
        public int Side;
        /// <summary>Left edge along the wall.</summary>
        public float U0;
        /// <summary>Right edge.</summary>
        public float U1;
        /// <summary>Bottom.</summary>
        public float V0;
        /// <summary>Top (apex for arches).</summary>
        public float V1;
        /// <summary>Round-arched (true) or rectangular.</summary>
        public bool Arched;
        /// <summary>See-through (a real opening into a modelled space) instead of a blind dark card.</summary>
        public bool Through;
        /// <summary>Fill kind name (Geometry.OpeningFill).</summary>
        public int Fill;
        /// <summary>Tag for builders that attach gameplay to it ("SC_B3_BAL.Door").</summary>
        public string Tag;
    }
}
