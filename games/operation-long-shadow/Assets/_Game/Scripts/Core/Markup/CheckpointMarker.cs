using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Checkpoint spawn (Appendix A kind "CP", id = checkpoint id "CP3b"). The player is teleported here on reload
    /// (position + yaw); the squad goes to <see cref="SquadNodeSetId"/> if set.
    /// </summary>
    public class CheckpointMarker : SceneMarker
    {
        [SerializeField, Tooltip("Node set the squad teleports to on reload (empty = squad decides).")]
        private string _squadNodeSetId;

        /// <summary>Checkpoint id (same as <see cref="SceneMarker.Id"/>).</summary>
        public string CheckpointId => Id;
        /// <summary>Squad node set id for reloads.</summary>
        public string SquadNodeSetId => _squadNodeSetId;
        /// <summary>Player yaw in degrees.</summary>
        public float PlayerYaw => transform.eulerAngles.y;

        /// <summary>Sets the squad node set (builders).</summary>
        public void SetSquadNodeSet(string id) => _squadNodeSetId = id;

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(0.2f, 1f, 1f, 0.9f);

        protected override void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
            MarkupGizmos.Arrow(transform.position + Vector3.up * 1.6f, transform.forward, 1f, GizmoColor);
            MarkupGizmos.Label(transform.position + Vector3.up * 2f, Id, GizmoColor);
        }
#endif
    }
}
