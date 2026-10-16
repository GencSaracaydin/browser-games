using LongShadow.Core;
using UnityEngine;

namespace LongShadow.UI.Dev
{
    /// <summary>An <see cref="ITargetable"/> wrapping a transform (test-bed sentries for the detection meter).</summary>
    public sealed class FakeTargetable : ITargetable
    {
        private readonly Transform _root;

        public FakeTargetable(string id, Transform root, Team team = Team.Legion)
        {
            ActorId = id;
            _root = root;
            Team = team;
        }

        public string ActorId { get; }
        public Transform Root => _root;
        public Vector3 Velocity => Vector3.zero;
        public PerchTag Perch => PerchTag.Ground;
        public bool SquadTarget { get; set; }
        public float LastEngagedByPlayer { get; private set; }
        public Team Team { get; }
        public bool IsAlive { get; set; } = true;
        public Transform AimPoint => _root;

        public void NotifyEngagedByPlayer() => LastEngagedByPlayer = Time.time;

        public void ApplyDamage(in DamageInfo d) => IsAlive = false;
    }
}
