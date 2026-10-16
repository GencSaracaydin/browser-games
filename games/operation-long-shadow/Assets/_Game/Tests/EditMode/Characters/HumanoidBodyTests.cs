using LongShadow.Characters.Animation;
using LongShadow.Characters.Body;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Characters
{
    /// <summary>The ICharacterBody action contract without an animator (scripts must never hang; slots are independent).</summary>
    public class HumanoidBodyTests
    {
        private GameObject _go;
        private HumanoidBody _body;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Body");
            _body = _go.AddComponent<HumanoidBody>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            LongShadow.Characters.Ragdoll.CorpseRegistry.Clear(); // OnDisable does not run in edit mode
        }

        [Test]
        public void UnknownActionCompletesImmediately()
        {
            var done = 0;
            _body.PlayAction("NoSuchAction", () => done++); // logs one warning (not a failure)
            Assert.That(done, Is.EqualTo(1));
            Assert.That(_body.IsActionPlaying, Is.False);
        }

        [Test]
        public void LoopsEstablishImmediatelyAndArmsActionsKeepTheSeat()
        {
            var sat = 0;
            _body.PlayAction(ActionIds.SitHeli, () => sat++);
            Assert.That(sat, Is.EqualTo(1), "a loop calls back as soon as it is established");
            Assert.That(_body.CurrentActionId, Is.EqualTo(ActionIds.SitHeli));
            var handset = 0;
            _body.PlayAction(ActionIds.HandsetToEar, () => handset++);
            Assert.That(handset, Is.EqualTo(1));
            Assert.That(_body.CurrentActionId, Is.EqualTo(ActionIds.SitHeli), "an arms action does not stand the body up");
            Assert.That(_body.CurrentArmsActionId, Is.EqualTo(ActionIds.HandsetToEar));
            var drop = 0;
            _body.PlayAction(ActionIds.SkidDrop, () => drop++);
            Assert.That(_body.CurrentArmsActionId, Is.Null, "a full-body action ends the arms action");
            Assert.That(_body.CurrentActionId, Is.EqualTo(ActionIds.SkidDrop));
            Assert.That(drop, Is.EqualTo(0), "a one-shot completes when it ends");
            Assert.That(_body.IsActionPlaying, Is.True);
            _body.PlayAction(ExtraActionIds.Stop);
            Assert.That(drop, Is.EqualTo(1), "Stop completes the pending callback");
            Assert.That(_body.CurrentActionId, Is.Null);
            Assert.That(_body.IsActionPlaying, Is.False);
        }

        [Test]
        public void MovingEndsLoopsButNotTraversals()
        {
            var a = 0;
            _body.PlayAction(ActionIds.ProneIdle, () => a++);
            _body.MoveTo(new Vector3(5f, 0f, 0f), 1.4f);
            Assert.That(_body.CurrentActionId, Is.Null, "a held full-body action ends on MoveTo");
            var v = 0;
            _body.PlayAction(ActionIds.Vault, () => v++);
            _body.MoveTo(new Vector3(6f, 0f, 0f), 3f);
            Assert.That(_body.CurrentActionId, Is.EqualTo(ActionIds.Vault), "a traversal keeps playing while the brain re-paths");
            Assert.That(v, Is.EqualTo(0));
        }

        [Test]
        public void CrouchRunIsAStanceAndDeadBodiesCompleteAtOnce()
        {
            var c = 0;
            _body.PlayAction(ActionIds.CrouchRun, () => c++);
            Assert.That(c, Is.EqualTo(1));
            Assert.That(_body.Stance, Is.EqualTo(Stance.Crouch));
            _body.Die(new DamageInfo(100f, DamageType.Bullet, HitZone.Torso, Vector3.zero, Vector3.forward, null, Team.Player));
            Assert.That(_body.IsDead, Is.True);
            var d = 0;
            _body.PlayAction(ActionIds.Reload, () => d++);
            Assert.That(d, Is.EqualTo(1));
        }
    }
}
