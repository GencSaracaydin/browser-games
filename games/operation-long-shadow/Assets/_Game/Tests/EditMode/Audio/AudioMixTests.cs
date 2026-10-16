using LongShadow.Audio.Core;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN D9: category gains and ducking applied to AudioSource.volume without an AudioMixer.</summary>
    public class AudioMixTests
    {
        [Test]
        public void NeutralMixIsUnity()
        {
            var m = new AudioMix();
            m.Tick(0f, 0.016f);
            for (var c = 0; c < MixCategories.Count; c++) Assert.That(m.Gain((MixCategory)c), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void SliderTaperIsSquared()
        {
            Assert.That(AudioMix.Taper(1f), Is.EqualTo(1f));
            Assert.That(AudioMix.Taper(0.5f), Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(AudioMix.Taper(-1f), Is.EqualTo(0f));
            Assert.That(AudioMix.Taper(float.NaN), Is.EqualTo(1f));
        }

        [Test]
        public void UserVolumesMapToCategories()
        {
            var m = new AudioMix();
            m.SetUserVolumes(1f, 0.5f, 1f, 0f);
            Assert.That(m.Gain(MixCategory.Music), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(m.Gain(MixCategory.Voice), Is.EqualTo(0f));
            Assert.That(m.Gain(MixCategory.Effects), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(m.Gain(MixCategory.Ambience), Is.EqualTo(1f).Within(1e-5f), "ambience follows the Effects slider");
            Assert.That(m.Gain(MixCategory.Insert), Is.EqualTo(1f).Within(1e-5f), "inserts ignore the sliders except master");
        }

        [Test]
        public void MasterScriptGainScalesEverything()
        {
            var m = new AudioMix();
            m.SetCategoryGain(AudioCategories.Master, 0.5f);
            Assert.That(m.Gain(MixCategory.Music), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(m.Gain(MixCategory.UI), Is.EqualTo(0.5f).Within(1e-5f));
            m.SetCategoryGain(AudioCategories.Voice, 0.5f);
            Assert.That(m.Gain(MixCategory.Voice), Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void DeepestDuckWinsAndReleases()
        {
            var m = new AudioMix { AttackSeconds = 0f, ReleaseSeconds = 0f };
            m.Duck(MixCategory.Music, -6f, 2f, 0f);
            m.Duck(MixCategory.Music, 12f, 1f, 0f); // positive values are cuts too
            m.Tick(0.1f, 0.1f);
            Assert.That(m.DuckDb(MixCategory.Music), Is.EqualTo(-12f).Within(0.01f));
            m.Tick(1.5f, 0.1f);
            Assert.That(m.DuckDb(MixCategory.Music), Is.EqualTo(-6f).Within(0.01f), "the -12 dB duck expired, -6 dB remains");
            m.Tick(2.5f, 0.1f);
            Assert.That(m.DuckDb(MixCategory.Music), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void HoldsLastUntilCleared()
        {
            var m = new AudioMix { AttackSeconds = 0f, ReleaseSeconds = 0f };
            m.SetHold(7, MixCategory.Ambience, -4f);
            m.SetHold(8, MixCategory.Ambience, -6f);
            m.Tick(100f, 0.1f);
            Assert.That(m.DuckDb(MixCategory.Ambience), Is.EqualTo(-6f).Within(0.01f));
            m.ClearHolds(8);
            m.Tick(100.1f, 0.1f);
            Assert.That(m.DuckDb(MixCategory.Ambience), Is.EqualTo(-4f).Within(0.01f));
            m.ClearHold(7, MixCategory.Ambience);
            m.Tick(100.2f, 0.1f);
            Assert.That(m.DuckDb(MixCategory.Ambience), Is.EqualTo(0f).Within(0.01f));
            Assert.That(m.HoldCount, Is.EqualTo(0));
        }

        [Test]
        public void DuckAttackIsFasterThanRelease()
        {
            var m = new AudioMix();
            m.SetHold(1, MixCategory.Music, -6f);
            m.Tick(0f, 0.06f);
            var afterAttack = m.DuckDb(MixCategory.Music);
            Assert.That(afterAttack, Is.LessThan(-3.5f), "about 63 % of the cut after one attack time");
            for (var i = 0; i < 60; i++) m.Tick(i * 0.016f, 0.016f);
            m.ClearHolds(1);
            m.Tick(1f, 0.06f);
            Assert.That(m.DuckDb(MixCategory.Music), Is.LessThan(-4.5f), "the release is slow: no pumping");
        }

        [Test]
        public void InsertGainMultipliesTheInsertBus()
        {
            var m = new AudioMix();
            m.SetCategoryGain(AudioCategories.Insert, 0.5f);
            Assert.That(m.GainDuringInsert(MixCategory.Voice), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(m.GainDuringInsert(MixCategory.Insert), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(m.Gain(MixCategory.Voice), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void CategoryNamesRoundTrip()
        {
            foreach (var n in new[] { AudioCategories.Music, AudioCategories.Effects, AudioCategories.Voice, AudioCategories.UI, AudioCategories.Insert, AudioCategories.Ambience })
                Assert.That(MixCategories.Name(MixCategories.Parse(n)), Is.EqualTo(n));
            Assert.That(MixCategories.Parse("nonsense"), Is.EqualTo(MixCategory.Effects));
            Assert.That(MixCategories.IgnoresListenerPause(MixCategory.UI) && MixCategories.IgnoresListenerPause(MixCategory.Insert), Is.True);
            Assert.That(MixCategories.IgnoresListenerPause(MixCategory.Music), Is.False);
            Assert.That(MixCategories.UnityPriority(MixCategory.Effects, 100), Is.LessThan(MixCategories.UnityPriority(MixCategory.Effects, 0)));
        }

        [Test]
        public void DbConversionIsConsistent()
        {
            Assert.That(MathX.DbToLinear(-6f), Is.EqualTo(0.501f).Within(0.002f));
            Assert.That(Mathf.Abs(MathX.LinearToDb(0.5f) + 6.02f), Is.LessThan(0.01f));
        }
    }
}
