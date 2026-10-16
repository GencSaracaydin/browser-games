using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// AI cover spot (TECH_PLAN §6.4). Forward faces the threat side. <see cref="ArcDegrees"/> is the arc of threat
    /// directions it protects against. Claims are kept by the AI's CoverNodeRegistry, not here.
    /// </summary>
    public class CoverNode : SceneMarker
    {
        [SerializeField, Tooltip("Standing (full) cover; false = crouch (half) cover.")]
        private bool _standing;
        [SerializeField] private bool _leanLeft = true;
        [SerializeField] private bool _leanRight = true;
        [SerializeField, Range(10f, 360f)] private float _arcDegrees = 120f;
        [SerializeField, Tooltip("Group id (e.g. \"B2.AlertCover\", \"B5.WT\").")]
        private string _group;

        /// <summary>Full-height cover.</summary>
        public bool Standing => _standing;
        /// <summary>Can peek left.</summary>
        public bool LeanLeft => _leanLeft;
        /// <summary>Can peek right.</summary>
        public bool LeanRight => _leanRight;
        /// <summary>Protected arc in degrees around forward.</summary>
        public float ArcDegrees => _arcDegrees;
        /// <summary>Group id.</summary>
        public string Group => _group;

        /// <summary>True if a threat at <paramref name="threatPosition"/> is inside the protected arc.</summary>
        public bool Protects(Vector3 threatPosition)
        {
            var d = threatPosition - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return false;
            var f = transform.forward;
            f.y = 0f;
            return Vector3.Angle(f, d) <= _arcDegrees * 0.5f;
        }

        /// <summary>Peek position to one side (0.6 m lateral by default).</summary>
        public Vector3 PeekPosition(bool left, float lateral = 0.6f) =>
            transform.position + (left ? -transform.right : transform.right) * lateral;

        /// <summary>Configures the node (builders).</summary>
        public void Configure(bool standing, bool leanLeft, bool leanRight, float arcDegrees, string group)
        {
            _standing = standing;
            _leanLeft = leanLeft;
            _leanRight = leanRight;
            _arcDegrees = arcDegrees;
            _group = group;
        }

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(0.6f, 0.6f, 1f, 0.9f);

        protected override void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            var h = _standing ? 1.6f : 1.0f;
            Gizmos.DrawWireCube(transform.position + transform.forward * 0.3f + Vector3.up * h * 0.5f, new Vector3(0.8f, h, 0.1f));
            MarkupGizmos.Arc(transform.position + Vector3.up * 0.05f, transform.forward, _arcDegrees, 1.2f, GizmoColor);
            MarkupGizmos.Label(transform.position + Vector3.up * (h + 0.2f), string.IsNullOrEmpty(Id) ? _group : Id, GizmoColor);
        }
#endif
    }
}
