using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// GDD §7.0 pacing table and §6.7 streaming plan as code data (TECH_PLAN §7.11 BeatTable, D12): beat 0 is the intro
    /// (click gate → "SESSION 12" → INSERTION → shot 9), beats 1-11 are play, beat 12 is the outro and the debrief.
    /// Music cue semantics: null keeps whatever plays, "" stops the music at the beat start (the GDD §7.0 music column;
    /// cues that start mid-beat — Beat 2's LO on the first loud shot, MUS_03 in MID-A, MUS_04 on the Beat 6 blast — are
    /// started by the beat scripts). The intro and outro music belongs to WP09's Timelines (TL_Intro, TL_Outro).
    /// </summary>
    public static class BeatTable
    {
        /// <summary>Beat number of the intro.</summary>
        public const int Intro = 0;
        /// <summary>Number of playable beats (HUD "OBJ n/11").</summary>
        public const int PlayableBeats = 11;
        /// <summary>Beat number of the outro and debrief.</summary>
        public const int Outro = 12;

        private static readonly BeatDef[] s_beats = BuildBeats();
        private static readonly CheckpointDef[] s_checkpoints = BuildCheckpoints();

        /// <summary>Every beat 0..12.</summary>
        public static IReadOnlyList<BeatDef> Beats => s_beats;

        /// <summary>Every checkpoint in mission order (CP1, CP2, CP3, CP3b, CP4 … CP10).</summary>
        public static IReadOnlyList<CheckpointDef> Checkpoints => s_checkpoints;

        /// <summary>A beat by number (clamped to 0..12).</summary>
        public static BeatDef Get(int beat) => s_beats[Mathf.Clamp(beat, 0, s_beats.Length - 1)];

        /// <summary>A checkpoint by id, or null.</summary>
        public static CheckpointDef Checkpoint(string cpId)
        {
            if (string.IsNullOrEmpty(cpId)) return null;
            for (var i = 0; i < s_checkpoints.Length; i++)
                if (s_checkpoints[i].Id == cpId) return s_checkpoints[i];
            return null;
        }

        /// <summary>Beat of a checkpoint (-1 if unknown).</summary>
        public static int BeatOf(string cpId) => Checkpoint(cpId)?.Beat ?? -1;

        /// <summary>The checkpoint that opens a beat (Beat 11 reuses CP10, GDD §7.0; the intro and outro have none).</summary>
        public static string StartCheckpoint(int beat)
        {
            if (beat <= Intro) return null;
            if (beat >= PlayableBeats) return SequenceIds.CP10;
            var b = Get(beat);
            return b.Checkpoints.Length > 0 ? b.Checkpoints[0] : null;
        }

        /// <summary>
        /// Sections that are loaded when <paramref name="beat"/> starts in a normal run: the boot set, every beat-start load
        /// and quiet-point load of the earlier beats, minus the beat-start unloads (GDD §6.7). Cold jumps (?cp=, SELECT BEAT,
        /// the smoke test) request exactly this set. CP3b adds nothing (D027 comes after it).
        /// </summary>
        public static HashSet<SectionId> SectionsAtStart(int beat)
        {
            var set = new HashSet<SectionId> { SectionId.Booth, SectionId.TownFar, SectionId.Approach };
            for (var b = 0; b <= Mathf.Clamp(beat, 0, Outro); b++)
            {
                var def = s_beats[b];
                foreach (var s in def.LoadOnEnter) set.Add(s);
                foreach (var s in def.UnloadOnEnter) set.Remove(s);
                if (b < beat)
                    foreach (var q in def.QuietLoads) set.Add(q.Section);
            }
            return set;
        }

        /// <summary>The beat whose s range contains <paramref name="s"/> (diagnostics, the smoke test).</summary>
        public static int BeatForS(float s)
        {
            for (var b = 1; b <= PlayableBeats; b++)
                if (s < s_beats[b].SEnd) return b;
            return PlayableBeats;
        }

        private static BeatDef B(int n, string name, float s0, float s1, string clock, string[] cps, string music, int layer,
                                 float fade, string[] objectives, string enter, string exit, SectionId? section,
                                 SectionId[] load = null, SectionId[] unload = null, QuietLoad[] quiet = null,
                                 string[] close = null, string[] crates = null) =>
            new BeatDef(n, name, s0, s1, clock, cps, music, layer, fade, objectives, enter, exit, section, load, unload, quiet,
                close, crates);

        private static string[] S(params string[] s) => s;

        private static BeatDef[] BuildBeats()
        {
            const int Lo = MissionCues.LayerLow;
            const int Hi = MissionCues.LayerHigh;
            return new[]
            {
                // GDD §7.0 row "Click gate, Intro, title card": booth → 0.00; MUS_01_SESSION is on TL_Intro (WP09).
                B(Intro, "SESSION 12", 0f, 0f, "03:12", S(), null, 0, 0f, S(),
                    "click gate", SequenceIds.IntroControlToPlayer, null,
                    load: new[] { SectionId.Booth, SectionId.TownFar, SectionId.Approach }),
                // GDD §7.1; §6.7: S2_Market at Beat 1 start (title card). MUS_01 tail fades out by 0:10.
                B(1, "THE SHOULDER", 0.00f, 0.15f, "17:58", S(SequenceIds.CP1), "", 0, 8f,
                    S(ObjectiveTable.B1Observe, ObjectiveTable.B1Follow, ObjectiveTable.B1Grove),
                    "intro shot 9 white-out ends", MissionTriggers.B1GroveEnter, SectionId.Approach,
                    load: new[] { SectionId.Market }),
                // GDD §7.2; §6.7: S3_Square at Beat 2 start, S4_Roofs on D019. MUS_02 LO starts on the first loud shot.
                B(2, "PUMP HOUSE SIX", 0.15f, 0.45f, "18:08", S(SequenceIds.CP2), "", 0, 2f,
                    S(ObjectiveTable.B2Sentries, ObjectiveTable.B2Weighbridge, ObjectiveTable.B2Enter),
                    MissionTriggers.B1GroveEnter, MissionTriggers.B2Exit, SectionId.Approach,
                    load: new[] { SectionId.Square },
                    quiet: new[] { new QuietLoad(LineIds.D019, SectionId.Roofs) },
                    crates: S(MissionMarkers.CrateWeighbridge)),
                // GDD §7.3; §6.7: S5_StationExt on D027.
                B(3, "THE LONG MARKET", 0.45f, 1.00f, "18:17", S(SequenceIds.CP3, SequenceIds.CP3b), MissionCues.MusicMarket, Lo, 2f,
                    S(ObjectiveTable.B3Push),
                    MissionTriggers.B2Exit, MissionTriggers.B3Exit, SectionId.Market,
                    quiet: new[] { new QuietLoad(LineIds.D027, SectionId.StationExt) }),
                // GDD §7.4; §6.7: S1_Approach unloads, Weighbridge closes behind, S6_StationInt on D034.
                B(4, "CLOCK SQUARE", 1.00f, 1.50f, "18:26", S(SequenceIds.CP4), MissionCues.MusicMarket, Hi, 1.5f,
                    S(ObjectiveTable.B4Flank, ObjectiveTable.B4Technical, ObjectiveTable.B4Regroup, ObjectiveTable.B4UpTop),
                    "squad at B3.NodeSet.M4", "ladder top (CP5)", SectionId.Square,
                    unload: new[] { SectionId.Approach },
                    quiet: new[] { new QuietLoad(LineIds.D034, SectionId.StationInt) },
                    close: S(GateTable.Weighbridge, GateTable.BlockMarketSouth),
                    crates: S(MissionMarkers.CrateMarketArch)),
                // GDD §7.5; §6.7: S2_Market unloads, S7_Pitch at MID-A start. MUS_03 swells in MID-A (0:05).
                B(5, "LAST LIGHT", 1.50f, 2.00f, "18:34", S(SequenceIds.CP5), "", 0, 4f,
                    S(ObjectiveTable.B5Wall, ObjectiveTable.B5Breach),
                    "ladder top", MissionTriggers.B6Alley, SectionId.Roofs,
                    unload: new[] { SectionId.Market },
                    quiet: new[] { new QuietLoad(SequenceIds.MidAStart, SectionId.Pitch) },
                    close: S(GateTable.BlockMarketArch, GateTable.BlockSideAlley),
                    crates: S(MissionMarkers.CrateR4)),
                // GDD §7.6; §6.7: S3_Square unloads. MUS_04 from the blast.
                B(6, "THE RELAY YARD", 2.00f, 2.40f, "18:41", S(SequenceIds.CP6), "", 0, 2f,
                    S(ObjectiveTable.B6StandClear, ObjectiveTable.B6Yard, ObjectiveTable.B6Hall),
                    MissionTriggers.B6Alley, MissionMarkers.HallDoor, SectionId.StationExt,
                    unload: new[] { SectionId.Square },
                    close: S(GateTable.BlockR4Stair)),
                // GDD §7.7; §6.7: S4_Roofs unloads, S8_Heli on D051. MUS_04 low layer.
                B(7, "THE RELAY HALL", 2.40f, 2.60f, "18:50", S(SequenceIds.CP7), MissionCues.MusicStation, Lo, 1.5f,
                    S(ObjectiveTable.B7Breach, ObjectiveTable.B7Ledgers, ObjectiveTable.B7Upstairs, ObjectiveTable.B7O1),
                    MissionMarkers.HallDoor, MissionTriggers.B7Landing, SectionId.StationInt,
                    unload: new[] { SectionId.Roofs },
                    quiet: new[] { new QuietLoad(LineIds.D051, SectionId.Heli) },
                    close: S(GateTable.BlockBreach)),
                // GDD §7.8: no music (projector).
                B(8, "THE REEL", 2.60f, 2.80f, "18:54", S(SequenceIds.CP8), "", 0, 3f,
                    S(ObjectiveTable.B8Thread, ObjectiveTable.B8Film, ObjectiveTable.B8Basement, ObjectiveTable.B8O2),
                    MissionTriggers.B7Landing, MissionTriggers.B8BasementFoot, SectionId.StationInt),
                // GDD §7.9: MUS_04 full. The beat ends at the postern (the tunnel is still S6_StationInt).
                B(9, "THE CLOCKMAKER", 2.80f, 3.20f, "18:58", S(SequenceIds.CP9), MissionCues.MusicStation, Hi, 2f,
                    S(ObjectiveTable.B9Defend, ObjectiveTable.B9Codebook, ObjectiveTable.B9Transmitter, ObjectiveTable.B9Exfil),
                    MissionTriggers.B8BasementFoot, MissionTriggers.B10Postern, SectionId.StationInt,
                    crates: S(MissionMarkers.CrateBasement)),
                // GDD §7.10; §6.7: S6_StationInt unloads, the postern closes behind.
                B(10, "THE PITCH", 3.20f, 3.70f, "19:05", S(SequenceIds.CP10), MissionCues.MusicExfil, 0, 2f,
                    S(ObjectiveTable.B10LZ, ObjectiveTable.B10Defend, ObjectiveTable.B10Rpg, ObjectiveTable.B10Board),
                    MissionTriggers.B10Postern, MissionMarkers.HeliDoor, SectionId.Pitch,
                    unload: new[] { SectionId.StationInt },
                    close: S(GateTable.BlockPostern),
                    crates: S(MissionMarkers.CrateLZ)),
                // GDD §7.11: on rails; MUS_05 until the RPG launch, then a hard cut.
                B(11, "MAGPIE", 3.70f, 4.00f, "19:10", S(), null, 0, 0f,
                    S(ObjectiveTable.B11Cover),
                    MissionMarkers.HeliDoor, "climax ends (cut to white)", SectionId.Heli),
                // GDD §8.8-8.9; §6.7: S7/S8 "end", S5 after the outro.
                B(Outro, "SIX-FORTY", 4.00f, 4.00f, "03:40", S(), null, 0, 0f, S(),
                    "white", "debrief", null),
            };
        }

        /// <summary>Sections unloaded once the debrief opens (GDD §6.7 "end", "after the outro").</summary>
        public static readonly SectionId[] UnloadAfterOutro = { SectionId.StationExt, SectionId.Pitch, SectionId.Heli };

        private static CheckpointDef C(string id, int beat, float x, float y, float z, float yaw, string nodes, float s, bool start = true) =>
            new CheckpointDef(id, beat, new Vector3(x, y, z), yaw, nodes, s, start);

        // Appendix A "CP" positions; yaw faces the beat's first objective (the CheckpointMarker in the scene wins).
        private static CheckpointDef[] BuildCheckpoints() => new[]
        {
            C(SequenceIds.CP1, 1, 42f, 22f, 30f, 45f, "B1.Squad.Start", 0.00f),
            C(SequenceIds.CP2, 2, 108f, 0f, 96f, 60f, null, 0.15f),
            C(SequenceIds.CP3, 3, 185f, 0f, 170f, 0f, "B3.NodeSet.M1", 0.45f),
            C(SequenceIds.CP3b, 3, 185f, 0f, 216f, 0f, "B3.NodeSet.M3", 0.60f, false),
            C(SequenceIds.CP4, 4, 185f, 0f, 248f, 0f, "B4.NodeSet.Q1", 1.00f),
            C(SequenceIds.CP5, 5, 236f, 9f, 304f, 30f, "B5.NodeSet.R1", 1.50f),
            C(SequenceIds.CP6, 6, 295f, 0f, 334f, 180f, null, 2.00f),
            C(SequenceIds.CP7, 7, 338f, 0f, 327f, 0f, "B6.NodeSet.Y3", 2.40f),
            C(SequenceIds.CP8, 8, 335f, 4.8f, 355f, 255f, "B8.NodeSet.Reel", 2.60f),
            C(SequenceIds.CP9, 9, 320.5f, -4.2f, 343f, 66f, "B9.NodeSet.Defend", 2.80f),
            C(SequenceIds.CP10, 10, 346f, 0f, 383f, 0f, "B10.NodeSet.P1", 3.20f),
        };
    }
}
