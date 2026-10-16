using UnityEngine;

namespace LongShadow.Characters.IK
{
    /// <summary>
    /// Anatomical frame of a hand computed from its finger roots, independent of the source rig's bone axes (Rocketbox
    /// Biped, Meshy, the UAL mannequin and the placeholder mannequin all differ). Used to place the weapon sockets so a
    /// weapon's <c>RightGrip</c>/<c>LeftGrip</c> (forward = barrel, up = top of the weapon) lands inside the fist.
    /// <list type="bullet">
    /// <item><see cref="Fingers"/>: wrist → middle-finger knuckle.</item>
    /// <item><see cref="Across"/>: index knuckle → little knuckle (orthogonal to Fingers).</item>
    /// <item><see cref="Palm"/>: palm normal (back of the hand → palm).</item>
    /// </list>
    /// </summary>
    public struct HandBasis
    {
        /// <summary>Wrist → middle knuckle (unit).</summary>
        public Vector3 Fingers;
        /// <summary>Index knuckle → little knuckle (unit, orthogonal to <see cref="Fingers"/>).</summary>
        public Vector3 Across;
        /// <summary>Palm normal (unit).</summary>
        public Vector3 Palm;
        /// <summary>Wrist → middle knuckle distance (metres); about 0.09 m on an adult.</summary>
        public float Length;
        /// <summary>Wrist position.</summary>
        public Vector3 Wrist;

        /// <summary>Typical adult wrist-to-knuckle length used to scale offsets.</summary>
        public const float ReferenceLength = 0.09f;

        /// <summary>Scale relative to an adult hand (1 = 9 cm metacarpals).</summary>
        public float Scale => Length > 1e-4f ? Length / ReferenceLength : 1f;

        /// <summary>
        /// Computes the frame. <paramref name="thumb"/> decides the palm side (the thumb root sits on the palm side of the
        /// knuckle plane); when it is missing or inconclusive, <paramref name="palmHint"/> is used (palms face down in a
        /// Unity T-pose, so pass -root.up).
        /// </summary>
        public static HandBasis Compute(Vector3 wrist, Vector3 indexRoot, Vector3 middleRoot, Vector3 littleRoot, Vector3? thumb,
                                        Vector3 palmHint)
        {
            var b = new HandBasis { Wrist = wrist };
            var f = middleRoot - wrist;
            b.Length = f.magnitude;
            b.Fingers = b.Length > 1e-5f ? f / b.Length : Vector3.forward;
            var k = littleRoot - indexRoot;
            k -= b.Fingers * Vector3.Dot(k, b.Fingers);
            b.Across = k.sqrMagnitude > 1e-10f ? k.normalized : Vector3.Cross(b.Fingers, palmHint).normalized;
            var n = Vector3.Cross(b.Fingers, b.Across).normalized;
            var sign = 0f;
            if (thumb.HasValue)
            {
                var d = Vector3.Dot(thumb.Value - wrist, n);
                if (Mathf.Abs(d) > 0.004f * b.Scale) sign = Mathf.Sign(d);
            }
            if (sign == 0f) sign = Vector3.Dot(n, palmHint) >= 0f ? 1f : -1f;
            b.Palm = n * sign;
            return b;
        }

        /// <summary>
        /// Explicit frame for rigs without fingers (placeholder mannequin): fingers along <paramref name="fingers"/>, palm
        /// along <paramref name="palm"/>; <paramref name="isLeft"/> decides which side the little finger is on.
        /// </summary>
        public static HandBasis FromAxes(Vector3 wrist, Vector3 fingers, Vector3 palm, float length, bool isLeft)
        {
            var b = new HandBasis { Wrist = wrist, Length = length, Fingers = fingers.normalized };
            b.Palm = (palm - b.Fingers * Vector3.Dot(palm, b.Fingers)).normalized;
            // cross(palm, fingers) points to the thumb of a right hand and to the little finger of a left hand
            // (right arm along +X, palm -Y: cross = +Z = thumb; left arm along -X, palm -Y: cross = -Z = little finger).
            var c = Vector3.Cross(b.Palm, b.Fingers);
            b.Across = isLeft ? c : -c;
            return b;
        }

        /// <summary>
        /// Primary (pistol-grip) socket: forward along the extended index (barrel), up toward the thumb web (top of the
        /// weapon), centred in the curled fingers (TECH_PLAN §7.8 "weapon on a right-hand socket").
        /// </summary>
        public void PrimaryGripPose(out Vector3 position, out Quaternion rotation)
        {
            position = Wrist + Fingers * (0.78f * Length) + Palm * (0.028f * Scale);
            rotation = Quaternion.LookRotation(Fingers, -Across);
        }

        /// <summary>
        /// Support (handguard) socket: the handguard runs across the palm (little → index points to the muzzle), the
        /// weapon sits on the palm (up = palm normal).
        /// </summary>
        public void SupportGripPose(out Vector3 position, out Quaternion rotation)
        {
            position = Wrist + Fingers * (0.70f * Length) + Palm * (0.032f * Scale);
            rotation = Quaternion.LookRotation(-Across, Palm);
        }
    }
}
