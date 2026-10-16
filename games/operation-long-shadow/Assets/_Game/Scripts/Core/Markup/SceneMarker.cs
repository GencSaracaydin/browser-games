using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// A named point in a section scene (Appendix A kind "M"), and the base class of every markup component
    /// (TECH_PLAN D13). Data only: it registers its <see cref="Id"/> with <see cref="MarkerRegistry"/> on enable and
    /// unregisters on disable. Systems find markers by id, never by scene reference. Ids follow
    /// "&lt;Beat&gt;.&lt;Kind&gt;.&lt;Name&gt;" (Appendix A). Markers without an id (cover/perch nodes) are still listed by type.
    /// </summary>
    public class SceneMarker : MonoBehaviour
    {
        [SerializeField, Tooltip("Appendix A id (\"B3.NodeSet.M2\"). Empty = anonymous (listed by type only).")]
        private string _id;
        [SerializeField, TextArea(1, 3)] private string _notes;

        /// <summary>Marker id (may be empty for anonymous nodes).</summary>
        public string Id => _id;
        /// <summary>Free-form notes from the markup CSV.</summary>
        public string Notes => _notes;
        /// <summary>World position.</summary>
        public Vector3 Position => transform.position;
        /// <summary>World rotation.</summary>
        public Quaternion Rotation => transform.rotation;
        /// <summary>Compass heading of the forward axis in degrees (Appendix A "h").</summary>
        public float Heading => MathX.BearingOf(transform.forward);

        /// <summary>Registers with the registry.</summary>
        protected virtual void OnEnable() => MarkerRegistry.Instance.Add(this);

        /// <summary>Unregisters from the registry.</summary>
        protected virtual void OnDisable() => MarkerRegistry.Instance.Remove(this);

        /// <summary>Sets the id (builders, MarkupTools). Re-registers when enabled.</summary>
        public void SetId(string id)
        {
            if (_id == id) return;
            var registered = isActiveAndEnabled && Application.isPlaying;
            if (registered) MarkerRegistry.Instance.Remove(this);
            _id = id;
            if (registered) MarkerRegistry.Instance.Add(this);
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Sets the notes (builders).</summary>
        public void SetNotes(string notes) => _notes = notes;

#if UNITY_EDITOR
        /// <summary>Gizmo colour per marker type.</summary>
        protected virtual Color GizmoColor => new Color(1f, 0.85f, 0.2f, 0.9f);

        /// <summary>Draws the marker.</summary>
        protected virtual void OnDrawGizmos()
        {
            MarkupGizmos.Diamond(transform.position, 0.25f, GizmoColor);
            MarkupGizmos.Arrow(transform.position, transform.forward, 0.6f, GizmoColor);
            MarkupGizmos.Label(transform.position + Vector3.up * 0.4f, _id, GizmoColor);
        }
#endif
    }
}
