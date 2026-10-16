using System.Text.RegularExpressions;
using LongShadow.Core;
using LongShadow.Platform;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>TECH_PLAN §5.2.4 URL flags: ?cp=, ?dev=1, ?autotest=1 (+ nolock, perf).</summary>
    public class LaunchOptionsTests
    {
        [Test]
        public void ParsesQueryAndHash()
        {
            var o = LaunchOptions.Parse("?cp=cp5&dev=1#autotest=1");
            Assert.That(o.Checkpoint, Is.EqualTo(SequenceIds.CP5));
            Assert.IsTrue(o.Dev);
            Assert.IsTrue(o.Autotest);
            Assert.IsFalse(o.NoLock);
            Assert.IsFalse(o.ForcePerformanceMode);
        }

        [Test]
        public void CheckpointIdsAreNormalisedToTheirCanonicalSpelling()
        {
            Assert.That(LaunchOptions.Parse("cp=CP3B").Checkpoint, Is.EqualTo(SequenceIds.CP3b));
        }

        [Test]
        public void UnknownCheckpointIsIgnoredWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("unknown checkpoint"));
            Assert.IsNull(LaunchOptions.Parse("?cp=CP99").Checkpoint);
        }

        [TestCase("dev=0", false)]
        [TestCase("dev=false", false)]
        [TestCase("dev=off", false)]
        [TestCase("dev", true)]
        [TestCase("dev=1", true)]
        [TestCase("dev=yes", true)]
        [TestCase("", false)]
        public void BooleanFlags(string query, bool dev)
        {
            Assert.That(LaunchOptions.Parse(query).Dev, Is.EqualTo(dev));
        }

        [Test]
        public void KeysAreCaseInsensitiveAndValuesDecoded()
        {
            var o = LaunchOptions.Parse("?NoLock=1&perf&Note=hello%20world+again&x=");
            Assert.IsTrue(o.NoLock);
            Assert.IsTrue(o.ForcePerformanceMode);
            Assert.That(o.Get("note"), Is.EqualTo("hello world again"));
            Assert.That(o.Get("x"), Is.EqualTo(string.Empty));
            Assert.IsNull(o.Get("missing"));
        }

        [Test]
        public void NullAndGarbageAreSafe()
        {
            Assert.DoesNotThrow(() => LaunchOptions.Parse(null));
            Assert.DoesNotThrow(() => LaunchOptions.Parse("?&&==&%E0%A4%A"));
            Assert.IsNull(LaunchOptions.Parse("#").Checkpoint);
        }
    }
}
