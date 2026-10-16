using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// The prop library of the level (TECH_PLAN D12: assets by id; WP10_CC0 / WP10_FAL produce the models into
    /// <c>Art/Models/{Props,Vehicles,Vegetation}</c>). Builders ask for a key; the editor resolves the model through
    /// <c>AssetManifest</c> and falls back to a greybox of the real size (logged once) until it lands. Sizes come from the
    /// content agents' import indexes (WP10_CC0 <c>size_m</c>) or the manifest briefs.
    /// </summary>
    public static class PropCatalog
    {
        private static Dictionary<string, PropDef> s_byKey;

        /// <summary>Every prop.</summary>
        public static IEnumerable<PropDef> All
        {
            get
            {
                Ensure();
                return s_byKey.Values;
            }
        }

        /// <summary>A prop by key (null if unknown).</summary>
        public static PropDef Get(string key)
        {
            Ensure();
            return key != null && s_byKey.TryGetValue(key, out var p) ? p : null;
        }

        private static void Ensure()
        {
            if (s_byKey != null) return;
            s_byKey = new Dictionary<string, PropDef>();
            foreach (var p in Build()) s_byKey[p.Key] = p;
        }

        private static PropDef D(string key, string manifest, string name, float x, float y, float z, SurfaceType surface, int collider = 1, bool lightmapped = false,
                                 string notes = null, PrimitiveType prim = PrimitiveType.Cube)
        {
            return new PropDef
            {
                Key = key, ManifestId = manifest, NameContains = name, Size = new Vector3(x, y, z), Surface = surface, Collider = collider,
                Lightmapped = lightmapped, Notes = notes, Primitive = prim, Color = ColorFor(surface),
            };
        }

        private static Color ColorFor(SurfaceType s)
        {
            switch (s)
            {
                case SurfaceType.Wood: return new Color(0.45f, 0.33f, 0.22f);
                case SurfaceType.Metal: return new Color(0.36f, 0.38f, 0.36f);
                case SurfaceType.Fabric: return new Color(0.55f, 0.5f, 0.38f);
                case SurfaceType.Sand: return new Color(0.66f, 0.58f, 0.44f);
                case SurfaceType.Stone: return new Color(0.5f, 0.46f, 0.4f);
                case SurfaceType.Concrete: return new Color(0.58f, 0.57f, 0.54f);
                case SurfaceType.Fruit: return new Color(0.9f, 0.45f, 0.1f);
                default: return new Color(0.52f, 0.5f, 0.46f);
            }
        }

        private static IEnumerable<PropDef> Build()
        {
            // ---- fal props (WP10_FAL)
            yield return D("Sandbag", "FAL_SANDBAG", "SM_Sandbag", 0.6f, 0.18f, 0.38f, SurfaceType.Sand, 1, false, "Stacked into walls and rings.");
            yield return D("CableDrum", "FAL_CABLE_DRUM", "SM_CableDrum", 1.4f, 1.3f, 1.0f, SurfaceType.Wood, 1, false, "Yard cover (Y2).");
            yield return D("DieselPump", "FAL_DIESEL_PUMP", "SM_DieselPump", 1.4f, 1.2f, 0.9f, SurfaceType.Metal);
            yield return D("MarketStall", "FAL_MARKET_STALL", "SM_MarketStall", 2.4f, 2.3f, 1.6f, SurfaceType.Wood, 1, false, "Beat 3 cover.");
            yield return D("FruitCrateOrange", "FAL_FRUIT_CRATE_ORANGE", "SM_FruitCrateOrange", 0.6f, 0.4f, 0.9f, SurfaceType.Wood);
            yield return D("FruitCratePomegranate", "FAL_FRUIT_CRATE_POMEGRANATE", "SM_FruitCratePomegranate", 0.6f, 0.4f, 0.9f, SurfaceType.Wood);
            yield return D("Orange", "FAL_ORANGE", "SM_Orange", 0.08f, 0.08f, 0.08f, SurfaceType.Fruit, 3, false, "Physics fruit.", PrimitiveType.Sphere);
            yield return D("Pomegranate", "FAL_POMEGRANATE", "SM_Pomegranate", 0.09f, 0.09f, 0.09f, SurfaceType.Fruit, 3, false, "Physics fruit.", PrimitiveType.Sphere);
            yield return D("Teletype", "FAL_TELETYPE", "SM_Teletype", 0.6f, 0.45f, 0.5f, SurfaceType.Metal, 0);
            yield return D("TubeRack", "FAL_TUBE_RACK", "SM_TubeRack", 0.7f, 2.2f, 0.7f, SurfaceType.Metal, 1, false, "Clockmaker racks R1-R4.");
            yield return D("WallSafe", "FAL_WALL_SAFE", "SM_WallSafe", 0.9f, 1.2f, 0.5f, SurfaceType.Metal, 1);
            yield return D("ReelToReel", "FAL_REEL_TO_REEL", "SM_ReelToReel", 0.45f, 0.45f, 0.3f, SurfaceType.Metal, 0);
            yield return D("PAHorn", "FAL_PA_HORN", "SM_PAHorn", 0.5f, 0.5f, 0.7f, SurfaceType.Metal, 0);
            yield return D("FilmCan", "FAL_FILM_CAN", "SM_FilmCan", 0.38f, 0.05f, 0.38f, SurfaceType.Metal, 0, false, null, PrimitiveType.Cylinder);
            yield return D("FilmCanClosed", "FAL_FILM_CAN", "SM_FilmCanClosed", 0.38f, 0.05f, 0.38f, SurfaceType.Metal, 0, false, null, PrimitiveType.Cylinder);
            yield return D("CinemaSeat", "FAL_CINEMA_SEAT", "SM_CinemaSeat", 0.55f, 0.95f, 0.65f, SurfaceType.Fabric);
            yield return D("Typewriter", "FAL_TYPEWRITER", "SM_Typewriter", 0.45f, 0.2f, 0.4f, SurfaceType.Metal, 0);
            yield return D("LedgerStack", "FAL_LEDGER_STACK", "SM_LedgerStack", 0.35f, 0.15f, 0.28f, SurfaceType.Default, 0);
            yield return D("MGTripod", "FAL_MG_TRIPOD", "SM_MGTripod", 0.9f, 0.6f, 0.9f, SurfaceType.Metal, 0);
            yield return D("OnAirLamp", "FAL_ON_AIR_LAMP", "SM_OnAirLamp", 0.5f, 0.2f, 0.12f, SurfaceType.Metal, 0);
            yield return D("Kettle", "FAL_KETTLE", "SM_Kettle", 0.2f, 0.2f, 0.2f, SurfaceType.Metal, 0);
            yield return D("MealPlate", "FAL_MEAL_PLATE", "SM_MealPlate", 0.28f, 0.06f, 0.28f, SurfaceType.Default, 0);
            yield return D("FuelBowser", "FAL_FUEL_BOWSER", "SM_FuelBowser", 1.9f, 1.9f, 3.6f, SurfaceType.Metal, 1, true, "E6-03 cover (not explosive).");
            yield return D("WoodenBench", "FAL_WOODEN_BENCH", "SM_WoodenBench", 1.6f, 0.45f, 0.4f, SurfaceType.Wood);
            yield return D("WallSpeaker", "FAL_WALL_SPEAKER", "SM_WallSpeaker", 0.4f, 0.3f, 0.2f, SurfaceType.Metal, 0);
            yield return D("Projector16mm", "PH_FILMSTRIP_PROJECTOR_8MM", "SM_Projector16mm", 0.33f, 0.45f, 0.78f, SurfaceType.Metal, 0);
            // ---- Poly Haven props (WP10_CC0)
            yield return D("AmmoBox", "PH_AMMO_BOX", "SM_AmmoBox", 0.09f, 0.18f, 0.26f, SurfaceType.Metal, 0);
            yield return D("BarrelGreen", "PH_BARREL_01", "SM_Barrel01", 0.56f, 0.88f, 0.56f, SurfaceType.Metal, 1, false, null, PrimitiveType.Cylinder);
            yield return D("Barrel03", "PH_BARREL_03", "SM_Barrel03", 0.63f, 0.93f, 0.64f, SurfaceType.Metal, 1, false, null, PrimitiveType.Cylinder);
            yield return D("CassettePlayer", "PH_CASSETTE_PLAYER", "SM_CassettePlayer", 0.13f, 0.24f, 0.05f, SurfaceType.Metal, 0);
            yield return D("HangingLamp", "PH_HANGING_INDUSTRIAL_LAMP", "SM_HangingIndustrialLamp", 0.55f, 1.36f, 0.55f, SurfaceType.Metal, 0);
            yield return D("IronGate", "PH_LARGE_IRON_GATE", "SM_LargeIronGate", 2.95f, 2.93f, 0.1f, SurfaceType.Metal, 0, false, "Weighbridge gate leaves.");
            yield return D("OfficeDesk", "PH_METAL_OFFICE_DESK", "SM_MetalOfficeDesk", 2.0f, 0.79f, 0.95f, SurfaceType.Metal);
            yield return D("MilitaryCrate", "PH_OLD_MILITARY_CRATE", "SM_OldMilitaryCrate_A", 0.82f, 0.3f, 0.54f, SurfaceType.Wood);
            yield return D("PaintedTable", "PH_PAINTED_WOODEN_TABLE", "SM_PaintedWoodenTable", 2.4f, 0.96f, 1.14f, SurfaceType.Wood);
            yield return D("Generator", "PH_PORTABLE_GENERATOR", "SM_PortableGenerator", 0.82f, 0.58f, 0.56f, SurfaceType.Metal);
            yield return D("ProjectorScreen", "PH_PROJECTOR_SCREEN", "SM_ProjectorScreen", 1.1f, 1.8f, 0.9f, SurfaceType.Fabric, 0);
            yield return D("Rock07", "PH_ROCK_07", "SM_Rock07", 0.17f, 0.14f, 0.32f, SurfaceType.Stone, 1, false, "Scale 3-15x.", PrimitiveType.Sphere);
            yield return D("Rock09", "PH_ROCK_09", "SM_Rock09", 0.07f, 0.03f, 0.15f, SurfaceType.Stone, 0, false, "Pebbles.", PrimitiveType.Sphere);
            yield return D("RockFace", "PH_ROCK_FACE_01", "SM_RockFace01", 4.95f, 3.57f, 3.83f, SurfaceType.Stone, 1, true, "Basalt outcrops; never show its open back.");
            yield return D("RollerDoor", "PH_ROLLERSHUTTER_DOOR", "SM_RollershutterDoor", 1.08f, 2.4f, 0.3f, SurfaceType.Metal);
            yield return D("SecurityLight", "PH_SECURITY_LIGHT", "SM_SecurityLight", 0.32f, 0.53f, 0.42f, SurfaceType.Metal, 0, false, "Flood heads F1-F4.");
            yield return D("WoodenChair", "PH_WOODENCHAIR_01", "SM_PaintedWoodenChair02", 0.64f, 1.26f, 0.66f, SurfaceType.Wood, 0);
            yield return D("WoodenCrate", "PH_WOODEN_CRATE_01", "SM_WoodenCrate01", 0.83f, 0.35f, 0.41f, SurfaceType.Wood);
            yield return D("WoodenCrateLong", "PH_WOODEN_CRATE_02", "SM_WoodenCrate02", 0.53f, 0.46f, 1.17f, SurfaceType.Wood);
            yield return D("LadderStraight", "PH_WOODEN_LADDER", "SM_WoodenLadderStraight", 0.47f, 3.6f, 0.07f, SurfaceType.Wood, 0);
            yield return D("StepLadder", "PH_WOODEN_LADDER", "SM_WoodenStepLadder", 0.96f, 1.34f, 0.5f, SurfaceType.Wood, 0);
            yield return D("WoodenTable", "PH_WOODEN_TABLE_02", "SM_WoodenTable02", 1.13f, 0.8f, 0.71f, SurfaceType.Wood);
            yield return D("CagedLight", "PH_CAGED_HANGING_LIGHT", "SM_CagedHangingLight", 0.3f, 0.75f, 0.3f, SurfaceType.Metal, 0);
            yield return D("CeilingFan", "PH_CEILING_FAN", "SM_CeilingFan", 1.46f, 0.52f, 1.46f, SurfaceType.Metal, 0);
            yield return D("RoadBarrier", "PH_CONCRETE_ROAD_BARRIER_02", "SM_ConcreteRoadBarrier02", 1.57f, 1.11f, 0.44f, SurfaceType.Concrete, 1, false, "Weighbridge barrier blocks (E2-04).");
            yield return D("CoveredCar", "PH_COVERED_CAR", "SM_CoveredCar", 1.79f, 1.41f, 4.38f, SurfaceType.Fabric, 1, true, "Clock Square cover.");
            yield return D("DeskLamp", "PH_DESK_LAMP_ARM_01", "SM_DeskLampArm01", 0.2f, 0.89f, 0.61f, SurfaceType.Metal, 0);
            yield return D("DrawerCabinet", "PH_DRAWER_CABINET", "SM_VintageWoodenDrawer01", 1.72f, 1.09f, 1.0f, SurfaceType.Wood);
            yield return D("DryBranchesA", "PH_DRY_BRANCHES_MEDIUM_01", "SM_DryBranchesMedium01_A", 0.32f, 0.34f, 1.3f, SurfaceType.Wood, 0);
            yield return D("DryBranchesB", "PH_DRY_BRANCHES_MEDIUM_01", "SM_DryBranchesMedium01_B", 0.15f, 0.14f, 1.04f, SurfaceType.Wood, 0);
            yield return D("Jerrycan", "PH_METAL_JERRYCAN_GREEN", "SM_MetalJerrycanGreen", 0.36f, 0.5f, 0.17f, SurfaceType.Metal, 0);
            yield return D("TrashCanRust", "PH_METAL_TRASH_CAN", "SM_MetalTrashCan_Rust", 0.77f, 0.91f, 0.55f, SurfaceType.Metal);
            yield return D("Boulder", "PH_NAMAQUALAND_BOULDER_04", "SM_NamaqualandBoulder04", 2.52f, 1.9f, 2.5f, SurfaceType.Stone, 1, true, "The wadi mantle boulder.", PrimitiveType.Sphere);
            yield return D("Notepads", "PH_OFFICE_NOTEPADS", "SM_OfficeNotepads", 1.1f, 0.02f, 0.66f, SurfaceType.Default, 0);
            yield return D("OldTyre", "PH_OLD_TYRE", "SM_OldTyre", 0.6f, 0.6f, 0.17f, SurfaceType.Default, 0);
            yield return D("SteelShelves", "PH_STEEL_FRAME_SHELVES_01", "SM_SteelFrameShelves01", 1.1f, 2.14f, 0.5f, SurfaceType.Metal);
            yield return D("UtilityBox", "PH_UTILITY_BOX_01", "SM_UtilityBox01", 0.52f, 1.12f, 0.43f, SurfaceType.Metal);
            yield return D("RadioSet", "PH_VINTAGE_RADIO_TRANSCEIVER", "SM_VintageRadioTransceiver", 0.57f, 0.43f, 0.41f, SurfaceType.Metal, 0);
            yield return D("DeadTrunk", "PH_DEAD_TREE_TRUNK", "SM_DeadTreeTrunk", 3.05f, 0.29f, 0.28f, SurfaceType.Wood, 1);
            yield return D("MetalStool", "PH_METAL_STOOL_01", "SM_MetalStool01", 0.35f, 0.88f, 0.35f, SurfaceType.Metal, 0);
            yield return D("PropaneTank", "PH_PROPANE_TANK", "SM_PropaneTank", 0.34f, 0.55f, 0.34f, SurfaceType.Metal, 0);
            yield return D("Trashbag", "PH_TRASHBAG", "SM_Trashbag", 0.53f, 0.57f, 0.46f, SurfaceType.Fabric, 0);
            yield return D("WickerBasket", "PH_WICKER_BASKET_01", "SM_WickerBasket01", 0.38f, 0.12f, 0.3f, SurfaceType.Wood, 0);
            yield return D("Clipboard", "PH_CLIPBOARD", "SM_Clipboard", 0.23f, 0.04f, 0.34f, SurfaceType.Default, 0);
            // ---- vegetation and vehicles
            yield return D("OliveTreeA", "VEG_OLIVE_TREE", "SM_OliveTree_A", 4f, 4.5f, 4f, SurfaceType.Wood, 2, false, null, PrimitiveType.Capsule);
            yield return D("OliveTreeB", "VEG_OLIVE_TREE", "SM_OliveTree_B", 4f, 4.5f, 4f, SurfaceType.Wood, 2, false, null, PrimitiveType.Capsule);
            yield return D("OliveTreeC", "VEG_OLIVE_TREE", "SM_OliveTree_C", 4f, 4.5f, 4f, SurfaceType.Wood, 2, false, null, PrimitiveType.Capsule);
            yield return D("PomegranateTreeA", "VEG_POMEGRANATE_TREE", "SM_PomegranateTree_A", 3f, 3f, 3f, SurfaceType.Wood, 2, false, null, PrimitiveType.Capsule);
            yield return D("PomegranateTreeB", "VEG_POMEGRANATE_TREE", "SM_PomegranateTree_B", 3f, 3f, 3f, SurfaceType.Wood, 2, false, null, PrimitiveType.Capsule);
            yield return D("PomegranateTreeC", "VEG_POMEGRANATE_TREE", "SM_PomegranateTree_C", 3f, 3f, 3f, SurfaceType.Wood, 2, false, null, PrimitiveType.Capsule);
            yield return D("DryGrass", "VEG_DRY_GRASS_CARDS", "SM_DryGrass", 0.6f, 0.5f, 0.6f, SurfaceType.Default, 0);
            yield return D("Technical", "VEH_TECHNICAL", "SM_Technical", 1.9f, 1.9f, 5.2f, SurfaceType.Metal, 1, false, "E4-01's DShK pickup; pedestal top at local (-0.136, 1.838, -1.026).");
            yield return D("CargoTruck", "VEH_CARGO_TRUCK", "SM_CargoTruck", 2.4f, 2.9f, 6.7f, SurfaceType.Metal, 1, false, "Weighbridge truck, Beat 10/11 trucks.");
            yield return D("CargoTruckTarpOff", "VEH_CARGO_TRUCK", "SM_CargoTruckTarpOff", 2.4f, 2.6f, 6.7f, SurfaceType.Metal, 1);
            yield return D("Helicopter", "VEH_HELICOPTER", "SM_Helicopter", 2.6f, 3.6f, 13f, SurfaceType.Metal, 1, false, "Magpie; main hub local (0.004, 4.537, 2.121), tail hub (-0.519, 3.992, -5.592).");
            yield return D("HeliMainRotor", "VEH_HELICOPTER", "SM_HeliMainRotor", 14.6f, 0.3f, 14.6f, SurfaceType.Metal, 0);
            yield return D("HeliTailRotor", "VEH_HELICOPTER", "SM_HeliTailRotor", 0.2f, 2.6f, 2.6f, SurfaceType.Metal, 0);
        }
    }
}
