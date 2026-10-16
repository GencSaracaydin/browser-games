using System.Collections;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Runtime;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>The deterministic coroutine runner the beat scripts run on.</summary>
    public class RunnerTests
    {
        private readonly List<string> _log = new List<string>();

        private IEnumerator Child(ScriptRunner r, float until)
        {
            _log.Add("child start");
            yield return new Wait(WaitKind.Seconds, null, () => r.Time >= until);
            _log.Add("child end");
        }

        private IEnumerator Parent(ScriptRunner r)
        {
            _log.Add("parent");
            yield return Child(r, 1f);
            _log.Add("after child");
            yield return null;
            _log.Add("next tick");
        }

        [Test]
        public void NestedRoutinesAndWaits()
        {
            var r = new ScriptRunner();
            var t = r.Start(Parent(r), "p");
            r.Tick(0.5f, 0.5f);
            CollectionAssert.AreEqual(new[] { "parent", "child start" }, _log);
            r.Tick(0.6f, 0.6f);
            CollectionAssert.AreEqual(new[] { "parent", "child start", "child end", "after child" }, _log);
            r.Tick(0.1f, 0.1f);
            Assert.IsTrue(t.IsDone);
            Assert.That(_log[_log.Count - 1], Is.EqualTo("next tick"));
        }

        private static IEnumerator Forever()
        {
            while (true) yield return null;
        }

        [Test]
        public void StoppingAnOwnerStopsItsTasks()
        {
            var r = new ScriptRunner();
            var owner = r.Start(Forever(), "owner");
            var child = r.Start(Forever(), "child", owner);
            var other = r.Start(Forever(), "other");
            r.Tick(0.1f, 0.1f);
            r.Stop(owner);
            Assert.IsTrue(owner.IsDone && owner.WasStopped);
            Assert.IsTrue(child.IsDone, "owned task stopped");
            Assert.IsFalse(other.IsDone);
        }

        private static IEnumerator Throws()
        {
            yield return null;
            throw new System.InvalidOperationException("boom");
        }

        [Test]
        public void AFaultingScriptEndsAndIsReported()
        {
            var r = new ScriptRunner();
            ScriptTask faulted = null;
            r.Faulted += (t, e) => faulted = t;
            var task = r.Start(Throws(), "bad");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                r.Tick(0.1f, 0.1f);
                r.Tick(0.1f, 0.1f);
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
            Assert.IsTrue(task.IsDone);
            Assert.IsNotNull(task.Error);
            Assert.AreSame(task, faulted);
        }

        private IEnumerator Cleanup()
        {
            try
            {
                while (true) yield return null;
            }
            finally
            {
                _log.Add("finally");
            }
        }

        [Test]
        public void StoppedScriptsRunTheirFinallyBlocks()
        {
            var r = new ScriptRunner();
            var t = r.Start(Cleanup(), "c");
            r.Tick(0.1f, 0.1f);
            r.Stop(t);
            CollectionAssert.Contains(_log, "finally");
        }

        [Test]
        public void ScaledWaitsFreezeWhilePausedUnscaledOnesRun()
        {
            var r = new ScriptRunner();
            var scaled = r.Start(Wait1(r, false), "scaled");
            var unscaled = r.Start(Wait1(r, true), "unscaled", null, true);
            for (var i = 0; i < 30; i++) r.Tick(0f, 0.05f);
            Assert.IsFalse(scaled.IsDone, "time scale 0 freezes gameplay waits");
            Assert.IsTrue(unscaled.IsDone, "real-time waits continue");
        }

        private static IEnumerator Wait1(ScriptRunner r, bool unscaled)
        {
            var end = r.Now(unscaled) + 1f;
            yield return new Wait(WaitKind.Seconds, null, () => r.Now(unscaled) >= end);
        }

        [Test]
        public void AnyAndAllWaits()
        {
            var a = false;
            var b = false;
            var any = Wait.Any(new Wait(WaitKind.Until, "a", () => a), new Wait(WaitKind.Until, "b", () => b));
            var all = Wait.All(new Wait(WaitKind.Until, "a", () => a), new Wait(WaitKind.Until, "b", () => b));
            Assert.IsFalse(any.IsDone);
            a = true;
            Assert.IsTrue(any.IsDone);
            Assert.IsFalse(all.IsDone);
            b = true;
            Assert.IsTrue(all.IsDone);
            var leaves = new List<Wait>();
            all.CollectLeaves(leaves);
            Assert.That(leaves.Count, Is.EqualTo(2));
        }
    }
}
