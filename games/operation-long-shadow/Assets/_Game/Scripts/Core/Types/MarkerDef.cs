using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// A HUD world marker (GDD §11 objective marker). If <see cref="Target"/> is null, <see cref="Offset"/> is the world
    /// position; otherwise the marker follows <c>Target.position + Offset</c>.
    /// </summary>
    public struct MarkerDef
    {
        /// <summary>Marker id (usually the objective id or the marker id it points at).</summary>
        public string Id;
        /// <summary>Followed transform, or null for a fixed world position.</summary>
        public Transform Target;
        /// <summary>Offset from the target (or world position when Target is null).</summary>
        public Vector3 Offset;
        /// <summary>Marker style.</summary>
        public MarkerKind Kind;
        /// <summary>Short label under the diamond.</summary>
        public string Label;
        /// <summary>Show the distance in metres (1 m steps &lt; 100 m, 5 m beyond).</summary>
        public bool ShowDistance;
        /// <summary>Visible through walls (red high-priority markers).</summary>
        public bool ThroughWalls;

        /// <summary>World position of the marker this frame.</summary>
        public Vector3 WorldPosition => Target != null ? Target.position + Offset : Offset;
    }
}
