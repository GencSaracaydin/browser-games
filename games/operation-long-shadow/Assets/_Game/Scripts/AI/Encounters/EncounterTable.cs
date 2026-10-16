using System.Collections.Generic;
using LongShadow.AI.Common;
using LongShadow.AI.Enemy;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// Every encounter of GDD §7 (E2-E11, 59 enemies) as code data (TECH_PLAN D12, §7.6): enemy id, archetype, position,
    /// facing, spawn kind, wave, trigger, delay, flank route and behaviour notes; max alive per encounter and the hard
    /// cap of 12 (GDD §14.3). Marker, closet, route and perch ids are Appendix A ids (checked against
    /// markup_ids.csv by the EditMode tests). Wave triggers are evaluated by the spawner; WP07 beat scripts may also
    /// call TriggerWave/Activate directly (both are idempotent).
    /// </summary>
    public static class EncounterTable
    {
        /// <summary>Hard cap on living enemies across all encounters (GDD §14.3).</summary>
        public const int HardCap = 12;
        /// <summary>Total enemies in the mission (GDD §14.1).</summary>
        public const int MissionTotal = 59;

        /// <summary>
        /// Beat 10 trucks: riders board this many seconds before the GDD arrival time (t 8 / t 40) so the truck, started
        /// by the spawner when idle, drives in and they dismount on arrival (WP10 authors B10.Truck1/2 at about 4 s).
        /// </summary>
        public const float VehicleLeadSeconds = 4f;

        public const string E2 = "E2";
        public const string E3 = "E3";
        public const string E4 = "E4";
        public const string E5 = "E5";
        public const string E6 = "E6";
        public const string E7 = "E7";
        public const string E9 = "E9";
        public const string E10 = "E10";
        public const string E11 = "E11";

        private static readonly List<EncounterDef> s_all = Build();

        /// <summary>Every encounter in mission order.</summary>
        public static IReadOnlyList<EncounterDef> All => s_all;

        /// <summary>An encounter by id, or null.</summary>
        public static EncounterDef Get(string id)
        {
            for (var i = 0; i < s_all.Count; i++)
                if (s_all[i].Id == id) return s_all[i];
            return null;
        }

        /// <summary>Finds the encounter and definition of an enemy id.</summary>
        public static bool TryFindEnemy(string enemyId, out EncounterDef encounter, out EnemySpawnDef enemy)
        {
            for (var i = 0; i < s_all.Count; i++)
            {
                var e = s_all[i].GetEnemy(enemyId);
                if (e == null) continue;
                encounter = s_all[i];
                enemy = e;
                return true;
            }
            encounter = null;
            enemy = null;
            return false;
        }

        /// <summary>Total enemies over all encounters.</summary>
        public static int TotalEnemies
        {
            get
            {
                var n = 0;
                for (var i = 0; i < s_all.Count; i++) n += s_all[i].Enemies.Count;
                return n;
            }
        }

        private static EnemySpawnDef E(string id, ArchetypeId a, float x, float y, float z, float heading) =>
            new EnemySpawnDef(id, a, new Vector3(x, y, z), heading);

        private static List<EncounterDef> Build()
        {
            const ArchetypeId R = ArchetypeId.LegionRifleman;
            const ArchetypeId G = ArchetypeId.LegionGunner;
            const ArchetypeId Gc = ArchetypeId.GreycoatAdvisor;
            var list = new List<EncounterDef>();

            // ---- E2 PUMP HOUSE SIX (GDD §7.2): 6 total, max alive 4 ---------------------------------------------------
            var e2 = new EncounterDef(E2, 2, SectionId.Approach, 4)
            {
                Notes = "Silent opener: sentries smoke and chat; gate team idle facing south unless alerted (AI.Alert.E2.Gate, " +
                        "B2.T.GateFight, or hearing an unsuppressed shot within 60 m). Hollis kills E2-02 on 'Mark' (ScriptedKill).",
            };
            e2.AddWave(new WaveDef("Sentries", WaveTrigger.OnActivate()).Note("Smoking idle; 30 HP while unaware."));
            e2.AddWave(new WaveDef("Gate", WaveTrigger.OnActivate())
                .AlertWhen(WaveTrigger.Any(WaveCondition.Event("B2.T.GateFight")))
                .Cover("B2.AlertCover")
                .Note("Gate fight trigger: player crosses z > 136, the alert branch, or 25 s after the sync shot (WP07 raises AI.Alert.E2.Gate)."));
            e2.Add(E("E2-01", R, 146f, 0f, 125f, 200f).InWave("Sentries").AsSentry().Note("Sentry L, smoking idle. 30 HP while unaware. Runs north on AI.Run.E2-01 (failure branch)."));
            e2.Add(E("E2-02", R, 154f, 0f, 126f, 160f).InWave("Sentries").AsSentry().Note("Sentry R, Hollis's target."));
            e2.Add(E("E2-03", R, 171f, 0f, 150f, 190f).InWave("Gate").Note("Guard hut; cassette radio plays in the hut."));
            e2.Add(E("E2-04", R, 186f, 0f, 146f, 200f).InWave("Gate").Note("Barrier blocks."));
            e2.Add(E("E2-05", R, 180f, 0f, 158f, 180f).InWave("Gate").Note("Arch; falls back to the arch if pushed (BK_LEG_09)."));
            e2.Add(E("E2-06", G, 190f, 2.2f, 150f, 200f).Perch("B2.Perch.TruckBed").InWave("Gate").Priority(0).Drops(false)
                .Note("RPK on the truck bed, long bursts, green tracers. Frag in the bed = kill. RPK stays on its mount."));
            list.Add(e2);

            // ---- E3 THE LONG MARKET (GDD §7.3): 9 total, max alive 6 ------------------------------------------------
            var e3 = new EncounterDef(E3, 3, SectionId.Market, 6);
            e3.AddWave(new WaveDef("A", WaveTrigger.OnActivate()).Note("Wave A behind the stalls."));
            e3.AddWave(new WaveDef("Balcony", WaveTrigger.Any(WaveCondition.Event("B3.T.Z185"))).Alerted()
                .Note("Player z > 185."));
            e3.AddWave(new WaveDef("B", WaveTrigger.Any(WaveCondition.Event("B3.T.Crossing"), WaveCondition.WaveAliveAtMost("A", 2))).Alerted()
                .Note("Player z > 216 OR wave A ≤ 2 alive. Tinsmiths' Lane south, cross-passage at z 197, flanking from behind-left."));
            e3.Add(E("E3-01", R, 183f, 0f, 192f, 180f).InWave("A").Note("Behind a stall."));
            e3.Add(E("E3-02", R, 187f, 0f, 196f, 180f).InWave("A"));
            e3.Add(E("E3-03", R, 184f, 0f, 203f, 180f).InWave("A"));
            e3.Add(E("E3-04", R, 186.5f, 0f, 207f, 180f).InWave("A"));
            e3.Add(E("E3-05", G, 190.5f, 4.2f, 222f, 225f).Closet("SC_B3_BAL").Perch("B3.Perch.Balcony").InWave("Balcony").Drops(true)
                .Note("Balcony gunner; shreds the awnings; drops an RPK pickup to the street."));
            e3.Add(E("E3-06", R, 178.5f, 8f, 214f, 135f).Closet("SC_B3_ROOF").Perch("B3.Perch.WestRoof").InWave("Balcony")
                .Note("West roof, with E3-05."));
            e3.Add(E("E3-07", R, 168f, 0f, 240f, 180f).Closet("SC_B3_N").InWave("B", 0f).Flank("B3.Route.WaveB"));
            e3.Add(E("E3-08", R, 168f, 0f, 240f, 180f).Closet("SC_B3_N").InWave("B", 1f).Flank("B3.Route.WaveB"));
            e3.Add(E("E3-09", R, 168f, 0f, 240f, 180f).Closet("SC_B3_N").InWave("B", 2f).Flank("B3.Route.WaveB"));
            list.Add(e3);

            // ---- E4 CLOCK SQUARE / CINEMA ZENITH (GDD §7.4): 8 total, max alive 6 --------------------------------------
            var e4 = new EncounterDef(E4, 4, SectionId.Square, 6);
            e4.AddWave(new WaveDef("Square", WaveTrigger.OnActivate()).Alerted().Note("Pinning the squad at the arch."));
            e4.AddWave(new WaveDef("Lobby", WaveTrigger.OnActivate()).Note("Ambush; holds until the side door opens."));
            e4.AddWave(new WaveDef("Greycoat", WaveTrigger.Any(WaveCondition.EnemyDead("E4-01", 4f))).Alerted()
                .Note("Gunner dead + 4 s; 3 clicks + BK_GRY_01."));
            e4.Add(E("E4-01", ArchetypeId.TechnicalGunner, 232f, 1.6f, 286f, 234f).AtMarker("B4.Technical").Perch().InWave("Square")
                .Note("DShK, 3-round bursts at the arch (scripted, 2 s after D028). Immune within ±60° of 234°. Truck ignites 1.5 s after death."));
            e4.Add(E("E4-02", R, 226f, 0f, 266f, 220f).InWave("Square").SquadFirstUntil("B4.T.Marquee").Note("Tower east; engages the arch."));
            e4.Add(E("E4-03", R, 240f, 0f, 272f, 240f).InWave("Square").SquadFirstUntil("B4.T.Marquee").Note("East arcade; retargets to the player on the marquee."));
            e4.Add(E("E4-04", R, 206f, 0f, 290f, 200f).InWave("Square").SquadFirstUntil("B4.T.Marquee").Note("North arcade below the marquee; retargets to the player on the marquee."));
            e4.Add(E("E4-05", R, 192f, 0f, 304f, 270f).InWave("Lobby").HoldUntilEvent("B4.T.SideDoor", "B4.SideDoor").Note("Lobby ambush."));
            e4.Add(E("E4-06", R, 196f, 0f, 309f, 270f).InWave("Lobby").HoldUntilEvent("B4.T.SideDoor", "B4.SideDoor").Note("Lobby ambush."));
            e4.Add(E("E4-07", Gc, 252f, 0f, 276f, 270f).Closet("SC_B4_E").InWave("Greycoat").Pair("E4.Pair", PairRole.Flanker).Flank("B4.Route.North")
                .Note("Flanks along the north arcade to (240, 0, 292)."));
            e4.Add(E("E4-08", Gc, 252f, 0f, 276f, 270f).Closet("SC_B4_E").InWave("Greycoat").Pair("E4.Pair", PairRole.Suppressor, "B4.SuppressPos")
                .Note("Pair partner; suppresses from (242, 0, 262) on the south arcade."));
            list.Add(e4);

            // ---- E5 LAST LIGHT (GDD §7.5): 3 total, max alive 3 ---------------------------------------------------------
            var e5 = new EncounterDef(E5, 5, SectionId.Roofs, 3);
            e5.AddWave(new WaveDef("Main", WaveTrigger.OnActivate()));
            e5.Add(E("E5-01", ArchetypeId.GreycoatMarksman, 378f, 25f, 362f, 250f).AtMarker("B5.Perch.Mast").Perch("B5.Perch.Mast").InWave("Main")
                .Note("Mast platform, ~120 m. Glint 1.5 s before each shot. 160 m sight. Ceases fire at MIDA.Start (BK_GRY_05), despawns at s ≥ 2.0."));
            e5.Add(E("E5-02", R, 267f, 10f, 324f, 270f).Perch().InWave("Main").Note("Guards the R3 nest (no roaming on roofs)."));
            e5.Add(E("E5-03", R, 272f, 10f, 329f, 260f).Perch().InWave("Main").Note("Guards the R3 nest."));
            list.Add(e5);

            // ---- E6 THE RELAY YARD (GDD §7.6): 8 total, max alive 6 ----------------------------------------------------
            var e6 = new EncounterDef(E6, 6, SectionId.StationExt, 6);
            e6.AddWave(new WaveDef("Yard", WaveTrigger.OnActivate())
                .AlertWhen(WaveTrigger.Any(WaveCondition.Event(AIEvents.B6Blast), WaveCondition.Event("B6.BreachWall")))
                .Note("Alerted by the blast."));
            e6.AddWave(new WaveDef("Gatehouse", WaveTrigger.Any(
                        WaveCondition.KilledAtLeast(3),
                        WaveCondition.AfterEvent(AIEvents.B6Blast, 25f),
                        WaveCondition.AfterEvent("B6.BreachWall", 25f)))
                .OnlyWhileAliveAtMost(3).Alerted()
                .Note("≥ 3 yard enemies dead OR 25 s after the blast (only while ≤ 3 are alive)."));
            e6.Add(E("E6-01", R, 316f, 0f, 305f, 290f).InWave("Yard").Note("Sandbags."));
            e6.Add(E("E6-02", R, 332f, 0f, 318f, 270f).InWave("Yard").Note("Cable drums."));
            e6.Add(E("E6-03", R, 350f, 0f, 312f, 280f).InWave("Yard").Note("Fuel bowser (not explosive)."));
            e6.Add(E("E6-04", R, 362f, 0f, 322f, 270f).InWave("Yard").Note("Crates."));
            e6.Add(E("E6-05", G, 346f, 4f, 294f, 300f).Perch("B6.Perch.Gatehouse").InWave("Yard").Drops(true).Note("Gatehouse roof; suppresses the breach; drops an RPK."));
            e6.Add(E("E6-06", Gc, 346f, 0f, 298f, 315f).Closet("SC_B6_GATE").InWave("Gatehouse", 0f).Pair("E6.Pair", PairRole.Flanker).Flank("B6.Route.SouthWall")
                .Note("Pair flanks along the south wall."));
            e6.Add(E("E6-07", Gc, 346f, 0f, 298f, 315f).Closet("SC_B6_GATE").InWave("Gatehouse", 1f).Pair("E6.Pair", PairRole.Suppressor)
                .Note("Pair partner (+1.0 s)."));
            e6.Add(E("E6-08", Gc, 346f, 0f, 298f, 315f).Closet("SC_B6_GATE").InWave("Gatehouse", 1.5f)
                .HoldThenPush(6f, new Vector3(332f, 0f, 318f), BarkIds.GryHold)
                .Note("Holds (BK_GRY_03), then pushes to the cable drums."));
            list.Add(e6);

            // ---- E7 THE RELAY HALL (GDD §7.7): 5 total, max alive 3 ----------------------------------------------------
            var e7 = new EncounterDef(E7, 7, SectionId.StationInt, 3);
            e7.AddWave(new WaveDef("Breach", WaveTrigger.OnActivate()).Alerted().Note("Slow-mo breach targets."));
            e7.AddWave(new WaveDef("Stair", new WaveTrigger()
                    .Or(WaveCondition.WaveDead("Breach"), WaveCondition.PlayerIn("B7.T.OfficeDoor"))
                    .Or(WaveCondition.After(25f)))
                .Alerted()
                .Note("Breach enemies dead AND player within 6 m of the office door, OR 25 s after the breach."));
            e7.Add(E("E7-01", R, 333f, 0.3f, 340f, 180f).InWave("Breach").Note("Behind teletype row 1."));
            e7.Add(E("E7-02", R, 343f, 0.3f, 336f, 200f).InWave("Breach").Note("Archive door."));
            e7.Add(E("E7-03", Gc, 328f, 0.3f, 353f, 160f).InWave("Breach").Note("Behind row 3; 3 clicks on breach."));
            e7.Add(E("E7-04", R, 334.5f, 4.8f, 358f, 180f).Closet("SC_B7_UP").InWave("Stair", 0f).Note("Spiral stair; comes down."));
            e7.Add(E("E7-05", R, 334.5f, 4.8f, 358f, 180f).Closet("SC_B7_UP").InWave("Stair", 1.5f));
            list.Add(e7);

            // ---- E9 THE CLOCKMAKER (GDD §7.9): 9 total, max alive 5. t = seconds since the drill started (activation) ---
            var e9 = new EncounterDef(E9, 9, SectionId.StationInt, 5)
            {
                Notes = "Activate when the drill starts (D060): wave timers are wall-clock seconds since then.",
            };
            e9.AddWave(new WaveDef("W1", WaveTrigger.OnActivate()).Alerted().Note("Drill start; down the west stair."));
            e9.AddWave(new WaveDef("W2", WaveTrigger.Any(WaveCondition.After(30f), WaveCondition.WaveAliveAtMost("W1", 1))).Alerted()
                .Note("t = 30 s OR wave 1 ≤ 1 alive; down the east stair; one frag on arrival; 2 clicks then 3."));
            e9.AddWave(new WaveDef("W3", WaveTrigger.Any(WaveCondition.After(60f), WaveCondition.WaveAliveAtMost("W2", 1))).Alerted()
                .Note("t = 60 s OR wave 2 ≤ 1 alive; up through the hatch."));
            e9.Add(E("E9-01", R, 318f, 0.3f, 336f, 180f).Closet("SC_B9_W").InWave("W1", 0f));
            e9.Add(E("E9-02", R, 318f, 0.3f, 336f, 180f).Closet("SC_B9_W").InWave("W1", 0.8f));
            e9.Add(E("E9-03", R, 318f, 0.3f, 336f, 180f).Closet("SC_B9_W").InWave("W1", 1.6f));
            e9.Add(E("E9-04", Gc, 351f, 0.3f, 336f, 180f).Closet("SC_B9_E").InWave("W2", 0f).FragFirst().Pair("E9.Pair", PairRole.Suppressor));
            e9.Add(E("E9-05", Gc, 351f, 0.3f, 336f, 180f).Closet("SC_B9_E").InWave("W2", 0.8f).Pair("E9.Pair", PairRole.Flanker));
            e9.Add(E("E9-06", Gc, 351f, 0.3f, 336f, 180f).Closet("SC_B9_E").InWave("W2", 1.6f));
            e9.Add(E("E9-07", R, 332f, -6f, 331f, 0f).Closet("SC_B9_H").InWave("W3", 0f).Note("Cable trench, up through the hatch (332, -4.2, 334.3)."));
            e9.Add(E("E9-08", R, 332f, -6f, 331f, 0f).Closet("SC_B9_H").InWave("W3", 1f));
            e9.Add(E("E9-09", Gc, 351f, 0.3f, 336f, 180f).Closet("SC_B9_E").InWave("W3", 3f).Note("Wave 3 (+3 s)."));
            list.Add(e9);

            // ---- E10 THE PITCH (GDD §7.10): 7 total, max alive 7. t = seconds since the hold started (activation) -------
            var e10 = new EncounterDef(E10, 10, SectionId.Pitch, 7)
            {
                Notes = "Activate at the end of D070 (hold start). Truck splines B10.Truck1/2 are World SplineVehicles; " +
                        "the riders ride them and dismount on arrival (the spawner starts a truck that is not moving yet).",
            };
            e10.AddWave(new WaveDef("Truck1", WaveTrigger.Any(WaveCondition.After(8f - VehicleLeadSeconds))).Alerted()
                .Note("Arrives t = 8 s (riders board at t 4); push west along the north edge (z 412)."));
            e10.AddWave(new WaveDef("NVG", WaveTrigger.Any(WaveCondition.After(20f))).Alerted().Note("t = 20 s; NVG flanker from the west."));
            e10.AddWave(new WaveDef("Truck2", WaveTrigger.Any(WaveCondition.After(40f - VehicleLeadSeconds))).Alerted()
                .Note("Arrives t = 40 s (riders board at t 36); 2 Legion + the RPG gunner on the bed."));
            var north = new[] { new Vector3(384f, -1f, 410f), new Vector3(366f, -1f, 412f), new Vector3(352f, -1f, 412f) };
            e10.Add(E("E10-01", R, 394f, -1f, 396f, 270f).Vehicle("B10.Truck1", 1).InWave("Truck1", 0f).Along(north));
            e10.Add(E("E10-02", R, 394f, -1f, 396f, 270f).Vehicle("B10.Truck1", 2).InWave("Truck1", 0f).Along(north));
            e10.Add(E("E10-03", R, 394f, -1f, 396f, 270f).Vehicle("B10.Truck1", 3).InWave("Truck1", 0f).Along(north));
            e10.Add(E("E10-04", Gc, 300f, -1f, 388f, 90f).Closet("SC_B10_W").InWave("NVG").WithNvg().AsVariant(EnemyVariants.GreycoatNvg)
                .Flank("B10.Route.West").Note("Green lens glow is his tell. BK_GRY_09 once at FL1."));
            e10.Add(E("E10-05", R, 362f, -1f, 424f, 180f).Vehicle("B10.Truck2", 1).InWave("Truck2", 0f));
            e10.Add(E("E10-06", R, 362f, -1f, 424f, 180f).Vehicle("B10.Truck2", 2).InWave("Truck2", 0f));
            e10.Add(E("E10-07", ArchetypeId.LegionRPG, 362f, 1f, 424f, 200f).Vehicle("B10.Truck2", 3, staysOnVehicle: true).InWave("Truck2", 0f)
                .Note("Shouts and raises the launcher 1.2 s before firing. Targets the squad (bleacher) until t 60, then Magpie at t 63; SquadTarget after the wave-off."));
            list.Add(e10);

            // ---- E11 MAGPIE (GDD §7.11): 4 rooftop targets; t = exfil seconds (B11.Exfil SplineVehicle) -----------------
            var e11 = new EncounterDef(E11, 11, SectionId.Heli, 4)
            {
                Notes = "Always aware of the helicopter; each fires only in its window. Trucks are World/Combat destructibles (ExfilSchedule).",
            };
            e11.AddWave(new WaveDef("Targets", WaveTrigger.OnActivate()).Alerted());
            e11.Add(E("E11-T1", ArchetypeId.RooftopTarget, 346f, 6f, 294f, 0f).AtMarker("B11.T1").Perch().InWave("Targets")
                .Window(ExfilSchedule.T1Start, ExfilSchedule.T1End).Note("Gatehouse roof (old E6-05 perch)."));
            e11.Add(E("E11-T2", ArchetypeId.RooftopTarget, 285f, 9f, 268f, 0f).AtMarker("B11.T2").Perch().InWave("Targets")
                .Window(ExfilSchedule.T2Start, ExfilSchedule.T2End).Note("Housing roof east of Clock Square."));
            e11.Add(E("E11-T3", ArchetypeId.RooftopTarget, 224f, 10f, 226f, 0f).AtMarker("B11.T3").Perch().InWave("Targets")
                .AsVariant(EnemyVariants.RooftopRpg).Weapon(WeaponIds.RPG7)
                .Window(ExfilSchedule.T3Start, ExfilSchedule.T3End).HiddenUntilThenFire(ExfilSchedule.T3Start, ExfilSchedule.T3FireAt)
                .Note("The RPG man: hidden until t 28, raises 1.2 s, fires at t 30 whatever happens."));
            e11.Add(E("E11-T4", ArchetypeId.RooftopTarget, 236f, 9f, 238f, 0f).AtMarker("B11.T4").Perch().InWave("Targets")
                .Window(ExfilSchedule.T4Start, ExfilSchedule.T4End).Note("Housing roof south-east of the square."));
            list.Add(e11);

            return list;
        }
    }
}
