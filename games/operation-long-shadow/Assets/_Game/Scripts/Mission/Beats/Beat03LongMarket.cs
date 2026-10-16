using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 3 THE LONG MARKET (GDD §7.3): leapfrog M1 → M2 (wave A ≤ 2 alive or z &gt; 200) → M3 (after the Crossing) →
    /// M4 (market clear); D021 entry, D022 balcony gunner, D023 first torn awning, D024 when the squad is at M2 with
    /// Tinker's white smoke at the Crossing, CP3b + D025 PA at the Crossing, D026 when wave B leaves the z 197 passage,
    /// D027 market clear (S5_StationExt loads); music LO → HI on wave B → LO. Exit: squad at M4 and the player within 8 m
    /// of the arch. Resumes at CP3 (beat start) or CP3b (the Crossing).
    /// </summary>
    public sealed class Beat03LongMarket : BeatScript
    {
        /// <summary>D026 fallback: seconds after wave B spawns when no wave-B position is known.</summary>
        public const float D026Fallback = 7f;
        /// <summary>D022 fallback: seconds after the balcony wave spawns without an RPK burst.</summary>
        public const float D022Fallback = 3f;

        private static readonly string[] s_awnings =
        {
            "B3.AW01", "B3.AW02", "B3.AW03", "B3.AW04", "B3.AW05", "B3.AW06", "B3.AW07", "B3.AW08",
        };

        /// <inheritdoc/>
        public override int Number => 3;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            var fromCrossing = entryCheckpoint == SequenceIds.CP3b;
            if (fromCrossing) ctx.Checkpoint(SequenceIds.CP3b);
            else SaveStartCheckpoint(ctx); // CP3 at (185, 0, 170)
            yield return WaitForSection(ctx);
            ctx.Activate(EncounterIds.E3);
            ctx.Objective(ObjectiveTable.B3Push);
            ctx.Run(BalconyLine(ctx), "B3 D022");
            ctx.Run(AwningLine(ctx), "B3 D023");
            ctx.Run(WaveBLines(ctx), "B3 wave B");

            if (!fromCrossing)
            {
                ctx.Nodes(MissionMarkers.B3NodeSetM1);
                ctx.Line(LineIds.D021);
                // M2 when wave A ≤ 2 alive or the player passes z > 200.
                yield return Wait.Any(ctx.AliveAtMost(EncounterIds.E3, EncounterIds.E3A, 2), ctx.Trigger(MissionTriggers.B3Z200),
                    ctx.Trigger(MissionTriggers.B3Crossing), ctx.Trigger(MissionTriggers.B3Exit));
                ctx.Nodes(MissionMarkers.B3NodeSetM2);
                ctx.Run(SmokeAtM2(ctx), "B3 D024 smoke");
                yield return Wait.Any(ctx.Trigger(MissionTriggers.B3Crossing), ctx.Trigger(MissionTriggers.B3Exit));
                ctx.Checkpoint(SequenceIds.CP3b); // deferred while enemies are within 10 m (GDD §17)
            }
            else
            {
                // The player stands at the Crossing: wave B's own trigger ("player z > 216") is already behind us.
                ctx.Wave(EncounterIds.E3, EncounterIds.E3B);
            }

            ctx.Line(LineIds.D025);
            ctx.Nodes(MissionMarkers.B3NodeSetM3);

            yield return ctx.Cleared(EncounterIds.E3);
            ctx.Line(LineIds.D027);
            ctx.PlayMusic(MissionCues.MusicMarket, MissionCues.LayerLow, 2f);
            ctx.Nodes(MissionMarkers.B3NodeSetM4);

            // Exit: squad at M4 and the player within 8 m of the arch.
            yield return Wait.All(ctx.SquadAt(MissionMarkers.B3NodeSetM4),
                new Wait(WaitKind.Trigger, MissionTriggers.B3Exit, () => ctx.Inside(MissionTriggers.B3Exit) || ctx.IsNear(MissionMarkers.B3ArchTarget, 8f)));
            ctx.Complete(ObjectiveTable.B3Push);
        }

        private static IEnumerator SmokeAtM2(MissionContext ctx)
        {
            yield return ctx.SquadAt(MissionMarkers.B3NodeSetM2);
            ctx.Line(LineIds.D024);
            ctx.SquadActionNoWait(SquadIds.Tinker, ActionIds.ThrowSmokeWhite, MissionMarkers.B3Smoke);
        }

        private static IEnumerator BalconyLine(MissionContext ctx)
        {
            // D022 "Balcony gunner spawns and fires".
            yield return ctx.WaveSpawnedWait(EncounterIds.E3, EncounterIds.E3Balcony);
            if (ctx.IsCleared(EncounterIds.E3)) yield break;
            var spawned = ctx.Now;
            yield return Wait.Until(() => ctx.Log.LastEnemyShot(WeaponIds.RPK) >= spawned || ctx.Now >= spawned + D022Fallback, "balcony gunner fires");
            ctx.Line(LineIds.D022);
        }

        private static IEnumerator AwningLine(MissionContext ctx)
        {
            // D023 "First awning panel swaps to its torn state" (Destructible ids B3.AW01…08, or WP10's event).
            yield return Wait.Until(() =>
            {
                if (ctx.HappenedThisBeat(EventChannel.Mission, MissionEvents.AwningTorn)) return true;
                for (var i = 0; i < s_awnings.Length; i++)
                    if (ctx.HappenedThisBeat(EventChannel.Destroyed, s_awnings[i])) return true;
                return false;
            }, "first awning tears");
            ctx.Line(LineIds.D023);
        }

        private static IEnumerator WaveBLines(MissionContext ctx)
        {
            yield return ctx.WaveSpawnedWait(EncounterIds.E3, EncounterIds.E3B);
            if (ctx.IsCleared(EncounterIds.E3)) yield break;
            ctx.PlayMusic(MissionCues.MusicMarket, MissionCues.LayerHigh, 1f); // LO → HI when wave B spawns
            var spawned = ctx.Now;
            // D026: wave B exits the south cross-passage (z 197) into the street, behind-left of the player.
            yield return Wait.Until(() => ctx.Now >= spawned + D026Fallback || WaveBInStreet(ctx), "wave B in the street");
            ctx.Line(LineIds.D026);
        }

        private static bool WaveBInStreet(MissionContext ctx)
        {
            var ids = EncounterIds.E3WaveB;
            for (var i = 0; i < ids.Length; i++)
            {
                var a = ctx.Actor(ids[i]);
                var root = a?.Root;
                if (root == null) continue;
                var p = root.position;
                if (p.x >= 179f && p.z <= 201f) return true;
            }
            return false;
        }
    }
}
