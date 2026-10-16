using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Beat polylines (TECH_PLAN Appendix A knots; WP10). Progress is monotonic per beat.</summary>
    public interface ICriticalPath
    {
        /// <summary>Normalised arc length 0..1 of a position projected on the beat's polyline (never decreasing).</summary>
        float Progress01(Vector3 position, int beat);
        /// <summary>Point on the beat's polyline at a normalised distance (squad follow).</summary>
        Vector3 PointAt(int beat, float t01);
    }
}
