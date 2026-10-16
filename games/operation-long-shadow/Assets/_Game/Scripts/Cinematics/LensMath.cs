using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Physical lens helpers (CINEMATIC_DIRECTION §1.1): a Super 35 sensor (24.89 × 18.66 mm) and focal lengths from 18
    /// to 85 mm. Cameras keep <c>usePhysicalProperties</c> off and get the equivalent vertical FOV, so every camera in the
    /// project (player, booth, cinematic) speaks vertical FOV; the focal length and f-stop feed the Bokeh DoF override.
    /// </summary>
    public static class LensMath
    {
        /// <summary>Super 35 sensor width in mm.</summary>
        public const float SensorWidthMm = 24.89f;
        /// <summary>Super 35 sensor height in mm.</summary>
        public const float SensorHeightMm = 18.66f;
        /// <summary>Shortest lens used (CINEMATIC_DIRECTION §1.1).</summary>
        public const float MinFocalMm = 10f;
        /// <summary>Longest lens used (CINEMATIC_DIRECTION §1.1 is 85 mm; zooms on the film feed go further).</summary>
        public const float MaxFocalMm = 300f;

        /// <summary>Vertical FOV in degrees of a focal length on the Super 35 sensor height.</summary>
        public static float VerticalFov(float focalMm)
        {
            var f = Mathf.Clamp(focalMm, MinFocalMm, MaxFocalMm);
            return 2f * Mathf.Atan(SensorHeightMm / (2f * f)) * Mathf.Rad2Deg;
        }

        /// <summary>Focal length in mm that gives a vertical FOV on the Super 35 sensor height.</summary>
        public static float FocalFromVerticalFov(float verticalFovDeg)
        {
            var v = Mathf.Clamp(verticalFovDeg, 1f, 170f) * 0.5f * Mathf.Deg2Rad;
            return SensorHeightMm / (2f * Mathf.Tan(v));
        }

        /// <summary>
        /// Interpolates two vertical FOVs as a zoom does (linear in focal length, not in degrees), so a push from 50° to
        /// 30° slows down toward the long end like a real zoom.
        /// </summary>
        public static float ZoomFov(float fromVFov, float toVFov, float t01)
        {
            var a = FocalFromVerticalFov(fromVFov);
            var b = FocalFromVerticalFov(toVFov);
            return VerticalFov(Mathf.Lerp(a, b, Mathf.Clamp01(t01)));
        }
    }
}
