using System;
using System.Collections;
using LongShadow.Core;
using LongShadow.Platform;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LongShadow.Tests.PlayMode.Platform
{
    /// <summary>
    /// TECH_PLAN §5.4 in play mode with the real services (registered in Awake): pause freezes time, mutes the listener,
    /// locks every input channel and raises PauseChanged; resume undoes all of it; time-scale priorities stack.
    /// </summary>
    public class PauseFlowTests
    {
        private GameObject _root;

        [TearDown]
        public void Cleanup()
        {
            if (_root != null) Object.Destroy(_root);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [UnityTest]
        public IEnumerator PauseFreezesTimeLocksInputAndResumes()
        {
            _root = new GameObject("BootRootTest");
            _root.AddComponent<TimeScaleService>();
            var locks = _root.AddComponent<InputLockService>();
            _root.AddComponent<PointerLockController>();
            var pause = _root.AddComponent<PauseService>();
            Assert.That(GameServices.Get<IPauseService>(), Is.SameAs(pause));

            bool? raised = null;
            Action<bool> onPause = p => raised = p;
            GameEvents.PauseChanged += onPause;
            try
            {
                pause.Pause();
                Assert.IsFalse(pause.IsPaused, "pausing is off until the click gate (CanPause)");

                pause.CanPause = true;
                pause.Pause();
                Assert.IsTrue(pause.IsPaused);
                Assert.That(Time.timeScale, Is.EqualTo(0f));
                Assert.IsTrue(AudioListener.pause);
                Assert.IsTrue(locks.IsLocked(InputLockFlags.All));
                Assert.That(raised, Is.EqualTo(true));
                yield return null;

                pause.Resume();
                Assert.IsFalse(pause.IsPaused);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.IsFalse(AudioListener.pause);
                Assert.IsFalse(locks.IsLocked(InputLockFlags.All));
                Assert.That(raised, Is.EqualTo(false));
            }
            finally
            {
                GameEvents.PauseChanged -= onPause;
            }
        }

        [UnityTest]
        public IEnumerator TimeScalePrioritiesStack()
        {
            _root = new GameObject("TimeTest");
            var time = _root.AddComponent<TimeScaleService>();
            var cine = time.Push(0.5f, TimePriority.Cinematic, "cinematic");
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            var insert = time.Push(0f, TimePriority.Insert, "insert");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            yield return null;
            time.Pop(insert);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.01f).Within(1e-5f), "physics step shrinks with the slow-down");
            time.Pop(cine);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(1e-5f));
        }
    }
}
