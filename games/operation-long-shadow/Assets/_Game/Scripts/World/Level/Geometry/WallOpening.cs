using System;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// An opening in a wall band, in the wall's own frame: u along the wall from its left end (seen from outside),
    /// v up from the building base. For arches <see cref="V1"/> is the apex.
    /// </summary>
    [Serializable]
    public struct WallOpening
    {
        /// <summary>Left edge (metres along the wall).</summary>
        public float U0;
        /// <summary>Right edge.</summary>
        public float U1;
        /// <summary>Bottom (sill or threshold).</summary>
        public float V0;
        /// <summary>Top (lintel soffit or arch apex).</summary>
        public float V1;
        /// <summary>Outline.</summary>
        public OpeningShape Shape;
        /// <summary>Reveal depth (how far the jambs go into the wall).</summary>
        public float Depth;
        /// <summary>True: a dark card closes the opening at the reveal depth (non-enterable interiors).</summary>
        public bool Blind;
        /// <summary>True: no sill quad (doors, arcade arches reaching the floor).</summary>
        public bool NoSill;
        /// <summary>What fills it (the kit adds frames, shutters, doors, grilles by this tag).</summary>
        public OpeningFill Fill;
        /// <summary>Variation seed for the fill (shutter state, paint).</summary>
        public int Seed;

        /// <summary>Width.</summary>
        public float Width => U1 - U0;
        /// <summary>Height to the apex.</summary>
        public float Height => V1 - V0;
        /// <summary>Centre along the wall.</summary>
        public float CenterU => (U0 + U1) * 0.5f;

        /// <summary>Springline height of an arch (= V1 for rectangles).</summary>
        public float Springline
        {
            get
            {
                switch (Shape)
                {
                    case OpeningShape.RoundArch: return V1 - Width * 0.5f;
                    case OpeningShape.SegmentalArch: return V1 - Width * 0.25f;
                    default: return V1;
                }
            }
        }

        /// <summary>Creates an opening.</summary>
        public static WallOpening Make(float u0, float u1, float v0, float v1, OpeningShape shape, float depth, bool blind, OpeningFill fill, int seed = 0, bool noSill = false) =>
            new WallOpening { U0 = u0, U1 = u1, V0 = v0, V1 = v1, Shape = shape, Depth = depth, Blind = blind, Fill = fill, Seed = seed, NoSill = noSill };
    }
}
