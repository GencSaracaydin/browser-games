using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>All live targetable actors (WP04 TargetRegistry). Non-allocating queries.</summary>
    public interface ITargetRegistry
    {
        /// <summary>Adds an actor (idempotent).</summary>
        void Register(ITargetable t);
        /// <summary>Removes an actor (idempotent).</summary>
        void Unregister(ITargetable t);
        /// <summary>Fills <paramref name="results"/> (cleared first) with live actors hostile to <paramref name="team"/>.</summary>
        void GetHostilesOf(Team team, List<ITargetable> results);
        /// <summary>Counts live actors of <paramref name="team"/> within <paramref name="radius"/> metres of a point.</summary>
        int CountWithin(Team team, Vector3 position, float radius);
        /// <summary>Finds an actor by id, or null.</summary>
        ITargetable Find(string actorId);
        /// <summary>The player as a targetable (null before the rig registers).</summary>
        ITargetable Player { get; }
        /// <summary>Fills <paramref name="results"/> (cleared first) with every registered actor.</summary>
        void GetAll(List<ITargetable> results);
    }
}
