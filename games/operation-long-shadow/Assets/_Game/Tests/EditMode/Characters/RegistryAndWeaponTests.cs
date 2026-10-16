using LongShadow.Characters.Animation;
using LongShadow.Characters.Ragdoll;
using LongShadow.Characters.Weapons;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Characters
{
    public class RegistryAndWeaponTests
    {
        private sealed class FakeCorpse : ICorpse
        {
            public float DiedAt { get; set; }
            public bool Evicted;
            public void Evict() => Evicted = true;
        }

        [SetUp]
        public void SetUp() => CorpseRegistry.Clear();

        [TearDown]
        public void TearDown()
        {
            CorpseRegistry.Clear();
            CorpseRegistry.Cap = CorpseRegistry.DefaultCap;
        }

        [Test]
        public void CorpseCapEvictsTheOldest()
        {
            Assert.That(CorpseRegistry.DefaultCap, Is.EqualTo(8), "TECH_PLAN §7.8 corpse cap 8");
            var corpses = new FakeCorpse[9];
            for (var i = 0; i < corpses.Length; i++)
            {
                corpses[i] = new FakeCorpse { DiedAt = 10f + i };
                CorpseRegistry.Register(corpses[i]);
            }
            Assert.That(CorpseRegistry.Count, Is.EqualTo(8));
            Assert.That(corpses[0].Evicted, Is.True);
            Assert.That(corpses[1].Evicted, Is.False);
            CorpseRegistry.Register(corpses[3]);
            Assert.That(CorpseRegistry.Count, Is.EqualTo(8), "re-registering is a no-op");
        }

        [Test]
        public void EveryWeaponIdHasAHoldRow()
        {
            foreach (var id in new[] { WeaponIds.Commando, WeaponIds.HushPuppy, WeaponIds.AK, WeaponIds.RPK, WeaponIds.AKMS, WeaponIds.SVD,
                                       WeaponIds.RPG7, WeaponIds.DShK, WeaponIds.DoorGun, WeaponIds.Frag, WeaponIds.Knife, WeaponIds.Smoke })
                Assert.That(WeaponHoldTable.TryGet(id, out _), Is.True, id);
            WeaponHoldTable.TryGet(WeaponIds.HushPuppy, out var pistol);
            Assert.That(pistol.Hold, Is.EqualTo(WeaponHold.Pistol));
            WeaponHoldTable.TryGet(WeaponIds.DShK, out var dshk);
            Assert.That(dshk.IsCarried, Is.False, "mounted guns are owned by the vehicle");
            WeaponHoldTable.TryGet(WeaponIds.Frag, out var frag);
            Assert.That(frag.IsCarried, Is.False);
            Assert.That(WeaponHoldTable.TryGet("NOPE", out var unknown), Is.False);
            Assert.That(unknown.Hold, Is.EqualTo(WeaponHold.Rifle), "unknown ids get rifle defaults");
        }

        [Test]
        public void SocketsUseWp03NamesAndIgnoreMeshNames()
        {
            var root = new GameObject("PF_W_AK");
            try
            {
                var grip = new GameObject("Grip").transform; // a greybox mesh, not a socket
                grip.SetParent(root.transform, false);
                var socket = new GameObject("Socket_LeftHand").transform;
                socket.SetParent(root.transform, false);
                var muzzle = new GameObject("Socket_Muzzle").transform;
                muzzle.SetParent(socket, false);
                Assert.That(WeaponSockets.Find(root.transform, WeaponSockets.RightGripNames), Is.Null);
                Assert.That(WeaponSockets.Find(root.transform, WeaponSockets.LeftGripNames), Is.SameAs(socket));
                Assert.That(WeaponSockets.Find(root.transform, WeaponSockets.MuzzleNames), Is.SameAs(muzzle));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
