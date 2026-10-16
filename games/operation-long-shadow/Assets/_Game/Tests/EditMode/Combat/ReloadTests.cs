using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>GDD §13.1 reload timings: tactical vs empty, the magazine seat commit, cancels, partial reserves.</summary>
    public class ReloadTests
    {
        private const float Dt = 1f / 60f;

        private static float RunToEnd(ReloadStateMachine sm, WeaponAmmo ammo, out ReloadEvents all)
        {
            all = ReloadEvents.None;
            var t = 0f;
            for (var i = 0; i < 2000 && sm.IsReloading; i++)
            {
                all |= sm.Tick(Dt, ammo);
                t += Dt;
            }
            return t;
        }

        [TestCase(WeaponIds.Commando)]
        [TestCase(WeaponIds.HushPuppy)]
        [TestCase(WeaponIds.AK)]
        [TestCase(WeaponIds.RPK)]
        public void TacticalReloadTakesTheGddTime(string id)
        {
            var w = WeaponTable.Get(id);
            var ammo = new WeaponAmmo(id, w.MagazineSize, w.MaxReserve, w.MagazineSize / 2, w.MaxReserve);
            var sm = new ReloadStateMachine();
            Assert.That(sm.Begin(w, ammo), Is.EqualTo(ReloadEvents.Started));
            Assert.That(sm.IsEmptyReload, Is.False);
            var t = RunToEnd(sm, ammo, out var ev);
            Assert.That(t, Is.EqualTo(w.TacticalReload.Duration).Within(Dt + 1e-4f));
            Assert.That(ammo.Magazine, Is.EqualTo(w.MagazineSize));
            Assert.That((ev & ReloadEvents.ChargeStart) == 0, Is.True, "no charge on a tactical reload");
            Assert.That((ev & ReloadEvents.MagIn) != 0 && (ev & ReloadEvents.Finished) != 0, Is.True);
        }

        [TestCase(WeaponIds.Commando)]
        [TestCase(WeaponIds.HushPuppy)]
        [TestCase(WeaponIds.AK)]
        [TestCase(WeaponIds.RPK)]
        public void EmptyReloadTakesTheGddTimeAndCharges(string id)
        {
            var w = WeaponTable.Get(id);
            var ammo = new WeaponAmmo(id, w.MagazineSize, w.MaxReserve, 0, w.MaxReserve);
            var sm = new ReloadStateMachine();
            sm.Begin(w, ammo);
            Assert.That(sm.IsEmptyReload, Is.True);
            var t = RunToEnd(sm, ammo, out var ev);
            Assert.That(t, Is.EqualTo(w.EmptyReload.Duration).Within(Dt + 1e-4f));
            Assert.That((ev & ReloadEvents.ChargeStart) != 0 && (ev & ReloadEvents.ChargeEnd) != 0, Is.True);
            Assert.That(ammo.Magazine, Is.EqualTo(w.MagazineSize));
            Assert.That(ammo.Reserve, Is.EqualTo(w.MaxReserve - w.MagazineSize));
        }

        [Test]
        public void EventsArriveInOrder()
        {
            var w = WeaponTable.AK;
            var ammo = new WeaponAmmo(w.Id, 30, 120, 0, 120);
            var sm = new ReloadStateMachine();
            sm.Begin(w, ammo);
            var order = new[] { ReloadEvents.TiltDone, ReloadEvents.MagOut, ReloadEvents.MagGrab, ReloadEvents.MagIn, ReloadEvents.ChargeStart, ReloadEvents.ChargeEnd, ReloadEvents.Finished };
            var next = 0;
            for (var i = 0; i < 1000 && sm.IsReloading; i++)
            {
                var e = sm.Tick(Dt, ammo);
                foreach (var flag in order)
                {
                    if ((e & flag) == 0) continue;
                    Assert.That(flag, Is.EqualTo(order[next]), "event order");
                    next++;
                }
            }
            Assert.That(next, Is.EqualTo(order.Length));
        }

        [Test]
        public void CancelBeforeTheSeatKeepsTheOldMagazine()
        {
            var w = WeaponTable.Commando;
            var ammo = new WeaponAmmo(w.Id, 30, 150, 12, 150);
            var sm = new ReloadStateMachine();
            sm.Begin(w, ammo);
            for (var t = 0f; t < w.TacticalReload.MagIn - 0.1f; t += Dt) sm.Tick(Dt, ammo);
            Assert.That(sm.Cancel(), Is.EqualTo(ReloadEvents.Cancelled));
            Assert.That(ammo.Magazine, Is.EqualTo(12));
            Assert.That(ammo.Reserve, Is.EqualTo(150));
        }

        [Test]
        public void CancelAfterTheSeatKeepsTheNewMagazine()
        {
            var w = WeaponTable.Commando;
            var ammo = new WeaponAmmo(w.Id, 30, 150, 12, 150);
            var sm = new ReloadStateMachine();
            sm.Begin(w, ammo);
            for (var t = 0f; t < w.TacticalReload.MagIn + 0.05f; t += Dt) sm.Tick(Dt, ammo);
            sm.Cancel();
            Assert.That(ammo.Magazine, Is.EqualTo(30));
            Assert.That(ammo.Reserve, Is.EqualTo(132));
        }

        [Test]
        public void PartialReserveAndRefusals()
        {
            var w = WeaponTable.AK;
            var ammo = new WeaponAmmo(w.Id, 30, 120, 5, 10);
            var sm = new ReloadStateMachine();
            sm.Begin(w, ammo);
            RunToEnd(sm, ammo, out _);
            Assert.That(ammo.Magazine, Is.EqualTo(15));
            Assert.That(ammo.Reserve, Is.EqualTo(0));
            Assert.That(sm.Begin(w, ammo), Is.EqualTo(ReloadEvents.None), "no reserve");
            var full = WeaponAmmo.Full(w);
            Assert.That(sm.Begin(w, full), Is.EqualTo(ReloadEvents.None), "full magazine");
        }

        [Test]
        public void RuntimeAutoReloadsAnEmptyMagazineOnTrigger()
        {
            var w = WeaponTable.HushPuppy;
            var ammo = new WeaponAmmo(w.Id, 8, 48, 0, 48);
            var rt = new WeaponRuntime(w, ammo);
            var f = rt.Tick(Dt, new WeaponInput { TriggerDown = true, TriggerHeld = true, SinceSprintEnd = 10f, Grounded = true });
            Assert.That(f.Shots, Is.EqualTo(0));
            Assert.That((f.Reload & ReloadEvents.Started) != 0, Is.True);
            Assert.That(rt.Reload.IsEmptyReload, Is.True);
        }

        [Test]
        public void RuntimeSprintCancelsAReload()
        {
            var w = WeaponTable.AK;
            var ammo = new WeaponAmmo(w.Id, 30, 120, 10, 120);
            var rt = new WeaponRuntime(w, ammo);
            rt.Tick(Dt, new WeaponInput { ReloadDown = true, SinceSprintEnd = 10f, Grounded = true });
            Assert.That(rt.Reload.IsReloading, Is.True);
            var f = rt.Tick(Dt, new WeaponInput { Sprinting = true, Grounded = true });
            Assert.That((f.Reload & ReloadEvents.Cancelled) != 0, Is.True);
            Assert.That(ammo.Magazine, Is.EqualTo(10));
        }

        [Test]
        public void NoFiringWhileReloading()
        {
            var w = WeaponTable.Commando;
            var ammo = new WeaponAmmo(w.Id, 30, 150, 10, 150);
            var rt = new WeaponRuntime(w, ammo);
            rt.Tick(Dt, new WeaponInput { ReloadDown = true, SinceSprintEnd = 10f, Grounded = true });
            var shots = 0;
            for (var i = 0; i < 30; i++) shots += rt.Tick(Dt, new WeaponInput { TriggerHeld = true, SinceSprintEnd = 10f, Grounded = true }).Shots;
            Assert.That(shots, Is.EqualTo(0));
        }
    }
}
