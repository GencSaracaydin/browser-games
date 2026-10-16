namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Material slot keys written by the kit and the generators. The editor's <c>LevelMaterials</c> maps each key to a
    /// CC0/fal material by manifest name (WP10_CC0 / WP10_FAL <c>materials_*.json</c>) with a greybox fallback colour, so
    /// generated meshes render before the textures land and pick them up when the builders are re-run.
    /// </summary>
    public static class KitSlots
    {
        /// <summary>Number of lime-wash tints (<c>Plaster0</c>…<c>Plaster5</c>): cream, ochre, sand, pale blue-grey, faded pink, white.</summary>
        public const int PlasterTints = 6;

        /// <summary>Lime-washed render, tint <paramref name="tint"/> (0..5).</summary>
        public static string Plaster(int tint) => "Plaster" + ((tint % PlasterTints + PlasterTints) % PlasterTints);

        /// <summary>Darker splash band at the foot of rendered walls.</summary>
        public const string PlasterBase = "PlasterBase";
        /// <summary>Rough white plaster (copings, trims, interiors).</summary>
        public const string PlasterRough = "PlasterRough";
        /// <summary>Peeling, damaged render (damage variants).</summary>
        public const string PlasterDamaged = "PlasterDamaged";
        /// <summary>Exposed sun-dried mudbrick (patches where the render fell off, garden and irrigation walls).</summary>
        public const string Mudbrick = "Mudbrick";
        /// <summary>Sandstone blocks (Weighbridge arch, clock tower, market arch).</summary>
        public const string Sandstone = "Sandstone";
        /// <summary>Dressed sandstone trim (archivolts, keystones, sills on civic buildings).</summary>
        public const string SandstoneTrim = "SandstoneTrim";
        /// <summary>Board-marked concrete (compound wall, gatehouse).</summary>
        public const string Concrete = "Concrete";
        /// <summary>Worn concrete floor and roof slabs.</summary>
        public const string ConcreteFloor = "ConcreteFloor";
        /// <summary>Earthen/lime screed on vernacular flat roofs.</summary>
        public const string RoofDeck = "RoofDeck";
        /// <summary>Painted render of the Art Deco Relay Hall.</summary>
        public const string DecoRender = "DecoRender";
        /// <summary>Art Deco trim bands and fins.</summary>
        public const string DecoTrim = "DecoTrim";
        /// <summary>Stone paving (Clock Square, market).</summary>
        public const string Paving = "Paving";
        /// <summary>Worn road surface (east street, compound road).</summary>
        public const string Road = "Road";
        /// <summary>Packed dirt (alleys, yard, pitch).</summary>
        public const string Dirt = "Dirt";
        /// <summary>Sand.</summary>
        public const string Sand = "Sand";
        /// <summary>Gravelly sand (tracks, yard).</summary>
        public const string Gravel = "Gravel";
        /// <summary>Salt crust (salt pan, pitch edges).</summary>
        public const string SaltCrust = "SaltCrust";
        /// <summary>Cracked dry mud.</summary>
        public const string MudCracked = "MudCracked";
        /// <summary>Basalt rock face.</summary>
        public const string Rock = "Rock";
        /// <summary>Terrazzo (cinema lobby).</summary>
        public const string Terrazzo = "Terrazzo";
        /// <summary>Floor tiles (Hall offices).</summary>
        public const string FloorTiles = "FloorTiles";
        /// <summary>Old linoleum (screening room, upper rooms).</summary>
        public const string Linoleum = "Linoleum";
        /// <summary>Cinema carpet (aisles).</summary>
        public const string Carpet = "Carpet";
        /// <summary>Weathered planks.</summary>
        public const string Wood = "Wood";
        /// <summary>Painted, peeling timber (shutters, doors, balconies).</summary>
        public const string WoodPainted = "WoodPainted";
        /// <summary>Dark stained planks (floors, benches).</summary>
        public const string WoodDark = "WoodDark";
        /// <summary>Rusty corrugated iron roofs.</summary>
        public const string Corrugated = "Corrugated";
        /// <summary>Worn galvanised corrugated iron.</summary>
        public const string CorrugatedWorn = "CorrugatedWorn";
        /// <summary>Roller-shutter steel (ribs horizontal through UVs).</summary>
        public const string MetalShutter = "MetalShutter";
        /// <summary>Painted steel frames, doors, rails.</summary>
        public const string SteelFrame = "SteelFrame";
        /// <summary>Green painted steel with rust (mast, flood towers, goalposts).</summary>
        public const string SteelGreen = "SteelGreen";
        /// <summary>Rusty wrought iron (grilles, brackets, ladders).</summary>
        public const string Iron = "Iron";
        /// <summary>Steel plate (truck scale, hatches).</summary>
        public const string SteelPlate = "SteelPlate";
        /// <summary>Sheet metal (water tanks, ducting).</summary>
        public const string SheetMetal = "SheetMetal";
        /// <summary>Dark glass.</summary>
        public const string Glass = "Glass";
        /// <summary>Dark interior card behind blind openings.</summary>
        public const string InteriorCard = "InteriorCard";
        /// <summary>Striped awning canvas.</summary>
        public const string Awning = "Awning";
        /// <summary>Plain canvas.</summary>
        public const string Canvas = "Canvas";
        /// <summary>Hessian (sacking, stall covers).</summary>
        public const string Hessian = "Hessian";
        /// <summary>Rope and cable.</summary>
        public const string Rope = "Rope";
        /// <summary>Electric cable (black).</summary>
        public const string Cable = "Cable";
        /// <summary>Laundry cloth, tint <paramref name="tint"/> (0..3).</summary>
        public static string Cloth(int tint) => "Cloth" + ((tint % 4 + 4) % 4);
        /// <summary>Red brick.</summary>
        public const string Brick = "Brick";
        /// <summary>Velvet (cinema seats, curtain).</summary>
        public const string Velvet = "Velvet";
        /// <summary>Sodium lamp lens (emission driven by WP10 Look).</summary>
        public const string SodiumLens = "SodiumLens";
        /// <summary>Red aviation light lens.</summary>
        public const string AviationLens = "AviationLens";
        /// <summary>Flickering EXIT sign face.</summary>
        public const string ExitSign = "ExitSign";
        /// <summary>Enamel clock face.</summary>
        public const string ClockFace = "ClockFace";
        /// <summary>Projection screen / white board.</summary>
        public const string Screen = "Screen";
        /// <summary>Flood lens.</summary>
        public const string FloodLens = "FloodLens";
        /// <summary>Dark window darks on far proxies.</summary>
        public const string ProxyWindow = "ProxyWindow";
        /// <summary>Red paint (barrier stripes, warning bands).</summary>
        public const string PaintRed = "PaintRed";
        /// <summary>White paint (barrier stripes, lines).</summary>
        public const string PaintWhite = "PaintWhite";
    }
}
