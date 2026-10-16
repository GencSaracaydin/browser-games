using System.Collections.Generic;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// The progression gates of GDD §7.13, the backtrack blockers of GDD §6.7 and the gate-driven trigger arms and
    /// interactions, as code data (TECH_PLAN §7.11 Gates). Ids are Appendix A "K" GateMarkers, TriggerVolumes and
    /// InteractionPoints placed by WP10/WP09. Every state is saved in the checkpoint snapshot (key "Gates").
    /// <see cref="GddName"/> holds the GDD §7.13 "Gate" cell so the tests can match rows.
    /// </summary>
    public static class GateTable
    {
        public const string Weighbridge = "Gate.Weighbridge";
        public const string CinemaSideDoor = "Gate.CinemaSideDoor";
        public const string LadderL = "Gate.LadderL";
        public const string R4Stair = "Gate.R4Stair";
        public const string HallDoor = "Gate.HallDoor";
        public const string ScreeningDoor = "Gate.ScreeningDoor";
        public const string WestStairDoor = "Gate.WestStairDoor";
        public const string Racks = "B9.Racks";
        public const string TunnelDoor = "Gate.TunnelDoor";
        public const string HeliDoor = "Gate.HeliDoor";
        public const string BlockMarketSouth = "Block.MarketSouth";
        public const string BlockMarketArch = "Block.MarketArch";
        public const string BlockSideAlley = "Block.SideAlley";
        public const string BlockR4Stair = "Block.R4Stair";
        public const string BlockBreach = "Block.Breach";
        public const string BlockPostern = "Block.Postern";
        public const string ArmLanding = "B7.T.Landing";
        public const string ArmBasementFoot = "B8.T.BasementFoot";
        public const string UseSideDoor = "B4.SideDoor";
        public const string UseHallDoor = "B6.HallDoor";
        public const string UseProjector = "B8.Projector";
        public const string UseHeliDoor = "B10.HeliDoor";

        /// <summary>Transmitter racks that stay invulnerable until D065 (GDD §7.9).</summary>
        public static readonly string[] RackIds = { "B9.R1", "B9.R2", "B9.R3", "B9.R4" };

        private static readonly GateDef[] s_all =
        {
            // ---- GDD §7.13 progression gates
            new GateDef(Weighbridge, GateKind.Gate, "entry to the Long Market", "3 s after `D020` starts (bolt cut)", 2, 4),
            new GateDef(CinemaSideDoor, GateKind.Gate, "the lobby", "player presses E", 4, 0),
            new GateDef(LadderL, GateKind.Gate, "the roofs", "every E4 enemy dead", 4, 0),
            new GateDef(R4Stair, GateKind.Gate, "the alley", "MID-A ends", 5, 0),
            new GateDef(HallDoor, GateKind.Gate, "the Hall", "squad at Y3 and player kicks (E)", 6, 0),
            new GateDef(ScreeningDoor, GateKind.Gate, "the screening room", "INTEL 1/3 and the stair wave dead", 7, 0),
            new GateDef(WestStairDoor, GateKind.Gate, "the basement", "INTEL 2/3", 8, 0),
            new GateDef(Racks, GateKind.Invulnerable, "the broadcast ending early", "`D065` starts", 9, 0, RackIds),
            new GateDef(TunnelDoor, GateKind.Gate, "the cable tunnel", "`D066`", 9, 0),
            new GateDef(HeliDoor, GateKind.Gate, "boarding", "`D076`", 10, 0),

            // ---- GDD §6.7 backtrack blockers (passable until their beat starts)
            new GateDef(BlockMarketSouth, GateKind.Blocker, "the Weighbridge (market south, z 165)", "Beat 4 start", 0, 4),
            new GateDef(BlockMarketArch, GateKind.Blocker, "the Long Market (arch, z 250)", "Beat 5 start", 0, 5),
            new GateDef(BlockSideAlley, GateKind.Blocker, "the Long Market (side alley, z 246)", "Beat 5 start", 0, 5),
            new GateDef(BlockR4Stair, GateKind.Blocker, "the roofs (R4 stair top)", "Beat 6 start", 0, 6),
            new GateDef(BlockBreach, GateKind.Blocker, "the alley (breach hole, x 300)", "Beat 7 start", 0, 7),
            new GateDef(BlockPostern, GateKind.Blocker, "the cable tunnel (postern)", "Beat 10 start", 0, 10),

            // ---- trigger arms and interactions driven by the same rules (Appendix A notes, GDD §7.4-7.10)
            new GateDef(ArmLanding, GateKind.TriggerArm, "Beat 8 start", "INTEL 1/3 and the stair wave dead", 7, 0),
            new GateDef(ArmBasementFoot, GateKind.TriggerArm, "Beat 9 start", "INTEL 2/3", 8, 0),
            new GateDef(UseSideDoor, GateKind.Interaction, "[E] Open", "Beat 4 start", 4, 5),
            new GateDef(UseHallDoor, GateKind.Interaction, "[E] Kick", "squad at Y3", 6, 7),
            new GateDef(UseProjector, GateKind.Interaction, "[E] Thread the projector", "Beat 8 start", 8, 9),
            new GateDef(UseHeliDoor, GateKind.Interaction, "[E] Board", "`D076`", 10, 11),
        };

        /// <summary>Every entry.</summary>
        public static IReadOnlyList<GateDef> All => s_all;

        /// <summary>An entry by id, or null.</summary>
        public static GateDef Get(string id)
        {
            for (var i = 0; i < s_all.Length; i++)
                if (s_all[i].Id == id) return s_all[i];
            return null;
        }

        /// <summary>The GDD §7.13 "Gate" cell of a progression gate (for the table-driven test), or null.</summary>
        public static string GddName(string id)
        {
            switch (id)
            {
                case Weighbridge: return "Weighbridge chained gate (183, 0, 163)";
                case CinemaSideDoor: return "Cinema side door (186, 0, 307)";
                case LadderL: return "PlayerClip at the foot of ladder L (236, 0, 301)";
                case R4Stair: return "`B5.T.Stair` PlayerClip at the R4 stair top (291, 12, 330)";
                case HallDoor: return "Relay Hall double door (338, 0.3, 330)";
                case ScreeningDoor: return "Screening-room door (330, 4.8, 353.5)";
                case WestStairDoor: return "Ground-floor west stair door (319, 0.3, 337)";
                case Racks: return "Racks R1–R4 invulnerable";
                case TunnelDoor: return "Tunnel door (344, −3, 359.8)";
                case HeliDoor: return "Helicopter door prompt";
                default: return null;
            }
        }
    }
}
