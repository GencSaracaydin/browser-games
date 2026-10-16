using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Generic scripted interaction placed by level builders (WP10/WP09): doors ("[E] Open" cinema side door,
    /// "[E] Kick" Hall door), the projector ("[E] Thread the projector"), switches. Using it raises
    /// <see cref="GameEvents.Interacted"/> with its id; the beat script (WP07) decides what happens and enables it with
    /// <see cref="SetAvailable"/>. Crates, pickups and intel use their own <see cref="IInteractable"/> types.
    /// Needs a non-trigger collider on layer Interactable (physics queries ignore triggers, TECH_PLAN §3.9); Reset adds one.
    /// </summary>
    public class InteractionPoint : SceneMarker, IInteractable, ISaveable
    {
        [SerializeField, Tooltip("Prompt including the key, e.g. \"[E] Open\" or \"HOLD [E] Take the ledgers\".")]
        private string _promptText = "[E] Use";
        [SerializeField] private bool _hold;
        [SerializeField, Min(0f)] private float _holdSeconds = 1f;
        [SerializeField, Tooltip("Usable from the start; otherwise a script calls SetAvailable(true).")]
        private bool _startsAvailable = true;
        [SerializeField, Tooltip("Becomes unavailable after one use.")]
        private bool _once = true;
        [SerializeField, Tooltip("Prompt anchor / LOS target (empty = this transform).")]
        private Transform _anchor;

        private bool _available;
        private bool _used;
        private bool _initialised;

        /// <summary>Raised after a successful interaction (also <see cref="GameEvents.Interacted"/>).</summary>
        public event Action<InteractionPoint> Used;

        /// <inheritdoc/>
        public string PromptText => _promptText;
        /// <inheritdoc/>
        public bool Hold => _hold;
        /// <inheritdoc/>
        public float HoldSeconds => _hold ? _holdSeconds : 0f;
        /// <inheritdoc/>
        public Transform Anchor => _anchor != null ? _anchor : transform;
        /// <summary>True while it can be used.</summary>
        public bool IsAvailable
        {
            get
            {
                Init();
                return _available && !(_once && _used);
            }
        }
        /// <summary>True after it was used at least once.</summary>
        public bool WasUsed => _used;

        /// <summary>Configures the point (builders).</summary>
        public void Configure(string promptText, bool hold, float holdSeconds, bool startsAvailable, bool once)
        {
            _promptText = promptText;
            _hold = hold;
            _holdSeconds = holdSeconds;
            _startsAvailable = startsAvailable;
            _once = once;
        }

        /// <summary>Enables or disables the interaction (scripts).</summary>
        public void SetAvailable(bool available)
        {
            Init();
            _available = available;
        }

        /// <summary>Back to the authored state (checkpoint before the interaction).</summary>
        public void ResetState()
        {
            _initialised = true;
            _available = _startsAvailable;
            _used = false;
        }

        private void Awake() => Init();

        private void Init()
        {
            if (_initialised) return;
            ResetState();
        }

        /// <inheritdoc/>
        public bool CanInteract(IPlayerRig player) => enabled && gameObject.activeInHierarchy && IsAvailable;

        /// <inheritdoc/>
        public void Interact(IPlayerRig player)
        {
            if (!CanInteract(player)) return;
            _used = true;
            Used?.Invoke(this);
            if (!string.IsNullOrEmpty(Id)) GameEvents.RaiseInteracted(Id);
        }

        /// <inheritdoc/>
        public string SaveKey => "Interaction." + (string.IsNullOrEmpty(Id) ? name : Id);

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            Init();
            w.Write("available", _available);
            w.Write("used", _used);
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            Init();
            _available = r.ReadBool("available", _startsAvailable);
            _used = r.ReadBool("used", false);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (GetComponent<Collider>() == null) gameObject.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 0.3f);
            gameObject.layer = LayerIds.Interactable;
        }

        protected override Color GizmoColor => new Color(0.4f, 1f, 0.9f, 0.9f);

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            MarkupGizmos.ColliderShape(GetComponent<Collider>(), GizmoColor);
            MarkupGizmos.Label(Anchor.position + Vector3.up * 0.3f, _promptText, GizmoColor);
        }
#endif
    }
}
