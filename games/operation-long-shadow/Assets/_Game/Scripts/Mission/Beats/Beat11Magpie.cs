using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 11 MAGPIE (GDD §7.11, §8.7): on rails along B11.Exfil (50 s); the player on the left door gun (WP03
    /// MountDoorGun at HELI.DoorGunPivot; incoming damage ×0.25, health floor 1); E11 rooftop targets with red markers in
    /// their windows (T1 8-16, T2 15-23, T4 26-33, T3 28-30) and the two Legion trucks (16-27); D077 at t 0, D078 when T1
    /// opens fire, D079 at t 25, D080 0.5 s after it; at T3's RPG launch (t 30): D081, the music hard cut, the alarm, the
    /// climax (TL_Climax, input locked: door gun off at +3 s, the pistol rise, D082, I4, cut to white). No checkpoint (a
    /// reload goes to CP10). LightStage s 3.70 → 4.00 follows exfil time / 50 s. Exit: the climax ends → outro.
    /// </summary>
    public sealed class Beat11Magpie : BeatScript
    {
        private struct Window
        {
            public string Id;
            public ObjectiveTarget Target;
            public float Start;
            public float End;
        }

        private ISplineVehicle _exfil;
        private float _t0;

        /// <inheritdoc/>
        public override int Number => 11;

        /// <summary>Exfil time in seconds (the spline's Elapsed, else time since the beat started).</summary>
        public float ExfilTime(MissionContext ctx) =>
            _exfil != null && (_exfil.IsMoving || _exfil.Elapsed > 0f) ? _exfil.Elapsed : ctx.Now - _t0;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            yield return WaitForSection(ctx);
            _t0 = ctx.Now;
            _exfil = null;
            var reg = ctx.Markers;
            if (reg != null && reg.TryGet(MissionMarkers.B11Exfil, out SceneMarker m) && m != null) _exfil = m.GetComponent<ISplineVehicle>();

            // On the door gun: incoming damage ×0.25 and a health floor of 1 (the player cannot die).
            var pivot = ctx.MarkerTransform(MissionMarkers.HeliDoorGunPivot);
            if (pivot != null) ctx.Weapons?.MountDoorGun(pivot);
            else LSLog.WarnOnce("b11-nopivot", LogCategory.Mission, "HELI.DoorGunPivot is not in the scene (S8_Heli): the door gun is not mounted.");
            ctx.Rig?.SetDamageModifiers(ExfilTimeline.DamageScale, ExfilTimeline.HealthFloor);
            ctx.OnBeatEnd(() =>
            {
                var w = ctx.Weapons;
                if (w != null && w.IsDoorGunMounted) w.DismountDoorGun();
                ctx.Rig?.SetDamageModifiers(1f, 0f);
            });
            ctx.SquadActionNoWait(SquadIds.Tinker, ActionIds.SitHeli, MissionMarkers.HeliTinker);
            ctx.SquadActionNoWait(SquadIds.Sparrow, ActionIds.SitHeli, MissionMarkers.HeliSparrow);
            ctx.SquadActionNoWait(SquadIds.Hollis, ActionIds.HaulBag, MissionMarkers.HeliHollis);

            _exfil?.Play();
            ctx.Director.PathProgressOverride = () => ExfilTime(ctx) / ExfilTimeline.Total;
            ctx.Raise(MissionEvents.ExfilStart);
            ctx.Activate(EncounterIds.E11);
            ctx.Objective(ObjectiveTable.B11Cover);
            ctx.Line(LineIds.D077);
            ctx.Run(TargetMarkers(ctx), "B11 markers");
            ctx.Run(GunfireLine(ctx), "B11 D078");
            ctx.Run(Trucks(ctx), "B11 trucks");
            ctx.Run(NumbersLines(ctx), "B11 D079/D080");

            // The climax at T3's RPG launch (t 30, whatever happens).
            yield return Wait.Any(ctx.Event(MissionEvents.ExfilRpgLaunch),
                Wait.Until(() => ExfilTime(ctx) >= ExfilTimeline.T3Fire + ExfilTimeline.RpgEventGrace, "t 30"));
            ctx.Line(LineIds.D081);
            ctx.Music?.HardCut();
            ctx.Audio?.Play2D(MissionCues.HeliAlarm);
            ctx.Raise(MissionEvents.ClimaxStart);
            ctx.Complete(ObjectiveTable.B11Cover);
            ctx.Lock(InputLockFlags.All, "Exfil climax");
            ctx.Run(Dismount(ctx), "B11 dismount");
            yield return ctx.Cinematic(SequenceIds.Climax, ExfilTimeline.ClimaxSeconds + 10f, () => ClimaxFallback(ctx));
        }

        private IEnumerator TargetMarkers(MissionContext ctx)
        {
            var spec = ObjectiveTable.Get(ObjectiveTable.B11Cover);
            var windows = new[]
            {
                W(spec, 0, ExfilTimeline.T1Start, ExfilTimeline.T1End),
                W(spec, 1, ExfilTimeline.T2Start, ExfilTimeline.T2End),
                W(spec, 2, ExfilTimeline.T3Start, ExfilTimeline.T3Fire),
                W(spec, 3, ExfilTimeline.T4Start, ExfilTimeline.T4End),
                W(spec, 4, ExfilTimeline.TrucksStart, ExfilTimeline.TrucksEnd),
                W(spec, 5, ExfilTimeline.TrucksStart, ExfilTimeline.TrucksEnd),
            };
            var shown = new bool[windows.Length];
            yield return Wait.Until(() =>
            {
                var t = ExfilTime(ctx);
                for (var i = 0; i < windows.Length; i++)
                {
                    var w = windows[i];
                    var dead = w.Target.Kind == ObjectiveTargetKind.Actor ? ctx.IsKilled(w.Target.Id) && ctx.Encounters != null : ctx.IsDestroyed(w.Target.Id);
                    var want = t >= w.Start && t < w.End && !dead && !ctx.IsComplete(ObjectiveTable.B11Cover);
                    if (want == shown[i]) continue;
                    shown[i] = want;
                    if (want) ctx.Objectives.ShowTarget(spec, i);
                    else ctx.Objectives.HideTarget(spec, i);
                }
                return ctx.IsComplete(ObjectiveTable.B11Cover);
            }, "B11 target windows");
        }

        private static Window W(ObjectiveSpec spec, int index, float start, float end) =>
            new Window { Id = spec.MarkerId(index), Target = spec.Targets[index], Start = start, End = end };

        private IEnumerator GunfireLine(MissionContext ctx)
        {
            // D078 "T1 (gatehouse roof) opens fire, about spline t = 8 s".
            yield return Wait.Until(() => ExfilTime(ctx) >= ExfilTimeline.T1Start - 0.5f, "T1 window");
            var seq = ctx.Log.Seq;
            yield return Wait.Until(() => ctx.Log.LastEnemyShotSeq > seq || ExfilTime(ctx) >= ExfilTimeline.T1Start + 1f, "T1 opens fire");
            ctx.Line(LineIds.D078);
        }

        private IEnumerator Trucks(MissionContext ctx)
        {
            yield return Wait.Until(() => ExfilTime(ctx) >= ExfilTimeline.TrucksStart, "t 16");
            ctx.Raise(MissionEvents.ExfilTrucks);
            var reg = ctx.Markers;
            if (reg != null && reg.TryGet(MissionMarkers.B11TruckSquare, out SceneMarker m) && m != null)
                m.GetComponent<ISplineVehicle>()?.Play();
        }

        private IEnumerator NumbersLines(MissionContext ctx)
        {
            yield return Wait.Until(() => ExfilTime(ctx) >= ExfilTimeline.D079At, "t 25");
            yield return ctx.Say(LineIds.D079);
            yield return ctx.Seconds(ExfilTimeline.D080Gap);
            ctx.Line(LineIds.D080);
        }

        private static IEnumerator Dismount(MissionContext ctx)
        {
            // Climax shot 2 (0:03): the door gun disengages; the camera is drawn across the cabin (TL_Climax).
            yield return ctx.RealSeconds(ExfilTimeline.ClimaxDismount);
            var w = ctx.Weapons;
            if (w != null && w.IsDoorGunMounted) w.DismountDoorGun();
        }

        private static IEnumerator ClimaxFallback(MissionContext ctx)
        {
            var t0 = ctx.RealNow;
            ctx.Rig?.Shake(0.6f);
            yield return Wait.Until(() => ctx.RealNow >= t0 + ExfilTimeline.ClimaxPistol, "pistol");
            ctx.FirstPersonBody?.PlayPistolRise(ExfilTimeline.PistolSeconds, null);
            yield return Wait.Until(() => ctx.RealNow >= t0 + ExfilTimeline.ClimaxD082, "pistol at chest height");
            ctx.Line(LineIds.D082);
            yield return Wait.Until(() => ctx.RealNow >= t0 + ExfilTimeline.ClimaxI4, "I4");
            ctx.Insert(SequenceIds.InsertI4);
            yield return Wait.Until(() => ctx.RealNow >= t0 + ExfilTimeline.ClimaxSeconds, "white");
            ctx.ScreenFX?.Flash(6f, 1f); // cut to white
        }
    }
}
