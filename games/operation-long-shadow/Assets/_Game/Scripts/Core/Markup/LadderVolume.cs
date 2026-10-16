using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Climbable ladder (TECH_PLAN §7.3: 2.2 m/s). The player motor (WP02) enters ladder mode inside the trigger.</summary>
    public class LadderVolume : TriggerVolume
    {
        [SerializeField] private Transform _bottom;
        [SerializeField] private Transform _top;
        [SerializeField] private float _climbSpeed = 2.2f;

        /// <summary>Bottom exit point (falls back to the volume position).</summary>
        public Vector3 Bottom => _bottom != null ? _bottom.position : transform.position;
        /// <summary>Top exit point (falls back to 3 m above the volume).</summary>
        public Vector3 Top => _top != null ? _top.position : transform.position + Vector3.up * 3f;
        /// <summary>Vertical speed in m/s.</summary>
        public float ClimbSpeed => _climbSpeed;

        /// <summary>Configures the ladder (builders).</summary>
        public void Configure(Transform bottom, Transform top, float climbSpeed = 2.2f)
        {
            _bottom = bottom;
            _top = top;
            _climbSpeed = climbSpeed;
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(Bottom, Top);
        }
#endif
    }
}
