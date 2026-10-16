using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.UI.Dev
{
    /// <summary><see cref="ISquadService"/> stand-in: member transforms only (nameplates, compass dots, FOLLOW markers).</summary>
    public sealed class FakeSquadService : ISquadService
    {
        private readonly Dictionary<string, Transform> _members = new Dictionary<string, Transform>();

        /// <summary>Registers a member transform ("HOL", "SPA", "TIN").</summary>
        public void SetMember(string id, Transform t) => _members[id] = t;

        public Transform Member(string id) => id != null && _members.TryGetValue(id, out var t) ? t : null;
        public void SetNodeSet(string setId) { }
        public void Follow() { }
        public void TeleportToNodeSet(string setId) { }
        public bool AllArrived(string setId) => true;
        public void PlayScripted(string member, string actionId, string markerId, Action done) => done?.Invoke();
        public void ScriptedKill(string member, ITargetable target) { }
        public void SetChemlights(bool on) { }
        public void SetLookTarget(Transform target, float weight) { }
    }
}
