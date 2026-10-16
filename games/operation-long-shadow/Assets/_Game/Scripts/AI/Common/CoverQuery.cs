using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>What an agent wants from a cover node (see <see cref="CoverSelector.Select"/>).</summary>
    public struct CoverQuery
    {
        /// <summary>Claiming agent id.</summary>
        public int Agent;
        /// <summary>Agent position.</summary>
        public Vector3 AgentPosition;
        /// <summary>The threat to hide from (player or last known position).</summary>
        public Vector3 ThreatPosition;
        /// <summary>Preferred distance band to the threat (archetype range).</summary>
        public float PreferredMin;
        /// <summary>Preferred distance band to the threat (archetype range).</summary>
        public float PreferredMax;
        /// <summary>Search radius around the agent (0 = default 30 m).</summary>
        public float SearchRadius;
        /// <summary>Greycoats want full (standing) cover; Legion take half cover (GDD §14.1).</summary>
        public bool PreferStanding;
        /// <summary>Preferred node group ("B2.AlertCover"); nodes of the group win ties strongly.</summary>
        public string PreferredGroup;
        /// <summary>Only nodes at least <see cref="MinGain"/> m closer to the threat (Legion push).</summary>
        public bool Advance;
        /// <summary>Only nodes at least <see cref="MinGain"/> m farther from the threat (fallback "one node back").</summary>
        public bool Retreat;
        /// <summary>Gain for <see cref="Advance"/> / <see cref="Retreat"/> (0 = default 4 m).</summary>
        public float MinGain;
        /// <summary>Prefer nodes that change the angle to the threat (Greycoat repositioning, flanks).</summary>
        public bool FlankBias;
        /// <summary>Node to exclude (the current one when relocating).</summary>
        public CoverNode Exclude;
    }
}
