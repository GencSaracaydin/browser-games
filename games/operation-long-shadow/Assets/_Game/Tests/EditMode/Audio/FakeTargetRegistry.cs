using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>A list-backed ITargetRegistry.</summary>
    internal sealed class FakeTargetRegistry : ITargetRegistry
    {
        public readonly List<ITargetable> Actors = new List<ITargetable>();
        public ITargetable PlayerActor;

        public void Register(ITargetable t) => Actors.Add(t);
        public void Unregister(ITargetable t) => Actors.Remove(t);
        public void GetHostilesOf(Team team, List<ITargetable> results) => results.AddRange(Actors);
        public int CountWithin(Team team, Vector3 position, float radius) => 0;
        public ITargetable Find(string actorId) => null;
        public ITargetable Player => PlayerActor;
        public void GetAll(List<ITargetable> results) => results.AddRange(Actors);
    }
}
