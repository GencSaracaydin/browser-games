using System;
using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Trigger box or sphere (Appendix A kind "T"). Raises <see cref="GameEvents.TriggerEntered"/>/<see cref="GameEvents.TriggerExited"/>
    /// with its id. Needs a trigger collider (a BoxCollider is added on Reset); the object is put on layer Trigger, which
    /// only meets Player and Character (TECH_PLAN §3.9). The CharacterController generates trigger messages by itself;
    /// AI capsules need a kinematic Rigidbody.
    /// <para><b>playerOnly</b>: only the player counts. <b>once</b>: fires on the first entry only. <b>armed</b>: a disarmed
    /// trigger tracks occupancy but raises nothing; arming it while the player is inside fires immediately (Appendix A
    /// "armed by the INTEL 1/3 gate").</para>
    /// <para>Occupancy is tracked per collider: a repeated OnTriggerEnter from a collider already inside (Unity re-sends it
    /// when a CharacterController is re-enabled after a mount or teleport) is ignored, and colliders that were destroyed or
    /// disabled without an OnTriggerExit stop counting.</para>
    /// </summary>
    public class TriggerVolume : SceneMarker
    {
        [SerializeField] private bool _playerOnly = true;
        [SerializeField] private bool _once;
        [SerializeField, Tooltip("Initial armed state; scripts arm gates with SetArmed(true).")]
        private bool _startArmed = true;

        private bool _armed;
        private bool _fired;
        private readonly List<Collider> _players = new List<Collider>(2);
        private readonly List<Collider> _others = new List<Collider>(4);
        private Collider _lastPlayer;
        private Collider _collider;

        /// <summary>Raised on a counted entry (after the armed/once filters).</summary>
        public event Action<TriggerVolume, Collider> Entered;
        /// <summary>Raised on a counted exit.</summary>
        public event Action<TriggerVolume, Collider> Exited;

        /// <summary>Only the player counts.</summary>
        public bool PlayerOnly => _playerOnly;
        /// <summary>Fires once.</summary>
        public bool Once => _once;
        /// <summary>Currently armed.</summary>
        public bool Armed => _armed;
        /// <summary>True after it fired (once triggers stop firing).</summary>
        public bool HasFired => _fired;
        /// <summary>True while the player is inside.</summary>
        public bool PlayerInside => Prune(_players) > 0;
        /// <summary>Non-player characters inside (when not playerOnly).</summary>
        public int OthersInside => Prune(_others);
        /// <summary>The trigger collider.</summary>
        public Collider Volume => _collider != null ? _collider : (_collider = GetComponent<Collider>());

        /// <summary>Configures the trigger (builders).</summary>
        public void Configure(bool playerOnly, bool once, bool startArmed)
        {
            _playerOnly = playerOnly;
            _once = once;
            _startArmed = startArmed;
        }

        protected virtual void Awake()
        {
            _armed = _startArmed;
            var c = Volume;
            if (c == null)
            {
                LSLog.Error(LogCategory.Level, "TriggerVolume '" + Id + "' has no collider", this);
                return;
            }
            c.isTrigger = true;
            if (gameObject.layer == LayerIds.Default) gameObject.layer = LayerIds.Trigger;
        }

        /// <summary>Arms or disarms. Arming while the player is inside fires now.</summary>
        public void SetArmed(bool armed)
        {
            if (_armed == armed) return;
            _armed = armed;
            if (armed && PlayerInside && _lastPlayer != null) Fire(_lastPlayer);
        }

        /// <summary>Forgets the fired state (checkpoint restore).</summary>
        public void ResetState()
        {
            _fired = false;
            _armed = _startArmed;
        }

        /// <summary>True if the point is inside the trigger collider.</summary>
        public bool Contains(Vector3 point)
        {
            var c = Volume;
            if (c == null) return false;
            return (c.ClosestPoint(point) - point).sqrMagnitude < 1e-6f;
        }

        /// <summary>True for the player's collider (layer Player or tag Player).</summary>
        public static bool IsPlayer(Collider c) => c != null && (c.gameObject.layer == LayerIds.Player || c.CompareTag(Tags.Player));

        private void OnTriggerEnter(Collider other)
        {
            var isPlayer = IsPlayer(other);
            if (isPlayer)
            {
                Prune(_players);
                if (_players.Contains(other)) return; // repeated enter without an exit (re-enabled controller)
                _players.Add(other);
                _lastPlayer = other;
            }
            else
            {
                if (_playerOnly) return;
                Prune(_others);
                if (_others.Contains(other)) return;
                _others.Add(other);
            }
            Fire(other);
        }

        private void OnTriggerExit(Collider other)
        {
            var isPlayer = IsPlayer(other);
            if (isPlayer)
            {
                if (!_players.Remove(other)) return;
            }
            else
            {
                if (_playerOnly) return;
                if (!_others.Remove(other)) return;
            }
            if (!_armed) return;
            Exited?.Invoke(this, other);
            if (!string.IsNullOrEmpty(Id)) GameEvents.RaiseTriggerExited(Id, other);
        }

        private void Fire(Collider other)
        {
            if (!_armed || (_once && _fired)) return;
            _fired = true;
            Entered?.Invoke(this, other);
            if (!string.IsNullOrEmpty(Id)) GameEvents.RaiseTriggerEntered(Id, other);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _players.Clear();
            _others.Clear();
        }

        /// <summary>Drops colliders that were destroyed or disabled without an exit; returns the remaining count.</summary>
        private static int Prune(List<Collider> list)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var c = list[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) list.RemoveAt(i);
            }
            return list.Count;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (GetComponent<Collider>() == null)
            {
                var b = gameObject.AddComponent<BoxCollider>();
                b.isTrigger = true;
            }
            gameObject.layer = LayerIds.Trigger;
        }

        protected override Color GizmoColor => new Color(0.2f, 0.9f, 0.4f, 0.8f);

        protected override void OnDrawGizmos()
        {
            MarkupGizmos.ColliderShape(Volume, GizmoColor);
            MarkupGizmos.Label(transform.position + Vector3.up * 0.5f, Id, GizmoColor);
        }
#endif
    }
}
