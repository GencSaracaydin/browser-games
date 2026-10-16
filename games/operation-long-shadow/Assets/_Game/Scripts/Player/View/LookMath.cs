using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Player.View
{
    /// <summary>
    /// Mouse-look math (GDD §11 Options, TECH_PLAN §7.3 PlayerLook): rotation in degrees = CSS px × sens × 0.03
    /// (3.0 gives 9° per 100 CSS px); ADS multiplies by the ADS sensitivity and by the FOV ratio
    /// tan(fov/2) / tan(hipFov/2) so the on-screen speed stays matched while zoomed; pitch clamps at ±85°.
    /// </summary>
    public static class LookMath
    {
        /// <summary>Maximum pitch up/down in degrees (TECH_PLAN §7.3).</summary>
        public const float MaxPitch = 85f;

        /// <summary>Degrees per CSS pixel for a sensitivity (GDD §11).</summary>
        public static float DegreesPerPixel(float sensitivity) => sensitivity * GameSettings.DegreesPerPixelPerSens;

        /// <summary>FOV ratio tan(v/2) / tan(hip/2) (1 when not zoomed).</summary>
        public static float FovRatio(float currentVerticalFov, float hipVerticalFov)
        {
            if (currentVerticalFov <= 0f || hipVerticalFov <= 0f) return 1f;
            var a = Mathf.Tan(Mathf.Clamp(currentVerticalFov, 1f, 179f) * 0.5f * Mathf.Deg2Rad);
            var b = Mathf.Tan(Mathf.Clamp(hipVerticalFov, 1f, 179f) * 0.5f * Mathf.Deg2Rad);
            return b > 1e-5f ? Mathf.Clamp(a / b, 0.01f, 4f) : 1f;
        }

        /// <summary>
        /// Sensitivity multiplier: 1 at the hip; × ADS sensitivity × FOV ratio while aiming (ADS or binoculars).
        /// Zoom without aiming (scripted FOV requests) still applies the FOV ratio.
        /// </summary>
        public static float Multiplier(bool aiming, float adsSensitivity, float currentVerticalFov, float hipVerticalFov)
        {
            var ratio = FovRatio(currentVerticalFov, hipVerticalFov);
            return aiming ? adsSensitivity * ratio : Mathf.Min(1f, ratio);
        }

        /// <summary>
        /// Look delta in degrees (x = yaw right, y = pitch up) for a mouse delta in CSS px (+y = mouse moved up).
        /// </summary>
        public static Vector2 LookDegrees(Vector2 cssPixels, float sensitivity, bool invertY, float multiplier)
        {
            var k = DegreesPerPixel(sensitivity) * multiplier;
            return new Vector2(cssPixels.x * k, cssPixels.y * k * (invertY ? -1f : 1f));
        }

        /// <summary>Clamps a pitch to ±<see cref="MaxPitch"/>.</summary>
        public static float ClampPitch(float pitch) => Mathf.Clamp(pitch, -MaxPitch, MaxPitch);

        /// <summary>
        /// Clamps yaw/pitch into a window around a centre (door gun, cinematics): yaw within ±<paramref name="yawLimit"/>
        /// of <paramref name="centerYaw"/>, pitch within [centre − down, centre + up]. Angles in degrees.
        /// </summary>
        public static void ClampToLimits(ref float yaw, ref float pitch, float centerYaw, float centerPitch, float yawLimit, float up, float down)
        {
            var rel = MathX.WrapAngle180(yaw - centerYaw);
            if (yawLimit < 180f) rel = Mathf.Clamp(rel, -yawLimit, yawLimit);
            yaw = centerYaw + rel;
            pitch = Mathf.Clamp(pitch, centerPitch - Mathf.Abs(down), centerPitch + Mathf.Abs(up));
        }

        /// <summary>Pitch in degrees (positive up) of a direction.</summary>
        public static float PitchOf(Vector3 direction)
        {
            var d = direction.normalized;
            return Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
        }
    }
}
