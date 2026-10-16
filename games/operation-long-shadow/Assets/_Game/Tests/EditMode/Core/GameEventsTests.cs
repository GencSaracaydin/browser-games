using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Core
{
    public class GameEventsTests
    {
        [TearDown]
        public void TearDown() => GameEvents.ResetAll();

        [Test]
        public void RaiseInvokesSubscribersAndResetClears()
        {
            string trigger = null;
            var kills = 0;
            NoiseEvent heard = default;
            GameEvents.TriggerEntered += (id, c) => trigger = id;
            GameEvents.ActorKilled += k => kills++;
            GameEvents.Noise += n => heard = n;
            GameEvents.RaiseTriggerEntered("B3.T.Z185", null);
            GameEvents.RaiseActorKilled(new KillInfo { VictimId = "E3-01" });
            GameEvents.RaiseNoise(new NoiseEvent(Vector3.one, 12f, Team.Player, NoiseKind.Footstep));
            Assert.That(trigger, Is.EqualTo("B3.T.Z185"));
            Assert.That(kills, Is.EqualTo(1));
            Assert.That(heard.Radius, Is.EqualTo(12f));
            GameEvents.ResetAll();
            GameEvents.RaiseActorKilled(default);
            Assert.That(kills, Is.EqualTo(1));
        }

        [Test]
        public void ResetAllClearsEveryEvent()
        {
            // Reflection is fine in editor tests; it guards R6 (every new event must be reset).
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var events = typeof(GameEvents).GetEvents(flags);
            Assert.That(events.Length, Is.GreaterThanOrEqualTo(50));
            foreach (var e in events)
            {
                var field = typeof(GameEvents).GetField(e.Name, flags);
                Assert.IsNotNull(field, e.Name);
                var handlerType = e.EventHandlerType;
                var invoke = handlerType.GetMethod("Invoke");
                var parameters = invoke.GetParameters();
                var lambdaParams = new System.Linq.Expressions.ParameterExpression[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                    lambdaParams[i] = System.Linq.Expressions.Expression.Parameter(parameters[i].ParameterType);
                var handler = System.Linq.Expressions.Expression.Lambda(handlerType, System.Linq.Expressions.Expression.Empty(), lambdaParams).Compile();
                e.AddEventHandler(null, handler);
                Assert.IsNotNull(field.GetValue(null), e.Name + " subscribed");
            }
            GameEvents.ResetAll();
            foreach (var e in events)
                Assert.IsNull(typeof(GameEvents).GetField(e.Name, flags).GetValue(null), e.Name + " not cleared by ResetAll");
        }

        [Test]
        public void NewEventsRaise()
        {
            string interacted = null, cleared = null, wave = null;
            bool? combat = null, mounted = null;
            GameEvents.Interacted += id => interacted = id;
            GameEvents.EncounterCleared += id => cleared = id;
            GameEvents.WaveSpawned += (enc, w) => wave = enc + "/" + w;
            GameEvents.CombatStateChanged += c => combat = c;
            GameEvents.PlayerMountChanged += m => mounted = m;
            GameEvents.RaiseInteracted("B4.SideDoor");
            GameEvents.RaiseEncounterCleared("E3");
            GameEvents.RaiseWaveSpawned("E3", "B");
            GameEvents.RaiseCombatStateChanged(true);
            GameEvents.RaisePlayerMountChanged(true);
            Assert.That(interacted, Is.EqualTo("B4.SideDoor"));
            Assert.That(cleared, Is.EqualTo("E3"));
            Assert.That(wave, Is.EqualTo("E3/B"));
            Assert.That(combat, Is.True);
            Assert.That(mounted, Is.True);
        }

        [Test]
        public void RaiseWithoutSubscribersIsSafe()
        {
            GameEvents.ResetAll();
            Assert.DoesNotThrow(() =>
            {
                GameEvents.RaisePlayerRespawned();
                GameEvents.RaiseLightStageChanged(1.5f);
                GameEvents.RaiseSubtitleShow(default);
                GameEvents.RaiseSectionLoaded(SectionId.Market);
            });
        }
    }
}
