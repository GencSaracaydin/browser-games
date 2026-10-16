using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Squad
{
    /// <summary>
    /// Follow-mode targets (GDD §15): file along the critical path, Hollis 6-10 m ahead of the player and never more
    /// than 12 m ahead, Sparrow 3-6 m ahead and Tinker 3-6 m behind (the Beat 1 file order: Hollis, Sparrow, player,
    /// Tinker). With an <see cref="ICriticalPath"/> the slots are arc-length offsets from the player's projection on the
    /// beat polyline (so they wait at corners and doors); without one they hang off the player's view direction.
    /// Pure apart from the path service calls; NavMesh snapping is the caller's.
    /// </summary>
    public static class SquadFollowPlanner
    {
        /// <summary>Hollis's lead (m), the middle of 6-10.</summary>
        public const float HollisLead = 8f;
        /// <summary>Hollis's lead band.</summary>
        public const float HollisLeadMin = 6f;
        /// <summary>Upper end of Hollis's band.</summary>
        public const float HollisLeadMax = 10f;
        /// <summary>Never more than this far ahead of the player (GDD §15).</summary>
        public const float MaxAhead = 12f;
        /// <summary>Sparrow's lead (m) and Tinker's trail (m), inside 3-6.</summary>
        public const float OtherOffset = 4f;
        /// <summary>Lateral offsets so the three never stack on one line (m): Hollis, Sparrow, Tinker.</summary>
        public static readonly float[] Lateral = { 0f, 1.1f, -0.9f };
        /// <summary>Along-path offsets (m): Hollis, Sparrow, Tinker.</summary>
        public static readonly float[] Along = { HollisLead, OtherOffset, -OtherOffset };

        /// <summary>Index of a member in <see cref="SquadIds.All"/> order (0 Hollis, 1 Sparrow, 2 Tinker; -1 unknown).</summary>
        public static int IndexOf(string memberId)
        {
            for (var i = 0; i < SquadIds.All.Length; i++)
                if (SquadIds.All[i] == memberId) return i;
            return -1;
        }

        /// <summary>
        /// Follow slot without a path: <paramref name="along"/> metres along the view direction and
        /// <paramref name="lateral"/> to its right.
        /// </summary>
        public static Vector3 SlotFromView(Vector3 player, Vector3 viewForward, float along, float lateral)
        {
            viewForward.y = 0f;
            if (viewForward.sqrMagnitude < 1e-6f) viewForward = Vector3.forward;
            viewForward.Normalize();
            var right = new Vector3(viewForward.z, 0f, -viewForward.x);
            return player + viewForward * along + right * lateral;
        }

        /// <summary>
        /// Follow slot on a beat path: the point <paramref name="along"/> metres from the player's progress (clamped to
        /// the path), pulled back toward the player until it is at most <see cref="MaxAhead"/> away, then offset
        /// <paramref name="lateral"/> metres to the right of the path direction.
        /// </summary>
        public static Vector3 SlotOnPath(ICriticalPath path, int beat, float pathLength, Vector3 player, float along, float lateral)
        {
            if (path == null || pathLength <= 0.01f) return player;
            var t = path.Progress01(player, beat);
            var s = Mathf.Clamp(t * pathLength + along, 0f, pathLength);
            var p = path.PointAt(beat, s / pathLength);
            for (var i = 0; i < 12 && along > 0f && Planar(p - player).magnitude > MaxAhead && s > t * pathLength; i++)
            {
                s = Mathf.Max(t * pathLength, s - 1f);
                p = path.PointAt(beat, s / pathLength);
            }
            if (Mathf.Abs(lateral) > 1e-3f)
            {
                var ahead = path.PointAt(beat, Mathf.Min(1f, (s + 1f) / pathLength));
                var behind = path.PointAt(beat, Mathf.Max(0f, (s - 1f) / pathLength));
                var dir = Planar(ahead - behind);
                if (dir.sqrMagnitude > 1e-4f)
                {
                    dir.Normalize();
                    p += new Vector3(dir.z, 0f, -dir.x) * lateral;
                }
            }
            return p;
        }

        /// <summary>Approximate length of a beat path (sum of 32 chords).</summary>
        public static float PathLength(ICriticalPath path, int beat, int samples = 32)
        {
            if (path == null) return 0f;
            var length = 0f;
            var prev = path.PointAt(beat, 0f);
            for (var i = 1; i <= samples; i++)
            {
                var p = path.PointAt(beat, i / (float)samples);
                length += (p - prev).magnitude;
                prev = p;
            }
            return length;
        }

        /// <summary>
        /// True when a follower should be re-pathed: Hollis outside his 6-10 m band ahead of the player (or beyond
        /// 12 m), anyone else more than 2.5 m from its slot.
        /// </summary>
        public static bool NeedsRepath(int memberIndex, Vector3 member, Vector3 slot, Vector3 player)
        {
            var toSlot = Planar(slot - member).magnitude;
            if (memberIndex != 0) return toSlot > 2.5f;
            var ahead = Planar(member - player).magnitude;
            return ahead > MaxAhead || ahead < HollisLeadMin - 1f || ahead > HollisLeadMax + 1f || toSlot > 4f;
        }

        private static Vector3 Planar(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
