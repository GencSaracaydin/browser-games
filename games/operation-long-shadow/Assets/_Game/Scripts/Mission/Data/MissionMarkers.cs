using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// Scene marker ids (TECH_PLAN Appendix A kinds M, N, CP, D, G, K and the splines) that the mission scripts address
    /// through <c>IMarkerRegistry</c>, <c>ISquadService</c>, <c>Destructibles</c> and <c>ISplineVehicle</c>, with their
    /// Appendix A world positions as fallbacks while the section is missing (greybox gaps, dev scenes). Booth and heli ids
    /// use local frames and have no world fallback. Scripts never write marker ids as literals; the tests check every id
    /// and every fallback position against <c>markup_ids.csv</c>.
    /// </summary>
    public static class MissionMarkers
    {
        // ---- global
        public const string PAHorn1 = "PA.Horn1";
        public const string PAHorn2 = "PA.Horn2";
        public const string PAHorn3 = "PA.Horn3";
        public const string CrateWeighbridge = "CRATE.Weighbridge";
        public const string CrateMarketArch = "CRATE.MarketArch";
        public const string CrateR4 = "CRATE.R4";
        public const string CrateBasement = "CRATE.Basement";
        public const string CrateLZ = "CRATE.LZ";

        // ---- Beat 1
        public const string B1PlayerStart = "B1.PlayerStart";
        public const string B1SquadStart = "B1.Squad.Start";
        public const string B1POI1 = "B1.POI1";
        public const string B1POI2 = "B1.POI2";
        public const string B1POI3 = "B1.POI3";
        public const string B1GroveTarget = "B1.GroveTarget";
        public const string B1WadiHead = "B1.WadiHead";

        // ---- Beat 2
        public const string B2FiringPos = "B2.FiringPos";
        public const string B2HollisMark = "B2.HollisMark";
        public const string B2Hold = "B2.Hold";
        public const string B2NodeSetA = "B2.NodeSet.A";
        public const string B2NodeSetB = "B2.NodeSet.B";
        public const string B2Gate = "B2.Gate";
        public const string B2TruckTarget = "B2.TruckTarget";
        public const string B2ArchTarget = "B2.ArchTarget";

        // ---- Beat 3
        public const string B3NodeSetM1 = "B3.NodeSet.M1";
        public const string B3NodeSetM2 = "B3.NodeSet.M2";
        public const string B3NodeSetM3 = "B3.NodeSet.M3";
        public const string B3NodeSetM4 = "B3.NodeSet.M4";
        public const string B3Smoke = "B3.Smoke";
        public const string B3ArchTarget = "B3.ArchTarget";

        // ---- Beat 4
        public const string B4NodeSetQ1 = "B4.NodeSet.Q1";
        public const string B4NodeSetQ2 = "B4.NodeSet.Q2";
        public const string B4SideDoor = "B4.SideDoor";
        public const string B4Technical = "B4.Technical";
        public const string B4Ladder = "B4.Ladder";

        // ---- Beat 5
        public const string B5NodeSetR1 = "B5.NodeSet.R1";
        public const string B5NodeSetR2 = "B5.NodeSet.R2";
        public const string B5NodeSetR3 = "B5.NodeSet.R3";
        public const string B5NodeSetMidA = "B5.NodeSet.MIDA";
        public const string B5R4Parapet = "B5.R4Parapet";
        public const string B5BreachPoint = "B5.BreachPoint";
        public const string B5SunLook = "B5.SunLook";

        // ---- Beat 6
        public const string B6StandClear = "B6.StandClear";
        public const string B6Charge = "B6.Charge";
        public const string B6BreachWall = "B6.BreachWall";
        public const string B6F1 = "B6.F1";
        public const string B6F2 = "B6.F2";
        public const string B6F3 = "B6.F3";
        public const string B6F4 = "B6.F4";
        public const string B6NodeSetY1 = "B6.NodeSet.Y1";
        public const string B6NodeSetY2 = "B6.NodeSet.Y2";
        public const string B6NodeSetY3 = "B6.NodeSet.Y3";
        /// <summary>The Relay Hall double door: InteractionPoint ("[E] Kick") and the beat 6 exit.</summary>
        public const string HallDoor = "B6.HallDoor";

        // ---- Beat 7
        public const string B7Ledgers = "B7.Ledgers";
        public const string B7O1 = "B7.O1";
        public const string B7Spiral = "B7.Spiral";
        public const string B7HollisA = "B7.HOL.A";
        public const string B7HollisB = "B7.HOL.B";
        public const string B7TinkerA = "B7.TIN.A";
        public const string B7TinkerB = "B7.TIN.B";
        public const string B7SparrowOffice = "B7.SPA.Office";

        // ---- Beat 8
        public const string B8Projector = "B8.Projector";
        public const string B8CanRack = "B8.CanRack";
        public const string B8O2 = "B8.O2";
        public const string B8WestStair = "B8.WestStair";
        public const string B8NodeSetReel = "B8.NodeSet.Reel";

        // ---- Beat 9
        public const string B9Safe = "B9.Safe";
        public const string B9NodeSetDefend = "B9.NodeSet.Defend";
        public const string B9R1 = "B9.R1";
        public const string B9R2 = "B9.R2";
        public const string B9R3 = "B9.R3";
        public const string B9R4 = "B9.R4";
        public const string B9Speaker = "B9.Speaker";
        public const string B9TunnelDoor = "B9.TunnelDoor";
        public const string B9EastStairFoot = "B9.EastStairFoot";

        // ---- Beat 10
        public const string B10LZLine = "B10.LZLine";
        public const string B10Pad = "B10.Pad";
        public const string B10NodeSetP1 = "B10.NodeSet.P1";
        public const string B10GreenSmoke = "B10.GreenSmoke";
        /// <summary>The helicopter's left door: InteractionPoint ("[E] Board") and the beat 10 exit.</summary>
        public const string HeliDoor = "B10.HeliDoor";
        public const string B10HeliApproach = "B10.HeliApproach";
        public const string B10HeliWaveOff = "B10.HeliWaveOff";
        public const string B10FL1 = "B10.FL1";
        public const string B10FL2 = "B10.FL2";
        public const string B10FL3 = "B10.FL3";

        // ---- Beat 11
        public const string B11Exfil = "B11.Exfil";
        public const string B11TruckSquare = "B11.TruckSquare";
        public const string HeliDoorGunPivot = "HELI.DoorGunPivot";
        public const string HeliHollis = "HELI.Hollis";
        public const string HeliSparrow = "HELI.Sparrow";
        public const string HeliTinker = "HELI.Tinker";

        /// <summary>
        /// Ids the mission needs that are not in Appendix A yet (requested in Docs/REQUESTS.md: the two Clock Square trucks
        /// of Beat 11 as <c>Destructible</c>s). The id tests accept exactly these.
        /// </summary>
        public static readonly string[] PendingIds = { ObjectiveTable.B11Truck1, ObjectiveTable.B11Truck2 };

        private static readonly Dictionary<string, Vector3> s_fallbacks = new Dictionary<string, Vector3>
        {
            { PAHorn1, new Vector3(185f, 8f, 200f) },
            { PAHorn2, new Vector3(220f, 20f, 275f) },
            { PAHorn3, new Vector3(346f, 6f, 294f) },
            { CrateWeighbridge, new Vector3(182f, 0f, 150f) },
            { CrateMarketArch, new Vector3(184f, 0f, 247f) },
            { CrateR4, new Vector3(288f, 12f, 330f) },
            { CrateBasement, new Vector3(322f, -4.2f, 355f) },
            { CrateLZ, new Vector3(338f, -1f, 389f) },
            { B1PlayerStart, new Vector3(42f, 22f, 30f) },
            { B1POI1, new Vector3(378f, 45f, 362f) },
            { B1POI2, new Vector3(220f, 18f, 275f) },
            { B1POI3, new Vector3(183f, 4f, 162f) },
            { B1GroveTarget, new Vector3(108f, 0f, 96f) },
            { B1WadiHead, new Vector3(70f, 18f, 50f) },
            { B2FiringPos, new Vector3(136f, 0f, 112f) },
            { B2HollisMark, new Vector3(139f, 0f, 113f) },
            { B2Hold, new Vector3(130f, 0f, 108f) },
            { B2Gate, new Vector3(183f, 0f, 163f) },
            { B2TruckTarget, new Vector3(190f, 3f, 150f) },
            { B2ArchTarget, new Vector3(183f, 2f, 166f) },
            { B3Smoke, new Vector3(185f, 0f, 212f) },
            { B3ArchTarget, new Vector3(185f, 4f, 252f) },
            { B4SideDoor, new Vector3(186f, 1.2f, 307f) },
            { B4Technical, new Vector3(232f, 1.6f, 286f) },
            { B4Ladder, new Vector3(236f, 0f, 301f) },
            { B5R4Parapet, new Vector3(291f, 13f, 328f) },
            { B5BreachPoint, new Vector3(300f, 1.5f, 322f) },
            { B5SunLook, new Vector3(-197.91f, 26.09f, 224.08f) },
            { B6StandClear, new Vector3(296f, 0f, 310f) },
            { B6Charge, new Vector3(300f, 1f, 322f) },
            { B6F1, new Vector3(306f, 8f, 296f) },
            { B6F2, new Vector3(386f, 8f, 296f) },
            { B6F3, new Vector3(306f, 8f, 376f) },
            { B6F4, new Vector3(386f, 8f, 376f) },
            { HallDoor, new Vector3(338f, 1.2f, 330f) },
            { B7Ledgers, new Vector3(351f, 1.1f, 355f) },
            { B7O1, new Vector3(341f, 1.2f, 351f) },
            { B7Spiral, new Vector3(334.5f, 0.3f, 358f) },
            { B7HollisA, new Vector3(320f, 0.3f, 333f) },
            { B7HollisB, new Vector3(330f, 0.3f, 342f) },
            { B7TinkerA, new Vector3(344f, 0.3f, 333f) },
            { B7TinkerB, new Vector3(340f, 0.3f, 348f) },
            { B7SparrowOffice, new Vector3(348f, 0.3f, 352f) },
            { B8Projector, new Vector3(323f, 5.6f, 352f) },
            { B8CanRack, new Vector3(324f, 5.6f, 350.5f) },
            { B8O2, new Vector3(319.5f, 5.8f, 350.5f) },
            { B8WestStair, new Vector3(319f, 0.3f, 337f) },
            { B9Safe, new Vector3(345.8f, -3.4f, 342f) },
            { B9Speaker, new Vector3(331.5f, -3.4f, 357f) },
            { B9TunnelDoor, new Vector3(344f, -3f, 359.8f) },
            { B9EastStairFoot, new Vector3(343.5f, -4.2f, 338.5f) },
            { B10LZLine, new Vector3(340f, -1f, 390.5f) },
            { B10Pad, new Vector3(350f, -1f, 406f) },
            { B10GreenSmoke, new Vector3(350f, -1f, 404f) },
            { HeliDoor, new Vector3(350f, 0.2f, 404f) },
            { B10FL1, new Vector3(346f, 70f, 404f) },
            { B10FL2, new Vector3(356f, 70f, 410f) },
            { B10FL3, new Vector3(340f, 70f, 400f) },
        };

        /// <summary>Every id with a world fallback position.</summary>
        public static IEnumerable<string> FallbackIds => s_fallbacks.Keys;

        /// <summary>The Appendix A world position of a marker id.</summary>
        public static bool TryGetFallback(string id, out Vector3 position)
        {
            if (id != null && s_fallbacks.TryGetValue(id, out position)) return true;
            var cp = BeatTable.Checkpoint(id);
            if (cp != null)
            {
                position = cp.Position;
                return true;
            }
            if (MissionTriggers.TryGetShape(id, out var shape))
            {
                position = shape.Center;
                return true;
            }
            position = default;
            return false;
        }
    }
}
