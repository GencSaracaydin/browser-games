using UnityEngine;

namespace LongShadow.Characters.IK
{
    /// <summary>
    /// Iterative aim solver: rotates a bone chain (spine → upper chest, or neck → head) so that an aim axis of an
    /// end transform (the weapon muzzle, or the head) points at a target. Unlike a bone-axis multi-aim, it aligns the
    /// <em>weapon</em> with the target, so it keeps working whatever torso twist the rifle clip has (bladed stance).
    /// Each bone takes its weight of the remaining error per iteration; give the last bone weight 1 to converge exactly.
    /// Allocation-free.
    /// </summary>
    public static class AimChain
    {
        /// <summary>
        /// Rotates <paramref name="bones"/> (root first) so <paramref name="aimTransform"/>'s local <paramref name="aimAxis"/>
        /// points from <paramref name="aimTransform"/> to <paramref name="target"/>.
        /// </summary>
        /// <param name="bones">Chain bones, parent first.</param>
        /// <param name="weights">Per-bone fraction of the remaining correction (0..1), same length as bones.</param>
        /// <param name="count">Number of bones to use.</param>
        /// <param name="aimTransform">Transform whose axis must point at the target (muzzle, head).</param>
        /// <param name="aimAxis">Local axis of <paramref name="aimTransform"/> (usually Vector3.forward).</param>
        /// <param name="target">World aim point.</param>
        /// <param name="weight">Overall blend 0..1.</param>
        /// <param name="iterations">Passes (2 removes the muzzle-offset parallax for targets beyond 2 m).</param>
        /// <param name="maxAngle">Clamp of the total correction (degrees) so the spine never folds; the body must turn beyond it.</param>
        /// <param name="minDistance">Targets closer than this to the first bone are pushed out along their direction.</param>
        /// <returns>Remaining angular error in degrees after solving.</returns>
        public static float Solve(Transform[] bones, float[] weights, int count, Transform aimTransform, Vector3 aimAxis, Vector3 target,
                                  float weight, int iterations = 2, float maxAngle = 75f, float minDistance = 1.2f)
        {
            if (bones == null || weights == null || aimTransform == null || count <= 0) return 180f;
            weight = Mathf.Clamp01(weight);
            if (weight <= 0f) return ErrorDegrees(aimTransform, aimAxis, target);
            count = Mathf.Min(count, Mathf.Min(bones.Length, weights.Length));

            // Keep the target out of the chest: very close targets make the muzzle-to-target direction unstable.
            var pivot = bones[0] != null ? bones[0].position : aimTransform.position;
            var toTarget = target - pivot;
            var dist = toTarget.magnitude;
            if (dist < minDistance)
                target = pivot + (dist > 1e-4f ? toTarget / dist : aimTransform.rotation * aimAxis) * minDistance;

            // Effective target: the requested direction, blended by weight and clamped to maxAngle from the animated aim.
            var startAxis = (aimTransform.rotation * aimAxis).normalized;
            var startDir = target - aimTransform.position;
            var startDist = startDir.magnitude;
            if (startDist < 1e-4f) return 0f;
            var totalAngle = Vector3.Angle(startAxis, startDir);
            var allowed = Mathf.Min(totalAngle * weight, maxAngle);
            var effectiveTarget = target;
            if (allowed < totalAngle - 1e-3f)
            {
                var dir = Vector3.RotateTowards(startAxis, startDir / startDist, allowed * Mathf.Deg2Rad, 0f);
                effectiveTarget = aimTransform.position + dir * startDist;
            }

            for (var it = 0; it < iterations; it++)
            {
                for (var i = 0; i < count; i++)
                {
                    var bone = bones[i];
                    var f = Mathf.Clamp01(weights[i]);
                    if (bone == null || f <= 0f) continue;
                    var axisWorld = aimTransform.rotation * aimAxis;
                    var desired = effectiveTarget - aimTransform.position;
                    if (desired.sqrMagnitude < 1e-8f) break;
                    // Fraction of the remaining error for this bone.
                    var delta = Quaternion.FromToRotation(axisWorld, desired);
                    bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, f) * bone.rotation;
                }
            }
            return ErrorDegrees(aimTransform, aimAxis, target);
        }

        /// <summary>Angle in degrees between the aim axis and the direction to the target.</summary>
        public static float ErrorDegrees(Transform aimTransform, Vector3 aimAxis, Vector3 target)
        {
            if (aimTransform == null) return 180f;
            var dir = target - aimTransform.position;
            if (dir.sqrMagnitude < 1e-8f) return 0f;
            return Vector3.Angle(aimTransform.rotation * aimAxis, dir);
        }

        /// <summary>
        /// The bone-local axis (±X, ±Y, ±Z) that best matches a world direction in the bone's current pose. Builders use
        /// it to find the "forward" of a head bone in the bind pose whatever the source rig's bone axes are.
        /// </summary>
        public static Vector3 ClosestLocalAxis(Transform bone, Vector3 worldDirection)
        {
            if (bone == null || worldDirection.sqrMagnitude < 1e-8f) return Vector3.forward;
            var local = Quaternion.Inverse(bone.rotation) * worldDirection.normalized;
            var ax = Mathf.Abs(local.x);
            var ay = Mathf.Abs(local.y);
            var az = Mathf.Abs(local.z);
            if (ax >= ay && ax >= az) return new Vector3(Mathf.Sign(local.x), 0f, 0f);
            if (ay >= az) return new Vector3(0f, Mathf.Sign(local.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(local.z));
        }
    }
}
