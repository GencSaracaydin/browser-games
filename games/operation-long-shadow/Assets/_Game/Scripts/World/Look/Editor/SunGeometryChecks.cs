using System.Collections.Generic;
using System.Text;
using LongShadow.Core;
using LongShadow.World.Look.Atmosphere;
using LongShadow.World.Look.Stage;
using UnityEngine;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// The WP10 definition-of-done sun-geometry checks (TECH_PLAN §11.4 WP10), run by <see cref="LookCapture"/> against
    /// the open scenes: at s 1.80 R4's floor is in direct sun and the disc is visible from R4 eye height; at s 2.00 the
    /// disc is fully behind the ridge silhouette; at s 0.6 the Sunset Lane band lands on the market floor at z ≈ 236; no
    /// light shaft glows in shade; fog transmittance to the Relay Station (≈ 470 m) per stage (GDD §9.2a acceptance).
    /// Rays use colliders (Default layer) for the town and the backdrop meshes (FarProxy layer or "Ridge" in the name,
    /// which have no colliders) through a CPU ray–triangle test.
    /// </summary>
    public static class SunGeometryChecks
    {
        /// <summary>R4 roof area (Appendix A B5.T.R4: x 277–292, z 312–332, floor ≈ +12).</summary>
        public static readonly Rect R4Area = new Rect(277f, 312f, 15f, 20f);
        /// <summary>R4 eye position at the parapet (B5.R4Parapet (291, 13, 328) + eye).</summary>
        public static readonly Vector3 R4Eye = new Vector3(289f, 13.7f, 328f);
        /// <summary>Sunset Lane floor point (Appendix A B3.SunsetLane (181, 0, 238); GDD §6.5 band at z ≈ 236).</summary>
        public static readonly Vector3 SunsetLanePoint = new Vector3(184f, 0.05f, 236f);
        /// <summary>Relay Station / mast distance from the ridge (GDD §9.2a, ≈ 470 m).</summary>
        public const float StationDistance = 470f;

        /// <summary>Runs every check and appends a report; returns the number of failures.</summary>
        public static int Run(StringBuilder report)
        {
            var fails = 0;
            var backdrop = CollectBackdropTriangles();
            report.AppendLine("Backdrop triangles for disc occlusion: " + backdrop.Count / 3 + ".");

            // 1. s 1.80: R4 floor in direct sun.
            var el180 = LightStageTable.Sample(1.80f).SunElevation;
            var lit = FractionLit(R4Area, 12.05f, LightStageTable.SunDirection(el180), out var tested);
            var r4Ok = tested > 0 && lit >= 0.95f;
            report.AppendLine((r4Ok ? "PASS" : tested == 0 ? "SKIP" : "FAIL") + "  s 1.80 R4 floor in direct sun: " + (lit * 100f).ToString("0") + " % of " + tested + " samples lit (target ≥ 95 %).");
            if (!r4Ok && tested > 0) fails++;

            // 2. s 1.80: disc visible from R4 eye height.
            var vis180 = DiscVisibility(R4Eye, el180, backdrop);
            var discOk = vis180 >= 0.99f;
            report.AppendLine((discOk ? "PASS" : "FAIL") + "  s 1.80 sun disc visible from R4: " + (vis180 * 100f).ToString("0") + " %.");
            if (!discOk) fails++;

            // 3. s 2.00: disc fully hidden by the ridge.
            var el200 = Mathf.Max(0f, LightStageTable.Sample(2.0f).SunElevation);
            var vis200 = backdrop.Count > 0 ? DiscVisibility(R4Eye, el200, backdrop) : 1f;
            var hiddenOk = backdrop.Count > 0 && vis200 <= 0.01f;
            report.AppendLine((hiddenOk ? "PASS" : backdrop.Count == 0 ? "SKIP" : "FAIL") + "  s 2.00 disc behind the ridge: " + (vis200 * 100f).ToString("0") +
                              " % visible (the LightStage also fades the disc 1.97 → 2.00)." + (backdrop.Count == 0 ? " (ridge mesh not loaded)" : ""));
            if (!hiddenOk && backdrop.Count > 0) fails++;

            // 4. s 0.6: Sunset Lane band on the market floor.
            var el06 = LightStageTable.Sample(0.6f).SunElevation;
            var laneLit = 0;
            var laneTested = 0;
            for (var x = 181.5f; x <= 188.5f; x += 1f)
            {
                var p = new Vector3(x, SunsetLanePoint.y, SunsetLanePoint.z);
                if (!HasFloor(p)) continue;
                laneTested++;
                if (!Physics.Raycast(p, LightStageTable.SunDirection(el06), 600f, 1 << LayerIds.Default, QueryTriggerInteraction.Ignore)) laneLit++;
            }
            var laneOk = laneTested > 0 && laneLit > 0;
            report.AppendLine((laneOk ? "PASS" : laneTested == 0 ? "SKIP" : "FAIL") + "  s 0.60 Sunset Lane band at z 236: " + laneLit + "/" + laneTested + " floor samples lit.");
            if (!laneOk && laneTested > 0) fails++;

            // 5. Light shafts in shade at s 0.3 (Golden) and 1.2 (Sunset).
            foreach (var s in new[] { 0.3f, 1.2f })
            {
                var dir = LightStageTable.SunDirection(LightStageTable.Sample(s).SunElevation);
                foreach (var shaft in Object.FindObjectsByType<LightShaft>(FindObjectsInactive.Exclude))
                {
                    var o = shaft.transform.position + dir * 0.05f;
                    if (!Physics.Raycast(o, dir, 400f, 1 << LayerIds.Default, QueryTriggerInteraction.Ignore)) continue;
                    report.AppendLine("WARN  s " + s.ToString("0.0") + " shaft '" + shaft.name + "' at " + shaft.transform.position + " is in shade (it fades out at runtime; move it).");
                }
            }

            // 6. Fog transmittance to the Relay Station from the ridge (GDD §9.2a acceptance test).
            var tuning = UnityEditor.AssetDatabase.LoadAssetAtPath<LightStageTuning>(LookPaths.TuningAsset);
            var scale = tuning != null ? tuning.FogDensityScale : 1f;
            foreach (var s in new[] { 0f, 0.3f, 1.2f, 1.9f, 2.5f, 3.5f })
            {
                var density = LightStageTable.Sample(s).FogDensity * scale;
                var t = Mathf.Exp(-(density * StationDistance) * (density * StationDistance));
                report.AppendLine((s <= 0.15f && t < 0.35f ? "FAIL" : "INFO") + "  s " + s.ToString("0.0") + " fog transmittance at 470 m: " + (t * 100f).ToString("0") + " % (density " + density.ToString("0.00000") + ").");
                if (s <= 0.15f && t < 0.35f) fails++;
            }
            return fails;
        }

        /// <summary>Fraction of grid samples on a horizontal area that see the sun.</summary>
        public static float FractionLit(Rect area, float y, Vector3 toSun, out int tested)
        {
            tested = 0;
            var lit = 0;
            for (var x = area.xMin + 0.5f; x < area.xMax; x += 1.5f)
                for (var z = area.yMin + 0.5f; z < area.yMax; z += 1.5f)
                {
                    var top = new Vector3(x, y + 3f, z);
                    if (!Physics.Raycast(top, Vector3.down, out var hit, 6f, LayerIds.Ground, QueryTriggerInteraction.Ignore)) continue;
                    tested++;
                    if (!Physics.Raycast(hit.point + hit.normal * 0.05f, toSun, 600f, 1 << LayerIds.Default, QueryTriggerInteraction.Ignore)) lit++;
                }
            return tested > 0 ? (float)lit / tested : 0f;
        }

        /// <summary>Fraction of the disc (centre + 8 rim points at the 0.53° diameter) not hidden by the backdrop or town.</summary>
        public static float DiscVisibility(Vector3 eye, float elevation, List<Vector3> backdropTris)
        {
            var center = LightStageTable.SunDirection(elevation);
            var up = Vector3.Cross(Vector3.Cross(center, Vector3.up), center).normalized;
            if (up.sqrMagnitude < 1e-6f) up = Vector3.forward;
            var right = Vector3.Cross(up, center).normalized;
            var r = Mathf.Tan(0.265f * Mathf.Deg2Rad);
            var visible = 0;
            const int samples = 9;
            for (var i = 0; i < samples; i++)
            {
                var d = center;
                if (i > 0)
                {
                    var a = (i - 1) / 8f * Mathf.PI * 2f;
                    d = (center + (up * Mathf.Sin(a) + right * Mathf.Cos(a)) * r).normalized;
                }
                if (Physics.Raycast(eye, d, 2900f, 1 << LayerIds.Default, QueryTriggerInteraction.Ignore)) continue;
                if (RayHitsTriangles(eye, d, 2950f, backdropTris)) continue;
                visible++;
            }
            return visible / (float)samples;
        }

        private static bool HasFloor(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 2f, Vector3.down, 4f, LayerIds.Ground, QueryTriggerInteraction.Ignore);

        /// <summary>World-space triangles of the backdrop meshes (FarProxy layer or "Ridge"/"FarHills" in the name).</summary>
        public static List<Vector3> CollectBackdropTriangles()
        {
            var tris = new List<Vector3>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var go = mf.gameObject;
                var isBackdrop = go.layer == LayerIds.FarProxy || go.name.IndexOf("Ridge", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 go.name.IndexOf("FarHills", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isBackdrop) continue;
                var mesh = mf.sharedMesh;
                if (!mesh.isReadable) continue;
                var v = mesh.vertices;
                var t = mesh.triangles;
                var m = mf.transform.localToWorldMatrix;
                for (var i = 0; i < t.Length; i += 3)
                {
                    tris.Add(m.MultiplyPoint3x4(v[t[i]]));
                    tris.Add(m.MultiplyPoint3x4(v[t[i + 1]]));
                    tris.Add(m.MultiplyPoint3x4(v[t[i + 2]]));
                }
            }
            return tris;
        }

        /// <summary>Möller–Trumbore against a triangle list.</summary>
        public static bool RayHitsTriangles(Vector3 o, Vector3 d, float maxT, List<Vector3> tris)
        {
            for (var i = 0; i + 2 < tris.Count; i += 3)
            {
                var e1 = tris[i + 1] - tris[i];
                var e2 = tris[i + 2] - tris[i];
                var p = Vector3.Cross(d, e2);
                var det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-9f) continue;
                var inv = 1f / det;
                var s = o - tris[i];
                var u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f) continue;
                var q = Vector3.Cross(s, e1);
                var v = Vector3.Dot(d, q) * inv;
                if (v < 0f || u + v > 1f) continue;
                var t = Vector3.Dot(e2, q) * inv;
                if (t > 0f && t < maxT) return true;
            }
            return false;
        }
    }
}
