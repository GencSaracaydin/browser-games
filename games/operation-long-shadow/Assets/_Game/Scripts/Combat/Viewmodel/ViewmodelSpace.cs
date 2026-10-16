using UnityEngine;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>
    /// Converts points drawn by the view-model camera (own FOV, TECH_PLAN D5) into the world-camera point that appears at
    /// the same pixel and depth, so tracers, muzzle lights, casings and dropped magazines start where the player sees the
    /// gun's muzzle, ejection port or magazine (both cameras share one transform).
    /// </summary>
    public static class ViewmodelSpace
    {
        /// <summary>World-camera position that looks like <paramref name="viewmodelPoint"/> through the view-model camera.</summary>
        public static Vector3 ToWorld(Vector3 viewmodelPoint, Camera viewmodelCamera, Camera worldCamera)
        {
            if (viewmodelCamera == null || worldCamera == null || viewmodelCamera == worldCamera) return viewmodelPoint;
            var vp = viewmodelCamera.WorldToViewportPoint(viewmodelPoint);
            if (vp.z <= 0f) return viewmodelPoint;
            return worldCamera.ViewportToWorldPoint(vp);
        }

        /// <summary>
        /// Same conversion with explicit vertical FOVs (no camera objects; tests and math): scales the point's offset from
        /// the view axis by tan(world/2) / tan(viewmodel/2) at constant depth.
        /// </summary>
        public static Vector3 ToWorldLocal(Vector3 localPoint, float viewmodelFov, float worldFov)
        {
            var k = Mathf.Tan(worldFov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1e-4f, Mathf.Tan(viewmodelFov * 0.5f * Mathf.Deg2Rad));
            return new Vector3(localPoint.x * k, localPoint.y * k, localPoint.z);
        }
    }
}
