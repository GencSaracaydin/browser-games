using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Beat 1 binocular point of interest (Appendix A kind "G"): ticks off after the view centre stays within
    /// <see cref="RadiusDegrees"/> (4 deg) for <see cref="DwellSeconds"/> (1.0 s), GDD §7.1.
    /// </summary>
    public class GlassingPOI : SceneMarker
    {
        [SerializeField] private float _radiusDegrees = 4f;
        [SerializeField] private float _dwellSeconds = 1f;

        /// <summary>Angular radius in degrees.</summary>
        public float RadiusDegrees => _radiusDegrees;
        /// <summary>Required dwell in seconds.</summary>
        public float DwellSeconds => _dwellSeconds;

        /// <summary>True if the POI is within the radius of the view ray's direction.</summary>
        public bool IsInView(Ray view) => Vector3.Angle(view.direction, transform.position - view.origin) <= _radiusDegrees;

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(1f, 1f, 1f, 0.9f);

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(transform.position, 1.5f);
        }
#endif
    }
}
