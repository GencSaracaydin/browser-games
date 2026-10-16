using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Elevated firing spot (Appendix A kind "P"): gunners, marksman, rooftop shooters; tags drive callouts.</summary>
    public class PerchNode : SceneMarker
    {
        [SerializeField] private PerchTag _tag = PerchTag.Rooftop;

        /// <summary>Perch tag.</summary>
        public PerchTag Tag => _tag;

        /// <summary>Sets the tag (builders).</summary>
        public void SetTag(PerchTag tag) => _tag = tag;

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(1f, 0.5f, 0.1f, 0.9f);
#endif
    }
}
