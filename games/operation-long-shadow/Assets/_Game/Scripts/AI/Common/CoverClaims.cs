using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Cover-node claims (TECH_PLAN §7.6 CoverNodeRegistry): one agent per <see cref="CoverNode"/>. Claims live here, not
    /// on the markup (the nodes are data only). Pure C# keyed by node instance.
    /// </summary>
    public sealed class CoverClaims
    {
        private readonly Dictionary<CoverNode, int> _byNode = new Dictionary<CoverNode, int>();
        private readonly Dictionary<int, CoverNode> _byAgent = new Dictionary<int, CoverNode>();

        /// <summary>Number of claimed nodes.</summary>
        public int Count => _byNode.Count;

        /// <summary>True if another agent holds the node.</summary>
        public bool IsClaimedByOther(CoverNode node, int agent) =>
            node != null && _byNode.TryGetValue(node, out var a) && a != agent;

        /// <summary>The node an agent holds, or null.</summary>
        public CoverNode ClaimOf(int agent) => _byAgent.TryGetValue(agent, out var n) && n != null ? n : null;

        /// <summary>Claims a node for an agent (releasing its previous claim). False if another agent holds it.</summary>
        public bool TryClaim(CoverNode node, int agent)
        {
            if (node == null) return false;
            if (IsClaimedByOther(node, agent)) return false;
            Release(agent);
            _byNode[node] = agent;
            _byAgent[agent] = node;
            return true;
        }

        /// <summary>Releases the agent's claim.</summary>
        public void Release(int agent)
        {
            if (!_byAgent.TryGetValue(agent, out var n)) return;
            _byAgent.Remove(agent);
            if (n != null && _byNode.TryGetValue(n, out var a) && a == agent) _byNode.Remove(n);
        }

        /// <summary>Releases everything.</summary>
        public void Clear()
        {
            _byNode.Clear();
            _byAgent.Clear();
        }
    }
}
