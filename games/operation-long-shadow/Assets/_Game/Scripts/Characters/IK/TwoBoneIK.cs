using UnityEngine;

namespace LongShadow.Characters.IK
{
    /// <summary>
    /// Analytic two-bone IK (law of cosines) on scene transforms, solved after the Animator in LateUpdate so it can
    /// follow targets that are themselves moved in LateUpdate (WP03 view model, a weapon in the other hand). Keeps the
    /// animated bend plane unless a hint is given, never flips at full extension, and can soften the last few percent of
    /// reach to avoid elbow popping. Allocation-free.
    /// </summary>
    public static class TwoBoneIK
    {
        private const float Epsilon = 1e-5f;

        /// <summary>
        /// Solves root → mid → tip so the tip reaches <paramref name="targetPosition"/> (and takes
        /// <paramref name="targetRotation"/> when <paramref name="rotationWeight"/> &gt; 0).
        /// </summary>
        /// <param name="root">Upper arm / thigh.</param>
        /// <param name="mid">Forearm / calf.</param>
        /// <param name="tip">Hand / foot.</param>
        /// <param name="targetPosition">World target for the tip.</param>
        /// <param name="targetRotation">World rotation for the tip.</param>
        /// <param name="positionWeight">0 keeps the animated pose, 1 reaches the target.</param>
        /// <param name="rotationWeight">Blend of the tip rotation toward the target rotation.</param>
        /// <param name="hint">World point the mid joint should bend toward (elbow / knee).</param>
        /// <param name="hintWeight">0 keeps the animated bend plane.</param>
        /// <param name="softness">Fraction of the chain length over which full extension is approached softly (0 = hard).</param>
        /// <returns>Distance from the solved tip to the requested target (metres) before weighting.</returns>
        public static float Solve(Transform root, Transform mid, Transform tip, Vector3 targetPosition, Quaternion targetRotation,
                                  float positionWeight, float rotationWeight, Vector3 hint, float hintWeight, float softness = 0f)
        {
            if (root == null || mid == null || tip == null) return float.PositiveInfinity;
            positionWeight = Mathf.Clamp01(positionWeight);
            rotationWeight = Mathf.Clamp01(rotationWeight);
            hintWeight = Mathf.Clamp01(hintWeight);
            if (positionWeight <= 0f && rotationWeight <= 0f) return 0f;

            var a = root.position;
            var b = mid.position;
            var c = tip.position;
            var animatedTipRotation = tip.rotation;
            var t = Vector3.Lerp(c, targetPosition, positionWeight);

            var ab = b - a;
            var bc = c - b;
            var abLen = ab.magnitude;
            var bcLen = bc.magnitude;
            if (abLen < Epsilon || bcLen < Epsilon) return float.PositiveInfinity;

            var at = t - a;
            var atLen = at.magnitude;
            var reach = abLen + bcLen;
            // Soft extension: approach full reach asymptotically instead of snapping straight.
            if (softness > 0f)
            {
                var soft = reach * softness;
                var hard = reach - soft;
                if (atLen > hard)
                {
                    var softened = hard + soft * (1f - Mathf.Exp(-(atLen - hard) / soft));
                    if (atLen > Epsilon) at *= softened / atLen;
                    atLen = softened;
                    t = a + at;
                }
            }
            atLen = Mathf.Clamp(atLen, Mathf.Abs(abLen - bcLen) + Epsilon, reach - Epsilon);

            var acLen = (c - a).magnitude;
            var oldAngle = TriangleAngle(acLen, abLen, bcLen);
            var newAngle = TriangleAngle(atLen, abLen, bcLen);

            // Bend axis: keep the animated plane; fall back to the hint, then to the target, then to world up.
            var axis = Vector3.Cross(ab, bc);
            if (axis.sqrMagnitude < Epsilon * Epsilon)
            {
                if (hintWeight > 0f) axis = Vector3.Cross(hint - a, bc);
                if (axis.sqrMagnitude < Epsilon * Epsilon) axis = Vector3.Cross(at, bc);
                if (axis.sqrMagnitude < Epsilon * Epsilon) axis = Vector3.up;
            }
            axis.Normalize();

            // Open/close the mid joint so |a-tip| equals |a-t|.
            mid.rotation = Quaternion.AngleAxis((oldAngle - newAngle) * Mathf.Rad2Deg, axis) * mid.rotation;

            // Swing the root so the tip lies on the a→t line.
            var acNew = tip.position - a;
            if (acNew.sqrMagnitude > Epsilon * Epsilon && at.sqrMagnitude > Epsilon * Epsilon)
                root.rotation = Quaternion.FromToRotation(acNew, at) * root.rotation;

            // Twist around a→t so the mid joint points toward the hint.
            if (hintWeight > 0f)
            {
                var acDir = at.normalized;
                var abNow = mid.position - a;
                var ah = hint - a;
                var abProj = abNow - acDir * Vector3.Dot(abNow, acDir);
                var ahProj = ah - acDir * Vector3.Dot(ah, acDir);
                if (abProj.sqrMagnitude > reach * reach * 1e-4f && ahProj.sqrMagnitude > Epsilon * Epsilon)
                {
                    var twist = Quaternion.FromToRotation(abProj, ahProj);
                    root.rotation = Quaternion.Slerp(Quaternion.identity, twist, hintWeight) * root.rotation;
                }
            }

            tip.rotation = rotationWeight > 0f ? Quaternion.Slerp(animatedTipRotation, targetRotation, rotationWeight) : animatedTipRotation;
            return (tip.position - targetPosition).magnitude;
        }

        /// <summary>
        /// World rotation and position the tip bone must take so that a socket (a child of the tip with the given local
        /// pose) coincides with <paramref name="gripPosition"/>/<paramref name="gripRotation"/>.
        /// </summary>
        public static void TipPoseForSocket(Vector3 gripPosition, Quaternion gripRotation, Vector3 socketLocalPosition,
                                            Quaternion socketLocalRotation, Vector3 tipLossyScale,
                                            out Vector3 tipPosition, out Quaternion tipRotation)
        {
            tipRotation = gripRotation * Quaternion.Inverse(socketLocalRotation);
            tipPosition = gripPosition - tipRotation * Vector3.Scale(socketLocalPosition, tipLossyScale);
        }

        /// <summary>Interior angle (radians) opposite <paramref name="opposite"/> in a triangle with sides a and b.</summary>
        public static float TriangleAngle(float opposite, float a, float b)
        {
            var denom = 2f * a * b;
            if (denom < Epsilon) return 0f;
            var cos = Mathf.Clamp((a * a + b * b - opposite * opposite) / denom, -1f, 1f);
            return Mathf.Acos(cos);
        }
    }
}
