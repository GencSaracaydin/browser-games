using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Progression gate or backtrack blocker (Appendix A kind "K": "Gate.*", "Block.*"; GDD §6.7, §7.13). Closed =
    /// blocker colliders on and closed visuals shown. WP10 places it; WP07 Gates opens/closes it and saves the state.
    /// </summary>
    public class GateMarker : SceneMarker
    {
        [SerializeField] private bool _startsOpen;
        [SerializeField, Tooltip("PlayerClip / door colliders enabled while closed.")]
        private Collider[] _blockers = new Collider[0];
        [SerializeField] private GameObject[] _closedVisuals = new GameObject[0];
        [SerializeField] private GameObject[] _openVisuals = new GameObject[0];

        private bool _open;
        private bool _initialised;

        /// <summary>Raised when the state changes (also <see cref="GameEvents.GateChanged"/>).</summary>
        public event Action<GateMarker, bool> Changed;

        /// <summary>True when open.</summary>
        public bool IsOpen
        {
            get
            {
                if (!_initialised) Apply(_startsOpen, false);
                return _open;
            }
        }

        private void Awake()
        {
            if (!_initialised) Apply(_startsOpen, false);
        }

        /// <summary>Opens or closes the gate.</summary>
        public void SetOpen(bool open)
        {
            if (_initialised && _open == open) return;
            Apply(open, true);
        }

        /// <summary>Back to the authored state (checkpoints before the gate).</summary>
        public void ResetState() => Apply(_startsOpen, true);

        /// <summary>Configures the gate (builders).</summary>
        public void Configure(bool startsOpen, Collider[] blockers, GameObject[] closedVisuals, GameObject[] openVisuals)
        {
            _startsOpen = startsOpen;
            _blockers = blockers ?? new Collider[0];
            _closedVisuals = closedVisuals ?? new GameObject[0];
            _openVisuals = openVisuals ?? new GameObject[0];
        }

        private void Apply(bool open, bool notify)
        {
            _initialised = true;
            _open = open;
            foreach (var c in _blockers)
                if (c != null) c.enabled = !open;
            foreach (var g in _closedVisuals)
                if (g != null) g.SetActive(!open);
            foreach (var g in _openVisuals)
                if (g != null) g.SetActive(open);
            if (!notify) return;
            Changed?.Invoke(this, open);
            if (!string.IsNullOrEmpty(Id)) GameEvents.RaiseGateChanged(Id, open);
        }

#if UNITY_EDITOR
        protected override Color GizmoColor => _startsOpen ? new Color(0.3f, 1f, 0.3f, 0.9f) : new Color(1f, 0.2f, 0.6f, 0.9f);

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            foreach (var c in _blockers) MarkupGizmos.ColliderShape(c, GizmoColor);
        }
#endif
    }
}
