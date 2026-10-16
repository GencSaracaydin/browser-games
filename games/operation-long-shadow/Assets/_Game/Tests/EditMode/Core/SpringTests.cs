using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Core
{
    public class SpringTests
    {
        [Test]
        public void CriticallyDampedConvergesWithoutOvershoot()
        {
            var s = new SpringFloat(4f, 1f) { Target = 1f };
            var max = 0f;
            for (var i = 0; i < 240; i++) max = Mathf.Max(max, s.Step(1f / 60f));
            Assert.That(s.Value, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(max, Is.LessThanOrEqualTo(1.0001f));
        }

        [Test]
        public void UnderdampedOvershoots()
        {
            var s = new SpringFloat(4f, 0.3f) { Target = 1f };
            var max = 0f;
            for (var i = 0; i < 240; i++) max = Mathf.Max(max, s.Step(1f / 60f));
            Assert.That(max, Is.GreaterThan(1.05f));
        }

        [Test]
        public void StableForHugeTimeSteps()
        {
            var s = new SpringFloat(20f, 0.5f) { Target = 5f };
            for (var i = 0; i < 20; i++) s.Step(1f);
            Assert.That(float.IsNaN(s.Value), Is.False);
            Assert.That(s.Value, Is.EqualTo(5f).Within(0.01f));
        }

        [Test]
        public void ImpulseReturnsToRest()
        {
            var s = new SpringFloat(6f, 0.8f);
            s.AddImpulse(3f);
            s.Step(0.02f);
            Assert.That(s.Value, Is.GreaterThan(0f));
            for (var i = 0; i < 300; i++) s.Step(0.02f);
            Assert.That(s.Value, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Vector3AndQuaternionConverge()
        {
            var v = new SpringVector3(5f, 1f) { Target = new Vector3(1, 2, 3) };
            for (var i = 0; i < 300; i++) v.Step(1f / 60f);
            Assert.That((v.Value - new Vector3(1, 2, 3)).magnitude, Is.LessThan(1e-3f));

            var q = new SpringQuaternion(5f, 1f);
            q.Target = Quaternion.Euler(10f, 20f, 0f);
            for (var i = 0; i < 300; i++) q.Step(1f / 60f);
            Assert.That(Quaternion.Angle(q.Value, q.Target), Is.LessThan(0.05f));
            q.Punch(Quaternion.Euler(-3f, 0f, 0f));
            for (var i = 0; i < 300; i++) q.Step(1f / 60f);
            Assert.That(Quaternion.Angle(q.Value, q.Target), Is.LessThan(0.05f));
        }
    }
}
