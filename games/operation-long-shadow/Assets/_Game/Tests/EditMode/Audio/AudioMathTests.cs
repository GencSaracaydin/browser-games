using LongShadow.Audio.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN §7.9: near/far crossfade (near &lt; 40 m, far &gt; 120 m), speed of sound, slow-motion pitch.</summary>
    public class AudioMathTests
    {
        [Test]
        public void FarShareCrossfadesWithEqualPower()
        {
            Assert.That(AudioMath.FarShare(10f, 40f, 120f, out var n, out var f), Is.EqualTo(0f));
            Assert.That(n, Is.EqualTo(1f));
            Assert.That(f, Is.EqualTo(0f));
            Assert.That(AudioMath.FarShare(80f, 40f, 120f, out n, out f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(n * n + f * f, Is.EqualTo(1f).Within(1e-4f), "constant power through the blend");
            Assert.That(n, Is.EqualTo(f).Within(1e-4f));
            Assert.That(AudioMath.FarShare(500f, 40f, 120f, out n, out f), Is.EqualTo(1f));
            Assert.That(n, Is.EqualTo(0f));
            Assert.That(f, Is.EqualTo(1f));
        }

        [Test]
        public void PropagationDelayIsDistanceOverSpeedOfSound()
        {
            Assert.That(AudioMath.PropagationDelay(10f), Is.EqualTo(0f), "masked below 20 m");
            Assert.That(AudioMath.PropagationDelay(AudioConstants.SpeedOfSound), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(AudioMath.PropagationDelay(5000f), Is.EqualTo(AudioConstants.SpeedOfSoundMaxDelay));
            Assert.That(AudioMath.PropagationDelay(float.NaN), Is.EqualTo(0f));
        }

        [Test]
        public void SlowMotionDipsPitchButPauseDoesNot()
        {
            Assert.That(AudioMath.SlowMotionPitch(1f, 0.6f), Is.EqualTo(1f));
            Assert.That(AudioMath.SlowMotionPitch(0f, 0.6f), Is.EqualTo(1f), "inserts and pause freeze, never bend");
            Assert.That(AudioMath.SlowMotionPitch(0.25f, 0.6f), Is.EqualTo(0.6f).Within(1e-4f));
            var mid = AudioMath.SlowMotionPitch(0.45f, 0.6f);
            Assert.That(mid, Is.InRange(0.6f, 1f));
        }

        [Test]
        public void RolloffGainFollowsUnityLogCurve()
        {
            Assert.That(AudioMath.RolloffGain(1f, 2f, 50f, AudioRolloffMode.Logarithmic), Is.EqualTo(1f));
            Assert.That(AudioMath.RolloffGain(4f, 2f, 50f, AudioRolloffMode.Logarithmic), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(AudioMath.RolloffGain(26f, 2f, 50f, AudioRolloffMode.Linear), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(AudioMath.RolloffGain(60f, 2f, 50f, AudioRolloffMode.Linear), Is.EqualTo(0f));
        }

        [Test]
        public void JitterStaysInRange()
        {
            for (var i = 0; i <= 10; i++)
            {
                var r = i / 10f;
                Assert.That(AudioMath.JitterPitch(0.03f, r), Is.InRange(0.97f - 1e-5f, 1.03f + 1e-5f));
                Assert.That(AudioMath.JitterGain(1.5f, r), Is.InRange(Mathf.Pow(10f, -1.5f / 20f) - 1e-5f, Mathf.Pow(10f, 1.5f / 20f) + 1e-5f));
            }
            Assert.That(AudioMath.JitterPitch(0f, 0.9f), Is.EqualTo(1f));
        }
    }
}
