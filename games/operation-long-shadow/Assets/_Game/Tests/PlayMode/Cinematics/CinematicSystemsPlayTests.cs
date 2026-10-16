using System.Collections;
using LongShadow.Cinematics;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LongShadow.Tests.PlayMode.Cinematics
{
    /// <summary>
    /// The WP09 runtime alone in an empty scene (no booth, no player, no audio): services register, fades work, a
    /// sequence starts and can be skipped with its callback and events, inserts freeze and restore without a booth.
    /// Systems must degrade, never throw (TECH_PLAN §5.3).
    /// </summary>
    public sealed class CinematicSystemsPlayTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CinematicSystems_Test");
            _root.AddComponent<CinematicSystems>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_root);
            SeenFlags.ClearMemoryForTests();
        }

        [UnityTest]
        public IEnumerator Services_RegisterAndFadesCoverTheScreen()
        {
            yield return null;
            Assert.IsNotNull(GameServices.Get<ICinematicService>());
            Assert.IsNotNull(GameServices.Get<IInsertService>());
            var fx = GameServices.Get<IScreenFX>();
            Assert.IsNotNull(fx);
            fx.FadeToBlack(0f);
            yield return null;
            Assert.IsTrue(fx.IsBlack);
            fx.FadeIn(0f);
            yield return null;
            Assert.IsFalse(fx.IsBlack);
        }

        [UnityTest]
        public IEnumerator Sequence_SkipCompletesWithEventsAndCallback()
        {
            yield return null;
            var service = GameServices.Get<ICinematicService>();
            var started = 0;
            var ended = 0;
            var done = false;
            void OnStart(string id) => started++;
            void OnEnd(string id) => ended++;
            GameEvents.CinematicStarted += OnStart;
            GameEvents.CinematicEnded += OnEnd;
            try
            {
                service.Play(SequenceIds.MidB, () => done = true);
                yield return null;
                Assert.IsTrue(service.IsPlaying);
                Assert.AreEqual(SequenceIds.MidB, service.Current);
                service.Skip();
                yield return null;
                Assert.IsTrue(done, "onDone after a skip");
                Assert.IsFalse(service.IsPlaying);
                Assert.AreEqual(1, started);
                Assert.AreEqual(1, ended);
            }
            finally
            {
                GameEvents.CinematicStarted -= OnStart;
                GameEvents.CinematicEnded -= OnEnd;
            }
        }

        [UnityTest]
        public IEnumerator Insert_PlaysAndRestoresWithoutABooth()
        {
            yield return null;
            var inserts = GameServices.Get<IInsertService>();
            var started = false;
            var ended = false;
            void OnStart(string id) => started = true;
            void OnEnd(string id) => ended = true;
            GameEvents.InsertStarted += OnStart;
            GameEvents.InsertEnded += OnEnd;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                inserts.Play(SequenceIds.InsertI2);
                Assert.IsTrue(inserts.IsPlaying);
                var t = 0f;
                while (inserts.IsPlaying && t < 3f)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                Assert.IsFalse(inserts.IsPlaying, "I2 finishes");
                Assert.IsTrue(started && ended);
                Assert.IsFalse(AudioListener.pause, "gameplay audio resumes");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                GameEvents.InsertStarted -= OnStart;
                GameEvents.InsertEnded -= OnEnd;
            }
        }
    }
}
