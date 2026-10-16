using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>A designated flank path (Appendix A kind "R", "B3.Route.WaveB"): world-space points in order.</summary>
    public class FlankRoute : SceneMarker
    {
        [SerializeField, Tooltip("World-space points, first to last.")]
        private Vector3[] _points = new Vector3[0];

        /// <summary>Route points (world space).</summary>
        public IReadOnlyList<Vector3> Points => _points;
        /// <summary>Route length in metres.</summary>
        public float Length => MathX.PolylineLength(_points);

        /// <summary>Sets the points (builders).</summary>
        public void SetPoints(Vector3[] points) => _points = points ?? new Vector3[0];

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(1f, 0.4f, 0.4f, 0.9f);

        protected override void OnDrawGizmos()
        {
            if (_points == null || _points.Length == 0)
            {
                base.OnDrawGizmos();
                return;
            }
            Gizmos.color = GizmoColor;
            for (var i = 1; i < _points.Length; i++)
            {
                Gizmos.DrawLine(_points[i - 1], _points[i]);
                Gizmos.DrawWireSphere(_points[i], 0.15f);
            }
            MarkupGizmos.Label(_points[0] + Vector3.up * 0.5f, Id, GizmoColor);
        }
#endif
    }
}
