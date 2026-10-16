using LongShadow.Core;
using UnityEngine;
using UnityEngine.AI;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// NavMesh helpers shared by enemies and squad (TECH_PLAN §7.15: agent radius 0.35, height 1.8; area "Doorway" cost 3
    /// where the squad never stops). Every helper degrades when there is no NavMesh (test scenes, sections whose NavMesh
    /// has not been baked yet): the input position is returned unchanged.
    /// </summary>
    public static class NavUtil
    {
        /// <summary>Name of the NavMesh area the squad must not stop in (GDD §15 "No blocking").</summary>
        public const string DoorwayAreaName = "Doorway";

        private static int s_doorwayMask = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_doorwayMask = int.MinValue;

        /// <summary>Area mask bit of "Doorway" (0 when the area does not exist).</summary>
        public static int DoorwayMask
        {
            get
            {
                if (s_doorwayMask == int.MinValue)
                {
                    var area = NavMesh.GetAreaFromName(DoorwayAreaName);
                    s_doorwayMask = area >= 0 ? 1 << area : 0;
                }
                return s_doorwayMask;
            }
        }

        /// <summary>All areas except Doorway.</summary>
        public static int NotDoorwayMask => NavMesh.AllAreas & ~DoorwayMask;

        /// <summary>Closest NavMesh point within <paramref name="maxDistance"/>; false (and the input) when none.</summary>
        public static bool Sample(Vector3 position, float maxDistance, out Vector3 result, int areaMask = NavMesh.AllAreas)
        {
            if (NavMesh.SamplePosition(position, out var hit, maxDistance, areaMask))
            {
                result = hit.position;
                return true;
            }
            result = position;
            return false;
        }

        /// <summary>Closest point that is not in a Doorway area (squad stop points); falls back to any area.</summary>
        public static Vector3 SampleStopPoint(Vector3 position, float maxDistance, bool allowDoorway)
        {
            if (!allowDoorway && DoorwayMask != 0 && Sample(position, maxDistance, out var p, NotDoorwayMask)) return p;
            Sample(position, maxDistance, out var any);
            return any;
        }

        /// <summary>True if the point lies on a Doorway area (within 0.5 m).</summary>
        public static bool IsDoorway(Vector3 position)
        {
            var mask = DoorwayMask;
            if (mask == 0) return false;
            return NavMesh.SamplePosition(position, out var hit, 0.5f, mask) && (hit.position - position).sqrMagnitude < 0.36f;
        }

        /// <summary>True when the agent exists, is enabled and stands on a NavMesh.</summary>
        public static bool Usable(NavMeshAgent agent) =>
            agent != null && agent.enabled && agent.gameObject.activeInHierarchy && agent.isOnNavMesh;

        /// <summary>True if a world point is outside the camera's view (behind it or off the viewport).</summary>
        public static bool IsOffScreen(Camera cam, Vector3 worldPoint, float margin = 0.05f)
        {
            if (cam == null) return true;
            var v = cam.WorldToViewportPoint(worldPoint);
            if (v.z <= 0f) return true;
            return v.x < -margin || v.x > 1f + margin || v.y < -margin || v.y > 1f + margin;
        }

        /// <summary>Ground height under a point (Ground mask), or the point's own y.</summary>
        public static float GroundHeight(Vector3 point, float probe = 30f)
        {
            if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out var hit, probe, LayerIds.Ground, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return point.y;
        }
    }
}
