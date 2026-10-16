using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Player.Movement
{
    /// <summary>
    /// Finds a ledge in front of the player for vault, climb and air mantle (GDD §12, TECH_PLAN §7.3 "capsule casts + top
    /// probe"). Allocation-free: single-hit raycasts, one capsule cast and capsule checks.
    /// <para>Steps: (1) downward rays from above the highest allowed top find the first walkable top surface ahead;
    /// (2) a horizontal ray just below the top must hit a steep face, and a second ray low down must hit the same face
    /// (rejects stairs and ramps); (3) the top depth decides "onto" vs "over" (thin walls); (4) the player must be able
    /// to rise straight up, sweep over the top and fit at the end (standing, else crouched).</para>
    /// </summary>
    public static class LedgeProbe
    {
        private const float TopRayHeadroom = 0.3f;
        private const float SampleStep = 0.1f;
        private const QueryTriggerInteraction Ignore = QueryTriggerInteraction.Ignore;

        /// <summary>Cheap pre-check: is there a steep face within <paramref name="distance"/> at a height above the feet?</summary>
        public static bool FaceAhead(Vector3 feet, Vector3 forward, float height, float distance)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return false;
            forward.Normalize();
            return Physics.Raycast(feet + Vector3.up * height, forward, out var hit, distance, PlayerLayers.Walkable, Ignore) &&
                   Mathf.Abs(hit.normal.y) < 0.5f;
        }

        /// <summary>Runs the probe. Returns false when there is no usable ledge.</summary>
        public static bool Probe(in LedgeQuery q, out LedgeInfo info)
        {
            info = default;
            var fwd = q.Forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) return false;
            fwd.Normalize();
            var up = Vector3.up;
            var feet = q.Feet;
            var cosSlope = Mathf.Cos(Mathf.Clamp(q.SlopeLimit, 0f, 89f) * Mathf.Deg2Rad);

            // (1) Top surface: first walkable hit of downward rays sampled ahead of the capsule.
            var rayStart = q.MaxTop + TopRayHeadroom;
            var rayLength = rayStart - q.MinTop + 0.02f;
            var found = false;
            var topHeight = 0f;
            var sampleDistance = 0f;
            for (var i = 0; i < 12; i++)
            {
                var d = q.Radius + 0.03f + i * SampleStep;
                if (d > q.MaxFaceDistance + 0.25f) break;
                var origin = feet + fwd * d + up * rayStart;
                if (!Physics.Raycast(origin, Vector3.down, out var hit, rayLength, PlayerLayers.Walkable, Ignore)) continue;
                var h = hit.point.y - feet.y;
                if (h > q.MaxTop + 0.02f) return false; // something taller is in the way
                if (hit.normal.y < cosSlope) continue;  // steep or sloped top: not a ledge
                topHeight = h;
                sampleDistance = d;
                found = true;
                break;
            }
            if (!found) return false;

            // (2) Face just below the top, and the same face low down (not stairs, not a ramp).
            var faceHeight = Mathf.Max(topHeight - 0.12f, 0.05f);
            var faceOrigin = feet + up * faceHeight;
            if (!Physics.Raycast(faceOrigin, fwd, out var faceHit, sampleDistance + 0.12f, PlayerLayers.Solid, Ignore)) return false;
            if (faceHit.collider.gameObject.layer == LayerIds.PlayerClip) return false;
            if (Mathf.Abs(faceHit.normal.y) > 0.5f) return false;
            var faceDistance = faceHit.distance;
            if (faceDistance > q.MaxFaceDistance + 0.01f) return false;
            var lowHeight = Mathf.Min(Mathf.Max(q.StepOffset + 0.05f, 0.05f), faceHeight);
            if (faceHeight - lowHeight > 0.2f)
            {
                if (Physics.Raycast(feet + up * lowHeight, fwd, out var lowHit, faceDistance + 0.25f, PlayerLayers.Walkable, Ignore))
                {
                    if (Mathf.Abs(lowHit.normal.y) > 0.5f) return false;
                    if (lowHit.distance < faceDistance - 0.2f) return false; // a nearer lower step: stairs
                }
                else if (q.StepOffset > 0f)
                {
                    return false; // on the ground the face must reach down to the feet
                }
            }

            // Edge: on the top, just behind the face.
            var edge = feet + fwd * (faceDistance + 0.05f);
            if (!TopAt(edge, feet.y + topHeight, cosSlope, out var edgeY)) return false;
            edge.y = edgeY;
            topHeight = edgeY - feet.y;
            if (topHeight < q.MinTop - 0.02f || topHeight > q.MaxTop + 0.02f) return false;

            // (3) Depth of the top: onto it, or over a thin wall.
            var thickness = 0f;
            var deep = true;
            for (var k = 1; k <= 5; k++)
            {
                var along = 0.15f * k;
                if (!TopAt(edge + fwd * along, edgeY, cosSlope, out _))
                {
                    thickness = along - 0.075f;
                    deep = false;
                    break;
                }
            }
            if (deep) thickness = 0.75f;

            Vector3 end;
            var over = false;
            if (deep)
            {
                end = edge + fwd * (q.Radius + 0.15f);
                if (TopAt(end, edgeY, cosSlope, out var endY)) end.y = endY;
                else end.y = edgeY;
            }
            else
            {
                if (!q.AllowOverVault || thickness > q.OverVaultMaxThickness) return false;
                over = true;
                end = edge + fwd * (thickness + q.Radius + 0.2f);
                var drop = topHeight + 3f;
                if (Physics.Raycast(new Vector3(end.x, edgeY + 0.3f, end.z), Vector3.down, out var groundHit, drop + 0.3f, PlayerLayers.Walkable, Ignore) &&
                    groundHit.normal.y >= cosSlope)
                    end.y = groundHit.point.y;
                else
                    end.y = edgeY; // nothing below within 3 m: go over and fall
            }

            // (4) Room to rise, sweep over and stand (or crouch) at the end.
            var r = q.Radius * 0.9f;
            var riseBottom = new Vector3(feet.x, edgeY + 0.06f + r, feet.z);
            var riseTop = riseBottom + up * Mathf.Max(0f, q.CrouchHeight - 2f * r);
            if (Physics.CheckCapsule(riseBottom, riseTop, r, PlayerLayers.Solid, Ignore)) return false;

            var sweepFrom = new Vector3(feet.x, 0f, feet.z);
            var sweepTo = new Vector3(end.x, 0f, end.z);
            var sweep = sweepTo - sweepFrom;
            var sweepLength = sweep.magnitude;
            if (sweepLength > 1e-3f &&
                Physics.CapsuleCast(riseBottom, riseTop, r, sweep / sweepLength, out _, sweepLength, PlayerLayers.Solid, Ignore))
                return false;

            var endCrouched = false;
            if (!Fits(end, q.StandHeight, r))
            {
                if (!Fits(end, q.CrouchHeight, r)) return false;
                endCrouched = true;
            }

            info = new LedgeInfo
            {
                Start = feet,
                Edge = edge,
                End = end,
                Forward = fwd,
                TopHeight = topHeight,
                FaceDistance = faceDistance,
                OverVault = over,
                EndCrouched = endCrouched,
            };
            return true;
        }

        /// <summary>True if a capsule of <paramref name="height"/> stands free at <paramref name="feet"/>.</summary>
        public static bool Fits(Vector3 feet, float height, float radius)
        {
            var bottom = feet + Vector3.up * (radius + 0.06f);
            var top = feet + Vector3.up * Mathf.Max(radius + 0.06f, height - radius);
            return !Physics.CheckCapsule(bottom, top, radius, PlayerLayers.Solid, Ignore);
        }

        private static bool TopAt(Vector3 point, float expectedY, float cosSlope, out float y)
        {
            y = expectedY;
            var origin = new Vector3(point.x, expectedY + 0.3f, point.z);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 0.6f, PlayerLayers.Walkable, Ignore)) return false;
            if (hit.normal.y < cosSlope) return false;
            y = hit.point.y;
            return Mathf.Abs(y - expectedY) <= 0.3f;
        }
    }
}
