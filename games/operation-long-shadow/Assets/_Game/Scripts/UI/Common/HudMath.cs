using LongShadow.Core;
using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Pure HUD math (no Unity objects except an optional camera): marker distance rounding, compass projection, edge
    /// clamping, damage-ring angles, letterbox heights and the CanvasScaler formula. Allocation-free; covered by EditMode
    /// tests (HudMathTests).
    /// </summary>
    public static class HudMath
    {
        /// <summary>Reference resolution of the HUD canvas (GDD §11).</summary>
        public static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        /// <summary>CanvasScaler match (GDD §11: Scale With Screen Size, match 0.5).</summary>
        public const float Match = 0.5f;

        /// <summary>
        /// Marker distance shown to the player: 1 m steps below 100 m, 5 m steps from 100 m (GDD §11).
        /// </summary>
        public static int RoundDistance(float metres)
        {
            if (float.IsNaN(metres) || metres <= 0f) return 0;
            var r = Mathf.RoundToInt(metres);
            if (r < 100) return r;
            return Mathf.RoundToInt(metres / 5f) * 5;
        }

        /// <summary>
        /// Horizontal offset on the compass strip of a bearing, relative to the heading at the strip's centre.
        /// Returns false when the bearing lies outside the visible half-field (<paramref name="x"/> is then clamped to
        /// the strip edge).
        /// </summary>
        public static bool CompassOffset(float bearing, float heading, float halfFieldDeg, float halfWidth, out float x)
        {
            var d = MathX.WrapAngle180(bearing - heading);
            var inside = Mathf.Abs(d) <= halfFieldDeg;
            x = Mathf.Clamp(d / halfFieldDeg, -1f, 1f) * halfWidth;
            return inside;
        }

        /// <summary>
        /// Z rotation (degrees, counter-clockwise positive as in RectTransform) that points a "up" sprite on the screen
        /// ring towards a world source seen from <paramref name="viewForward"/> (damage arcs, grenade arrows).
        /// A source straight ahead gives 0, to the right gives -90, behind gives 180.
        /// </summary>
        public static float RingAngle(Vector3 viewForward, Vector3 toSource) =>
            -MathX.WrapAngle180(MathX.BearingOf(toSource) - MathX.BearingOf(viewForward));

        /// <summary>
        /// Projects a world point to canvas units (origin bottom-left). <paramref name="behind"/> is true when the point is
        /// behind the camera; its position is then mirrored so edge clamping points the right way.
        /// </summary>
        public static Vector2 WorldToCanvas(Camera cam, Vector3 world, float scaleFactor, out bool behind)
        {
            var sp = cam.WorldToScreenPoint(world);
            behind = sp.z < 0f;
            var x = sp.x;
            var y = sp.y;
            if (behind)
            {
                // Mirror through the view centre and push far out so the clamp lands on the edge.
                var r = cam.pixelRect;
                var cx = r.x + r.width * 0.5f;
                var cy = r.y + r.height * 0.5f;
                var dx = cx - x;
                var dy = cy - y;
                if (Mathf.Abs(dx) < 1f && Mathf.Abs(dy) < 1f) dy = -1f;
                var k = 10000f / Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
                x = cx + dx * k;
                y = cy + dy * k;
            }
            var inv = scaleFactor > 0f ? 1f / scaleFactor : 1f;
            return new Vector2(x * inv, y * inv);
        }

        /// <summary>
        /// Clamps a canvas point into the rectangle centred on <paramref name="center"/> with <paramref name="halfExtents"/>
        /// along the ray from the centre. Returns true when it was clamped; <paramref name="angleDeg"/> is the direction of
        /// the point from the centre (0 = +X, counter-clockwise), for the edge arrow.
        /// </summary>
        public static bool ClampToRect(ref Vector2 point, Vector2 center, Vector2 halfExtents, out float angleDeg)
        {
            var d = point - center;
            angleDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var ax = Mathf.Abs(d.x);
            var ay = Mathf.Abs(d.y);
            if (ax <= halfExtents.x && ay <= halfExtents.y) return false;
            var sx = ax > 1e-4f ? halfExtents.x / ax : float.MaxValue;
            var sy = ay > 1e-4f ? halfExtents.y / ay : float.MaxValue;
            var s = Mathf.Min(sx, sy);
            point = center + d * s;
            return true;
        }

        /// <summary>Height of each letterbox bar for a target aspect (e.g. 2.39) on a canvas of the given size.</summary>
        public static float LetterboxBarHeight(float width, float height, float aspect)
        {
            if (aspect <= 0f || width <= 0f) return 0f;
            return Mathf.Max(0f, (height - width / aspect) * 0.5f);
        }

        /// <summary>CanvasScaler "Scale With Screen Size" factor for a screen (logarithmic match, Unity's formula).</summary>
        public static float CanvasScale(Vector2 screen, Vector2 reference, float match)
        {
            if (screen.x <= 0f || screen.y <= 0f) return 1f;
            var lw = Mathf.Log(screen.x / reference.x, 2f);
            var lh = Mathf.Log(screen.y / reference.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(lw, lh, match));
        }

        /// <summary>Canvas size in reference units for a screen size.</summary>
        public static Vector2 CanvasSize(Vector2 screen) => screen / CanvasScale(screen, Reference, Match);

        /// <summary>
        /// Opacity of a marker: 30 % while aiming down sights (GDD §11), dimmer when occluded and not through-walls.
        /// </summary>
        public static float MarkerAlpha(float adsBlend, bool occluded, bool throughWalls)
        {
            var a = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(adsBlend));
            if (occluded && !throughWalls) a *= 0.45f;
            return a;
        }

        /// <summary>
        /// Alpha of the screen-edge damage vignette for a health ratio: 0 at or above 50 % HP, 1 at 0 HP (GDD §11).
        /// </summary>
        public static float VignetteAlpha(float health01) => Mathf.Clamp01(1f - Mathf.Clamp01(health01) / 0.5f);

        /// <summary>Crosshair arm offset in reference pixels for a spread 0..1.</summary>
        public static float CrosshairGap(float spread01, float minGap, float maxGap) =>
            Mathf.Lerp(minGap, maxGap, Mathf.Clamp01(spread01));
    }
}
