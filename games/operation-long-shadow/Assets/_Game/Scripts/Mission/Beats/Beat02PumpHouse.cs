using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 2 PUMP HOUSE SIX (GDD §7.2): the silent opener (D014; FIRING POSITION; TargetX on the sentries within 3 m;
    /// "Mark" (D015) when the player is at the position and ADS on the left sentry ≥ 1.5 s or 12 s passed; Hollis's
    /// ScriptedKill on the right sentry; clean → I1 + D017 1 s after both fall; failure → D016, the left sentry runs,
    /// the gate team starts alerted; an unsuppressed shot alone alerts the gate), the gate fight (z &gt; 136, the alert
    /// branch, or 25 s after the sync shot; node set A, D018 on the gunner's first burst, node set B once he is dead),
    /// D019 → D020 with Tinker's 3 s bolt cut, the gate opening 3 s after D020 starts, ENTER ZAROUD. Exit: B2.T.Exit.
    /// </summary>
    public sealed class Beat02PumpHouse : BeatScript
    {
        /// <summary>ADS on the left sentry needed for "Mark" (GDD §7.2).</summary>
        public const float AdsForMark = 1.5f;
        /// <summary>Time at the firing position after which "Mark" comes anyway.</summary>
        public const float TimeForMark = 12f;
        /// <summary>Half-angle of "ADS on the left sentry".</summary>
        public const float AdsConeDegrees = 3f;
        /// <summary>"Mark" is the last word of D015: this long before its end.</summary>
        public const float MarkBeforeLineEnd = 0.35f;
        /// <summary>The left sentry must be dead this long after the mark, else the failure branch (GDD §7.2).</summary>
        public const float FailAfterMark = 0.5f;
        /// <summary>Clean window after the mark.</summary>
        public const float CleanWindow = 1f;
        /// <summary>I1 fires this long after both sentries fall on the clean branch (GDD §5.5).</summary>
        public const float InsertDelay = 1f;
        /// <summary>Gate fight starts this long after the sync shot at the latest.</summary>
        public const float GateFightAfterMark = 25f;
        /// <summary>The chained gate opens this long after D020 starts (GDD §7.13).</summary>
        public const float GateOpensAfterD020 = 3f;

        private float _markAt = -1f;

        /// <inheritdoc/>
        public override int Number => 2;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            _markAt = -1f;
            SaveStartCheckpoint(ctx); // CP2 at (108, 0, 96)
            ctx.Rig?.SetBinocularsAvailable(false);
            yield return WaitForSection(ctx);
            ctx.Activate(EncounterIds.E2);
            ctx.Line(LineIds.D014);
            ctx.Objective(ObjectiveTable.B2Sentries);
            ctx.SquadActionNoWait(SquadIds.Hollis, MissionActionIds.Kneel, MissionMarkers.B2HollisMark);
            ctx.SquadActionNoWait(SquadIds.Sparrow, MissionActionIds.Kneel, MissionMarkers.B2Hold);
            ctx.SquadActionNoWait(SquadIds.Tinker, MissionActionIds.Kneel, MissionMarkers.B2Hold);

            ctx.Run(MusicOnFirstLoudShot(ctx), "B2 music");
            ctx.Run(TargetMarkers(ctx), "B2 sentry markers");
            ctx.Run(GateFight(ctx), "B2 gate fight");
            ctx.Run(GunnerLines(ctx), "B2 D018");

            yield return SentryOpener(ctx);
            yield return ctx.Killed(EncounterIds.SentryLeft, EncounterIds.SentryRight);
            ctx.Complete(ObjectiveTable.B2Sentries);

            // 2. CLEAR THE WEIGHBRIDGE: E2-03…06.
            ctx.Objective(ObjectiveTable.B2Weighbridge);
            yield return Wait.All(ctx.Killed(EncounterIds.E2GateTeam), ctx.Cleared(EncounterIds.E2));
            ctx.Complete(ObjectiveTable.B2Weighbridge);

            // D019 (S4_Roofs loads on it) → D020 while Tinker bolt-cuts the chain; the gate opens 3 s after D020 starts.
            yield return ctx.Say(LineIds.D019);
            ctx.Line(LineIds.D020);
            ctx.SquadActionNoWait(SquadIds.Tinker, ActionIds.BoltCut, MissionMarkers.B2Gate);
            ctx.SfxAt(MissionCues.BoltCutter, MissionMarkers.B2Gate);
            yield return ctx.Seconds(GateOpensAfterD020);
            ctx.Open(GateTable.Weighbridge);
            ctx.SfxAt(MissionCues.GateCreak, MissionMarkers.B2Gate);
            ctx.SetFlag(MissionFlags.B2GateOpen, true);
            ctx.Raise(MissionEvents.WeighbridgeOpen);
            ctx.Follow();

            // 3. ENTER ZAROUD: the arch; exit when the player passes z > 168.
            ctx.Objective(ObjectiveTable.B2Enter);
            yield return ctx.Trigger(MissionTriggers.B2Exit);
            ctx.Complete(ObjectiveTable.B2Enter);
        }

        private IEnumerator SentryOpener(MissionContext ctx)
        {
            var atPosition = 0f;
            var ads = 0f;
            var last = ctx.Now;
            var markRequested = false;
            var firedEarly = false;
            // One wait that polls every tick (its kind/id tell tools and the test autopilot where the player must go).
            yield return new Wait(WaitKind.Trigger, MissionTriggers.B2FiringPos, () =>
            {
                if (BothSentriesDead(ctx) || GateFightStarted(ctx)) return true;
                if (PlayerFired(ctx))
                {
                    firedEarly = true;
                    return true;
                }
                var dt = ctx.Now - last;
                last = ctx.Now;
                if (!ctx.Inside(MissionTriggers.B2FiringPos))
                {
                    ads = 0f;
                    return false;
                }
                atPosition += dt;
                ads = AdsOnLeftSentry(ctx) ? ads + dt : 0f;
                if (ads < AdsForMark && atPosition < TimeForMark) return false;
                markRequested = true;
                return true;
            });

            if (markRequested)
            {
                // "Three. Two. Mark." — Hollis fires on the last word.
                ctx.Line(LineIds.D015);
                var markAt = ctx.Now + Mathf.Max(0f, ctx.Lines.Length(LineIds.D015) - MarkBeforeLineEnd);
                yield return Wait.Until(() =>
                {
                    if (PlayerFired(ctx)) firedEarly = true;
                    return ctx.Now >= markAt;
                }, "Mark");
                _markAt = ctx.Now;
                ctx.ScriptedKill(SquadIds.Hollis, EncounterIds.SentryRight);
                yield return ctx.Seconds(FailAfterMark);
                if (!firedEarly && !ctx.IsKilled(EncounterIds.SentryLeft)) firedEarly = true; // left sentry alive 0.5 s after the mark
                if (!firedEarly)
                {
                    yield return ctx.At(_markAt + CleanWindow);
                    var clean = BothSentriesDead(ctx) && ctx.Log.LastPlayerLoudShotSeq <= ctx.BeatSeq;
                    if (clean)
                    {
                        ctx.SetFlag(MissionFlags.B2Clean, true);
                        ctx.After(InsertDelay, () => ctx.Insert(SequenceIds.InsertI1, LineIds.D017), "B2 I1");
                        yield break;
                    }
                    if (BothSentriesDead(ctx))
                    {
                        // Both died in time but an unsuppressed shot was fired: no D016, the gate team starts alerted.
                        Alert(ctx);
                        yield break;
                    }
                }
            }

            if (BothSentriesDead(ctx) && !firedEarly)
            {
                if (ctx.Log.LastPlayerLoudShotSeq > ctx.BeatSeq) Alert(ctx);
                yield break;
            }

            // Failure branch (no fail state): D016, the left sentry runs north shouting BK_LEG_01, the gate team is alerted.
            if (!ctx.IsKilled(EncounterIds.SentryLeft))
            {
                ctx.Line(LineIds.D016);
                ctx.Raise(MissionEvents.Run(EncounterIds.SentryLeft));
            }
            if (!ctx.IsKilled(EncounterIds.SentryRight)) ctx.ScriptedKill(SquadIds.Hollis, EncounterIds.SentryRight);
            Alert(ctx);
        }

        private static void Alert(MissionContext ctx)
        {
            ctx.SetFlag(MissionFlags.B2Alerted, true);
            ctx.Raise(MissionEvents.Alert(EncounterIds.E2, EncounterIds.E2Gate));
        }

        private bool GateFightStarted(MissionContext ctx) =>
            ctx.Reached(MissionTriggers.B2GateFight) ||
            ctx.HappenedThisBeat(EventChannel.Mission, MissionEvents.Alert(EncounterIds.E2, EncounterIds.E2Gate)) ||
            (_markAt >= 0f && ctx.Now >= _markAt + GateFightAfterMark);

        private IEnumerator GateFight(MissionContext ctx)
        {
            yield return Wait.Until(() => GateFightStarted(ctx), "gate fight trigger");
            // GDD §7.2: the trigger alerts the gate team (z > 136 is also an AlertWhen condition in WP04's table).
            if (!ctx.HappenedThisBeat(EventChannel.Mission, MissionEvents.Alert(EncounterIds.E2, EncounterIds.E2Gate))) Alert(ctx);
            if (!ctx.IsKilled(EncounterIds.SentryRight)) ctx.ScriptedKill(SquadIds.Hollis, EncounterIds.SentryRight);
            ctx.Nodes(MissionMarkers.B2NodeSetA);
            ctx.Tutorial(Tutorials.FragKey, Tutorials.FragText);
            ctx.Tutorial(Tutorials.AimKey, Tutorials.AimText);
            yield return ctx.Killed(EncounterIds.TruckGunner);
            ctx.Nodes(MissionMarkers.B2NodeSetB);
        }

        private static IEnumerator TargetMarkers(MissionContext ctx)
        {
            yield return ctx.Trigger(MissionTriggers.B2FiringPos);
            if (ctx.IsComplete(ObjectiveTable.B2Sentries)) yield break;
            var red = MarkerKind.TargetX;
            if (!ctx.IsKilled(EncounterIds.SentryLeft))
                ctx.Objectives.ShowMarker(ObjectiveTable.B2Sentries + "/" + EncounterIds.SentryLeft,
                    ObjectiveTarget.Actor(EncounterIds.SentryLeft, 146f, 0f, 125f), red, "", false, false, ObjectiveTable.B2Sentries);
            if (!ctx.IsKilled(EncounterIds.SentryRight))
                ctx.Objectives.ShowMarker(ObjectiveTable.B2Sentries + "/" + EncounterIds.SentryRight,
                    ObjectiveTarget.Actor(EncounterIds.SentryRight, 154f, 0f, 126f), red, "", false, false, ObjectiveTable.B2Sentries);
            if (ctx.Weapons != null && ctx.Weapons.CurrentWeaponId == WeaponIds.Commando)
                ctx.Tutorial(Tutorials.PistolKey, Tutorials.PistolText);
        }

        private static IEnumerator MusicOnFirstLoudShot(MissionContext ctx)
        {
            // GDD §7.2: MUS_02_MARKET LO layer starts on the first unsuppressed shot.
            yield return Wait.Until(() => ctx.Log.LastLoudShotSeq > ctx.BeatSeq, "first loud shot");
            ctx.PlayMusic(MissionCues.MusicMarket, MissionCues.LayerLow, 0.5f);
        }

        private static IEnumerator GunnerLines(MissionContext ctx)
        {
            // D018 "Truck gunner opens fire" (E2-06 is the only RPK in E2).
            var start = ctx.Now;
            yield return Wait.Until(() => ctx.Log.LastEnemyShot(WeaponIds.RPK) >= start && ctx.Log.LastEnemyShot(WeaponIds.RPK) >= 0f,
                "truck gunner fires");
            ctx.Line(LineIds.D018);
        }

        private static bool BothSentriesDead(MissionContext ctx) =>
            ctx.IsKilled(EncounterIds.SentryLeft) && ctx.IsKilled(EncounterIds.SentryRight);

        private static bool PlayerFired(MissionContext ctx) => ctx.Log.LastPlayerShotSeq > ctx.BeatSeq;

        private static bool AdsOnLeftSentry(MissionContext ctx)
        {
            var rig = ctx.Rig;
            if (rig == null || !rig.IsADS) return false;
            var sentry = ctx.Actor(EncounterIds.SentryLeft);
            var aim = sentry?.AimPoint;
            if (aim == null) return false;
            var ray = rig.ViewRay;
            return Vector3.Angle(ray.direction, aim.position - ray.origin) <= AdsConeDegrees;
        }
    }
}
