using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Squad
{
    /// <summary>
    /// Squad callouts (TECH_PLAN §7.7 SquadCallouts, GDD §15): the first sighting of each enemy is called by the member
    /// who saw it, relative to the player's view (front ±45°, left, right, rear) or by perch tag ("Rooftop!",
    /// "Balcony!", "Window!", also "Rooftop!" for anyone 4 m or more above the player); reload callouts with a 20 s
    /// cooldown per member; all with a 4 s global cooldown and <see cref="VOPriority.Callout"/> (below scripted VO).
    /// A sighting that cannot be voiced within 4 s is dropped. Plain C#, owned by <see cref="SquadService"/>.
    /// </summary>
    public sealed class SquadCallouts
    {
        /// <summary>Global cooldown (GDD §15).</summary>
        public const float GlobalCooldown = 4f;
        /// <summary>Reload-callout cooldown per member (GDD §15).</summary>
        public const float ReloadCooldown = 20f;
        /// <summary>A sighting older than this is no longer called.</summary>
        public const float StaleSeconds = 4f;

        private readonly Dictionary<ITargetable, float> _seen = new Dictionary<ITargetable, float>();
        private readonly Dictionary<string, float> _reloadNext = new Dictionary<string, float>();
        private readonly List<ITargetable> _scratch = new List<ITargetable>(8);
        private ServiceRef<IVOService> _vo;
        private float _globalNext = float.NegativeInfinity;

        /// <summary>The last line requested (diagnostics, tests).</summary>
        public string LastLine { get; private set; }
        /// <summary>Lines requested so far (diagnostics, tests).</summary>
        public int Count { get; private set; }
        /// <summary>Optional sink for tests (receives every voiced line instead of / besides the VO service).</summary>
        public System.Action<string, SquadMember> Sink { get; set; }

        /// <summary>Forgets cooldowns and sightings (checkpoint).</summary>
        public void Reset()
        {
            _seen.Clear();
            _reloadNext.Clear();
            _globalNext = float.NegativeInfinity;
        }

        /// <summary>Voices a line if the global cooldown allows it (or <paramref name="force"/>). True when sent.</summary>
        public bool Say(SquadMember member, string lineId, float now, bool force = false)
        {
            if (string.IsNullOrEmpty(lineId) || member == null) return false;
            if (!force && now < _globalNext) return false;
            _globalNext = now + GlobalCooldown;
            LastLine = lineId;
            Count++;
            Sink?.Invoke(lineId, member);
            _vo.Value?.Play(lineId, new VOOptions { Speaker = member.Head, Priority = VOPriority.Callout });
            return true;
        }

        /// <summary>A member reloads: "Reloading!" at most every 20 s per member.</summary>
        public void OnReload(SquadMember member, float now)
        {
            if (member == null) return;
            if (_reloadNext.TryGetValue(member.MemberId, out var next) && now < next) return;
            if (Say(member, CalloutIds.Reload(member.MemberId), now)) _reloadNext[member.MemberId] = now + ReloadCooldown;
        }

        /// <summary>The line for an enemy sighting (pure, used by tests): perch tag first, else the view direction.</summary>
        public static string LineFor(string memberId, Vector3 playerPosition, Vector3 viewForward, Vector3 enemyPosition, PerchTag perch)
        {
            if (perch == PerchTag.Ground && enemyPosition.y - playerPosition.y >= CalloutDirection.RooftopHeight) perch = PerchTag.Rooftop;
            var dir = CalloutDirection.Classify(playerPosition, viewForward, enemyPosition);
            return CalloutIds.Spot(memberId, dir, perch);
        }

        /// <summary>Calls out new sightings (call a few times per second).</summary>
        public void TickSpotting(IReadOnlyList<SquadMember> members, Vector3 playerPosition, Vector3 viewForward, float now)
        {
            // Forget dead or despawned enemies (pooled actors come back as new enemies).
            _scratch.Clear();
            foreach (var kv in _seen)
                if (kv.Key == null || !kv.Key.IsAlive || (kv.Key is Object o && o == null)) _scratch.Add(kv.Key);
            for (var i = 0; i < _scratch.Count; i++) _seen.Remove(_scratch[i]);
            _scratch.Clear();

            for (var m = 0; m < members.Count; m++)
            {
                var member = members[m];
                var perception = member?.Perception;
                if (perception == null || member.IsBusy) continue;
                var mems = perception.Memories;
                for (var i = 0; i < mems.Count; i++)
                {
                    var mem = mems[i];
                    var t = mem.Target;
                    if (!mem.Aware || !mem.Visible || t == null || !t.IsAlive || t.Root == null || !Teams.AreHostile(Team.Squad, t.Team)) continue;
                    if (_seen.TryGetValue(t, out var first))
                    {
                        if (first < 0f) continue;                                               // already called (or dropped)
                        if (now - first > StaleSeconds)
                        {
                            _seen[t] = -1f;                                                     // too old: never called
                            continue;
                        }
                    }
                    else _seen[t] = now;
                    var line = LineFor(member.MemberId, playerPosition, viewForward, t.Root.position, t.Perch);
                    if (Say(member, line, now)) _seen[t] = -1f;
                    return;                                                                     // at most one per tick
                }
            }
        }
    }
}
