using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>A marker placed away from its Appendix A coordinates, with the reason (validator accepts it).</summary>
    public sealed class MarkupOverride
    {
        /// <summary>Marker id.</summary>
        public string Id;
        /// <summary>Placed position.</summary>
        public Vector3 Position;
        /// <summary>Why (logged in Docs/DECISIONS.md).</summary>
        public string Reason;
    }
}
