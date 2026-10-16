using LongShadow.Audio.Dev;
using LongShadow.Audio.Foley;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>Every AI and squad body gets footsteps; the player does not (WP02 does its own).</summary>
    public class FootstepDirectorTests
    {
        private FakeTargetRegistry _registry;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _registry = new FakeTargetRegistry();
            GameServices.Register<ITargetRegistry>(_registry);
            _go = new GameObject("Footsteps");
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Unregister<ITargetRegistry>(_registry);
            Object.DestroyImmediate(_go);
            AudioTestUtil.Cleanup();
        }

        private AuditionBody Body(string name, Team team)
        {
            var b = AudioTestUtil.Track(new GameObject(name)).AddComponent<AuditionBody>();
            b.BodyTeam = team;
            _registry.Actors.Add(b);
            return b;
        }

        [Test]
        public void AddsFootstepsToBodiesOnce()
        {
            var enemy = Body("E3-01", Team.Legion);
            var squad = Body("HOL", Team.Squad);
            var player = Body("PLAYER", Team.Player);
            _registry.PlayerActor = player;
            var d = _go.AddComponent<FootstepDirector>();
            Assert.That(d.Scan(), Is.EqualTo(2));
            Assert.That(enemy.GetComponent<CharacterFootsteps>(), Is.Not.Null);
            Assert.That(squad.GetComponent<CharacterFootsteps>(), Is.Not.Null);
            Assert.That(player.GetComponent<CharacterFootsteps>(), Is.Null);
            Assert.That(d.Scan(), Is.EqualTo(0), "idempotent");
        }
    }
}
