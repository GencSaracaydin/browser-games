using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 6 THE RELAY YARD (GDD §7.6): D043 and the chemlights in the alley; Tinker plants the satchel (3 s); STAND CLEAR
    /// ring; D044 with the player in the ring, the blast 2 s later (never with the player within 8 m of the charge): the
    /// wall swaps to blown, 0.3 s at timeScale 0.3, 1.5 s tinnitus, shake, MUS_04 from the blast, B6.Blast for the AI;
    /// D045 PA +3 s; BK_LEG_08 and the floods slam on +4 s; D046 +2 s with red markers on the live floods; node sets Y1 →
    /// Y2 (≤ 3 alive) → Y3 (yard clear, D047); D048 when the squad stacks at Y3, then [E] Kick. Exit: the kick → Beat 7.
    /// </summary>
    public sealed class Beat06RelayYard : BeatScript
    {
        /// <summary>Delay from D044 to the blast (GDD §7.6).</summary>
        public const float BlastDelay = 2f;
        /// <summary>Tinker never detonates while the player is within this distance of the charge.</summary>
        public const float SafeDistance = 8f;
        /// <summary>Breach slow motion (GDD §7.6: 0.3 s at timeScale 0.3).</summary>
        public const float BlastSlowScale = 0.3f;
        /// <summary>Real seconds of the breach slow motion.</summary>
        public const float BlastSlowSeconds = 0.3f;
        /// <summary>Tinnitus muffle (GDD §7.6).</summary>
        public const float TinnitusSeconds = 1.5f;
        /// <summary>Camera shake trauma of the blast.</summary>
        public const float BlastShake = 0.8f;
        /// <summary>Harrou's PA line after the blast.</summary>
        public const float PaDelay = 3f;
        /// <summary>BK_LEG_08 and the floods after the blast.</summary>
        public const float FloodDelay = 4f;
        /// <summary>D046 after the floods.</summary>
        public const float D046Delay = 2f;
        /// <summary>The squad moves to Y2 at this many living yard enemies.</summary>
        public const int Y2AtAlive = 3;
        /// <summary>Blast damage at the charge (Combat Explode; the wall is the target, the squad and the player are 12 m off).</summary>
        public const float BlastDamage = 200f;

        private static readonly string[] s_floods = { MissionMarkers.B6F1, MissionMarkers.B6F2, MissionMarkers.B6F3, MissionMarkers.B6F4 };

        /// <inheritdoc/>
        public override int Number => 6;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            SaveStartCheckpoint(ctx); // CP6 in the alley (295, 0, 334)
            yield return WaitForSection(ctx);
            ctx.Activate(EncounterIds.E6);

            // D043 and the chemlights (G7) when the player enters the alley.
            ctx.Line(LineIds.D043);
            ctx.Squad?.SetChemlights(true);
            ctx.Sfx(MissionCues.ChemlightCrack, ctx.PlayerPosition);
            var plant = ctx.Run(ctx.SquadAction(SquadIds.Tinker, ActionIds.PlantSatchel, MissionMarkers.B6Charge, 10f), "B6 satchel");
            ctx.SfxAt(MissionCues.SatchelPlant, MissionMarkers.B6Charge);

            // 1. STAND CLEAR.
            ctx.Objective(ObjectiveTable.B6StandClear);
            yield return ctx.Trigger(MissionTriggers.B6StandClear);
            ctx.Complete(ObjectiveTable.B6StandClear);
            ctx.Line(LineIds.D044);
            yield return plant;
            yield return ctx.Seconds(BlastDelay);
            yield return new Wait(WaitKind.Until, "player clear of the charge", () =>
                !ctx.TryPosition(MissionMarkers.B6Charge, out var c) || (ctx.PlayerPosition - c).sqrMagnitude >= SafeDistance * SafeDistance);
            Blast(ctx);

            // 2. CLEAR THE YARD (no marker until D046).
            ctx.Objective(ObjectiveTable.B6Yard);
            ctx.Nodes(MissionMarkers.B6NodeSetY1);
            ctx.RunDetached(AfterBlast(ctx), "B6 after the blast"); // set piece: PA, floods and D046 even if the yard falls fast
            ctx.Run(ToY2(ctx), "B6 Y2");
            yield return ctx.Cleared(EncounterIds.E6);
            ctx.Complete(ObjectiveTable.B6Yard);
            ctx.Line(LineIds.D047);
            ctx.Nodes(MissionMarkers.B6NodeSetY3);

            // 3. ENTER THE RELAY HALL: D048 when the squad is at Y3, then [E] Kick.
            yield return ctx.SquadAt(MissionMarkers.B6NodeSetY3);
            ctx.Line(LineIds.D048);
            ctx.Objective(ObjectiveTable.B6Hall);
            ctx.Open(GateTable.UseHallDoor);
            yield return ctx.Interacted(MissionMarkers.HallDoor);
            ctx.Close(GateTable.UseHallDoor);
            ctx.Open(GateTable.HallDoor);
            ctx.SfxAt(MissionCues.DoorKick, MissionMarkers.HallDoor);
            ctx.Complete(ObjectiveTable.B6Hall);
        }

        private static void Blast(MissionContext ctx)
        {
            ctx.SetFlag(MissionFlags.B6Blown, true);
            ctx.Raise(MissionEvents.B6Blast);
            if (Destructibles.TryGet(MissionMarkers.B6BreachWall, out var wall) && !wall.IsDestroyed) wall.ForceDestroy();
            if (ctx.TryPosition(MissionMarkers.B6Charge, out var at))
            {
                ctx.Combat?.Explode(at, BlastDamage, 2.5f, 6f, null, Team.Squad);
                ctx.Sfx(MissionCues.BreachExplosion, at);
            }
            ctx.Run(ctx.SlowMo(BlastSlowScale, BlastSlowSeconds, "Breach blast"), "B6 slow-mo");
            ctx.Audio?.Tinnitus(TinnitusSeconds);
            ctx.Rig?.Shake(BlastShake);
            ctx.PlayMusic(MissionCues.MusicStation, MissionCues.LayerHigh, 0.5f); // MUS_04_STATION from the blast
        }

        private static IEnumerator AfterBlast(MissionContext ctx)
        {
            var t0 = ctx.Now;
            yield return ctx.At(t0 + PaDelay);
            ctx.Line(LineIds.D045);
            yield return ctx.At(t0 + FloodDelay);
            var shouter = FirstLivingYardEnemy(ctx);
            if (shouter != null) ctx.VO?.PlayBark(LineIds.BarkLeg08, shouter, Team.Legion);
            else ctx.RadioBark(LineIds.BarkLeg08);
            ctx.Lines.NoteRequested(LineIds.BarkLeg08);
            ctx.Raise(MissionEvents.FloodsOn);
            for (var i = 0; i < s_floods.Length; i++) ctx.SfxAt(MissionCues.FloodSlamOn, s_floods[i]);
            yield return ctx.At(t0 + FloodDelay + D046Delay);
            ctx.Line(LineIds.D046);
            var spec = ObjectiveTable.Get(ObjectiveTable.B6Yard);
            if (ctx.IsComplete(ObjectiveTable.B6Yard)) yield break;
            for (var i = 0; i < spec.Targets.Length; i++)
                if (!ctx.IsDestroyed(spec.Targets[i].Id)) ctx.Objectives.ShowTarget(spec, i);
        }

        private static IEnumerator ToY2(MissionContext ctx)
        {
            yield return Wait.Until(() => ctx.IsCleared(EncounterIds.E6) || ctx.Alive(EncounterIds.E6) <= Y2AtAlive, "≤ 3 alive");
            if (!ctx.IsCleared(EncounterIds.E6)) ctx.Nodes(MissionMarkers.B6NodeSetY2);
        }

        private static Transform FirstLivingYardEnemy(MissionContext ctx)
        {
            string[] ids = { "E6-01", "E6-02", "E6-03", "E6-04" };
            for (var i = 0; i < ids.Length; i++)
            {
                var a = ctx.Actor(ids[i]);
                if (a != null && a.IsAlive && a.Root != null) return a.Root;
            }
            return null;
        }
    }
}
