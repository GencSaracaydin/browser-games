using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Lit volume that extends AI sight (GDD §14.2: x2 up to 70 m inside an active zone): flood cones F1-F4, flares,
    /// headlights. <see cref="OwnerId"/> names the light that owns it (a destroyed flood deactivates its zone). Usually
    /// anonymous (empty id) and sharing the GameObject with the owner's SceneMarker.
    /// </summary>
    public class IlluminationZone : SceneMarker
    {
        [SerializeField] private IlluminationShape _shape = IlluminationShape.Cone;
        [SerializeField] private float _range = 40f;
        [SerializeField, Range(1f, 179f), Tooltip("Full cone angle in degrees.")]
        private float _coneAngle = 60f;
        [SerializeField, Tooltip("Id of the light that owns this zone (\"B6.F1\").")]
        private string _ownerId;
        [SerializeField] private bool _active = true;
        [SerializeField] private float _sightMultiplier = 2f;
        [SerializeField] private float _maxSightRange = 70f;

        private static readonly List<IlluminationZone> s_scratch = new List<IlluminationZone>();

        /// <summary>Shape.</summary>
        public IlluminationShape Shape => _shape;
        /// <summary>Range in metres.</summary>
        public float Range => _range;
        /// <summary>Full cone angle in degrees.</summary>
        public float ConeAngle => _coneAngle;
        /// <summary>Owning light id.</summary>
        public string OwnerId => _ownerId;
        /// <summary>Active (lit).</summary>
        public bool IsActive => _active && isActiveAndEnabled;
        /// <summary>AI sight multiplier inside (2).</summary>
        public float SightMultiplier => _sightMultiplier;
        /// <summary>AI sight cap inside (70 m).</summary>
        public float MaxSightRange => _maxSightRange;

        /// <summary>Turns the zone on/off (flood destroyed, flare burnt out).</summary>
        public void SetActive(bool active) => _active = active;

        /// <summary>Configures the zone (builders, flares).</summary>
        public void Configure(IlluminationShape shape, float range, float coneAngle, string ownerId)
        {
            _shape = shape;
            _range = range;
            _coneAngle = coneAngle;
            _ownerId = ownerId;
        }

        /// <summary>True if the point is lit by this zone.</summary>
        public bool Contains(Vector3 point)
        {
            if (!IsActive) return false;
            if (_shape == IlluminationShape.Sphere) return (point - transform.position).sqrMagnitude <= _range * _range;
            return MathX.InCone(transform.position, transform.forward, _coneAngle * 0.5f, _range, point);
        }

        /// <summary>True if any active zone lights the point; outputs the strongest zone.</summary>
        public static bool IsLit(Vector3 point, out IlluminationZone zone)
        {
            MarkerRegistry.Instance.GetAll(s_scratch);
            zone = null;
            for (var i = 0; i < s_scratch.Count; i++)
            {
                var z = s_scratch[i];
                if (z.Contains(point) && (zone == null || z._sightMultiplier > zone._sightMultiplier)) zone = z;
            }
            s_scratch.Clear();
            return zone != null;
        }

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(1f, 0.95f, 0.5f, 0.6f);

        protected override void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            if (_shape == IlluminationShape.Sphere) Gizmos.DrawWireSphere(transform.position, _range);
            else
            {
                var m = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawFrustum(Vector3.zero, _coneAngle, _range, 0.1f, 1f);
                Gizmos.matrix = m;
            }
            MarkupGizmos.Label(transform.position, string.IsNullOrEmpty(Id) ? "I:" + _ownerId : Id, GizmoColor);
        }
#endif
    }
}
