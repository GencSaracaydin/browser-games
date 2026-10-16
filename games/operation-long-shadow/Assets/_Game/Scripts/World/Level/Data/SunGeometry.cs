using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Sun geometry used to place what the GDD's light depends on (GDD §6.1 azimuth 258°, §6.5 Sunset Lane, §7.3 street
    /// shade line, §8.5 MID-A ridge silhouette, TECH_PLAN Review log 9 and 11). Pure maths, tested in EditMode.
    /// </summary>
    public static class SunGeometry
    {
        /// <summary>Unit vector pointing from the ground towards the sun (not the light direction).</summary>
        public static Vector3 ToSun(float elevationDegrees, float azimuthDegrees = WorldConst.SunAzimuth)
        {
            var el = elevationDegrees * Mathf.Deg2Rad;
            var az = azimuthDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        }

        /// <summary>Rotation of a directional light shining from the sun (TECH_PLAN §3.8: Euler (elevation, 78, 0)).</summary>
        public static Quaternion LightRotation(float elevationDegrees) =>
            Quaternion.Euler(elevationDegrees, WorldConst.SunLightYaw, 0f);

        /// <summary>Horizontal shadow length of an edge <paramref name="height"/> above the receiving plane.</summary>
        public static float ShadowLength(float height, float elevationDegrees)
        {
            var t = Mathf.Tan(Mathf.Max(0.01f, elevationDegrees) * Mathf.Deg2Rad);
            return height / t;
        }

        /// <summary>
        /// Height of the sun/shade line on a wall that faces the sun across a street: the far wall is lit above
        /// <c>H - d·tan(el)</c> where H is the height of the near (sun-side) roof edge and d the horizontal distance
        /// between the facades measured along the sun's azimuth (GDD §7.3: "upper storeys lit above ≈ 6 m").
        /// </summary>
        public static float ShadeLineOnFarWall(float nearEdgeHeight, float facadeGap, float elevationDegrees,
                                               float streetAxisBearing = 0f)
        {
            // Distance along the sun's horizontal direction between two facades parallel to the street axis.
            var angle = Mathf.DeltaAngle(WorldConst.SunAzimuth, streetAxisBearing + 90f) * Mathf.Deg2Rad;
            var cos = Mathf.Abs(Mathf.Cos(angle));
            var along = facadeGap / Mathf.Max(0.05f, cos);
            return nearEdgeHeight - along * Mathf.Tan(elevationDegrees * Mathf.Deg2Rad);
        }

        /// <summary>World height of a silhouette crest that sits <paramref name="degreesAboveEye"/> above an eye.</summary>
        public static float CrestHeight(float eyeY, float distance, float degreesAboveEye) =>
            eyeY + Mathf.Tan(degreesAboveEye * Mathf.Deg2Rad) * distance;

        /// <summary>
        /// The ridge-silhouette crest height for the MID-A image: R4 floor +12, eye +1.65, crest 0.25° above the eye line
        /// at 2.5 km (GDD §8.5, §9.3: about 25 m).
        /// </summary>
        public static float MidARidgeCrest() =>
            CrestHeight(12f + WorldConst.EyeHeight, WorldConst.RidgeSilhouetteDistance, WorldConst.RidgeCrestAboveEyeDegrees);

        /// <summary>Point on the ground at <paramref name="distance"/> from <paramref name="from"/> along a compass bearing.</summary>
        public static Vector3 AlongBearing(Vector3 from, float bearingDegrees, float distance)
        {
            var b = bearingDegrees * Mathf.Deg2Rad;
            return from + new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b)) * distance;
        }

        /// <summary>
        /// The look target 500 m from R4 towards the sun at 1.5° (Appendix A <c>B5.SunLook</c>), from the R4 parapet marker.
        /// </summary>
        public static Vector3 MidASunLook(Vector3 r4Parapet)
        {
            var dir = ToSun(1.5f);
            return r4Parapet + dir * 500f;
        }

        /// <summary>
        /// Where a ray from the sun that passes the point <paramref name="through"/> hits the plane y = <paramref name="planeY"/>.
        /// Used to check that the Sunset Lane band lands on the market floor (GDD §6.5 A5, TECH_PLAN WP10 DoD).
        /// </summary>
        public static Vector3 ProjectAlongSunRay(Vector3 through, float elevationDegrees, float planeY)
        {
            var toSun = ToSun(elevationDegrees);
            var down = -toSun;
            if (down.y >= -1e-5f) return through;
            var t = (planeY - through.y) / down.y;
            return through + down * t;
        }

        /// <summary>
        /// The z coordinate where the centre of the Sunset Lane band crosses a north-south line at <paramref name="x"/>
        /// (the lane axis runs at azimuth 258° through (181, 0, 238); GDD §6.5 A5).
        /// </summary>
        public static float SunsetLaneBandZ(float x)
        {
            // Lane axis direction (towards the sun): bearing 258 → (sin, cos) = (-0.978, -0.208).
            var b = WorldConst.SunAzimuth * Mathf.Deg2Rad;
            var dx = Mathf.Sin(b);
            var dz = Mathf.Cos(b);
            var t = (x - LaneMouth.x) / dx;
            return LaneMouth.z + dz * t;
        }

        /// <summary>East end of the Sunset Lane axis at the market's west facade line (GDD §6.5 A5).</summary>
        public static readonly Vector3 LaneMouth = new Vector3(181f, 0f, 238f);
        /// <summary>West end of the lane at the town edge (GDD §6.5 A5).</summary>
        public static readonly Vector3 LaneEnd = new Vector3(120f, 0f, 225f);
        /// <summary>Lane width in metres (GDD §6.5 A5).</summary>
        public const float LaneWidth = 5f;
    }
}
