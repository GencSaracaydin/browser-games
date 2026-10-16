using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Out-of-sight spawn point for reinforcements (Appendix A kind "C", "SC_B3_BAL"). The spawner checks line of sight to
    /// <see cref="Door"/> and keeps at least <see cref="MinPlayerDistance"/> (15 m) from the player (TECH_PLAN §7.6).
    /// </summary>
    public class SpawnCloset : SceneMarker
    {
        [SerializeField, Tooltip("Door/opening the spawns walk out of (LOS test target).")]
        private Transform _door;
        [SerializeField, Tooltip("Spawn points inside the closet (empty = this transform).")]
        private Transform[] _spawnPoints = new Transform[0];
        [SerializeField] private float _minPlayerDistance = 15f;

        /// <summary>Door transform (falls back to this transform).</summary>
        public Transform Door => _door != null ? _door : transform;
        /// <summary>Number of spawn points (at least 1).</summary>
        public int SpawnPointCount => _spawnPoints != null && _spawnPoints.Length > 0 ? _spawnPoints.Length : 1;
        /// <summary>Minimum player distance in metres.</summary>
        public float MinPlayerDistance => _minPlayerDistance;

        /// <summary>Spawn point by index (wraps; falls back to this transform).</summary>
        public Transform SpawnPoint(int index)
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0) return transform;
            var t = _spawnPoints[((index % _spawnPoints.Length) + _spawnPoints.Length) % _spawnPoints.Length];
            return t != null ? t : transform;
        }

        /// <summary>Configures the closet (builders).</summary>
        public void Configure(Transform door, Transform[] spawnPoints, float minPlayerDistance = 15f)
        {
            _door = door;
            _spawnPoints = spawnPoints ?? new Transform[0];
            _minPlayerDistance = minPlayerDistance;
        }

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(0.9f, 0.2f, 0.2f, 0.9f);

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
            if (_door != null) Gizmos.DrawLine(transform.position + Vector3.up, _door.position);
        }
#endif
    }
}
