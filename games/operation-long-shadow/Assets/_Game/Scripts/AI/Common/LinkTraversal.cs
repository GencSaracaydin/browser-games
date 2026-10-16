using LongShadow.Core;
using UnityEngine;
using UnityEngine.AI;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Off-mesh link traversal for AI bodies (TECH_PLAN §7.15 NavMeshLinks: the wadi boulder, roof planks, tank stand,
    /// steel stairs, R3/R4 drops, the ladder; WP05: "WP04 handles NavMeshLink traversal by calling PlayAction(Vault|
    /// Climb|Drop) and moving the agent across the link"). Agents run with <c>autoTraverseOffMeshLink</c> off; when one
    /// reaches a link this picks the clip by height (climb up more than 1.4 m, a ladder above 2.6 m, drop down more than
    /// 1 m, otherwise a vault), plays it on the body and moves the agent along the link over the clip's length with a
    /// small arc, then completes the link. Plain C#; one per agent.
    /// </summary>
    public sealed class LinkTraversal
    {
        /// <summary>Rise above which a link is climbed (GDD §12 climb 1.4-2.1 m).</summary>
        public const float ClimbRise = 1.4f;
        /// <summary>Rise above which a link is a ladder (scripted climb, GDD §15).</summary>
        public const float LadderRise = 2.6f;
        /// <summary>Fall beyond which a link is a drop.</summary>
        public const float DropFall = 1.0f;
        /// <summary>Ladder climbing speed (m/s, TECH_PLAN §7.3).</summary>
        public const float LadderSpeed = 2.2f;

        private bool _active;
        private Vector3 _start;
        private Vector3 _end;
        private float _time;
        private float _duration;
        private float _arc;

        /// <summary>True while crossing a link.</summary>
        public bool Active => _active;

        /// <summary>The action a link of this rise and length uses (pure).</summary>
        public static string ActionFor(float rise, float horizontal)
        {
            if (rise > LadderRise) return ActionIds.LadderClimb;
            if (rise > ClimbRise) return ActionIds.Climb;
            if (rise < -DropFall) return ActionIds.Drop;
            return horizontal > 0.4f || rise > 0.5f ? ActionIds.Vault : null;
        }

        /// <summary>Configures an agent for manual link traversal.</summary>
        public static void Prepare(NavMeshAgent agent)
        {
            if (agent != null) agent.autoTraverseOffMeshLink = false;
        }

        /// <summary>Advances the traversal; call every frame before other movement logic. True while traversing.</summary>
        public bool Tick(NavMeshAgent agent, ICharacterBody body, float dt)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                _active = false;
                return false;
            }
            if (!_active)
            {
                if (!agent.isOnOffMeshLink) return false;
                var data = agent.currentOffMeshLinkData;
                _start = agent.transform.position;
                _end = data.endPos + Vector3.up * agent.baseOffset;
                var d = _end - _start;
                var horizontal = new Vector2(d.x, d.z).magnitude;
                var action = ActionFor(d.y, horizontal);
                if (action == ActionIds.LadderClimb) _duration = Mathf.Max(1f, d.y / LadderSpeed);
                else if (action != null) _duration = Mathf.Max(0.35f, ActionDurations.Of(action));
                else _duration = Mathf.Max(0.25f, horizontal / Mathf.Max(1f, agent.speed));
                _arc = action == ActionIds.Vault ? 0.35f : 0f;
                _time = 0f;
                _active = true;
                if (action != null) body?.PlayAction(action);
            }
            _time += dt;
            var u = Mathf.Clamp01(_time / _duration);
            var p = Vector3.Lerp(_start, _end, u) + Vector3.up * (_arc * Mathf.Sin(u * Mathf.PI));
            var flat = _end - _start;
            flat.y = 0f;
            agent.transform.position = p;
            if (flat.sqrMagnitude > 1e-4f) agent.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            if (u < 1f) return true;
            agent.CompleteOffMeshLink();
            _active = false;
            return false;
        }
    }
}
