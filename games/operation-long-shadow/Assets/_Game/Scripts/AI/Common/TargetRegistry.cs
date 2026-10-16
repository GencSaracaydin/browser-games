using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// <see cref="ITargetRegistry"/> (TECH_PLAN §7.6): all live targetable actors (enemies, squad, player). Lives in
    /// S_Persistent on PF_AIServices; created on demand by <see cref="EnsureExists"/> in test scenes. The player rig should
    /// register itself; when it does not, the player is found through IPlayerHealth / IPlayerRig if either is an
    /// <see cref="ITargetable"/>.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.AI - 50)]
    [DisallowMultipleComponent]
    public sealed class TargetRegistry : MonoBehaviour, ITargetRegistry
    {
        private readonly TargetSet _set = new TargetSet();
        private ServiceRef<IPlayerHealth> _playerHealth;
        private ServiceRef<IPlayerRig> _playerRig;

        /// <summary>The underlying set (diagnostics, dev arena).</summary>
        public TargetSet Set => _set;

        private void Awake()
        {
            _set.PlayerFallback = ResolvePlayer;
            GameServices.Register<ITargetRegistry>(this);
        }

        private void OnDestroy() => GameServices.Unregister<ITargetRegistry>(this);

        private ITargetable ResolvePlayer()
        {
            if (_playerHealth.Value is ITargetable h) return h;
            if (_playerRig.Value is ITargetable r) return r;
            return null;
        }

        /// <inheritdoc/>
        public void Register(ITargetable t) => _set.Register(t);
        /// <inheritdoc/>
        public void Unregister(ITargetable t) => _set.Unregister(t);
        /// <inheritdoc/>
        public void GetHostilesOf(Team team, List<ITargetable> results) => _set.GetHostilesOf(team, results);
        /// <inheritdoc/>
        public int CountWithin(Team team, Vector3 position, float radius) => _set.CountWithin(team, position, radius);
        /// <inheritdoc/>
        public ITargetable Find(string actorId) => _set.Find(actorId);
        /// <inheritdoc/>
        public ITargetable Player => _set.Player;
        /// <inheritdoc/>
        public void GetAll(List<ITargetable> results) => _set.GetAll(results);

        /// <summary>
        /// Returns the registered <see cref="ITargetRegistry"/>, creating a <see cref="TargetRegistry"/> at runtime when
        /// none exists (test scenes without S_Persistent). Never creates one in edit mode.
        /// </summary>
        public static ITargetRegistry EnsureExists()
        {
            if (GameServices.TryGet(out ITargetRegistry existing)) return existing;
            if (!Application.isPlaying) return null;
            var go = new GameObject("[TargetRegistry]");
            var r = go.AddComponent<TargetRegistry>();
            LSLog.WarnOnce("ai-autocreate-targets", LogCategory.AI,
                "No ITargetRegistry in the scene; created one (S_Persistent should contain PF_AIServices).");
            return r;
        }
    }
}
