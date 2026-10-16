using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Positions for Hollis, Sparrow and Tinker (Appendix A kind "N", e.g. "B3.NodeSet.M2"). Members count as arrived
    /// within <see cref="ArriveRadius"/> (1.5 m). <see cref="AllowDoorway"/> lets the squad stop in Doorway areas (B8 reel).
    /// </summary>
    public class SquadNodeSet : SceneMarker
    {
        [SerializeField] private SquadNode _hollis;
        [SerializeField] private SquadNode _sparrow;
        [SerializeField] private SquadNode _tinker;
        [SerializeField] private bool _allowDoorway;
        [SerializeField] private float _arriveRadius = 1.5f;

        /// <summary>Hollis slot.</summary>
        public SquadNode Hollis => _hollis;
        /// <summary>Sparrow slot.</summary>
        public SquadNode Sparrow => _sparrow;
        /// <summary>Tinker slot.</summary>
        public SquadNode Tinker => _tinker;
        /// <summary>Squad may stop in Doorway nav areas.</summary>
        public bool AllowDoorway => _allowDoorway;
        /// <summary>Arrival radius in metres.</summary>
        public float ArriveRadius => _arriveRadius;

        /// <summary>Slot for a member id (<see cref="SquadIds"/>); default if unknown.</summary>
        public SquadNode Get(string memberId)
        {
            switch (memberId)
            {
                case SquadIds.Hollis: return _hollis;
                case SquadIds.Sparrow: return _sparrow;
                case SquadIds.Tinker: return _tinker;
                default: return default;
            }
        }

        /// <summary>World position for a member (falls back to the set's position).</summary>
        public Vector3 PositionOf(string memberId)
        {
            var n = Get(memberId);
            return n.IsValid ? n.Point.position : transform.position;
        }

        /// <summary>World facing for a member (falls back to the set's rotation).</summary>
        public Quaternion RotationOf(string memberId)
        {
            var n = Get(memberId);
            return n.IsValid ? n.Point.rotation : transform.rotation;
        }

        /// <summary>Sets the slots (builders, MarkupTools).</summary>
        public void Configure(SquadNode hollis, SquadNode sparrow, SquadNode tinker, bool allowDoorway = false)
        {
            _hollis = hollis;
            _sparrow = sparrow;
            _tinker = tinker;
            _allowDoorway = allowDoorway;
        }

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(0.3f, 0.8f, 1f, 0.9f);

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            DrawSlot(_hollis, "HOL");
            DrawSlot(_sparrow, "SPA");
            DrawSlot(_tinker, "TIN");
        }

        private void DrawSlot(SquadNode n, string label)
        {
            if (!n.IsValid) return;
            var p = n.Point.position;
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(p, 0.3f);
            Gizmos.DrawLine(transform.position, p);
            MarkupGizmos.Arrow(p, n.Point.forward, 0.5f, GizmoColor);
            MarkupGizmos.Label(p + Vector3.up * 0.4f, label, GizmoColor);
        }
#endif
    }
}
