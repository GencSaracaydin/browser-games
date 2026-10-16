using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// One generated building (GDD §6.6 BLD-xx; TECH_PLAN §7.15 BuildingSpec): footprint, storeys, style, roof and a facade
    /// programme per side (sides 0 south, 1 east, 2 north, 3 west). The <see cref="BuildingCatalog"/> lists every building
    /// of the tile; the BuildingGenerator turns specs into combined meshes, colliders and far-proxy shells.
    /// </summary>
    public sealed class BuildingSpec
    {
        /// <summary>Unique id ("BLD-03.W2b").</summary>
        public string Id;
        /// <summary>GDD building id ("BLD-03").</summary>
        public string Bld;
        /// <summary>Section scene that builds it (S9 only for <see cref="FarOnly"/> buildings).</summary>
        public SectionId Section;
        /// <summary>Far-proxy block id (null = always shown, far-only housing).</summary>
        public string Block;
        /// <summary>Footprint in world metres (before <see cref="Yaw"/>, around its centre).</summary>
        public AreaRect Footprint;
        /// <summary>Rotation about the footprint centre in degrees (Sunset Lane houses follow the lane at 258°).</summary>
        public float Yaw;
        /// <summary>Ground level.</summary>
        public float BaseY;
        /// <summary>Storey heights from the ground up (floor to floor, metres).</summary>
        public float[] Storeys = { 3.6f, 3.2f };
        /// <summary>Parapet height above the roof deck.</summary>
        public float Parapet = 0.9f;
        /// <summary>Roof.</summary>
        public RoofKind Roof = RoofKind.FlatParapet;
        /// <summary>Style.</summary>
        public BuildingStyle Style = BuildingStyle.Vernacular;
        /// <summary>Lime-wash tint index (KitSlots.Plaster).</summary>
        public int Tint;
        /// <summary>Facade programme per side (0 S, 1 E, 2 N, 3 W).</summary>
        public FacadeKind[] Facades = { FacadeKind.Residential, FacadeKind.Blank, FacadeKind.Blank, FacadeKind.Blank };
        /// <summary>Neighbour heights per side for party walls (the wall above it is built); 0 = none.</summary>
        public float[] PartyHeights = new float[4];
        /// <summary>Arcade depth on <see cref="FacadeKind.Arcade"/> sides.</summary>
        public float ArcadeDepth = 2.6f;
        /// <summary>Timber balconies on upper floors of street facades.</summary>
        public bool Balconies;
        /// <summary>Roof the player walks on (Laundry Roofs): walkable colliders, low parapet, clutter.</summary>
        public bool WalkableRoof;
        /// <summary>PlayerClip lid over the roof (OOB.MarketRoofs: roofs the player must not stand on).</summary>
        public bool RoofClip;
        /// <summary>Rooftop clutter (tanks, antennas, laundry, huts) for roofs seen from above.</summary>
        public bool RoofClutter = true;
        /// <summary>Weathering: chipped render patches and damage.</summary>
        public float Wear = 0.5f;
        /// <summary>Generated only in S9 (dense housing nobody walks next to).</summary>
        public bool FarOnly;
        /// <summary>Explicit openings (overrides the composer on that side where they overlap).</summary>
        public List<FeatureOpening> Features = new List<FeatureOpening>();
        /// <summary>Rooms cut into the solid volume (closets, the E3-05 loggia room), in world coordinates.</summary>
        public List<InteriorRoom> Rooms = new List<InteriorRoom>();
        /// <summary>Sides that receive no generated openings at ground level (blocked by a neighbouring structure).</summary>
        public bool[] NoGroundOpenings = new bool[4];
        /// <summary>Variation seed.</summary>
        public int Seed;
        /// <summary>Notes (why it is there, which beat uses it).</summary>
        public string Notes;

        /// <summary>Height of the roof deck above <see cref="BaseY"/>.</summary>
        public float WallHeight
        {
            get
            {
                var h = 0f;
                if (Storeys != null)
                    foreach (var s in Storeys) h += s;
                return h;
            }
        }

        /// <summary>World height of the roof deck.</summary>
        public float RoofY => BaseY + WallHeight;

        /// <summary>World height of the parapet top (or eaves).</summary>
        public float TopY => RoofY + (Roof == RoofKind.FlatParapet ? Parapet : 0.6f);

        /// <summary>Height above the base of the floor of storey <paramref name="index"/>.</summary>
        public float FloorAt(int index)
        {
            var h = 0f;
            for (var i = 0; i < index && i < Storeys.Length; i++) h += Storeys[i];
            return h;
        }
    }
}
