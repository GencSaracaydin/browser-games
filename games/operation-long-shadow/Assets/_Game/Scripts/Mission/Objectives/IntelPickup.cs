using LongShadow.Core;
using LongShadow.Mission.Data;
using UnityEngine;

namespace LongShadow.Mission.Objectives
{
    /// <summary>
    /// An intel item the player takes with HOLD [E] for 1.2 s (TECH_PLAN §7.11 IntelPickup; GDD §7.7-7.8: the ledgers,
    /// the S-3 reels, optional O1 and O2). Spawned at its Appendix A marker by the mission (<c>MissionSpawner</c>); the
    /// interaction collects it through <see cref="IMissionDirector"/>'s owner (INTEL n/3, intel card, line). Needs a
    /// non-trigger collider on layer Interactable (the spawner adds one around the model).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IntelPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _intelId;
        [SerializeField] private Transform _anchor;

        private System.Func<string, bool> _collect;
        private System.Func<string, bool> _isCollected;

        /// <summary>The intel id ("Intel.Ledgers").</summary>
        public string IntelId => _intelId;

        /// <summary>Sets the item and the collect callbacks (spawner).</summary>
        public void Configure(string intelId, System.Func<string, bool> collect, System.Func<string, bool> isCollected, Transform anchor = null)
        {
            _intelId = intelId;
            _collect = collect;
            _isCollected = isCollected;
            _anchor = anchor;
        }

        /// <inheritdoc/>
        public string PromptText => IntelTable.Get(_intelId)?.Prompt ?? "HOLD [E] Take";
        /// <inheritdoc/>
        public bool Hold => true;
        /// <inheritdoc/>
        public float HoldSeconds => IntelDef.HoldSeconds;
        /// <inheritdoc/>
        public Transform Anchor => _anchor != null ? _anchor : transform;

        /// <inheritdoc/>
        public bool CanInteract(IPlayerRig player) =>
            isActiveAndEnabled && _collect != null && !string.IsNullOrEmpty(_intelId) && (_isCollected == null || !_isCollected(_intelId));

        /// <inheritdoc/>
        public void Interact(IPlayerRig player)
        {
            if (!CanInteract(player)) return;
            _collect(_intelId);
        }
    }
}
