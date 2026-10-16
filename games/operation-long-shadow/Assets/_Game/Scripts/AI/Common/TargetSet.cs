using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// The logic behind <see cref="TargetRegistry"/> (pure C#, testable): every live <see cref="ITargetable"/>, hostility
    /// queries and the player. When the player rig has not registered itself, <see cref="PlayerFallback"/> supplies it
    /// (TargetRegistry looks for an <see cref="ITargetable"/> behind IPlayerHealth / IPlayerRig). Non-allocating queries.
    /// </summary>
    public sealed class TargetSet
    {
        private readonly List<ITargetable> _all = new List<ITargetable>(32);
        private ITargetable _player;

        /// <summary>Supplies the player when none registered with team Player (may return null).</summary>
        public Func<ITargetable> PlayerFallback { get; set; }

        /// <summary>Registered actors (including dead ones that have not unregistered yet).</summary>
        public int Count => _all.Count;

        /// <summary>The player (registered with <see cref="Team.Player"/>, else the fallback).</summary>
        public ITargetable Player
        {
            get
            {
                if (IsValid(_player)) return _player;
                _player = null;
                for (var i = 0; i < _all.Count; i++)
                {
                    var t = _all[i];
                    if (IsValid(t) && t.Team == Team.Player)
                    {
                        _player = t;
                        return t;
                    }
                }
                var f = PlayerFallback?.Invoke();
                return IsValid(f) ? f : null;
            }
        }

        /// <summary>Adds an actor (idempotent).</summary>
        public void Register(ITargetable t)
        {
            if (!IsValid(t) || _all.Contains(t)) return;
            _all.Add(t);
            if (t.Team == Team.Player) _player = t;
        }

        /// <summary>Removes an actor (idempotent).</summary>
        public void Unregister(ITargetable t)
        {
            if (t == null) return;
            _all.Remove(t);
            if (ReferenceEquals(_player, t)) _player = null;
        }

        /// <summary>Clears every actor (tests).</summary>
        public void Clear()
        {
            _all.Clear();
            _player = null;
        }

        /// <summary>Fills <paramref name="results"/> (cleared first) with live actors hostile to <paramref name="team"/>.</summary>
        public void GetHostilesOf(Team team, List<ITargetable> results)
        {
            results.Clear();
            Prune();
            var playerListed = false;
            for (var i = 0; i < _all.Count; i++)
            {
                var t = _all[i];
                if (!t.IsAlive || !Teams.AreHostile(team, t.Team)) continue;
                results.Add(t);
                if (t.Team == Team.Player) playerListed = true;
            }
            if (!playerListed && Teams.AreHostile(team, Team.Player))
            {
                var p = Player;
                if (p != null && p.IsAlive && !results.Contains(p)) results.Add(p);
            }
        }

        /// <summary>Counts live actors of a team within a radius.</summary>
        public int CountWithin(Team team, Vector3 position, float radius)
        {
            Prune();
            var r2 = radius * radius;
            var n = 0;
            for (var i = 0; i < _all.Count; i++)
            {
                var t = _all[i];
                if (t.Team != team || !t.IsAlive || t.Root == null) continue;
                if ((t.Root.position - position).sqrMagnitude <= r2) n++;
            }
            return n;
        }

        /// <summary>Finds an actor by id (the player answers to "PLAYER").</summary>
        public ITargetable Find(string actorId)
        {
            if (string.IsNullOrEmpty(actorId)) return null;
            Prune();
            for (var i = 0; i < _all.Count; i++)
                if (_all[i].ActorId == actorId) return _all[i];
            if (actorId == SquadIds.Player) return Player;
            return null;
        }

        /// <summary>Fills <paramref name="results"/> (cleared first) with every registered actor.</summary>
        public void GetAll(List<ITargetable> results)
        {
            results.Clear();
            Prune();
            results.AddRange(_all);
        }

        private void Prune()
        {
            for (var i = _all.Count - 1; i >= 0; i--)
                if (!IsValid(_all[i])) _all.RemoveAt(i);
        }

        private static bool IsValid(ITargetable t)
        {
            if (t == null) return false;
            if (t is Object o) return o != null;
            return true;
        }
    }
}
