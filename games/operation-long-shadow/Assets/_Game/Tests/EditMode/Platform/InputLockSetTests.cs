using LongShadow.Core;
using LongShadow.Platform;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>TECH_PLAN §5.4: reference-counted channel locks.</summary>
    public class InputLockSetTests
    {
        [Test]
        public void ChannelStaysLockedUntilEveryHandleIsReleased()
        {
            var s = new InputLockSet();
            var a = s.Lock(InputLockFlags.Look | InputLockFlags.Fire, "insert");
            var b = s.Lock(InputLockFlags.Fire, "cinematic");
            Assert.IsTrue(s.IsLocked(InputLockFlags.Fire));
            Assert.IsTrue(s.IsLocked(InputLockFlags.Look));
            Assert.IsFalse(s.IsLocked(InputLockFlags.Move));
            Assert.IsTrue(s.Unlock(a));
            Assert.IsTrue(s.IsLocked(InputLockFlags.Fire));
            Assert.IsFalse(s.IsLocked(InputLockFlags.Look));
            Assert.IsTrue(s.Unlock(b));
            Assert.That(s.Union, Is.EqualTo(InputLockFlags.None));
        }

        [Test]
        public void AllLocksEveryChannel()
        {
            var s = new InputLockSet();
            var h = s.Lock(InputLockFlags.All, "pause");
            foreach (var f in new[] { InputLockFlags.Move, InputLockFlags.Look, InputLockFlags.Fire, InputLockFlags.Weapons, InputLockFlags.Interact, InputLockFlags.Jump })
                Assert.IsTrue(s.IsLocked(f), f.ToString());
            s.Unlock(h);
            Assert.IsFalse(s.IsLocked(InputLockFlags.All));
        }

        [Test]
        public void UnknownAndRepeatedUnlocksAreIgnored()
        {
            var s = new InputLockSet();
            var h = s.Lock(InputLockFlags.Move, "a");
            Assert.IsTrue(s.Unlock(h));
            Assert.IsFalse(s.Unlock(h));
            Assert.IsFalse(s.Unlock(-1));
            Assert.That(s.Count, Is.Zero);
        }

        [Test]
        public void NoneLockIsValidAndLocksNothing()
        {
            var s = new InputLockSet();
            var h = s.Lock(InputLockFlags.None, "noop");
            Assert.That(h, Is.GreaterThan(0));
            Assert.IsFalse(s.IsLocked(InputLockFlags.All));
            s.Clear();
            Assert.That(s.Count, Is.Zero);
        }
    }
}
