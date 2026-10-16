using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Hollis/Sparrow/Tinker control (WP04 SquadService). Member ids are in <see cref="SquadIds"/>.</summary>
    public interface ISquadService
    {
        /// <summary>Sends the squad to a <see cref="SquadNodeSet"/> by id.</summary>
        void SetNodeSet(string setId);
        /// <summary>Follow mode along the critical path.</summary>
        void Follow();
        /// <summary>Teleports the squad onto a node set (checkpoints).</summary>
        void TeleportToNodeSet(string setId);
        /// <summary>True when every member is within 1.5 m of its node.</summary>
        bool AllArrived(string setId);
        /// <summary>Plays a scripted action at a marker; <paramref name="done"/> fires when finished.</summary>
        void PlayScripted(string member, string actionId, string markerId, Action done);
        /// <summary>Scripted lethal shot by a member.</summary>
        void ScriptedKill(string member, ITargetable target);
        /// <summary>Chemlights on/off (Beat 6 on).</summary>
        void SetChemlights(bool on);
        /// <summary>Overrides every member's look-at (null clears; overrides speaker/player/threat look-at).</summary>
        void SetLookTarget(Transform target, float weight);
        /// <summary>Member root by id, or null.</summary>
        Transform Member(string id);
    }
}
