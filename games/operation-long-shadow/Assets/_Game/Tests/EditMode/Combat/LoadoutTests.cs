using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>GDD §13.1-13.2 loadout, pickups, resupply and the GDD §17 checkpoint round trip.</summary>
    public class LoadoutTests
    {
        [Test]
        public void MissionStartLoadout()
        {
            var l = new LoadoutState();
            l.Reset();
            Assert.That(l.InSlot(1), Is.EqualTo(WeaponIds.Commando));
            Assert.That(l.InSlot(2), Is.EqualTo(WeaponIds.HushPuppy));
            Assert.That(l.CurrentSlot, Is.EqualTo(1));
            Assert.That(l.Frags, Is.EqualTo(3));
            Assert.That(l.AmmoOf(WeaponIds.Commando).Magazine, Is.EqualTo(30));
            Assert.That(l.AmmoOf(WeaponIds.Commando).Reserve, Is.EqualTo(150));
            Assert.That(l.AmmoOf(WeaponIds.HushPuppy).Magazine, Is.EqualTo(8));
            Assert.That(l.AmmoOf(WeaponIds.HushPuppy).Reserve, Is.EqualTo(48));
        }

        [Test]
        public void SwappingAPickupDropsTheOldPrimaryWithItsAmmo()
        {
            var l = new LoadoutState();
            l.Reset();
            l.AmmoOf(WeaponIds.Commando).Set(12, 40);
            var dropped = l.SwapPrimary(WeaponIds.AK, 20, 60);
            Assert.That(dropped.IsValid, Is.True);
            Assert.That(dropped.Id, Is.EqualTo(WeaponIds.Commando));
            Assert.That(dropped.Magazine, Is.EqualTo(12));
            Assert.That(dropped.Reserve, Is.EqualTo(40));
            Assert.That(l.InSlot(1), Is.EqualTo(WeaponIds.AK));
            Assert.That(l.AmmoOf(WeaponIds.AK).Magazine, Is.EqualTo(20));
            Assert.That(l.AmmoOf(WeaponIds.AK).Reserve, Is.EqualTo(60));
            Assert.That(l.AmmoOf(WeaponIds.Commando), Is.Null);
            Assert.That(l.InSlot(2), Is.EqualTo(WeaponIds.HushPuppy), "slot 2 is always the Hush Puppy");
        }

        [Test]
        public void WalkOverTopUpClampsAtTheMaximum()
        {
            var l = new LoadoutState();
            l.Reset();
            l.SwapPrimary(WeaponIds.AK, 30, 100);
            Assert.That(l.TopUpReserve(WeaponIds.AK, 50), Is.EqualTo(20));
            Assert.That(l.AmmoOf(WeaponIds.AK).Reserve, Is.EqualTo(120));
            Assert.That(l.TopUpReserve(WeaponIds.RPK, 50), Is.EqualTo(0), "not carried");
        }

        [Test]
        public void ResupplyRefillsEverythingAndFourFrags()
        {
            var l = new LoadoutState();
            l.Reset();
            l.AmmoOf(WeaponIds.Commando).Set(0, 0);
            l.TryUseFrag();
            l.Resupply();
            Assert.That(l.AmmoOf(WeaponIds.Commando).Magazine, Is.EqualTo(30));
            Assert.That(l.AmmoOf(WeaponIds.Commando).Reserve, Is.EqualTo(150));
            Assert.That(l.Frags, Is.EqualTo(4), "GDD §13.2: 4 max");
        }

        [Test]
        public void FragsNeverExceedFourOrGoNegative()
        {
            var l = new LoadoutState();
            l.Reset();
            Assert.That(l.AddFrags(10), Is.EqualTo(1));
            for (var i = 0; i < 4; i++) Assert.That(l.TryUseFrag(), Is.True);
            Assert.That(l.TryUseFrag(), Is.False);
            Assert.That(l.Frags, Is.EqualTo(0));
        }

        [Test]
        public void CheckpointRoundTripTopsFragsUpToTwo()
        {
            var l = new LoadoutState();
            l.Reset();
            l.SwapPrimary(WeaponIds.RPK, 41, 75);
            l.Select(2);
            l.TryUseFrag();
            l.TryUseFrag();
            l.TryUseFrag();
            var record = new SaveRecord { Key = "Weapons" };
            l.Capture(new SaveWriter(record));

            var r = new LoadoutState();
            r.Reset();
            r.Restore(new SaveReader(record), WeaponTable.Frag.CheckpointMinimum);
            Assert.That(r.InSlot(1), Is.EqualTo(WeaponIds.RPK));
            Assert.That(r.AmmoOf(WeaponIds.RPK).Magazine, Is.EqualTo(41));
            Assert.That(r.AmmoOf(WeaponIds.RPK).Reserve, Is.EqualTo(75));
            Assert.That(r.CurrentSlot, Is.EqualTo(2));
            Assert.That(r.Frags, Is.EqualTo(2), "GDD §17: frags topped up to at least 2");
        }

        [Test]
        public void EmptySnapshotRestoresTheStartLoadout()
        {
            var r = new LoadoutState();
            r.Restore(new SaveReader(new SaveRecord { Key = "Weapons" }), 2);
            Assert.That(r.InSlot(1), Is.EqualTo(WeaponIds.Commando));
            Assert.That(r.Frags, Is.EqualTo(3));
        }

        [Test]
        public void TheDoorGunCannotBeSlotted()
        {
            var l = new LoadoutState();
            l.Reset();
            Assert.That(l.Give(WeaponIds.DoorGun), Is.False);
            Assert.That(l.Give("NOPE"), Is.False);
        }

        [Test]
        public void AmmoBookkeeping()
        {
            var a = new WeaponAmmo(WeaponIds.AK, 30, 120, 3, 10);
            Assert.That(a.TryConsume(), Is.True);
            Assert.That(a.Magazine, Is.EqualTo(2));
            Assert.That(a.CommitReload(), Is.EqualTo(10));
            Assert.That(a.Magazine, Is.EqualTo(12));
            Assert.That(a.CanReload, Is.False);
            a.Set(99, 999);
            Assert.That(a.Magazine, Is.EqualTo(30));
            Assert.That(a.Reserve, Is.EqualTo(120));
        }
    }
}
