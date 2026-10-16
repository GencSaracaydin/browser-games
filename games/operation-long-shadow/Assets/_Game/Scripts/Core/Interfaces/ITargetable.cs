using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>An actor AI can see, target and shoot (enemies, squad, player). Registered in <see cref="ITargetRegistry"/>.</summary>
    public interface ITargetable : IDamageable
    {
        /// <summary>Stable id ("PLAYER", "HOL", "E3-02").</summary>
        string ActorId { get; }
        /// <summary>Root transform (feet).</summary>
        Transform Root { get; }
        /// <summary>World velocity in m/s.</summary>
        Vector3 Velocity { get; }
        /// <summary>Where the actor stands (callouts, perch rules).</summary>
        PerchTag Perch { get; }
        /// <summary>Squad may kill this actor (scripted targets, RPG after the first wave-off, TECH_PLAN §7.7).</summary>
        bool SquadTarget { get; set; }
        /// <summary><see cref="Time.time"/> when the player last shot at or hit this actor (kill-share rule, 8 s).</summary>
        float LastEngagedByPlayer { get; }
        /// <summary>Called by player weapons when they engage this actor.</summary>
        void NotifyEngagedByPlayer();
    }
}
