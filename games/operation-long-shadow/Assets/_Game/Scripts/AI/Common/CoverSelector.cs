using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Cover choice (GDD §14.3 "Cover"): the nearest unclaimed hand-placed <see cref="CoverNode"/> whose protected arc
    /// faces the threat (breaks line of sight) and that allows a peek, inside the archetype's preferred range. Optional
    /// ray checks (budgeted by the caller) reject nodes the threat can see over. Allocation-free.
    /// </summary>
    public static class CoverSelector
    {
        /// <summary>Default search radius in metres.</summary>
        public const float DefaultSearchRadius = 30f;
        /// <summary>Default advance/retreat gain in metres.</summary>
        public const float DefaultGain = 4f;

        private struct Scored
        {
            public CoverNode Node;
            public float Score;
        }

        private static readonly List<Scored> s_scratch = new List<Scored>(64);
        private static readonly Comparison<Scored> s_byScore = (a, b) => a.Score.CompareTo(b.Score);

        /// <summary>
        /// Picks the best node or null. <paramref name="isExposed"/> (optional) is evaluated in score order on at most
        /// <paramref name="maxRayChecks"/> nodes; a node it reports exposed is skipped.
        /// </summary>
        public static CoverNode Select(in CoverQuery q, List<CoverNode> candidates, CoverClaims claims,
                                       Func<CoverNode, bool> isExposed = null, int maxRayChecks = 0)
        {
            s_scratch.Clear();
            if (candidates == null || candidates.Count == 0) return null;
            var radius = q.SearchRadius > 0f ? q.SearchRadius : DefaultSearchRadius;
            var r2 = radius * radius;
            var gain = q.MinGain > 0f ? q.MinGain : DefaultGain;
            var agentToThreat = Planar(q.ThreatPosition - q.AgentPosition).magnitude;
            for (var i = 0; i < candidates.Count; i++)
            {
                var n = candidates[i];
                if (n == null || !n.enabled || !n.gameObject.activeInHierarchy || n == q.Exclude) continue;
                if (claims != null && claims.IsClaimedByOther(n, q.Agent)) continue;
                var p = n.Position;
                var dAgent2 = (p - q.AgentPosition).sqrMagnitude;
                if (dAgent2 > r2) continue;
                if (!n.Protects(q.ThreatPosition)) continue;
                if (!n.LeanLeft && !n.LeanRight && n.Standing) continue; // no way to peek
                var dThreat = Planar(q.ThreatPosition - p).magnitude;
                if (q.Advance && dThreat > agentToThreat - gain) continue;
                if (q.Retreat && dThreat < agentToThreat + gain) continue;
                s_scratch.Add(new Scored { Node = n, Score = Score(q, n, Mathf.Sqrt(dAgent2), dThreat) });
            }
            if (s_scratch.Count == 0) return null;
            s_scratch.Sort(s_byScore);
            CoverNode fallback = s_scratch[0].Node;
            if (isExposed == null || maxRayChecks <= 0)
            {
                s_scratch.Clear();
                return fallback;
            }
            var checks = 0;
            for (var i = 0; i < s_scratch.Count && checks < maxRayChecks; i++, checks++)
            {
                var n = s_scratch[i].Node;
                if (!isExposed(n))
                {
                    s_scratch.Clear();
                    return n;
                }
            }
            // Every checked node was exposed: the best unchecked one, else the best overall (half cover beats none).
            var result = s_scratch.Count > checks ? s_scratch[checks].Node : fallback;
            s_scratch.Clear();
            return result;
        }

        /// <summary>Score of a node (lower is better).</summary>
        public static float Score(in CoverQuery q, CoverNode n, float distanceToAgent, float distanceToThreat)
        {
            var s = distanceToAgent;
            if (q.PreferredMax > 0f)
            {
                if (distanceToThreat < q.PreferredMin) s += (q.PreferredMin - distanceToThreat) * 1.5f;
                else if (distanceToThreat > q.PreferredMax) s += (distanceToThreat - q.PreferredMax) * 1.5f;
            }
            if (!string.IsNullOrEmpty(q.PreferredGroup) && n.Group == q.PreferredGroup) s -= 15f;
            if (n.Standing != q.PreferStanding) s += 4f;
            if (q.FlankBias)
            {
                var a = Planar(q.AgentPosition - q.ThreatPosition);
                var b = Planar(n.Position - q.ThreatPosition);
                if (a.sqrMagnitude > 1e-3f && b.sqrMagnitude > 1e-3f) s -= Vector3.Angle(a, b) * 0.15f;
            }
            return s;
        }

        private static Vector3 Planar(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
